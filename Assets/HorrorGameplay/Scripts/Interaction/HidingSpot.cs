// Controla a entrada e a saída dos lugares em que é possível se esconder.

using UnityEngine;

namespace HorrorGame
{
    public sealed class HidingSpot : MonoBehaviour, IInteractable
    {
        [Header("Pose markers")]
        [SerializeField]
        public Transform cameraPose, bodyPose, exitPose;
        [SerializeField]
        public string displayName = "armário";
        [SerializeField]
        public SoundSet enterSound, gaspSound;
        [Header("Breath holding")]
        [SerializeField]
        float breathCapacity = 7, breathRecovery = 2, gaspNoiseRadius = 4;
        PlayerInteraction occupant;
        Vector3 entryPosition;
        float entryHeading, entryPitch, breath, peekYaw;
        bool exhausted;
        public bool Occupied => occupant;
        public float Breath01 => Mathf.Clamp01(breath / Mathf.Max(.1f, breathCapacity));

        public string Prompt(PlayerInteraction player) => occupant ? "Sair  ·  [Alt] Prender respiração  ·  [A/D] Espiar" : "Esconder-se no " + displayName;
        public bool CanInteract(PlayerInteraction player) => cameraPose && (!occupant || occupant == player);
        public void Interact(PlayerInteraction player)
        {
            if (occupant == player)
                Exit();
            else if (!occupant)
                Enter(player);
        }

        void Enter(PlayerInteraction player)
        {
            if (!cameraPose)
                return;
            occupant = player;
            entryPosition = player.motor.transform.position;
            entryHeading = player.motor.transform.eulerAngles.y;
            entryPitch = player.motor.Pitch;
            player.grabber?.Drop();
            player.motor.SetHidden(true);
            if (bodyPose)
                player.motor.transform.position = bodyPose.position;
            player.CurrentHidingSpot = this;
            player.motor.view.OverridePose(cameraPose);
            breath = breathCapacity;
            exhausted = false;
            peekYaw = 0;
            AudioManager.Instance?.Play(enterSound, transform.position, .35f);
        }

        void Update()
        {
            if (!occupant || Time.deltaTime <= 0 || GameSession.Instance && !GameSession.Instance.CanControl)
                return;
            if (exhausted && Breath01 >= .35f)
                exhausted = false;
            bool holding = occupant.input && occupant.input.Breath && !exhausted;
            occupant.motor.HoldingBreath = holding;
            if (holding)
            {
                breath = Mathf.Max(0, breath - Time.deltaTime);
                if (breath <= 0)
                {
                    exhausted = true;
                    occupant.motor.HoldingBreath = false;
                    AudioManager.Instance?.Play(gaspSound, cameraPose.position, .45f, AudioChannel.Voice);
                    NoiseSystem.Emit(cameraPose.position, gaspNoiseRadius, occupant.gameObject, NoiseKind.Breath);
                }
            }
            else
                breath = Mathf.Min(breathCapacity, breath + breathRecovery * Time.deltaTime);
            if (occupant.input)
            {
                peekYaw = Mathf.Clamp(peekYaw + occupant.input.Look.x * .02f, -1, 1);
                occupant.motor.view.Peek(new Vector2(Mathf.Clamp(occupant.input.Move.x + peekYaw, -1, 1), 0));
            }
        }

        public bool Exit(bool force = false)
        {
            if (!occupant)
                return true;
            var player = occupant;
            Vector3 position = exitPose ? exitPose.position : entryPosition;
            float radius = player.motor.Controller.radius;
            Vector3 lower = position + Vector3.up * (radius + .05f), upper = position + Vector3.up * (player.motor.Controller.height - radius);
            if (!force)
            {
                foreach (var c in Physics.OverlapCapsule(lower, upper, radius * .95f, player.motor.collisionMask, QueryTriggerInteraction.Ignore))
                    if (!c.transform.IsChildOf(player.transform))
                    {
                        player.ShowMessage("A saída está bloqueada.");
                        return false;
                    }
            }

            player.CurrentHidingSpot = null;
            player.motor.SetHidden(false);
            player.motor.view.ClearOverride();
            player.motor.Warp(position, entryHeading, entryPitch, false);
            occupant = null;
            return true;
        }

        void OnDisable()
        {
            if (occupant)
                Exit(true);
        }
    }
}
