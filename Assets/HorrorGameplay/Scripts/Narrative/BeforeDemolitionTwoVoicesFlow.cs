// Coordena o corredor infinito, a fita e a saída para o espelho.

using System;
using TMPro;
using UnityEngine;

namespace HorrorGame
{
    [Serializable]
    public sealed class BeforeDemolitionTwoVoicesState
    {
        public int version = 1;
        public int loopCount;
        public int wrapCount;
        public bool ValidFields => version == 1 && loopCount >= 0 && loopCount <= 3 && wrapCount>=0 && wrapCount<=100000;

        public static BeforeDemolitionTwoVoicesState Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Checkpoint do corredor vazio.", nameof(json));
            var state = new BeforeDemolitionTwoVoicesState { version = 0, loopCount = -1 };
            JsonUtility.FromJsonOverwrite(json, state);
            return state;
        }
    }

    [DefaultExecutionOrder(-125)]
    public sealed class BeforeDemolitionTwoVoicesFlow : PersistentObject
    {
        public const string ChapterId = "bd.two-voices", StateId = "bd.loop.flow";
        public const string TapeId = "bd.helena.tape", TapeHeardId = "bd.helena.tape.heard";
        public GameSession game;
        public BeforeDemolitionWhistle whistle;
        public BoxCollider loopVolume, correctRoomVolume, mirrorApproach;
        public Transform returnPose, quietAnchor, voiceAnchor;
        public InteractiveDoor sideDoor, mirrorDoor;
        public InspectableItem tapeItem;
        public GameObject[] occupiedVisuals = Array.Empty<GameObject>();
        public Transform[] watchedHeads = Array.Empty<Transform>();
        public TMP_Text[] roomLabels = Array.Empty<TMP_Text>();
        public SoundSet quietSteps;
        public CorridorAtmosphere corridorAtmosphere;
        public GameObject corridorEndBlock;
        public BeforeDemolitionTwoVoicesState State => state;
        public int TraversalCount=>Mathf.Max(state.wrapCount,state.loopCount);
        public event Action<int> LoopRepeated;
        public bool IsActive => Director && Director.IsInitialized && BeforeDemolitionCampaign.Is(Director.book)
            && Director.Progress.chapterId == ChapterId;
        public bool Escaped => Director && Director.HasSignal("bd.loop.escaped");
        public bool TapeHeard => Director && Director.HasSignal("bd.loop.tape");

        BeforeDemolitionTwoVoicesState state = new BeforeDemolitionTwoVoicesState();
        Quaternion[] headRotations;
        InspectionManager listening;
        AudioSource guidance;
        AudioClip guidanceClip;
        bool wasActive, wasInsideLoop, pendingTape, introPending;
        float warpCooldown, guidanceAge;
        NarrativeDirector Director => game ? game.narrative : null;
        Inventory Inventory => game && game.interaction ? game.interaction.inventory : null;

        void Awake()
        {
            persistentId = StateId;
            if (!game) game = GetComponentInParent<GameSession>();
            headRotations = new Quaternion[watchedHeads.Length];
            for (int i = 0; i < watchedHeads.Length; i++)
                if (watchedHeads[i]) headRotations[i] = watchedHeads[i].localRotation;
        }

        void OnEnable()
        {
            if (Director) Director.ChapterInitialized += ChapterInitialized;
            BindInspection();
        }

        void BindInspection()
        {
            var current = game ? game.GetComponent<InspectionManager>() : null;
            if (!current) current = InspectionManager.Instance;
            if (current == listening) return;
            if (listening) listening.RecordingCompleted -= RecordingCompleted;
            listening = current;
            if (listening) listening.RecordingCompleted += RecordingCompleted;
        }

        void ChapterInitialized(string chapter)
        {
            if (chapter == ChapterId) RestoreWorldSilently();
            else
            {
                if (wasActive) Cleanup();
                if (corridorEndBlock) corridorEndBlock.SetActive(false);
                if (chapter == BeforeDemolitionMirrorFlow.ChapterId && mirrorDoor) mirrorDoor.SetLocked(false);
            }
        }

        bool Inside(BoxCollider volume)
        {
            if (!volume || !game || !game.player) return false;
            Vector3 p = volume.transform.InverseTransformPoint(game.player.transform.position + Vector3.up * .8f) - volume.center;
            Vector3 half = volume.size * .5f;
            return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z;
        }

        public void RestoreWorldSilently()
        {
            if (!IsActive) return;
            corridorAtmosphere?.Restore();
            game.GetComponent<DemonVoice>()?.RestoreSilently();
            Debug.Assert(ValidState(state), "Invalid corridor state.", this);
            StopGuidance(); pendingTape = false; warpCooldown = 1.25f; guidanceAge = .8f;
            wasInsideLoop = Inside(loopVolume); wasActive = true;
            introPending = !Director.HasSequence("bd.loop.voice");
            ApplyWorld(true);
        }

        void ApplyWorld(bool silent)
        {
            if (corridorEndBlock) corridorEndBlock.SetActive(false);
            if (tapeItem) tapeItem.gameObject.SetActive(IsActive && Escaped && Inventory && !Inventory.Contains(TapeId));
            if (sideDoor) { sideDoor.requiredKey = ""; if (sideDoor.pairedLeaf) sideDoor.pairedLeaf.requiredKey = ""; sideDoor.SetLocked(false); }
            if (mirrorDoor)
            {
                mirrorDoor.requiredKey = "";
                if (mirrorDoor.pairedLeaf) mirrorDoor.pairedLeaf.requiredKey = "";
                mirrorDoor.lockedMessage = "Antes de continuar, preciso ouvir a gravação desta sala até o fim.";
                mirrorDoor.SetLocked(!TapeHeard);
                if (!TapeHeard) mirrorDoor.SetOpen(false, silent);
            }
            for (int i = 0; i < occupiedVisuals.Length; i++) if (occupiedVisuals[i]) occupiedVisuals[i].SetActive(i < state.loopCount);
            string[] names = { "Marta", "Caio", "Luan", "Bia" };
            for (int i = 0; i < roomLabels.Length; i++) if (roomLabels[i])
                roomLabels[i].text = state.loopCount >= 3 ? "João" : state.loopCount == 2 ? "Helena Souza" : names[i % names.Length];
            ResetHeads();
            if (whistle) { whistle.echoAnchor = quietAnchor; whistle.enableEcho = !Escaped; }
        }

        void Update()
        {
            bool active = IsActive;
            if (!active) { if (wasActive) Cleanup(); return; }
            if (!wasActive) RestoreWorldSilently();
            bool corridorStarted = !corridorAtmosphere || corridorAtmosphere.IsApplied;
            if (introPending && corridorStarted && game.CanControl && Director.PlaySequence("bd.loop.voice")) introPending = false;
            BindInspection();
            if (!game.CanControl || (listening && listening.IsCompleting)) return;
            warpCooldown = Mathf.Max(0, warpCooldown - Time.deltaTime);
            if (!Escaped && Director.NextSignal == "bd.loop.escaped" && Inside(correctRoomVolume) && PhoneProgression.CanAdvance(game,"bd.loop.escaped"))
            {
                if (Director.Signal("bd.loop.escaped")) { StopGuidance(); ApplyWorld(false); }
            }
            CommitTape();
            if (TapeHeard && Director.NextSignal == "bd.loop.mirror" && Inside(mirrorApproach)) Director.Signal("bd.loop.mirror");
            if (!IsActive) { Cleanup(); return; }
            bool insideLoop = Inside(loopVolume);
            if (!corridorAtmosphere && !Escaped && insideLoop && !wasInsideLoop && warpCooldown <= 0 && returnPose && state.loopCount < 3)
            {
                state.loopCount++;
                LoopRepeated?.Invoke(state.loopCount);
                game.player.Warp(returnPose.position, returnPose.eulerAngles.y, 0);
                Physics.SyncTransforms(); warpCooldown = 1.25f; guidanceAge = .2f;
                ApplyWorld(false); Director.PlaySequence("bd.loop.voice"); Director.PlaySequence("bd.loop.phone"); Director.ScheduleSave();
                insideLoop = Inside(loopVolume);
            }
            // Three repetitions are enough; the side room still requires actual entry to progress.
            // An entry during the brief cooldown remains pending instead of being lost forever.
            wasInsideLoop = insideLoop && (warpCooldown <= 0 || wasInsideLoop);
            if (corridorStarted && !Escaped && quietAnchor && !Director.SequencePlaying)
            {
                guidanceAge -= Time.deltaTime;
                if (guidanceAge <= 0)
                {
                    StopGuidance(); guidance = AudioManager.Instance?.Play(quietSteps, quietAnchor.position, .13f, AudioChannel.SFX, true);
                    guidanceClip = guidance ? guidance.clip : null; guidanceAge = 8;
                }
            }
            UpdateHeads();
        }

        void RecordingCompleted(string id)
        {
            if (IsActive && Escaped && id == TapeId && !TapeHeard) pendingTape = true;
        }

        public bool CommitModuleWrap(Vector3 destination)
        {
            if(!IsActive||Escaped||!game.CanControl||!corridorAtmosphere)return false;
            state.wrapCount=Mathf.Min(100000,TraversalCount+1);state.loopCount=Mathf.Min(3,state.wrapCount);
            LoopRepeated?.Invoke(state.loopCount);
            game.player.WrapTo(destination);
            Physics.SyncTransforms();guidanceAge=.2f;ApplyWorld(false);
            game.GetComponent<DemonVoice>()?.QueueCorridor();Director.ScheduleSave();return true;
        }

        void CommitTape()
        {
            if (!Escaped || TapeHeard || !Inventory || !Inventory.Contains(TapeId)
                || (!pendingTape && !Inventory.Contains(TapeHeardId)) || Director.NextSignal != "bd.loop.tape") return;
            // RecordingCompleted precedes ordinary inspection pickup; commit both facts after pickup.
            Inventory.Add(new InventoryItem { id = TapeHeardId, title = "Fita 3 ouvida até o fim" });
            if (Director.Signal("bd.loop.tape")) { pendingTape = false; ApplyWorld(false); }
        }

        void UpdateHeads()
        {
            var camera = game.player && game.player.view ? game.player.view.playerCamera : null;
            for (int i = 0; i < watchedHeads.Length; i++)
            {
                var head = watchedHeads[i];
                if (!head || i >= headRotations.Length) continue;
                bool watched = false;
                if (camera && head.gameObject.activeInHierarchy)
                {
                    Vector3 delta = head.position - camera.transform.position;
                    watched = delta.sqrMagnitude > .01f && delta.sqrMagnitude < 324 && Vector3.Angle(camera.transform.forward, delta) < 12
                        && !Physics.Raycast(camera.transform.position, delta.normalized, delta.magnitude - .05f,
                            ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore);
                }
                head.localRotation = headRotations[i] * Quaternion.Euler(0, watched ? 1 : 0, 0);
            }
        }

        void ResetHeads()
        {
            if (headRotations == null) return;
            for (int i = 0; i < watchedHeads.Length && i < headRotations.Length; i++)
                if (watchedHeads[i]) watchedHeads[i].localRotation = headRotations[i];
        }

        void StopGuidance()
        {
            if (guidance && guidance.clip == guidanceClip) guidance.Stop();
            guidance = null; guidanceClip = null;
        }

        void Cleanup()
        {
            wasActive = false; pendingTape = false; StopGuidance(); ResetHeads();
            for (int i = 0; i < occupiedVisuals.Length; i++) if (occupiedVisuals[i]) occupiedVisuals[i].SetActive(false);
            if (whistle) { whistle.enableEcho = false; whistle.CancelPending(); }
        }

        public static bool ValidState(BeforeDemolitionTwoVoicesState value) => value != null && value.ValidFields;
        public static bool ValidState(string json)
        {
            try { return ValidState(BeforeDemolitionTwoVoicesState.Parse(json)); }
            catch (ArgumentException) { return false; }
        }
        public override string CaptureState() => JsonUtility.ToJson(state);
        public override void RestoreState(string json)
        {
            var restored = BeforeDemolitionTwoVoicesState.Parse(json);
            if (!ValidState(restored)) throw new ArgumentException("Checkpoint do corredor inválido.", nameof(json));
            state = restored; RestoreWorldSilently();
        }

        void OnDisable()
        {
            if (Director) Director.ChapterInitialized -= ChapterInitialized;
            if (listening) listening.RecordingCompleted -= RecordingCompleted;
            listening = null; Cleanup();
        }
#if UNITY_EDITOR
        protected override void OnValidate() { persistentId = StateId; }
#endif
    }
}

