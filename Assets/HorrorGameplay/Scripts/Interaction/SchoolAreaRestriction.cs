// Restringe interações ou áreas que estão fora da rota configurada.

using UnityEngine;

namespace HorrorGame
{
    /// <summary>Secondary rooms stay blocked, but an older checkpoint inside can still leave.</summary>
    public sealed class SchoolAreaRestriction : MonoBehaviour, IInteractable
    {
        public InteractiveDoor door;
        [Tooltip("World-space room interior up to the doorway plane; exclude the public corridor.")]
        public Bounds interior;
        [TextArea] public string blockedMessage = "Emperrada. Há móveis bloqueando a passagem pelo outro lado.";
        [Min(0)] public float exitClearance = .55f;

        bool initialized, exitGranted;
        Quaternion closedRotation, partnerClosedRotation;
        InteractiveDoor partner;

        void Awake() => Initialize();
        void OnEnable()
        {
            if (GameSession.Instance) GameSession.Instance.StateChanged += OnStateChanged;
        }
        void OnDisable()
        {
            if (GameSession.Instance) GameSession.Instance.StateChanged -= OnStateChanged;
        }
        void OnStateChanged(GameState state)
        {
            if (state != GameState.Playing) return;
            exitGranted = false;
            var game = GameSession.Instance;
            if (game && game.player) ApplyRestriction(game.player.transform.position);
        }

        bool Initialize()
        {
            if (initialized) return true;
            if (!door) door = GetComponentInParent<InteractiveDoor>();
            if (!door) return false;
            // The authored secondary leaf is closed before any saved door pose is restored.
            closedRotation = (door.hinge ? door.hinge : door.transform).localRotation;
            if (door.pairedLeaf && door.pairedLeaf != door && door.pairedLeaf.pairedLeaf == door)
            {
                partner = door.pairedLeaf;
                partnerClosedRotation = (partner.hinge ? partner.hinge : partner.transform).localRotation;
            }
            initialized = true;
            return true;
        }

        void LateUpdate()
        {
            var game = GameSession.Instance;
            if (game && game.player) ApplyRestriction(game.player.transform.position);
        }

        void ApplyRestriction(Vector3 position)
        {
            if (!Initialize()) return;
            if (interior.Contains(position)) exitGranted = true;
            else if (exitGranted)
            {
                var threshold = interior;
                threshold.Expand(new Vector3(exitClearance * 2, 0, exitClearance * 2));
                if (!threshold.Contains(position)) exitGranted = false;
            }

            door.requiredKey = "";
            door.lockedMessage = blockedMessage;
            door.SetLocked(!exitGranted);
            if (!exitGranted && door.IsOpen) door.SetOpen(false, ClosedPoseClear());
        }

        bool ClosedPoseClear()
        {
            return ClosedPoseClear(door, closedRotation)
                && (!partner || ClosedPoseClear(partner, partnerClosedRotation));
        }

        static bool ClosedPoseClear(InteractiveDoor target, Quaternion rotation)
        {
            if (!target.leafCollider || target.playerMask.value == 0) return true;
            var hinge = target.hinge ? target.hinge : target.transform;
            var leaf = target.leafCollider;
            Quaternion world = (hinge.parent ? hinge.parent.rotation : Quaternion.identity) * rotation;
            Quaternion delta = world * Quaternion.Inverse(hinge.rotation);
            Vector3 center = hinge.position + delta * (leaf.transform.TransformPoint(leaf.center) - hinge.position);
            Vector3 size = Vector3.Scale(leaf.size, leaf.transform.lossyScale);
            Vector3 half = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) * .5f;
            return !Physics.CheckBox(center, half, delta * leaf.transform.rotation,
                target.playerMask, QueryTriggerInteraction.Ignore);
        }

        public string Prompt(PlayerInteraction player) => door && exitGranted ? door.Prompt(player) : "Verificar " + (door ? door.displayName : "acesso");
        public bool CanInteract(PlayerInteraction player) => door;
        public void Interact(PlayerInteraction player)
        {
            if (player && player.motor) ApplyRestriction(player.motor.transform.position);
            if (door) door.Interact(player);
        }
    }
}
