// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    /// <summary>Union of eligible native rectangular wall solids, after photo corrections and before batching.</summary>
    public static class SchoolMasonryOptimizationPass
    {
        public const int SourceRevision = 2;
        public const string BaselineFile = "School-masonry-optimization-baseline.json";
        public const string ReportFile = "School-masonry-optimization-report.json";
        public const string ProofFile = "School-masonry-optimization-proof.json";
        const float FaceTolerance = .00002f;
        [Serializable] public sealed class Rectangle
        {
            public string parentPath, name, materialPath;
            public int siblingIndex;
            public Vector3 min, max;
            public bool optimized;
        }
        [Serializable] public sealed class Trim
        {
            public string parentPath, name;
            public int siblingIndex;
            public Vector3 min, max;
            public bool containedByMasonry, containedExactly;
            public double rendererBoundsExcessMeters;
        }
        [Serializable] public sealed class BuildReport
        {
            public int sourceRevision = SourceRevision;
            public int renderedWallPiecesBefore, renderedWallPiecesAfter, wallBoxCollidersBefore, wallBoxCollidersAfter;
            public int renderersRemoved, collidersRemoved, mergedRuns, containedPiecesRemoved, rejectedFloatRepresentations, generatedMeshAssets;
            public int rendererOnlyTrims, rendererOnlyTrimsContained, totalArchitectureCollidersBefore, totalArchitectureCollidersAfter;
            public int rendererOnlyTrimsContainedExactly, rendererOnlyTrimsWithinRendererPrecision;
            public float trimContainmentToleranceMeters = FaceTolerance;
            public double maxTrimRendererBoundsExcessMeters;
            public bool exactPhysicalUnionPreserved, interactionIdsPreserved, trimColliderOmissionCovered;
            public double independentlyMeasuredDifferenceCubicMeters;
            public string architecturePath, baselineFile, interactionDigest;
            public string[] notes;
        }
        [Serializable] public sealed class Baseline
        {
            public int sourceRevision = SourceRevision;
            public string architecturePath, interactionDigest;
            public Rectangle[] original, normalized;
            public Trim[] trims;
            public BuildReport build;
        }
        [Serializable] public sealed class Failure
        {
            public string parentPath, name, detail;
            public Vector3 point;
        }
        [Serializable] public sealed class ProofReport
        {
            public int sourceRevision = SourceRevision;
            public string scene, baselineFile;
            public bool passed, exactPhysicalUnionPreserved, interactionIdsPreserved, editorOnly = true, gameplayExecuted, checkpointTouched;
            public int renderedWallPiecesBefore, renderedWallPiecesAfter, collidersRemoved;
            public int nativeColliderChecks, missingOrChangedColliders, renderedFaceSamples, renderedFaceFailures;
            public int rendererOnlyTrims, rendererOnlyTrimsContained, trimFailures;
            public int rendererOnlyTrimsContainedExactly, rendererOnlyTrimsWithinRendererPrecision;
            public float trimContainmentToleranceMeters = FaceTolerance;
            public double maxTrimRendererBoundsExcessMeters;
            public double independentlyMeasuredDifferenceCubicMeters;
            public Failure[] failures;
            public string[] notes;
        }
        sealed class Part
        {
            public GameObject go;
            public Transform parent;
            public MeshFilter filter;
            public MeshRenderer renderer;
            public BoxCollider collider;
            public Bounds bounds;
            public string key;
            public bool optimized;
        }
        sealed class RenderMesh
        {
            public string material;
            public Bounds bounds;
            public Vector3[] vertices;
            public int[] triangles;
        }
        static string meshFolder;
        static int meshIndex;

        public static BuildReport Apply(Transform architecture)
        {
            if (!architecture) throw new ArgumentNullException(nameof(architecture));
            var parts = architecture.GetComponentsInChildren<MeshRenderer>(true).Select(Eligible).Where(p => p != null).ToList();
            var original = parts.Select(Record).ToArray();
            var interactions = InteractionDigest(architecture);
            var report = new BuildReport { architecturePath = Path(architecture), interactionDigest = interactions,
                renderedWallPiecesBefore = parts.Count, wallBoxCollidersBefore = parts.Count,
                totalArchitectureCollidersBefore = architecture.GetComponentsInChildren<Collider>(true).Length,
                baselineFile = SchoolMapBuilder.Output + "/" + BaselineFile };
            meshFolder = null; meshIndex = 0;
            // Containment removes wholly redundant cubes without changing the survivor.
            foreach (var group in parts.GroupBy(p => p.key))
            {
                var members = group.ToArray();
                for (int i = 0; i < members.Length; i++) for (int j = i + 1; j < members.Length; j++)
                {
                    var a = members[i]; var b = members[j];
                    if (!a.go || !b.go) continue;
                    Part keep = Contains(a.bounds, b.bounds) ? a : Contains(b.bounds, a.bounds) ? b : null;
                    if (keep == null) continue;
                    var discard = keep == a ? b : a;
                    keep.optimized = true; UnityEngine.Object.DestroyImmediate(discard.go); report.containedPiecesRemoved++;
                }
            }
            parts.RemoveAll(p => !p.go);
            bool changed;
            do
            {
                changed = false;
                // The two sweeps can join full-width adjacent pieces, or parallel
                // CAD face cubes spanning the same wall length. No positive gap joins.
                foreach (bool alongX in new[] { false, true })
                {
                    foreach (var group in parts.Where(p => p.go).GroupBy(p => SweepKey(p, alongX)).ToArray())
                    {
                        var ordered = group.OrderBy(p => alongX ? p.bounds.min.x : p.bounds.min.z).ToArray();
                        var run = new List<Part>(); float end = float.NegativeInfinity;
                        foreach (var p in ordered)
                        {
                            float start = alongX ? p.bounds.min.x : p.bounds.min.z;
                            if (run.Count > 0 && start > end)
                            {
                                changed |= MergeRun(run, report); run.Clear(); end = float.NegativeInfinity;
                            }
                            run.Add(p); end = Mathf.Max(end, alongX ? p.bounds.max.x : p.bounds.max.z);
                        }
                        changed |= MergeRun(run, report);
                    }
                    parts.RemoveAll(p => !p.go);
                }
            } while (changed);
            var normalized = parts.Select(Record).ToArray();
            double difference = VolumeDifference(original, normalized);
            var trims = InspectTrims(architecture);
            report.renderedWallPiecesAfter = normalized.Length; report.wallBoxCollidersAfter = normalized.Length;
            report.renderersRemoved = report.renderedWallPiecesBefore - normalized.Length;
            report.collidersRemoved = report.wallBoxCollidersBefore - normalized.Length;
            report.totalArchitectureCollidersAfter = architecture.GetComponentsInChildren<Collider>(true).Length;
            report.generatedMeshAssets = meshIndex; report.independentlyMeasuredDifferenceCubicMeters = difference;
            report.exactPhysicalUnionPreserved = difference < 1e-10;
            report.interactionIdsPreserved = interactions == InteractionDigest(architecture);
            report.rendererOnlyTrims = trims.Length; report.rendererOnlyTrimsContained = trims.Count(t => t.containedByMasonry);
            report.rendererOnlyTrimsContainedExactly = trims.Count(t => t.containedExactly);
            report.rendererOnlyTrimsWithinRendererPrecision = trims.Count(t => t.containedByMasonry && !t.containedExactly);
            report.maxTrimRendererBoundsExcessMeters = trims.Length == 0 ? 0 : trims.Max(t => t.rendererBoundsExcessMeters);
            report.trimColliderOmissionCovered = report.rendererOnlyTrims == report.rendererOnlyTrimsContained;
            report.notes = new[] {
                "Only enabled static single-material unit-cube wall renderers with matching BoxColliders, no children and no special components are eligible.",
                "Same parent/material/name/renderer state/physics material/contact offset/height. Parallel MeshColliders, multi-material finishes, interactive objects and decorative meshes are excluded.",
                "Sweeps join exactly matching transverse extents and overlapping/touching intervals. Positive gaps are preserved; float32 representations changing any physical boundary are rejected.",
                "Independent 3D endpoint-cell integration compares the original and final occupied union per parent and material. Overlapping source volumes are never counted twice.",
                "New cube UVs extrapolate the original per-face world-coordinate UV mapping, retaining texture density instead of stretching across a longer merged piece.",
                "Renderer-only base trims are counted separately. Exact containment and float renderer-bound discrepancies up to 0.00002m are reported separately; larger protrusions fail. This tolerance only compares renderer bounds with an existing masonry BoxCollider; it never changes native collision geometry or its exact union comparison. No trim collider is removed by this pass.",
                "Door/drawer IDs remain unchanged. Run before static surfaces are combined; saved-scene proof tests colliders and visible triangles in grouped meshes." };
            var baseline = new Baseline { architecturePath = report.architecturePath, interactionDigest = interactions,
                original = original, normalized = normalized, trims = trims, build = report };
            Directory.CreateDirectory(SchoolMapBuilder.Output);
            File.WriteAllText(report.baselineFile, JsonUtility.ToJson(baseline, true));
            File.WriteAllText(SchoolMapBuilder.Output + "/" + ReportFile, JsonUtility.ToJson(report, true));
            if (!report.exactPhysicalUnionPreserved || !report.interactionIdsPreserved || !report.trimColliderOmissionCovered)
                throw new InvalidOperationException("Native masonry union changed occupied geometry, interactions, or uncovered a renderer-only trim. See the native optimization report.");
            return report;
        }

        static Part Eligible(MeshRenderer renderer)
        {
            if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy || !renderer.gameObject.isStatic || !WallName(renderer.name)) return null;
            var go = renderer.gameObject; var t = go.transform;
            if (t.childCount != 0 || !t.parent || Quaternion.Angle(t.rotation, Quaternion.identity) > .00001f
                || Quaternion.Angle(t.parent.rotation, Quaternion.identity) > .00001f) return null;
            if (t.lossyScale.x <= 0 || t.lossyScale.y <= 0 || t.lossyScale.z <= 0
                || t.parent.lossyScale.x <= 0 || t.parent.lossyScale.y <= 0 || t.parent.lossyScale.z <= 0
                || renderer.HasPropertyBlock() || (renderer.lightmapIndex >= 0 && renderer.lightmapIndex != 65535)
                || (renderer.realtimeLightmapIndex >= 0 && renderer.realtimeLightmapIndex != 65535)) return null;
            if (go.GetComponents<Component>().Any(c => !(c is Transform) && !(c is MeshRenderer) && !(c is MeshFilter) && !(c is BoxCollider))) return null;
            var box = go.GetComponent<BoxCollider>(); var filter = go.GetComponent<MeshFilter>();
            if (!box || !box.enabled || box.isTrigger || box.attachedRigidbody || !filter || !Cube(filter.sharedMesh)) return null;
            if (renderer.sharedMaterials.Length != 1 || !renderer.sharedMaterial
                || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(renderer.sharedMaterial))) return null;
            var bounds = box.bounds;
            // Both the renderer and physical box must represent the same cuboid.
            if (!Equal(bounds.min, renderer.bounds.min) || !Equal(bounds.max, renderer.bounds.max)
                || !Equal(box.center, Vector3.zero) || !Equal(box.size, Vector3.one)) return null;
            var m = renderer.sharedMaterial;
            foreach (var property in m.GetTexturePropertyNames())
            {
                var texture = m.GetTexture(property);
                if (texture && texture.wrapMode != TextureWrapMode.Repeat && texture.wrapMode != TextureWrapMode.Mirror) return null;
            }
            return new Part { go = go, parent = t.parent, renderer = renderer, filter = filter, collider = box, bounds = bounds,
                key = Path(t.parent) + "|" + AssetDatabase.GetAssetPath(m) + "|" + go.name + "|" + go.layer + "|" + go.tag
                    + "|" + (int)GameObjectUtility.GetStaticEditorFlags(go) + "|" + renderer.shadowCastingMode + "|" + renderer.receiveShadows
                    + "|" + renderer.lightProbeUsage + "|" + renderer.reflectionProbeUsage + "|" + renderer.renderingLayerMask
                    + "|" + (box.sharedMaterial ? box.sharedMaterial.GetEntityId().ToString() : "none") + "|" + N(box.contactOffset)
                    + "|" + N(bounds.min.y) + "|" + N(bounds.max.y) };
        }
        static bool Cube(Mesh mesh)
        {
            if (!mesh || !mesh.isReadable || mesh.subMeshCount != 1 || mesh.vertexCount != 24 || mesh.triangles.Length != 36
                || !Equal(mesh.bounds.min, Vector3.one * -.5f) || !Equal(mesh.bounds.max, Vector3.one * .5f)) return false;
            var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv; var triangles = mesh.triangles;
            if (normals.Length != vertices.Length || uv.Length != vertices.Length
                || !vertices.All(v => Mathf.Abs(v.x) == .5f && Mathf.Abs(v.y) == .5f && Mathf.Abs(v.z) == .5f)) return false;
            var faceAreas = new float[6]; var faceTriangles = new int[6]; var firstTriangle = new Vector3[6][];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                var a = vertices[ia]; var b = vertices[ib]; var c = vertices[ic];
                var cross = Vector3.Cross(b - a, c - a); int face = -1;
                for (int axis = 0; axis < 3; axis++)
                    if (a[axis] == b[axis] && a[axis] == c[axis] && Mathf.Abs(a[axis]) == .5f)
                    {
                        var expected = Vector3.zero; expected[axis] = Mathf.Sign(a[axis]);
                        if (Vector3.Dot(cross, expected) <= 0 || !Equal(normals[ia], expected)
                            || !Equal(normals[ib], expected) || !Equal(normals[ic], expected)) return false;
                        face = axis * 2 + (a[axis] > 0 ? 1 : 0); break;
                    }
                if (face < 0 || cross.sqrMagnitude != 1) return false;
                var corners = new[] { a, b, c };
                if (firstTriangle[face] == null) firstTriangle[face] = corners;
                else
                {
                    var diagonal = firstTriangle[face].Intersect(corners).ToArray();
                    if (diagonal.Length != 2 || (diagonal[1] - diagonal[0]).sqrMagnitude != 2) return false;
                }
                faceAreas[face] += cross.magnitude * .5f; faceTriangles[face]++;
            }
            return faceAreas.All(a => a == 1) && faceTriangles.All(n => n == 2);
        }
        static bool WallName(string name) => name.StartsWith("Parede") || name == "Peitoril" || name.StartsWith("Verga") || name == "Rodapé";
        static bool Equal(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
        static bool Contains(Bounds a, Bounds b) => a.min.x <= b.min.x && a.min.y <= b.min.y && a.min.z <= b.min.z
            && a.max.x >= b.max.x && a.max.y >= b.max.y && a.max.z >= b.max.z;
        static double TrimBoundsExcess(Bounds masonry, Bounds trim)
        {
            double excess = 0;
            for (int axis = 0; axis < 3; axis++)
                excess = Math.Max(excess, Math.Max((double)masonry.min[axis] - trim.min[axis], (double)trim.max[axis] - masonry.max[axis]));
            return excess;
        }
        static double TrimBoundsExcess(Bounds[] masonry, Bounds trim) => masonry.Length == 0
            ? double.MaxValue : masonry.Min(m => TrimBoundsExcess(m, trim));
        static string N(float f) => f.ToString("R", CultureInfo.InvariantCulture);
        static string SweepKey(Part p, bool alongX) => p.key + (alongX ? "|z|" + N(p.bounds.min.z) + "|" + N(p.bounds.max.z) :
            "|x|" + N(p.bounds.min.x) + "|" + N(p.bounds.max.x));
        static Rectangle Record(Part p) => new Rectangle { parentPath = Path(p.parent), name = p.go.name, siblingIndex = p.go.transform.GetSiblingIndex(),
            materialPath = AssetDatabase.GetAssetPath(p.renderer.sharedMaterial), min = p.bounds.min, max = p.bounds.max, optimized = p.optimized };

        static bool MergeRun(List<Part> run, BuildReport report)
        {
            if (run.Count < 2) return false;
            var keep = run.OrderBy(p => p.go.transform.GetSiblingIndex()).First();
            var min = run.Select(p => p.bounds.min).Aggregate(Vector3.Min);
            var max = run.Select(p => p.bounds.max).Aggregate(Vector3.Max);
            var beforePosition = keep.go.transform.position; var beforeScale = keep.go.transform.localScale;
            var sourceMatrix = keep.go.transform.localToWorldMatrix;
            var sourceMesh = keep.filter.sharedMesh;
            var parentScale = keep.parent.lossyScale;
            keep.go.transform.position = (min + max) * .5f;
            keep.go.transform.localScale = new Vector3((max.x - min.x) / parentScale.x, (max.y - min.y) / parentScale.y, (max.z - min.z) / parentScale.z);
            Physics.SyncTransforms();
            var actual = keep.collider.bounds;
            if (!Equal(actual.min, min) || !Equal(actual.max, max) || !Equal(keep.renderer.bounds.min, min) || !Equal(keep.renderer.bounds.max, max))
            {
                keep.go.transform.position = beforePosition; keep.go.transform.localScale = beforeScale;
                Physics.SyncTransforms(); report.rejectedFloatRepresentations++; return false;
            }
            keep.filter.sharedMesh = PersistMesh(WorldUvCube(sourceMesh, sourceMatrix, keep.go.transform.localToWorldMatrix));
            keep.bounds = actual; keep.optimized = true;
            foreach (var p in run) if (p != keep) UnityEngine.Object.DestroyImmediate(p.go);
            report.mergedRuns++; return true;
        }
        static Mesh WorldUvCube(Mesh source, Matrix4x4 oldMatrix, Matrix4x4 newMatrix)
        {
            var mesh = UnityEngine.Object.Instantiate(source); mesh.name = "Native masonry exact rectangular union";
            var v = source.vertices; var normals = source.normals; var oldUv = source.uv;
            if (normals.Length != v.Length || oldUv.Length != v.Length) throw new InvalidDataException("Eligible cube has no per-face UV/normal data.");
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var face = Enumerable.Range(0, v.Length).Where(j => Vector3.Dot(normals[i], normals[j]) > .999f).ToArray();
                int a = face[0], b = -1, c = -1;
                for (int j = 1; j < face.Length && c < 0; j++) for (int k = j + 1; k < face.Length && c < 0; k++)
                    if (Vector3.Cross(v[face[j]] - v[a], v[face[k]] - v[a]).sqrMagnitude > .1f) { b = face[j]; c = face[k]; }
                if (c < 0) throw new InvalidDataException("Cube UV face is degenerate.");
                var p = newMatrix.MultiplyPoint3x4(v[i]); var p0 = oldMatrix.MultiplyPoint3x4(v[a]);
                var u = oldMatrix.MultiplyPoint3x4(v[b]) - p0; var w = oldMatrix.MultiplyPoint3x4(v[c]) - p0; var d = p - p0;
                double uu = Vector3.Dot(u, u), uw = Vector3.Dot(u, w), ww = Vector3.Dot(w, w), du = Vector3.Dot(d, u), dw = Vector3.Dot(d, w);
                double denominator = uu * ww - uw * uw;
                float x = (float)((du * ww - dw * uw) / denominator), y = (float)((dw * uu - du * uw) / denominator);
                uv[i] = oldUv[a] + x * (oldUv[b] - oldUv[a]) + y * (oldUv[c] - oldUv[a]);
            }
            mesh.uv = uv; mesh.RecalculateTangents(); return mesh;
        }
        static Mesh PersistMesh(Mesh mesh)
        {
            if (meshFolder == null)
            {
                string baseFolder = SchoolMapBuilder.Folder + "/Meshes/MasonryOptimization";
                if (!AssetDatabase.IsValidFolder(baseFolder)) AssetDatabase.CreateFolder(SchoolMapBuilder.Folder + "/Meshes", "MasonryOptimization");
                string name = "Build-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                AssetDatabase.CreateFolder(baseFolder, name); meshFolder = baseFolder + "/" + name;
            }
            string path = meshFolder + "/Wall-" + meshIndex++ + ".asset";
            AssetDatabase.CreateAsset(mesh, path); return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        static string Path(Transform t)
        {
            var names = new List<string>(); while (t) { names.Add(t.name); t = t.parent; }
            names.Reverse(); return string.Join("/", names);
        }
        static Transform Find(Scene scene, string path)
        {
            var names = path.Split('/'); var root = scene.GetRootGameObjects().SingleOrDefault(g => g.name == names[0]);
            if (!root) return null; var found = root.transform;
            for (int i = 1; i < names.Length && found; i++) found = found.Cast<Transform>().SingleOrDefault(t => t.name == names[i]);
            return found;
        }
        static string InteractionDigest(Transform architecture)
        {
            var ids = architecture.GetComponentsInChildren<HorrorGame.InteractiveDoor>(true).Select(d => "door|" + d.persistentId)
                .Concat(architecture.GetComponentsInChildren<HorrorGame.InteractiveDrawer>(true).Select(d => "drawer|" + d.persistentId)).OrderBy(s => s);
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", ids)))).Replace("-", "");
        }
        static Trim[] InspectTrims(Transform architecture)
        {
            var masonry = architecture.GetComponentsInChildren<BoxCollider>(true).Where(c => c.enabled && !c.isTrigger && WallName(c.name)).Select(c => c.bounds).ToArray();
            return architecture.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.name == "Rodapé" && !r.GetComponent<Collider>()).Select(r => {
                var b = r.bounds;
                double excess = TrimBoundsExcess(masonry, b);
                return new Trim { parentPath = Path(r.transform.parent), name = r.name, siblingIndex = r.transform.GetSiblingIndex(), min = b.min, max = b.max,
                    containedExactly = excess == 0, containedByMasonry = excess <= FaceTolerance, rendererBoundsExcessMeters = excess };
            }).ToArray();
        }

        // Independent endpoint-cell integration, including unions of overlapping 3D boxes.
        public static double VolumeDifference(Rectangle[] before, Rectangle[] after)
        {
            double result = 0;
            foreach (string group in before.Concat(after).Select(r => r.parentPath + "|" + r.materialPath).Distinct())
            {
                var a = before.Where(r => r.parentPath + "|" + r.materialPath == group).ToArray();
                var b = after.Where(r => r.parentPath + "|" + r.materialPath == group).ToArray(); var all = a.Concat(b).ToArray();
                var xs = all.SelectMany(r => new[] { r.min.x, r.max.x }).Distinct().OrderBy(x => x).ToArray();
                var ys = all.SelectMany(r => new[] { r.min.y, r.max.y }).Distinct().OrderBy(x => x).ToArray();
                var zs = all.SelectMany(r => new[] { r.min.z, r.max.z }).Distinct().OrderBy(x => x).ToArray();
                for (int iy = 1; iy < ys.Length; iy++)
                {
                    double y = ((double)ys[iy - 1] + ys[iy]) * .5;
                    var left = Coverage(a, xs, zs, y); var right = Coverage(b, xs, zs, y);
                    for (int ix = 1; ix < xs.Length; ix++) for (int iz = 1; iz < zs.Length; iz++)
                        if (left[ix - 1, iz - 1] != right[ix - 1, iz - 1])
                            result += ((double)xs[ix] - xs[ix - 1]) * ((double)zs[iz] - zs[iz - 1]) * ((double)ys[iy] - ys[iy - 1]);
                }
            }
            return result;
        }
        static bool[,] Coverage(Rectangle[] boxes, float[] xs, float[] zs, double y)
        {
            var cells = new bool[xs.Length - 1, zs.Length - 1];
            foreach (var r in boxes.Where(r => y > r.min.y && y < r.max.y))
            {
                int x0 = Array.BinarySearch(xs, r.min.x), x1 = Array.BinarySearch(xs, r.max.x);
                int z0 = Array.BinarySearch(zs, r.min.z), z1 = Array.BinarySearch(zs, r.max.z);
                for (int x = x0; x < x1; x++) for (int z = z0; z < z1; z++) cells[x, z] = true;
            }
            return cells;
        }

        public static ProofReport Inspect(Scene scene)
        {
            if (scene.path != SchoolIntegrationPass.ScenePath) throw new InvalidOperationException("Inspect the saved school scene.");
            string file = SchoolMapBuilder.Output + "/" + BaselineFile;
            var baseline = JsonUtility.FromJson<Baseline>(File.ReadAllText(file));
            var architecture = Find(scene, baseline.architecturePath);
            if (!architecture) throw new InvalidOperationException("Missing architecture root in native scene.");
            Physics.SyncTransforms(); var failures = new List<Failure>();
            var meshes = RenderMeshes(scene);
            var actual = new List<Rectangle>(); int faceSamples = 0, faceFailures = 0, colliderFailures = 0;
            foreach (var expected in baseline.normalized)
            {
                var parent = Find(scene, expected.parentPath);
                var go = parent && expected.siblingIndex < parent.childCount ? parent.GetChild(expected.siblingIndex).gameObject : null;
                var box = go ? go.GetComponent<BoxCollider>() : null;
                if (!box || !box.enabled || box.isTrigger || go.name != expected.name || !Equal(box.bounds.min, expected.min) || !Equal(box.bounds.max, expected.max))
                {
                    colliderFailures++; failures.Add(new Failure { parentPath = expected.parentPath, name = expected.name,
                        point = (expected.min + expected.max) * .5f, detail = "Saved BoxCollider is missing or differs from the optimized exact rectangle." }); continue;
                }
                actual.Add(new Rectangle { parentPath = expected.parentPath, name = expected.name, materialPath = expected.materialPath,
                    siblingIndex = expected.siblingIndex, min = box.bounds.min, max = box.bounds.max, optimized = expected.optimized });
                if (!expected.optimized) continue;
                var center = (expected.min + expected.max) * .5f;
                foreach (var direction in new[] { Vector3.left, Vector3.right, Vector3.down, Vector3.up, Vector3.back, Vector3.forward })
                {
                    var p = center + Vector3.Scale(direction, (expected.max - expected.min) * .5f); faceSamples++;
                    if (meshes.Any(m => m.material == expected.materialPath && RenderedAt(m, p))) continue;
                    faceFailures++; failures.Add(new Failure { parentPath = expected.parentPath, name = expected.name, point = p,
                        detail = "No visible triangle with the original material covers this optimized cube face, including grouped static meshes." });
                }
            }
            var masonry = architecture.GetComponentsInChildren<BoxCollider>(true).Where(c => c.enabled && !c.isTrigger && WallName(c.name)).Select(c => c.bounds).ToArray();
            int coveredTrims = 0, exactTrims = 0; double maxTrimExcess = 0;
            foreach (var trim in baseline.trims)
            {
                var b = new Bounds(); b.SetMinMax(trim.min, trim.max);
                double excess = TrimBoundsExcess(masonry, b); maxTrimExcess = Math.Max(maxTrimExcess, excess);
                if (excess == 0) exactTrims++;
                if (excess <= FaceTolerance) coveredTrims++;
                else failures.Add(new Failure { parentPath = trim.parentPath, name = trim.name, point = b.center,
                    detail = "Renderer-only base trim exceeds an existing native masonry BoxCollider by " + excess.ToString("R", CultureInfo.InvariantCulture)
                        + "m, beyond the explicit 0.00002m renderer-bounds precision tolerance. Physical colliders were not expanded." });
            }
            double difference = VolumeDifference(baseline.original, actual.ToArray());
            var report = new ProofReport { scene = scene.path, baselineFile = file,
                renderedWallPiecesBefore = baseline.build.renderedWallPiecesBefore, renderedWallPiecesAfter = baseline.build.renderedWallPiecesAfter,
                collidersRemoved = baseline.build.collidersRemoved, nativeColliderChecks = baseline.normalized.Length, missingOrChangedColliders = colliderFailures,
                renderedFaceSamples = faceSamples, renderedFaceFailures = faceFailures, rendererOnlyTrims = baseline.trims.Length,
                rendererOnlyTrimsContained = coveredTrims, trimFailures = baseline.trims.Length - coveredTrims,
                rendererOnlyTrimsContainedExactly = exactTrims, rendererOnlyTrimsWithinRendererPrecision = coveredTrims - exactTrims,
                maxTrimRendererBoundsExcessMeters = maxTrimExcess,
                independentlyMeasuredDifferenceCubicMeters = difference, exactPhysicalUnionPreserved = difference < 1e-10,
                interactionIdsPreserved = baseline.interactionDigest == InteractionDigest(architecture), failures = failures.ToArray(),
                notes = new[] { "Original and optimized native rectangles are independently reloaded from the pre-batch baseline.",
                    "Every surviving expected BoxCollider is checked after save/reopen. Changed wall faces are checked against actual visible mesh triangles with matching material.",
                    "Grouped static surfaces are inspected; no individual wall renderer is required to survive batching.",
                    "Renderer-only base trim containment reports exact matches separately from at most 0.00002m renderer-bound rounding differences. No collider is expanded and the physical union comparison remains exact. Known openings and multi-material/decorative/interactive components are untouched.",
                    "No Editor geometry mutation, door pose change, Play, gameplay or checkpoint." } };
            report.passed = report.exactPhysicalUnionPreserved && report.interactionIdsPreserved && colliderFailures == 0 && faceFailures == 0 && report.trimFailures == 0;
            File.WriteAllText(SchoolMapBuilder.Output + "/" + ProofFile, JsonUtility.ToJson(report, true)); return report;
        }
        static RenderMesh[] RenderMeshes(Scene scene)
        {
            var list = new List<RenderMesh>();
            foreach (var renderer in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true))
                .Where(r => r.enabled && r.gameObject.activeInHierarchy))
            {
                var filter = renderer.GetComponent<MeshFilter>(); if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable) continue;
                var mesh = filter.sharedMesh; var vertices = mesh.vertices.Select(renderer.transform.TransformPoint).ToArray();
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < Mathf.Min(materials.Length, mesh.subMeshCount); i++) if (materials[i])
                    list.Add(new RenderMesh { material = AssetDatabase.GetAssetPath(materials[i]), bounds = renderer.bounds,
                        vertices = vertices, triangles = mesh.GetTriangles(i) });
            }
            return list.ToArray();
        }
        static bool RenderedAt(RenderMesh mesh, Vector3 point)
        {
            var b = mesh.bounds; b.Expand(FaceTolerance * 2); if (!b.Contains(point)) return false;
            for (int i = 0; i < mesh.triangles.Length; i += 3)
            {
                var a = mesh.vertices[mesh.triangles[i]]; var c = mesh.vertices[mesh.triangles[i + 1]]; var d = mesh.vertices[mesh.triangles[i + 2]];
                var normal = Vector3.Cross(c - a, d - a); float n = normal.magnitude;
                if (n < .000001f || Mathf.Abs(Vector3.Dot(point - a, normal / n)) > FaceTolerance) continue;
                var u = c - a; var v = d - a; var w = point - a;
                double uu = Vector3.Dot(u, u), uv = Vector3.Dot(u, v), vv = Vector3.Dot(v, v), wu = Vector3.Dot(w, u), wv = Vector3.Dot(w, v);
                double den = uu * vv - uv * uv; if (Math.Abs(den) < 1e-12) continue;
                double x = (wu * vv - wv * uv) / den, y = (wv * uu - wu * uv) / den;
                if (x >= -.00001 && y >= -.00001 && x + y <= 1.00001) return true;
            }
            return false;
        }
    }
}
