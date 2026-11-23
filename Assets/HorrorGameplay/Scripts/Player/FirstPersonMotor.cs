// Executa movimento, corrida, agachamento, salto e colisões do jogador.

using System;
using UnityEngine;

namespace HorrorGame
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        public InputReader input;
        [SerializeField]
        public FirstPersonCamera view;
        [Header("Movement (metres / seconds)")]
        [SerializeField]
        public float walkSpeed = 2.6f, sprintSpeed = 4.5f, crouchSpeed = 1.25f;
        [SerializeField]
        float acceleration = 9, deceleration = 13, gravity = -24, groundStick = 2.5f;
        [SerializeField]
        public bool allowJump = false;
        [SerializeField]
        float jumpHeight = .8f, slideSpeed = 2;
        [Header("Stance")]
        [SerializeField]
        public float standingHeight = 1.8f, crouchedHeight = 1.05f;
        [SerializeField]
        float stanceSpeed = 5, eyeInset = .16f;
        [SerializeField]
        public LayerMask collisionMask = ~0;
        [Header("Look")]
        [SerializeField]
        float sensitivity = .07f, smoothing = .035f;
        [SerializeField]
        public Vector2 pitchClamp = new Vector2(-80, 80);
        [Header("Stamina")]
        [SerializeField, InspectorName("staminaMax")]
        public float staminaCapacity = 7.2f;
        [SerializeField, InspectorName("drainPorSegundo")]
        public float staminaDrain = 1;
        [SerializeField, InspectorName("regenPorSegundo")]
        public float staminaRecovery = 1.6f;
        [SerializeField, InspectorName("delayAntesDeRegenerar")]
        public float recoveryDelay = .8f;
        [SerializeField]
        public SoundSet exhaustionBreath;
        [Header("Physics pushing")]
        [SerializeField]
        float pushForce = 35, maxPushMass = 70;
        public CharacterController Controller { get; private set; }
        public bool Grounded { get; private set; }
        public bool IsCrouched { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsHidden { get; private set; }
        public bool HoldingBreath { get; set; }
        public float Stamina { get; private set; }
        public float Stamina01 => Stamina / Mathf.Max(.1f, staminaCapacity);
        public float EyeHeight => Controller ? Controller.height - eyeInset : standingHeight - eyeInset;
        public float Pitch { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;

        public event Action<float> Landed;
        Vector3 horizontal;
        float vertical, yaw, targetYaw, targetPitch, yawVelocity, pitchVelocity, recoverAt;
        bool exhausted, wasGrounded;
        Collider[] standingOverlaps = new Collider[16];
        public bool CanMove => !IsHidden && (!GameSession.Instance || GameSession.Instance.CanControl);

        void Awake()
        {
            Controller = GetComponent<CharacterController>();
            Stamina = staminaCapacity;
            yaw = targetYaw = transform.eulerAngles.y;
            Grounded = false;
        }

        void Update()
        {
            if (!CanMove)
            {
                horizontal = Vector3.zero;
                HorizontalSpeed = 0;
                IsSprinting = false;
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0)
                return;
            Look(dt);
            Stance(dt);
            ProbeGround();
            // Limita a entrada para João manter a mesma velocidade quando anda na diagonal.
            Vector2 movement = input ? Vector2.ClampMagnitude(input.Move, 1) : Vector2.zero;
            if (exhausted && Stamina01 >= .2f)
                exhausted = false;
            // Só deixa correr no chão, para a frente, com fôlego e sem estar agachado.
            IsSprinting = input && input.Sprint && !IsCrouched && movement.y > .1f && Grounded && !exhausted && Stamina > 0;
            if (IsSprinting)
            {
                // Gasta fôlego na proporção do tempo que passou neste quadro.
                Stamina = Mathf.Max(0, Stamina - staminaDrain * dt);
                recoverAt = Time.time + recoveryDelay;
                if (Stamina <= 0)
                {
                    exhausted = true;
                    IsSprinting = false;
                    AudioManager.Instance?.Play(exhaustionBreath, transform.position, .2f, AudioChannel.Voice, false);
                }
            }
            else if (Time.time >= recoverAt)
                Stamina = Mathf.Min(staminaCapacity, Stamina + staminaRecovery * dt);
            // Escolhe a velocidade conforme a postura e a corrida.
            float speed = IsCrouched ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            Vector3 wanted = transform.right * movement.x + transform.forward * movement.y;
            if (Grounded && Vector3.Angle(GroundNormal, Vector3.up) <= Controller.slopeLimit)
                wanted = Vector3.ProjectOnPlane(wanted, GroundNormal).normalized * movement.magnitude;
            horizontal = Vector3.MoveTowards(horizontal, wanted * speed, (movement.sqrMagnitude > .01f ? acceleration : deceleration) * dt);
            if (Grounded && vertical < 0)
                vertical = -groundStick;
            if (allowJump && input && input.Jump && Grounded && !IsCrouched)
            {
                vertical = Mathf.Sqrt(jumpHeight * -2 * gravity);
                Grounded = false;
            }

            vertical += gravity * dt;
            float falling = vertical;
            Vector3 slide = Grounded && Vector3.Angle(GroundNormal, Vector3.up) > Controller.slopeLimit ? Vector3.ProjectOnPlane(Vector3.down, GroundNormal) * slideSpeed : Vector3.zero;
            Vector3 before = transform.position;
            // Move a cápsula do personagem e recebe de volta as colisões que aconteceram.
            var flags = Controller.Move((horizontal + slide + Vector3.up * vertical) * dt);
            Grounded = (flags & CollisionFlags.Below) != 0 || Controller.isGrounded;
            if ((flags & CollisionFlags.Above) != 0 && vertical > 0)
                vertical = 0;
            if (Grounded)
            {
                if (!wasGrounded && falling < -3.5f)
                    Landed?.Invoke(-falling);
                vertical = -groundStick;
            }

            var actual = transform.position - before;
            actual.y = 0;
            HorizontalSpeed = actual.magnitude / dt;
            wasGrounded = Grounded;
        }

        void ProbeGround()
        {
            GroundNormal = Vector3.up;
            var origin = transform.position + Vector3.up * (Controller.radius + .08f);
            if (Physics.SphereCast(origin, Controller.radius * .88f, Vector3.down, out var hit, .22f, collisionMask, QueryTriggerInteraction.Ignore))
            {
                GroundNormal = hit.normal;
                Grounded = Controller.isGrounded || hit.distance < .15f;
            }
            else
                Grounded = Controller.isGrounded;
        }

        void Look(float dt)
        {
            if (!input)
                return;
            var delta = input.Look;
            float scale = PlayerPrefs.GetFloat("HorrorMenu.Sensitivity", 1) * sensitivity;
            // O mouse já informa quanto se moveu neste quadro. O analógico informa uma velocidade de giro.
            var device = input.Actions["Gameplay/Look"].activeControl?.device;
            if (device is UnityEngine.InputSystem.Gamepad)
                delta *= 180f * dt / sensitivity;
            targetYaw += delta.x * scale;
            targetPitch = Mathf.Clamp(targetPitch - delta.y * scale * (PlayerPrefs.GetInt("HorrorMenu.InvertY", 0) == 1 ? -1 : 1), pitchClamp.x, pitchClamp.y);
            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, smoothing);
            Pitch = Mathf.SmoothDampAngle(Pitch, targetPitch, ref pitchVelocity, smoothing);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        void Stance(float dt)
        {
            bool crouch = input && input.Crouch;
            if (!crouch && !CanStand())
                crouch = true;
            IsCrouched = crouch;
            float height = Mathf.MoveTowards(Controller.height, crouch ? crouchedHeight : standingHeight, stanceSpeed * dt);
            Controller.height = height;
            Controller.center = Vector3.up * height * .5f;
            Controller.stepOffset = Mathf.Min(.3f, height * .2f);
        }

        public bool CanStand()
        {
            float radius = Mathf.Max(.05f, Controller.radius - .025f);
            Vector3 lower = transform.position + Vector3.up * Mathf.Max(Controller.height - radius, radius);
            Vector3 upper = transform.position + Vector3.up * (standingHeight - radius);
            int count;
            while (true)
            {
                count = Physics.OverlapCapsuleNonAlloc(lower, upper, radius, standingOverlaps, collisionMask, QueryTriggerInteraction.Ignore);
                if (count < standingOverlaps.Length) break;
                // Se a lista de colisões encheu, confira de novo antes de deixar João levantar. O teto pode ter ficado fora da lista.
                Array.Resize(ref standingOverlaps, standingOverlaps.Length * 2);
            }
            for (int i = 0; i < count; i++)
            {
                var col = standingOverlaps[i];
                if (!col.transform.IsChildOf(transform))
                    return false;
            }
            return true;
        }

        public void SetHidden(bool hidden)
        {
            IsHidden = hidden;
            HoldingBreath = false;
            Controller.enabled = !hidden;
            horizontal = Vector3.zero;
            vertical = 0;
        }

        // Ao repetir um trecho do corredor, João mantém a velocidade e o giro que já começou.
        public void WrapTo(Vector3 position)
        {
            bool active = Controller.enabled;
            Controller.enabled = false;
            transform.position = position;
            Controller.enabled = active;
            view?.Snap();
        }

        public void Warp(Vector3 position, float heading, float pitch = 0, bool snapCamera = true)
        {
            bool active = Controller.enabled;
            Controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, heading, 0));
            Controller.enabled = active;
            yaw = targetYaw = heading;
            Pitch = targetPitch = pitch;
            horizontal = Vector3.zero;
            vertical = 0;
            yawVelocity = pitchVelocity = 0;
            if (snapCamera)
                view?.Snap();
        }

        public void RestoreStance(bool crouched)
        {
            IsCrouched = crouched;
            Controller.height = crouched ? crouchedHeight : standingHeight;
            Controller.center = Vector3.up * Controller.height * .5f;
            Controller.stepOffset = Mathf.Min(.3f, Controller.height * .2f);
            view?.Snap();
        }

        public void RestoreStamina(float amount)
        {
            Stamina = Mathf.Clamp(amount, 0, staminaCapacity);
            exhausted = Stamina <= 0;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var body = hit.rigidbody;
            if (!body || body.isKinematic || body.mass > maxPushMass || hit.moveDirection.y < -.3f)
                return;
            Vector3 dir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
            body.AddForceAtPosition(dir * pushForce * Time.deltaTime, hit.point, ForceMode.Impulse);
        }

        void OnValidate()
        {
            standingHeight = Mathf.Max(1.2f, standingHeight);
            crouchedHeight = Mathf.Clamp(crouchedHeight, .8f, standingHeight);
            staminaCapacity = Mathf.Max(.1f, staminaCapacity);
        }
    }
}

