// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HorrorGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SchoolCEEP.Editor
{
    /// <summary>
    /// Native static conversion of the two approved Poly Haven 1K glTF props.
    /// Papers/books share meshes, URP materials and two LODs; originals and
    /// publisher checksums remain in the workspace. No package/importer runtime.
    /// </summary>
    public static class SchoolRefinementReadyPropsPass
    {
        public const string GroupName = "Escola — livros e cadernos CC0 refinados";
        public const string Generated = "Assets/models/RefinementIntegration";
        public const string Output = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RefinementAssets";
        static readonly string[] Ids = { "office_notepads", "book_encyclopedia_set_01" };

        [Serializable] public sealed class MaterialInfo
        { public string name, diffuse, normal, metallicSmoothness, occlusion; public bool doubleSided; }
        [Serializable] public sealed class VariantInfo
        { public string name, meshDataFile; public Vector3 size; public int triangles, distantTriangles; }
        [Serializable] public sealed class Catalogue
        { public string id; public MaterialInfo[] materials; public VariantInfo[] variants; }
        [Serializable] public sealed class ImportReport
        { public int models, variants, materials, originalTriangles, distantTriangles; public string[] prefabPaths; }
        [Serializable] public sealed class Report
        {
            public int rooms, books, notepads, rejectedCandidates, addedColliders, lodGroups;
            public string architectureBefore, architectureAfter;
            public bool architecturePreserved, storyPreserved, allPiecesSupported;
            public string[] notes;
        }
        sealed class Room
        { public Bounds bounds; public float floor; public Vector3 center, aisleStart, aisleEnd; public Vector3[] thresholds; public string name; }

        public static string PrefabPath(string id, string variant) => "Assets/models/decoracao/" + id + "/" + variant + ".prefab";

        /// <summary>Use the approved real paper mesh on the existing safe loose-page footprints.</summary>
        public static int UpgradeLoosePages(Transform root)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Dress pages outside Play.");
            var group = root.Find(SchoolFurnitureRefinementPass.GroupName);
            if (!group) return 0;
            var variants = new[] { "office_notepads_a4_a", "office_notepads_a4_b", "office_notepads_a4_c" };
            var material = AssetDatabase.LoadAssetAtPath<Material>(Generated + "/office_notepads-material-0.mat");
            if (!material) throw new InvalidOperationException("Import the approved notepads first.");
            var filters = group.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name == "Papel antigo — espalhado").ToArray();
            for (int i = 0; i < filters.Length; i++)
            {
                var filter = filters[i]; var original = filter.sharedMesh;
                var replacement = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/models/decoracao/office_notepads/LOD0-" + variants[i % variants.Length] + "-0.asset");
                if (!original || !replacement) throw new InvalidOperationException("Prepared paper mesh absent.");
                if (filter.sharedMesh == replacement) continue;
                var center = filter.transform.TransformPoint(original.bounds.center);
                var scale = filter.transform.localScale;
                scale.x *= original.bounds.size.x / replacement.bounds.size.x;
                scale.z *= original.bounds.size.z / replacement.bounds.size.z;
                filter.transform.localScale = scale;
                filter.transform.position = center - filter.transform.TransformVector(replacement.bounds.center);
                filter.sharedMesh = replacement;
                var renderer = filter.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                EditorUtility.SetDirty(filter); EditorUtility.SetDirty(renderer);
            }
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            return filters.Length;
        }

        public static ImportReport Import()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Import approved props outside Play mode.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Folder(Generated);
            var report = new ImportReport { models = Ids.Length };
            var paths = new List<string>();
            foreach (var id in Ids)
            {
                string folder = "Assets/models/decoracao/" + id;
                var data = JsonUtility.FromJson<Catalogue>(File.ReadAllText(folder + "/PreparedCatalog.json"));
                if (data?.materials == null || data.variants == null || data.variants.Length == 0)
                    throw new InvalidDataException("Approved model preparation missing: " + id);
                ImportTextures(id);
                var materials = data.materials.Select((m, i) => Material(id, i, m)).ToArray();
                report.materials += materials.Length;
                foreach (var variant in data.variants)
                {
                    var meshData = JsonUtility.FromJson<SchoolReadyAssetPass.MeshData>(File.ReadAllText(folder + "/" + variant.meshDataFile));
                    if (meshData?.parts == null || meshData.distantParts == null || meshData.parts.Length == 0)
                        throw new InvalidDataException("Converted static glTF parts missing: " + variant.name);
                    var preview = EditorSceneManager.NewPreviewScene();
                    var root = new GameObject(variant.name + " — CC0");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
                    try
                    {
                        var near = Meshes(root.transform, folder, variant.name, meshData.parts, materials, "LOD0");
                        var distant = Meshes(root.transform, folder, variant.name, meshData.distantParts, materials, "LOD1");
                        var lod = root.AddComponent<LODGroup>();
                        lod.SetLODs(new[] { new LOD(.09f, near), new LOD(.008f, distant) });
                        lod.fadeMode = LODFadeMode.None; lod.RecalculateBounds();
                        var path = PrefabPath(id, variant.name);
                        PrefabUtility.SaveAsPrefabAsset(root, path); paths.Add(path);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
                    report.variants++; report.originalTriangles += variant.triangles; report.distantTriangles += variant.distantTriangles;
                }
            }
            report.prefabPaths = paths.ToArray();
            AssetDatabase.SaveAssets(); Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/native-import.json", JsonUtility.ToJson(report, true));
            return report;
        }

        static Renderer[] Meshes(Transform parent, string folder, string variant,
            SchoolReadyAssetPass.MeshPart[] parts, Material[] materials, string level)
        {
            var result = new List<Renderer>();
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p.vertices.Length != p.normals.Length || p.vertices.Length != p.uv.Length
                    || p.material < 0 || p.material >= materials.Length || p.triangles.Any(t => t < 0 || t >= p.vertices.Length))
                    throw new InvalidDataException("Invalid approved mesh indices/attributes: " + variant);
                var mesh = new Mesh { name = p.name, indexFormat = IndexFormat.UInt32 };
                mesh.vertices = p.vertices; mesh.normals = p.normals; mesh.uv = p.uv; mesh.triangles = p.triangles;
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                string path = folder + "/" + level + "-" + variant + "-" + i + ".asset";
                Save(mesh, path);
                var go = new GameObject(level + " — " + p.name); go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = materials[p.material];
                renderer.shadowCastingMode = variant.StartsWith("office_", StringComparison.Ordinal) ? ShadowCastingMode.Off : ShadowCastingMode.On;
                renderer.receiveShadows = true; result.Add(renderer);
            }
            return result.ToArray();
        }

        static void ImportTextures(string id)
        {
            foreach (var file in Directory.GetFiles("Assets/textures/" + id).Where(p => p.EndsWith(".jpg") || p.EndsWith(".png")))
            {
                string path = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) throw new InvalidDataException("Approved texture importer missing: " + path);
                bool normal = path.Contains("_nor_gl_");
                bool linear = normal || path.Contains("MetallicSmoothness") || path.Contains("Occlusion") || path.Contains("_arm_") || path.Contains("_roughness_");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !linear; importer.mipmapEnabled = true; importer.maxTextureSize = 1024;
                importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 2;
                importer.textureCompression = TextureImporterCompression.Compressed; importer.isReadable = false;
                importer.alphaSource = path.EndsWith("MetallicSmoothness.png") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = false; if (normal) importer.convertToNormalmap = false;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                    name = "Standalone", overridden = true, maxTextureSize = 1024, compressionQuality = 75,
                    format = normal ? TextureImporterFormat.BC5 : path.EndsWith(".png") ? TextureImporterFormat.BC7 : TextureImporterFormat.DXT1 });
                importer.SaveAndReimport();
            }
        }

        static Material Material(string id, int index, MaterialInfo info)
        {
            string path = Generated + "/" + id + "-material-" + index + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("Approved props require existing URP Lit shader.");
                material = new Material(shader) { name = info.name + " — PBR CC0 envelhecido" }; AssetDatabase.CreateAsset(material, path);
            }
            string tex = "Assets/textures/" + id + "/";
            material.SetTexture("_BaseMap", Texture(tex + info.diffuse));
            material.SetTexture("_BumpMap", Texture(tex + info.normal));
            material.SetTexture("_MetallicGlossMap", Texture(tex + info.metallicSmoothness));
            material.SetTexture("_OcclusionMap", Texture(tex + info.occlusion));
            material.SetColor("_BaseColor", id == "office_notepads" ? new Color(.81f, .77f, .65f, 1) : new Color(.83f, .78f, .70f, 1));
            material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", .8f);
            material.SetFloat("_BumpScale", .7f); material.SetFloat("_OcclusionStrength", .85f);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_OCCLUSIONMAP");
            material.DisableKeyword("_EMISSION"); material.SetFloat("_Cull", info.doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            material.SetFloat("_Surface", 0); material.SetFloat("_ZWrite", 1);
            material.enableInstancing = true; material.doubleSidedGI = info.doubleSided;
            EditorUtility.SetDirty(material); return material;
        }

        static Texture2D Texture(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new InvalidDataException("Missing approved texture: " + path);

        public static Report Apply(Transform root)
        {
            if (!root || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Place approved props outside Play mode.");
            var existing = root.Find(GroupName);
            if (existing) return Audit(root);
            var paper = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(Ids[0], "office_notepads_yellow_pad"));
            var books = Enumerable.Range(1, 20).Select(i => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(Ids[1], "book_encyclopedia_set_01_book" + i.ToString("00")))).ToArray();
            if (!paper || books.Any(b => !b)) throw new InvalidDataException("Run SchoolRefinementReadyPropsPass.Import before scene placement.");
            var report = new Report { architectureBefore = SchoolChapterThreePass.ArchitectureSignature(root) };
            var scripts = root.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b).ToArray();
            string storyBefore = ScriptSignature(scripts);
            var colliders = root.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger).ToArray();
            var floors = colliders.Where(IsFloor).ToArray();
            var doors = root.GetComponentsInChildren<InteractiveDoor>(true).Where(d => d.leafCollider).Select(d => d.leafCollider.bounds).ToArray();
            var clues = scripts.Where(b => b.GetType().Name.Contains("Clue") || b.GetType().Name.Contains("Anchor") || b.GetType().Name.Contains("Pickup")).Select(b => b.transform.position).ToArray();
            var rooms = ReadRooms(root);
            var acceptedBounds = new List<Bounds>();
            var group = new GameObject(GroupName).transform; group.SetParent(root, false);
            Physics.SyncTransforms();
            try
            {
                int index = 0;
                foreach (var room in rooms)
                {
                    var roomGroup = new GameObject(room.name).transform; roomGroup.SetParent(group, false);
                    var rng = new System.Random(StableSeed(room.name));
                    int placed = 0;
                    // Flat books and notebooks stay at room edges, away from the
                    // source aisles, door thresholds and all investigative props.
                    for (int kind = 0; kind < 3; kind++)
                    {
                        var prefab = kind == 2 ? paper : books[(index * 7 + kind * 3) % books.Length];
                        var piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab, roomGroup);
                        piece.transform.localScale = Vector3.one;
                        piece.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, kind == 2 ? 0 : 90);
                        bool accepted = false;
                        for (int attempt = 0; attempt < 18; attempt++)
                        {
                            bool xEdge = rng.Next(2) == 0, high = rng.Next(2) == 0;
                            var p = new Vector3(xEdge ? (high ? room.bounds.max.x - .44f : room.bounds.min.x + .44f)
                                    : Mathf.Lerp(room.bounds.min.x + .45f, room.bounds.max.x - .45f, (float)rng.NextDouble()), room.floor,
                                !xEdge ? (high ? room.bounds.max.z - .44f : room.bounds.min.z + .44f)
                                    : Mathf.Lerp(room.bounds.min.z + .45f, room.bounds.max.z - .45f, (float)rng.NextDouble()));
                            piece.transform.position = p;
                            var bounds = MeshBounds(piece.transform);
                            if (!FloorAt(floors, p, room.floor, out float floor)) { report.rejectedCandidates++; continue; }
                            piece.transform.position += Vector3.up * (floor + .024f - bounds.min.y);
                            bounds = MeshBounds(piece.transform);
                            if (!Fits(room, bounds) || NearAisle(room, bounds) || doors.Any(d => SameLevel(d, bounds) && HorizontalDistance(d.center, bounds) < 1.25f)
                                || clues.Any(c => Mathf.Abs(c.y - bounds.center.y) < 2 && HorizontalDistance(c, bounds) < 1.15f)
                                || !Supported(floors, bounds, floor) || colliders.Any(c => !IsFloor(c) && c.bounds.Intersects(bounds))
                                || acceptedBounds.Any(b => b.Intersects(bounds)))
                            { report.rejectedCandidates++; continue; }
                            accepted = true; acceptedBounds.Add(bounds); piece.name = kind == 2 ? "Caderno antigo — CC0" : "Enciclopédia caída — CC0";
                            piece.isStatic = true; placed++;
                            if (kind == 2) report.notepads++; else report.books++;
                            break;
                        }
                        if (!accepted) UnityEngine.Object.DestroyImmediate(piece);
                    }
                    if (placed > 0) report.rooms++;
                    else UnityEngine.Object.DestroyImmediate(roomGroup.gameObject);
                    index++;
                }
                report.architectureAfter = SchoolChapterThreePass.ArchitectureSignature(root);
                report.architecturePreserved = report.architectureBefore == report.architectureAfter;
                report.storyPreserved = storyBefore == ScriptSignature(scripts);
                report.addedColliders = group.GetComponentsInChildren<Collider>(true).Length;
                report.lodGroups = group.GetComponentsInChildren<LODGroup>(true).Length;
                report.allPiecesSupported = true;
                report.notes = new[] { "Only two approved Poly Haven CC0 models, source glTF/1K maps preserved and checksum verified.",
                    "Individual books have separate cover/paper materials and reduced distant LOD; paper variants use their original curved geometry.",
                    "Decorative pieces have no colliders, interactive scripts or light. Native floors support each footprint; source aisle, thresholds and clue zones remain clear.",
                    "Deterministic edge dressing supplements the independent classroom furniture refinement; it does not modify existing props, walls, story IDs or source transforms." };
                if (!report.architecturePreserved || !report.storyPreserved || report.addedColliders != 0)
                    throw new InvalidOperationException("Ready props changed protected school state.");
                if (report.books == 0 || report.notepads == 0) throw new InvalidOperationException("No safe floor area found for the approved books/notepads.");
                Directory.CreateDirectory(Output); File.WriteAllText(Output + "/placement.json", JsonUtility.ToJson(report, true));
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                return report;
            }
            catch { UnityEngine.Object.DestroyImmediate(group.gameObject); throw; }
        }

        static Room[] ReadRooms(Transform root)
        {
            var source = JsonUtility.FromJson<SchoolClassroomPhotoPass.Report>(File.ReadAllText(SchoolMapBuilder.Output + "/School-classroom-photo-report.json"));
            var units = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Sala — cadeira azul com carteira").ToArray();
            var doors = root.GetComponentsInChildren<InteractiveDoor>(true).Where(d => d.leafCollider).ToArray();
            var rooms = new List<Room>();
            foreach (var data in source.rooms)
            {
                string name = data.name + " — pavimento " + data.floor;
                var roomUnits = units.Where(u => u.parent && u.parent.name == name).ToArray();
                if (roomUnits.Length == 0) continue;
                float floor = roomUnits.Select(u => u.position.y).OrderBy(y => y).ElementAt(roomUnits.Length / 2);
                var box = data.adaptedPdfBox;
                var nw = SchoolMapBuilder.Point(box[0], box[1], floor); var se = SchoolMapBuilder.Point(box[2], box[3], floor);
                rooms.Add(new Room { name = name, floor = floor,
                    bounds = new Bounds((nw + se) * .5f, new Vector3(Mathf.Abs(se.x - nw.x), 0, Mathf.Abs(nw.z - se.z))),
                    center = new Vector3(data.center.x, floor, data.center.z),
                    aisleStart = new Vector3(data.aisleStart.x, floor, data.aisleStart.z),
                    aisleEnd = new Vector3(data.aisleEnd.x, floor, data.aisleEnd.z),
                    thresholds = doors.Where(d => data.doorIds.Contains(d.persistentId)).Select(d =>
                        new Vector3(d.leafCollider.bounds.center.x, floor, d.leafCollider.bounds.center.z)).ToArray() });
            }
            return rooms.ToArray();
        }

        static bool IsFloor(Collider c)
        {
            var n = c.name.ToLowerInvariant(); return c.bounds.size.y < .65f && (n.Contains("piso") || n.Contains("laje") || n.Contains("patamar") || n.Contains("degrau"));
        }
        static bool FloorAt(Collider[] floors, Vector3 p, float expected, out float height)
        {
            height = expected; bool found = false; float nearest = .31f;
            var ray = new Ray(new Vector3(p.x, expected + .55f, p.z), Vector3.down);
            foreach (var c in floors)
                if (c.Raycast(ray, out var hit, .9f) && hit.normal.y > .95f && Mathf.Abs(hit.point.y - expected) < nearest)
                { height = hit.point.y; nearest = Mathf.Abs(height - expected); found = true; }
            return found;
        }
        static bool Supported(Collider[] floors, Bounds b, float height)
        {
            foreach (float x in new[] { b.min.x + .01f, b.max.x - .01f })
                foreach (float z in new[] { b.min.z + .01f, b.max.z - .01f })
                    if (!FloorAt(floors, new Vector3(x, height, z), height, out float other) || Mathf.Abs(other - height) > .02f) return false;
            return true;
        }
        static bool Fits(Room r, Bounds b) => b.min.x > r.bounds.min.x + .14f && b.max.x < r.bounds.max.x - .14f
            && b.min.z > r.bounds.min.z + .14f && b.max.z < r.bounds.max.z - .14f;
        static bool SameLevel(Bounds a, Bounds b) => Mathf.Abs(a.min.y - b.min.y) < 1;
        static float HorizontalDistance(Vector3 p, Bounds b)
        { float x = Mathf.Max(b.min.x - p.x, 0, p.x - b.max.x), z = Mathf.Max(b.min.z - p.z, 0, p.z - b.max.z); return Mathf.Sqrt(x * x + z * z); }
        static bool NearAisle(Room r, Bounds b)
        {
            var d = r.aisleEnd - r.aisleStart; d.y = 0; var cross = new Vector3(d.z, 0, -d.x).normalized;
            float half = Mathf.Abs(cross.x) > .5f ? r.bounds.extents.x : r.bounds.extents.z;
            return NearSegment(b, r.aisleStart, r.aisleEnd, .64f) || NearSegment(b, r.center - cross * half, r.center + cross * half, .57f)
                || r.thresholds.Any(t => NearSegment(b, t, r.center, .64f));
        }
        static bool NearSegment(Bounds b, Vector3 a, Vector3 end, float clearance)
        {
            // Sample a finite segment at <=10cm intervals; reserve its full XZ
            // radius plus the footprint, even though these props cannot collide.
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, end) / .1f));
            for (int i = 0; i <= count; i++) if (HorizontalDistance(Vector3.Lerp(a, end, i / (float)count), b) < clearance) return true;
            return false;
        }
        static Bounds MeshBounds(Transform t)
        {
            bool first = true; var result = new Bounds();
            foreach (var f in t.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh) foreach (var p in f.sharedMesh.vertices)
                { var point = f.transform.TransformPoint(p); if (first) { result = new Bounds(point, Vector3.zero); first = false; } else result.Encapsulate(point); }
            if (first) throw new InvalidDataException("Approved prefab contains no valid mesh."); return result;
        }
        static string ScriptSignature(MonoBehaviour[] components)
        {
            string value = string.Join("\n", components.Where(c => c).Select(c => c.GetEntityId() + ":" + EditorJsonUtility.ToJson(c)).OrderBy(s => s, StringComparer.Ordinal));
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
        static int StableSeed(string name) { unchecked { int seed = 271; foreach (char c in name) seed = seed * 31 + c; return seed; } }
        public static Report Audit(Transform root)
        {
            var r = JsonUtility.FromJson<Report>(File.ReadAllText(Output + "/placement.json"));
            var group = root.Find(GroupName); if (!group) throw new InvalidOperationException("Ready prop group missing.");
            r.addedColliders = group.GetComponentsInChildren<Collider>(true).Length; r.lodGroups = group.GetComponentsInChildren<LODGroup>(true).Length;
            if (r.addedColliders != 0 || r.lodGroups != r.books + r.notepads || group.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("Ready prop native audit failed.");
            return r;
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
