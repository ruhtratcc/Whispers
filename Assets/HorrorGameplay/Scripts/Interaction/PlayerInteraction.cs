// Identifica o alvo à frente da câmera e encaminha a ação escolhida.

using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame
{
    public sealed class PlayerInteraction : MonoBehaviour
    {
        [SerializeField]
        public InputReader input;
        [SerializeField]
        public Camera view;
        [SerializeField]
        public Inventory inventory;
        [SerializeField]
        public PhysicsGrabber grabber;
        [SerializeField]
        public FirstPersonMotor motor;
        [Header("Aim interaction")]
        [SerializeField]
        float range = 2.6f;
        [SerializeField]
        public LayerMask mask = ~0;
        public IInteractable Target { get; private set; }
        public string CurrentPrompt { get; private set; } = "";
        public bool HasTarget => Target != null;
        public HidingSpot CurrentHidingSpot { get; set; }
        float equipmentBlockedUntil;
        InteractiveDrawer equipmentDrawer;
        public bool EquipmentInteractionBusy => Time.time < equipmentBlockedUntil
            || equipmentDrawer && Mathf.Abs(equipmentDrawer.OpenFraction-(equipmentDrawer.IsOpen?1:0))>.02f;
        readonly List<MonoBehaviour> interactionBehaviours = new List<MonoBehaviour>(8);

        void Update()
        {
            if (GameSession.Instance && !GameSession.Instance.CanControl)
            {
                Target = null;
                CurrentPrompt = "";
                return;
            }

            if (CurrentHidingSpot)
            {
                Target = CurrentHidingSpot;
                CurrentPrompt = CurrentHidingSpot.Prompt(this);
                if (input && input.Interact)
                    CurrentHidingSpot.Interact(this);
                return;
            }

            if (grabber && grabber.Held)
            {
                Target = null;
                CurrentPrompt = "[E / G] Soltar  ·  [clique / botão direito] Arremessar";
                if (input && (input.Drop || input.Interact))
                    grabber.Drop();
                else if (input && (input.Throw || input.Primary))
                    grabber.Throw();
                return;
            }

            Target = null;
            CurrentPrompt = "";
            if (view && Physics.Raycast(view.transform.position, view.transform.forward, out var hit, range, mask, QueryTriggerInteraction.Ignore))
            {
                hit.collider.GetComponentsInParent(false, interactionBehaviours);
                foreach (var behaviour in interactionBehaviours)
                    if (behaviour.isActiveAndEnabled && behaviour is IInteractable candidate && candidate.CanInteract(this))
                    {
                        Target = candidate;
                        break;
                    }
            }

            // Dá uma margem na mira para pegar pilhas pequenas, mesmo com a lanterna descarregada.
            var flashlight = motor ? motor.GetComponent<FlashlightController>() : null;
            if (Target == null && view && flashlight && flashlight.IsAvailable
                && Physics.SphereCast(view.transform.position, .16f, view.transform.forward,
                out var batteryHit, range, mask, QueryTriggerInteraction.Ignore))
            {
                var battery = batteryHit.collider.GetComponentInParent<CollectibleItem>();
                if (battery && battery.kind == CollectibleKind.Battery && battery.CanInteract(this))
                {
                    Vector3 point = batteryHit.collider.bounds.center;
                    Vector3 toBattery = point - view.transform.position;
                    if (Physics.Raycast(view.transform.position, toBattery.normalized, out var visible,
                        toBattery.magnitude + .01f, mask, QueryTriggerInteraction.Ignore)
                        && visible.collider.GetComponentInParent<CollectibleItem>() == battery) Target = battery;
                }
            }

            // Dá uma margem na mira para pegar o grampo. O teste de visibilidade impede pegar através de paredes ou gavetas fechadas.
            if(Target==null&&view&&Physics.SphereCast(view.transform.position,.23f,view.transform.forward,out var pinHit,range+.4f,mask,QueryTriggerInteraction.Ignore))
            {
                var pin=pinHit.collider.GetComponentInParent<LockpickPickup>();
                if(pin&&pin.CanInteract(this))
                {
                    Vector3 delta=pinHit.collider.bounds.center-view.transform.position;
                    if(Physics.Raycast(view.transform.position,delta.normalized,out var clear,delta.magnitude+.01f,mask,QueryTriggerInteraction.Ignore)&&clear.collider.GetComponentInParent<LockpickPickup>()==pin)Target=pin;
                }
            }
            if (Target != null)
            {
                CurrentPrompt = "[E] " + Target.Prompt(this);
                if (input && (input.Interact || input.Primary && (!GameSession.Instance || !GameSession.Instance.equipment || !GameSession.Instance.equipment.ReservesPrimaryClick)))
                {
                    equipmentDrawer=Target as InteractiveDrawer;
                    if(Target is InteractiveDoor door)
                        equipmentBlockedUntil=Time.time+Mathf.Abs(door.openAngle)/Mathf.Max(1,door.speed)+.05f;
                    Target.Interact(this);
                }
            }
        }

        public void ShowMessage(string text) => GameSession.Instance?.hud?.Notify(text);
    }
}
