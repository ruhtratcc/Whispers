// Executa o toque, o tempo de espera, as falas e as respostas.

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorGame
{
    [DefaultExecutionOrder(250), DisallowMultipleComponent]
    public sealed class WhistleSystem : MonoBehaviour
    {
        public const string ReserveBatteryId = "bd.whistle.reserve-battery";
        public const string HintsPreference = "Gameplay.WhistleHints";
        public GameSession game;
        [HideInInspector]
        public EquipmentController equipment;
        public WhistleConfig config;
        public Transform handVisual;
        public Transform helenaAnchor;
        public WhistleRoom[] rooms = Array.Empty<WhistleRoom>();
        public event Action<WhistleContext> Blown;
        public event Action Discovery;
        public const string LoreFlag = "whistle.lore_known", DiscoveredFlag = "whistle.discovered";
        const string ControlHint = "Q: pegar/guardar o apito. Clique para tocar o apito.";
        public bool IsSchool => gameObject.scene.name == "SchoolCEEP";
        public bool SupernaturalActive => IsSchool && state.loreKnown && state.discovered;

        public bool HasFlag(string id) => id == LoreFlag ? state.loreKnown : id == DiscoveredFlag ? state.discovered : game && game.narrative && game.narrative.HasSignal(id);
        public float VisualAge => pulseAge;
        public Vector3 PulseOrigin { get; private set; }
        public Camera Eye => eye;

        WhistleSense sense;
        bool loreCommentPending;
        AudioSource speechSource;
        AudioClip speechClip;
        public WhistleState State => state;
        public Inventory Inventory => game && game.interaction ? game.interaction.inventory : null;
        public string Chapter => game && game.narrative ? game.narrative.Progress.chapterId : "";
        public bool Equipped { get; private set; }
        public bool Busy => phase != Phase.Idle;

        public float EchoDelay
        {
            get
            {
                float delay = config ? config.echoDelay : 0;
                Vector3 p = eye ? eye.transform.position : transform.position;
                for (int i = 0; i < rooms.Length; i++)
                    if (rooms[i] != null && rooms[i].bounds.Contains(p))
                    {
                        delay = rooms[i].echoDelay;
                        break;
                    }

                return delay + EffectiveTension * .08f;
            }
        }

        public static bool HintsEnabled => PlayerPrefs.GetInt(HintsPreference, 1) == 1;

        public static void SetHintsEnabled(bool value)
        {
            PlayerPrefs.SetInt(HintsPreference, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static WhistleSystem For(GameSession session) => session ? session.GetComponent<WhistleSystem>() : null;
        public bool CanUse => !Returned && config && game && game.CanControl && !game.IsBlackingOut && game.narrative && game.narrative.IsInitialized && (!game.tutorials || !game.tutorials.BlocksWhistle) && BeforeDemolitionCampaign.Is(game.narrative.book) && !game.narrative.SequencePlaying && Inventory && Inventory.Contains(BeforeDemolitionCampaign.WhistleItemId) && (!road || !road.driver.Seated);

        float EffectiveTension => state.tension * (Chapter == BeforeDemolitionTwoVoicesFlow.ChapterId || Chapter == BeforeDemolitionMirrorFlow.ChapterId ? .1f : 1);

        enum Phase
        {
            Idle,
            Raising,
            AtMouth,
            Lowering
        }

        Phase phase;
        WhistleState state = new WhistleState();
        BeforeDemolitionRoadFlow road;
        BeforeDemolitionMirrorFlow mirrorFlow;
        public bool Returned => mirrorFlow && mirrorFlow.Data.apitoReturned;

        Camera eye;
        AudioSource primary;
        AudioClip primaryClip;
        float age, pressedFor, holdFor, holdTick, pulseAge, ownedAge, stuckAge, speechAge = 20;
        bool released, firstAttempt, pickupPending, hudHidden;
        int hintsGiven;
        float oldInteraction, oldBattery, oldStamina;
        bool oldReticle;
        float flickerAge, lampBaseline;
        bool flickering;
        Volume pulseVolume;
        VolumeProfile pulseProfile;
        readonly Vector3 restingHand = new Vector3(.14f, -.28f, .30f), mouthHand = new Vector3(.015f, -.11f, .13f);
        void Awake()
        {
            if (!game)
                game = GetComponentInParent<GameSession>();
            if (game)
            {
                road = game.GetComponent<BeforeDemolitionRoadFlow>();
                mirrorFlow = game.GetComponentInChildren<BeforeDemolitionMirrorFlow>(true);
                eye = game.player && game.player.view ? game.player.view.playerCamera : null;
            }

            state.owned = Inventory && Inventory.Contains(BeforeDemolitionCampaign.WhistleItemId);
            sense = GetComponent<WhistleSense>();
            if (IsSchool && !GetComponent<WhistleChildAnswers>())
                gameObject.AddComponent<WhistleChildAnswers>();
            if (handVisual)
                handVisual.gameObject.SetActive(false);
            if (IsSchool && game && game.atmosphere && game.atmosphere.volume)
            {
                var go = new GameObject("Apito — pulso visual discreto");
                go.transform.SetParent(transform, false);
                pulseVolume = go.AddComponent<Volume>();
                pulseVolume.isGlobal = true;
                pulseVolume.priority = game.atmosphere.volume.priority + 1;
                pulseVolume.weight = 0;
                pulseProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                var vignette = pulseProfile.Add<Vignette>(true);
                float baselineVignette = 0;
                if (game.atmosphere.volume.sharedProfile && game.atmosphere.volume.sharedProfile.TryGet<Vignette>(out var oldVignette))
                    baselineVignette = oldVignette.intensity.value;
                vignette.intensity.Override(Mathf.Min(.75f, baselineVignette + .025f));
                vignette.smoothness.Override(.55f);
                var colour = pulseProfile.Add<ColorAdjustments>(true);
                float baselineSaturation = 0;
                if (game.atmosphere.volume.sharedProfile && game.atmosphere.volume.sharedProfile.TryGet<ColorAdjustments>(out var oldGrade))
                    baselineSaturation = oldGrade.saturation.value;
                colour.saturation.Override(Mathf.Clamp(baselineSaturation + (config ? config.desaturation : -12), -100, 100));
                pulseVolume.sharedProfile = pulseProfile;
            }
        }

        void OnEnable()
        {
            if (Inventory)
                Inventory.Changed += InventoryChanged;
            if (game)
            {
                game.StateChanged += GameStateChanged;
                if (game.narrative)
                    game.narrative.SignalAccepted += Progressed;
            }
        }

        void InventoryChanged()
        {
            bool owned = Inventory && Inventory.Contains(BeforeDemolitionCampaign.WhistleItemId);
            if (owned && !state.owned && !state.pickupLinePlayed)
            {
                pickupPending = true;
                ownedAge = 0;
            }

            state.owned = owned;
            stuckAge = 0;
            hintsGiven = 0;
        }

        void Progressed(string id)
        {
            stuckAge = 0;
            hintsGiven = 0;
        }

        void GameStateChanged(GameState next)
        {
            if (next == GameState.Paused)
            {
                if (primary && primary.clip == primaryClip)
                    primary.Pause();
                for (int i = 0; i < WhistleResponder.Active.Count; i++)
                    WhistleResponder.Active[i].PauseSound(true);
                RestoreHud();
            }
            else if (next == GameState.Playing)
            {
                if (primary && primary.clip == primaryClip)
                    primary.UnPause();
                for (int i = 0; i < WhistleResponder.Active.Count; i++)
                    WhistleResponder.Active[i].PauseSound(false);
            }
            else
                Cancel();
        }

        void Update()
        {
            if (!game || !config)
                return;
            if (game.State == GameState.Paused)
                return;
            float dt = Time.deltaTime;
            state.Cool(dt, config);
            speechAge += dt;
            if (!CanUse)
            {
                if (Busy || Equipped)
                    Cancel();
                AudioManager.Instance?.SetWhistleBreathing(0);
                return;
            }

            if (pickupPending)
            {
                pickupPending = false;
                state.pickupLinePlayed = true;
                Speak("pickup", true);
            }

            ownedAge += dt;
            stuckAge += dt;
            if (loreCommentPending)
            {
                loreCommentPending = false;
                Speak("lore", true);
            }

            if (!equipment && IsSchool && state.loreKnown && !state.discovered && HintsEnabled && !state.inputHintShown)
            {
                state.inputHintShown = true;
                game.hud?.Notify(ControlHint);
                Save();
            }

            if (SupernaturalActive && HintsEnabled && stuckAge > 60 + hintsGiven * 50 && hintsGiven < 3)
            {
                Speak("hint." + (hintsGiven + 1));
                hintsGiven++;
            }

            if (!equipment && game.input && game.input.WhistleEquip)
                ToggleEquipped();
            if (game.input && (game.input.WhistlePressed || equipment && equipment.IsActive(EquipmentKind.Whistle) && equipment.CanSwitch && game.input.UseEquipmentPressed))
                Press();
            // Cada aperto produz um toque curto. Segurar o botão não repete nem prolonga o toque.
            Advance(dt);
            for (int i = 0; i < WhistleResponder.Active.Count; i++)
            {
                var r = WhistleResponder.Active[i];
                if (r && r.gameObject.scene == gameObject.scene)
                    r.Tick(dt, true);
            }

            TryReserveBattery();
        }

        public void ToggleEquipped()
        {
            if (equipment)
            {
                equipment.RequestSlot(2);
                return;
            }

            if (!CanUse)
                return;
            SetEquipped(!Equipped);
        }

        public void SetEquipped(bool value)
        {
            if (!value)
            {
                if (Equipped || Busy)
                    Cancel();
                return;
            }

            Equipped = true;
            if (handVisual)
            {
                handVisual.localPosition = restingHand;
                handVisual.gameObject.SetActive(true);
            }
        }

        public bool Press()
        {
            // Só começa um toque se o apito estiver equipado, livre e fora do tempo de espera.
            if (!CanUse || !Equipped || Busy || equipment && equipment.Switching || state.cooldownRemaining > 0)
                return false;
            firstAttempt = IsSchool && state.loreKnown && !state.schoolHesitated;
            phase = Phase.Raising;
            age = pressedFor = holdFor = holdTick = 0;
            released = false;
            if (firstAttempt)
                Speak("hesitate", true);
            return true;
        }

        public void Release()
        {
        } // Este método continua disponível para scripts antigos. O toque acontece ao apertar.

        public void Advance(float seconds)
        {
            if (!CanUse || !float.IsFinite(seconds) || seconds <= 0)
                return;
            if (phase == Phase.Idle)
                return;
            age += seconds;
            if (!released)
                pressedFor += seconds;
            if (phase == Phase.Raising)
            {
                float duration = firstAttempt ? 1.25f : .22f, t = Mathf.Clamp01(age / duration);
                float lift = firstAttempt ? (t < .4f ? t / .4f * .7f : t < .65f ? Mathf.Lerp(.7f, .18f, (t - .4f) / .25f) : Mathf.Lerp(.18f, 1, (t - .65f) / .35f)) : t;
                if (handVisual)
                    handVisual.localPosition = Vector3.Lerp(restingHand, mouthHand, lift);
                if (age < duration)
                    return;
                phase = Phase.AtMouth;
                age = 0;
            }

            // Depois de levantar a mão, toca o apito uma vez e começa a abaixar.
            if (phase == Phase.AtMouth)
            {
                Emit(WhistleKind.Tap);
                BeginLowering();
            }
            else if (phase == Phase.Lowering)
            {
                if (handVisual)
                    handVisual.localPosition = Vector3.Lerp(mouthHand, restingHand, Mathf.Clamp01(age / .2f));
                if (age >= .2f)
                {
                    phase = Phase.Idle;
                    RestoreHud();
                }
            }
        }

        void BeginLowering()
        {
            phase = Phase.Lowering;
            age = 0;
            state.cooldownRemaining = Mathf.Clamp(config.cooldown, .1f, 600);
            Save();
        }

        void Emit(WhistleKind kind)
        {
            bool discoveryNow = IsSchool && state.loreKnown && !state.discovered;
            state.totalBlows++;
            state.hesitated = true;
            state.owned = true;
            state.cooldownRemaining = Mathf.Clamp(config.cooldown, .1f, 600);
            if (kind == WhistleKind.Tap)
                state.learnedTap = true;
            else
                state.learnedHold = true;
            if (discoveryNow)
            {
                state.discovered = true;
                state.schoolHesitated = true;
                state.rareCommentRemaining = config.rareCommentCooldown;
            }

            if (SupernaturalActive)
            {
                float scale = Chapter == BeforeDemolitionTwoVoicesFlow.ChapterId || Chapter == BeforeDemolitionMirrorFlow.ChapterId ? .1f : 1;
                state.tension = Mathf.Min(Mathf.Min(100, config.stressMaximum), state.tension + config.stressPerBlow * scale);
            }

            primaryClip = kind == WhistleKind.Tap ? config.tapClip : config.holdClip;
            primary = AudioManager.Instance?.PlayClip(primaryClip, eye ? eye.transform.position : transform.position, kind == WhistleKind.Tap ? config.tapVolume : config.holdVolume, .99f + (state.totalBlows % 3) * .01f, AudioChannel.SFX, true);
            if (primary && kind == WhistleKind.Hold)
                primary.loop = true;
            game.hud?.ShowSubtitle(config.tapCaption, .8f);
            // Se o efeito sobrenatural estiver ativo, procura pistas e objetos que possam responder.
            stuckAge = 0;
            if (SupernaturalActive)
            {
                PulseOrigin = eye ? eye.transform.position : transform.position;
                pulseAge = config.effectDuration;
                sense?.Pulse(Context(kind, 0));
                Dispatch(kind, 0);
                if (discoveryNow)
                    Discovery?.Invoke();
            }
            else
            {
                if (!IsSchool)
                    state.triedOutside = true;
                if (!state.plainCommentPlayed)
                {
                    state.plainCommentPlayed = true;
                    var secretary = IsSchool ? GetComponent<SecretaryEvidence>() : null;
                    // Esses rastros aparecem em resposta ao primeiro toque na escola, antes do mural.
                    if (!secretary || !secretary.flow || !secretary.flow.IsActive)
                        Speak("plain", true);
                }
            }

            Blown?.Invoke(Context(kind, 0));
            if (game.hud && game.hud.notification && game.hud.notification.text == ControlHint)
                game.hud.notification.text = "";
            if (SupernaturalActive && EffectiveTension >= config.stressThreshold && state.rareCommentRemaining <= 0)
            {
                Speak("helena.overuse", true);
                state.rareCommentRemaining = config.rareCommentCooldown;
            }

            if (SupernaturalActive && EffectiveTension >= config.stressThreshold)
            {
                flickerAge = .8f;
                Speak("exerted");
            }

            Save();
        }

        void Dispatch(WhistleKind kind, float duration)
        {
            if (!SupernaturalActive)
                return;
            var context = Context(kind, duration);
            int heard = 0;
            for (int i = 0; i < WhistleResponder.Active.Count; i++)
            {
                var r = WhistleResponder.Active[i];
                if (!r || r.gameObject.scene != gameObject.scene || !r.CanHear(context))
                    continue;
                heard++;
                r.OnWhistleHeard(context);
                if (kind == WhistleKind.Tap)
                    r.OnWhistleTap(context);
                else
                    r.OnWhistleHold(context, duration);
            }

            if (kind == WhistleKind.Tap && heard == 0 && state.totalBlows % 3 == 0 && !GetComponent<WhistleChildAnswers>())
                Speak("nothing");
        }

        WhistleContext Context(WhistleKind kind, float duration)
        {
            string room = gameObject.scene.name;
            Vector3 p = eye ? eye.transform.position : transform.position;
            for (int i = 0; i < rooms.Length; i++)
                if (rooms[i] != null && rooms[i].bounds.Contains(p))
                {
                    room = rooms[i].name;
                    break;
                }

            return new WhistleContext(this, p, eye ? eye.transform.forward : transform.forward, kind, duration, state.totalBlows, gameObject.scene.name, room);
        }

        public bool BlowTap()
        {
            if (!CanUse || Busy || equipment && equipment.Switching || state.cooldownRemaining > 0)
                return false;
            Emit(WhistleKind.Tap);
            state.cooldownRemaining = Mathf.Clamp(config.cooldown, .1f, 600);
            return true;
        }

        public bool HasResponded(string id) => !string.IsNullOrEmpty(id) && state.responders.Contains(id);
        public void MarkResponded(string id)
        {
            if (!string.IsNullOrWhiteSpace(id) && !HasResponded(id))
            {
                state.responders.Add(id);
                Save();
            }
        }

        public void Response(WhistleResponder responder, WhistleContext context)
        {
            if (responder.role == WhistleResponseRole.CorridorLost)
            {
                Speak("lost");
                if (state.totalBlows % 3 == 0 && state.rareCommentRemaining <= 0)
                {
                    Speak("helena.corridor", true);
                    state.rareCommentRemaining = 75;
                }

                return;
            }

            if (responder.role == WhistleResponseRole.Glass && HasResponded(responder.responderId))
                return;
            string trigger = !string.IsNullOrEmpty(responder.dialogueTrigger) ? responder.dialogueTrigger : responder.role == WhistleResponseRole.CorridorCorrect ? "correct" : "response";
            if (!Speak(trigger, responder.role == WhistleResponseRole.OfficeCloset || responder.role == WhistleResponseRole.Glass) && speechAge > 2)
                game.hud?.ShowSubtitle(config.responseCaption, 1);
        }

        public void LearnLore()
        {
            if (!IsSchool || state.loreKnown)
                return;
            state.loreKnown = true;
            state.inputHintShown = false;
            loreCommentPending = true;
            game?.tutorials?.Discover("whistle");
            Save();
        }

        public bool SpeakAt(string trigger, Vector3 position) => Say(trigger, true, true, position);
        public bool Speak(string trigger, bool important = false) => Say(trigger, important, trigger == "helena.corridor" && helenaAnchor, helenaAnchor ? helenaAnchor.position : transform.position);
        bool Say(string trigger, bool important, bool spatial, Vector3 position)
        {
            if (!config || !config.dialogue || !game || !game.hud || game.narrative && game.narrative.SequencePlaying || !important && speechAge < 7)
                return false;
            if (!important && game.hud.SubtitleVisible && !(game.hud.subtitle.text.StartsWith("[") && !game.hud.subtitle.text.Contains("\n")))
                return false;
            var line = config.dialogue.Find(trigger, state.totalBlows);
            if (line == null || string.IsNullOrWhiteSpace(line.text))
                return false;
            var objective = game.GetComponent<ObjectiveService>();
            string caption = line.text.Replace("{objetivo}", objective ? objective.CurrentTitle : "continuar a investigação");
            game.hud.ShowSubtitle(line.speaker + ": " + caption, line.voice ? line.voice.length : line.seconds);
            speechAge = 0;
            if (speechSource && speechSource.clip == speechClip)
                speechSource.Stop();
            if (line.voice)
            {
                speechClip = line.voice;
                speechSource = AudioManager.Instance?.PlayClip(line.voice, spatial ? position : eye ? eye.transform.position : transform.position, spatial ? .16f : .6f, 1, AudioChannel.Voice, spatial);
                if (speechSource && spatial)
                {
                    speechSource.minDistance = 1;
                    speechSource.maxDistance = 20;
                }
            }

            return true;
        }

        void TryReserveBattery()
        {
            var flashlight = game.save ? game.save.flashlight : null;
            if (gameObject.scene.name != "SchoolCEEP" || state.reserveBatteryUsed || !Inventory.Contains(ReserveBatteryId) || !flashlight || !flashlight.IsAvailable || flashlight.Battery01 > .2f)
                return;
            if (!Inventory.Remove(ReserveBatteryId))
                return;
            state.reserveBatteryUsed = true;
            flashlight.Recharge(120);
            flashlight.Restore(flashlight.BatterySeconds, true);
            game.hud?.Notify("Usei a pilha de reserva do escritório.");
            Save();
        }

        void LateUpdate()
        {
            if (!game || !config)
                return;
            if (game.State != GameState.Paused)
                pulseAge = Mathf.Max(0, pulseAge - Time.deltaTime);
            if (pulseVolume)
                pulseVolume.weight = PresentationSettings.ReduceEffects || !SupernaturalActive ? 0 : VisualEnvelope;
            AudioManager.Instance?.SetWhistleBreathing(CanUse && SupernaturalActive ? Mathf.Clamp01((EffectiveTension - config.stressThreshold) / 2) : 0);
            var lamp = game.save && game.save.flashlight ? game.save.flashlight.lamp : null;
            if (lamp)
            {
                if (flickerAge > 0 && !PresentationSettings.ReduceEffects && game.save.flashlight.IsOn)
                {
                    if (!flickering)
                    {
                        lampBaseline = lamp.intensity;
                        flickering = true;
                    }

                    if (game.State != GameState.Paused)
                        flickerAge = Mathf.Max(0, flickerAge - Time.deltaTime);
                    lamp.intensity = lampBaseline * (.85f + .15f * (.5f + .5f * Mathf.Sin(flickerAge * Mathf.PI * 2)));
                }
                else if (flickering)
                {
                    lamp.intensity = lampBaseline;
                    flickering = false;
                }
            }

            if (Busy && game.State == GameState.Playing && game.hud)
            {
                if (!hudHidden)
                {
                    oldReticle = game.hud.crosshair && game.hud.crosshair.enabled;
                    oldInteraction = game.hud.interactionGroup ? game.hud.interactionGroup.alpha : 0;
                    oldBattery = game.hud.batteryGroup ? game.hud.batteryGroup.alpha : 0;
                    oldStamina = game.hud.staminaGroup ? game.hud.staminaGroup.alpha : 0;
                    hudHidden = true;
                }

                if (game.hud.crosshair)
                    game.hud.crosshair.enabled = false;
                if (game.hud.interactionGroup)
                    game.hud.interactionGroup.alpha = 0;
                if (game.hud.batteryGroup)
                    game.hud.batteryGroup.alpha = 0;
                if (game.hud.staminaGroup)
                    game.hud.staminaGroup.alpha = 0;
            }
        }

        public float VisualEnvelope => !config || pulseAge <= 0 ? 0 : Mathf.Clamp01((config.effectDuration - pulseAge) / .3f) * Mathf.Clamp01(pulseAge / .8f);

        void RestoreHud()
        {
            if (!hudHidden || !game || !game.hud)
                return;
            if (game.hud.crosshair)
                game.hud.crosshair.enabled = oldReticle;
            if (game.hud.interactionGroup)
                game.hud.interactionGroup.alpha = oldInteraction;
            if (game.hud.batteryGroup)
                game.hud.batteryGroup.alpha = oldBattery;
            if (game.hud.staminaGroup)
                game.hud.staminaGroup.alpha = oldStamina;
            hudHidden = false;
        }

        void Save()
        {
            if (game && game.narrative)
                game.narrative.ScheduleSave();
        }

        void StopPrimary()
        {
            if (primary && primary.clip == primaryClip)
            {
                primary.loop = false;
                primary.Stop();
            }

            primary = null;
            primaryClip = null;
        }

        public void Cancel()
        {
            sense?.Clear();
            if (speechSource && speechSource.clip == speechClip)
                speechSource.Stop();
            StopPrimary();
            phase = Phase.Idle;
            Equipped = false;
            pulseAge = flickerAge = 0;
            if (pulseVolume)
                pulseVolume.weight = 0;
            if (flickering && game && game.save && game.save.flashlight && game.save.flashlight.lamp)
                game.save.flashlight.lamp.intensity = lampBaseline;
            flickering = false;
            if (handVisual)
                handVisual.gameObject.SetActive(false);
            RestoreHud();
            for (int i = 0; i < WhistleResponder.Active.Count; i++)
                if (WhistleResponder.Active[i] && WhistleResponder.Active[i].gameObject.scene == gameObject.scene)
                    WhistleResponder.Active[i].Cancel();
            AudioManager.Instance?.SetWhistleBreathing(0);
        }

        public WhistleState Capture()
        {
            state.owned = Inventory && Inventory.Contains(BeforeDemolitionCampaign.WhistleItemId);
            return state.Copy();
        }

        public void Restore(WhistleState saved)
        {
            if (saved != null && !saved.Valid)
                throw new ArgumentException("Estado do apito inválido.");
            Cancel();
            GetComponent<WhistleChildAnswers>()?.Cancel();
            state = saved != null ? saved.Copy() : new WhistleState();
            state.owned = Inventory && Inventory.Contains(BeforeDemolitionCampaign.WhistleItemId);
            if (saved == null && state.owned)
                state.pickupLinePlayed = true;
            if (!state.loreKnown)
                state.inputHintShown = false;
            if ((saved == null || saved.version < 2) && IsSchool && !state.discovered && (Chapter == BeforeDemolitionFourStudentsFlow.ChapterId || Chapter == BeforeDemolitionTwoVoicesFlow.ChapterId || Chapter == BeforeDemolitionMirrorFlow.ChapterId))
            {
                state.loreKnown = state.discovered = state.schoolHesitated = true;
            }

            state.version = 2;
            loreCommentPending = false;
            pickupPending = false;
            ownedAge = stuckAge = 0;
            hintsGiven = 0;
        }

        void OnDisable()
        {
            Cancel();
            if (Inventory)
                Inventory.Changed -= InventoryChanged;
            if (game)
            {
                game.StateChanged -= GameStateChanged;
                if (game.narrative)
                    game.narrative.SignalAccepted -= Progressed;
            }
        }

        void OnDestroy()
        {
            if (pulseProfile)
                Destroy(pulseProfile);
        }
    }
}
