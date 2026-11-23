// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SchoolCEEP.Editor
{
    /// <summary>Finish existing architectural triangles; retain the original assets and all physical/story state.</summary>
    public static class SchoolSurfaceFinishPass
    {
        public const string Output = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/SurfaceFixturePolish";
        public const string JournalPath = Output + "/surface-journal.json";
        const string ScenePath = "Assets/SchoolCEEP/Scenes/SchoolCEEP.unity";
        const string TextureFolder = "Assets/textures/SurfaceFixturePolish";
        const string MeshFolder = "Assets/SchoolCEEP/Meshes/SurfaceFixturePolish";
        const string MaterialFolder = "Assets/SchoolCEEP/Materials/SurfaceFixturePolish";
        const string NightJournalPath = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/AbandonmentPolish/night-material-journal.json";

        sealed class Finish
        {
            public string id; public float repeat, normal, smoothness;
            public Finish(string id, float repeat, float normal, float smoothness)
            { this.id = id; this.repeat = repeat; this.normal = normal; this.smoothness = smoothness; }
        }
        static readonly Dictionary<string, Finish> Finishes = new Dictionary<string, Finish>(StringComparer.Ordinal) {
            { "painted_plaster_wall", new Finish("painted_plaster_wall", 2f, .26f, .16f) },
            { "blue_plaster_wall", new Finish("blue_plaster_wall", 2f, .24f, .14f) },
            { "red_plaster_weathered", new Finish("red_plaster_weathered", 2f, .29f, .16f) },
            { "long_white_tiles", new Finish("long_white_tiles", 1.27f, .34f, .20f) },
            { "ceiling_interior", new Finish("ceiling_interior", 2f, .27f, .12f) },
            { "wooden_planks", new Finish("wooden_planks", 2f, .28f, .15f) },
            { "corrugated_iron", new Finish("corrugated_iron", 1.12f, .35f, .20f) }
        };
        static readonly HashSet<string> Plaster = Names("Reboco envelhecido", "Pintura verde antiga", "PhotoWestWhiteWall",
            "PhotoPlasterWhite", "PhotoWestCreamWall", "PhotoClassroomWhite", "PhotoClassroomLowerGrey", "PhotoCentralWhite",
            "PhotoWallOchre", "FrontPhotoOffWhite", "PhotoPlasterGrey", "PhotoRampMasonry", "PhotoRampCap");
        static readonly HashSet<string> Blue = Names("PhotoFrameBlue", "PhotoFacadeBlue", "PhotoBlueConcrete", "FrontPhotoConcreteBlue");
        static readonly HashSet<string> Red = Names("PhotoWallTerracotta", "PhotoTerracotta");
        static readonly HashSet<string> Tiles = Names("PhotoWestYellowTiles", "PhotoCentralWallTiles", "PhotoRefectoryBlueTiles", "FrontPhotoCeramicBlue");
        static readonly HashSet<string> Columns = Names("PhotoColumnGrey", "PhotoWestBrownColumn");
        static readonly HashSet<string> Roofs = Names("Cobertura", "PhotoRoofGrey", "CampusCourtCorrugatedGrey", "WestBlockAccessRoofGrey");

        [Serializable] public sealed class MaterialRecord
        { public string source, sourceId, replacement, replacementId, family; }
        [Serializable] public sealed class FaceRecord
        {
            public int sourceSlot, sourceTriangle, replacementSlot, firstVertex;
            public int a, b, c;
            public string family;
            public Vector3 uAxis, vAxis;
        }
        [Serializable] public sealed class RendererRecord
        {
            public string path, rendererId, beforeMesh, afterMesh, beforeMeshId, afterMeshId, beforeMeshName;
            public int[] hierarchy;
            public string[] beforeMaterials, afterMaterials;
            public FaceRecord[] faces;
        }
        [Serializable] public sealed class FamilyCount { public string family; public int renderers, triangles; }
        [Serializable] public sealed class Coverage
        { public string material, path, reason; public int consideredTriangles, selectedTriangles, retainedTriangles; public string[] examples; }
        [Serializable] public sealed class Report
        {
            public int revision = 1, changedRenderers, selectedTriangles, retainedTriangles, originalRenderers, resultingRenderers;
            public string scene, root, rootId, state, protectedBefore, protectedAfter;
            public bool protectedStatePreserved, geometryPreserved, metricUVsVerified, noAddedRenderers;
            public FamilyCount[] families;
            public Coverage[] coverage;
            public string[] missingTextures, notes;
            public MaterialRecord[] materials;
            public RendererRecord[] renderers;
        }
        sealed class PlannedFace { public FaceRecord record; }
        sealed class PlannedRenderer
        { public MeshRenderer renderer; public Mesh mesh; public Material[] materials; public readonly List<PlannedFace> faces = new List<PlannedFace>(); }
        sealed class Plan
        { public Report report; public readonly List<PlannedRenderer> renderers = new List<PlannedRenderer>(); }
        sealed class CoverageBuilder
        { public Coverage record; public readonly HashSet<string> examples = new HashSet<string>(); }
        [Serializable] sealed class NightJournal
        { public string scene, root, state; public NightMaterial[] materials; public NightRenderer[] renderers; }
        [Serializable] sealed class NightMaterial { public string source, replacement; }
        [Serializable] sealed class NightRenderer { public string path; public int[] hierarchy; public string[] beforeMaterials, afterMaterials; }

        public static Report Preview(Transform root)
        {
            RequireScene(root);
            if (File.Exists(JournalPath) && Read(root).state == "applied") return Audit(root);
            return BuildPlan(root).report;
        }

        public static Report Apply(Transform root)
        {
            RequireScene(root);
            if (File.Exists(JournalPath))
            {
                var journal = Read(root);
                return journal.state == "applied" ? Audit(root) : Assign(root, journal, true);
            }
            RequireNightRestored(root);
            var plan = BuildPlan(root); var report = plan.report;
            if (report.missingTextures.Length != 0) throw new InvalidDataException("Surface texture maps missing: " + string.Join(", ", report.missingTextures));
            if (plan.renderers.Count == 0) throw new InvalidOperationException("No reviewed architectural triangles qualify.");
            EnsureFolder(MeshFolder); EnsureFolder(MaterialFolder);
            PersistOriginalMeshes(root, plan);
            ConfigureTextures(report.families.Select(v => v.family));
            var variants = new Dictionary<string, Material>(); var materialRows = new List<MaterialRecord>(); var rows = new List<RendererRecord>();
            foreach (var row in plan.renderers)
                rows.Add(Prepare(root, row, variants, materialRows));
            report.materials = materialRows.ToArray(); report.renderers = rows.ToArray(); report.state = "pending";
            AssetDatabase.SaveAssets();
            VerifyGeometry(root, report); VerifyMaterials(report);
            Write(report); // Durable restoration references precede the first assignment.
            return Assign(root, report, true);
        }

        static void PersistOriginalMeshes(Transform root, Plan plan)
        {
            var originals = plan.renderers.Select(v => v.mesh).Distinct().Where(v => string.IsNullOrEmpty(AssetDatabase.GetAssetPath(v))).ToArray();
            if (originals.Length == 0) return;
            string before = SchoolFixtureReplacementPass.ProtectedSignature(root);
            if (before != plan.report.protectedBefore) throw new InvalidOperationException("Protected state changed before persisting architectural originals.");
            var filters = root.gameObject.scene.GetRootGameObjects().SelectMany(v => v.GetComponentsInChildren<MeshFilter>(true))
                .Select(v => new { filter = v, mesh = v.sharedMesh }).ToArray();
            var digests = originals.ToDictionary(v => v, OriginalMeshDigest);
            EnsureFolder(MeshFolder + "/Originals");
            foreach (var mesh in originals)
            {
                string name = new string(mesh.name.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
                if (name.Length > 80) name = name.Substring(0, 80);
                // Assetize the same object; do not clone or assign a MeshFilter.
                AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(MeshFolder + "/Originals/Original-" + name + ".asset"));
            }
            AssetDatabase.SaveAssets(); // IDs must be durable before Prepare captures restoration references.
            foreach (var mesh in originals)
            {
                string path = AssetDatabase.GetAssetPath(mesh);
                if (string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<Mesh>(path) != mesh
                    || !GlobalObjectId.TryParse(Id(mesh), out var global) || GlobalObjectId.GlobalObjectIdentifierToObjectSlow(global) != mesh)
                    throw new InvalidDataException("Original mesh did not acquire a durable native identity: " + mesh.name);
                if (OriginalMeshDigest(mesh) != digests[mesh]) throw new InvalidDataException("Persisting an original mesh changed its vertex/index buffers or bounds: " + mesh.name);
            }
            if (filters.Any(v => v.filter.sharedMesh != v.mesh) || SchoolFixtureReplacementPass.ProtectedSignature(root) != before)
                throw new InvalidOperationException("Persisting architectural originals changed a MeshFilter, Collider, Light or story reference.");
            plan.report.notes = plan.report.notes.Concat(new[] { "Persisted " + originals.Length + " original transient meshes as the same objects; complete buffers/bounds, MeshFilter references and protected state verified unchanged before journal creation." }).ToArray();
        }

        static string OriginalMeshDigest(Mesh mesh)
        {
            using (var bytes = new MemoryStream())
            using (var writer = new BinaryWriter(bytes))
            using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                // CreateAsset can rename an object to match its file. Only mesh data belongs in this geometry digest.
                writer.Write(mesh.vertexCount); writer.Write((int)mesh.indexFormat); writer.Write(mesh.subMeshCount);
                var bounds = mesh.bounds;
                foreach (float value in new[] { bounds.center.x, bounds.center.y, bounds.center.z, bounds.extents.x, bounds.extents.y, bounds.extents.z }) writer.Write(value);
                var layout = mesh.GetVertexAttributes(); writer.Write(layout.Length);
                foreach (var attribute in layout) { writer.Write((int)attribute.attribute); writer.Write((int)attribute.format); writer.Write(attribute.dimension); writer.Write(attribute.stream); }
                var source = data[0]; writer.Write(source.vertexBufferCount);
                for (int stream = 0; stream < source.vertexBufferCount; stream++) { var buffer = source.GetVertexData<byte>(stream).ToArray(); writer.Write(buffer.Length); writer.Write(buffer); }
                var indices = source.GetIndexData<byte>().ToArray(); writer.Write(indices.Length); writer.Write(indices);
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    var descriptor = source.GetSubMesh(sub);
                    writer.Write((int)descriptor.topology); writer.Write(descriptor.indexStart); writer.Write(descriptor.indexCount); writer.Write(descriptor.baseVertex);
                    writer.Write(descriptor.firstVertex); writer.Write(descriptor.vertexCount); bounds = descriptor.bounds;
                    foreach (float value in new[] { bounds.center.x, bounds.center.y, bounds.center.z, bounds.extents.x, bounds.extents.y, bounds.extents.z }) writer.Write(value);
                }
                writer.Flush(); using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(bytes.ToArray()));
            }
        }

        public static Report Restore(Transform root)
        {
            RequireNightRestored(root);
            return Assign(root, Read(root), false);
        }

        public static Report Audit(Transform root)
        {
            var report = Read(root);
            if (report.state != "applied") throw new InvalidOperationException("The architectural finish journal is not applied.");
            ValidateReferences(root, report, false, true); VerifyGeometry(root, report); VerifyMaterials(report);
            report.protectedAfter = SchoolFixtureReplacementPass.ProtectedSignature(root);
            if (report.protectedAfter != report.protectedBefore) throw new InvalidOperationException("Physical, light or story state differs from the finish baseline.");
            report.resultingRenderers = root.GetComponentsInChildren<MeshRenderer>(true).Length;
            if (report.resultingRenderers != report.originalRenderers) throw new InvalidOperationException("Renderer inventory differs from the finish baseline.");
            report.protectedStatePreserved = report.geometryPreserved = report.metricUVsVerified = report.noAddedRenderers = true;
            return report;
        }

        static Plan BuildPlan(Transform root)
        {
            var plan = new Plan { report = new Report { scene = root.gameObject.scene.path, root = root.name,
                rootId = Id(root), state = "preview", protectedBefore = SchoolFixtureReplacementPass.ProtectedSignature(root),
                originalRenderers = root.GetComponentsInChildren<MeshRenderer>(true).Length } };
            var walls = root.GetComponentsInChildren<BoxCollider>(true).Where(v => v.enabled && !v.isTrigger && v.gameObject.activeInHierarchy
                && MasonryName(v.name) && v.bounds.size.y > .15f && Mathf.Min(v.bounds.size.x, v.bounds.size.z) < .75f).ToArray();
            var roofs = root.GetComponentsInChildren<BoxCollider>(true).Where(v => v.enabled && !v.isTrigger && v.gameObject.activeInHierarchy
                && (v.name.StartsWith("Cobertura", StringComparison.Ordinal) || v.name == "Forro")).ToArray();
            var excluded = NarrativeVisuals(root); var coverage = new Dictionary<string, CoverageBuilder>();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || Excluded(renderer, root, excluded)) continue;
                var filter = renderer.GetComponent<MeshFilter>(); var source = filter ? filter.sharedMesh : null;
                var materials = renderer.sharedMaterials;
                if (!source || !materials.Any(v => SafeMaterial(v) && (Known(BaseName(v)) || ArchitecturalLabel(renderer.name)))) continue;
                if (!source.isReadable || materials.Length != source.subMeshCount)
                    throw new InvalidDataException("Ambiguous/unreadable architectural mesh: " + Path(root, renderer.transform));
                var row = new PlannedRenderer { renderer = renderer, mesh = source, materials = materials };
                var vertices = source.vertices;
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    if (source.GetTopology(sub) != MeshTopology.Triangles) throw new InvalidDataException("Architectural mesh is not triangle topology.");
                    var material = materials[sub]; if (!SafeMaterial(material)) continue;
                    string name = BaseName(material); bool known = Known(name);
                    if (!known && !ArchitecturalLabel(renderer.name)) continue;
                    string key = AssetDatabase.GetAssetPath(material);
                    if (!coverage.TryGetValue(key, out var cov))
                    {
                        cov = new CoverageBuilder { record = new Coverage { material = name, path = key,
                            reason = known ? "Select reviewed material family only where geometry proves architectural use; retained faces include door/furniture/beam uses." : "Material outside the reviewed architectural family allowlist." } };
                        coverage.Add(key, cov);
                    }
                    cov.examples.Add(Path(root, renderer.transform));
                    var triangles = source.GetTriangles(sub);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                        var a = renderer.transform.TransformPoint(vertices[ia]); var b = renderer.transform.TransformPoint(vertices[ib]); var c = renderer.transform.TransformPoint(vertices[ic]);
                        var cross = Vector3.Cross(b - a, c - a); if (cross.sqrMagnitude < 1e-12f) { cov.record.retainedTriangles++; continue; }
                        var normal = cross.normalized;
                        string family = SelectFamily(root, renderer, name, a, b, c, normal, walls, roofs);
                        cov.record.consideredTriangles++;
                        if (family == null) { cov.record.retainedTriangles++; continue; }
                        ProjectionAxes(normal, out var u, out var v);
                        row.faces.Add(new PlannedFace { record = new FaceRecord { sourceSlot = sub, sourceTriangle = i / 3,
                            a = ia, b = ib, c = ic, family = family, uAxis = u, vAxis = v } });
                        cov.record.selectedTriangles++;
                    }
                }
                if (row.faces.Count != 0) plan.renderers.Add(row);
            }
            plan.report.changedRenderers = plan.renderers.Count;
            plan.report.selectedTriangles = plan.renderers.Sum(v => v.faces.Count);
            plan.report.retainedTriangles = plan.renderers.Sum(v => v.mesh.triangles.Length / 3 - v.faces.Count);
            plan.report.families = Finishes.Keys.Select(f => new FamilyCount { family = f,
                triangles = plan.renderers.Sum(r => r.faces.Count(t => t.record.family == f)),
                renderers = plan.renderers.Count(r => r.faces.Any(t => t.record.family == f)) }).Where(v => v.triangles > 0).ToArray();
            plan.report.coverage = coverage.Values.Select(v => { v.record.examples = v.examples.Take(5).ToArray(); return v.record; }).ToArray();
            plan.report.missingTextures = plan.report.families.SelectMany(f => MapPaths(f.family)).Where(p => !AssetDatabase.LoadAssetAtPath<Texture2D>(p)).ToArray();
            plan.report.notes = new[] {
                "Existing Abandoned-* mossy plaster assignments, glass, boards, papers, fixtures, imported props and narrative targets are excluded.",
                "Blue paint uses reviewed wall backing and the exact authored entrance panel. The exact authored refectory ceramic panels also qualify; metal window frames retain their material.",
                "Ceilings and roofs use explicit material/renderer evidence and face orientation. Matte material variants preserve the existing base color.",
                "Only selected triangle vertices receive metric UV0 and new tangents. All source vertices/attributes and unselected triangles remain; no coplanar overlay or new renderer is created.",
                "No Collider, Light, MonoBehaviour, transform or gameplay assignment is edited. Native source mesh/material references are kept in the journal." };
            return plan;
        }

        static string SelectFamily(Transform root, MeshRenderer renderer, string name, Vector3 a, Vector3 b, Vector3 c, Vector3 n, BoxCollider[] walls, BoxCollider[] roofs)
        {
            float low = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
            float xSpan = Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - Mathf.Min(a.x, Mathf.Min(b.x, c.x));
            float zSpan = Mathf.Max(a.z, Mathf.Max(b.z, c.z)) - Mathf.Min(a.z, Mathf.Min(b.z, c.z));
            float hSpan = Mathf.Max(xSpan, zSpan);
            if (name == "PhotoWestWoodCeiling" && low > 1.6f) return "wooden_planks";
            if (Roofs.Contains(name) && low > 1.6f)
            {
                if (name == "Cobertura" && n.y < -.85f) return "ceiling_interior";
                if (Mathf.Abs(n.y) > .12f || ArchitecturalLabel(renderer.name)) return name == "Cobertura" && Mathf.Abs(n.y) <= .12f ? "painted_plaster_wall" : "corrugated_iron";
            }
            if (Columns.Contains(name) && Mathf.Max(a.y, Mathf.Max(b.y, c.y)) - low > .16f) return "painted_plaster_wall";
            if (Plaster.Contains(name) && low > 1.6f && n.y < -.95f && xSpan > .25f && zSpan > .25f
                && (renderer.name == "Forro" || renderer.name.Contains("Superfícies agrupadas") || BackedHorizontal(a, b, c, roofs))) return "ceiling_interior";
            // These two authored architectural panels intentionally have no
            // colliders. Their exact material and batch paths prove their role.
            if (Mathf.Abs(n.y) < .035f && Mathf.Max(Mathf.Abs(n.x), Mathf.Abs(n.z)) > .98f && hSpan > .25f
                && Mathf.Max(a.y, Mathf.Max(b.y, c.y)) - low > .16f)
            {
                string panelPath = Path(root, renderer.transform);
                if (name == "PhotoFacadeBlue" && panelPath == "Arquitetura das fotos/Superfícies agrupadas — PhotoFacadeBlue") return "blue_plaster_wall";
                if (name == "FrontPhotoCeramicBlue" && panelPath == "Detalhes das novas fotos/Superfícies agrupadas — FrontPhotoCeramicBlue") return "long_white_tiles";
            }
            if (hSpan < .25f || !BackedWall(a, b, c, n, walls)) return null;
            if (Blue.Contains(name)) return "blue_plaster_wall";
            if (Red.Contains(name)) return "red_plaster_weathered";
            if (Tiles.Contains(name)) return "long_white_tiles";
            return Plaster.Contains(name) ? "painted_plaster_wall" : null;
        }

        static bool BackedWall(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, BoxCollider[] walls)
        {
            if (Mathf.Abs(normal.y) > .035f || Mathf.Max(Mathf.Abs(normal.x), Mathf.Abs(normal.z)) < .98f
                || Mathf.Max(a.y, Mathf.Max(b.y, c.y)) - Mathf.Min(a.y, Mathf.Min(b.y, c.y)) < .16f) return false;
            var center = (a + b + c) / 3;
            foreach (var wall in walls)
            {
                var bounds = wall.bounds; bool thinX = bounds.size.x < bounds.size.z;
                if ((thinX ? Mathf.Abs(normal.x) : Mathf.Abs(normal.z)) < .98f) continue;
                float plane = thinX ? center.x : center.z;
                float lo = thinX ? bounds.min.x : bounds.min.z, hi = thinX ? bounds.max.x : bounds.max.z;
                if (Mathf.Min(Mathf.Abs(plane - lo), Mathf.Abs(plane - hi)) > .09f) continue;
                bounds.Expand(new Vector3(.18f, .025f, .18f));
                if (bounds.Contains(a) && bounds.Contains(b) && bounds.Contains(c)) return true;
            }
            return false;
        }
        static bool BackedHorizontal(Vector3 a, Vector3 b, Vector3 c, BoxCollider[] roofs)
        {
            foreach (var roof in roofs)
            {
                var bounds = roof.bounds; bounds.Expand(.12f);
                if (bounds.Contains(a) && bounds.Contains(b) && bounds.Contains(c)) return true;
            }
            return false;
        }
        static void ProjectionAxes(Vector3 normal, out Vector3 u, out Vector3 v)
        {
            if (Mathf.Abs(normal.y) < .1f)
            { u = Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? Vector3.forward : Vector3.right; v = Vector3.up; return; }
            if (Mathf.Abs(normal.y) > .999f) { u = Vector3.right; v = Vector3.forward; return; }
            if (normal.y < 0) normal = -normal;
            // Roof facets get a true unit tangent basis, rather than stretching cube UVs.
            var axis = Mathf.Abs(normal.z) < .8f ? Vector3.forward : Vector3.right;
            u = Vector3.ProjectOnPlane(axis, normal).normalized; v = Vector3.Cross(normal, u).normalized;
        }
        static Vector2 MetricUV(Vector3 world, FaceRecord face) => new Vector2(Vector3.Dot(world, face.uAxis), Vector3.Dot(world, face.vAxis)) / Finishes[face.family].repeat;

        static RendererRecord Prepare(Transform root, PlannedRenderer planned, Dictionary<string, Material> variants, List<MaterialRecord> materialRows)
        {
            var source = planned.mesh; var originalVertices = source.vertices; var originalNormals = source.normals;
            if (originalNormals.Length != originalVertices.Length || source.blendShapeCount != 0 || source.bindposes.Length != 0)
                throw new InvalidDataException("Only static architectural meshes with authored normals qualify.");
            var vertices = originalVertices.ToList(); var normals = originalNormals.ToList(); var colors = source.colors.ToList();
            var originalTangents = source.tangents;
            var tangents = originalTangents.Length == originalVertices.Length ? originalTangents.ToList() : originalNormals.Select(n => DefaultTangent(n)).ToList();
            var uv = Enumerable.Range(0, 8).Select(_ => new List<Vector4>()).ToArray();
            var uvDimensions = new int[8];
            for (int channel = 0; channel < 8; channel++)
            {
                source.GetUVs(channel, uv[channel]);
                if (uv[channel].Count != 0 && uv[channel].Count != originalVertices.Length) throw new InvalidDataException("Partial architectural UV channel.");
                if (uv[channel].Count != 0) uvDimensions[channel] = source.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0 + channel));
            }
            if (uv[0].Count == 0) { uv[0].AddRange(Enumerable.Repeat(Vector4.zero, originalVertices.Length)); uvDimensions[0] = 2; }
            if (colors.Count != 0 && colors.Count != originalVertices.Length) throw new InvalidDataException("Partial architectural vertex colors.");
            var slots = planned.materials.ToList(); var indices = Enumerable.Range(0, source.subMeshCount).Select(_ => new List<int>()).ToList();
            var selected = planned.faces.ToDictionary(v => v.record.sourceSlot + ":" + v.record.sourceTriangle, v => v.record);
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                var sourceIndices = source.GetTriangles(sub);
                for (int offset = 0; offset < sourceIndices.Length; offset += 3)
                {
                    if (!selected.TryGetValue(sub + ":" + offset / 3, out var face)) { indices[sub].AddRange(new[] { sourceIndices[offset], sourceIndices[offset + 1], sourceIndices[offset + 2] }); continue; }
                    var material = Variant(planned.materials[sub], face.family, variants, materialRows);
                    int slot = slots.IndexOf(material); if (slot < 0) { slot = slots.Count; slots.Add(material); indices.Add(new List<int>()); }
                    face.replacementSlot = slot; face.firstVertex = vertices.Count;
                    var origins = new[] { face.a, face.b, face.c };
                    var mappedUV = origins.Select(i => MetricUV(planned.renderer.transform.TransformPoint(originalVertices[i]), face)).ToArray();
                    var tangent = TriangleTangent(originalVertices[face.a], originalVertices[face.b], originalVertices[face.c], mappedUV[0], mappedUV[1], mappedUV[2]);
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int original = origins[corner]; indices[slot].Add(vertices.Count);
                        vertices.Add(originalVertices[original]); normals.Add(originalNormals[original]);
                        tangents.Add(OrthogonalTangent(tangent.Item1, tangent.Item2, originalNormals[original]));
                        for (int channel = 0; channel < 8; channel++)
                            if (uv[channel].Count != 0) uv[channel].Add(channel == 0 ? new Vector4(mappedUV[corner].x, mappedUV[corner].y, 0, 0) : uv[channel][original]);
                        if (colors.Count != 0) colors.Add(colors[original]);
                    }
                }
            }
            var mesh = new Mesh { name = "Surface finishes — " + source.name, indexFormat = vertices.Count > 65535 || source.indexFormat == IndexFormat.UInt32 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTangents(tangents);
            for (int channel = 0; channel < 8; channel++) if (uv[channel].Count > 0) SetUVs(mesh, channel, uv[channel], uvDimensions[channel]);
            if (colors.Count > 0) mesh.SetColors(colors);
            mesh.subMeshCount = indices.Count;
            for (int sub = 0; sub < indices.Count; sub++) mesh.SetTriangles(indices[sub], sub, false);
            mesh.bounds = source.bounds;
            string path = AssetDatabase.GenerateUniqueAssetPath(MeshFolder + "/Finish-" + variants.Count + "-" + planned.renderer.GetEntityId() + ".asset");
            AssetDatabase.CreateAsset(mesh, path);
            return new RendererRecord { path = Path(root, planned.renderer.transform), rendererId = Id(planned.renderer), hierarchy = Hierarchy(root, planned.renderer.transform),
                beforeMesh = AssetDatabase.GetAssetPath(source), beforeMeshName = source.name, beforeMeshId = Id(source), afterMesh = path, afterMeshId = Id(mesh),
                beforeMaterials = MaterialPaths(planned.materials), afterMaterials = MaterialPaths(slots.ToArray()), faces = selected.Values.OrderBy(v => v.firstVertex).ToArray() };
        }

        static Material Variant(Material source, string family, Dictionary<string, Material> variants, List<MaterialRecord> rows)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source), key = sourcePath + "|" + family;
            if (variants.TryGetValue(key, out var found)) return found;
            var finish = Finishes[family]; string path = AssetDatabase.GenerateUniqueAssetPath(MaterialFolder + "/Finish-" + family + "-" + AssetDatabase.AssetPathToGUID(sourcePath) + ".mat");
            var material = new Material(source) { name = "Finish-" + family + " — " + source.name };
            string folder = TextureFolder + "/" + family + "/";
            material.SetTexture("_BaseMap", Required<Texture2D>(folder + "AlbedoColorPreserving.png"));
            material.SetTexture("_BumpMap", Required<Texture2D>(folder + family + "_nor_gl_1k.jpg"));
            material.SetTexture("_MetallicGlossMap", Required<Texture2D>(folder + "MetallicSmoothness.png"));
            material.SetTexture("_OcclusionMap", Required<Texture2D>(folder + "Occlusion.png"));
            foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" }) { material.SetTextureScale(property, Vector2.one); material.SetTextureOffset(property, Vector2.zero); }
            material.SetFloat("_BumpScale", finish.normal); material.SetFloat("_OcclusionStrength", .3f);
            material.SetFloat("_Metallic", family == "corrugated_iron" ? 1 : 0); material.SetFloat("_Smoothness", finish.smoothness);
            if (material.HasProperty("_SmoothnessTextureChannel")) material.SetFloat("_SmoothnessTextureChannel", 0);
            material.SetFloat("_EnvironmentReflections", 0); material.SetFloat("_SpecularHighlights", family == "corrugated_iron" ? 1 : 0);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_OCCLUSIONMAP");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (family == "corrugated_iron") material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF"); else material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.black);
            if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", null);
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            AssetDatabase.CreateAsset(material, path); variants.Add(key, material);
            rows.Add(new MaterialRecord { source = sourcePath, sourceId = Id(source), replacement = path, replacementId = Id(material), family = family });
            return material;
        }
        static void ConfigureTextures(IEnumerable<string> families)
        {
            foreach (string family in families.Distinct()) foreach (string path in MapPaths(family))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) throw new InvalidDataException("Expected a native texture importer: " + path);
                bool normal = path.EndsWith("_nor_gl_1k.jpg", StringComparison.Ordinal), color = path.EndsWith("AlbedoColorPreserving.png", StringComparison.Ordinal);
                bool changed = importer.textureType != (normal ? TextureImporterType.NormalMap : TextureImporterType.Default)
                    || importer.sRGBTexture != color || importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled || importer.maxTextureSize != 1024;
                if (!changed) continue;
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = color; importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true; importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
        }
        static string[] MapPaths(string family) => new[] { "AlbedoColorPreserving.png", family + "_nor_gl_1k.jpg", "MetallicSmoothness.png", "Occlusion.png" }.Select(p => TextureFolder + "/" + family + "/" + p).ToArray();

        static Report Assign(Transform root, Report report, bool replacement)
        {
            RequireScene(root); RequireNightRestored(root); ValidateReferences(root, report, true, replacement); VerifyGeometry(root, report); VerifyMaterials(report);
            var assignments = report.renderers.Select(row => { var renderer = Resolve(root, row); return new { renderer,
                beforeMesh = renderer.GetComponent<MeshFilter>().sharedMesh, beforeMaterials = renderer.sharedMaterials,
                mesh = MeshAsset(row, replacement), materials = (replacement ? row.afterMaterials : row.beforeMaterials)
                    .Select(p => string.IsNullOrEmpty(p) ? null : Required<Material>(p)).ToArray() }; }).ToArray();
            string before = SchoolFixtureReplacementPass.ProtectedSignature(root);
            if (before != report.protectedBefore) throw new InvalidOperationException("Physical/story/light state differs from the journal baseline.");
            int rendererCount = root.GetComponentsInChildren<MeshRenderer>(true).Length;
            try
            {
                foreach (var row in assignments) { row.renderer.GetComponent<MeshFilter>().sharedMesh = row.mesh; row.renderer.sharedMaterials = row.materials; }
                string after = SchoolFixtureReplacementPass.ProtectedSignature(root);
                if (before != after || rendererCount != root.GetComponentsInChildren<MeshRenderer>(true).Length)
                    throw new InvalidOperationException("Architectural finish changed physical/story/light state or the renderer inventory.");
                report.protectedBefore = before; report.protectedAfter = after; report.resultingRenderers = rendererCount;
                report.protectedStatePreserved = report.geometryPreserved = report.metricUVsVerified = report.noAddedRenderers = true;
                report.state = replacement ? "applied" : "restored"; Write(report); EditorSceneManager.MarkSceneDirty(root.gameObject.scene); return report;
            }
            catch
            {
                foreach (var row in assignments) { row.renderer.GetComponent<MeshFilter>().sharedMesh = row.beforeMesh; row.renderer.sharedMaterials = row.beforeMaterials; }
                throw;
            }
        }

        static void VerifyGeometry(Transform root, Report report)
        {
            foreach (var row in report.renderers)
            {
                var source = MeshAsset(row, false); var after = MeshAsset(row, true); var renderer = Resolve(root, row);
                var a = source.vertices; var b = after.vertices; var na = source.normals; var nb = after.normals; var ca = source.colors; var cb = after.colors;
                var sourceTriangles = Enumerable.Range(0, source.subMeshCount).Select(sub => source.GetTriangles(sub)).ToArray();
                var afterTriangles = Enumerable.Range(0, after.subMeshCount).Select(sub => after.GetTriangles(sub)).ToArray();
                var afterUV0 = after.uv;
                if (after.vertexCount != source.vertexCount + row.faces.Length * 3 || source.bounds != after.bounds
                    || !a.SequenceEqual(b.Take(a.Length)) || !na.SequenceEqual(nb.Take(na.Length))
                    || (ca.Length != 0 && !ca.SequenceEqual(cb.Take(ca.Length)))) throw new InvalidDataException("A source vertex, normal, color or bound changed: " + row.path);
                var ta = source.tangents; var tb = after.tangents;
                if (ta.Length != 0 && !ta.SequenceEqual(tb.Take(ta.Length))) throw new InvalidDataException("A retained source tangent changed: " + row.path);
                var origins = Enumerable.Range(0, source.vertexCount).ToList();
                foreach (var face in row.faces)
                {
                    if (face.firstVertex != origins.Count || face.replacementSlot < source.subMeshCount || !Finishes.ContainsKey(face.family)) throw new InvalidDataException("Invalid selected face record.");
                    var tri = sourceTriangles[face.sourceSlot]; int offset = face.sourceTriangle * 3;
                    if (tri[offset] != face.a || tri[offset + 1] != face.b || tri[offset + 2] != face.c) throw new InvalidDataException("Source triangle order changed.");
                    origins.AddRange(new[] { face.a, face.b, face.c });
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int index = face.firstVertex + corner, original = origins[index];
                        if (b[index] != a[original] || nb[index] != na[original] || (ca.Length != 0 && cb[index] != ca[original])) throw new InvalidDataException("A selected triangle attribute changed.");
                        var expected = MetricUV(renderer.transform.TransformPoint(a[original]), face);
                        var actual = afterUV0[index];
                        if (!Finite(actual.x) || !Finite(actual.y) || (expected - actual).sqrMagnitude > 1e-9f) throw new InvalidDataException("A selected UV is non-finite or not world-metric.");
                    }
                }
                for (int channel = 0; channel < 8; channel++)
                {
                    var oldUV = new List<Vector4>(); var newUV = new List<Vector4>(); source.GetUVs(channel, oldUV); after.GetUVs(channel, newUV);
                    if (oldUV.Count != 0 && !oldUV.SequenceEqual(newUV.Take(oldUV.Count))) throw new InvalidDataException("A retained UV channel changed.");
                    if (channel != 0 && oldUV.Count != 0)
                        for (int i = source.vertexCount; i < after.vertexCount; i++) if (newUV[i] != oldUV[origins[i]]) throw new InvalidDataException("A duplicated secondary UV changed.");
                }
                if (!TriangleKeys(source, null).SequenceEqual(TriangleKeys(after, origins))) throw new InvalidDataException("A triangle was lost, duplicated or its winding changed: " + row.path);
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    var removed = new HashSet<int>(row.faces.Where(f => f.sourceSlot == sub).Select(f => f.sourceTriangle)); var tri = sourceTriangles[sub];
                    var retained = Enumerable.Range(0, tri.Length / 3).Where(i => !removed.Contains(i)).SelectMany(i => new[] { tri[i * 3], tri[i * 3 + 1], tri[i * 3 + 2] });
                    if (!retained.SequenceEqual(afterTriangles[sub])) throw new InvalidDataException("An unselected material/triangle assignment changed.");
                }
                for (int sub = source.subMeshCount; sub < after.subMeshCount; sub++)
                {
                    var expected = row.faces.Where(f => f.replacementSlot == sub).SelectMany(f => new[] { f.firstVertex, f.firstVertex + 1, f.firstVertex + 2 });
                    if (!expected.SequenceEqual(afterTriangles[sub])) throw new InvalidDataException("Selected finish assignment differs from its journal.");
                }
            }
        }
        static string[] TriangleKeys(Mesh mesh, IList<int> origins)
        {
            var result = new List<string>(); var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3) result.Add(string.Join(",", new[] { triangles[i], triangles[i + 1], triangles[i + 2] }.Select(n => origins == null ? n : origins[n])));
            return result.OrderBy(v => v, StringComparer.Ordinal).ToArray();
        }
        static void VerifyMaterials(Report report)
        {
            foreach (var row in report.materials)
            {
                var original = Required<Material>(row.source); var replacement = Required<Material>(row.replacement);
                if (Id(original) != row.sourceId || Id(replacement) != row.replacementId || original.shader != replacement.shader
                    || original.GetColor("_BaseColor") != replacement.GetColor("_BaseColor") || replacement.IsKeywordEnabled("_EMISSION")
                    || replacement.GetColor("_EmissionColor").maxColorComponent > 0 || replacement.GetFloat("_EnvironmentReflections") != 0
                    || replacement.GetFloat("_SpecularHighlights") != (row.family == "corrugated_iron" ? 1 : 0)
                    || !replacement.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF")
                    || replacement.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF") != (row.family != "corrugated_iron")) throw new InvalidDataException("Finish material changed color or has an unexpected glow/reflection: " + row.replacement);
                var paths = MapPaths(row.family); var properties = new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" };
                for (int i = 0; i < paths.Length; i++) if (AssetDatabase.GetAssetPath(replacement.GetTexture(properties[i])) != paths[i]
                    || replacement.GetTextureScale(properties[i]) != Vector2.one || replacement.GetTextureOffset(properties[i]) != Vector2.zero) throw new InvalidDataException("Finish PBR map or tiling differs from its journal.");
                foreach (string property in new[] { "_Surface", "_Blend", "_Cull", "_ZWrite", "_SrcBlend", "_DstBlend" })
                    if (original.HasProperty(property) && original.GetFloat(property) != replacement.GetFloat(property)) throw new InvalidDataException("Finish material changed its surface state.");
            }
        }

        static bool SafeMaterial(Material material)
        {
            if (!material || material.shader.name != "Universal Render Pipeline/Lit") return false;
            string path = AssetDatabase.GetAssetPath(material);
            if (!path.StartsWith("Assets/SchoolCEEP/Materials/", StringComparison.Ordinal) || path.Contains("/Chapter") || path.Contains("/FinalChapters/")
                || path.Contains("/Data/") || path.Contains("/UI/") || material.name.StartsWith("Abandoned-", StringComparison.Ordinal)
                || material.name.StartsWith("Finish-", StringComparison.Ordinal)) return false;
            if (material.IsKeywordEnabled("_EMISSION") || (material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > .00001f)
                || (material.HasProperty("_Surface") && material.GetFloat("_Surface") > .5f)) return false;
            return true;
        }
        static string BaseName(Material material)
        {
            string path = AssetDatabase.GetAssetPath(material), filename = System.IO.Path.GetFileNameWithoutExtension(path);
            if (filename.StartsWith("Night-", StringComparison.Ordinal) && filename.Length == 38)
            {
                string sourcePath = AssetDatabase.GUIDToAssetPath(filename.Substring(6));
                var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath); if (source) return source.name;
            }
            string name = material.name;
            if (name == "Night-PhotoFrameBlueWall") return "PhotoFrameBlue";
            return name.StartsWith("CanteenNight-", StringComparison.Ordinal) ? name.Substring("CanteenNight-".Length) : name;
        }
        static bool Known(string name) => Plaster.Contains(name) || Blue.Contains(name) || Red.Contains(name) || Tiles.Contains(name) || Columns.Contains(name) || Roofs.Contains(name) || name == "PhotoWestWoodCeiling";
        static bool MasonryName(string name)
        { var n = name.ToLowerInvariant(); return n.StartsWith("parede") || n.StartsWith("verga") || n.StartsWith("peitoril") || n.StartsWith("mureta") || n.StartsWith("muro"); }
        static bool ArchitecturalLabel(string name)
        { var n = name.ToLowerInvariant(); return MasonryName(name) || n.Contains("cobertura") || n.Contains("telhado") || n.Contains("forro") || n.StartsWith("pilar") || n.StartsWith("coluna"); }
        static HashSet<Transform> NarrativeVisuals(Transform root)
        {
            var result = new HashSet<Transform>();
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true).Where(v => v))
            {
                var serialized = new SerializedObject(script);
                foreach (string field in new[] { "chair", "changedChair", "demonVisual", "ghostVisual", "actorVisual" })
                {
                    var property = serialized.FindProperty(field);
                    if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = property.objectReferenceValue;
                    if (value is Transform t) result.Add(t); else if (value is GameObject go) result.Add(go.transform); else if (value is Renderer r) result.Add(r.transform);
                }
            }
            return result;
        }
        static bool Excluded(MeshRenderer renderer, Transform root, HashSet<Transform> excluded)
        {
            if (excluded.Any(v => renderer.transform == v || renderer.transform.IsChildOf(v))) return true;
            for (var t = renderer.transform; t && t != root; t = t.parent)
            {
                if (t.GetComponent<Canvas>() || t.GetComponent<Rigidbody>()) return true;
                foreach (var script in t.GetComponents<MonoBehaviour>().Where(v => v))
                {
                    string name = script.GetType().Name;
                    if (new[] { "Clue", "Anchor", "Pickup", "Collectible", "Ghost", "StoryWorldEvent", "Poltergeist", "InteractiveDrawer", "Demon", "Enemy" }.Any(name.Contains)) return true;
                }
            }
            return false;
        }
        static Tuple<Vector3, Vector3> TriangleTangent(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            var e1 = b - a; var e2 = c - a; var d1 = ub - ua; var d2 = uc - ua; float determinant = d1.x * d2.y - d1.y * d2.x;
            if (Mathf.Abs(determinant) < 1e-10f) throw new InvalidDataException("Metric UV projection is degenerate.");
            return Tuple.Create((e1 * d2.y - e2 * d1.y) / determinant, (e2 * d1.x - e1 * d2.x) / determinant);
        }
        static Vector4 OrthogonalTangent(Vector3 tangent, Vector3 bitangent, Vector3 normal)
        { var t = Vector3.ProjectOnPlane(tangent, normal).normalized; return new Vector4(t.x, t.y, t.z, Vector3.Dot(Vector3.Cross(normal, t), bitangent) < 0 ? -1 : 1); }
        static Vector4 DefaultTangent(Vector3 normal)
        { var t = Vector3.Cross(Mathf.Abs(normal.y) < .9f ? Vector3.up : Vector3.forward, normal).normalized; return new Vector4(t.x, t.y, t.z, 1); }
        static void SetUVs(Mesh mesh, int channel, List<Vector4> values, int dimension)
        {
            if (dimension <= 2) mesh.SetUVs(channel, values.Select(v => new Vector2(v.x, v.y)).ToList());
            else if (dimension == 3) mesh.SetUVs(channel, values.Select(v => new Vector3(v.x, v.y, v.z)).ToList());
            else mesh.SetUVs(channel, values);
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static HashSet<string> Names(params string[] names) => new HashSet<string>(names, StringComparer.Ordinal);
        static string Id(UnityEngine.Object value) => GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();
        static string[] MaterialPaths(Material[] materials) => materials.Select(v => v ? AssetDatabase.GetAssetPath(v) : "").ToArray();
        static int[] Hierarchy(Transform root, Transform t)
        { var indices = new List<int>(); for (; t != root; t = t.parent) indices.Add(t.GetSiblingIndex()); indices.Reverse(); return indices.ToArray(); }
        static string Path(Transform root, Transform t)
        { var names = new List<string>(); for (; t != root; t = t.parent) names.Add(t.name); names.Reverse(); return string.Join("/", names); }
        static T Required<T>(string path) where T : UnityEngine.Object
        { var value = AssetDatabase.LoadAssetAtPath<T>(path); if (!value) throw new InvalidDataException("Required native asset missing: " + path); return value; }
        static Mesh MeshAsset(RendererRecord row, bool replacement)
        {
            string id = replacement ? row.afterMeshId : row.beforeMeshId, path = replacement ? row.afterMesh : row.beforeMesh;
            Mesh mesh = null;
            if (GlobalObjectId.TryParse(id, out var global)) mesh = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(global) as Mesh;
            if (!mesh && !replacement && path == "Library/unity default resources") mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().SingleOrDefault(v => v.name == row.beforeMeshName);
            if (!mesh || AssetDatabase.GetAssetPath(mesh) != path) throw new InvalidDataException("Journal mesh identity missing: " + row.path);
            return mesh;
        }
        static MeshRenderer Resolve(Transform root, RendererRecord row)
        {
            MeshRenderer renderer = null;
            if (GlobalObjectId.TryParse(row.rendererId, out var global)) renderer = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(global) as MeshRenderer;
            if (!renderer)
            {
                var t = root;
                foreach (int index in row.hierarchy) { if (index < 0 || index >= t.childCount) throw new InvalidDataException("Journal hierarchy changed."); t = t.GetChild(index); }
                renderer = t.GetComponent<MeshRenderer>();
            }
            if (!renderer || !renderer.transform.IsChildOf(root) || Path(root, renderer.transform) != row.path) throw new InvalidDataException("Journal renderer identity changed: " + row.path);
            return renderer;
        }
        static void ValidateReferences(Transform root, Report report, bool either, bool replacement)
        {
            // A later, separately journalled Night pass can replace retained old
            // material slots. Finish slots themselves and the surface mesh must
            // remain exact. Assignment/Restore still require NightRestore first.
            var night = !either && replacement ? AppliedNight(root) : null;
            foreach (var row in report.renderers)
            {
                var renderer = Resolve(root, row); var materialPaths = MaterialPaths(renderer.sharedMaterials); var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                bool old = materialPaths.SequenceEqual(row.beforeMaterials) && mesh == MeshAsset(row, false);
                bool newer = AfterMaterialsMatch(row, materialPaths, night) && mesh == MeshAsset(row, true);
                if (!(either ? old || newer : replacement ? newer : old)) throw new InvalidOperationException("Journal assignment was edited independently: " + row.path);
            }
        }
        static bool AfterMaterialsMatch(RendererRecord surface, string[] current, NightJournal night)
        {
            if (current.SequenceEqual(surface.afterMaterials)) return true;
            if (night == null || current.Length != surface.afterMaterials.Length) return false;
            var matches = night.renderers.Where(v => v.path == surface.path && v.hierarchy != null && surface.hierarchy != null
                && v.hierarchy.SequenceEqual(surface.hierarchy)).ToArray();
            if (matches.Length != 1) return false;
            var row = matches[0];
            if (row.beforeMaterials == null || row.afterMaterials == null || row.beforeMaterials.Length != current.Length || row.afterMaterials.Length != current.Length) return false;
            for (int slot = 0; slot < current.Length; slot++)
            {
                string expected = surface.afterMaterials[slot];
                if (current[slot] == expected) continue;
                if (slot >= surface.beforeMaterials.Length || row.beforeMaterials[slot] != expected || row.afterMaterials[slot] != current[slot]
                    || !night.materials.Any(v => v.source == expected && v.replacement == current[slot])) return false;
            }
            return true;
        }
        static NightJournal AppliedNight(Transform root)
        {
            if (!File.Exists(NightJournalPath)) return null;
            var night = JsonUtility.FromJson<NightJournal>(File.ReadAllText(NightJournalPath));
            if (night == null || night.state != "applied") return null;
            if (night.scene != root.gameObject.scene.path || night.root != root.name || night.materials == null || night.renderers == null)
                throw new InvalidDataException("Applied Night journal does not match this school.");
            return night;
        }
        static void RequireNightRestored(Transform root)
        {
            if (AppliedNight(root) != null) throw new InvalidOperationException("Restore SchoolNightMaterialPass before assigning or restoring architectural finishes.");
        }
        static Report Read(Transform root)
        {
            RequireScene(root); if (!File.Exists(JournalPath)) throw new InvalidOperationException("No architectural finish journal exists.");
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(JournalPath));
            if (report == null || report.revision != 1 || report.scene != root.gameObject.scene.path || report.root != root.name
                || report.rootId != Id(root) || report.renderers == null || report.materials == null) throw new InvalidDataException("Architectural finish journal does not match this saved school.");
            return report;
        }
        static void Write(Report report)
        {
            Directory.CreateDirectory(Output); string temporary = JournalPath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            if (File.Exists(JournalPath)) File.Replace(temporary, JournalPath, null); else File.Move(temporary, JournalPath);
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return; int split = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, split)); AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
        static void RequireScene(Transform root)
        {
            if (!root || EditorUtility.IsPersistent(root) || EditorApplication.isPlayingOrWillChangePlaymode || root.gameObject.scene.path != ScenePath)
                throw new InvalidOperationException("Operate on the saved SchoolCEEP architecture outside Play mode.");
        }
    }
}
