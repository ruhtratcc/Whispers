// Toca o efeito de impacto de objetos físicos.

using UnityEngine;

namespace HorrorGame
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ImpactAudio : MonoBehaviour
    {
        [SerializeField]
        public SoundSet sound;
        [SerializeField]
        float minimumSpeed = .7f, fullVolumeSpeed = 6, cooldown = .2f, maxNoiseRadius = 12, fullVolumeImpulse = 45;
        float nextImpact;
        void OnCollisionEnter(Collision collision)
        {
            Vector3 point = transform.position;
            float speed = collision.relativeVelocity.magnitude;
            if (collision.contactCount > 0)
            {
                var contact = collision.GetContact(0);
                point = contact.point;
                // A fast slide along a wall is not a hard strike against it.
                speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            }
            if (speed < minimumSpeed || Time.time < nextImpact)
                return;
            nextImpact = Time.time + cooldown;
            float gain = Mathf.Clamp01(Mathf.Max(Mathf.InverseLerp(minimumSpeed, fullVolumeSpeed, speed), collision.impulse.magnitude / Mathf.Max(.1f, fullVolumeImpulse)));
            AudioManager.Instance?.Play(sound, point, Mathf.Lerp(.15f, 1, gain));
            NoiseSystem.Emit(point, Mathf.Lerp(1, maxNoiseRadius, gain), gameObject, NoiseKind.Impact);
        }
    }
}
