// Calcula a sanidade e determina o momento do blackout.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame
{
    /// <summary>Explicit school-only sanity, stored alongside existing persistent scene objects.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(300)]
    public sealed class SchoolSanityController : PersistentObject
    {
        public bool enabledForSchool;
        public string schoolSceneName = "SchoolCEEP";
        public GameSession game;
        public NarrativeDirector director;
        public FlashlightController flashlight;
        public HorrorAtmosphere atmosphere;
        public VintageCameraEffect cameraEffect;
        public SoundSet heartbeat;
        public SchoolSanityView presentation;
        public SchoolMemoryAnchor[] anchors = Array.Empty<SchoolMemoryAnchor>();
        public Light[] recoveryLights = Array.Empty<Light>();
        public LayerMask illuminationMask = ~0;
        [Range(0, 100)] public float sanity = 100;
        [Min(0)] public float darknessGraceSeconds = 20;
        [Min(0)] public float darknessLossPerSecond = .22f;
        [Tooltip("After the darkness grace period, an exhausted flashlight makes prolonged darkness lethal.")]
        [Min(0)] public float emptyBatteryLossPerSecond = 1.1f;
        [Min(0)] public float flashlightRecoveryPerSecond = .14f;
        [Min(0)] public float lightRecoveryPerSecond = .22f;
        [Min(.1f)] public float blackoutSeconds = 1.5f;
        [Range(1, 99)] public float blackoutRecoverySanity = 60;
        [Min(.1f)] public float wakeSeconds = .65f;

        [Serializable]
        sealed class Data
        {
            public int version = 1;
            public float sanity = 100, darknessSeconds;
            public List<string> feltSignals = new List<string>();
            public SaveData chapterStart;
            // Optional metadata expands v1 without invalidating existing school saves.
            public string chapterStartId;
            public float chapterStartSanity = 100, chapterStartDarknessSeconds;
            public List<string> chapterStartFeltSignals = new List<string>();
        }

        NarrativeDirector subscribedDirector;
        SaveData sceneTemplate, chapterStart;
        SaveData blackoutCheckpoint;
        readonly HashSet<string> feltSignals = new HashSet<string>();
        readonly HashSet<string> chapterStartFeltSignals = new HashSet<string>();
        float chapterStartSanity = 100, chapterStartDarknessSeconds;
        float darknessSeconds, blackoutAge, presentationClock, nextInterference, nextHeartbeat, heartbeatExpires;
        AudioSource heartbeatVoice;
        AudioClip heartbeatClip;
        float lampBaseline;
        bool lampChanged, blackoutStarted, waking, checkpointRestored, darknessDeath;
        AudioLowPassFilter blackoutFilter;
        bool ownsBlackoutFilter, filterWasEnabled;
        float filterCutoff;
        int muffleStartFrame;
        public bool IsSchoolActive => enabledForSchool && gameObject.scene.name == schoolSceneName
            && director && director.IsInitialized
            && (BeforeDemolitionCampaign.Is(director.book) && BeforeDemolitionCampaign.IsSchoolChapter(director.Progress.chapterId)
                || director.Progress.chapterId == "school" || director.Progress.chapterId == "left-behind"
                || director.Progress.chapterId == "memories" || director.Progress.chapterId == "terrace"
                || director.Progress.chapterId == "last-exit");
        public bool BlackingOut => blackoutStarted;
        public bool HasChapterStart => chapterStart != null;
        public float Sanity01 => Mathf.Clamp01(sanity / 100);
        public float DarknessSeconds => darknessSeconds;
        public float DarknessDanger01 => flashlight && flashlight.BatterySeconds <= 0
            ? Mathf.Clamp01(Mathf.Max(0, darknessSeconds - darknessGraceSeconds)
                * Mathf.Max(0, Mathf.Max(darknessLossPerSecond, emptyBatteryLossPerSecond)) / 100) : 0;
        public float Blackout01 => !blackoutStarted ? 0 : waking
            ? 1 - Mathf.Clamp01(blackoutAge / Mathf.Max(.1f, wakeSeconds))
            : Mathf.Clamp01(blackoutAge / Mathf.Max(.1f, blackoutSeconds));
        bool CanProgress => IsSchoolActive && game && game.CanControl && !director.SequencePlaying
            && (BeforeDemolitionCampaign.Is(director.book) || director.HasSignal("school.entered")) && !director.Progress.IsChapterComplete(director.book);

        void OnEnable()
        {
            if (!game) game = GameSession.Instance;
            if (!director && game) director = game.narrative;
            if (!atmosphere && game) atmosphere = game.atmosphere;
            if (!flashlight && game && game.save) flashlight = game.save.flashlight;
            sceneTemplate = CaptureSceneTemplate();
            subscribedDirector = director;
            if (subscribedDirector)
            {
                subscribedDirector.ChapterInitialized += ChapterInitialized;
                subscribedDirector.SignalAccepted += ReactToSignal;
                if (subscribedDirector.IsInitialized) CaptureChapterStart();
            }
        }

        void Update() => AdvanceSanity(Time.deltaTime, Time.timeScale <= 0);

        /// <summary>Gameplay time only. Readers, authored captions, pause and chapter cards suspend exposure.</summary>
        public void AdvanceSanity(float deltaTime, bool paused)
        {
            if (!game || paused || !Finite(deltaTime) || deltaTime <= 0
                || game.State == GameState.Paused || game.State == GameState.GameOver || game.State == GameState.StoryScreen) return;
            if (blackoutStarted)
            {
                blackoutAge += deltaTime;
                if (waking && blackoutAge >= Mathf.Max(.1f, wakeSeconds)) FinishBlackout();
                else if (!waking && blackoutAge >= Mathf.Max(.1f, blackoutSeconds))
                {
                    if (darknessDeath)
                    {
                        blackoutStarted = false;
                        RestoreMuffle();
                        game.CompleteDarknessDeath();
                    }
                    else RecoverCheckpoint();
                }
                return;
            }
            if (!IsSchoolActive || !CanProgress) return;
            if (sanity <= 0) { BeginBlackout(); return; }
            presentationClock += deltaTime;
            float recovery = RecoveryAtPlayer();
            bool lethalExposure = false;
            if (recovery > 0)
            {
                darknessSeconds = Mathf.Max(0, darknessSeconds - deltaTime * 2);
                sanity = Mathf.Min(100, sanity + recovery * deltaTime);
            }
            else
            {
                float before = Mathf.Max(0, darknessSeconds - darknessGraceSeconds);
                darknessSeconds += deltaTime;
                float exposed = Mathf.Max(0, darknessSeconds - darknessGraceSeconds) - before;
                lethalExposure = flashlight && flashlight.BatterySeconds <= 0 && exposed > 0;
                float loss = lethalExposure ? Mathf.Max(darknessLossPerSecond, emptyBatteryLossPerSecond) : darknessLossPerSecond;
                if (!lethalExposure && flashlight && flashlight.IsAvailable && !flashlight.IsOn
                    && game.player && game.player.HorizontalSpeed > .35f)
                    loss += .55f;
                sanity = Mathf.Max(0, sanity - exposed * Mathf.Max(0, loss));
            }
            if (sanity < 40 && presentationClock >= nextInterference)
            {
                cameraEffect?.PulseInterference(Mathf.Lerp(.08f, .22f, 1 - sanity / 40), .10f);
                nextInterference = presentationClock + 9;
            }
            if (sanity < 35 && heartbeat && presentationClock >= nextHeartbeat
                && (!heartbeatVoice || !heartbeatVoice.isPlaying || heartbeatVoice.clip != heartbeatClip || presentationClock >= heartbeatExpires))
            {
                heartbeatVoice = AudioManager.Instance?.Play(heartbeat, game.player ? game.player.transform.position : transform.position,
                    Mathf.Lerp(.10f, .18f, 1 - sanity / 35), AudioChannel.SFX, false);
                heartbeatClip = heartbeatVoice ? heartbeatVoice.clip : null;
                heartbeatExpires = presentationClock + (heartbeatClip ? heartbeatClip.length / Mathf.Max(.1f, heartbeatVoice.pitch) : 0);
                nextHeartbeat = presentationClock + 1.2f;
            }
            if (sanity <= 0) BeginBlackout(lethalDarkness: lethalExposure);
        }

        float RecoveryAtPlayer()
        {
            float recovery = flashlight && flashlight.IsOn ? Mathf.Max(0, flashlightRecoveryPerSecond) : 0;
            if (!game.player) return recovery;
            Vector3 position = game.player.transform.position;
            foreach (var anchor in anchors)
                if (anchor && anchor.Contains(position)) recovery = Mathf.Max(recovery, anchor.recoveryPerSecond);
            Vector3 eye = game.player.view && game.player.view.playerCamera
                ? game.player.view.playerCamera.transform.position : position + Vector3.up * 1.5f;
            foreach (var light in recoveryLights)
            {
                if (!light || !light.isActiveAndEnabled || light.intensity <= .01f
                    || light.type == LightType.Directional || (flashlight && light == flashlight.lamp)) continue;
                Vector3 delta = light.transform.position - eye;
                float distance = delta.magnitude;
                float radius = Mathf.Min(7, light.range * .65f);
                if (distance > radius || radius <= 0) continue;
                if (light.type == LightType.Spot && Vector3.Angle(light.transform.forward, -delta) > light.spotAngle * .5f) continue;
                if (distance > .2f && Physics.Raycast(eye, delta / distance, distance - .15f, illuminationMask, QueryTriggerInteraction.Ignore)) continue;
                recovery = Mathf.Max(recovery, Mathf.Max(0, lightRecoveryPerSecond));
            }
            return recovery;
        }

        public void ReactToSignal(string signal)
        {
            if (!IsSchoolActive || !game || game.State == GameState.Paused || game.State == GameState.GameOver
                || game.State == GameState.StoryScreen || blackoutStarted) return;
            float loss;
            switch (signal)
            {
                case "library.newspaper": loss = 3; break;
                case "library.photo": loss = 4; break;
                case "hospicio.register": loss = 4; break;
                case "hospicio.director-tape": loss = 4; break;
                case "memories.truth": loss = 10; break;
                case "school.photograph": loss = 4; break;
                case "school.blackboard-seen": loss = 9; break;
                case "school.ghost-approached": loss = 12; break;
                case "school.drawing": loss = 5; break;
                case "left-behind.documents-found": loss = 6; break;
                case "left-behind.photograph": loss = 10; break;
                case "left-behind.demon-appeared": loss = 18; break;
                // Saved legacy ID: this is a stationary presence, never a chase.
                case "left-behind.chase-started": loss = 8; break;
                case "left-behind.wall-message": loss = 6; break;
                case "memories.past-echoes": loss = 5; break;
                case "memories.recording-one": loss = 5; break;
                case "memories.recording-two": loss = 7; break;
                case "memories.childhood-recognized": loss = 15; break;
                case "memories.ghost-appeared": loss = 4; break;
                case "terrace.voices-finished": loss = 4; break;
                case "terrace.cycle-revelation": loss = 12; break;
                // Legacy ID: the environment distorts; nothing pursues or attacks the player.
                case "terrace.final-chase": loss = 4; break;
                case "last-exit.phone-ringing": loss = 3; break;
                case "last-exit.final-call": loss = 4; break;
                case "last-exit.last-window": loss = 3; break;
                default: return;
            }
            if (!feltSignals.Add(signal)) return;
            sanity = Mathf.Max(0, sanity - loss);
        }

        /// <summary>A small environmental disturbance, eligible only during active school exploration.</summary>
        public void Disturb(float points)
        {
            if (!CanProgress || blackoutStarted || !Finite(points) || points <= 0) return;
            sanity = Mathf.Max(0, sanity - points);
        }

        public void ApplyInspectionStress(float points)
        {
            if (!IsSchoolActive || !game || game.State != GameState.Inspecting
                || blackoutStarted || !Finite(points) || points <= 0) return;
            sanity = Mathf.Max(0, sanity - points);
            if (sanity <= 0) BeginBlackout(true);
        }

        void BeginBlackout(bool inspection = false, bool lethalDarkness = false)
        {
            if (blackoutStarted || (!CanProgress && !(inspection && game && game.State == GameState.Inspecting))) return;
            blackoutCheckpoint = game.save && game.save.TryReadCurrent(out var saved) ? saved : null;
            blackoutStarted = true; blackoutAge = 0;
            darknessDeath = lethalDarkness && !BeforeDemolitionCampaign.Is(director.book);
            waking = checkpointRestored = false;
            muffleStartFrame = Time.frameCount;
            StopHeartbeat();
            game.BeginBlackout(darknessDeath ? "A escuridão tomou conta." : "Voz infantil: Você nunca deveria ter voltado.");
        }

        void RecoverCheckpoint()
        {
            checkpointRestored = game.save && blackoutCheckpoint != null
                && game.save.RestoreSnapshot(blackoutCheckpoint, sceneTemplate?.objects);
            if (checkpointRestored && blackoutCheckpoint.objects?.Exists(item => item != null && item.id == persistentId) != true)
                feltSignals.Clear();
            // A missing/corrupt save must never erase progress or send João to the prologue.
            if (!checkpointRestored) game.hud?.Notify("Não foi possível voltar ao último ponto salvo. O progresso atual foi mantido.");
            sanity = Finite(blackoutRecoverySanity) ? Mathf.Clamp(blackoutRecoverySanity, 1, 99) : 60;
            darknessSeconds = 0;
            foreach (var perception in FindObjectsByType<SchoolPerceptionEvents>(FindObjectsInactive.Include))
                if (perception.isActiveAndEnabled && perception.gameObject.scene == gameObject.scene) perception.ApplyBlackoutConsequence();
            blackoutStarted = waking = true; blackoutAge = 0;
            game.hud?.ShowSubtitle("Voz infantil: Você nunca deveria ter voltado.", Mathf.Max(.1f, wakeSeconds));
            if (presentation) presentation.Present(sanity, 1);
            else if (game.hud && game.hud.deathCurtain) game.hud.deathCurtain.alpha = 1;
        }

        void FinishBlackout()
        {
            blackoutStarted = waking = false; blackoutAge = 0; blackoutCheckpoint = null;
            RestoreMuffle();
            if (presentation) presentation.Clear();
            if (game.hud && game.hud.deathCurtain) game.hud.deathCurtain.alpha = 0;
            game.EndBlackout();
            director.InitializeAfterSession(true);
            if (checkpointRestored && game.save && !game.save.Save())
                game.hud?.Notify("Não foi possível salvar a recuperação. Checkpoint anterior mantido.");
            checkpointRestored = false;
        }

        void PresentMuffle()
        {
            // Inspection destroys its temporary listener filter at frame end; acquire ours afterwards.
            if (!blackoutStarted || Time.frameCount <= muffleStartFrame || !game.player || !game.player.view || !game.player.view.playerCamera) return;
            if (!blackoutFilter)
            {
                var camera = game.player.view.playerCamera;
                if (!camera.GetComponent<AudioListener>()) return;
                blackoutFilter = camera.GetComponent<AudioLowPassFilter>();
                ownsBlackoutFilter = !blackoutFilter;
                if (!blackoutFilter) blackoutFilter = camera.gameObject.AddComponent<AudioLowPassFilter>();
                if (!blackoutFilter) return;
                filterWasEnabled = blackoutFilter.enabled; filterCutoff = blackoutFilter.cutoffFrequency;
            }
            blackoutFilter.enabled = true;
            blackoutFilter.cutoffFrequency = Mathf.Lerp(filterWasEnabled ? filterCutoff : 22000, 650, Blackout01);
        }

        void RestoreMuffle()
        {
            if (blackoutFilter)
            {
                if (ownsBlackoutFilter) Destroy(blackoutFilter);
                else { blackoutFilter.cutoffFrequency = filterCutoff; blackoutFilter.enabled = filterWasEnabled; }
            }
            blackoutFilter = null;
        }

        void LateUpdate()
        {
            bool active = (IsSchoolActive || blackoutStarted) && game && game.State != GameState.StoryScreen;
            PresentMuffle();
            float danger = active && game.State != GameState.GameOver
                ? Mathf.Max(DarknessDanger01, flashlight && flashlight.BatterySeconds <= 0 && darknessSeconds > darknessGraceSeconds
                    ? (1 - Sanity01) * .85f : 0) : 0;
            cameraEffect?.SetDarknessDistortion(danger);
            if (IsSchoolActive && atmosphere) atmosphere.SetTension(active ? Mathf.Clamp01((60 - sanity) / 60) : 0);
            if (!CanProgress || sanity >= 35) StopHeartbeat();
            if (presentation && game && game.State != GameState.GameOver) presentation.Present(active ? sanity : 100,
                active && game.State != GameState.GameOver ? Blackout01 : 0);
            else if (game && game.hud && game.hud.deathCurtain && game.State != GameState.GameOver)
                game.hud.deathCurtain.alpha = active ? Blackout01 : 0;
            if (!active || game.State != GameState.Playing || director.SequencePlaying || !flashlight
                || !flashlight.isActiveAndEnabled || !flashlight.lamp || !flashlight.IsOn || sanity >= 30)
            { RestoreLamp(); return; }
            // FlashlightController reapplies its battery-aware baseline in Update before this scene effect.
            lampBaseline = flashlight.lamp.intensity;
            flashlight.lamp.intensity = lampBaseline * (1 - (1 - sanity / 30) * .08f
                * (.5f + .5f * Mathf.Sin(presentationClock * 2.1f)));
            lampChanged = true;
        }

        void RestoreLamp()
        {
            if (lampChanged && flashlight && flashlight.lamp) flashlight.lamp.intensity = lampBaseline;
            lampChanged = false;
        }

        void StopHeartbeat()
        {
            // Only stop this still-owned pooled voice, never another sound that reused its source.
            if (heartbeatVoice && heartbeatVoice.isPlaying && heartbeatVoice.clip == heartbeatClip
                && presentationClock < heartbeatExpires) heartbeatVoice.Stop();
            heartbeatVoice = null; heartbeatClip = null;
        }

        SaveData CaptureSceneTemplate()
        {
            if (!enabledForSchool || gameObject.scene.name != schoolSceneName || !game || !game.player) return null;
            var template = new SaveData
            {
                scene = gameObject.scene.name, position = game.player.transform.position,
                heading = game.player.transform.eulerAngles.y, pitch = game.player.Pitch,
                stamina = game.player.Stamina, crouched = game.player.IsCrouched
            };
            var ids = new HashSet<string>();
            foreach (var item in FindObjectsByType<PersistentObject>(FindObjectsInactive.Include))
            {
                if (item == this || item.gameObject.scene != gameObject.scene) continue;
                if (string.IsNullOrWhiteSpace(item.persistentId) || !ids.Add(item.persistentId)) return null;
                template.objects.Add(new SavedObject { id = item.persistentId, json = item.CaptureState() });
            }
            return template;
        }

        /// <summary>Initialized fires after checkpoint/carry restoration, so incoming equipment is captured correctly.</summary>
        void ChapterInitialized(string chapterId) => CaptureChapterStart();
        public void CaptureChapterStart()
        {
            if (!IsSchoolActive || !game) return;
            string chapterId = director.Progress.chapterId;
            if (chapterStart != null && chapterStart.narrative?.chapterId == chapterId) return;
            bool opening = chapterId == "school";
            // First school entry uses the authored fresh scene; an in-place continuation
            // must keep its actual doors/items/position instead of the original spawn.
            chapterStart = Clone(opening ? sceneTemplate ?? CaptureSceneTemplate() : CaptureSceneTemplate());
            if (chapterStart == null) return;
            chapterStart.inventory = CloneInventory(director.inventory ? director.inventory.items : null);
            // Legacy school checkpoints have no baseline; preserve incoming gear while removing this chapter's clues.
            if (opening) chapterStart.inventory.RemoveAll(item => item != null && (item.id == "school_documents"
                || item.id == "school_photograph" || item.id == "school_drawing"));
            chapterStart.battery = flashlight ? flashlight.BatterySeconds : 0;
            chapterStart.flashlightOn = flashlight && flashlight.IsOn;
            chapterStart.tension = atmosphere ? atmosphere.Tension : 0;
            chapterStart.narrative = AtChapterStart(director.CaptureProgress());
            chapterStartSanity = opening ? 100 : sanity;
            chapterStartDarknessSeconds = opening ? 0 : darknessSeconds;
            chapterStartFeltSignals.Clear();
            if (!opening) chapterStartFeltSignals.UnionWith(feltSignals);
        }

        NarrativeProgress AtChapterStart(NarrativeProgress progress)
        {
            progress = progress != null ? progress.Copy() : new NarrativeProgress();
            string chapterId = director.Progress.chapterId;
            progress.chapterId = chapterId; progress.pendingChapterId = ""; progress.endingSeen = false;
            var chapter = director.book ? director.book.Chapter(chapterId) : null;
            var signals = new HashSet<string>();
            if (chapter != null)
                foreach (var milestone in chapter.milestones) if (milestone != null) signals.Add(milestone.id);
            string prefix = chapterId + ".";
            progress.milestones?.RemoveAll(id => signals.Contains(id) || (!string.IsNullOrEmpty(id) && id.StartsWith(prefix, StringComparison.Ordinal)));
            progress.sequences?.RemoveAll(id => !string.IsNullOrEmpty(id) && id.StartsWith(prefix, StringComparison.Ordinal));
            progress.completedChapters?.RemoveAll(id => id == chapterId);
            return progress;
        }

        /// <summary>Pure snapshot construction for verification; no write or scene change occurs here.</summary>
        public bool PrepareChapterRestart(out SaveData snapshot)
        {
            snapshot = null;
            if (!IsSchoolActive || chapterStart == null || chapterStart.narrative?.chapterId != director.Progress.chapterId
                || string.IsNullOrWhiteSpace(persistentId)) return false;
            snapshot = Clone(chapterStart);
            if (snapshot.objects == null) snapshot.objects = new List<SavedObject>();
            snapshot.objects.RemoveAll(item => item == null || item.id == persistentId);
            var state = new Data { sanity = chapterStartSanity, darknessSeconds = chapterStartDarknessSeconds,
                feltSignals = new List<string>(chapterStartFeltSignals), chapterStart = Clone(chapterStart),
                chapterStartId = chapterStart.narrative.chapterId, chapterStartSanity = chapterStartSanity,
                chapterStartDarknessSeconds = chapterStartDarknessSeconds,
                chapterStartFeltSignals = new List<string>(chapterStartFeltSignals) };
            snapshot.objects.Add(new SavedObject { id = persistentId, json = JsonUtility.ToJson(state) });
            return true;
        }

        /// <summary>Retry a lethal darkness checkpoint without carrying its nearly fatal exposure forward.</summary>
        public SaveData PrepareDarknessRetrySnapshot(SaveData checkpoint)
        {
            if (checkpoint == null || checkpoint.scene != gameObject.scene.name || string.IsNullOrWhiteSpace(persistentId)) return null;
            var snapshot = Clone(checkpoint);
            if (snapshot.objects == null) snapshot.objects = new List<SavedObject>();
            var own = snapshot.objects.Find(item => item != null && item.id == persistentId);
            Data state;
            try { state = own != null ? JsonUtility.FromJson<Data>(own.json) : new Data { sanity = 60 }; }
            catch (ArgumentException) { return null; }
            if (state == null || state.version != 1 || !Finite(state.sanity) || !Finite(state.darknessSeconds)) return null;
            float recovery = Finite(blackoutRecoverySanity) ? Mathf.Clamp(blackoutRecoverySanity, 60, 100) : 60;
            state.sanity = Mathf.Clamp(Mathf.Max(recovery, state.sanity), 60, 100);
            state.darknessSeconds = 0;
            if (own == null)
            {
                own = new SavedObject { id = persistentId };
                snapshot.objects.Add(own);
            }
            own.json = JsonUtility.ToJson(state);
            return snapshot;
        }

        public override string CaptureState() => JsonUtility.ToJson(new Data
        {
            sanity = sanity, darknessSeconds = darknessSeconds,
            feltSignals = new List<string>(feltSignals), chapterStart = Clone(chapterStart),
            chapterStartId = chapterStart?.narrative?.chapterId, chapterStartSanity = chapterStartSanity,
            chapterStartDarknessSeconds = chapterStartDarknessSeconds,
            chapterStartFeltSignals = new List<string>(chapterStartFeltSignals)
        });

        public override void RestoreState(string json)
        {
            var data = JsonUtility.FromJson<Data>(json);
            if (data == null || data.version != 1 || !Finite(data.sanity) || !Finite(data.darknessSeconds)) return;
            sanity = Mathf.Clamp(data.sanity, 0, 100); darknessSeconds = Mathf.Max(0, data.darknessSeconds);
            feltSignals.Clear();
            foreach (string id in data.feltSignals ?? new List<string>()) if (!string.IsNullOrEmpty(id)) feltSignals.Add(id);
            chapterStart = data.chapterStart != null && data.chapterStart.scene == gameObject.scene.name
                ? Clone(data.chapterStart) : null;
            bool hasBaselineState = chapterStart != null && !string.IsNullOrEmpty(data.chapterStartId)
                && data.chapterStartId == chapterStart.narrative?.chapterId
                && Finite(data.chapterStartSanity) && Finite(data.chapterStartDarknessSeconds);
            // Old v1 snapshots deliberately restarted the opening at full sanity.
            chapterStartSanity = hasBaselineState ? Mathf.Clamp(data.chapterStartSanity, 0, 100) : 100;
            chapterStartDarknessSeconds = hasBaselineState ? Mathf.Max(0, data.chapterStartDarknessSeconds) : 0;
            chapterStartFeltSignals.Clear();
            if (hasBaselineState) chapterStartFeltSignals.UnionWith(data.chapterStartFeltSignals ?? new List<string>());
            blackoutStarted = waking = darknessDeath = false; blackoutAge = 0;
            cameraEffect?.SetDarknessDistortion(0);
            RestoreLamp();
            StopHeartbeat();
        }

        static SaveData Clone(SaveData value) => value == null ? null : JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(value));
        static List<InventoryItem> CloneInventory(List<InventoryItem> value)
        {
            var container = new SaveData { inventory = value ?? new List<InventoryItem>() };
            return Clone(container).inventory;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        void OnDisable()
        {
            if (subscribedDirector)
            {
                subscribedDirector.ChapterInitialized -= ChapterInitialized;
                subscribedDirector.SignalAccepted -= ReactToSignal;
            }
            subscribedDirector = null;
            cameraEffect?.SetDarknessDistortion(0);
            RestoreLamp();
            StopHeartbeat();
            if (enabledForSchool && gameObject.scene.name == schoolSceneName) atmosphere?.SetTension(0);
            RestoreMuffle();
            if (blackoutStarted && game) game.EndBlackout();
            if (presentation) presentation.Clear();
            if (game && game.hud && game.hud.deathCurtain && game.State != GameState.GameOver) game.hud.deathCurtain.alpha = 0;
        }
    }
}
