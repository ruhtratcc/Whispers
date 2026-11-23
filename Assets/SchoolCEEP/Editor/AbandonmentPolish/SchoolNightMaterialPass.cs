// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>School-instance material correction. Original prefab materials and narrative assets remain untouched.</summary>
    public static class SchoolNightMaterialPass
    {
        public const string Output = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/AbandonmentPolish";
        public const string JournalPath = Output + "/night-material-journal.json";
        const string Folder = SchoolAbandonedSurfacePass.MaterialFolder + "/Night";
        static readonly HashSet<string> GlassPaths = new HashSet<string>(StringComparer.Ordinal) {
            "Assets/SchoolCEEP/Materials/Vidro escuro.mat",
            "Assets/SchoolCEEP/Materials/ChapterThree/Vidro sujo.mat",
            "Assets/SchoolCEEP/Materials/AbandonmentPolish/Canteen/CanteenNight-PhotoRefectoryServiceGlass.mat"
        };
        [Serializable] public sealed class MaterialRecord { public string source, replacement; public bool glass; }
        [Serializable] public sealed class RendererRecord
        {
            public string path, beforeMesh, afterMesh, beforeMeshId, afterMeshId;
            public int[] hierarchy;
            public string[] beforeMaterials, afterMaterials;
            public int blueTriangles;
        }
        [Serializable] public sealed class Report
        {
            public int revision = 1, changedRenderers, changedMaterialSlots, glassSlots, blueWallRenderers, blueWallTriangles;
            public string scene, root, state, protectedBefore, protectedAfter;
            public bool protectedStatePreserved, geometryAndUVsPreserved;
            public MaterialRecord[] materials;
            public RendererRecord[] renderers;
            public string[] notes;
        }
        [Serializable] sealed class Survey { public SurveyRow[] materials; }
        [Serializable] sealed class SurveyRow { public string path; public float metallic, env; }

        public static Report Apply(Transform root)
        {
            RequireScene(root);
            if (File.Exists(JournalPath))
            {
                var existing = Read(root);
                if (existing.state == "applied") return Audit(root);
                return Assign(root, existing, true);
            }
            string surveyPath = Output + "/material-survey-before.json";
            if (!File.Exists(surveyPath)) throw new InvalidDataException("The school material survey is required.");
            var survey = JsonUtility.FromJson<Survey>("{\"materials\":" + File.ReadAllText(surveyPath) + "}");
            if (survey.materials == null || survey.materials.Length != 184)
                throw new InvalidDataException("Expected the reviewed 184-material school survey.");
            var allowed = new HashSet<string>(survey.materials.Where(v => GlassPaths.Contains(v.path)
                || (SafeWorldPath(v.path) && v.env > .5f && v.metallic > .1f)).Select(v => v.path), StringComparer.Ordinal);
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var variants = new Dictionary<string, Material>(StringComparer.Ordinal);
            var materialRecords = new List<MaterialRecord>();
            var records = new Dictionary<MeshRenderer, RendererRecord>();
            var excluded = NarrativeVisuals(root);
            var report = new Report { scene = root.gameObject.scene.path, root = root.name, state = "pending",
                protectedBefore = SchoolAbandonedSurfacePass.CaptureProtectedState(root) };
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.gameObject.activeInHierarchy || ExcludedRenderer(renderer, root, excluded)) continue;
                var before = renderer.sharedMaterials;
                var after = ReplaceMaterials(before, allowed, variants, materialRecords);
                if (!before.SequenceEqual(after)) records.Add(renderer, MakeRecord(renderer, root, before, after));
            }
            try
            {
                SchoolAbandonedSurfacePass.ApplyBlueWalls(root, blue =>
                {
                    foreach (var change in blue)
                    {
                        if (ExcludedRenderer(change.renderer, root, excluded))
                            throw new InvalidOperationException("Narrative visual unexpectedly selected as blue masonry.");
                        change.afterMaterials = ReplaceMaterials(change.afterMaterials, allowed, variants, materialRecords);
                        if (!records.TryGetValue(change.renderer, out var record))
                        {
                            record = MakeRecord(change.renderer, root, change.beforeMaterials, change.afterMaterials);
                            records.Add(change.renderer, record);
                        }
                        record.afterMaterials = MaterialPaths(change.afterMaterials);
                        record.beforeMesh = AssetPath(change.beforeMesh); record.afterMesh = AssetPath(change.afterMesh);
                        record.beforeMeshId = GlobalObjectId.GetGlobalObjectIdSlow(change.beforeMesh).ToString();
                        record.afterMeshId = GlobalObjectId.GetGlobalObjectIdSlow(change.afterMesh).ToString();
                        record.blueTriangles = change.selectedTriangles;
                    }
                    report.materials = materialRecords.ToArray(); report.renderers = records.Values.ToArray();
                    if (report.renderers.Length == 0) throw new InvalidOperationException("No surveyed school material assignments qualify.");
                    AssetDatabase.SaveAssets(); Write(report);
                });
                return Assign(root, report, true);
            }
            catch
            {
                if (report.renderers != null)
                {
                    SetReferences(root, report, false);
                    report.state = "restored"; Write(report);
                    EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                }
                throw;
            }
        }

        public static Report Restore(Transform root) => Assign(root, Read(root), false);

        public static Report Audit(Transform root)
        {
            var report = Read(root);
            if (report.state != "applied") throw new InvalidOperationException("The material journal is not applied.");
            ValidateReferences(root, report, false, true);
            VerifyMaterials(report); VerifyGeometry(report);
            report.geometryAndUVsPreserved = true;
            return report;
        }

        static Report Assign(Transform root, Report report, bool replacement)
        {
            RequireScene(root); ValidateReferences(root, report, true, replacement);
            if (replacement) { VerifyMaterials(report); VerifyGeometry(report); }
            string before = SchoolAbandonedSurfacePass.CaptureProtectedState(root);
            // Journal already exists before assigning any renderer or mesh reference.
            SetReferences(root, report, replacement);
            string after = SchoolAbandonedSurfacePass.CaptureProtectedState(root);
            if (before != after)
            {
                SetReferences(root, report, !replacement);
                throw new InvalidOperationException("A collider, light or story component changed during the night material correction.");
            }
            report.protectedBefore = before; report.protectedAfter = after; report.protectedStatePreserved = true;
            report.geometryAndUVsPreserved = true; report.state = replacement ? "applied" : "restored";
            report.changedRenderers = report.renderers.Length;
            report.blueWallRenderers = report.renderers.Count(v => v.blueTriangles > 0);
            report.blueWallTriangles = report.renderers.Sum(v => v.blueTriangles);
            report.changedMaterialSlots = report.renderers.Sum(v => v.afterMaterials.Where((p, i) => i >= v.beforeMaterials.Length || p != v.beforeMaterials[i]).Count());
            var glass = new HashSet<string>(report.materials.Where(v => v.glass).Select(v => v.replacement));
            report.glassSlots = report.renderers.Sum(v => v.afterMaterials.Count(p => glass.Contains(p)));
            report.notes = new[] {
                "Only surveyed school-instance materials are replaced. Original materials, textures, prefab references and other scenes remain untouched.",
                "Architectural glass retains its color, alpha, blend, culling, textures and UVs; environment reflections and direct highlights are disabled, metallic is zero and smoothness is at most .12.",
                "Static world metal keeps authored PBR maps, metallic, smoothness and direct flashlight specular; only environment reflections are disabled. Chapter/Data/UI, emissive materials and narrative chair targets are excluded; the surveyed non-narrative Vidro sujo decoration is the sole Chapter material exception.",
                "Blue masonry paint is separated from window frames using native backing colliders and a minimum .25 m horizontal face span. All vertices, triangle winding and UV channels remain identical; paint is matte and 35% desaturated.",
                "Journal precedes scene assignments. Reapply audits existing references; Restore uses retained original mesh/material assets. No map rebuild, collider, runtime component, light, scene save or new model." };
            Write(report); EditorSceneManager.MarkSceneDirty(root.gameObject.scene); return report;
        }

        static Material[] ReplaceMaterials(Material[] materials, HashSet<string> allowed,
            Dictionary<string, Material> variants, List<MaterialRecord> records)
        {
            var result = (Material[])materials.Clone();
            for (int i = 0; i < result.Length; i++)
            {
                var source = result[i]; string path = source ? AssetDatabase.GetAssetPath(source) : "";
                if (!allowed.Contains(path) || Emissive(source)) continue;
                if (!variants.TryGetValue(path, out var replacement))
                {
                    bool glass = GlassPaths.Contains(path);
                    string clonePath = Folder + "/Night-" + AssetDatabase.AssetPathToGUID(path) + ".mat";
                    replacement = AssetDatabase.LoadAssetAtPath<Material>(clonePath);
                    if (!replacement)
                    {
                        replacement = new Material(source) { name = "Night-" + source.name };
                        replacement.SetFloat("_EnvironmentReflections", 0); replacement.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                        if (glass)
                        {
                            replacement.SetFloat("_Metallic", 0);
                            replacement.SetFloat("_Smoothness", Mathf.Min(.12f, source.GetFloat("_Smoothness")));
                            replacement.SetFloat("_SpecularHighlights", 0); replacement.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                        }
                        AssetDatabase.CreateAsset(replacement, clonePath);
                    }
                    variants.Add(path, replacement); records.Add(new MaterialRecord { source = path, replacement = clonePath, glass = glass });
                }
                result[i] = replacement;
            }
            return result;
        }

        static bool SafeWorldPath(string path) => !string.IsNullOrEmpty(path)
            && (path.StartsWith("Assets/models/SchoolIntegration/", StringComparison.Ordinal)
                || path.StartsWith("Assets/models/RefinementIntegration/", StringComparison.Ordinal)
                || path.StartsWith("Assets/SchoolCEEP/Materials/", StringComparison.Ordinal))
            && !path.Contains("/Chapter") && !path.Contains("/FinalChapters/") && !path.Contains("/Data/")
            && !path.Contains("/UI/") && !path.Contains("/Night/");
        static bool Emissive(Material material) => material.IsKeywordEnabled("_EMISSION")
            || (material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > .00001f);
        static HashSet<Transform> NarrativeVisuals(Transform root)
        {
            var result = new HashSet<Transform>();
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true).Where(v => v))
            {
                var serialized = new SerializedObject(script);
                foreach (string field in new[] { "chair", "changedChair", "demonVisual" })
                {
                    var property = serialized.FindProperty(field);
                    if (property != null && property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue is Transform target) result.Add(target);
                }
            }
            return result;
        }
        static bool ExcludedRenderer(MeshRenderer renderer, Transform root, HashSet<Transform> excluded)
        {
            if (excluded.Any(v => renderer.transform == v || renderer.transform.IsChildOf(v))) return true;
            for (var t = renderer.transform; t && t != root; t = t.parent)
            {
                if (t.GetComponent<Canvas>() || t.GetComponent<Rigidbody>()) return true;
                foreach (var script in t.GetComponents<MonoBehaviour>().Where(v => v))
                {
                    string name = script.GetType().Name;
                    if (name.Contains("Clue") || name.Contains("Anchor") || name.Contains("Pickup") || name.Contains("Collectible")
                        || name.Contains("Ghost") || name.Contains("StoryWorldEvent") || name.Contains("Poltergeist")) return true;
                }
            }
            return false;
        }

        static RendererRecord MakeRecord(MeshRenderer renderer, Transform root, Material[] before, Material[] after)
        {
            var hierarchy = new List<int>(); var names = new List<string>();
            for (var t = renderer.transform; t != root; t = t.parent) { hierarchy.Add(t.GetSiblingIndex()); names.Add(t.name); }
            hierarchy.Reverse(); names.Reverse();
            return new RendererRecord { path = string.Join("/", names), hierarchy = hierarchy.ToArray(),
                beforeMaterials = MaterialPaths(before), afterMaterials = MaterialPaths(after) };
        }
        static string AssetPath(UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) throw new InvalidDataException("A replacement source is not a persistent asset: " + asset);
            return path;
        }
        static string[] MaterialPaths(Material[] materials) => materials.Select(v => v ? AssetPath(v) : "").ToArray();
        static Material[] Materials(string[] paths) => paths.Select(p => string.IsNullOrEmpty(p) ? null : Required<Material>(p)).ToArray();
        static T Required<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) throw new InvalidDataException("Journal asset is missing: " + path);
            return asset;
        }
        static Mesh MeshAsset(RendererRecord row, bool replacement)
        {
            string id = replacement ? row.afterMeshId : row.beforeMeshId;
            if (!GlobalObjectId.TryParse(id, out var global)) throw new InvalidDataException("Invalid journal mesh identity: " + row.path);
            var mesh = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(global) as Mesh;
            if (!mesh || AssetDatabase.GetAssetPath(mesh) != (replacement ? row.afterMesh : row.beforeMesh))
                throw new InvalidDataException("Journal mesh asset is missing: " + row.path);
            return mesh;
        }
        static MeshRenderer Resolve(Transform root, RendererRecord record)
        {
            var t = root; var names = new List<string>();
            foreach (int index in record.hierarchy)
            {
                if (index < 0 || index >= t.childCount) throw new InvalidDataException("Journal hierarchy changed: " + record.path);
                t = t.GetChild(index); names.Add(t.name);
            }
            var renderer = t.GetComponent<MeshRenderer>();
            if (!renderer || string.Join("/", names) != record.path) throw new InvalidDataException("Journal renderer moved: " + record.path);
            return renderer;
        }
        static void ValidateReferences(Transform root, Report report, bool either, bool replacement)
        {
            foreach (var record in report.renderers)
            {
                var renderer = Resolve(root, record); var current = MaterialPaths(renderer.sharedMaterials);
                bool old = current.SequenceEqual(record.beforeMaterials), newer = current.SequenceEqual(record.afterMaterials);
                if (!(either ? old || newer : replacement ? newer : old)) throw new InvalidOperationException("Journal material references changed: " + record.path);
                if (string.IsNullOrEmpty(record.beforeMesh)) continue;
                var filter = renderer.GetComponent<MeshFilter>(); var mesh = filter ? filter.sharedMesh : null;
                var before = MeshAsset(record, false); var after = MeshAsset(record, true);
                if (!(either ? mesh == before || mesh == after : replacement ? mesh == after : mesh == before))
                    throw new InvalidOperationException("Journal mesh reference changed: " + record.path);
            }
        }
        static void SetReferences(Transform root, Report report, bool replacement)
        {
            // Resolve all assets first; a missing source cannot leave a partially restored map.
            var planned = report.renderers.Select(r => new { record = r, renderer = Resolve(root, r),
                materials = Materials(replacement ? r.afterMaterials : r.beforeMaterials),
                mesh = string.IsNullOrEmpty(r.beforeMesh) ? null : MeshAsset(r, replacement) }).ToArray();
            foreach (var row in planned)
            {
                row.renderer.sharedMaterials = row.materials;
                if (row.mesh) row.renderer.GetComponent<MeshFilter>().sharedMesh = row.mesh;
            }
        }
        static void VerifyMaterials(Report report)
        {
            foreach (var row in report.materials)
            {
                var source = Required<Material>(row.source); var replacement = Required<Material>(row.replacement);
                if (replacement.shader != source.shader || replacement.GetFloat("_EnvironmentReflections") != 0
                    || !replacement.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"))
                    throw new InvalidDataException("Night material does not suppress environment reflections: " + row.replacement);
                foreach (string property in source.GetTexturePropertyNames())
                    if (source.GetTexture(property) != replacement.GetTexture(property)
                        || source.GetTextureScale(property) != replacement.GetTextureScale(property)
                        || source.GetTextureOffset(property) != replacement.GetTextureOffset(property))
                        throw new InvalidDataException("Night material changed PBR texture or tiling: " + row.replacement);
                foreach (string property in new[] { "_BaseColor", "_EmissionColor" })
                    if (source.HasProperty(property) && source.GetColor(property) != replacement.GetColor(property))
                        throw new InvalidDataException("Night material changed color/opacity: " + row.replacement);
                foreach (string property in new[] { "_Surface", "_Blend", "_Cull", "_ZWrite", "_SrcBlend", "_DstBlend" })
                    if (source.HasProperty(property) && source.GetFloat(property) != replacement.GetFloat(property))
                        throw new InvalidDataException("Night material changed surface state: " + row.replacement);
                if (row.glass ? replacement.GetFloat("_Metallic") != 0 || replacement.GetFloat("_Smoothness") > .12f
                    || replacement.GetFloat("_SpecularHighlights") != 0 || !replacement.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF")
                    : new[] { "_Metallic", "_Smoothness", "_SpecularHighlights" }.Any(p => source.HasProperty(p) && source.GetFloat(p) != replacement.GetFloat(p)))
                    throw new InvalidDataException("Night material has an unexpected direct-light response: " + row.replacement);
            }
        }
        static void VerifyGeometry(Report report)
        {
            foreach (var row in report.renderers.Where(v => !string.IsNullOrEmpty(v.beforeMesh)))
            {
                var before = MeshAsset(row, false); var after = MeshAsset(row, true);
                if (before.vertexCount != after.vertexCount || before.bounds != after.bounds || !before.vertices.SequenceEqual(after.vertices)
                    || !before.normals.SequenceEqual(after.normals) || !before.tangents.SequenceEqual(after.tangents)
                    || !before.colors32.SequenceEqual(after.colors32)) throw new InvalidDataException("Blue paint changed vertex data: " + row.path);
                for (int channel = 0; channel < 8; channel++)
                {
                    var a = new List<Vector4>(); var b = new List<Vector4>(); before.GetUVs(channel, a); after.GetUVs(channel, b);
                    if (!a.SequenceEqual(b)) throw new InvalidDataException("Blue paint changed UV channel " + channel + ": " + row.path);
                }
                if (!Triangles(before).SequenceEqual(Triangles(after))) throw new InvalidDataException("Blue paint lost or flipped a triangle: " + row.path);
            }
        }
        static string[] Triangles(Mesh mesh)
        {
            var result = new List<string>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var indices = mesh.GetTriangles(sub);
                for (int i = 0; i < indices.Length; i += 3) result.Add(indices[i] + "," + indices[i + 1] + "," + indices[i + 2]);
            }
            return result.OrderBy(v => v, StringComparer.Ordinal).ToArray();
        }
        static Report Read(Transform root)
        {
            RequireScene(root);
            if (!File.Exists(JournalPath)) throw new InvalidOperationException("No night material journal exists.");
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(JournalPath));
            if (report.revision != 1 || report.scene != root.gameObject.scene.path || report.root != root.name
                || report.materials == null || report.renderers == null) throw new InvalidDataException("Night material journal does not match this school.");
            return report;
        }
        static void Write(Report report)
        {
            Directory.CreateDirectory(Output); string temporary = JournalPath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            if (File.Exists(JournalPath)) File.Replace(temporary, JournalPath, null); else File.Move(temporary, JournalPath);
        }
        static void RequireScene(Transform root)
        {
            if (!root || EditorUtility.IsPersistent(root) || EditorApplication.isPlayingOrWillChangePlaymode
                || root.gameObject.scene.path != SchoolIntegrationPass.ScenePath)
                throw new InvalidOperationException("Operate on the saved SchoolCEEP scene outside Play mode.");
        }
    }
}
