// Recorta a figura do reflexo para aparecer dentro do vidro.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorGame
{
    [DefaultExecutionOrder(180), DisallowMultipleComponent]
    public sealed class MirrorReflectionMask : MonoBehaviour
    {
        public Renderer glass;
        public GameObject figure;
        public Shader actorShader;
        public Material surfaceMask;
        readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        readonly List<Actor> actors = new List<Actor>();
        sealed class Actor
        {
            public Renderer renderer;
            public Material[] materials;
            public bool enabled, occlusion;
            public ShadowCastingMode shadows;
            public uint layers;
        }

        MeshRenderer mask;
        GameSession game;
        Light reflectionLight;
        // School renderers use bit 0; bit 7 isolates the reflected lamp from the real room.
        const uint ReflectionLayer = 1u << 7;
        public bool IsReady => mask && actors.Count > 0;

        void Awake()
        {
            var filter = glass ? glass.GetComponent<MeshFilter>() : null;
            if (!filter || !filter.sharedMesh || !figure || !actorShader || !surfaceMask)
                return;
            game = FindAnyObjectByType<GameSession>();
            var go = new GameObject("Espelho — máscara apenas no vidro");
            go.layer = glass.gameObject.layer;
            go.transform.SetParent(glass.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            mask = go.AddComponent<MeshRenderer>();
            mask.sharedMaterial = surfaceMask;
            mask.shadowCastingMode = ShadowCastingMode.Off;
            mask.receiveShadows = false;
            mask.allowOcclusionWhenDynamic = false;
            foreach (var renderer in figure.GetComponentsInChildren<Renderer>(true))
            {
                var original = renderer.sharedMaterials;
                var clipped = new Material[original.Length];
                actors.Add(new Actor { renderer = renderer, materials = original, enabled = renderer.enabled, occlusion = renderer.allowOcclusionWhenDynamic, shadows = renderer.shadowCastingMode, layers = renderer.renderingLayerMask });
                for (int i = 0; i < original.Length; i++)
                {
                    var source = original[i];
                    if (!source)
                        continue;
                    if (!copies.TryGetValue(source, out var copy))
                    {
                        copy = new Material(actorShader)
                        {
                            name = source.name + " — somente no espelho",
                            hideFlags = HideFlags.DontSave
                        };
                        copy.CopyPropertiesFromMaterial(source);
                        copy.SetFloat("_ZWrite", 0);
                        copy.SetFloat("_SrcBlend", 1);
                        copy.SetFloat("_DstBlend", 0);
                        copy.SetFloat("_SrcBlendAlpha", 1);
                        copy.SetFloat("_DstBlendAlpha", 0);
                        copy.SetFloat("_Cull", 0);
                        copy.SetFloat("_Surface", 0);
                        copy.SetFloat("_ReceiveShadows", 0);
                        copy.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                        copy.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        copy.renderQueue = 3100;
                        copies[source] = copy;
                    }

                    clipped[i] = copy;
                }

                renderer.sharedMaterials = clipped;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.allowOcclusionWhenDynamic = false;
                renderer.renderingLayerMask = ReflectionLayer;
            }

            if (game)
            {
                reflectionLight = new GameObject("Espelho — luz apenas do reflexo").AddComponent<Light>();
                reflectionLight.transform.SetParent(transform, false);
                reflectionLight.type = LightType.Spot;
                reflectionLight.GetUniversalAdditionalLightData().renderingLayers = ReflectionLayer;
                reflectionLight.shadows = LightShadows.None;
                reflectionLight.enabled = false;
            }
        }

        void OnEnable()
        {
            foreach (var actor in actors)
                if (actor.renderer)
                    actor.renderer.enabled = actor.enabled;
        }

        void LateUpdate()
        {
            if (mask)
                mask.enabled = glass && glass.enabled && glass.gameObject.activeInHierarchy;
            if (!reflectionLight)
                return;
            var torch = game && game.save ? game.save.flashlight : null;
            var source = torch ? torch.lamp : null;
            reflectionLight.enabled = mask && mask.enabled && figure.activeInHierarchy && torch && torch.IsOn && source;
            if (!reflectionLight.enabled)
                return;
            var pose = source.transform;
            reflectionLight.transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (game.equipment && game.equipment.PresentationLight && torch.view)
            {
                reflectionLight.transform.SetPositionAndRotation(torch.view.transform.TransformPoint(new Vector3(.04f, -.035f, .10f)), torch.view.transform.rotation);
            }

            reflectionLight.intensity = source.intensity;
            reflectionLight.color = source.color;
            reflectionLight.range = source.range;
            reflectionLight.spotAngle = source.spotAngle;
            reflectionLight.innerSpotAngle = source.innerSpotAngle;
            reflectionLight.cookie = source.cookie;
        }

        void OnDisable()
        {
            if (mask)
                mask.enabled = false;
            if (reflectionLight)
                reflectionLight.enabled = false;
            foreach (var actor in actors)
                if (actor.renderer)
                    actor.renderer.enabled = false;
        }

        void OnDestroy()
        {
            foreach (var actor in actors)
                if (actor.renderer)
                {
                    actor.renderer.sharedMaterials = actor.materials;
                    actor.renderer.enabled = actor.enabled;
                    actor.renderer.shadowCastingMode = actor.shadows;
                    actor.renderer.allowOcclusionWhenDynamic = actor.occlusion;
                    actor.renderer.renderingLayerMask = actor.layers;
                }

            foreach (var material in copies.Values)
                if (material)
                    Destroy(material);
            if (mask)
                Destroy(mask.gameObject);
            if (reflectionLight)
                Destroy(reflectionLight.gameObject);
        }
    }
}
