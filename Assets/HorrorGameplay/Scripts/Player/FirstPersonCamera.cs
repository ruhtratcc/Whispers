// Posiciona a câmera, aplica balanço e responde aos controles de visão.

using UnityEngine;

namespace HorrorGame
{
    [DefaultExecutionOrder(100)]
    public sealed class FirstPersonCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] public FirstPersonMotor motor;
        [SerializeField] public Camera playerCamera;
        [Header("Motion")]
        [SerializeField] public bool bobEnabled = true, swayEnabled = true;
        [SerializeField] float walkBob = .013f, sprintBob = .03f, crouchBob = .007f, bobFrequency = 7.5f, breathSway = .004f;
        [SerializeField] float positionSmooth = .035f, lookFollow = 35;
        [Header("Field of view")]
        [SerializeField] float baseFov = 49, sprintFov = 49, fovSpeed = 4;
        [SerializeField, Range(30, 65), Tooltip("Hold Z to look more closely during exploration.")]
        float focusFov = 49;
        [Header("Scare response")]
        [SerializeField, Min(0)] float maximumShake = .12f;
        [SerializeField, Range(0, 5), Tooltip("Maximum roll during a fear impulse, in degrees.")]
        float maximumFearRoll = 2.2f;
        [SerializeField, Range(.08f, .8f)] float fearRecovery = .28f;
        Transform overridePose;
        bool trackOverridePositionImmediately;
        Vector3 worldPosition, positionVelocity, fearOffset, fearVelocity, fearAngles, fearAngularVelocity;
        Quaternion worldRotation;
        float phase, shakeUntil, shakeDuration, shakeStrength;
        Vector2 peek;
        bool initialized;
        int impulseSequence;
        public bool HasOverride => overridePose;
        public float Fear01 => Mathf.Clamp01(fearAngles.magnitude / Mathf.Max(.01f, maximumFearRoll));

        public void OverridePose(Transform pose) => OverridePose(pose, false);
        /// <summary>No carro, a câmera acompanha a posição do banco sem atraso. Nas demais cenas, ela se move aos poucos.</summary>
        public void OverridePose(Transform pose, bool trackPositionImmediately)
        {
            overridePose = pose;
            trackOverridePositionImmediately = trackPositionImmediately;
        }
        public void Peek(Vector2 value) => peek = Vector2.ClampMagnitude(value, 1);
        public void ClearOverride() { overridePose = null; trackOverridePositionImmediately = false; peek = Vector2.zero; }

        public void Shake(float strength, float duration)
        {
            if (!initialized && motor) Snap();
            strength = Mathf.Clamp(strength, 0, maximumShake);
            if (strength <= 0 || duration <= 0) return;
            // Se já há um tremor forte, um tremor mais fraco não o substitui.
            float remaining = Mathf.Max(0, shakeUntil - Time.time);
            float activeStrength = remaining > 0 ? shakeStrength * Mathf.Clamp01(remaining / shakeDuration) : 0;
            shakeStrength = Mathf.Max(activeStrength, strength);
            shakeDuration = Mathf.Max(remaining, duration);
            shakeUntil = Time.time + shakeDuration;
            if (strength < .03f && duration < .35f) return;
            float side = (++impulseSequence & 1) == 0 ? 1 : -1;
            fearOffset = Vector3.ClampMagnitude(fearOffset + new Vector3(side * .5f, .22f, -.2f) * strength, maximumShake);
            fearAngles += new Vector3(-strength * 11, side * strength * 7, side * strength * 28);
            fearAngles = Vector3.ClampMagnitude(fearAngles, maximumFearRoll * 1.3f);
            fearAngles.z = Mathf.Clamp(fearAngles.z, -maximumFearRoll, maximumFearRoll);
        }

        public void Snap()
        {
            if (!motor) return;
            worldPosition = motor.transform.TransformPoint(Vector3.up * motor.EyeHeight);
            worldRotation = motor.transform.rotation * Quaternion.Euler(motor.Pitch, 0, 0);
            positionVelocity = fearOffset = fearVelocity = fearAngles = fearAngularVelocity = Vector3.zero;
            shakeStrength = 0; shakeUntil = Time.time; shakeDuration = .01f;
            initialized = true;
            transform.SetPositionAndRotation(worldPosition, worldRotation);
        }

        void LateUpdate()
        {
            if (!motor || !playerCamera) return;
            if (!initialized) Snap();
            var game = GameSession.Instance;
            bool bodyMotion = (!game || game.CanControl) && !overridePose && !motor.IsHidden;
            float dt = Time.deltaTime;
            bool sprinting = Time.timeScale > 0 && bodyMotion && motor.IsSprinting;
            bool focusing = Time.timeScale > 0 && bodyMotion && !sprinting
                && motor.input && motor.input.Focus;
            float targetFov = focusing ? Mathf.Min(baseFov, focusFov)
                : sprinting ? sprintFov : baseFov;
            // Ao pausar, sai do modo de foco. A posição da câmera e o tremor ficam parados.
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov,
                1 - Mathf.Exp(-fovSpeed * (Time.timeScale > 0 ? dt : Time.unscaledDeltaTime)));
            if (Time.timeScale <= 0 || dt <= 0) return;
            Vector3 motion = Vector3.zero;
            bool moving = bodyMotion && motor.Grounded && motor.HorizontalSpeed > .1f;
            if (moving && bobEnabled)
            {
                phase += motor.HorizontalSpeed * dt * bobFrequency;
                float amplitude = motor.IsCrouched ? crouchBob : motor.IsSprinting ? sprintBob : walkBob;
                motion = new Vector3(Mathf.Sin(phase * .5f) * amplitude * .5f, Mathf.Sin(phase) * amplitude, 0);
            }
            else if (bodyMotion && swayEnabled && !motor.HoldingBreath)
                motion = new Vector3(Mathf.Sin(Time.time * .73f) * breathSway, Mathf.Sin(Time.time * 1.2f) * breathSway, 0);

            fearOffset = Vector3.SmoothDamp(fearOffset, Vector3.zero, ref fearVelocity, fearRecovery, Mathf.Infinity, dt);
            fearAngles = Vector3.SmoothDamp(fearAngles, Vector3.zero, ref fearAngularVelocity, fearRecovery, Mathf.Infinity, dt);
            float fade = Time.time < shakeUntil ? Mathf.Clamp01((shakeUntil - Time.time) / shakeDuration) : 0;
            float steadyBreath = motor.IsHidden && motor.HoldingBreath ? .4f : 1;
            Vector3 noise = new Vector3(Mathf.PerlinNoise(Time.time * 21, 1) - .5f,
                Mathf.PerlinNoise(2, Time.time * 23) - .5f, 0) * shakeStrength * 2 * fade;
            Vector3 shake = Vector3.ClampMagnitude(noise + fearOffset, maximumShake) * steadyBreath;
            Vector3 angles = fearAngles * steadyBreath;
            if (PresentationSettings.ReduceEffects) { motion *= .2f; shake *= .2f; angles *= .2f; }
            Vector3 desired = motor.transform.TransformPoint(Vector3.up * motor.EyeHeight + motion + shake);
            Quaternion rotation = motor.transform.rotation * Quaternion.Euler(motor.Pitch, 0, 0);
            if (overridePose)
            {
                desired = overridePose.TransformPoint(new Vector3(peek.x * .055f, 0, 0) + shake);
                rotation = overridePose.rotation * Quaternion.Euler(-peek.y * 3, peek.x * 5, 0);
            }
            rotation *= Quaternion.Euler(angles);
            if (overridePose && trackOverridePositionImmediately)
            {
                // No carro, suavizar a posição mundial faria a câmera ficar para trás.
                // O destino já inclui os pequenos movimentos de medo e de espiar.
                worldPosition = desired;
                positionVelocity = Vector3.zero;
            }
            else
                worldPosition = Vector3.SmoothDamp(worldPosition, desired, ref positionVelocity,
                    overridePose ? .18f : positionSmooth, Mathf.Infinity, dt);
            worldRotation = Quaternion.Slerp(worldRotation, rotation, 1 - Mathf.Exp(-lookFollow * dt));
            transform.SetPositionAndRotation(worldPosition, worldRotation);
        }
    }
}
