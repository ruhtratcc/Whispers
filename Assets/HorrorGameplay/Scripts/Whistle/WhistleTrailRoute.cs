// Define os rastros que podem orientar João durante a investigação.

using UnityEngine;

namespace HorrorGame
{
    public sealed class WhistleTrailRoute : MonoBehaviour
    {
        public string chapter, hiddenWhenItemOwned, requiredFlag;
        public WhistleResponder directionGate;
        public Renderer[] marks = System.Array.Empty<Renderer>();
        WhistleSystem owner;
        float age, duration;
        MaterialPropertyBlock block;
        public bool Visible => age > 0;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            Clear();
        }

        public bool Eligible(WhistleContext context) => context.system && context.system.SupernaturalActive && WhistleSense.OnPlayerFloor(context.system, transform.position) && context.system.Chapter == chapter && (!directionGate || directionGate.CanHear(context)) && (string.IsNullOrEmpty(requiredFlag) || context.system.HasFlag(requiredFlag)) && (string.IsNullOrEmpty(hiddenWhenItemOwned) || !context.system.Inventory.Contains(hiddenWhenItemOwned)) && (transform.position - context.position).sqrMagnitude <= context.system.config.range * context.system.config.range;
        public void Show(WhistleSystem s)
        {
            if (PresentationSettings.ReduceEffects)
                return;
            owner = s;
            age = duration = s.config.effectDuration;
        }

        public void Tick(float dt)
        {
            if (PresentationSettings.ReduceEffects)
            {
                Clear();
                return;
            }

            if (age <= 0)
                return;
            if (!owner || !string.IsNullOrEmpty(hiddenWhenItemOwned) && owner.Inventory.Contains(hiddenWhenItemOwned))
            {
                Clear();
                return;
            }

            age = Mathf.Max(0, age - dt);
            for (int i = 0; i < marks.Length; i++)
                if (marks[i])
                {
                    float delay = i * .08f;
                    float alpha = Mathf.Clamp01((duration - age - delay) / .25f) * Mathf.Clamp01(age / .8f);
                    block.SetColor("_SenseColor", owner.config.pathColor);
                    block.SetFloat("_SenseAlpha", alpha * .19f);
                    marks[i].SetPropertyBlock(block);
                    marks[i].enabled = age > 0 && alpha > 0 && WhistleSense.OnPlayerFloor(owner, marks[i].bounds.center);
                }
        }

        public void Clear()
        {
            age = 0;
            for (int i = 0; i < marks.Length; i++)
                if (marks[i])
                    marks[i].enabled = false;
        }
    }
}
