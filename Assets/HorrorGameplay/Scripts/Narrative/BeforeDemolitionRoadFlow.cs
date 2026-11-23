// Coordena a viagem, as ações dentro do carro e a entrada na escola.

using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorGame
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class BeforeDemolitionRoadFlow : PersistentObject
    {
        public const string StateId = "bd.road.flow";
        public const string EmailDeliveryItemId = "bd.email.delivery.08";
        public GameSession game;
        public RoadJourneyFlow driver;
        public EmailInboxController inbox;
        public BeforeDemolitionRoadRadio radio;
        public Transform phone;
        public GameObject phoneModel;
        public Vector3 phoneNearHandLocal = new Vector3(-.06f, 1.04f, .15f);
        public TMP_Text phoneScreen;
        [Min(.1f)] public float glanceSeconds = 1;
        [Min(1)] public float ignoredBuzzSeconds = 8, incomingCallSeconds = 12;
        public BeforeDemolitionRoadState State => state;
        public RoadPhoneCallView CallView => callView;
        public bool IsActive => initialized && game && game.narrative && game.narrative.IsInitialized
            && BeforeDemolitionCampaign.Is(game.narrative.book) && game.narrative.Progress.chapterId == BeforeDemolitionCampaign.RoadChapterId;
        public bool Parked => state != null && state.phase == RoadRadioPhase.Parked;
        public bool Exited => game && game.narrative.HasSignal("bd.road.car-exited");
        public bool HasTorch => game && game.narrative.HasSignal("bd.road.flashlight-taken");
        public bool IncomingCall => state != null && state.callOffered && !state.callAnswered;
        public bool Glancing => glanceAge >= 0;
        public float DriveProgress => state != null && radio ? radio.Progress(state) : 0;
        NarrativeDirector Director => game ? game.narrative : null;
        Inventory Inventory => game && game.interaction ? game.interaction.inventory : null;
        bool WorldTicks => IsActive && !SceneTransitionService.ControlsBlocked && !game.IsRestoringCheckpoint
            && (game.State == GameState.Playing || game.State == GameState.Inspecting);
        BeforeDemolitionRoadState state;
        RoadPhoneCallView callView;
        Vector3 originalPhoneLocal;
        Vector3 originalPhoneModelLocal;
        bool enteringSchool;
        bool initialized, diaryDirty = true, diaryWasOpen;
        float glanceAge = -1, saveAge, nextBuzzAt;
        bool messagesOnly;
        PhoneMessages messages;
        string lastObjective;
        Collider[] phoneColliders=Array.Empty<Collider>();
        int lastBuzz;
        float vibrationAge;

        void Awake()
        {
            using(LoadingMetrics.Measure("Road.Awake")){
            if (!game) game = GetComponent<GameSession>();
            if (inbox) { inbox.game = game; inbox.openOnStart = false; inbox.Configure(inbox.catalog); }
            state = new BeforeDemolitionRoadState { version = 2, emails = inbox ? inbox.CaptureState() : null,
                radioVolume = .75f, radioVolumeInitialized = true };
            if (phone) originalPhoneLocal = phone.localPosition;
            if (phone) phoneColliders=phone.GetComponentsInChildren<Collider>(true);
            if (phoneModel) originalPhoneModelLocal = phoneModel.transform.localPosition;
            if (phone)
            {
                foreach (var body in phone.GetComponentsInChildren<Rigidbody>(true))
                {
                    if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                    body.isKinematic = true; body.useGravity = false;
                }
                var playerColliders = game && game.player ? game.player.GetComponentsInChildren<Collider>(true) : Array.Empty<Collider>();
                var carColliders = driver && driver.carRoot ? driver.carRoot.GetComponentsInChildren<Collider>(true) : Array.Empty<Collider>();
                foreach (var collider in phoneColliders)
                {
                    foreach (var other in playerColliders) if (collider && other && collider != other) Physics.IgnoreCollision(collider, other);
                    foreach (var other in carColliders) if (collider && other && collider != other && !other.transform.IsChildOf(phone)) Physics.IgnoreCollision(collider, other);
                }
            }
        
            }
        }
        void OnEnable()
        {
            if (Director) Director.Initialized += Initialize;
            messages = GetComponent<PhoneMessages>(); if (messages) messages.ConversationRead += MessagesRead;
            if (inbox) inbox.ReadFlagRaised += EmailRead;
        }
        void Initialize()
        {
            initialized = Director && BeforeDemolitionCampaign.Is(Director.book)
                && Director.Progress.chapterId == BeforeDemolitionCampaign.RoadChapterId;
            if (!initialized) return;
            if (!Inventory || !Inventory.Contains(BeforeDemolitionCampaign.WhistleItemId))
            {
                initialized = false;
                game.hud?.Notify("Comece pelo escritório para continuar esta investigação.");
                game.ShowStoryScreen(); game.ReturnToMenu(); return;
            }
            StartCoroutine(WarmSchoolAfterOpening());
            SyncInboxFromInventory();
            radio.Initialize(game);
            if (!callView)
            {
                var root = new GameObject("Celular — chamada sem nome", typeof(RectTransform)); root.transform.SetParent(transform, false);
                callView = root.AddComponent<RoadPhoneCallView>(); callView.Initialize(game, inbox.normalFont); callView.Closed += radio.EndCall;
            }
            RestoreWorldSilently();
        }
        IEnumerator WarmSchoolAfterOpening()
        {
            yield return new WaitForSecondsRealtime(3);
            if(game&&gameObject.scene.name=="RoadJourney"&&!SceneTransitionService.ControlsBlocked)
                SceneTransitionService.Preload("SchoolCEEP",true);
        }
        void SyncInboxFromInventory()
        {
            if (!inbox || !inbox.catalog) return;
            if (inbox.State == null) inbox.Configure(inbox.catalog);
            var emails = inbox.CaptureState();
            bool changed = false;
            if (Inventory)
                for (int i = 1; i <= 8; i++)
                {
                    string id = "email.0" + i;
                    if (i == 8 && state.version == 1 && Inventory.Contains(EmailDeliveryItemId)) changed |= emails.Deliver(inbox.catalog, id);
                    if (Inventory.Contains("bd.note." + id)) changed |= emails.MarkRead(inbox.catalog, id);
                }
            if (changed) inbox.RestoreState(emails);
            state.emails = emails.Copy();
        }
        void AddReadNotes()
        {
            if (!Inventory || !inbox || inbox.State == null) return;
            foreach (var message in inbox.catalog.messages)
                if (inbox.State.IsRead(message.id)) Inventory.Add(new InventoryItem {
                    id = "bd.note." + message.id, title = "Email " + message.id.Substring(7) + " — " + message.subject,
                    text = message.PlainText, note = true });
            diaryDirty = true;
        }
        void MessagesRead()
        {
            if (!IsActive || state.phase < RoadRadioPhase.Song || state.callOffered) return;
            state.callOffered = true; Director.ScheduleSave();
        }
        void EmailRead(string flag)
        {
            if (!IsActive) return;
            AddReadNotes();
            if (flag == "email.08.read" && state.phase >= RoadRadioPhase.Song && !state.callOffered)
            { state.callOffered = true; if (!Parked) radio.Vibrate(); }
            state.emails = inbox.CaptureState(); RefreshObjective(); Director.ScheduleSave();
        }
        public void RestoreWorldSilently()
        {
            if (!IsActive) return;
            if(radio.musicInterlude&&state.phase>=RoadRadioPhase.Silence)state.radioSwitchedOff=true;
            nextBuzzAt = state.phaseAge + ignoredBuzzSeconds;
            glanceAge = -1; saveAge = 0; enteringSchool = false;
            if (callView) callView.Close();
            messages?.Close();
            if (inbox.IsOpen) inbox.Close();
            AddReadNotes();
            driver.ReconstructExternalJourney(DriveProgress, Exited, HasTorch);
            driver.SetExternalWindow(Parked);
            if (phone) phone.localPosition = originalPhoneLocal;
            UpdatePhonePresence();
            if (!state.radioVolumeInitialized) { state.radioVolume = .75f; state.radioVolumeInitialized = true; }
            radio.SetVolume(state.radioVolume);
            radio.ApplyPhase(state.phase, state.phaseAge, reconstruct: true, skippedMusic: state.skippedMusic);
            driver.SetExternalDrivingAudio(!Parked);
            UpdatePhoneScreen(); lastObjective = null; RefreshObjective();
        }
        void Update()
        {
            if (!IsActive) return;
            bool running = WorldTicks;
            radio.Tick(running, Time.deltaTime); driver.SetExternalDrivingAudio(running && !Parked);
            if (!running) return;
            Advance(Time.deltaTime, followNewsAudio: true);
        }
        public void Advance(float seconds) => Advance(seconds, followNewsAudio: false);

        void Advance(float seconds, bool followNewsAudio)
        {
            if (!WorldTicks || !float.IsFinite(seconds) || seconds <= 0) return;
            float remaining = seconds;
            while (state.phase < RoadRadioPhase.Parked && remaining > 0)
            {
                float duration = radio.Duration(state.phase, state.skippedMusic);
                if (followNewsAudio && state.phase == RoadRadioPhase.News && radio.TryGetNewsPlaybackAge(out float playbackAge))
                {
                    state.phaseAge = Mathf.Min(playbackAge, duration);
                    remaining = 0;
                }
                else
                {
                    float step = Mathf.Min(remaining, Mathf.Max(0, duration - state.phaseAge));
                    state.phaseAge += step;
                    remaining -= step;
                }
                if (state.phaseAge + .00001f < duration) break;
                state.phase++; state.phaseAge = 0; EnterPhase();
            }
            if (!Parked && state.phase == RoadRadioPhase.Song)
            {
                if (!state.callAnswered && messages && !messages.RoadRead && state.phaseAge >= ignoredBuzzSeconds)
                    messages.Trigger(PhoneTrigger.RoadIgnored);
                if (!state.callAnswered && messages && !messages.RoadRead && state.phaseAge >= nextBuzzAt)
                { radio.Vibrate(); nextBuzzAt = state.phaseAge + ignoredBuzzSeconds; }
                if (!state.callOffered && state.phaseAge >= incomingCallSeconds)
                { state.callOffered = true; radio.Vibrate(); Director.ScheduleSave(); }
            }
            driver.SetExternalProgress(DriveProgress);
            radio.ApplyPhase(state.phase, state.phaseAge, skippedMusic: state.skippedMusic);
            if(radio.musicInterlude&&state.phase==RoadRadioPhase.Song&&!state.hauntedPowerTried&&state.phaseAge>=radio.hauntedAttemptDelay)TryHauntedPower();
            if (glanceAge >= 0)
            {
                glanceAge += seconds; driver.GlanceAtExternalPhone(phone, 1 - Mathf.Exp(-seconds * 9));
                if (glanceAge >= Mathf.Max(.1f, glanceSeconds)) { glanceAge = -1; PresentPhone(); }
            }
            saveAge += seconds;
            if (!Parked && saveAge >= 10) { saveAge = 0; Director.ScheduleSave(); }
            UpdatePhonePresence(); UpdatePhoneScreen(); RefreshObjective();
            CheckSchoolEntry();
        }
        void EnterPhase()
        {
            if(state.phase==RoadRadioPhase.Silence&&radio.musicInterlude)state.radioSwitchedOff=true;
            if (state.phase == RoadRadioPhase.Song)
            {
                messages?.Trigger(PhoneTrigger.RoadSong);
                Inventory.Add(new InventoryItem { id = EmailDeliveryItemId, title = messages ? messages.catalog.inventoryTitle : "", text = messages ? messages.catalog.inventoryText : "" });
                state.emails = inbox.CaptureState();
                radio.Vibrate(); nextBuzzAt = ignoredBuzzSeconds;
                if (Director.NextSignal == "bd.road.radio-changed") Director.Signal("bd.road.radio-changed");
            }
            if (Parked)
            {
                driver.SetExternalProgress(1); driver.SetExternalDrivingAudio(false); driver.SetExternalWindow(true);
                radio.ApplyPhase(RoadRadioPhase.Parked, 0);
                if (Director.NextSignal == "bd.road.car-parked") Director.Signal("bd.road.car-parked");
            }
            Director.ScheduleSave();
        }
        bool AtSchoolThreshold()
        {
            if (!driver || !driver.gate || !game || !game.player) return false;
            Vector3 point=driver.gate.transform.InverseTransformPoint(game.player.transform.position);
            // The closed leaf stops the player's capsule before the gate's centre plane.
            return Mathf.Abs(point.x)<=3.2f && Mathf.Abs(point.y)<=3 && point.z>=-.65f && point.z<=2;
        }
        void CheckSchoolEntry()
        {
            if (!Exited || !game.CanControl || Director.NextSignal != "bd.road.school-gate" || !driver.gate) return;
            if (!enteringSchool && AtSchoolThreshold())
            { enteringSchool = true; radio.BeginEntryFade(); }
            if (enteringSchool && radio.EntryFaded) Director.Signal("bd.road.school-gate");
        }
        void UpdatePhoneScreen()
        {
            if (phoneScreen) phoneScreen.text = IncomingCall ? "CHAMADA\nSEM NOME" : state.phase >= RoadRadioPhase.Song
                ? (messages ? messages.catalog.sender.ToUpperInvariant() : "")+"\n"+(GameClock.Instance?GameClock.Instance.TimeText:"")+"\n" + (messages ? (messages.RoadRead ? messages.catalog.messagesRead : messages.catalog.newMessages).ToUpperInvariant() : "") : GameClock.Instance?GameClock.Instance.TimeText:"";
        }
        void RefreshObjective()
        {
            string objective = !Parked ? state.phase >= RoadRadioPhase.Song ? messages&&!messages.Acquired ? "O celular vibrou. Pegar no carro." : "P: confira as mensagens no celular." : radio.IsMusicPhase(state.phase)?"Troque a música ou desligue o rádio no painel.":"Siga até a escola. Ouça o rádio."
                : messages&&!messages.Acquired ? "Pegue o celular antes de sair do carro." : !HasTorch ? "Pegue a lanterna no banco do passageiro."
                : !Exited ? "Saia pela porta do motorista." : "Ir até o portão da escola. P: consultar o celular.";
            if (objective != lastObjective) { lastObjective = objective; game.hud?.ShowObjective(objective); diaryDirty = true; }
        }
        public bool CanInteract(BeforeDemolitionRoadAction action)
        {
            if (!IsActive || !game.CanControl || Glancing) return false;
            switch (action)
            {
                case BeforeDemolitionRoadAction.Phone: return state.phase>=RoadRadioPhase.Song&&(!messages||!messages.Acquired)&&!inbox.IsOpen&&(!callView||!callView.IsOpen);
                case BeforeDemolitionRoadAction.RadioPower:
                case BeforeDemolitionRoadAction.RadioStation:
                case BeforeDemolitionRoadAction.RadioVolumeUp:
                case BeforeDemolitionRoadAction.RadioVolumeDown: return driver.Seated && !enteringSchool;
                case BeforeDemolitionRoadAction.Flashlight: return driver.Seated && Parked && !HasTorch;
                case BeforeDemolitionRoadAction.Exit: return driver.Seated && Parked && HasTorch && (!messages||messages.Acquired) && !Exited;
                case BeforeDemolitionRoadAction.Gate: return Exited && AtSchoolThreshold() && Director.NextSignal == "bd.road.school-gate";
                default: return false;
            }
        }
        public bool Interact(BeforeDemolitionRoadAction action, PlayerInteraction player)
        {
            if (!player || player != game.interaction || !CanInteract(action)) return false;
            switch (action)
            {
                case BeforeDemolitionRoadAction.Phone: OpenPhone(); return true;
                case BeforeDemolitionRoadAction.RadioVolumeUp:
                case BeforeDemolitionRoadAction.RadioVolumeDown:
                    radio.SetVolume(radio.Volume + (action == BeforeDemolitionRoadAction.RadioVolumeUp ? .1f : -.1f), true);
                    state.radioVolume = radio.Volume; state.radioVolumeInitialized = true; Director.ScheduleSave(); return true;
                case BeforeDemolitionRoadAction.RadioPower:
                case BeforeDemolitionRoadAction.RadioStation:
                    if(radio.IsMusicPhase(state.phase))
                    {
                        if(action==BeforeDemolitionRoadAction.RadioStation&&state.phase==RoadRadioPhase.ThirdAnnouncement){radio.AttemptTune();return true;}
                        bool powerOff = action == BeforeDemolitionRoadAction.RadioPower;
                        if (powerOff)
                        {
                            state.skipStartProgress = DriveProgress;
                            state.skippedMusic = true;
                            state.radioSwitchedOff = true;
                        }
                        state.phase = powerOff ? RoadRadioPhase.Song : state.phase + 1;
                        state.phaseAge = 0;
                        EnterPhase();
                        radio.ApplyPhase(state.phase, 0, skippedMusic: state.skippedMusic);
                        driver.SetExternalProgress(DriveProgress);
                        return true;
                    }
                    if(state.phase==RoadRadioPhase.Song || Parked){TryHauntedPower();return true;}
                    if(radio.musicInterlude&&state.phase==RoadRadioPhase.Silence){game.hud?.Notify("O rádio está desligado.");return true;}
                    radio.AttemptTune(); return true;
                case BeforeDemolitionRoadAction.Flashlight:
                    if (!Director.Signal("bd.road.flashlight-taken")) return false;
                    driver.SetExternalFlashlight(true, true); game.equipment?.CollectedFlashlight(); Director.ScheduleSave(); return true;
                case BeforeDemolitionRoadAction.Exit:
                    if (!driver.ExitExternalCar() || !Director.Signal("bd.road.car-exited")) return false;
                    AudioManager.Instance?.PlayCue("car_door",driver.carRoot?driver.carRoot.position:transform.position,.25f);
                    Director.ScheduleSave(); return true;
                case BeforeDemolitionRoadAction.Gate:
                    if (!AtSchoolThreshold()) return false;
                    enteringSchool = true; radio.BeginEntryFade(); return true;
                default: return false;
            }
        }
        public bool SkipChapter()
        {
            if (!IsActive || !game.CanControl || Director.SequencePlaying || game.IsRestoringCheckpoint || SceneTransitionService.ControlsBlocked) return false;
            glanceAge = -1;
            Advance(3600);
            messages?.Close(); if (inbox.IsOpen) inbox.Close(); if (callView) callView.Close();
            messages?.Acquire();
            if (messages) foreach (var m in messages.State.received) { m.read = true; m.announced = true; }
            state.callOffered = state.callAnswered = true;
            if (!HasTorch && !Interact(BeforeDemolitionRoadAction.Flashlight, game.interaction)) return false;
            if (!Exited && !Interact(BeforeDemolitionRoadAction.Exit, game.interaction)) return false;
            radio.StopAll();
            return Director.NextSignal == "bd.road.school-gate" && Director.Signal("bd.road.school-gate");
        }
        void TryHauntedPower()
        {
            if (state.skippedMusic) return;
            bool first=!state.hauntedPowerTried;state.hauntedPowerTried=true;
            radio.RefuseHauntedPower(first);if(first)Director.ScheduleSave();
        }
        public void OpenPhone(bool conversationOnly = false)
        {
            if (!IsActive || !game.CanControl || Glancing || inbox.IsOpen || messages && messages.IsOpen || callView && callView.IsOpen) return;
            if(messages&&!messages.Acquired&&state.phase<RoadRadioPhase.Song)return;
            messagesOnly = conversationOnly;
            if (driver.Seated&&messages&&!messages.Acquired) glanceAge = 0; else PresentPhone();
        }
        void PresentPhone()
        {
            if (!game.CanControl) return;
            if(messages&&!messages.Acquired){messages.Acquire();UpdatePhonePresence();RefreshObjective();return;}
            if (!messagesOnly && IncomingCall && (!messages || messages.RoadRead) && callView.Open())
            { state.callAnswered = true; radio.BeginCall(); Director.ScheduleSave(); }
            else messages?.OpenDirect();
        }
        void UpdatePhonePresence()
        {
            bool visible=!messages||!messages.Acquired;
            if (phone) phone.localPosition = originalPhoneLocal;
            if(phoneModel&&phoneModel.activeSelf!=visible)phoneModel.SetActive(visible);
            foreach(var collider in phoneColliders)if(collider)collider.enabled=visible;
            if(radio&&radio.VibrationCount!=lastBuzz){lastBuzz=radio.VibrationCount;vibrationAge=.8f;}
            if(phoneModel&&visible)
            {
                vibrationAge=Mathf.Max(0,vibrationAge-Time.deltaTime);
                phoneModel.transform.localPosition=originalPhoneModelLocal+(vibrationAge>0?new Vector3(Mathf.Sin(Time.time*83)*.0015f,0,Mathf.Cos(Time.time*71)*.0007f):Vector3.zero);
            }
        }
        void LateUpdate()
        {
            if (!IsActive || !game.hud || !game.hud.inventoryPanel) return;
            bool open = game.hud.inventoryPanel.activeSelf;
            if (open && (!diaryWasOpen || diaryDirty))
            {
                game.hud.inventoryText.text = "OBJETIVO\n" + game.hud.objective.text + "\n\nDIÁRIO E EQUIPAMENTO\n"
                    + string.Join("\n\n", Inventory.items.Select(i => i.title + (i.note ? "\n" + i.text : "")));
                diaryDirty = false;
            }
            diaryWasOpen = open;
        }
        public override string CaptureState()
        {
            if (state == null) state = new BeforeDemolitionRoadState();
            if (radio) { state.radioVolume = radio.Volume; state.radioVolumeInitialized = true; }
            state.phaseDuration = radio.Duration(state.phase, state.skippedMusic);
            SyncInboxFromInventory(); state.emails = inbox.CaptureState(); return JsonUtility.ToJson(state);
        }
        public override void RestoreState(string json)
        {
            var restored = BeforeDemolitionRoadState.Parse(json);
            if (!restored.ValidFields || restored.emails == null || !restored.emails.Validate(inbox.catalog)
                || restored.emails.IsDelivered("email.08") != (restored.version == 1 && restored.phase >= RoadRadioPhase.Song))
                throw new ArgumentException("Checkpoint de viagem inválido.", nameof(json));
            state = restored; state.phaseAge = Mathf.Min(state.phaseAge, radio.Duration(state.phase, state.skippedMusic));
            inbox.RestoreState(state.emails); if (IsActive) RestoreWorldSilently();
        }
        void OnDisable()
        {
            if (Director) Director.Initialized -= Initialize;
            if (messages) { messages.ConversationRead -= MessagesRead; messages.Close(); }
            if (inbox) { inbox.ReadFlagRaised -= EmailRead; inbox.Close(); }
            if (callView) callView.Close();
            if (radio) radio.StopAll(); if (driver) driver.SetExternalDrivingAudio(false);
            initialized = false; glanceAge = -1;
        }
    }
}
