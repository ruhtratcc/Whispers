// Executa oscilações de luz configuradas no cenário.

using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    [RequireComponent(typeof(Light))]
    public sealed class FlickerLight : MonoBehaviour
    {
        [SerializeField]
        float minimum = .6f, frequency = 1.5f, dropoutChance = .08f;
        [SerializeField]
        public bool flickerEnabled = true;
        [SerializeField]
        public UnityEvent onBurnout = new UnityEvent();
        Light lamp;
        float original, nextDropout, dropoutUntil;
        bool burnt;
        float seed;
        void Awake()
        {
            lamp = GetComponent<Light>();
            original = lamp.intensity;
            seed = Random.Range(0f, 100f);
            nextDropout = Time.time + Random.Range(2, 8);
        }

        void Update()
        {
            if (!lamp || burnt)
                return;
            if (Time.time >= nextDropout)
            {
                nextDropout = Time.time + Random.Range(1f, 3);
                if (!PresentationSettings.ReduceEffects && Random.value < dropoutChance)
                    dropoutUntil = Time.time + Random.Range(.04f, .16f);
            }

            lamp.intensity = flickerEnabled ? original * Mathf.Lerp(minimum, 1, Mathf.PerlinNoise(Time.time * Mathf.Clamp(frequency,.1f,2), seed)) : original;
            lamp.enabled = PresentationSettings.ReduceEffects || Time.time >= dropoutUntil;
        }

        public void BurnOut()
        {
            if (burnt)
                return;
            burnt = true;
            if (lamp)
                lamp.enabled = false;
            onBurnout.Invoke();
        }

        public void TurnOff()
        {
            burnt = true;
            if (lamp)
                lamp.enabled = false;
        }

        public void TurnOn()
        {
            burnt = false;
            if (lamp)
                lamp.enabled = true;
        }
    }
}
