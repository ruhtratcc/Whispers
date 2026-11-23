// Apresenta partículas de poeira no feixe da lanterna.

using UnityEngine;

namespace HorrorGame
{
    /// <summary>Small particles visible only in João's lit beam; no physics or lights.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolFlashlightDust : MonoBehaviour
    {
        public FlashlightController flashlight;
        public ParticleSystem particles;
        bool emitting;
        void Update()
        {
            bool wanted = flashlight && flashlight.IsOn && GameSession.Instance && GameSession.Instance.CanControl;
            if (!particles || wanted == emitting) return;
            emitting = wanted;
            if (emitting) particles.Play();
            else particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void OnDisable()
        {
            emitting=false;
            if (particles) particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
