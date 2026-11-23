// Coordena a última saída na versão anterior.

using System;
using UnityEngine;

namespace HorrorGame
{
    /// <summary>A physical walk to the exit, an explicitly answered call, and an uncertain last glimpse.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolLastExitFlow : PersistentObject
    {
        public const string PersistentId = "school.final.last-exit";
        public NarrativeDirector director;
        public Transform player;
        public Camera playerCamera;
        public BoxCollider entranceVolume, outsideVolume, phoneVolume;
        public InteractiveDoor[] entranceDoors = Array.Empty<InteractiveDoor>();
        public GameObject ghostGuide, distortionVisuals, phoneVisual, windowSilhouette;
        public Vector3 phoneHandOffset = new Vector3(.23f, -.18f, .46f);
        public Vector3 phoneHandEuler;
        public Transform windowLook;
        public Light windowLight;
        public GameObject memoryAnchor;
        public CollectibleItem[] supplies = Array.Empty<CollectibleItem>();
        public LayerMask observationMask = ~0;
        public float observationAngle = 26, observationDistance = 70;
        [Min(.1f)] public float gazeSeconds = 1.1f;

        [Serializable] sealed class Data
        {
            public int version = 1;
            // Explicit consent to answer survives a save made before the manual sequence's final line.
            public bool callAnswered;
        }
        Data state = new Data();
        NarrativeDirector subscribed;
        float gazeAge;

        public bool IsChapterActive => director && director.IsInitialized && director.Progress.chapterId == "last-exit";
        bool CanAct => IsChapterActive && director.game && director.game.CanControl && !director.SequencePlaying;
        bool CanPresent => IsChapterActive && director.game && Time.timeScale > 0
            && (director.game.State == GameState.Playing || director.game.State == GameState.Cutscene);
        public bool CanAnswerPhone => CanAct && director.NextSignal == "last-exit.final-call"
            && director.HasSignal("last-exit.phone-ringing") && !state.callAnswered;

        void Awake() { if (string.IsNullOrWhiteSpace(persistentId)) persistentId = PersistentId; }
        void OnEnable()
        {
            MakeImmaterial(ghostGuide); MakeImmaterial(windowSilhouette);
            subscribed = director;
            if (!subscribed) return;
            subscribed.Initialized += RestoreWorldSilently;
            subscribed.SignalAccepted += Accepted;
            subscribed.SequenceStarted += SequenceStarted;
            subscribed.SequenceCompleted += SequenceCompleted;
            if (subscribed.IsInitialized) RestoreWorldSilently();
        }

        /// <summary>Apply ownership and the saved exit gate without playing sounds or advancing the story.</summary>
        public void RestoreWorldSilently()
        {
            gazeAge = 0;
            ApplyEntranceState(true);
            if (memoryAnchor) memoryAnchor.SetActive(IsChapterActive);
            foreach (var item in supplies)
                if (item) item.gameObject.SetActive(IsChapterActive && item.CanInteract(null));
            RenderPresentation();
            if (phoneVisual && phoneVisual.activeInHierarchy)
                phoneVisual.GetComponentInChildren<SchoolFinalPhone>(true)?.ResetForRestore();
        }

        void Update() => AdvanceFlow(Time.deltaTime);
        void LateUpdate()
        {
            // Keep this owned model in the chapter group; never reparent or alter the player camera.
            if (!IsChapterActive || !playerCamera || !phoneVisual || !phoneVisual.activeInHierarchy) return;
            phoneVisual.transform.SetPositionAndRotation(playerCamera.transform.TransformPoint(phoneHandOffset),
                playerCamera.transform.rotation * Quaternion.Euler(phoneHandEuler));
        }
        public void AdvanceFlow(float deltaTime)
        {
            RenderPresentation();
            if (!CanAct || deltaTime <= 0) return;
            switch (director.NextSignal)
            {
                case "last-exit.entrance-reached":
                    if (Inside(entranceVolume)) director.Signal("last-exit.entrance-reached");
                    break;
                case "last-exit.outside":
                    // A real exterior volume, rather than a door click or a camera teleport, proves the exit.
                    if (director.HasSignal("last-exit.entrance-reached") && Inside(outsideVolume)) director.Signal("last-exit.outside");
                    break;
                case "last-exit.phone-ringing":
                    if (director.HasSignal("last-exit.outside") && Inside(phoneVolume)) director.Signal("last-exit.phone-ringing");
                    break;
                case "last-exit.final-call":
                    // Never answer automatically. Only resume an interrupted call previously accepted by interaction.
                    if (state.callAnswered) director.PlaySequence("last-exit.final-call");
                    break;
                case "last-exit.last-window":
                    gazeAge = Observes(windowLook, observationDistance) ? gazeAge + deltaTime : 0;
                    if (gazeAge >= gazeSeconds) director.Signal("last-exit.last-window");
                    break;
            }
        }
        public bool AnswerPhone()
        {
            if (!CanAnswerPhone || !director.PlaySequence("last-exit.final-call")) return false;
            state.callAnswered = true;
            director.ScheduleSave();
            return true;
        }

        bool Inside(BoxCollider volume) => player && volume && volume.enabled && volume.gameObject.activeInHierarchy
            && volume.bounds.Contains(player.position + Vector3.up);
        void ApplyEntranceState(bool silent)
        {
            if (!director || !director.IsInitialized) return;
            bool reached = director.HasSignal("last-exit.entrance-reached");
            // The earlier school chapters own these existing doors until the final chapter begins.
            if (!IsChapterActive && !reached) return;
            foreach (var door in entranceDoors)
            {
                if (!door) continue;
                door.requiredKey = "";
                door.SetLocked(!reached);
                if (!reached) door.SetOpen(false, silent);
            }
        }
        void RenderPresentation()
        {
            bool active = IsChapterActive, presenting = CanPresent;
            bool outside = active && director.HasSignal("last-exit.outside");
            bool called = active && director.HasSignal("last-exit.final-call");
            bool ended = active && director.HasSignal("last-exit.ending");
            if (ghostGuide) ghostGuide.SetActive(presenting && !outside);
            if (distortionVisuals) distortionVisuals.SetActive(presenting && !outside);
            if (phoneVisual) phoneVisual.SetActive(active && director.HasSignal("last-exit.phone-ringing") && !called);
            if (windowSilhouette) windowSilhouette.SetActive(presenting && called && !ended);
            if (windowLight) windowLight.enabled = presenting && called && !ended;
        }
        public bool Observes(Transform target, float distance)
        {
            if (!target || !playerCamera) return false;
            Vector3 delta = target.position - playerCamera.transform.position;
            if (delta.magnitude > distance || Vector3.Angle(playerCamera.transform.forward, delta) >= observationAngle) return false;
            return !Physics.Raycast(playerCamera.transform.position, delta.normalized, out var hit,
                Mathf.Max(0, delta.magnitude - .16f), observationMask, QueryTriggerInteraction.Ignore)
                || hit.transform == target || hit.transform.IsChildOf(target);
        }
        void Accepted(string signal)
        {
            if (!signal.StartsWith("last-exit.", StringComparison.Ordinal)) return;
            gazeAge = 0;
            ApplyEntranceState(false);
            RenderPresentation();
        }
        void SequenceStarted(string id)
        {
            if (!IsChapterActive || (id != "last-exit.final-call" && id != "ending")) return;
            director.vintageCamera?.PulseInterference(.1f, .16f);
            RenderPresentation();
        }
        void SequenceCompleted(string id)
        {
            if (id == "last-exit.final-call" || id == "ending") RenderPresentation();
        }
        static void MakeImmaterial(GameObject visual)
        {
            if (!visual) return;
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.detectCollisions = false; }
        }

        public override string CaptureState() => JsonUtility.ToJson(state);
        public override void RestoreState(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            var restored = JsonUtility.FromJson<Data>(json);
            if (restored != null && restored.version == 1) state = restored;
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
            if (ghostGuide) ghostGuide.SetActive(false);
            if (distortionVisuals) distortionVisuals.SetActive(false);
            if (phoneVisual) phoneVisual.SetActive(false);
            if (windowSilhouette) windowSilhouette.SetActive(false);
            if (windowLight) windowLight.enabled = false;
        }
    }
}
