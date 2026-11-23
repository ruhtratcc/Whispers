// Controla as descobertas e transições das memórias no roteiro anterior.

using System;
using UnityEngine;

namespace HorrorGame
{
    /// <summary>Recovered memories and an immaterial child guide; no pursuit, damage or architecture changes.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolMemoriesFlow : PersistentObject
    {
        public const string PersistentId = "school.chapter5.memories";
        public NarrativeDirector director;
        public Transform player;
        public Camera playerCamera;
        public SchoolMemoriesClue firstRecording, secondRecording, photograph;
        public BoxCollider archiveVolume;
        public GameObject ghostGuide, memoryVisuals;
        public GameObject memoryAnchor;
        public CollectibleItem[] supplies = Array.Empty<CollectibleItem>();
        public Transform ghostLook;
        public Light memoryLight;
        public LayerMask observationMask = ~0;
        [Min(.1f)] public float gazeSeconds = 1.1f, memoryDuration = 2.4f;
        public float observationAngle = 26, observationDistance = 12;

        [Serializable] sealed class Data
        {
            public int version = 1;
            // Persist the invitation, not a transient flash. An interrupted arrival resumes even outside the volume.
            public bool archiveEntered;
        }
        Data state = new Data();
        NarrativeDirector subscribed;
        float gazeAge, flashRemaining;
        string flashSequence = "";
        GameObject cachedMemoryGroup;
        Transform[] figures = Array.Empty<Transform>();
        Vector3[] figureOrigins = Array.Empty<Vector3>();

        public bool IsChapterActive => director && director.IsInitialized && director.Progress.chapterId == "memories";
        bool CanAct => IsChapterActive && director.game && director.game.CanControl && !director.SequencePlaying;
        Inventory Inventory => director ? director.inventory : null;

        void Awake() { if (string.IsNullOrWhiteSpace(persistentId)) persistentId = PersistentId; }
        void OnEnable()
        {
            CacheMemoryPoses();
            subscribed = director;
            if (!subscribed) return;
            subscribed.Initialized += RestoreWorldSilently;
            subscribed.SignalAccepted += Accepted;
            subscribed.SequenceStarted += SequenceStarted;
            subscribed.SequenceCompleted += SequenceCompleted;
            if (subscribed.IsInitialized) RestoreWorldSilently();
        }

        /// <summary>Apply saved ownership/progress without replaying a memory or issuing a story signal.</summary>
        public void RestoreWorldSilently()
        {
            gazeAge = 0;
            StopFlash();
            ApplyClueVisibility();
            ApplyGuideVisibility();
            if (memoryAnchor) memoryAnchor.SetActive(IsChapterActive);
            foreach (var item in supplies)
                if (item) item.gameObject.SetActive(IsChapterActive && item.CanInteract(null));
        }

        public bool CanReadClue(SchoolMemoriesClueKind kind)
        {
            if (!CanAct || !Inventory) return false;
            switch (kind)
            {
                case SchoolMemoriesClueKind.RecordingOne:
                    return director.NextSignal == "memories.recording-one-found";
                case SchoolMemoriesClueKind.RecordingTwo:
                    return director.NextSignal == "memories.recording-two-found";
                default:
                    return director.NextSignal == "memories.childhood-recognized";
            }
        }
        public bool ReadClue(SchoolMemoriesClue clue)
        {
            if (!clue || clue.flow != this || !CanReadClue(clue.kind)) return false;
            string noteId;
            if (clue.kind == SchoolMemoriesClueKind.Photograph)
            {
                // Keep the clue available if presentation cannot be accepted.
                if (!director.PlaySequence("memories.revelation")) return false;
                noteId = "memories_group_photo";
            }
            else
            {
                bool first = clue.kind == SchoolMemoriesClueKind.RecordingOne;
                if (!director.Signal(first ? "memories.recording-one-found" : "memories.recording-two-found")) return false;
                noteId = first ? "memory_recording_one" : "memory_recording_two";
                // The accepted milestone queues the existing recording sequence. It resumes through the director on load.
            }
            Inventory.Add(new InventoryItem { id = noteId, title = clue.title, text = clue.body, note = true });
            clue.SetVisible(false);
            director.ScheduleSave();
            return true;
        }

        void Update() => AdvanceFlow(Time.deltaTime);
        public void AdvanceFlow(float deltaTime)
        {
            AdvanceFlash(deltaTime);
            if (!CanAct || deltaTime <= 0) return;
            string next = director.NextSignal;
            if (next == "memories.past-echoes")
            {
                if ((state.archiveEntered || InsideArchive()) && director.PlaySequence("memories.past-echo"))
                {
                    state.archiveEntered = true;
                    director.ScheduleSave();
                }
            }
            else if (next == "memories.childhood-recognized" && Inventory && Inventory.Contains("memories_group_photo"))
            {
                // A manual photograph sequence has no accepted milestone until its last line.
                // The saved note is therefore the durable indication to restart an interrupted revelation.
                director.PlaySequence("memories.revelation");
            }
            else if (next == "memories.ghost-appeared")
            {
                gazeAge = Observes(ghostLook, observationDistance) ? gazeAge + deltaTime : 0;
                if (gazeAge >= gazeSeconds) director.Signal("memories.ghost-appeared");
            }
        }

        bool InsideArchive() => player && archiveVolume && archiveVolume.bounds.Contains(player.position + Vector3.up);
        public bool Observes(Transform target, float distance)
        {
            if (!target || !playerCamera) return false;
            Vector3 delta = target.position - playerCamera.transform.position;
            if (delta.magnitude > distance
                || Vector3.Angle(playerCamera.transform.forward, delta) >= observationAngle) return false;
            return !Physics.Raycast(playerCamera.transform.position, delta.normalized, out var hit,
                Mathf.Max(0, delta.magnitude - .16f), observationMask, QueryTriggerInteraction.Ignore)
                || hit.transform == target || hit.transform.IsChildOf(target);
        }

        void Accepted(string signal)
        {
            if (!signal.StartsWith("memories.", StringComparison.Ordinal)) return;
            gazeAge = 0;
            ApplyClueVisibility();
            ApplyGuideVisibility();
        }
        void ApplyClueVisibility()
        {
            bool active = IsChapterActive;
            firstRecording?.SetVisible(active && !director.HasSignal("memories.recording-one-found")
                && !(Inventory && Inventory.Contains("memory_recording_one")));
            secondRecording?.SetVisible(active && !director.HasSignal("memories.recording-two-found")
                && !(Inventory && Inventory.Contains("memory_recording_two")));
            photograph?.SetVisible(active && !director.HasSignal("memories.childhood-recognized")
                && !(Inventory && Inventory.Contains("memories_group_photo")));
        }
        void ApplyGuideVisibility()
        {
            if (ghostGuide) ghostGuide.SetActive(IsChapterActive && director.HasSignal("memories.childhood-recognized")
                && !director.HasSignal("memories.staircase-indicated"));
        }

        void SequenceStarted(string id)
        {
            if (!IsChapterActive || (id != "memories.past-echo" && id != "memories.revelation")) return;
            StopFlash();
            CacheMemoryPoses();
            flashSequence = id;
            flashRemaining = Mathf.Max(.1f, memoryDuration);
            RenderFlash(true);
        }
        void SequenceCompleted(string id)
        {
            if (id == flashSequence) StopFlash();
            if (id.StartsWith("memories.", StringComparison.Ordinal))
            { ApplyClueVisibility(); ApplyGuideVisibility(); }
        }
        void AdvanceFlash(float deltaTime)
        {
            bool presenting = IsChapterActive && director.game
                && (director.game.State == GameState.Playing || director.game.State == GameState.Cutscene)
                && director.SequencePlaying && director.ActiveSequenceId == flashSequence;
            if (!presenting || deltaTime <= 0 || flashRemaining <= 0)
            {
                // Pausing removes the apparition immediately and preserves its remaining time.
                RenderFlash(false);
                return;
            }
            flashRemaining = Mathf.Max(0, flashRemaining - deltaTime);
            RenderFlash(flashRemaining > 0);
        }
        void CacheMemoryPoses()
        {
            if (!memoryVisuals || cachedMemoryGroup == memoryVisuals) return;
            cachedMemoryGroup = memoryVisuals;
            int count = memoryVisuals.transform.childCount;
            figures = new Transform[count]; figureOrigins = new Vector3[count];
            for (int i = 0; i < count; i++)
            { figures[i] = memoryVisuals.transform.GetChild(i); figureOrigins[i] = figures[i].localPosition; }
        }
        void RenderFlash(bool visible)
        {
            CacheMemoryPoses();
            if (memoryVisuals) memoryVisuals.SetActive(visible);
            if (memoryLight) memoryLight.enabled = visible;
            float progress = visible ? 1 - flashRemaining / Mathf.Max(.1f, memoryDuration) : 0;
            for (int i = 0; i < figures.Length; i++)
                if (figures[i]) figures[i].localPosition = figureOrigins[i] + Vector3.forward * (.70f * progress);
        }
        void StopFlash() { flashSequence = ""; flashRemaining = 0; RenderFlash(false); }

        public override string CaptureState() => JsonUtility.ToJson(state);
        public override void RestoreState(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            var restored = JsonUtility.FromJson<Data>(json);
            if (restored != null && restored.version == 1) state = restored;
            // Initialized follows all persistent restores and applies the world silently.
        }
        void OnDisable()
        {
            if (subscribed)
            {
                subscribed.Initialized -= RestoreWorldSilently;
                subscribed.SignalAccepted -= Accepted;
                subscribed.SequenceStarted -= SequenceStarted;
                subscribed.SequenceCompleted -= SequenceCompleted;
            }
            subscribed = null;
            StopFlash();
        }
    }
}
