// Coordena os quatro alunos, os itens, a gravação de Bia e o acesso à escada.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame
{
    [DefaultExecutionOrder(-125)]
    public sealed class BeforeDemolitionFourStudentsFlow : MonoBehaviour
    {
        public const string ChapterId = "bd.four-students";
        public const string MartaNotebookId = "bd.marta.notebook", CaioCapId = "bd.caio.cap";
        public const string LuanLunchId = "bd.luan.lunch", BiaTapeId = "bd.bia.tape";
        public const string BiaTapeHeardId = "bd.bia.tape.heard", StairKeyId = "bd.stairs.key";
        public const string GatheredSignal = "bd.four.gathered", KeySignal = "bd.four.key", UpstairsSignal = "bd.four.upstairs";
        public const string IntercomSequenceId = "bd.four.intercom";
        public static readonly string[] StudentItemIds = { MartaNotebookId, CaioCapId, LuanLunchId, BiaTapeId };

        public GameSession game;
        public SchoolInspectionCatalog items;
        public InteractiveDoor[] roomDoors = Array.Empty<InteractiveDoor>();
        public InteractiveDoor stairDoor;
        public InteractiveDoor[] rampDoors = Array.Empty<InteractiveDoor>();
        public GameObject[] roomBoards = Array.Empty<GameObject>();
        public GameObject[] studentVisuals = new GameObject[4];
        public GameObject stairKeyVisual;
        public BoxCollider upstairsEntry, upstairsStairTop;
        public GameObject upstairsBackBlock;
        public Transform intercomSpeaker;
        public Bounds intercomArrival=new Bounds(new Vector3(-1,1,52),new Vector3(18,5,18));
        public bool IntercomArrivalReached=>game&&game.player&&intercomArrival.Contains(game.player.transform.position);
        public Transform caioKnockAnchor;
        public SoundSet caioKnock;
        public Light caioLight;
        public GameObject[] appearanceVisuals = new GameObject[4];
        public Transform[] appearanceAnchors = new Transform[4], movedChairs = new Transform[4];

        NarrativeDirector subscribedDirector;
        Inventory subscribedInventory;
        InspectionManager subscribedInspection;
        readonly Vector3[] chairOriginal = new Vector3[4], chairChanged = new Vector3[4];
        bool dirty = true, pendingKnock, pendingTape, lightReserved, lightWasEnabled, sequenceSubscribed;
        float knockRemaining, appearanceAge;
        int activeAppearance = -1;
        AudioSource knockSource;
        AudioClip knockClip;
        AudioSource biaSource;
        AudioClip biaClip;
        string biaPreviewCaption;
        string lastObjective;

        NarrativeDirector Director => game ? game.narrative : null;
        Inventory Inventory => game && game.interaction ? game.interaction.inventory : null;
        Camera PlayerCamera => game && game.player && game.player.view ? game.player.view.playerCamera : null;
        bool Ready => Director && Director.IsInitialized && BeforeDemolitionCampaign.Is(Director.book);
        public bool IsActive => Ready && Director.Progress.chapterId == ChapterId;
        public bool TapeHeard => Owns(BiaTapeHeardId);
        public bool IntercomCompleted => Ready && Director.HasSequence(IntercomSequenceId);
        public bool Gathered => Ready && Director.HasSignal(GatheredSignal);
        public bool HasStairKey => Owns(StairKeyId);

        void Awake()
        {
            using(LoadingMetrics.Measure("FourStudents.Awake")){
            if (!game) game = GetComponent<GameSession>();
            if (!game) game = GameSession.Instance;
            for (int i = 0; i < 4; i++)
                if (At(movedChairs, i))
                {
                    chairOriginal[i] = movedChairs[i].position;
                    chairChanged[i] = chairOriginal[i] + movedChairs[i].right * .35f;
                }
        
            }
        }

        void OnEnable()
        {
            subscribedDirector = Director;
            if (subscribedDirector) subscribedDirector.Initialized += Initialize;
            subscribedInventory = Inventory;
            if (subscribedInventory) subscribedInventory.Changed += InventoryChanged;
            if (game) game.StateChanged += StateChanged;
        }

        void Start() { if (Ready) Initialize(); }

        void Initialize()
        {
            BindRecording();
            RestoreWorldSilently();
        }

        void BindRecording()
        {
            if (sequenceSubscribed != IsActive && subscribedDirector)
            {
                if (IsActive) subscribedDirector.SequenceCompleted += SequenceCompleted;
                else subscribedDirector.SequenceCompleted -= SequenceCompleted;
                sequenceSubscribed = IsActive;
            }
            var manager = IsActive ? InspectionManager.Instance : null;
            if (subscribedInspection == manager) return;
            if (subscribedInspection) subscribedInspection.RecordingCompleted -= RecordingCompleted;
            subscribedInspection = manager;
            if (subscribedInspection) subscribedInspection.RecordingCompleted += RecordingCompleted;
        }

        public void RestoreWorldSilently()
        {
            CleanupTransients();
            ApplyWorld();
            for (int i = 0; i < 4; i++)
                if (At(movedChairs, i)) movedChairs[i].position = Owns(EventId(i)) ? chairChanged[i] : chairOriginal[i];
            dirty = true; lastObjective = null;
        }

        bool AtOrAfter(string chapter)
        {
            if (!Ready) return false;
            int first = Director.book.ChapterIndex(chapter), current = Director.book.ChapterIndex(Director.Progress.chapterId);
            return first >= 0 && current >= first;
        }

        void ApplyWorld()
        {
            if (!Ready) return;
            if (upstairsBackBlock) upstairsBackBlock.SetActive(AtOrAfter(BeforeDemolitionTwoVoicesFlow.ChapterId));
            bool available = AtOrAfter(ChapterId);
            if (available)
            {
                foreach (var door in roomDoors)
                {
                    if (!door) continue;
                    DisableRestriction(door); door.requiredKey = ""; door.SetLocked(false);
                    if (door.pairedLeaf) door.pairedLeaf.requiredKey = "";
                }
                foreach (var board in roomBoards) if (board) board.SetActive(false);
            }
            if (AtOrAfter(BeforeDemolitionCampaign.SchoolChapterId))
            {
                ApplyStairLock(stairDoor);
                foreach (var door in rampDoors)
                {
                    ApplyStairLock(door);
                    if (!door || !AtOrAfter(BeforeDemolitionTwoVoicesFlow.ChapterId)) continue;
                    door.SetOpen(false, true);
                    door.requiredKey = "";
                    door.lockedMessage = "A porta da rampa emperrou.";
                    if (door.pairedLeaf)
                    {
                        door.pairedLeaf.requiredKey = "";
                        door.pairedLeaf.lockedMessage = door.lockedMessage;
                    }
                    door.SetLocked(true);
                }
            }
            for (int i = 0; i < 4; i++) if (At(studentVisuals, i)) studentVisuals[i].SetActive(!Owns(StudentItemIds[i]));
            if (stairKeyVisual) stairKeyVisual.SetActive(IsActive && Gathered && IntercomCompleted && !HasStairKey);
        }

        bool AtUpstairsTrigger(BoxCollider volume) => volume && volume.bounds.Contains(game.player.transform.position + Vector3.up * .8f);

        void ApplyStairLock(InteractiveDoor door)
        {
            if (!door) return;
            DisableRestriction(door);
            door.requiredKey = StairKeyId;
            door.lockedMessage = "Trancada. Procure a chave no refeitório.";
            if (door.pairedLeaf)
            {
                door.pairedLeaf.requiredKey = StairKeyId;
                door.pairedLeaf.lockedMessage = door.lockedMessage;
            }
            door.SetLocked(!HasStairKey);
            if (!HasStairKey && door.IsOpen) door.SetOpen(false, true);
        }

        static void DisableRestriction(InteractiveDoor door)
        {
            foreach (var restriction in door.GetComponentsInChildren<SchoolAreaRestriction>(true)) restriction.enabled = false;
            if (door.pairedLeaf)
                foreach (var restriction in door.pairedLeaf.GetComponentsInChildren<SchoolAreaRestriction>(true)) restriction.enabled = false;
        }

        void InventoryChanged() { dirty = true; }
        bool Owns(string id) => Inventory && Inventory.Contains(id);
        static string EventId(int student) => "bd.event.student." + student;
        static T At<T>(T[] values, int index) where T : UnityEngine.Object => values != null && index >= 0 && index < values.Length ? values[index] : null;

        void Update()
        {
            if (!Ready) return;
            BindRecording();
            if (game.IsRestoringCheckpoint) return;
            if (dirty) ApplyWorld();
            if (!IsActive || !game.CanControl) return;
            BeginBiaRecordingIfNear();
            if (pendingTape && Owns(BiaTapeId))
            {
                pendingTape = false;
                Inventory.Add(new InventoryItem { id = BiaTapeHeardId, title = "Fita da Bia ouvida até o fim" });
                dirty = true; Director.ScheduleSave();
            }
            if (Director.NextSignal == GatheredSignal && AllEvidenceCollected() && PhoneProgression.CanAdvance(game,GatheredSignal)) Signal(GatheredSignal);
            if(Gathered&&!IntercomCompleted&&IntercomArrivalReached&&!Director.SequencePlaying)Director.PlaySequence(IntercomSequenceId);
            if (IsActive && Director.NextSignal == UpstairsSignal && HasStairKey && IntercomCompleted
                && game.player && (AtUpstairsTrigger(upstairsStairTop) || AtUpstairsTrigger(upstairsEntry)))
                Signal(UpstairsSignal);
            if (IsActive) AdvanceKnock();
        }

        bool AllEvidenceCollected()
        {
            foreach (string id in StudentItemIds) if (!Owns(id)) return false;
            return TapeHeard;
        }

        void BeginBiaRecordingIfNear()
        {
            const string eventId = "bd.event.bia-preview";
            var visual = At(studentVisuals, 3);
            if (!visual || Owns(eventId) || Director.SequencePlaying || !game.player
                || (visual.transform.position - game.player.transform.position).sqrMagnitude > 16) return;
            var tape = items ? items.Find(BiaTapeId) : null;
            if (!tape) return;
            Inventory.Add(new InventoryItem { id = eventId, title = "O gravador da Bia já estava tocando." });
            biaPreviewCaption = "Gravador — Bia: " + tape.recordingText;
            game.hud?.ShowSubtitle(biaPreviewCaption, tape.captionRecordingSeconds);
            var recording = tape.recording;
            biaClip = recording ? recording.Next() : null;
            biaSource = recording ? VoiceAudioPresentation.PlayInWorld(biaClip, transform, visual.transform.position,
                VoicePlaybackStyle.Tape, true, .6f * recording.Volume, recording.Pitch) : null;
            // A room preview never grants the inspection's full recording proof.
            Director.ScheduleSave();
        }

        void Signal(string id) { if (Director.Signal(id)) dirty = true; }

        public bool CanInteract(BeforeDemolitionFourStudentsAction action, PlayerInteraction player)
        {
            if (!IsActive || !game.CanControl || !player || player != game.interaction || !Inventory) return false;
            int student = (int)action;
            if (student >= 0 && student < 4) return !Owns(StudentItemIds[student]) && items && items.Find(StudentItemIds[student]);
            return action == BeforeDemolitionFourStudentsAction.StairKey && Gathered && IntercomCompleted
                && !HasStairKey && Director.NextSignal == KeySignal && items && items.Find(StairKeyId);
        }

        public void Interact(BeforeDemolitionFourStudentsAction action, PlayerInteraction player)
        {
            if (!CanInteract(action, player)) return;
            int student = (int)action;
            if (student >= 0 && student < 4)
            {
                AddItem(StudentItemIds[student]);
                if (action == BeforeDemolitionFourStudentsAction.Caio) pendingKnock = true;
            }
            else
            {
                AddItem(StairKeyId); Signal(KeySignal);
            }
            ApplyWorld(); Director.ScheduleSave(); dirty = true;
        }

        void AddItem(string id)
        {
            var item = items ? items.Find(id) : null;
            if (item) Inventory.Add(new InventoryItem { id = id, title = item.title, text = item.backText, note = true, inspectionId = id });
        }

        void RecordingCompleted(string id)
        {
            if (!IsActive || id != BiaTapeId || TapeHeard || !Inventory) return;
            // Inspection completion collects the tape; listening alone cannot bypass its original proxy.
            if (Owns(BiaTapeId))
            {
                Inventory.Add(new InventoryItem { id = BiaTapeHeardId, title = "Fita da Bia ouvida até o fim" });
                dirty = true; Director.ScheduleSave();
            }
            else pendingTape = true;
        }

        void SequenceCompleted(string id)
        {
            if (IsActive && id == IntercomSequenceId) { ApplyWorld(); dirty = true; }
        }

        void AdvanceKnock()
        {
            if (pendingKnock)
            {
                pendingKnock = false; knockRemaining = 1.2f;
                if (caioLight) { lightWasEnabled = caioLight.enabled; lightReserved = true; caioLight.enabled = false; }
                Vector3 position = caioKnockAnchor ? caioKnockAnchor.position : game.player.transform.position;
                knockSource = AudioManager.Instance?.Play(caioKnock, position, .18f);
                knockClip = knockSource ? knockSource.clip : null;
                PlayerCamera?.GetComponent<VintageCameraEffect>()?.PulseInterference(.12f, .18f);
            }
            if (knockRemaining <= 0) return;
            knockRemaining -= Time.deltaTime;
            if (knockRemaining <= 0) StopKnock();
        }

        void LateUpdate()
        {
            if (!IsActive) return;
            if (lightReserved && caioLight) caioLight.enabled = false;
            if (dirty && game.CanControl) { RefreshObjective(); dirty = false; }
            if (!PlayerCamera) return;
            var camera = PlayerCamera.transform;
            if (activeAppearance >= 0)
            {
                var anchor = At(appearanceAnchors, activeAppearance);
                if (game.CanControl) appearanceAge += Time.deltaTime;
                if (!anchor || Vector3.Angle(camera.forward, anchor.position - camera.position) < 100 || appearanceAge >= 5)
                    FinishAppearance(true);
                return;
            }
            if (!game.CanControl || Director.SequencePlaying) return;
            for (int i = 0; i < 4; i++)
            {
                var anchor = At(appearanceAnchors, i); var visual = At(appearanceVisuals, i);
                if (!anchor || !visual || Owns(EventId(i))) continue;
                Vector3 delta = anchor.position - camera.position;
                if (delta.sqrMagnitude < 1 || delta.sqrMagnitude > 64 || Vector3.Angle(camera.forward, delta) < 110) continue;
                visual.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
                visual.SetActive(true); activeAppearance = i; appearanceAge = 0;
                Inventory.Add(new InventoryItem { id = EventId(i), title = "Uma cadeira fora do lugar — " + new[] { "Marta", "Caio", "Luan", "Bia" }[i] });
                Director.ScheduleSave(); break;
            }
        }

        void FinishAppearance(bool audible=false)
        {
            if (activeAppearance < 0) return;
            if(audible&&game.CanControl&&!game.IsRestoringCheckpoint){var origin=At(appearanceAnchors,activeAppearance);AudioManager.Instance?.PlayCue("child_vanish",origin?origin.position:transform.position,.035f);}
            if (At(appearanceVisuals, activeAppearance)) appearanceVisuals[activeAppearance].SetActive(false);
            if (At(movedChairs, activeAppearance)) movedChairs[activeAppearance].position = chairChanged[activeAppearance];
            activeAppearance = -1;
        }

        void RefreshObjective()
        {
            var missing = new List<string>();
            if (!Owns(MartaNotebookId)) missing.Add("caderno da Marta (sala de artes)");
            if (!Owns(CaioCapId)) missing.Add("boné do Caio (sanitário masculino)");
            if (!Owns(LuanLunchId)) missing.Add("marmita do Luan (refeitório)");
            if (!Owns(BiaTapeId)) missing.Add("fita da Bia (Informática A)");
            else if (!TapeHeard) missing.Add("ouvir a fita da Bia até o fim (R na inspeção)");
            string text = missing.Count > 0 ? "Investigar os quatro alunos. Falta: " + string.Join("; ", missing) + "."
                : !IntercomCompleted ? "Ir até a parte principal da escola."
                : !HasStairKey ? "Procure a chave no refeitório."
                : "Abrir a grade da escada e subir ao andar de cima.";
            if (text != lastObjective) { game.hud?.ShowObjective(text); lastObjective = text; }
        }

        void StopKnock()
        {
            if (lightReserved && caioLight) caioLight.enabled = lightWasEnabled;
            lightReserved = false; knockRemaining = 0;
            if (knockSource && knockSource.clip == knockClip) knockSource.Stop();
            knockSource = null; knockClip = null;
        }

        void CleanupTransients()
        {
            pendingKnock = false; StopKnock(); FinishAppearance();
            StopBiaPreview();
            foreach (var visual in appearanceVisuals) if (visual) visual.SetActive(false);
        }

        void StopBiaPreview()
        {
            if (biaSource && biaSource.clip == biaClip) biaSource.Stop();
            biaSource = null;
            biaClip = null;
            if (biaPreviewCaption != null && game) game.hud?.ClearSubtitle(biaPreviewCaption);
            biaPreviewCaption = null;
        }

        void StateChanged(GameState state)
        {
            if (state == GameState.Inspecting) StopBiaPreview();
            if (state == GameState.BlackingOut || state == GameState.StoryScreen || state == GameState.GameOver) CleanupTransients();
        }

        void OnDisable()
        {
            if (subscribedDirector)
            {
                subscribedDirector.Initialized -= Initialize;
                subscribedDirector.SequenceCompleted -= SequenceCompleted;
            }
            if (subscribedInventory) subscribedInventory.Changed -= InventoryChanged;
            if (subscribedInspection) subscribedInspection.RecordingCompleted -= RecordingCompleted;
            if (game) game.StateChanged -= StateChanged;
            subscribedDirector = null; subscribedInventory = null; subscribedInspection = null;
            sequenceSubscribed = false;
            CleanupTransients();
        }
    }
}
