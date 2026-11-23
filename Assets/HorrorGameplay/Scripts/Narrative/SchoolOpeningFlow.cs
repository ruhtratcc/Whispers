// Coordena os primeiros eventos no roteiro anterior da escola.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    /// <summary>The school opening, driven only by the existing saved school milestones.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolOpeningFlow : MonoBehaviour
    {
        [Header("Narrative and player")]
        public NarrativeDirector director;
        public Transform player;
        public Camera playerCamera;
        [Header("Entry — volume lies safely inside the door swing")]
        public BoxCollider entryVolume;
        public InteractiveDoor entryDoor;
        [Header("Investigation")]
        public SchoolOpeningClue documentsClue, photographClue, drawingClue;
        public Transform boardAnchor, corridorLookAnchor, ghostAnchor;
        public GameObject boardMessage, ghostVisual;
        [Min(.1f)] public float boardReadDistance = 6;
        [Min(.1f)] public float corridorLookDistance = 14;
        [Min(.1f)] public float boardReturnDistance = 9;
        [Range(1, 80)] public float observationAngle = 24;
        [Min(.1f)] public float ghostApproachDistance = 2;
        public LayerMask observationMask = ~0;
        [Header("Environment — exclude the player's flashlight")]
        public Light[] schoolLights = Array.Empty<Light>();
        public SoundSet entryCloseSound, corridorSlamSound, ghostDisappearSound;
        [Min(0)] public float blackoutDelay = 2;
        [Header("One-time atmosphere hooks; restore never invokes these")]
        public UnityEvent onEntered = new UnityEvent();
        public UnityEvent onCorridorSlam = new UnityEvent();
        public UnityEvent onGhostDisappeared = new UnityEvent();
        public UnityEvent onBlackout = new UnityEvent();

        public bool IsSchoolActive => initialized && director && director.IsInitialized
            && director.Progress.chapterId == "school";
        public bool BlackoutComplete => director && director.HasSignal("school.blackout");
        bool initialized;
        bool[] initialLightStates;
        float blackoutAge;
        NarrativeDirector subscribedDirector;
        InteractiveDoor subscribedDoor, subscribedDoorPartner;

        void OnEnable()
        {
            subscribedDirector = director;
            if (subscribedDirector)
            {
                subscribedDirector.Initialized += RestoreWorldSilently;
                subscribedDirector.SignalAccepted += SignalAccepted;
            }
            subscribedDoor = entryDoor;
            if (subscribedDoor) subscribedDoor.onLockedAttempt.AddListener(EntryLockAttempted);
            subscribedDoorPartner = entryDoor && entryDoor.pairedLeaf && entryDoor.pairedLeaf.pairedLeaf == entryDoor
                ? entryDoor.pairedLeaf : null;
            if (subscribedDoorPartner) subscribedDoorPartner.onLockedAttempt.AddListener(EntryLockAttempted);
            if (director && director.IsInitialized) RestoreWorldSilently();
        }

        public void InitializeEntryOpen()
        {
            if (!IsSchoolActive || !entryDoor || director.HasSignal("school.entered")) return;
            entryDoor.SetLocked(false);
            entryDoor.SetOpen(true, true);
        }

        /// <summary>Rebuild visuals from saved progress, without replaying sounds or scares.</summary>
        public void RestoreWorldSilently()
        {
            initialized = director && director.IsInitialized && director.Progress.chapterId == "school";
            blackoutAge = 0;
            // Completed Chapter 3 visuals must stay collected when Chapter 4 reloads the same scene.
            if (!director || !director.IsInitialized) return;
            CaptureLightingBaseline();
            // The final chapter now owns the entry. Preserve its saved door pose after reopening it.
            if (director.HasSignal("school.entered") && !director.HasSignal("last-exit.entrance-reached"))
            {
                if (entryDoor) { entryDoor.SetLocked(true); entryDoor.SetOpen(false, true); }
            }
            else InitializeEntryOpen();
            ApplyProgressVisuals();
        }

        void CaptureLightingBaseline()
        {
            if (initialLightStates != null && initialLightStates.Length == schoolLights.Length) return;
            initialLightStates = new bool[schoolLights.Length];
            for (int i = 0; i < schoolLights.Length; i++)
                initialLightStates[i] = schoolLights[i] && schoolLights[i].enabled;
        }

        bool CanAct => IsSchoolActive && director.game && director.game.State == GameState.Playing
            && director.game.CanControl && !director.SequencePlaying;
        Inventory PlayerInventory => director ? director.inventory : null;
        bool HasDrawingItem => PlayerInventory && PlayerInventory.Contains("school_drawing");

        void Update()
        {
            if (!CanAct) return;
            if (director.NextSignal == "school.entered" && PlayerInsideEntry()) EnterSchool();
            else if (director.NextSignal == "school.blackboard-seen") ReadBlackboard();
            else if (director.NextSignal == "school.blackboard-vanished") LookBackToCorridor();
            else if (director.NextSignal == "school.ghost-approached") ApproachGhost();
            else if (director.NextSignal == "school.photograph" && PlayerInventory
                && PlayerInventory.Contains("school_photograph"))
                director.PlaySequence("school.photo-inscription");
            else if (director.NextSignal == "school.drawing" && HasDrawingItem)
            {
                // A checkpoint can have the item while its caption was interrupted.
                // Resume that incomplete caption, never the ghost or collection sound.
                director.PlaySequence("school.drawing-inscription");
            }
            else if (director.NextSignal == "school.blackout")
            {
                blackoutAge += Time.deltaTime;
                if (blackoutAge >= blackoutDelay) director.Signal("school.blackout");
            }
        }

        bool PlayerInsideEntry()
        {
            if (!player || !entryVolume || !entryVolume.enabled) return false;
            Vector3 local = entryVolume.transform.InverseTransformPoint(player.position) - entryVolume.center;
            Vector3 half = entryVolume.size * .5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        public bool EnterSchool()
        {
            return CanAct && PlayerInsideEntry() && director.Signal("school.entered");
        }

        void EntryLockAttempted() => VerifyEntryLock();
        public bool VerifyEntryLock()
        {
            return CanAct && entryDoor && entryDoor.locked && director.Signal("school.entry-locked");
        }

        public bool CanReadClue(SchoolOpeningClueKind kind)
        {
            if (!CanAct) return false;
            switch (kind)
            {
                case SchoolOpeningClueKind.Documents: return director.NextSignal == "school.documents-found";
                case SchoolOpeningClueKind.Photograph: return director.NextSignal == "school.photograph";
                case SchoolOpeningClueKind.Drawing: return director.NextSignal == "school.drawing"
                    && director.HasSignal("school.ghost-approached") && !HasDrawingItem;
                default: return false;
            }
        }

        public bool ReadClue(SchoolOpeningClue clue)
        {
            if (!clue || clue.flow != this || !CanReadClue(clue.kind) || !PlayerInventory) return false;
            switch (clue.kind)
            {
                case SchoolOpeningClueKind.Documents:
                    PlayerInventory.Add(new InventoryItem { id = "school_documents", title = clue.title,
                        text = clue.body, note = true });
                    if (!director.Signal("school.documents-found")) return false;
                    if (director.game.hud) director.game.hud.ShowNote(clue.title, clue.body);
                    break;
                case SchoolOpeningClueKind.Photograph:
                    if (!director.PlaySequence("school.photo-inscription")) return false;
                    PlayerInventory.Add(new InventoryItem { id = "school_photograph", title = clue.title,
                        text = clue.body, note = true });
                    break;
                case SchoolOpeningClueKind.Drawing:
                    PlayerInventory.Add(new InventoryItem { id = "school_drawing", title = clue.title,
                        text = clue.body, note = true });
                    if (!director.PlaySequence("school.drawing-inscription")) return false;
                    break;
            }
            clue.SetVisible(false);
            director.ScheduleSave();
            return true;
        }

        public bool ReadBlackboard()
        {
            return CanAct && director.NextSignal == "school.blackboard-seen"
                && Observes(boardAnchor, boardReadDistance)
                && director.PlaySequence("school.blackboard-inscription");
        }

        public bool LookBackToCorridor()
        {
            if (!CanAct || director.NextSignal != "school.blackboard-vanished" || !playerCamera || !boardAnchor
                || Vector3.Distance(playerCamera.transform.position, boardAnchor.position) > boardReturnDistance
                || !Observes(corridorLookAnchor, corridorLookDistance)) return false;
            return director.Signal("school.blackboard-vanished");
        }

        public bool ApproachGhost()
        {
            if (!CanAct || director.NextSignal != "school.ghost-approached" || !player || !ghostAnchor
                || Vector3.Distance(player.position, ghostAnchor.position) > ghostApproachDistance
                || !Observes(ghostAnchor, ghostApproachDistance + 2)) return false;
            return director.Signal("school.ghost-approached");
        }

        public bool Observes(Transform target, float maxDistance)
        {
            if (!target || !playerCamera) return false;
            Vector3 origin = playerCamera.transform.position;
            Vector3 delta = target.position - origin;
            float distance = delta.magnitude;
            if (distance > maxDistance || distance < .01f
                || Vector3.Angle(playerCamera.transform.forward, delta) > observationAngle) return false;
            if (!Physics.Raycast(origin, delta / distance, out var hit, distance, observationMask, QueryTriggerInteraction.Ignore)) return true;
            return hit.transform == target || hit.transform.IsChildOf(target)
                || target.IsChildOf(hit.transform) || hit.distance >= distance - .15f;
        }

        void SignalAccepted(string id)
        {
            if (!IsSchoolActive) return;
            ApplyProgressVisuals();
            switch (id)
            {
                case "school.entered":
                    if (entryDoor) { entryDoor.SetLocked(true); entryDoor.SetOpen(false); }
                    Play(entryCloseSound, entryDoor ? entryDoor.transform.position : transform.position, .45f);
                    onEntered.Invoke();
                    break;
                case "school.blackboard-vanished":
                    Play(corridorSlamSound, corridorLookAnchor ? corridorLookAnchor.position : transform.position, .6f);
                    onCorridorSlam.Invoke();
                    break;
                case "school.ghost-approached":
                    Play(ghostDisappearSound, ghostAnchor ? ghostAnchor.position : transform.position, .25f);
                    onGhostDisappeared.Invoke();
                    break;
                case "school.drawing": blackoutAge = 0; break;
                case "school.blackout": onBlackout.Invoke(); break;
            }
        }

        void ApplyProgressVisuals()
        {
            if (documentsClue) documentsClue.SetVisible(!director.HasSignal("school.documents-found"));
            if (photographClue) photographClue.SetVisible(!director.HasSignal("school.photograph")
                && !(PlayerInventory && PlayerInventory.Contains("school_photograph")));
            if (boardMessage) boardMessage.SetActive(director.HasSignal("school.photograph")
                && !director.HasSignal("school.blackboard-vanished"));
            if (ghostVisual) ghostVisual.SetActive(director.HasSignal("school.blackboard-vanished")
                && !director.HasSignal("school.ghost-approached"));
            if (drawingClue) drawingClue.SetVisible(director.HasSignal("school.ghost-approached")
                && !director.HasSignal("school.drawing") && !HasDrawingItem);
            if (initialLightStates == null) return;
            for (int i = 0; i < schoolLights.Length; i++)
                if (schoolLights[i]) schoolLights[i].enabled = !BlackoutComplete && initialLightStates[i];
        }

        static void Play(SoundSet sound, Vector3 position, float gain)
        {
            if (sound) AudioManager.Instance?.Play(sound, position, gain);
        }

        void OnDisable()
        {
            if (subscribedDirector)
            {
                subscribedDirector.Initialized -= RestoreWorldSilently;
                subscribedDirector.SignalAccepted -= SignalAccepted;
            }
            if (subscribedDoor) subscribedDoor.onLockedAttempt.RemoveListener(EntryLockAttempted);
            if (subscribedDoorPartner) subscribedDoorPartner.onLockedAttempt.RemoveListener(EntryLockAttempted);
            subscribedDirector = null;
            subscribedDoor = null;
            subscribedDoorPartner = null;
            initialized = false;
        }
    }
}
