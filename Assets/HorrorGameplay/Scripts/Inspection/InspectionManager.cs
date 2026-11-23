// Abre e fecha a inspeção, gerencia o item atual e confirma ações de coleta.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HorrorGame
{
    [DisallowMultipleComponent, DefaultExecutionOrder(410)]
    public sealed class InspectionManager : MonoBehaviour
    {
        public static InspectionManager Instance { get; private set; }

        public GameSession game;
        public SchoolInspectionCatalog catalog;
        public bool IsOpen { get; private set; }
        public bool IsCompleting { get; private set; }
        public InspectionItemData CurrentItem { get; private set; }
        public RenderTexture Texture => stage ? stage.Texture : null;
        public bool BackVisible => stage && stage.BackVisible;
        public bool DetailVisible => stage && stage.DetailVisible;
        public bool FullscreenZoom => stage && stage.FullscreenZoom;
        public float PresentationZoom01 => stage ? stage.ZoomPresentation01 : 0;
        public int InspectionCount { get; private set; }
        public bool RecordingPlaying => IsOpen && recordingRequested && (captionRecording || sound && sound.isPlaying);
        public bool InspectingRecorder => CurrentItem && CurrentItem.itemId == "hospicio_recorder";
        public string RecordingText => recordingItem ? recordingItem.recordingText : "";

        public string RecordingCaption
        {
            get
            {
                if (!RecordingPlaying || captionTrack == null)
                    return "";
                float time = captionRecording ? captionRecordingAge : sound.timeSamples / (float)requestedRecording.frequency;
                for (int i = captionTrack.Length - 1; i >= 0; i--)
                {
                    var cue = captionTrack[i];
                    if (time >= cue.start)
                        return time < cue.end ? cue.text : "";
                }

                return "";
            }
        }

        public string PlayingRecordingId => recordingRequested && recordingItem ? recordingItem.itemId : "";

        public event Action<string, int> PageViewed;
        internal void ReportPageViewed(int index)
        {
            if (IsOpen && CurrentItem && CurrentItem.pages != null && index >= 0 && index < CurrentItem.pages.Length)
                PageViewed?.Invoke(CurrentItem.itemId, index);
        }

        public event Action<string> RecordingStarted;
        public event Action<string> RecordingCompleted;
        public float Alpha { get; private set; }
        public InspectionCameraController CameraController => stage;

        InspectionCameraController stage;
        InspectionUI ui;
        SchoolSanityController sanity;
        InspectableItem source;
        PlayerInteraction interactor;
        InputActionMap controls;
        InputAction drag, rotate, zoom, flip, close, escape, play, read;
        Renderer[] reservedRenderers;
        bool[] rendererStates;
        readonly List<(Light light, int mask)> worldLights = new List<(Light, int)>();
        FlashlightController flashlight;
        bool flashlightEnabled, lampEnabled, handVisible;
        int cameraMask, openedFrame, generation;
        bool explicitExamination, playOnOpen;
        string archiveRecordId;
        bool closing, restoring, fromNotebook, lowPassOwned, backgroundReserved, recordingRequested, returningFromZoom;
        float stressApplied, whisperAt;
        string chapter;
        AudioSource sound;
        AudioClip requestedRecording;
        RecordingCaptionCue[] captionTrack;
        InspectionItemData recordingItem;
        VoiceAudioPresentation recordingPresentation;
        bool recordingHeard;
        bool captionRecording;
        float captionRecordingAge;
        AudioLowPassFilter lowPass;
        bool lowPassEnabled;
        float lowPassCutoff;
        Volume blurVolume;
        VolumeProfile blurProfile;
        UniversalAdditionalCameraData cameraData;
        CameraOverrideOption depthOption;
        bool postProcessing;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Instance = null;
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallStartingScene() => Install(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        static void Install(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "SchoolCEEP" && scene.name != "Chapter1Office")
                return;
            foreach (var root in scene.GetRootGameObjects())
            {
                var session = root.GetComponentInChildren<GameSession>(true);
                if (session && !session.GetComponent<InspectionManager>())
                    session.gameObject.AddComponent<InspectionManager>();
            }
        }

        void Awake()
        {
            using (LoadingMetrics.Measure("Inspection.Awake"))
            {
                if (Instance && Instance != this)
                {
                    Destroy(this);
                    return;
                }

                Instance = this;
                if (!game)
                    game = GetComponent<GameSession>();
                if (!catalog)
                    catalog = Resources.Load<SchoolInspectionCatalog>("SchoolInspectionItems");
            }
        }

        IEnumerator Start()
        {
            yield return null;
            if (!game || !catalog || LayerMask.NameToLayer("Inspect") < 0)
            {
                Debug.LogError("Inspection requires a school catalog, GameSession and Inspect layer.", this);
                enabled = false;
                yield break;
            }

            stage = gameObject.AddComponent<InspectionCameraController>();
            var panel = new GameObject("InspectionUI", typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            ui = panel.AddComponent<InspectionUI>();
            ui.Initialize(this, game.hud.noteBody.font);
            sanity = GetComponent<SchoolSanityController>();
            if (!sanity)
                sanity = FindAnyObjectByType<SchoolSanityController>();
            var emitter = new GameObject("Inspection audio");
            emitter.transform.SetParent(transform, false);
            sound = emitter.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0;
            recordingPresentation = emitter.AddComponent<VoiceAudioPresentation>();
            sound.bypassListenerEffects = true;
            if (AudioManager.Instance)
                sound.outputAudioMixerGroup = AudioManager.Instance.sfxGroup;
            controls = new InputActionMap("Inspection");
            drag = controls.AddAction("Drag", InputActionType.Button, "<Mouse>/leftButton");
            rotate = controls.AddAction("Rotate", InputActionType.Value, "<Mouse>/delta");
            zoom = controls.AddAction("Zoom", InputActionType.Value, "<Mouse>/scroll/y");
            flip = controls.AddAction("Flip", InputActionType.Button, "<Keyboard>/f");
            close = controls.AddAction("Close", InputActionType.Button, "<Keyboard>/e");
            escape = controls.AddAction("Back", InputActionType.Button, "<Keyboard>/escape");
            play = controls.AddAction("Play", InputActionType.Button, "<Keyboard>/r");
            read = controls.AddAction("Read", InputActionType.Button, "<Keyboard>/t");
            game.StateChanged += StateChanged;
            if (game.narrative)
                game.narrative.ChapterInitialized += ChapterChanged;
            BindSchoolItems();
        }

        void BindSchoolItems()
        {
            foreach (var clue in FindObjectsByType<SchoolOpeningClue>(FindObjectsInactive.Include))
                Bind(clue, clue.kind == SchoolOpeningClueKind.Documents ? "school_documents" : clue.kind == SchoolOpeningClueKind.Photograph ? "school_photograph" : "school_drawing", clue.visual);
            foreach (var clue in FindObjectsByType<SchoolLeftBehindClue>(FindObjectsInactive.Include))
                Bind(clue, clue.kind == SchoolLeftBehindClueKind.Report ? "old_disappearance_report" : clue.kind == SchoolLeftBehindClueKind.Photograph ? "old_student_photo" : "old_key", clue.visual);
            foreach (var clue in FindObjectsByType<SchoolMemoriesClue>(FindObjectsInactive.Include))
                Bind(clue, clue.kind == SchoolMemoriesClueKind.RecordingOne ? "memory_recording_one" : clue.kind == SchoolMemoriesClueKind.RecordingTwo ? "memory_recording_two" : "memories_group_photo", clue.visual);
        }

        void Bind(MonoBehaviour original, string id, GameObject visual)
        {
            var data = catalog.Find(id);
            if (!data || !data.model)
                return;
            var item = original.GetComponent<InspectableItem>();
            if (!item)
                item = original.gameObject.AddComponent<InspectableItem>();
            item.item = data;
            item.originalInteraction = original;
            item.visual = visual;
        }

        public static bool TryInspect(MonoBehaviour original, PlayerInteraction player)
        {
            if (!Instance || Instance.IsCompleting)
                return false;
            var item = original.GetComponent<InspectableItem>();
            if (!item || !item.CanInteract(player))
                return false;
            return Instance.Begin(item, player);
        }

        readonly System.Collections.Generic.Dictionary<string, InspectionItemData> archivedItems = new System.Collections.Generic.Dictionary<string, InspectionItemData>(StringComparer.Ordinal);
        internal void RegisterArchivedItem(InspectionItemData item) => archivedItems[item.itemId] = item;
        public InspectionItemData FindItem(string id)
        {
            var item = catalog ? catalog.Find(id) : null;
            if (item)
                return item;
            if (!string.IsNullOrEmpty(id) && archivedItems.TryGetValue(id, out var archived))
                return archived;
            var saved = game && game.interaction && game.interaction.inventory ? game.interaction.inventory.items.Find(x => x.id == id) : null;
            if (saved == null || string.IsNullOrEmpty(saved.inspectionId))
                return null;
            if (archivedItems.TryGetValue(saved.inspectionId, out archived))
                return archived;
            return catalog ? catalog.Find(saved.inspectionId) : null;
        }

        public bool Begin(InspectableItem item, PlayerInteraction player)
        {
            if (!item || !player || !item.CanInteract(player) || !stage || !game.CanControl || IsCompleting)
                return false;
            return Open(item.item, item, player, false);
        }

        bool Open(InspectionItemData item, InspectableItem worldItem, PlayerInteraction player, bool notes, string savedId = null)
        {
            if (!item || !item.model || IsOpen || IsCompleting || !game.BeginInspection())
                return false;
            IsOpen = true;
            CurrentItem = item;
            source = worldItem;
            interactor = player;
            fromNotebook = notes;
            archiveRecordId = savedId;
            explicitExamination = false;
            playOnOpen = false;
            closing = returningFromZoom = false;
            Alpha = 0;
            stressApplied = 0;
            openedFrame = Time.frameCount;
            chapter = game.narrative ? game.narrative.Progress.chapterId : "";
            var saved = game.interaction.inventory.Find(savedId ?? item.itemId);
            InspectionCount = (saved?.inspectionCount ?? 0) + 1;
            try
            {
                ReserveWorld();
                var camera = game.player.view.playerCamera;
                Vector2 origin = source ? (Vector2)camera.WorldToViewportPoint(source.transform.position) : new Vector2(.5f, .35f);
                stage.Open(item, LayerMask.NameToLayer("Inspect"), origin);
                if (InspectionCount >= 2 && sanity && sanity.Sanity01 < .4f && !string.IsNullOrWhiteSpace(item.alternateBackText))
                {
                    var inscription = stage.Camera.transform.parent.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var text in inscription)
                        if (text.name == "Back inscription")
                            text.text = item.alternateBackText;
                }

                SetBackground(true);
                controls.Enable();
                PlaySound(item.pickupSound, .26f);
                whisperAt = Time.time + 4.5f;
                ui.Present(item, stage.Texture, Alpha, false, false, sanity ? sanity.Sanity01 : 1);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                Cancel(true);
                return false;
            }
        }

        internal void MarkReaderExamined() => explicitExamination = true;
        public bool Reopen(string id, bool play = false)
        {
            var item = FindItem(id);
            if (!item || !game || IsOpen || IsCompleting || !game.interaction.inventory.Contains(id))
                return false;
            if (game.State == GameState.Paused && game.hud.inventoryPanel.activeSelf)
            {
                game.Resume();
                StartCoroutine(OpenFromNotebook(item, id, play));
                return true;
            }

            if (!game.CanControl || !Open(item, null, game.interaction, true, id))
                return false;
            playOnOpen = play;
            return true;
        }

        IEnumerator OpenFromNotebook(InspectionItemData item, string id, bool play)
        {
            int token = generation;
            string expectedChapter = game.narrative ? game.narrative.Progress.chapterId : "";
            yield return null;
            if (token == generation && isActiveAndEnabled && game && game.CanControl && (!game.narrative || game.narrative.Progress.chapterId == expectedChapter))
                playOnOpen = Open(item, null, game.interaction, true, id) && play;
        }

        void Update()
        {
            if (playOnOpen && game.State == GameState.Inspecting)
            {
                playOnOpen = false;
                if (IsOpen && HasRecording(0))
                    PlayRecording();
            }

            if (!IsOpen || !game || game.State != GameState.Inspecting || !stage)
                return;
            try
            {
                UpdateInspection();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                Cancel(true);
            }
        }

        void UpdateInspection()
        {
            float dt = Time.deltaTime;
            AdvanceRecording();
            Alpha = Mathf.MoveTowards(Alpha, closing ? 0 : 1, dt / (closing ? .32f : .4f));
            stage.Animate(Alpha);
            bool dragging = !closing && drag.IsPressed() && ui.CanDragAt(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);
            if (Time.frameCount > openedFrame + 1 && !closing)
            {
                if (dragging)
                    stage.Drag(rotate.ReadValue<Vector2>());
                float scroll = zoom.ReadValue<float>();
                if (!ui.ReaderOpen)
                {
                    if (Mathf.Abs(scroll) > .001f)
                        returningFromZoom = false;
                    stage.Zoom(Mathf.Abs(scroll) > 5 ? scroll / 120 : scroll);
                    if (flip.WasPressedThisFrame())
                        Flip();
                }

                if (read.WasPressedThisFrame())
                    ui.ToggleReader();
                if (play.WasPressedThisFrame())
                {
                    if (CurrentItem.canPlay || InspectingRecorder)
                        PlayRecording();
                    else
                        ui.ToggleReader();
                }

                if (close.WasPressedThisFrame())
                    Close();
                else if (escape.WasPressedThisFrame())
                    Back();
            }

            stage.Advance(dt, dragging);
            if (returningFromZoom && PresentationZoom01 <= .01f)
                returningFromZoom = false;
            float health = sanity ? sanity.Sanity01 : 1;
            stage.LightIntensity = health < .35f ? 1 - (.35f - health) * .3f * (.5f + .5f * Mathf.Sin(Time.time * 11)) : 1;
            if (!closing && sanity && CurrentItem.stressPerSecond > 0)
            {
                float loss = Mathf.Min(CurrentItem.stressPerSecond * dt, Mathf.Max(0, CurrentItem.maximumStress - stressApplied));
                stressApplied += loss;
                sanity.ApplyInspectionStress(loss);
                if (!IsOpen)
                    return;
            }

            if (!closing && Time.time >= whisperAt && !RecordingPlaying)
            {
                PlaySound(CurrentItem.whisper, Mathf.Lerp(.09f, .035f, health));
                whisperAt = Time.time + 22;
            }

            ui.Present(CurrentItem, stage.Texture, Alpha, BackVisible, DetailVisible, health);
            if (closing && Alpha <= 0)
                StartCoroutine(Finish());
        }

        void AdvanceRecording()
        {
            if (recordingRequested && captionRecording)
            {
                if (!game || game.State != GameState.Inspecting || AudioListener.pause)
                    return;
                captionRecordingAge += Time.deltaTime;
                if (captionRecordingAge >= Mathf.Max(1, recordingItem.captionRecordingSeconds))
                {
                    recordingRequested = captionRecording = false;
                    RecordingCompleted?.Invoke(recordingItem.itemId);
                }

                return;
            }

            if (recordingRequested && sound && sound.clip == requestedRecording)
            {
                if (sound.isPlaying)
                    recordingHeard = true;
                // A paused source keeps its sample cursor; a naturally finished source resets it.
                else if (recordingHeard && !AudioListener.pause && sound.timeSamples == 0)
                {
                    recordingRequested = false;
                    RecordingCompleted?.Invoke(recordingItem.itemId);
                }
            }
        }

        public void Flip()
        {
            if (IsOpen && !closing && game.State == GameState.Inspecting)
            {
                stage.Flip();
                AudioManager.Instance?.PlayCue("paper", game.player.transform.position, .13f);
            }
        }

        public void Back()
        {
            if (!IsOpen || closing || game.State != GameState.Inspecting)
                return;
            if (ui && ui.CloseReader())
                return;
            if (!returningFromZoom && (FullscreenZoom || PresentationZoom01 > .01f))
            {
                stage.ResetZoom();
                returningFromZoom = true;
            }
            else
                Close();
        }

        public void Close()
        {
            if (!IsOpen || closing || game.State != GameState.Inspecting)
                return;
            if (RecordingPlaying)
                AudioManager.Instance?.PlayCue("tape_stop", game.player.transform.position, .13f);
            recordingRequested = false;
            closing = true;
            IsCompleting = true;
            controls.Disable();
        }

        public void PlayRecording()
        {
            PlayRecording(0);
        }

        public bool HasRecording(int tapeIndex)
        {
            var item = ResolveRecording(tapeIndex);
            if (!item || !item.canPlay)
                return false;
            return item.recording && item.recording.clips != null && Array.Exists(item.recording.clips, clip => clip) || item.captionOnlyRecording && !string.IsNullOrWhiteSpace(item.recordingText);
        }

        InspectionItemData ResolveRecording(int tapeIndex)
        {
            if (!InspectingRecorder)
                return CurrentItem;
            if (tapeIndex < 0 || tapeIndex > 1 || !game || !game.interaction || !game.interaction.inventory)
                return null;
            string id = tapeIndex == 0 ? "hospicio_child_tape" : "hospicio_director_tape";
            return game.interaction.inventory.Contains(id) ? FindItem(id) : null;
        }

        public void PlayRecording(int tapeIndex)
        {
            if (!IsOpen || closing || game.State != GameState.Inspecting)
                return;
            var item = ResolveRecording(tapeIndex);
            if (!item || !HasRecording(tapeIndex))
            {
                game.hud?.Notify("Preciso encontrar uma fita. Posso voltar ao gravador nas Anotações.");
                return;
            }

            if (RecordingPlaying && recordingItem == item)
            {
                sound.Stop();
                recordingRequested = captionRecording = false;
                AudioManager.Instance?.PlayCue("tape_stop", transform.position, .18f);
                return;
            }

            bool hasAudio = item.recording && item.recording.clips != null && Array.Exists(item.recording.clips, clip => clip);
            AudioManager.Instance?.PlayCue("tape_play", transform.position, .18f);
            if (!hasAudio && item.captionOnlyRecording)
            {
                sound.Stop();
                requestedRecording = null;
                recordingItem = item;
                recordingRequested = captionRecording = true;
                captionRecordingAge = 0;
                captionTrack = BuildCaptionTrack(item, Mathf.Max(1, item.captionRecordingSeconds));
                RecordingStarted?.Invoke(item.itemId);
                return;
            }

            captionRecording = false;
            if (!PlaySound(item.recording, .85f, true))
                return;
            recordingItem = item;
            recordingRequested = true;
            requestedRecording = sound.clip;
            captionTrack = BuildCaptionTrack(item, requestedRecording.length);
            recordingHeard = false;
            RecordingStarted?.Invoke(item.itemId);
        }

        static RecordingCaptionCue[] BuildCaptionTrack(InspectionItemData item, float duration)
        {
            if (item.recordingCues != null && item.recordingCues.Length > 0)
                return item.recordingCues;
            if (string.IsNullOrWhiteSpace(item.recordingText))
                return Array.Empty<RecordingCaptionCue>();
            // Legacy recordings without authored timecodes still reveal the transcript by phrase.
            string[] phrases = System.Text.RegularExpressions.Regex.Split(item.recordingText.Trim(), @"(?<=[.!?…])\s+|
+");
            int characters = 0;
            foreach (string phrase in phrases)
                characters += phrase.Length;
            var cues = new RecordingCaptionCue[phrases.Length];
            float start = 0;
            for (int i = 0; i < phrases.Length; i++)
            {
                float end = start + duration * phrases[i].Length / characters;
                cues[i] = new RecordingCaptionCue
                {
                    start = start,
                    end = end,
                    text = phrases[i].Trim()
                };
                start = end;
            }

            return cues;
        }

        bool PlaySound(SoundSet set, float gain, bool voice = false)
        {
            if (!set || !sound)
                return false;
            var clip = set.Next();
            if (!clip)
                return false;
            recordingRequested = false;
            if (AudioManager.Instance)
                sound.outputAudioMixerGroup = voice ? AudioManager.Instance.voiceGroup : AudioManager.Instance.sfxGroup;
            if (voice)
            {
                if (!recordingPresentation)
                    recordingPresentation = sound.GetComponent<VoiceAudioPresentation>() ?? sound.gameObject.AddComponent<VoiceAudioPresentation>();
                var camera = game && game.player && game.player.view ? game.player.view.playerCamera : null;
                recordingPresentation.Configure(sound, VoicePlaybackStyle.Tape, SceneManager.GetActiveScene().name == "SchoolCEEP", camera ? camera.transform : null, new Vector3(.15f, -.2f, .4f));
            }
            else
            {
                if (recordingPresentation)
                    recordingPresentation.Configure(sound, VoicePlaybackStyle.Natural, false);
                sound.spatialBlend = 0;
            }

            sound.clip = clip;
            sound.volume = gain * set.Volume;
            sound.pitch = set.Pitch;
            sound.Play();
            return true;
        }

        IEnumerator Finish()
        {
            int token = ++generation;
            var item = CurrentItem;
            var picked = source;
            var player = interactor;
            bool notes = fromNotebook;
            IsOpen = false;
            RestorePresentation(false);
            restoring = true;
            game.EndInspection();
            restoring = false;
            // Session deliberately accepts input only on the frame after state restoration.
            yield return null;
            if (token != generation || !game || !game.CanControl || (game.narrative && game.narrative.Progress.chapterId != chapter))
            {
                RestoreReservedVisuals();
                IsCompleting = false;
                CurrentItem = null;
                yield break;
            }

            try
            {
                if (picked && picked.originalInteraction is IInteractable original && !original.CanInteract(player))
                    yield break;
                if (picked)
                    picked.Complete(player);
                var inventory = game.interaction.inventory;
                var saved = inventory.Find(archiveRecordId ?? item.itemId);
                if (saved == null && !notes && item.recordInNotes)
                {
                    saved = new InventoryItem
                    {
                        id = item.itemId,
                        title = item.title,
                        text = item.backText,
                        note = true
                    };
                    inventory.Add(saved);
                }

                if (saved != null)
                {
                    saved.inspectionId = item.itemId;
                    saved.inspectionCount = InspectionCount;
                    if (notes || explicitExamination)
                        inventory.MarkExamined(saved.id);
                    if (game.narrative)
                        game.narrative.ScheduleSave();
                    else
                        game.save?.Save();
                }

                if (notes && game.CanControl)
                    game.hud.ShowInventory();
            }
            finally
            {
                RestoreReservedVisuals();
                IsCompleting = false;
                CurrentItem = null;
                source = null;
            }
        }

        void ReserveWorld()
        {
            if (!source)
                return;
            var visual = source.visual ? source.visual : source.gameObject;
            reservedRenderers = visual.GetComponentsInChildren<Renderer>(true);
            rendererStates = new bool[reservedRenderers.Length];
            for (int i = 0; i < reservedRenderers.Length; i++)
            {
                rendererStates[i] = reservedRenderers[i].enabled;
                reservedRenderers[i].enabled = false;
            }
        }

        void RestoreReservedVisuals()
        {
            if (reservedRenderers != null)
                for (int i = 0; i < reservedRenderers.Length; i++)
                    if (reservedRenderers[i])
                        reservedRenderers[i].enabled = rendererStates[i];
            reservedRenderers = null;
            rendererStates = null;
        }

        void SetBackground(bool active)
        {
            var camera = game.player.view.playerCamera;
            if (active)
            {
                backgroundReserved = true;
                int mask = 1 << LayerMask.NameToLayer("Inspect");
                cameraMask = camera.cullingMask;
                camera.cullingMask &= ~mask;
                worldLights.Clear();
                foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include))
                    if (light.type == LightType.Directional && light.gameObject.layer != LayerMask.NameToLayer("Inspect") && light != stage.Camera.GetComponent<Light>() && !light.transform.IsChildOf(transform))
                    {
                        worldLights.Add((light, light.cullingMask));
                        light.cullingMask &= ~mask;
                    }

                flashlight = game.save ? game.save.flashlight : null;
                if (flashlight)
                {
                    flashlightEnabled = flashlight.enabled;
                    lampEnabled = flashlight.lamp && flashlight.lamp.enabled;
                    handVisible = flashlight.handRig && flashlight.handRig.gameObject.activeSelf;
                    flashlight.enabled = false;
                    if (flashlight.lamp)
                        flashlight.lamp.enabled = false;
                    if (flashlight.handRig)
                        flashlight.handRig.gameObject.SetActive(false);
                }

                lowPass = camera.GetComponent<AudioLowPassFilter>();
                lowPassOwned = !lowPass;
                if (!lowPass)
                    lowPass = camera.gameObject.AddComponent<AudioLowPassFilter>();
                lowPassEnabled = lowPass.enabled;
                lowPassCutoff = lowPass.cutoffFrequency;
                lowPass.cutoffFrequency = 1800;
                lowPass.enabled = true;
                cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
                if (cameraData)
                {
                    depthOption = cameraData.requiresDepthOption;
                    postProcessing = cameraData.renderPostProcessing;
                    cameraData.requiresDepthOption = CameraOverrideOption.On;
                    cameraData.renderPostProcessing = true;
                    var volumeObject = new GameObject("Inspection background blur (temporary)");
                    volumeObject.transform.SetParent(transform, false);
                    for (int layer = 0; layer < 32; layer++)
                        if ((cameraData.volumeLayerMask.value & (1 << layer)) != 0)
                        {
                            volumeObject.layer = layer;
                            break;
                        }

                    blurVolume = volumeObject.AddComponent<Volume>();
                    blurVolume.isGlobal = true;
                    blurVolume.priority = 1000;
                    blurProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                    blurProfile.hideFlags = HideFlags.DontSave;
                    var dof = blurProfile.Add<DepthOfField>(true);
                    dof.mode.Override(DepthOfFieldMode.Gaussian);
                    dof.gaussianStart.Override(.1f);
                    dof.gaussianEnd.Override(1);
                    dof.gaussianMaxRadius.Override(.6f);
                    dof.highQualitySampling.Override(false);
                    blurVolume.sharedProfile = blurProfile;
                }
            }
            else
            {
                if (!backgroundReserved)
                    return;
                backgroundReserved = false;
                camera.cullingMask = cameraMask;
                foreach (var entry in worldLights)
                    if (entry.light)
                        entry.light.cullingMask = entry.mask;
                worldLights.Clear();
                if (flashlight)
                {
                    flashlight.enabled = flashlightEnabled;
                    if (flashlight.lamp)
                        flashlight.lamp.enabled = lampEnabled;
                    if (flashlight.handRig)
                        flashlight.handRig.gameObject.SetActive(handVisible);
                }

                if (lowPass)
                {
                    if (lowPassOwned)
                        Destroy(lowPass);
                    else
                    {
                        lowPass.cutoffFrequency = lowPassCutoff;
                        lowPass.enabled = lowPassEnabled;
                    }
                }

                lowPass = null;
                if (cameraData)
                {
                    cameraData.requiresDepthOption = depthOption;
                    cameraData.renderPostProcessing = postProcessing;
                }

                if (blurVolume)
                    Destroy(blurVolume.gameObject);
                if (blurProfile)
                    Destroy(blurProfile);
                blurVolume = null;
                blurProfile = null;
            }
        }

        void RestorePresentation(bool visuals)
        {
            recordingRequested = false;
            requestedRecording = null;
            recordingHeard = false;
            recordingItem = null;
            controls?.Disable();
            ui?.Hide();
            stage?.Close();
            if (sound)
                sound.Stop();
            if (game && game.player && game.player.view && game.player.view.playerCamera)
                SetBackground(false);
            if (visuals)
                RestoreReservedVisuals();
        }

        public void Cancel(bool restoreControl)
        {
            ++generation;
            StopAllCoroutines();
            if (!IsOpen && !IsCompleting)
                return;
            IsOpen = false;
            IsCompleting = false;
            closing = false;
            RestorePresentation(true);
            CurrentItem = null;
            source = null;
            if (restoreControl && game && (game.State == GameState.Inspecting || game.State == GameState.Paused))
            {
                restoring = true;
                game.EndInspection();
                restoring = false;
            }
        }

        void StateChanged(GameState state)
        {
            if (restoring || !IsOpen)
                return;
            if (state == GameState.Paused)
            {
                controls.Disable();
                ui.Hide();
                stage.SetVisible(false);
            }
            else if (state == GameState.Inspecting)
            {
                controls.Enable();
                stage.SetVisible(true);
            }
            else
                Cancel(false);
        }

        void ChapterChanged(string id) => Cancel(true);
        void OnDisable() => Cancel(true);
        void OnDestroy()
        {
            Cancel(false);
            if (game)
            {
                game.StateChanged -= StateChanged;
                if (game.narrative)
                    game.narrative.ChapterInitialized -= ChapterChanged;
            }

            controls?.Dispose();
            if (ui)
                Destroy(ui.gameObject);
            if (stage)
                Destroy(stage);
            if (sound)
                Destroy(sound);
            if (Instance == this)
                Instance = null;
        }
    }
}
