// Aplica a apresentação visual e sonora associada à sanidade.

using UnityEngine;

namespace HorrorGame
{
    /// <summary>Optional scene-owned overlays; the ordinary HUD remains readable above these layers.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolSanityView : MonoBehaviour
    {
        public CanvasGroup vignette, blackout;
        [Range(0, .3f)] public float maximumVignette = .16f;
        public void Present(float sanity, float fade)
        {
            Set(vignette, Mathf.Clamp01((50 - sanity) / 50) * maximumVignette);
            Set(blackout, Mathf.Clamp01(fade));
        }
        public void Clear() { Set(vignette, 0); Set(blackout, 0); }
        static void Set(CanvasGroup group, float alpha)
        {
            if (!group) return;
            group.alpha = alpha;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
        void OnDisable() => Clear();
    }
}
