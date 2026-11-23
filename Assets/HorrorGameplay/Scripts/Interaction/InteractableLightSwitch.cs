// Liga ou desliga luzes ligadas ao interruptor.

using UnityEngine;

namespace HorrorGame
{
    /// <summary>A reusable wall switch. Every scene load starts in its configured state.</summary>
    public sealed class InteractableLightSwitch : MonoBehaviour, IInteractable
    {
        [Header("Circuit")]
        [SerializeField] public Light[] targetLights;
        [SerializeField] public bool startsOn = true;
        [SerializeField, Tooltip("Keep this circuit lit and make the physical switch decorative.")]
        public bool alwaysOn;
        [SerializeField] public string displayName = "luz do escritório";
        [Header("Physical switch")]
        [SerializeField] public Transform rocker;
        [SerializeField, Range(1, 25)] public float rockerAngle = 12;
        [SerializeField, Min(1)] float rockerSpeed = 240;
        [SerializeField] public Renderer[] bulbRenderers;
        [SerializeField, ColorUsage(false, true)] public Color bulbEmission = new Color(.9f, .66f, .38f);
        [Header("Click")]
        [SerializeField] public SoundSet onSound, offSound;
        [SerializeField, Range(0, 1)] public float clickGain = .4f;
        [SerializeField, Min(0)] float interactionCooldown = .15f;

        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock block;
        Quaternion targetRotation;
        float nextInteraction;
        [SerializeField, HideInInspector] bool isOn;
        public bool IsOn => isOn;

        void Awake() => SetState(startsOn, false);

        void Update()
        {
            if (rocker)
                rocker.localRotation = Quaternion.RotateTowards(rocker.localRotation, targetRotation, rockerSpeed * Time.deltaTime);
        }

        public string Prompt(PlayerInteraction player) => alwaysOn ? string.Empty : (IsOn ? "Apagar " : "Acender ") + displayName;
        public bool CanInteract(PlayerInteraction player)
        {
            if (alwaysOn) return false;
            if (targetLights != null)
                foreach (var light in targetLights) if (light) return true;
            return false;
        }

        public void Interact(PlayerInteraction player)
        {
            if (Time.time < nextInteraction || !CanInteract(player)) return;
            nextInteraction = Time.time + interactionCooldown;
            SetState(!IsOn, true);
        }

        /// <summary>Also callable by map events. No save state overrides the chapter's startup state.</summary>
        public void SetState(bool on, bool playSound = false)
        {
            on = alwaysOn || on;
            bool changed = IsOn != on;
            isOn = on;
            if (targetLights != null)
                foreach (var light in targetLights) if (light) light.enabled = on;
            targetRotation = Quaternion.Euler(on ? -rockerAngle : rockerAngle, 0, 0);
            if (rocker && (!Application.isPlaying || !isActiveAndEnabled)) rocker.localRotation = targetRotation;
            if (bulbRenderers != null)
            {
                if (block == null) block = new MaterialPropertyBlock();
                foreach (var renderer in bulbRenderers)
                {
                    if (!renderer) continue;
                    renderer.GetPropertyBlock(block);
                    block.SetColor(Emission, on ? bulbEmission : Color.black);
                    renderer.SetPropertyBlock(block);
                }
            }
            if (changed && playSound && Application.isPlaying && AudioManager.Instance)
                AudioManager.Instance.Play(on ? onSound : offSound, transform.position, clickGain);
        }
    }
}
