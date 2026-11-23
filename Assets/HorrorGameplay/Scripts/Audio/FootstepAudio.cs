// Dispara passos e aterrissagens quando João se movimenta.

using UnityEngine;

namespace HorrorGame
{
    [RequireComponent(typeof(FirstPersonMotor))]
    public sealed class FootstepAudio : MonoBehaviour
    {
        [SerializeField]
        public FirstPersonMotor motor;
        [SerializeField]
        public FootstepLibrary library;
        [System.Serializable]
        public sealed class SurfacePatch
        {
            public MeshFilter mesh;
            public FootstepLibrary.Entry footsteps;
            Mesh cached;
            Vector3[] vertices;
            int[] triangles;

            public bool Contains(Vector3 groundPoint)
            {
                if (!mesh || !mesh.gameObject.activeInHierarchy || !mesh.sharedMesh)
                    return false;
                var shape = mesh.sharedMesh;
                var p = mesh.transform.InverseTransformPoint(groundPoint);
                var b = shape.bounds;
                if (Mathf.Abs(p.y - b.center.y) > .06f || p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z)
                    return false;
                if (cached != shape)
                {
                    cached = shape;
                    vertices = shape.vertices;
                    triangles = shape.triangles;
                }
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var a = vertices[triangles[i]];
                    var c = vertices[triangles[i + 1]];
                    var d = vertices[triangles[i + 2]];
                    float area = Cross(c - a, d - a);
                    if (Mathf.Abs(area) < .000001f)
                        continue;
                    float u = Cross(c - p, d - p) / area;
                    float v = Cross(d - p, a - p) / area;
                    if (u >= -.00001f && v >= -.00001f && u + v <= 1.00001f)
                        return true;
                }
                return false;
            }

            static float Cross(Vector3 a, Vector3 b) => a.x * b.z - a.z * b.x;
        }
        [Tooltip("Visual floor patches; keeps collision on the original flat floor.")]
        [SerializeField] public SurfacePatch[] groundPatches;
        [Header("Step distances")]
        [SerializeField]
        float walkStride = .9f, sprintStride = 1.35f, crouchStride = .75f;
        [Header("Loudness and hearing radius")]
        [SerializeField]
        float walkVolume = .5f, sprintVolume = .8f, crouchVolume = .2f;
        [SerializeField]
        float walkRadius = 5, sprintRadius = 10, crouchRadius = 1.7f;
        Vector3 previous;
        float distance, nextStep;
        void Awake()
        {
            if (!motor)
                motor = GetComponent<FirstPersonMotor>();
            previous = transform.position;
        }

        void OnEnable()
        {
            if (!motor)
                motor = GetComponent<FirstPersonMotor>();
            motor.Landed += Land;
            previous = transform.position;
            distance = 0;
        }

        void OnDisable()
        {
            if (motor)
                motor.Landed -= Land;
        }

        void LateUpdate()
        {
            Vector3 delta = transform.position - previous;
            previous = transform.position;
            delta.y = 0;
            if (!motor.CanMove || !motor.Grounded || motor.HorizontalSpeed < .08f || delta.magnitude > 2)
            {
                distance = 0;
                return;
            }

            distance += delta.magnitude;
            float stride = motor.IsCrouched ? crouchStride : motor.IsSprinting ? sprintStride : walkStride;
            stride = Mathf.Max(.1f, stride);
            if (distance >= stride && Time.time >= nextStep)
            {
                distance %= stride;
                Step();
                nextStep = Time.time + .1f;
            }
        }

        FootstepLibrary.Entry Ground()
        {
            if (!library)
                return null;
            if (Physics.SphereCast(transform.position + Vector3.up * .45f, .2f, Vector3.down, out var hit, .7f, motor.collisionMask, QueryTriggerInteraction.Ignore))
                return ResolveGround(hit.collider, hit.point);
            return library.Resolve(null);
        }

        public FootstepLibrary.Entry ResolveGround(Collider ground, Vector3 point)
        {
            if (ground && groundPatches != null)
                foreach (var patch in groundPatches)
                    if (patch != null && patch.footsteps != null && patch.Contains(point))
                        return patch.footsteps;
            return library ? library.Resolve(ground) : null;
        }

        void Step()
        {
            var e = Ground();
            float volume = motor.IsCrouched ? crouchVolume : motor.IsSprinting ? sprintVolume : walkVolume;
            AudioManager.Instance?.Play(e?.steps, transform.position, volume);
            NoiseSystem.Emit(transform.position, motor.IsCrouched ? crouchRadius : motor.IsSprinting ? sprintRadius : walkRadius, gameObject, NoiseKind.Footstep);
        }

        void Land(float velocity)
        {
            // The landing already supplies the foot contact; do not double it with a step.
            distance = 0;
            nextStep = Time.time + .16f;
            var e = Ground();
            float gain = Mathf.Clamp01(velocity / 12);
            AudioManager.Instance?.Play(e != null && e.landing ? e.landing : e?.steps, transform.position, gain);
            NoiseSystem.Emit(transform.position, Mathf.Lerp(3, 13, gain), gameObject, NoiseKind.Landing);
            motor.view?.Shake(.02f * gain, .2f);
        }
    }
}
