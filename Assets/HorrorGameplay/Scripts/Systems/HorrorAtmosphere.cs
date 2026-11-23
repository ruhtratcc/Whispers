// Integra sinais de tensão e ajustes atmosféricos.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorGame
{
    public sealed class HorrorAtmosphere : MonoBehaviour
    {
        [SerializeField]
        public Volume volume;
        [SerializeField]
        public FirstPersonMotor player;
        [SerializeField]
        public FlashlightController flashlight;
        [Header("Tension / sanity")]
        [SerializeField]
        public bool tensionEnabled = true;
        [SerializeField, Range(0, 1)]
        public float tension;
        [SerializeField]
        float calmRate = .035f, darknessAdaptSeconds = 9;
        [SerializeField, Range(0, 1)]
        float defaultDarkness = 1;
        [Header("Human breathing")]
        [SerializeField] public bool breathingEnabled = true;
        [SerializeField] BreathingResponse breathingResponse = new BreathingResponse();
        [Header("Eye adaptation limits (exposure stops)")]
        [SerializeField]
        float baseExposure = .15f, adaptationGain = .65f;
        Vignette vignette;
        ColorAdjustments color;
        ChromaticAberration chromatic;
        float adaptation;
        int darkZones;
        public float Tension => tensionEnabled ? tension : 0;

        void Start()
        {
            if (!volume)
                return;
            volume.profile.TryGet(out vignette);
            volume.profile.TryGet(out color);
            volume.profile.TryGet(out chromatic);
        }

        void Update()
        {
            if (Time.deltaTime <= 0)
                return;
            if (tensionEnabled)
                tension = Mathf.MoveTowards(tension, 0, calmRate * Time.deltaTime);
            else
                tension = 0;
            float darkness = darkZones > 0 ? 1 : defaultDarkness;
            float wanted = flashlight && flashlight.IsOn ? 0 : darkness;
            adaptation = Mathf.MoveTowards(adaptation, wanted, Time.deltaTime / Mathf.Max(.1f, darknessAdaptSeconds));
            if (vignette)
                vignette.intensity.value = Mathf.Lerp(.28f, .48f, Tension);
            if (chromatic)
                chromatic.intensity.value = 0; // VintageCameraEffect owns lens artifacts after the prologue.
            if (color)
                color.postExposure.value = baseExposure + adaptationGain * adaptation;
            AudioManager.Instance?.SetTension(Tension);
            bool holding = player && player.HoldingBreath;
            bool running = player && player.IsSprinting && !player.IsHidden && player.HorizontalSpeed > 1;
            float breath = breathingResponse.Step(Time.deltaTime, running,
                player ? player.Stamina01 : 1, Tension, holding);
            AudioManager.Instance?.SetBreathing(breathingEnabled ? breath : 0, holding || !breathingEnabled);
        }

        public void AddTension(float amount) => tension = Mathf.Clamp01(tension + amount);
        public void ReactToScare(float intensity = .6f, float seconds = 5)
        {
            breathingResponse.Fright(intensity, seconds);
        }
        public void SetTension(float value) => tension = Mathf.Clamp01(value);
        public void EnterDarkness() => darkZones++;
        public void LeaveDarkness() => darkZones = Mathf.Max(0, darkZones - 1);
    }
}
