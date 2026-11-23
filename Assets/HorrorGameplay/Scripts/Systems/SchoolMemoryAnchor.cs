// Define um ponto usado para recuperar a sanidade.

using UnityEngine;

namespace HorrorGame
{
    /// <summary>A small, finite place where a familiar detail lets João regain his bearings.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolMemoryAnchor : MonoBehaviour
    {
        public BoxCollider shape;
        [Min(.1f)] public float radius = 1.5f;
        [Min(0)] public float recoveryPerSecond = .65f;
        public Light supportingLight;
        public bool requiresLight;

        public bool Contains(Vector3 position)
        {
            if (!isActiveAndEnabled || !Finite(recoveryPerSecond) || recoveryPerSecond <= 0) return false;
            if (requiresLight && (!supportingLight || !supportingLight.isActiveAndEnabled || supportingLight.intensity <= .01f)) return false;
            if (shape)
            {
                if (!shape.gameObject.activeInHierarchy) return false;
                Vector3 local = shape.transform.InverseTransformPoint(position) - shape.center;
                Vector3 half = shape.size * .5f;
                return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
            }
            return Finite(radius) && radius > 0 && (position - transform.position).sqrMagnitude <= radius * radius;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
