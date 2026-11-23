// Escolhe o som do piso pelo tipo de superfície, material físico ou tag.

using UnityEngine;

namespace HorrorGame
{
    [CreateAssetMenu(menuName = "Whispers/Footstep Library")]
    public sealed class FootstepLibrary : ScriptableObject
    {
        [System.Serializable]
        public sealed class Entry
        {
            public GroundSurface surface;
            public SoundSet steps, landing;
            [Tooltip("Optional fallback when SurfaceType is not present.")]
            public PhysicsMaterial material;
        }

        [SerializeField]
        public Entry[] entries;
        [SerializeField]
        public GroundSurface defaultSurface = GroundSurface.Concrete;
        public Entry Resolve(Collider ground)
        {
            if (entries == null) return null;
            var type = ground ? ground.GetComponentInParent<SurfaceType>() : null;
            GroundSurface wanted = type ? type.surface : defaultSurface;
            if (!type && ground)
            {
                foreach (var e in entries)
                    if (e != null && e.material && e.material == ground.sharedMaterial)
                        return e;
                if (System.Enum.TryParse(ground.tag, true, out GroundSurface tagged))
                    wanted = tagged;
            }

            Entry fallback = null;
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                if (entry.surface == wanted) return entry;
                fallback ??= entry;
            }
            return fallback;
        }
    }
}
