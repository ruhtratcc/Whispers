// Controla a subida e as revelações do antigo capítulo do terraço.

using System;
using UnityEngine;

namespace HorrorGame
{
    /// <summary>The school closes around João; every presence is stationary and immaterial.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolTerraceFlow : PersistentObject
    {
        public const string PersistentId = "school.chapter6.terrace";
        public NarrativeDirector director;
        public Transform player;
        public Camera playerCamera;
        public BoxCollider ascentVolume, terraceVolume, descentVolume;
        public InteractiveDoor terraceDoor;
        public GameObject demonVisual, ghostGuide, childrenVisuals, distortionVisuals;
        public Transform demonLook;
        public Light eventLight;
        public GameObject memoryAnchor;
        public CollectibleItem[] supplies = Array.Empty<CollectibleItem>();
        public LayerMask observationMask = ~0;
        public float observationAngle = 26, observationDistance = 24;
        [Min(.1f)] public float gazeSeconds = 2.2f, presenceSeconds = 8;

        [Serializable] sealed class Data
        {
            public int version = 1;
            // A restored presence may continue, but one already dismissed never returns.
            public bool demonEligible, demonHidden, demonSeen;
            public float presenceAge;
        }
        Data state = new Data();
        NarrativeDirector subscribed;
        float gazeAge, lightRemaining;

        public bool IsChapterActive => director && director.IsInitialized && director.Progress.chapterId == "terrace";
        bool CanAct => IsChapterActive && director.game && director.game.CanControl && !director.SequencePlaying;
        bool CanPresent => IsChapterActive && director.game && Time.timeScale > 0
            && (director.game.State == GameState.Playing || director.game.State == GameState.Cutscene);

        void Awake() { if (string.IsNullOrWhiteSpace(persistentId)) persistentId = PersistentId; }
        void OnEnable()
        {
            MakeImmaterial(demonVisual); MakeImmaterial(ghostGuide); MakeImmaterial(childrenVisuals);
            subscribed = director;
            if (!subscribed) return;
            subscribed.Initialized += RestoreWorldSilently;
            subscribed.SignalAccepted += Accepted;
            subscribed.SequenceStarted += SequenceStarted;
            subscribed.SequenceCompleted += SequenceCompleted;
            if (subscribed.IsInitialized) RestoreWorldSilently();
        }

        /// <summary>Restore chapter-owned state without signals, interference or transient light flashes.</summary>
        public void RestoreWorldSilently()
        {
            gazeAge = lightRemaining = 0;
            ApplyDoorState(true);
            if (memoryAnchor) memoryAnchor.SetActive(IsChapterActive);
            foreach (var item in supplies)
                if (item) item.gameObject.SetActive(IsChapterActive && item.CanInteract(null));
            RenderPresentation();
        }

        void Update() => AdvanceFlow(Time.deltaTime);
        public void AdvanceFlow(float deltaTime)
        {
            AdvancePresentation(deltaTime);
            if (!CanAct || deltaTime <= 0) return;
            switch (director.NextSignal)
            {
                case "terrace.ascent-started":
                    if (Inside(ascentVolume)) director.Signal("terrace.ascent-started");
                    break;
                case "terrace.entered":
                    // The volume is beyond the physical roof doorway. Opening a door alone is insufficient.
                    if (director.HasSignal("terrace.voices-finished") && terraceDoor && terraceDoor.IsOpen
                        && Inside(terraceVolume)) director.Signal("terrace.entered");
                    break;
                case "terrace.door-closed":
                    // Wait until the capsule has left the entire hinge sweep before closing behind João.
                    if (Inside(terraceVolume) && DoorwayClear()) director.Signal("terrace.door-closed");
                    break;
                case "terrace.cycle-revelation":
                    // These manual sequences have no intermediate milestone. NextSignal is their durable resume gate.
                    director.PlaySequence("terrace.truth");
                    break;
                case "terrace.final-chase":
                    // Legacy story ID: the environment unravels, never a chase or an enemy attack.
                    director.PlaySequence("terrace.unravel");
                    break;
                case "terrace.descent-exit":
                    if (Inside(descentVolume)) director.Signal("terrace.descent-exit");
                    break;
            }
        }

        bool Inside(BoxCollider volume) => player && volume && volume.enabled && volume.gameObject.activeInHierarchy
            && volume.bounds.Contains(player.position + Vector3.up);
        bool DoorwayClear()
        {
            if (!player || !terraceDoor) return false;
            Transform hinge = terraceDoor.hinge ? terraceDoor.hinge : terraceDoor.transform;
            float radius = 1.6f;
            if (terraceDoor.leafCollider)
            {
                Bounds bounds = terraceDoor.leafCollider.bounds;
                float centreRadius = Vector3.ProjectOnPlane(bounds.center - hinge.position, Vector3.up).magnitude;
                float halfDiagonal = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
                var capsule = player.GetComponent<CharacterController>();
                radius = Mathf.Max(radius, centreRadius + halfDiagonal + (capsule ? capsule.radius : .4f) + .1f);
            }
            return Vector3.ProjectOnPlane(player.position - hinge.position, Vector3.up).magnitude > radius;
        }

        void ApplyDoorState(bool silent)
        {
            if (!terraceDoor || !director || !director.IsInitialized) return;
            terraceDoor.requiredKey = "";
            // Open during the unravel caption, before returning movement. The leaf must
            // not sweep across João when he immediately turns back towards the stairs.
            if (director.HasSignal("terrace.final-chase") || director.ActiveSequenceId == "terrace.unravel")
            {
                terraceDoor.SetLocked(false);
                terraceDoor.SetOpen(true, silent);
            }
            else if (director.HasSignal("terrace.door-closed"))
            {
                terraceDoor.SetLocked(true);
                terraceDoor.SetOpen(false, silent);
            }
            else
            {
                bool locked = !director.HasSignal("terrace.voices-finished");
                terraceDoor.SetLocked(locked);
                // Once the voices finish, preserve the player's saved open/closed choice.
                if (locked) terraceDoor.SetOpen(false, silent);
            }
        }

        void AdvancePresentation(float deltaTime)
        {
            if (!CanPresent || deltaTime <= 0) { RenderPresentation(); return; }
            lightRemaining = Mathf.Max(0, lightRemaining - deltaTime);
            if (DemonAvailable())
            {
                state.presenceAge += deltaTime;
                bool observed = Observes(demonLook, observationDistance);
                if (observed && !state.demonSeen)
                { state.demonSeen = true; director.ScheduleSave(); }
                gazeAge = observed ? gazeAge + deltaTime : 0;
                if (gazeAge >= gazeSeconds || (state.demonSeen && !observed) || state.presenceAge >= presenceSeconds)
                {
                    state.demonHidden = true;
                    gazeAge = 0;
                    director.ScheduleSave();
                }
            }
            RenderPresentation();
        }
        bool DemonAvailable() => IsChapterActive && state.demonEligible && !state.demonHidden
            && !director.HasSignal("terrace.final-chase");
        void RenderPresentation()
        {
            bool presenting = CanPresent;
            string sequence = director ? director.ActiveSequenceId : "";
            if (demonVisual) demonVisual.SetActive(presenting && DemonAvailable());
            if (childrenVisuals) childrenVisuals.SetActive(presenting
                && (sequence == "terrace.truth" || director.HasSignal("terrace.cycle-revelation"))
                && !director.HasSignal("terrace.descent-exit"));
            if (ghostGuide) ghostGuide.SetActive(presenting && director.HasSignal("terrace.final-chase")
                && !director.HasSignal("terrace.descent-exit"));
            if (distortionVisuals) distortionVisuals.SetActive(presenting
                && (sequence == "terrace.voices" || sequence == "terrace.unravel"));
            if (eventLight) eventLight.enabled = presenting && lightRemaining > 0;
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
            if (!signal.StartsWith("terrace.", StringComparison.Ordinal)) return;
            ApplyDoorState(false);
            if (signal == "terrace.final-chase") state.demonHidden = true;
            RenderPresentation();
        }
        void SequenceStarted(string id)
        {
            if (!IsChapterActive || !id.StartsWith("terrace.", StringComparison.Ordinal)) return;
            if (id == "terrace.unravel") ApplyDoorState(false);
            if (id == "terrace.truth" && !state.demonEligible)
            {
                state.demonEligible = true;
                state.presenceAge = 0;
                director.ScheduleSave();
            }
            lightRemaining = .45f;
            director.vintageCamera?.PulseInterference(.13f, .18f);
            RenderPresentation();
        }
        void SequenceCompleted(string id)
        {
            if (!id.StartsWith("terrace.", StringComparison.Ordinal)) return;
            lightRemaining = 0;
            RenderPresentation();
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
            if (demonVisual) demonVisual.SetActive(false);
            if (ghostGuide) ghostGuide.SetActive(false);
            if (childrenVisuals) childrenVisuals.SetActive(false);
            if (distortionVisuals) distortionVisuals.SetActive(false);
            if (eventLight) eventLight.enabled = false;
        }
    }
}
