// Calcula a intensidade da respiração conforme esforço, medo e pausa.

using System;
using UnityEngine;

namespace HorrorGame
{
    /// <summary>Exertion builds during actual running and recovers gradually; a fright has its own envelope.</summary>
    [Serializable]
    public sealed class BreathingResponse
    {
        [SerializeField, Min(.1f)] float sprintBuildSeconds = 2.4f;
        [SerializeField, Min(.1f)] float recoverySeconds = 7;
        [SerializeField, Range(0, 1)] float tensionContribution = .22f;
        float exertion, fear, fearRemaining, fearDuration;
        public float Exertion => exertion;
        public float Demand { get; private set; }

        public void Fright(float intensity, float seconds)
        {
            fear = Mathf.Max(fear * FearEnvelope, Mathf.Clamp01(intensity));
            fearRemaining = fearDuration = Mathf.Max(.1f, seconds);
        }

        float FearEnvelope => fearDuration > 0 ? Mathf.Clamp01(fearRemaining / fearDuration) : 0;

        public float Step(float deltaTime, bool running, float stamina, float tension, bool holdingBreath)
        {
            if (deltaTime > 0)
            {
                exertion = Mathf.MoveTowards(exertion, running ? 1 : 0,
                    deltaTime / Mathf.Max(.1f, running ? sprintBuildSeconds : recoverySeconds));
                fearRemaining = Mathf.Max(0, fearRemaining - deltaTime);
            }
            // Walking and pressing sprint into a wall do not start the exertion layer.
            float effort = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.07f, .9f, exertion));
            float exhaustion = effort * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.3f, 0, stamina));
            Demand = holdingBreath ? 0 : Mathf.Clamp01(Mathf.Max(exhaustion, Mathf.Max(fear * FearEnvelope,
                Mathf.Clamp01(tension) * tensionContribution)));
            return Demand;
        }

        public void Reset()
        {
            exertion = fear = fearRemaining = fearDuration = Demand = 0;
        }
    }
}
