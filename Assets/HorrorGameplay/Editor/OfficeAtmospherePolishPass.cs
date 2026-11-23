// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HorrorGame.Editor
{
    /// <summary>Three shadowed spots, a shallow visual street, a wall light switch and a mesh fedora. No story edits.</summary>
    public static class OfficeAtmospherePolishPass
    {
        const string ScenePath = "Assets/ChapterOneOffice/Scenes/Chapter1Office.unity";
        const string AssetRoot = "Assets/ChapterOneOffice/";
        const string RootName = "Escritório de João de Claus — Capítulo 1";
        const string MeshPrefix = AssetRoot + "Models/OfficeAtmosphere_";
        const string ExteriorName = "Rua vista da janela — cenário visual";
        const string HatName = "Chapéu de João — fedora";
        const string SwitchName = "Interruptor da luz do escritório";
        static readonly Vector3 StreetBulbPosition = new Vector3(4.95f, 2.965f, 1.98f);
        static readonly Vector3 StreetLightPosition = new Vector3(4.95f, 2.91f, 1.98f);
        static readonly Color CeilingColor = new Color(1, .93f, .82f);
        static readonly Color CeilingEmission = new Color(1.35f, 1.25f, 1.08f);
        public const string ReportPath = AssetRoot + "Documentation/Office-atmosphere-polish-report.json";
        static Transform room;
        static string preservedIds;
        static bool changedPipeline;
        static string pipelinePath;
        static Material felt, ribbon, metal, asphalt, pavement, facade, windowGlass, warmGlass, trim, paint, streetBulb, ceilingBulb, switchPlate;

        static void Initialize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Abra Chapter1Office fora do Play Mode antes de aplicar este passe.");
            room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName)?.transform;
            if (!room) throw new InvalidOperationException("Raiz do escritório ausente.");
        }

        public static void Apply()
        {
            Initialize(); preservedIds = InteractionIds(); changedPipeline = false;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Sombras, rua visual e chapéu do escritório");
            Undo.RegisterFullObjectHierarchyUndo(room.gameObject, "Sombras, rua visual e chapéu do escritório");
            EnsurePipelineSupportsShadows();
            ConfigureLights();
            CreateMaterials();
            DressWindowExterior();
            CreateWallSwitch();
            ReplaceCoatWithFedora();
            if (InteractionIds() != preservedIds) throw new InvalidOperationException("IDs de porta, gaveta ou documentos alterados.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(group);
            Debug.Log("Office atmosphere polish complete: three shadowed spots, aligned streetlamp, wall switch and mesh fedora; interaction IDs preserved.");
        }

        static Transform Find(string name) => room.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        static Transform Require(string name) => Find(name) ? Find(name) : throw new InvalidOperationException("Objeto necessário ausente: " + name);
        static string InteractionIds() => string.Join("|", room.GetComponentsInChildren<PersistentObject>(true).Select(p => p.GetType().FullName + ":" + p.persistentId).OrderBy(s => s));
        static Transform Group(string name, Transform parent, Vector3 pos = default, Vector3 angles = default)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false);
            t.localPosition = pos; t.localRotation = Quaternion.Euler(angles); t.gameObject.layer = parent.gameObject.layer; return t;
        }
        static void Remove(Transform parent, params string[] names)
        {
            foreach (var t in parent.Cast<Transform>().Where(c => names.Contains(c.name)).ToArray()) UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        static void EnsurePipelineSupportsShadows()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!pipeline) throw new InvalidOperationException("O escritório precisa da URP ativa para validar sombras.");
            pipelinePath = AssetDatabase.GetAssetPath(pipeline);
            if (pipeline.supportsAdditionalLightShadows && pipeline.additionalLightsRenderingMode == LightRenderingMode.PerPixel) return;
            Undo.RecordObject(pipeline, "Enable the existing additional-light shadow path");
            var serialized = new SerializedObject(pipeline);
            if (!pipeline.supportsAdditionalLightShadows)
            {
                serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
                var any = serialized.FindProperty("m_AnyShadowsSupported"); if (any != null) any.boolValue = true;
            }
            if (pipeline.additionalLightsRenderingMode != LightRenderingMode.PerPixel)
                serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline); changedPipeline = true;
        }

        static void ConfigureSpot(Light light, Vector3 target, float intensity, float range, float outer, float inner, int tier, float bias, float normalBias)
        {
            light.type = LightType.Spot; light.transform.rotation = Quaternion.LookRotation(target - light.transform.position);
            light.intensity = intensity; light.range = range; light.spotAngle = outer; light.innerSpotAngle = inner;
            light.shadows = LightShadows.Soft; light.shadowBias = bias; light.shadowNormalBias = normalBias; light.shadowNearPlane = .06f;
            light.renderMode = LightRenderMode.ForcePixel;
            var data = light.GetComponent<UniversalAdditionalLightData>();
            if (!data) data = light.gameObject.AddComponent<UniversalAdditionalLightData>();
            data.usePipelineSettings = false;
            data.softShadowQuality = SoftShadowQuality.Medium;
            // The URP 17/6000.6 public setter is runtime-only; Inspector serialization is the Editor-supported path.
            var serialized = new SerializedObject(data);
            serialized.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = tier;
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(data); EditorUtility.SetDirty(light);
        }

        static void ConfigureLights()
        {
            foreach (var light in room.GetComponentsInChildren<Light>(true))
                if (light.type == LightType.Point) { light.shadows = LightShadows.None; EditorUtility.SetDirty(light); }
            var desk = Require("Mesa principal — João de Claus");
            var lamp = Require("Luminária de mesa");
            // The CRT otherwise blocks the source completely. Moving only the lamp to the clear writing side
            // keeps the physical source under its diffuser and the beam reaches the actual desk props.
            lamp.localPosition = new Vector3(-.86f, .84f, -.25f);
            var deskLight = Require("Luz quente da mesa").GetComponent<Light>();
            deskLight.transform.localPosition = new Vector3(0, .315f, -.070f);
            deskLight.color = new Color(1, .76f, .46f);
            ConfigureSpot(deskLight, desk.TransformPoint(new Vector3(.08f, .79f, -.02f)), 10.5f, 3.6f, 122, 93,
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium, .08f, .22f);
            var ceiling = Require("Luz suave do teto").GetComponent<Light>();
            var ceilingGlass = Require("Globo de vidro").GetComponent<Renderer>();
            // Place the source outside the opaque proxy mesh. A wide shadowed beam lights the
            // room's objects; the existing point source supplies diffuse light to walls and shelves.
            var emitter = ceilingGlass.bounds.center - Vector3.up * (ceilingGlass.bounds.extents.y + .08f);
            ceiling.transform.position = emitter; ceiling.color = CeilingColor; ceiling.enabled = true;
            ceilingGlass.shadowCastingMode = ShadowCastingMode.Off;
            ConfigureSpot(ceiling, emitter + Vector3.down * 2.5f, 18.0f, 11.0f, 158, 140,
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium, .08f, .22f);
            ceiling.GetComponent<UniversalAdditionalLightData>().softShadowQuality = SoftShadowQuality.Low;
            var fill = Require("Preenchimento discreto").GetComponent<Light>();
            fill.type = LightType.Point;
            fill.transform.position = emitter + Vector3.down * .04f; fill.color = CeilingColor;
            fill.intensity = 7.5f; fill.range = 11; fill.shadows = LightShadows.None; fill.enabled = true;
            fill.renderMode = LightRenderMode.ForcePixel; EditorUtility.SetDirty(fill);
            var exterior = Require("Luz externa entrando pelas persianas").GetComponent<Light>();
            exterior.transform.position = room.TransformPoint(StreetLightPosition);
            exterior.color = new Color(.68f, .78f, 1);
            ConfigureSpot(exterior, room.TransformPoint(new Vector3(1.15f, .4f, 1.05f)), 38, 11, 106, 65,
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh, .10f, .50f);
            exterior.shadowNearPlane = .10f;
            foreach (var t in room.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Tábua de assoalho"))
            {
                var renderer = t.GetComponent<Renderer>(); if (!renderer) continue;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
            }
        }

        static Material Surface(string key, Color color, float smoothness = .1f, bool unlit = false, int grain = 0)
        {
            string path = AssetRoot + "Materials/OfficeAtmosphere_" + key + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing) return existing;
            var material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            material.name = "Office atmosphere " + key; material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
            if (grain != 0)
            {
                var texture = new Texture2D(128, 128, TextureFormat.RGB24, true) { name = key + " texture", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                var data = new Color[128 * 128]; var random = new System.Random(3271 + grain);
                for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                {
                    float n = .87f + Mathf.PerlinNoise(x * .2f, y * .2f) * .19f + (float)random.NextDouble() * .04f;
                    if (grain == 2) n *= y % 4 == 0 ? .80f : 1;
                    data[y * 128 + x] = Color.white * n;
                }
                texture.SetPixels(data); texture.Apply(); AssetDatabase.CreateAsset(texture, AssetRoot + "Textures/OfficeAtmosphere_" + key + ".asset"); material.SetTexture("_BaseMap", texture);
            }
            AssetDatabase.CreateAsset(material, path); return material;
        }

        static void CreateMaterials()
        {
            felt = Surface("Fedora brown felt", new Color(.17f, .135f, .095f), .08f, grain: 1);
            ribbon = Surface("Fedora dark grosgrain", new Color(.043f, .032f, .022f), .17f, grain: 2);
            metal = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "Materials/Aged gunmetal.mat");
            asphalt = Surface("Night asphalt", new Color(.065f, .069f, .075f), .29f, grain: 1);
            pavement = Surface("Concrete sidewalk", new Color(.24f, .26f, .27f), .10f, grain: 1);
            facade = Surface("Opposite old facade", new Color(.265f, .246f, .215f), .10f, grain: 1);
            trim = Surface("Facade window trim", new Color(.10f, .115f, .12f), .17f);
            windowGlass = Surface("Dark street windows", new Color(.010f, .017f, .022f), .4f, true);
            warmGlass = Surface("One faint occupied window", new Color(.175f, .098f, .043f), .2f, true);
            paint = Surface("Faded road paint", new Color(.33f, .33f, .27f), .07f);
            switchPlate = Surface("Aged ivory switch plate", new Color(.68f, .64f, .53f), .34f);
            streetBulb = EmissiveSurface("Streetlamp visible bulb", new Color(.35f, .39f, .47f), new Color(2.2f, 2.6f, 3.1f));
            ceilingBulb = EmissiveSurface("Ceiling fixture switched glass", new Color(.76f, .73f, .66f), CeilingEmission);
        }

        static Material EmissiveSurface(string key, Color color, Color emission)
        {
            var material = Surface(key, color, .45f);
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", emission);
            // URP's material validator derives _EMISSION from AnyEmissive. None clears the
            // keyword on import; the baked flag keeps the bulb visible without realtime GI.
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material); return material;
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = size; go.layer = parent.gameObject.layer;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic); return go;
        }

        static void DressWindowExterior()
        {
            var existing = Require("Exterior visual — iluminação da rua");
            // Keep the original pole, glow and window beam; replace only the shallow opaque backdrop.
            Remove(existing, "Fundo noturno discreto", "Fachada distante", ExteriorName);
            var street = Group(ExteriorName, existing);
            Box("Calçada diante do escritório", street, new Vector3(4.77f, -.18f, .9f), new Vector3(1.38f, .12f, 14), pavement);
            Box("Meio-fio próximo", street, new Vector3(5.43f, -.185f, .9f), new Vector3(.10f, .17f, 14), pavement);
            Box("Asfalto da rua", street, new Vector3(7.60f, -.275f, .9f), new Vector3(4.30f, .11f, 14), asphalt);
            Box("Calçada do outro lado", street, new Vector3(10.16f, -.18f, .9f), new Vector3(.82f, .12f, 14), pavement);
            Box("Meio-fio oposto", street, new Vector3(9.76f, -.185f, .9f), new Vector3(.11f, .17f, 14), pavement);
            for (int i = 0; i < 5; i++)
                Box("Marca gasta da pista", street, new Vector3(7.77f, -.218f, -4.1f + i * 2.05f), new Vector3(.065f, .002f, .72f), paint);
            for (int i = 0; i < 10; i++)
                Box("Junta da calçada", street, new Vector3(4.77f, -.117f, -5.2f + i * 1.35f), new Vector3(1.30f, .0015f, .013f), trim);
            Box("Fachada oposta à janela", street, new Vector3(10.72f, 2.08f, .9f), new Vector3(.25f, 4.60f, 12.8f), facade);
            Box("Rodapé da fachada oposta", street, new Vector3(10.56f, .15f, .9f), new Vector3(.065f, .70f, 12.90f), trim);
            Box("Friso entre andares", street, new Vector3(10.55f, 2.42f, .9f), new Vector3(.085f, .065f, 12.90f), pavement);
            Box("Beiral da fachada oposta", street, new Vector3(10.48f, 4.42f, .9f), new Vector3(.39f, .10f, 13.05f), trim);
            for (int floor = 0; floor < 2; floor++) for (int i = 0; i < 5; i++)
            {
                float z = -3.50f + i * 2.20f, y = 1.33f + floor * 2.10f;
                var opening = Group("Janela distante " + floor + "/" + i, street);
                Box("Vidro visto à distância", opening, new Vector3(10.566f, y, z), new Vector3(.022f, 1.11f, .86f), floor == 1 && i == 1 ? warmGlass : windowGlass);
                foreach (float side in new[] { -.46f, .46f }) Box("Batente vertical da fachada", opening, new Vector3(10.545f, y, z + side), new Vector3(.06f, 1.23f, .065f), trim);
                foreach (float edge in new[] { -.59f, .59f }) Box("Batente horizontal da fachada", opening, new Vector3(10.545f, y + edge, z), new Vector3(.06f, .065f, .98f), trim);
                Box("Travessa do vidro distante", opening, new Vector3(10.53f, y, z), new Vector3(.025f, .021f, .86f), trim);
            }
            var sky = Surface("Night backdrop", new Color(.011f, .019f, .034f), unlit: true);
            Box("Fundo noturno além da rua", street, new Vector3(15.5f, 2.55f, .9f), new Vector3(.025f, 7.1f, 16.5f), sky);
            var bulb = Require("Vidro luminoso do poste");
            bulb.position = room.TransformPoint(StreetBulbPosition); bulb.localScale = new Vector3(.19f, .13f, .19f);
            bulb.GetComponent<Renderer>().sharedMaterial = streetBulb;
            bulb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            // All three light paths now originate immediately under the visible bulb. A broad street-facing
            // spot brightens the asphalt/façade without spending another shadow map or flooding the office.
            var glow = Require("Brilho fraco do poste").GetComponent<Light>();
            glow.transform.position = room.TransformPoint(StreetLightPosition); glow.color = new Color(.68f, .78f, 1);
            glow.intensity = 2.2f; glow.range = 3.2f; glow.shadows = LightShadows.None; EditorUtility.SetDirty(glow);
            var streetLightTransform = Find("Luz do poste sobre a rua");
            if (!streetLightTransform) streetLightTransform = Group("Luz do poste sobre a rua", existing);
            var streetLight = streetLightTransform.GetComponent<Light>();
            if (!streetLight) streetLight = streetLightTransform.gameObject.AddComponent<Light>();
            streetLight.transform.position = room.TransformPoint(StreetLightPosition);
            streetLight.transform.rotation = Quaternion.LookRotation(room.TransformPoint(new Vector3(8.3f, -.15f, .9f)) - streetLight.transform.position);
            streetLight.type = LightType.Spot; streetLight.spotAngle = 128; streetLight.innerSpotAngle = 85;
            streetLight.color = new Color(.68f, .78f, 1); streetLight.intensity = 44; streetLight.range = 13;
            streetLight.shadows = LightShadows.None; streetLight.renderMode = LightRenderMode.ForcePixel;
            EditorUtility.SetDirty(streetLight);
        }

        static void CreateWallSwitch()
        {
            Remove(room, SwitchName);
            int layer = LayerMask.NameToLayer("Interactable");
            if (layer < 0) throw new InvalidOperationException("A camada Interactable existente está ausente.");
            var root = Group(SwitchName, room, new Vector3(-1.14f, 1.48f, -3.424f));
            root.gameObject.layer = layer;
            Box("Placa de baquelite marfim", root, Vector3.zero, new Vector3(.105f, .152f, .030f), switchPlate);
            var rocker = Box("Tecla basculante", root, new Vector3(0, 0, .023f), new Vector3(.049f, .079f, .019f), switchPlate).transform;
            GameObjectUtility.SetStaticEditorFlags(rocker.gameObject, 0);
            foreach (float y in new[] { -.059f, .059f })
            {
                var screw = GameObject.CreatePrimitive(PrimitiveType.Cylinder); screw.name = "Parafuso da placa";
                screw.transform.SetParent(root, false); screw.transform.localPosition = new Vector3(0, y, .016f);
                screw.transform.localRotation = Quaternion.Euler(90, 0, 0); screw.transform.localScale = new Vector3(.009f, .001f, .009f);
                screw.layer = layer; UnityEngine.Object.DestroyImmediate(screw.GetComponent<Collider>());
                screw.GetComponent<Renderer>().sharedMaterial = metal; screw.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            var collider = root.gameObject.AddComponent<BoxCollider>(); collider.center = new Vector3(0, 0, .015f);
            collider.size = new Vector3(.11f, .16f, .06f);
            var control = root.gameObject.AddComponent<InteractableLightSwitch>();
            control.targetLights = new[] { Require("Luz suave do teto").GetComponent<Light>(), Require("Preenchimento discreto").GetComponent<Light>() };
            control.startsOn = true; control.alwaysOn = false; control.rocker = rocker;
            var glass = Require("Globo de vidro").GetComponent<Renderer>(); glass.sharedMaterial = ceilingBulb;
            control.bulbRenderers = new[] { glass }; control.bulbEmission = CeilingEmission;
            control.onSound = AssetDatabase.LoadAssetAtPath<SoundSet>("Assets/HorrorGameplay/Audio/Recorded/Sets/Flashlight on.asset");
            control.offSound = AssetDatabase.LoadAssetAtPath<SoundSet>("Assets/HorrorGameplay/Audio/Recorded/Sets/Flashlight off.asset");
            control.SetState(true, false); EditorUtility.SetDirty(control);
        }

        sealed class Geometry
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int> indices = new List<int>();
            public int Add(Vector3 p, Vector2 tex = default) { vertices.Add(p); uv.Add(tex); return vertices.Count - 1; }
            public void T(int a, int b, int c, Vector3 outward)
            {
                var n = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (n.sqrMagnitude < 1e-18f) return;
                if (Vector3.Dot(n, outward) < 0) { int t = b; b = c; c = t; }
                indices.Add(a); indices.Add(b); indices.Add(c);
            }
            public void Q(int a, int b, int c, int d, Vector3 outward) { T(a, b, c, outward); T(a, c, d, outward); }
            public Mesh Build()
            {
                var m = new Mesh(); m.SetVertices(vertices); m.SetUVs(0, uv); m.SetTriangles(indices, 0);
                m.RecalculateNormals(); m.RecalculateBounds(); return m;
            }
        }

        static Mesh Model(string key, Func<Mesh> create)
        {
            string path = MeshPrefix + key + "_v1.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing) return existing;
            var mesh = create(); mesh.name = "Office atmosphere " + key; AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = Group(name, parent).gameObject; go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic); return go;
        }

        static Vector3 CrownPoint(int ring, float a, bool inside)
        {
            float[] heights = { .0205f, .055f, .131f, .167f, .161f };
            float[] rx = { .108f, .110f, .099f, .090f, .051f }, rz = { .132f, .126f, .112f, .097f, .071f };
            float sin = Mathf.Sin(a), cos = Mathf.Cos(a);
            float x = sin * rx[ring], z = cos * rz[ring];
            float pinch = Mathf.Pow(Mathf.Max(0, -cos), 2) * Mathf.Abs(sin) * .020f * Mathf.Clamp01(ring / 3f);
            x -= Mathf.Sign(x) * pinch;
            float y = heights[ring];
            if (ring >= 3) y -= .019f * Mathf.Exp(-Mathf.Pow(x / .036f, 2)) * (ring == 4 ? 1 : .55f);
            if (inside) { x *= .974f; z *= .977f; y -= ring == 0 ? 0 : .0035f; }
            return new Vector3(x, y, z);
        }

        static Mesh FedoraCrown()
        {
            return Model("FedoraHollowCreasedCrown", () =>
            {
                const int n = 80, rings = 5; var g = new Geometry();
                for (int layer = 0; layer < 2; layer++) for (int k = 0; k < rings; k++) for (int i = 0; i < n; i++)
                    g.Add(CrownPoint(k, i * Mathf.PI * 2 / n, layer == 1), new Vector2(i / (float)n, k / 4f));
                for (int layer = 0; layer < 2; layer++) for (int k = 0; k < rings - 1; k++) for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n, o = layer * n * rings; var p = (g.vertices[o + k * n + i] + g.vertices[o + k * n + j]) * .5f;
                    g.Q(o + k * n + i, o + k * n + j, o + (k + 1) * n + j, o + (k + 1) * n + i,
                        new Vector3(p.x, 0, p.z) * (layer == 0 ? 1 : -1));
                }
                int top = g.Add(new Vector3(0, .145f, 0)), innerTop = g.Add(new Vector3(0, .1415f, 0));
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    g.T(top, (rings - 1) * n + i, (rings - 1) * n + j, Vector3.up);
                    g.T(innerTop, (rings * 2 - 1) * n + i, (rings * 2 - 1) * n + j, Vector3.down);
                    g.Q(i, j, rings * n + j, rings * n + i, Vector3.down);
                }
                return g.Build();
            });
        }

        static Vector3 BrimPoint(float a, float u, bool below)
        {
            float sin = Mathf.Sin(a), cos = Mathf.Cos(a);
            float rx = Mathf.Lerp(.106f, .219f, u), rz = Mathf.Lerp(.129f, .192f, u);
            float y = .021f + (.019f * cos * cos + .016f * Mathf.Max(0, Mathf.Abs(sin) - .3f)) * u * u
                - .018f * Mathf.Pow(Mathf.Max(0, -cos), 3) * u * u * u;
            return new Vector3(sin * rx, y - (below ? .0035f : 0), cos * rz);
        }

        static Mesh FedoraBrim()
        {
            return Model("FedoraCurvedBrim", () =>
            {
                const int n = 80, rings = 7; var g = new Geometry();
                for (int layer = 0; layer < 2; layer++) for (int k = 0; k < rings; k++) for (int i = 0; i < n; i++)
                    g.Add(BrimPoint(i * Mathf.PI * 2 / n, k / 6f, layer == 1), new Vector2(i / (float)n, k / 6f));
                for (int layer = 0; layer < 2; layer++) for (int k = 0; k < rings - 1; k++) for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n, o = layer * n * rings;
                    g.Q(o + k * n + i, o + k * n + j, o + (k + 1) * n + j, o + (k + 1) * n + i, layer == 0 ? Vector3.up : Vector3.down);
                }
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n, last = (rings - 1) * n;
                    var p = (g.vertices[last + i] + g.vertices[last + j]) * .5f;
                    g.Q(last + i, last + j, last + rings * n + j, last + rings * n + i, new Vector3(p.x, 0, p.z));
                    p = (g.vertices[i] + g.vertices[j]) * .5f;
                    g.Q(i, j, rings * n + j, rings * n + i, -new Vector3(p.x, 0, p.z));
                }
                return g.Build();
            });
        }

        static Mesh FedoraBand()
        {
            return Model("FedoraGrosgrainBand", () =>
            {
                const int n = 80; var g = new Geometry();
                for (int k = 0; k < 2; k++) for (int i = 0; i < n; i++)
                {
                    float t = k == 0 ? .26f : .98f, a = i * Mathf.PI * 2 / n;
                    Vector3 p = Vector3.Lerp(CrownPoint(0, a, false), CrownPoint(1, a, false), t);
                    p.x *= 1.013f; p.z *= 1.013f; g.Add(p, new Vector2(i / 80f, k));
                }
                for (int i = 0; i < n; i++) { int j = (i + 1) % n; var p = (g.vertices[i] + g.vertices[j]) * .5f; g.Q(i, j, n + j, n + i, new Vector3(p.x, 0, p.z)); }
                return g.Build();
            });
        }

        static Mesh FedoraBow()
        {
            return Model("FedoraRibbonFold", () =>
            {
                var g = new Geometry();
                var points = new[] {
                    new Vector3(-.1125f,.039f,-.029f), new Vector3(-.1140f,.052f,-.026f), new Vector3(-.118f,.053f,-.007f),
                    new Vector3(-.113f,.042f,.002f), new Vector3(-.1135f,.049f,.028f), new Vector3(-.1120f,.036f,.029f),
                    new Vector3(-.118f,.038f,.005f) };
                foreach (var p in points) g.Add(p);
                g.T(0, 1, 2, Vector3.left); g.T(0, 2, 6, Vector3.left);
                g.T(2, 3, 6, Vector3.left); g.T(3, 4, 5, Vector3.left); g.T(3, 5, 6, Vector3.left);
                return g.Build();
            });
        }

        static void ReplaceCoatWithFedora()
        {
            var stand = Require("Cabideiro e casaco");
            Remove(stand, "Casaco de investigação", "Manga do casaco", HatName);
            var hat = Group(HatName, stand, new Vector3(0, 1.764f, -.232f), new Vector3(15, -23, 12));
            MeshObject("Copa oca com vinco e pinças frontais", hat, FedoraCrown(), felt);
            MeshObject("Aba curva de feltro", hat, FedoraBrim(), felt);
            MeshObject("Faixa de gorgurão", hat, FedoraBand(), ribbon);
            var bow = MeshObject("Laço dobrado da faixa", hat, FedoraBow(), ribbon);
            bow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            // No collider or interaction is added; the stand, hooks and base remain the original objects.
        }

        [Serializable] sealed class LightInfo
        {
            public string name, type, shadows, softShadowQuality;
            public Vector3 position;
            public float intensity, range, spotAngle, depthBias, normalBias;
            public int resolutionTier, shadowFaces;
        }
        [Serializable] sealed class Report
        {
            public bool passed, interactionIdsPreserved, hatPresent, coatRemoved, visualStreetPresent, windowColliderPreserved, supportsAdditionalLightShadows, pipelineChanged;
            public bool wallSwitchPresent, wallSwitchStartsOn, switchAlwaysOn, keepLightingPassed, streetLightAligned, streetBulbEmissive;
            public bool ceilingEmitterClear, generalLightingConfigured;
            public string generatedUtc, scene, pipelineAsset;
            public string[] errors, warnings, screenshots;
            public LightInfo[] lights;
            public int fixedShadowFaces, maximumShadowFacesWithFlashlight, fedoraTriangles, externalColliders;
            public float deskShadowMeanDifference;
            public float ceilingFixtureClearance;
            public int deskShadowChangedBlocks;
        }
        static int Faces(Light light) => !light.enabled || !light.gameObject.activeInHierarchy || light.shadows == LightShadows.None ? 0 : light.type == LightType.Point ? 6 : 1;

        public static void ValidateAndCapture()
        {
            Initialize(); var errors = new List<string>(); var warnings = new List<string>();
            void Check(bool condition, string error) { if (!condition) errors.Add(error); }
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Check(pipeline && pipeline.supportsAdditionalLightShadows && pipeline.additionalLightsRenderingMode == LightRenderingMode.PerPixel, "URP sem sombras de luzes adicionais por pixel.");
            var lights = room.GetComponentsInChildren<Light>(true);
            var entries = lights.Select(l => new LightInfo { name = l.name, type = l.type.ToString(), shadows = l.shadows.ToString(), position = l.transform.position,
                intensity = l.intensity, range = l.range, spotAngle = l.spotAngle, depthBias = l.shadowBias, normalBias = l.shadowNormalBias, shadowFaces = Faces(l),
                softShadowQuality = l.GetComponent<UniversalAdditionalLightData>() ? l.GetComponent<UniversalAdditionalLightData>().softShadowQuality.ToString() : "pipeline",
                resolutionTier = l.GetComponent<UniversalAdditionalLightData>() ? l.GetComponent<UniversalAdditionalLightData>().additionalLightsShadowResolutionTier : -2 }).ToArray();
            int faces = lights.Sum(Faces);
            Check(faces == 3, "Orçamento esperado de três mapas de sombra fixos não foi mantido.");
            Check(lights.Where(l => l.type == LightType.Point).All(l => l.shadows == LightShadows.None), "Uma luz pontual voltou a gerar seis mapas de sombra.");
            foreach (string name in new[] { "Luz quente da mesa", "Luz suave do teto", "Luz externa entrando pelas persianas" })
            {
                var l = lights.FirstOrDefault(light => light.name == name);
                Check(l && l.type == LightType.Spot && l.shadows == LightShadows.Soft && l.enabled, "Spot principal inválido: " + name);
            }
            bool ids = preservedIds == null || InteractionIds() == preservedIds;
            Check(ids && room.GetComponentsInChildren<InteractiveDoor>(true).Length == 1 && room.GetComponentsInChildren<InteractiveDrawer>(true).Length == 1
                && room.GetComponentsInChildren<CollectibleItem>(true).Length == 2, "Interações ou identificadores originais alterados.");
            Check(Find("Receptor curvo") && Find("Fundo da caixa móvel"), "Telefone/gaveta refinados foram alterados.");
            var desk = Require("Mesa principal — João de Claus");
            Check(Mathf.Abs(Mathf.DeltaAngle(desk.localEulerAngles.y, 180)) < .1f, "Orientação da mesa alterada.");
            var hat = Find(HatName); var street = Find(ExteriorName); var pane = Find("Colisão do vidro da janela");
            Check(hat && Find("Copa oca com vinco e pinças frontais") && Find("Aba curva de feltro") && Find("Faixa de gorgurão"), "Fedora incompleto.");
            Check(!Find("Casaco de investigação") && !Find("Manga do casaco"), "Roupa antiga ainda presente.");
            Check(street && Find("Asfalto da rua") && Find("Calçada diante do escritório") && Find("Fachada oposta à janela"), "Exterior visual incompleto.");
            int colliders = street ? street.GetComponentsInChildren<Collider>(true).Length : -1;
            Check(colliders == 0, "O cenário visual externo recebeu colisores/área de gameplay.");
            Check(pane && pane.GetComponent<BoxCollider>() && !pane.GetComponent<Renderer>(), "O colisor da janela foi alterado.");
            var switchRoot = Find(SwitchName);
            var control = switchRoot ? switchRoot.GetComponent<InteractableLightSwitch>() : null;
            var ceiling = Require("Luz suave do teto").GetComponent<Light>();
            var fill = Require("Preenchimento discreto").GetComponent<Light>();
            var ceilingGlass = Require("Globo de vidro").GetComponent<Renderer>();
            float ceilingClearance = ceilingGlass.bounds.min.y - ceiling.transform.position.y;
            bool emitterClear = ceilingClearance >= .06f && ceilingGlass.shadowCastingMode == ShadowCastingMode.Off;
            Check(emitterClear, "A fonte do teto não está livre abaixo do difusor.");
            bool generalLighting = ceiling.intensity >= 16 && ceiling.range >= 10 && ceiling.spotAngle >= 155 && ceiling.innerSpotAngle >= 135
                && fill.type == LightType.Point && fill.intensity >= 7 && fill.range >= 10 && fill.shadows == LightShadows.None
                && Vector3.Distance(fill.transform.position, ceiling.transform.position) <= .10f;
            Check(generalLighting, "A iluminação geral do teto/paredes não está configurada para a sala.");
            bool switchRefs = control && control.targetLights != null && control.targetLights.Length == 2
                && control.targetLights.Contains(ceiling) && control.targetLights.Contains(fill) && control.rocker
                && control.bulbRenderers != null && control.bulbRenderers.Length == 1 && control.bulbRenderers[0]
                && control.onSound && control.offSound;
            Check(switchRefs, "Referências do interruptor incompletas.");
            bool startsOn = control && control.startsOn && control.IsOn && ceiling.enabled && fill.enabled;
            Check(startsOn, "O escritório não começa com seu circuito principal ligado.");
            bool alwaysOn = control && control.alwaysOn;
            Check(!alwaysOn, "O interruptor do escritório não está configurado para interação.");
            Check(!ceiling.GetComponent<FlickerLight>() && !fill.GetComponent<FlickerLight>(), "Uma luz básica do escritório pode apagar por FlickerLight.");
            var switchCollider = switchRoot ? switchRoot.GetComponent<BoxCollider>() : null;
            Check(switchCollider && !switchCollider.isTrigger && switchRoot.gameObject.layer == LayerMask.NameToLayer("Interactable")
                && switchRoot.localPosition.z + switchCollider.center.z - switchCollider.size.z * .5f >= -3.441f,
                "O interruptor está sem colisor acessível diante da parede.");
            bool keepLighting = false;
            if (switchRefs)
            {
                try
                {
                    control.SetState(false, false);
                    keepLighting = !control.IsOn && control.targetLights.All(l => !l.enabled) && control.CanInteract(null);
                }
                finally
                {
                    control.SetState(true, false);
                }
            }
            Check(keepLighting, "O interruptor não desliga corretamente seu circuito.");
            var streetBulbTransform = Require("Vidro luminoso do poste");
            var windowLight = Require("Luz externa entrando pelas persianas").GetComponent<Light>();
            var roadLight = Find("Luz do poste sobre a rua")?.GetComponent<Light>();
            var poleGlow = Require("Brilho fraco do poste").GetComponent<Light>();
            bool streetAligned = roadLight && Vector3.Distance(windowLight.transform.position, streetBulbTransform.position) < .10f
                && Vector3.Distance(roadLight.transform.position, streetBulbTransform.position) < .10f
                && Vector3.Distance(poleGlow.transform.position, streetBulbTransform.position) < .10f;
            Check(streetAligned, "As luzes da rua e da janela estão separadas do bulbo visível do poste.");
            var bulbMaterial = streetBulbTransform.GetComponent<Renderer>().sharedMaterial;
            bool bulbEmission = bulbMaterial && bulbMaterial.IsKeywordEnabled("_EMISSION") && bulbMaterial.GetColor("_EmissionColor").maxColorComponent > 1;
            Check(bulbEmission, "O bulbo do poste não está visivelmente emissivo.");
            Check(roadLight && roadLight.enabled && roadLight.shadows == LightShadows.None && roadLight.intensity >= 35 && roadLight.range >= 10,
                "A iluminação da rua está insuficiente ou adicionou sombras desnecessárias.");
            int triangles = 0;
            if (hat) foreach (var filter in hat.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                Check(mesh && AssetDatabase.GetAssetPath(mesh).StartsWith(MeshPrefix, StringComparison.Ordinal), "Mesh persistente do chapéu ausente.");
                if (!mesh) continue;
                var v = mesh.vertices; var t = mesh.triangles; triangles += t.Length / 3;
                Check(v.All(p => !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z)), "Vértice inválido no chapéu.");
                for (int i = 0; i < t.Length; i += 3)
                    if (Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).sqrMagnitude < 1e-18f) { errors.Add("Triângulo degenerado: " + mesh.name); break; }
            }
            Check(triangles >= 2500, "Geometria do chapéu não foi persistida corretamente.");
            Directory.CreateDirectory(AssetRoot + "Documentation/");
            var screenshots = new List<string>();
            string ImagePath(string name) => AssetRoot + "Documentation/Office-atmosphere-" + name + ".png";
            var source = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.CompareTag("MainCamera"));
            var flashlights = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FlashlightController>(true)).ToArray();
            var flashlightStates = flashlights.Where(f => f.lamp).Select(f => (light: f.lamp, enabled: f.lamp.enabled)).ToArray();
            var shadowLights = lights.Where(l => Faces(l) > 0).Select(l => (light: l, shadows: l.shadows)).ToArray();
            float shadowDifference = 0; int changedBlocks = 0;
            try
            {
                // Static Edit-mode preview uses the intended flashlight-off startup state.
                foreach (var state in flashlightStates) state.light.enabled = false;
                Capture(source, new Vector3(-1.28f, 1.68f, -2.45f), new Vector3(.10f, 1.2f, 1.9f), ImagePath("room"), 66); screenshots.Add(ImagePath("room"));
                Capture(source, new Vector3(2.80f, 1.68f, .90f), new Vector3(8.0f, 1.63f, 1.60f), ImagePath("window"), 70); screenshots.Add(ImagePath("window"));
                if (control) { Capture(source, room.TransformPoint(new Vector3(-.67f, 1.58f, -2.10f)), control.transform.position, ImagePath("switch"), 42); screenshots.Add(ImagePath("switch")); }
                if (hat) { Capture(source, hat.TransformPoint(new Vector3(-.67f, .49f, -.10f)), hat.TransformPoint(new Vector3(0, .085f, 0)), ImagePath("hat"), 37); screenshots.Add(ImagePath("hat")); }
                Vector3 deskPose = desk.TransformPoint(new Vector3(-1.22f, 1.43f, -1.47f)), deskTarget = desk.TransformPoint(new Vector3(.12f, .92f, .03f));
                var on = Capture(source, deskPose, deskTarget, ImagePath("desk-shadows"), 54); screenshots.Add(ImagePath("desk-shadows"));
                foreach (var state in shadowLights) state.light.shadows = LightShadows.None;
                var off = Capture(source, deskPose, deskTarget, ImagePath("desk-no-shadows-debug"), 54); screenshots.Add(ImagePath("desk-no-shadows-debug"));
                CompareShadowBlocks(on, off, out shadowDifference, out changedBlocks);
                if (changedBlocks < 2 || shadowDifference < .001f)
                    warnings.Add("Diferença visual de sombras pequena nessa prévia; comparar as imagens e usar o Profiler para medir o custo na GPU.");
            }
            catch (Exception e) { errors.Add("Captura da atmosfera: " + e.Message); Debug.LogException(e); }
            finally
            {
                foreach (var state in shadowLights) if (state.light) state.light.shadows = state.shadows;
                foreach (var state in flashlightStates) if (state.light) state.light.enabled = state.enabled;
            }
            var report = new Report { passed = errors.Count == 0, scene = ScenePath, generatedUtc = DateTime.UtcNow.ToString("O"), errors = errors.ToArray(), warnings = warnings.ToArray(), screenshots = screenshots.ToArray(),
                lights = entries, fixedShadowFaces = faces, maximumShadowFacesWithFlashlight = faces + (flashlights.Any(f => f.lamp && f.lamp.shadows != LightShadows.None) ? 1 : 0),
                fedoraTriangles = triangles, externalColliders = colliders, interactionIdsPreserved = ids, hatPresent = hat, coatRemoved = !Find("Casaco de investigação") && !Find("Manga do casaco"),
                visualStreetPresent = street, windowColliderPreserved = pane && pane.GetComponent<BoxCollider>(),
                wallSwitchPresent = switchRefs, wallSwitchStartsOn = startsOn, switchAlwaysOn = alwaysOn, keepLightingPassed = keepLighting, streetLightAligned = streetAligned, streetBulbEmissive = bulbEmission,
                ceilingEmitterClear = emitterClear, generalLightingConfigured = generalLighting, ceilingFixtureClearance = ceilingClearance,
                supportsAdditionalLightShadows = pipeline && pipeline.supportsAdditionalLightShadows, pipelineChanged = changedPipeline, pipelineAsset = pipeline ? AssetDatabase.GetAssetPath(pipeline) : pipelinePath,
                deskShadowMeanDifference = shadowDifference, deskShadowChangedBlocks = changedBlocks };
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            AssetDatabase.Refresh();
            Debug.Log("Office atmosphere validation " + (report.passed ? "PASS" : "FAILED") + ": " + errors.Count + " errors; " + faces + " fixed shadow faces; " + triangles + " fedora triangles.");
            if (!report.passed) throw new InvalidOperationException(string.Join("; ", errors));
        }

        static void CompareShadowBlocks(Color32[] a, Color32[] b, out float difference, out int changed)
        {
            const int width = 1600, height = 900, block = 50;
            difference = 0; changed = 0;
            int count = 0;
            for (int y = 0; y < height; y += block) for (int x = 0; x < width; x += block)
            {
                float one = 0, two = 0; int pixels = 0;
                for (int dy = 0; dy < block; dy++) for (int dx = 0; dx < block; dx++)
                {
                    int i = (y + dy) * width + x + dx;
                    one += (a[i].r * .2126f + a[i].g * .7152f + a[i].b * .0722f) / 255;
                    two += (b[i].r * .2126f + b[i].g * .7152f + b[i].b * .0722f) / 255; pixels++;
                }
                float delta = Mathf.Abs(one - two) / pixels; difference += delta;
                if (delta > .009f) changed++; count++;
            }
            difference /= count;
        }

        static Color32[] Capture(Camera source, Vector3 pos, Vector3 target, string path, float fov)
        {
            var go = EditorUtility.CreateGameObjectWithHideFlags("Office atmosphere preview camera", HideFlags.HideAndDontSave, typeof(Camera));
            RenderTexture rt = null; Texture2D pixels = null; var previous = RenderTexture.active;
            try
            {
                var camera = go.GetComponent<Camera>(); if (source) camera.CopyFrom(source);
                camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.02f, .025f, .035f);
                camera.rect = new Rect(0, 0, 1, 1);
                camera.fieldOfView = fov; camera.nearClipPlane = .035f; camera.farClipPlane = 40; camera.aspect = 16f / 9;
                camera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos));
                camera.ResetWorldToCameraMatrix(); camera.ResetProjectionMatrix();
                var data = go.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true; data.renderShadows = true; data.volumeTrigger = camera.transform;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                var original = source ? source.GetComponent<UniversalAdditionalCameraData>() : null; data.volumeLayerMask = original ? original.volumeLayerMask : ~0;
                rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); rt.Create(); camera.targetTexture = rt;
                var request = new RenderPipeline.StandardRequest { destination = rt, isPreview = true };
                if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("A URP não suporta esta captura de câmera.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = rt; pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
                var colors = pixels.GetPixels32(); int minimum = 255, maximum = 0;
                for (int i = 0; i < colors.Length; i += 61)
                {
                    int value = Mathf.Max(colors[i].r, Mathf.Max(colors[i].g, colors[i].b));
                    minimum = Mathf.Min(minimum, value); maximum = Mathf.Max(maximum, value);
                }
                if (maximum <= 2 || maximum - minimum < 2)
                    throw new InvalidOperationException("Captura do escritório sem conteúdo visível (pixels " + minimum + "–" + maximum + "): " + path);
                File.WriteAllBytes(path, pixels.EncodeToPNG()); return colors;
            }
            finally
            {
                RenderTexture.active = previous; if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
                if (rt) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); } UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
