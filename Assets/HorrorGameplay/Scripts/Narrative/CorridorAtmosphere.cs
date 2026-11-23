// Monta a apresentação do corredor infinito, com névoa, luz e marcas.

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame
{
    [DefaultExecutionOrder(-120), DisallowMultipleComponent]
    public sealed class CorridorAtmosphere : MonoBehaviour
    {
        [Serializable]
        public sealed class MeshBinding
        {
            public MeshFilter target;
            public Mesh original, outside;
        }

        [Serializable]
        public sealed class Module
        {
            public Transform root;
            public GameObject bufferDoors;
            public Light[] lights = Array.Empty<Light>();
        }

        [Serializable]
        public sealed class Mark
        {
            public Renderer renderer;
            public Material[] variants = Array.Empty<Material>();
            public Transform bufferModule;
            public int threshold;
            public bool deepen;
            [NonSerialized]
            public int applied = -1;
        }

        public GameSession game;
        public BeforeDemolitionTwoVoicesFlow flow;
        public CorridorAtmosphereConfig settings;
        public Transform moduleOrigin;
        public GameObject modulesRoot;
        public Module[] modules = Array.Empty<Module>();
        public MeshBinding[] bindings = Array.Empty<MeshBinding>();
        public Mark[] marks = Array.Empty<Mark>();
        public Light[] suppressedLights = Array.Empty<Light>();
        public GameObject[] suppressedObjects = Array.Empty<GameObject>();
        public bool IsApplied { get; private set; }
        public int DeferredMarks { get; private set; }

        bool fog;
        FogMode fogMode;
        float density, start, end, far, flashRange;
        Color fogColor, background, ambient;
        AmbientMode ambientMode;
        CameraClearFlags clear;
        bool presentationActive;
        bool[] lightStates;
        bool[] objectStates;
        Camera camera;
        Light flashlight;
        LightShadows flashlightShadows;
        readonly Plane[] planes = new Plane[6];
        void Awake()
        {
            if (!game)
                game = GetComponent<GameSession>();
            if (!flow)
                flow = GetComponent<BeforeDemolitionTwoVoicesFlow>();
        }

        public bool InCore(Vector3 world)
        {
            if (!settings || !moduleOrigin)
                return false;
            var p = moduleOrigin.InverseTransformPoint(world);
            return Mathf.Abs(p.x) < settings.corridorHalfWidth && Mathf.Abs(p.y) < 2.5f;
        }

        public static float WrapDelta(float coordinate, float length) => length > 0 && (coordinate < -.002f || coordinate >= length) ? Mathf.Floor(coordinate / length) * length : 0;
        void Update()
        {
            bool active = settings && flow && flow.IsActive && !flow.Escaped && game && game.player && moduleOrigin;
            if (!active)
            {
                Restore();
                return;
            }

            if (!presentationActive)
                BeginPresentation();
            if (!IsApplied)
            {
                var feet = game.player.transform.position;
                float depth = moduleOrigin.InverseTransformPoint(feet).z;
                if (InCore(feet) && depth >= .02f && depth < settings.moduleLength * 2.2f)
                    Apply();
            }

            if (!IsApplied)
                return;
            if (game.CanControl && InCore(game.player.transform.position))
            {
                float z = moduleOrigin.InverseTransformPoint(game.player.transform.position).z;
                float shift = WrapDelta(z, settings.moduleLength);
                if (Mathf.Abs(shift) > .01f && flow.CommitModuleWrap(game.player.transform.position - moduleOrigin.forward * shift))
                    Recycle(shift);
            }

            int revision = flow.TraversalCount;
            DeferredMarks = 0;
            if (camera)
                GeometryUtility.CalculateFrustumPlanes(camera, planes);
            foreach (var mark in marks)
            {
                if (!mark.renderer)
                    continue;
                bool bufferVisible = !mark.bufferModule || Mathf.Abs(moduleOrigin.InverseTransformPoint(mark.bufferModule.position).z) > .1f;
                mark.renderer.enabled = mark.applied >= 0 && bufferVisible;
                if (!bufferVisible)
                    continue;
                int desired = revision >= mark.threshold ? (mark.deepen ? Mathf.Min(revision, mark.variants.Length - 1) : 0) : -1;
                if (desired == mark.applied)
                    continue;
                if (Visible(mark.renderer))
                {
                    DeferredMarks++;
                    continue;
                }

                mark.renderer.enabled = desired >= 0;
                if (desired >= 0 && mark.variants.Length > 0)
                    mark.renderer.sharedMaterial = mark.variants[desired];
                mark.applied = desired;
            }

            for (int i = 0; i < modules.Length; i++)
            {
                var slot = modules[i];
                if (!slot.root)
                    continue;
                int index = Mathf.RoundToInt(moduleOrigin.InverseTransformPoint(slot.root.position).z / settings.moduleLength);
                if (slot.bufferDoors && slot.bufferDoors.activeSelf != (index != 0))
                    slot.bufferDoors.SetActive(index != 0);
                float target = Mathf.Abs(index) <= settings.litNeighbours ? settings.lightIntensity : 0;
                foreach (var lamp in slot.lights)
                {
                    if (!lamp)
                        continue;
                    lamp.intensity = Mathf.MoveTowards(lamp.intensity, target, Time.deltaTime * settings.lightIntensity / Mathf.Max(.1f, settings.lightFadeSeconds));
                    lamp.enabled = lamp.intensity > .001f;
                }
            }
        }

        void LateUpdate()
        {
            if (!presentationActive)
                return;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = settings.fogColor;
            RenderSettings.fogDensity = settings.fogDensity;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.008f, .008f, .008f);
            if (camera)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.farClipPlane = Mathf.Min(far, settings.visibility + 2);
            }

            if (flashlight)
                flashlight.range = Mathf.Min(flashRange, settings.flashlightRange);
        }

        bool Visible(Renderer renderer)
        {
            if (!camera)
                return false;
            var b = renderer.bounds;
            if (b.SqrDistance(camera.transform.position) > settings.visibility * settings.visibility)
                return false;
            return GeometryUtility.TestPlanesAABB(planes, b);
        }

        void Recycle(float shift)
        {
            float span = modules.Length * settings.moduleLength, half = (modules.Length / 2) * settings.moduleLength;
            foreach (var slot in modules)
                if (slot.root)
                {
                    var p = moduleOrigin.InverseTransformPoint(slot.root.position);
                    p.z -= shift;
                    while (p.z < -half - .01f)
                        p.z += span;
                    while (p.z > half + .01f)
                        p.z -= span;
                    slot.root.position = moduleOrigin.TransformPoint(p);
                }
        }

        void BeginPresentation()
        {
            camera = game.player.view ? game.player.view.playerCamera : null;
            flashlight = game.save && game.save.flashlight ? game.save.flashlight.lamp : null;
            fog = RenderSettings.fog;
            fogMode = RenderSettings.fogMode;
            fogColor = RenderSettings.fogColor;
            density = RenderSettings.fogDensity;
            start = RenderSettings.fogStartDistance;
            end = RenderSettings.fogEndDistance;
            ambient = RenderSettings.ambientLight;
            ambientMode = RenderSettings.ambientMode;
            if (camera)
            {
                far = camera.farClipPlane;
                clear = camera.clearFlags;
                background = camera.backgroundColor;
            }

            if (flashlight)
            {
                flashRange = flashlight.range;
                flashlightShadows = flashlight.shadows;
            }

            presentationActive = true;
        }

        void Apply()
        {
            // Static module copies share their seam with the school shell. Avoid casting
            // duplicate flashlight shadows across that join while the modules are active.
            if (flashlight)
                flashlight.shadows = LightShadows.None;
            lightStates = new bool[suppressedLights.Length];
            for (int i = 0; i < suppressedLights.Length; i++)
                if (suppressedLights[i])
                {
                    lightStates[i] = suppressedLights[i].enabled;
                    suppressedLights[i].enabled = false;
                }

            objectStates = new bool[suppressedObjects.Length];
            for (int i = 0; i < suppressedObjects.Length; i++)
                if (suppressedObjects[i])
                {
                    objectStates[i] = suppressedObjects[i].activeSelf;
                    suppressedObjects[i].SetActive(false);
                }

            foreach (var b in bindings)
                if (b.target)
                    b.target.sharedMesh = b.outside;
            int revision = flow.TraversalCount;
            foreach (var m in marks)
                if (m.renderer)
                {
                    m.applied = revision >= m.threshold ? (m.deepen ? Mathf.Min(revision, m.variants.Length - 1) : 0) : -1;
                    m.renderer.enabled = m.applied >= 0 && (!m.bufferModule || Mathf.Abs(moduleOrigin.InverseTransformPoint(m.bufferModule.position).z) > .1f);
                    if (m.applied >= 0 && m.variants.Length > 0)
                        m.renderer.sharedMaterial = m.variants[m.applied];
                }

            if (modulesRoot)
                modulesRoot.SetActive(true);
            IsApplied = true;
        }

        public void Restore()
        {
            if (!presentationActive)
                return;
            if (IsApplied)
            {
                foreach (var b in bindings)
                    if (b.target)
                        b.target.sharedMesh = b.original;
                foreach (var mark in marks)
                    if (mark.renderer)
                        mark.renderer.enabled = false;
                if (modulesRoot)
                    modulesRoot.SetActive(false);
                for (int i = 0; i < suppressedLights.Length; i++)
                    if (suppressedLights[i] && lightStates != null)
                        suppressedLights[i].enabled = lightStates[i];
                for (int i = 0; i < suppressedObjects.Length; i++)
                    if (suppressedObjects[i] && objectStates != null)
                        suppressedObjects[i].SetActive(objectStates[i]);
                IsApplied = false;
            }

            presentationActive = false;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = density;
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = end;
            RenderSettings.ambientLight = ambient;
            RenderSettings.ambientMode = ambientMode;
            if (camera)
            {
                camera.farClipPlane = far;
                camera.clearFlags = clear;
                camera.backgroundColor = background;
            }

            if (flashlight)
            {
                flashlight.range = flashRange;
                flashlight.shadows = flashlightShadows;
            }
        }

        void OnDisable() => Restore();
    }
}
