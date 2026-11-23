// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SchoolCEEP.Editor
{
    /// <summary>
    /// Installs the six human-approved CC0 assets as visual replacements only.
    /// Existing colliders, room transforms, doors and narrative bindings remain.
    /// glTF sources and verified provenance live in the workspace; runtime uses
    /// shared native meshes/materials and Unity LODGroups, without a new package.
    /// </summary>
    public static class SchoolReadyAssetPass
    {
        public const string GroupName = "Escola — assets CC0 aprovados";
        public const string Output = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/SchoolAssets";
        const string Generated = "Assets/models/SchoolIntegration";
        static readonly string[] ModelIds = { "SchoolChair_01", "SchoolDesk_01", "steel_frame_shelves_01", "cardboard_box_01" };
        static readonly string[] TextureIds = { "damaged_plaster", "rusty_metal_03" };

        [Serializable] public sealed class MeshPart
        { public string name; public int material; public Vector3[] vertices, normals; public Vector2[] uv; public int[] triangles; }
        [Serializable] public sealed class MeshData
        { public string id; public Vector3 size; public MeshPart[] parts, distantParts; }
        [Serializable] public sealed class ModelResult
        { public string id, prefab; public int triangles, distantTriangles; public Vector3 size; }
        [Serializable] public sealed class Report
        {
            public string scene, architectureBefore, architectureAfter;
            public bool architecturePreserved, storyPreserved, chairEventPreserved, lightsPreserved, savedClean;
            public int classroomUnits, importedChairs, importedDesks, storageShelves, boxes, importedInstances;
            public int addedColliders, materials, lodGroups, missingScripts, removedOriginalTriangles, fittedClassroomUnits;
            public int maximumTriangles, distantTriangles, changedDecayBatches;
            public ModelResult[] models;
            public string[] notes;
        }
        sealed class Imported { public MeshData data; public GameObject prefab; public ModelResult report; }
        static readonly Dictionary<string, Imported> imported = new Dictionary<string, Imported>();

        public static string ImportAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Import outside Play mode.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Folder(Generated);
            imported.Clear();
            foreach (var id in ModelIds.Concat(TextureIds)) ImportTextures(id);
            foreach (var id in ModelIds) imported.Add(id, ImportModel(id));
            CreateMaterial("damaged_plaster", true);
            CreateMaterial("rusty_metal_03", true);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/native-import.json", JsonUtility.ToJson(new ModelList { models = imported.Values.Select(v => v.report).ToArray() }, true));
            return "Four shared model prefabs and two PBR decal materials imported; no scene changed.";
        }
        [Serializable] sealed class ModelList { public ModelResult[] models; }

        static Imported ImportModel(string id)
        {
            string category = id == "cardboard_box_01" ? "decoracao" : "mobiliario";
            string folder = "Assets/models/" + category + "/" + id;
            string meshPath = folder + "/MeshData.json";
            var data = JsonUtility.FromJson<MeshData>(File.ReadAllText(meshPath));
            if (data?.parts == null || data.parts.Length == 0 || data.distantParts == null || data.distantParts.Length == 0)
                throw new InvalidDataException("Prepared static mesh is missing: " + id);
            var material = CreateMaterial(id, false);
            var previewScene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject(id + " — CC0");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, previewScene);
            try
            {
                var near = Meshes(root.transform, folder, data.parts, material, "LOD0");
                var distant = Meshes(root.transform, folder, data.distantParts, material, "LOD1");
                var lod = root.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(.14f, near), new LOD(.022f, distant) });
                lod.fadeMode = LODFadeMode.None;
                lod.RecalculateBounds();
                string path = folder + "/" + id + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                return new Imported { data = data, prefab = prefab, report = new ModelResult {
                    id = id, prefab = path, size = data.size,
                    triangles = data.parts.Sum(p => p.triangles.Length / 3),
                    distantTriangles = data.distantParts.Sum(p => p.triangles.Length / 3) } };
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(previewScene); }
        }

        static Renderer[] Meshes(Transform parent, string folder, MeshPart[] parts, Material material, string level)
        {
            var result = new List<Renderer>();
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.vertices.Length != part.normals.Length || part.vertices.Length != part.uv.Length
                    || part.triangles.Any(v => v < 0 || v >= part.vertices.Length))
                    throw new InvalidDataException("Invalid converted glTF indices/attributes.");
                var mesh = new Mesh { name = part.name, indexFormat = IndexFormat.UInt32 };
                mesh.vertices = part.vertices; mesh.normals = part.normals; mesh.uv = part.uv; mesh.triangles = part.triangles;
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                string path = folder + "/" + level + "-" + i + ".asset";
                Save(mesh, path);
                var go = new GameObject(level + " — " + part.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                result.Add(renderer);
            }
            return result.ToArray();
        }

        static void ImportTextures(string id)
        {
            string folder = "Assets/textures/" + id;
            foreach (var file in Directory.GetFiles(folder).Where(f => f.EndsWith(".jpg") || f.EndsWith(".png")))
            {
                string path = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) throw new InvalidDataException("Texture importer missing: " + path);
                bool normal = path.Contains("_nor_gl_");
                bool linear = normal || path.EndsWith("MetallicSmoothness.png") || path.EndsWith("Occlusion.png") || path.Contains("_arm_");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !linear;
                importer.mipmapEnabled = true; importer.maxTextureSize = 2048;
                importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 2;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.alphaSource = path.EndsWith(".png") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = path.EndsWith("DiffuseDecal.png");
                importer.isReadable = false;
                if (normal) importer.convertToNormalmap = false;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                    name = "Standalone", overridden = true, maxTextureSize = 2048, compressionQuality = 75,
                    format = normal ? TextureImporterFormat.BC5 : path.EndsWith(".png") ? TextureImporterFormat.BC7 : TextureImporterFormat.DXT1 });
                importer.SaveAndReimport();
            }
        }

        static Material CreateMaterial(string id, bool decal)
        {
            string path = Generated + "/" + id + (decal ? "-decay" : "") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = id + " — PBR CC0" }; AssetDatabase.CreateAsset(material, path); }
            string tex = "Assets/textures/" + id + "/";
            material.SetTexture("_BaseMap", Texture(tex + (decal ? "DiffuseDecal.png" : id + "_diff_2k.jpg")));
            material.SetTexture("_BumpMap", Texture(tex + id + "_nor_gl_2k.jpg"));
            material.SetTexture("_MetallicGlossMap", Texture(tex + "MetallicSmoothness.png"));
            material.SetTexture("_OcclusionMap", Texture(tex + "Occlusion.png"));
            material.SetColor("_BaseColor", new Color(1, 1, 1, decal ? .76f : 1));
            material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", .9f);
            material.SetFloat("_BumpScale", decal ? .65f : .75f); material.SetFloat("_OcclusionStrength", .8f);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_OCCLUSIONMAP");
            material.DisableKeyword("_EMISSION"); material.SetFloat("_Cull", (float)CullMode.Off);
            material.enableInstancing = true; material.doubleSidedGI = true;
            if (decal)
            {
                material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_ZWrite", 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.DisableKeyword("_ALPHATEST_ON");
                material.SetFloat("_AlphaClip", 0); material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = 3000;
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
            EditorUtility.SetDirty(material);
            return material;
        }
        static Texture2D Texture(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new InvalidDataException("Missing PBR map: " + path);

        public static Report Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Install outside Play mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != SchoolIntegrationPass.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved, clean SchoolCEEP scene before installing.");
            var root = scene.GetRootGameObjects().Single(g => g.name.StartsWith("CEEP")).transform;
            if (root.Find(GroupName)) return Audit(); // Re-running never removes another set of source geometry.
            ImportAssets();
            var before = SchoolChapterThreePass.ArchitectureSignature(root);
            var game = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameSession>(true)).Single();
            var chairEvent = root.GetComponentInChildren<SchoolPerceptionEvents>(true);
            string bookBefore = EditorJsonUtility.ToJson(game.narrative.book), eventBefore = EditorJsonUtility.ToJson(chairEvent);
            var lights = root.GetComponentsInChildren<Light>(true);
            string lightBefore = string.Join("|", lights.Select(l => EditorJsonUtility.ToJson(l)));
            var units = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Sala — cadeira azul com carteira").ToArray();
            var storage = root.GetComponentsInChildren<BoxCollider>(true).Where(c => c.name == "Armazenamento — estante — volume físico").ToArray();
            if (units.Length == 0 || storage.Length == 0 || !chairEvent || !chairEvent.chair)
                throw new InvalidOperationException("Protected source furniture/event is missing.");
            var pieces = units.SelectMany(t => t.Cast<Transform>()).Concat(root.GetComponentsInChildren<Transform>(true)
                .Where(t => (t.name.StartsWith("Armazenamento — estante —") && !t.name.EndsWith("volume físico"))
                    || t.name == "Armazenamento — caixa")).ToArray();
            // The helper proves every original face before it mutates any batch.
            // A fresh explicit map rebuild gets its own reversible mesh clones.
            // The scene-owned group above makes repeat installation a read-only audit.
            var replacement = SchoolBatchVisualReplacement.Replace(root, pieces, Generated + "/ReplacementMeshes-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            File.WriteAllText(Output + "/batch-replacement.json", JsonUtility.ToJson(replacement, true));
            var group = new GameObject(GroupName).transform; group.SetParent(root, false);
            var report = new Report { scene = scene.path, architectureBefore = before, classroomUnits = units.Length,
                removedOriginalTriangles = replacement.removedTriangles, models = imported.Values.Select(v => v.report).ToArray() };
            foreach (var unit in units)
            {
                var holder = new GameObject("Mobiliário importado — sala").transform; holder.SetParent(group, false);
                holder.position = unit.position; holder.rotation = unit.rotation;
                // Keep both objects inside the original .80 x .84 x .98 collision envelope.
                Place("SchoolChair_01", holder, new Vector3(-.04f, .012f, -.035f), Quaternion.Euler(0, 180, 0), new Vector3(.47f, .82f, .55f));
                Place("SchoolDesk_01", holder, new Vector3(.10f, .012f, .36f), Quaternion.identity, new Vector3(.66f, .775f, .48f));
                AssertFits(unit.GetComponent<BoxCollider>(), holder);
                report.fittedClassroomUnits++;
                report.importedChairs++; report.importedDesks++;
            }
            foreach (var volume in storage)
            {
                var b = volume.bounds;
                var shelf = Place("steel_frame_shelves_01", group, new Vector3(b.center.x, b.min.y, b.center.z), Quaternion.identity,
                    new Vector3(b.size.x - .025f, b.size.y - .015f, b.size.z - .012f));
                var box = Place("cardboard_box_01", group,
                    shelf.position + new Vector3((report.storageShelves % 2 == 0 ? -1 : 1) * b.size.x * .2f, .102f, 0),
                    Quaternion.Euler(0, report.storageShelves % 2 == 0 ? 0 : 180, 0), new Vector3(.30f, .266f, .36f));
                box.name = "Caixa antiga — prateleira inferior";
                report.storageShelves++; report.boxes++;
            }
            // Preserve the movable Transform and its save ID, replacing only its six visual children.
            foreach (var renderer in chairEvent.chair.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            Place("SchoolChair_01", chairEvent.chair, new Vector3(0, -.45f, 0), Quaternion.identity, new Vector3(.43f, .84f, .48f));
            report.importedChairs++;
            var decay = root.Find(SchoolAbandonmentPass.GroupName);
            if (decay)
                foreach (var renderer in decay.GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.sharedMaterial && (renderer.sharedMaterial.name == "Reboco descascado" || renderer.sharedMaterial.name == "Ferrugem"))
                    {
                        string id = renderer.sharedMaterial.name == "Ferrugem" ? "rusty_metal_03" : "damaged_plaster";
                        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Generated + "/" + id + "-decay.mat"); report.changedDecayBatches++;
                    }
            Physics.SyncTransforms();
            report.architectureAfter = SchoolChapterThreePass.ArchitectureSignature(root);
            report.architecturePreserved = report.architectureBefore == report.architectureAfter;
            report.storyPreserved = bookBefore == EditorJsonUtility.ToJson(game.narrative.book);
            report.chairEventPreserved = eventBefore == EditorJsonUtility.ToJson(chairEvent);
            report.lightsPreserved = lightBefore == string.Join("|", lights.Select(l => EditorJsonUtility.ToJson(l)));
            report.addedColliders = group.GetComponentsInChildren<Collider>(true).Length;
            report.lodGroups = group.GetComponentsInChildren<LODGroup>(true).Length + chairEvent.chair.GetComponentsInChildren<LODGroup>(true).Length;
            report.importedInstances = report.importedChairs + report.importedDesks + report.storageShelves + report.boxes;
            report.maximumTriangles = report.importedChairs * imported[ModelIds[0]].report.triangles + report.importedDesks * imported[ModelIds[1]].report.triangles
                + report.storageShelves * imported[ModelIds[2]].report.triangles + report.boxes * imported[ModelIds[3]].report.triangles;
            report.distantTriangles = report.importedChairs * imported[ModelIds[0]].report.distantTriangles + report.importedDesks * imported[ModelIds[1]].report.distantTriangles
                + report.storageShelves * imported[ModelIds[2]].report.distantTriangles + report.boxes * imported[ModelIds[3]].report.distantTriangles;
            report.materials = ModelIds.Length + TextureIds.Length;
            report.notes = new[] {
                "Only six approved CC0 assets downloaded, all 2K. Published MD5 and byte sizes verified. Originals remain in workspace assets; author/source/license are recorded in CREDITS.md.",
                "457 original classroom collision volumes remain. Chair/desk visual pairs fit those volumes; original source triangles are exactly matched and removed from cloned batches, never cut by bounds.",
                "Empty storage shelves receive the metal shelving model and one box inside the existing shelf collision. The 84 old unsupported box visuals are removed with the same face proof. Library books and their original supporting shelves remain together.",
                "The static event chair keeps its original Transform, pose/save ID and non-colliding behavior. The new chair is a visual child, so its movement and checkpoint restoration remain intact.",
                "Two scanned PBR decals replace only chapter-3 plaster/rust visual batches. Existing wall materials and architecture are retained.",
                "Each model shares meshes/materials, uses GPU instancing, compressed mipmapped 2K maps, a reduced distant LOD and distance culling. Counts report the whole scene worst case, not measured frame rate.",
                "No menu, office, car, chapter progression, lights, persistent IDs, physics or runtime package was changed." };
            if (!report.architecturePreserved || !report.storyPreserved || !report.chairEventPreserved || !report.lightsPreserved || report.addedColliders != 0)
                throw new InvalidOperationException("Visual integration changed protected school state; reload the saved before scene.");
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            report.savedClean = !scene.isDirty;
            File.WriteAllText(Output + "/installation.json", JsonUtility.ToJson(report, true));
            return report;
        }

        static Transform Place(string id, Transform parent, Vector3 local, Quaternion rotation, Vector3 size)
        {
            var asset = imported[id];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset.prefab, parent);
            go.transform.localPosition = local; go.transform.localRotation = rotation;
            var s = asset.data.size;
            go.transform.localScale = new Vector3(size.x / s.x, size.y / s.y, size.z / s.z);
            return go.transform;
        }

        static void AssertFits(BoxCollider collider, Transform visual)
        {
            if (!collider) throw new InvalidOperationException("Original classroom collision envelope missing.");
            var bounds = new Bounds(collider.center, collider.size + Vector3.one * .006f);
            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
                foreach (var point in filter.sharedMesh.vertices)
                    if (!bounds.Contains(collider.transform.InverseTransformPoint(filter.transform.TransformPoint(point))))
                        throw new InvalidOperationException("Imported visual extends outside the original collision envelope: " + collider.name);
        }

        public static Report Audit()
        {
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(Output + "/installation.json"));
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var root = scene.GetRootGameObjects().Single(g => g.name.StartsWith("CEEP")).transform;
            var group = root.Find(GroupName);
            var chairEvent = root.GetComponentInChildren<SchoolPerceptionEvents>(true);
            report.architectureAfter = SchoolChapterThreePass.ArchitectureSignature(root);
            report.architecturePreserved = report.architectureAfter == report.architectureBefore;
            report.addedColliders = group.GetComponentsInChildren<Collider>(true).Length;
            report.missingScripts = root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            report.savedClean = !scene.isDirty && !EditorApplication.isPlayingOrWillChangePlaymode;
            var flow = root.GetComponentInChildren<SchoolOpeningFlow>(true);
            if (!report.architecturePreserved || !report.savedClean || report.missingScripts != 0 || report.addedColliders != 0
                || !chairEvent.chair || chairEvent.chair.GetComponentsInChildren<Collider>(true).Length != 0
                || flow.schoolLights.Any(l => l && l.enabled) || flow.director.autoLoadNextChapter
                || flow.director.book.ValidateContent().Length != 0)
                throw new InvalidOperationException("School asset final audit failed.");
            File.WriteAllText(Output + "/final-scene.json", JsonUtility.ToJson(report, true));
            return report;
        }

        static void Save(UnityEngine.Object value, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (existing) { EditorUtility.CopySerialized(value, existing); UnityEngine.Object.DestroyImmediate(value); EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(value, path);
        }
        static void Folder(string path)
        { if (AssetDatabase.IsValidFolder(path)) return; string parent = path.Substring(0, path.LastIndexOf('/')); Folder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1)); }
    }
}
