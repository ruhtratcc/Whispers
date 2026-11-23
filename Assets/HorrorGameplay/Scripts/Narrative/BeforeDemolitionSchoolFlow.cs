// Coordena a secretaria, as evidências, a chave e a visita à biblioteca.

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorGame
{
    [DefaultExecutionOrder(-130)]
    public sealed class BeforeDemolitionSchoolFlow : PersistentObject
    {
        public const string StateId = "bd.school.flow", KeyId = "bd.library.key", ReportId = "bd.report.2006", TapeId = "bd.tape.01", TapeHeardId = "bd.tape.01.heard";
        public GameSession game;
        public EmailInboxController inbox;
        public InteractiveDoor secretaryDoor, administrationDoor, libraryDoor;
        public InteractiveDoor entranceGate;
        public BoxCollider entranceInside;
        public InteractiveDrawer drawer;
        public BoxCollider secretaryVolume, libraryVolume, libraryExitVolume;
        public GameObject keyVisual, reportVisual;
        public Collider lunchboxCollider;
        public Transform lunchLid, telephone;
        public Light secretaryLight;
        public SoundSet phoneRing, phoneBreathing, phonePickup;
        public SchoolInspectionCatalog items;
        public AudioClip childWarning;
        public BeforeDemolitionSchoolState State => state;
        public bool IsActive => initialized && game && game.narrative && game.narrative.Progress.chapterId == BeforeDemolitionCampaign.SchoolChapterId;
        public bool ReportTaken => Has("bd.school.report");
        public bool TapeHeard => Has("bd.school.tape");
        public bool PhoneRinging => IsActive && ReportTaken && !state.phoneAnswered;

        BeforeDemolitionSchoolState state;
        WhistleSystem whistle;
        bool initialized, diaryWasOpen, diaryDirty = true;
        AudioSource ringing, breathing, childVoice;
        AudioClip ringClip, breathingClip;
        float ringAge;
        Quaternion lidClosed;
        NarrativeDirector Director => game ? game.narrative : null;

        Inventory Inventory => game && game.interaction ? game.interaction.inventory : null;

        bool Has(string id) => Director && Director.HasSignal(id);
        bool Inside(BoxCollider volume) => volume && game && game.player && volume.bounds.Contains(game.player.transform.position + Vector3.up * .8f);
        void Awake()
        {
            using (LoadingMetrics.Measure("School.Awake"))
            {
                if (!game)
                    game = GetComponent<GameSession>();
                whistle = GetComponent<WhistleSystem>();
                inbox.game = game;
                inbox.openOnStart = false;
                inbox.Configure(inbox.catalog);
                state = new BeforeDemolitionSchoolState
                {
                    version = 2,
                    emails = inbox.CaptureState()
                };
                if (lunchLid)
                    lidClosed = lunchLid.localRotation;
            }
        }

        void OnEnable()
        {
            if (Director)
                Director.Initialized += Initialize;
            if (inbox)
                inbox.ReadFlagRaised += EmailRead;
        }

        void Start()
        {
            if (InspectionManager.Instance)
                InspectionManager.Instance.RecordingCompleted += RecordingCompleted;
        }

        void Initialize()
        {
            initialized = Director && BeforeDemolitionCampaign.Is(Director.book) && Director.Progress.chapterId == BeforeDemolitionCampaign.SchoolChapterId;
            if (!initialized)
            {
                StopPhone();
                return;
            }

            if (!Inventory || !Inventory.Contains(BeforeDemolitionCampaign.WhistleItemId) || !Inventory.Contains(BeforeDemolitionRoadFlow.EmailDeliveryItemId))
            {
                initialized = false;
                game.hud?.Notify("Continuar a investigação pelo escritório ou pelo último ponto salvo.");
                game.ShowStoryScreen();
                game.ReturnToMenu();
                return;
            }

            SyncEmails();
            RestoreWorldSilently();
        }

        void SyncEmails()
        {
            var emails = inbox.CaptureState();
            bool changed = false;
            if (Inventory)
            {
                if (state.version == 1 && Inventory.Contains(BeforeDemolitionRoadFlow.EmailDeliveryItemId))
                    changed |= emails.Deliver(inbox.catalog, "email.08");
                for (int i = 1; i <= 8; i++)
                    if (Inventory.Contains("bd.note.email.0" + i))
                        changed |= emails.MarkRead(inbox.catalog, "email.0" + i);
            }

            if (changed)
                inbox.RestoreState(emails);
            state.emails = emails.Copy();
        }

        void EmailRead(string flag)
        {
            if (!Director || !BeforeDemolitionCampaign.Is(Director.book))
                return;
            foreach (var message in inbox.catalog.messages)
                if (inbox.State.IsRead(message.id))
                    Inventory.Add(new InventoryItem { id = "bd.note." + message.id, title = "Email — " + message.subject, text = message.PlainText, note = true });
            state.emails = inbox.CaptureState();
            diaryDirty = true;
            Director.ScheduleSave();
        }

        public void RestoreWorldSilently()
        {
            if (!IsActive)
                return;
            StopPhone();
            ringAge = 1;
            if (entranceGate)
            {
                bool entered = entranceGate.locked || Has("bd.school.secretary") || PastEntrance();
                entranceGate.SetLocked(entered);
                entranceGate.SetOpen(false, true);
            }

            administrationDoor.SetLocked(false);
            administrationDoor.SetOpen(true, true);
            secretaryDoor.SetLocked(false);
            secretaryDoor.SetOpen(true, true);
            if (secretaryLight)
                secretaryLight.enabled = true;
            if (keyVisual)
                keyVisual.SetActive(Has("bd.school.lunchbox") && !Has("bd.school.key"));
            RefreshLunchboxCollider();
            if (reportVisual)
                reportVisual.SetActive(!ReportTaken);
            if (lunchLid)
                lunchLid.localRotation = lidClosed * Quaternion.Euler(Has("bd.school.lunchbox") ? -105 : 0, 0, 0);
            ApplyLibrary(true);
            diaryDirty = true;
        }

        void RefreshLunchboxCollider()
        {
            if (lunchboxCollider && drawer)
                lunchboxCollider.enabled = !Has("bd.school.lunchbox") && drawer.OpenFraction >= .8f;
        }

        void ApplyLibrary(bool silent)
        {
            bool trapped = ReportTaken && !TapeHeard;
            libraryDoor.requiredKey = trapped ? "" : KeyId;
            libraryDoor.lockedMessage = trapped ? "A porta não cede. O gravador ainda está na mesa." : "Biblioteca trancada. A chave deve estar na secretaria.";
            libraryDoor.SetLocked(trapped || !Has("bd.school.key"));
            if (trapped || !Has("bd.school.key"))
                libraryDoor.SetOpen(false, silent);
            else if (TapeHeard)
                libraryDoor.SetOpen(true, silent);
        }

        void Update()
        {
            if (!IsActive)
                return;
            RefreshLunchboxCollider();
            if (game.CanControl)
            {
                if (entranceGate && !entranceGate.locked && PastEntrance())
                {
                    entranceGate.SetOpen(false);
                    entranceGate.SetLocked(true);
                    Director.ScheduleSave();
                }

                // The completed recording now teaches the signal too; old checkpoints may have missed the optional note.
                if (TapeHeard && whistle && !whistle.State.loreKnown)
                    whistle.LearnLore();
                if (Director.NextSignal == "bd.school.secretary" && Inside(secretaryVolume))
                    Signal("bd.school.secretary");
                if (Director.NextSignal == "bd.school.library" && Inside(libraryVolume))
                    Signal("bd.school.library");
                if (Director.NextSignal == "bd.school.exit" && TapeHeard && libraryVolume && !Inside(libraryVolume))
                    Signal("bd.school.exit");
            }

            if (PhoneRinging && game.CanControl)
            {
                ringAge -= Time.deltaTime;
                if (ringAge <= 0)
                {
                    ringing = AudioManager.Instance?.Play(phoneRing, telephone.position, .22f, AudioChannel.SFX, true);
                    ringClip = ringing ? ringing.clip : null;
                    ringAge = 3.6f;
                }
            }
            else if (ringing)
            {
                if (ringing.clip == ringClip)
                    ringing.Stop();
                ringing = null;
                ringClip = null;
            }
        }

        bool PastEntrance()
        {
            if (!entranceInside || !entranceGate || !game || !game.player)
                return false;
            Vector3 gateCenter = entranceGate.pairedLeaf ? (entranceGate.transform.position + entranceGate.pairedLeaf.transform.position) * .5f : entranceGate.transform.position;
            Vector3 inward = entranceInside.bounds.center - gateCenter;
            inward.y = 0;
            if (inward.sqrMagnitude < .01f)
                return false;
            return Vector3.Dot(game.player.transform.position - entranceInside.bounds.center, inward.normalized) >= 0;
        }

        void Signal(string id)
        {
            if (Director.Signal(id))
            {
                diaryDirty = true;
                Director.ScheduleSave();
            }
        }

        public bool CanInteract(BeforeDemolitionSchoolAction action, PlayerInteraction player)
        {
            if (!IsActive || !player || player != game.interaction || !game.CanControl)
                return false;
            switch (action)
            {
                case BeforeDemolitionSchoolAction.Cups:
                    return !Has("bd.school.cups") && Has("bd.school.secretary");
                case BeforeDemolitionSchoolAction.Lunchbox:
                    return Has("bd.school.cups") && !Has("bd.school.lunchbox") && drawer.OpenFraction >= .8f;
                case BeforeDemolitionSchoolAction.Key:
                    return Has("bd.school.lunchbox") && !Has("bd.school.key") && drawer.OpenFraction >= .8f && PhoneProgression.CanAdvance(game, "bd.school.key");
                case BeforeDemolitionSchoolAction.Report:
                    return Has("bd.school.library") && !ReportTaken;
                case BeforeDemolitionSchoolAction.Tape:
                    return ReportTaken;
                case BeforeDemolitionSchoolAction.Phone:
                    return PhoneRinging;
                default:
                    return true;
            }
        }

        public void Interact(BeforeDemolitionSchoolAction action, PlayerInteraction player)
        {
            if (!CanInteract(action, player))
                return;
            switch (action)
            {
                case BeforeDemolitionSchoolAction.Cups:
                    Signal("bd.school.cups");
                    break;
                case BeforeDemolitionSchoolAction.Lunchbox:
                    Signal("bd.school.lunchbox");
                    if (lunchboxCollider)
                        lunchboxCollider.enabled = false;
                    if (lunchLid)
                        lunchLid.localRotation = lidClosed * Quaternion.Euler(-105, 0, 0);
                    AudioManager.Instance?.PlayCue("metal_lid", lunchLid ? lunchLid.position : transform.position, .23f);
                    keyVisual.SetActive(true);
                    break;
                case BeforeDemolitionSchoolAction.Key:
                    AddItem(KeyId);
                    Signal("bd.school.key");
                    keyVisual.SetActive(false);
                    ApplyLibrary(false);
                    Inventory.Add(new InventoryItem { id = "bd.phone.welcome", title = "Helena — celular, 03:07", text = "Boa noite, João. Fico feliz que tenha vindo.", note = true });
                    game.hud?.ShowSubtitle("Celular — Helena: Boa noite, João. Fico feliz que tenha vindo.", 7);
                    AudioManager.Instance?.Play(phonePickup, game.player.transform.position, .12f);
                    break;
                case BeforeDemolitionSchoolAction.Report:
                    AddItem(ReportId);
                    Signal("bd.school.report");
                    reportVisual.SetActive(false);
                    ApplyLibrary(false);
                    ringAge = .8f;
                    break;
                case BeforeDemolitionSchoolAction.Phone:
                    state.phoneAnswered = true;
                    StopPhone();
                    PhoneMessages.For(game)?.Trigger(PhoneTrigger.LibraryWarning);
                    AudioManager.Instance?.Play(phonePickup, telephone.position, .18f);
                    breathing = AudioManager.Instance?.Play(phoneBreathing, telephone.position, .12f, AudioChannel.SFX, true);
                    breathingClip = breathing ? breathing.clip : null;
                    game.hud?.ShowSubtitle("Voz infantil, ao telefone: não responde ela", 6);
                    childVoice = VoiceAudioPresentation.PlayInWorld(childWarning, transform, telephone.position, VoicePlaybackStyle.Telephone, false, .66f);
                    if (childVoice)
                        childVoice.maxDistance = 9;
                    Director.ScheduleSave();
                    break;
            }
        }

        void AddItem(string id)
        {
            var item = items.Find(id);
            Inventory.Add(new InventoryItem { id = id, title = item.title, text = item.backText, note = true, inspectionId = id });
            diaryDirty = true;
        }

        void RecordingCompleted(string id)
        {
            if (!IsActive || !ReportTaken || TapeHeard || id != TapeId)
                return;
            AddItem(TapeId);
            Inventory.Add(new InventoryItem { id = TapeHeardId, title = "Fita 1 ouvida até o fim" });
            Signal("bd.school.tape");
            ApplyLibrary(false);
        }

        void LateUpdate()
        {
            if (!IsActive || !game.hud || !game.hud.inventoryPanel)
                return;
            bool open = game.hud.inventoryPanel.activeSelf;
            if (open && (!diaryWasOpen || diaryDirty))
            {
                game.hud.inventoryText.text = "OBJETIVO\n" + game.hud.objective.text + "\n\nDIÁRIO E EQUIPAMENTO\n" + string.Join("\n\n", Inventory.items.Select(i => i.title + (i.note ? "\n" + i.text : "")));
                diaryDirty = false;
            }

            diaryWasOpen = open;
        }

        public override string CaptureState()
        {
            SyncEmails();
            return JsonUtility.ToJson(state);
        }

        public override void RestoreState(string json)
        {
            var restored = BeforeDemolitionSchoolState.Parse(json);
            if (!restored.ValidFields || !restored.emails.Validate(inbox.catalog))
                throw new ArgumentException("Checkpoint escolar inválido.", nameof(json));
            state = restored;
            inbox.RestoreState(state.emails);
            RestoreWorldSilently();
        }

        void StopPhone()
        {
            if (ringing && ringing.clip == ringClip)
                ringing.Stop();
            if (breathing && breathing.clip == breathingClip)
                breathing.Stop();
            if (childVoice)
                Destroy(childVoice.gameObject);
            ringing = breathing = childVoice = null;
            ringClip = breathingClip = null;
        }

        void OnDisable()
        {
            if (Director)
                Director.Initialized -= Initialize;
            if (inbox)
                inbox.ReadFlagRaised -= EmailRead;
            if (InspectionManager.Instance)
                InspectionManager.Instance.RecordingCompleted -= RecordingCompleted;
            StopPhone();
        }
    }
}
