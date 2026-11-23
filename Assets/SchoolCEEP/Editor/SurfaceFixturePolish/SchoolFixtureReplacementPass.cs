// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HorrorGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>Replace proved static fixture faces without changing electrical or story objects.</summary>
    public static class SchoolFixtureReplacementPass
    {
        public const string GroupName = "Escola — luminárias prontas e fixadas";
        public const string Models = "Assets/SchoolCEEP/Models/SurfaceFixturePolish";
        public const string Output = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/SurfaceFixturePolish";
        public const string ManifestPath = Models + "/fixture-replacement-manifest.json";
        const float Gap = .002f, PlaneTolerance = .0025f, CellSize = 2f;
        // Reviewed prepared mesh: the authored ceiling plane is below the
        // screw tip. The plate and the chain stud occupy separate patches on
        // that same plane; the narrow screw tip legitimately enters the roof.
        static readonly Vector3 PendantPlate = new Vector3(.03981735f, -.0153373f, .00000235f);
        static readonly Vector3 PendantPlateSize = new Vector3(.0520629f, 0, .0512725f);
        static readonly Vector3 PendantStud = new Vector3(-.00000005f, -.0153373f, -.00000015f);
        static readonly Vector3 PendantStudSize = new Vector3(.0063146302f, 0, .0053618707f);
        const float CeilingSearchRadius = .65f;
        static readonly Vector3[] CeilingOffsets = Enumerable.Range(-13, 27)
            .SelectMany(x => Enumerable.Range(-13, 27).Select(z => new Vector3(x * .05f, 0, z * .05f)))
            .Where(p => p.sqrMagnitude <= CeilingSearchRadius * CeilingSearchRadius + .000001f)
            .OrderBy(p => p.sqrMagnitude).ThenBy(p => p.x).ThenBy(p => p.z).ToArray();
        static readonly string[] Fluorescents = { "mounted_fluorescent_lights_b", "mounted_fluorescent_lights_d",
            "mounted_fluorescent_lights_b", "mounted_fluorescent_lights_c", "mounted_fluorescent_lights_d",
            "mounted_fluorescent_lights_e", "mounted_fluorescent_lights_f", "mounted_fluorescent_lights_g" };
        static readonly HashSet<string> AnchorNames = new HashSet<string>(StringComparer.Ordinal) {
            "Luminária fluorescente", "Sala — luminária fluorescente comprida", "Central — luminária retangular",
            "Passarela — luminária sob cobertura", "Hall — luminária suspensa", "Escada — luminária de parede",
            "WestBlock canopy — luminária fluorescente", "Refeitório — corpo de plafon",
            "Ligação refeitório/bloco — lâmpada sob forro", "Oeste alto — base da luminária oval",
            "Vestíbulo — carcaça de luminária sob telhado", "Carcaça da luminária" };
        static readonly HashSet<string> ClassParts = new HashSet<string> { "Sala — refletor da luminária", "Sala — tubo fluorescente", "Sala — aleta do refletor" };
        static readonly HashSet<string> AllVisualNames = new HashSet<string>(AnchorNames.Concat(new[] {
            "Central — tubo fluorescente", "Refeitório — difusor luminoso", "Cantina — fixação de luminária",
            "Ligação refeitório/bloco — soquete", "Oeste alto — luminária oval opalina",
            "Vestíbulo — difusor branco de luminária", "Vestíbulo — fixação da luminária",
            "Difusor emissivo", "Suspensão", "Pátio oeste — braço de arandela", "Mesas de jogos — braço de arandela" }).Concat(ClassParts));
        static readonly Regex InstanceReference = new Regex("\\\"instanceID\\\"\\s*:\\s*(-?[0-9]+)", RegexOptions.Compiled);
        // EditorJsonUtility still emits legacy integer instanceID references in
        // Unity 6000.6. Its public compatibility conversion is error-obsolete
        // for source calls, so the normalizer invokes that exact public bridge.
        static readonly MethodInfo LegacyEntityId = typeof(EntityId).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "op_Implicit" && m.ReturnType == typeof(EntityId)
                && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(int));

        [Serializable] public sealed class SourceRecord
        {
            public string name, path, matrix, primitive;
            public int[] hierarchy;
            public bool directRenderer;
        }
        [Serializable] public sealed class FixtureRecord
        {
            public string source, family, model, path, removalReason, supportFilter, supportMesh, nearestLight;
            public Vector3 originalCenter, position, supportPoint, supportNormal, size, contactSize, localMountPoint, floorPoint, horizontalOffset;
            public Vector3 attachmentPoint, attachmentSupportPoint;
            public Quaternion rotation;
            public float scale, floorHeight, bottomHeight, originalLightDistance, replacementLightDistance, lightDistanceChange, attachmentInset, mountSearchRadius;
            public int mountAttempts, supportHits, coverageFailures, clearanceFailures, pendantSlopeFailures;
            public Vector3[] contactPoints, attachmentContactPoints;
            public bool mounted, removedUnsupported;
        }
        [Serializable] public sealed class Report
        {
            public int revision = 2, fixtures, replaced, removedUnsupported, ceilingFixtures, hangingFixtures, wallFixtures;
            public int sourceParts, removedTriangles, changedBatches, directRenderersDisabled, supportTriangles;
            public int addedLights, addedColliders, addedScripts, remainingOriginalFaces;
            public bool protectedStatePreserved, originalTransformsPreserved, allReplacementsMounted, allMeaningfulFixturesHandled, alreadyApplied;
            public string scene, state, protectedBefore, protectedAfter, replacementFolder;
            public SourceRecord[] sources;
            public FixtureRecord[] pieces;
            public SchoolBatchVisualReplacement.Report batchRemoval;
            public string[] notes;
        }
        sealed class Candidate
        {
            public Transform anchor;
            public string family;
            public readonly List<Transform> parts = new List<Transform>();
            public FixtureRecord record;
        }
        sealed class Model
        {
            public string id;
            public GameObject prefab;
            public Bounds bounds;
        }
        sealed class Plan
        {
            public Report report;
            public Candidate[] candidates;
            public Transform[] parts;
            public Dictionary<string, Model> models;
        }
        struct Face
        {
            public Vector3 a, b, c, normal;
            public Bounds bounds;
            public string filter, mesh;
        }
        struct Hit
        {
            public Vector3 point, normal;
            public string filter, mesh;
            public float distance;
        }
        sealed class Surfaces
        {
            public readonly List<Face> faces = new List<Face>();
            readonly Dictionary<Vector2Int, List<int>> grid = new Dictionary<Vector2Int, List<int>>();
            public void Add(Face face)
            {
                int index = faces.Count; faces.Add(face);
                var lo = Cell(face.bounds.min); var hi = Cell(face.bounds.max);
                for (int x = lo.x; x <= hi.x; x++) for (int z = lo.y; z <= hi.y; z++)
                {
                    var cell = new Vector2Int(x, z);
                    if (!grid.TryGetValue(cell, out var list)) { list = new List<int>(); grid.Add(cell, list); }
                    list.Add(index);
                }
            }
            public bool Cast(Vector3 origin, Vector3 direction, float distance, Func<Vector3, bool> normalTest, out Hit hit)
            {
                hit = default; hit.distance = distance + .001f;
                var lo = Cell(Vector3.Min(origin, origin + direction * distance));
                var hi = Cell(Vector3.Max(origin, origin + direction * distance));
                var seen = new HashSet<int>();
                for (int x = lo.x; x <= hi.x; x++) for (int z = lo.y; z <= hi.y; z++)
                {
                    if (!grid.TryGetValue(new Vector2Int(x, z), out var list)) continue;
                    foreach (int index in list)
                    {
                        if (!seen.Add(index)) continue;
                        var face = faces[index];
                        if (!normalTest(face.normal) || !TriangleRay(origin, direction, face.a, face.b, face.c, out float d)
                            || d < -.0001f || d >= hit.distance || d > distance) continue;
                        hit = new Hit { distance = d, point = origin + direction * d, normal = face.normal, filter = face.filter, mesh = face.mesh };
                    }
                }
                return hit.distance <= distance;
            }
        }

        public static Report Preview(Transform root)
        {
            Require(root);
            if (root.Find(GroupName)) { var prior = Audit(root); prior.alreadyApplied = true; return prior; }
            return Prepare(root).report;
        }

        public static Report Apply(Transform root)
        {
            Require(root);
            if (root.Find(GroupName)) { var prior = Audit(root); prior.alreadyApplied = true; return prior; }
            if (File.Exists(ManifestPath)) throw new InvalidOperationException("A fixture manifest already exists without its scene group. Restore the saved scene/manifest together before retrying.");
            if (root.gameObject.scene.isDirty) throw new InvalidOperationException("Start fixture replacement from a saved clean school, after restoring the previous material journal.");
            // All source faces, prefab content, five-point mounts and clearance
            // are proved before creating any scene object or editing a batch.
            var plan = Prepare(root); var report = plan.report;
            report.replacementFolder = Models + "/FixtureReplacement-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            report.state = "pending"; Write(report);
            Transform group = null;
            try
            {
                report.batchRemoval = SchoolBatchVisualReplacement.Replace(root, plan.parts, report.replacementFolder);
                report.removedTriangles = report.batchRemoval.removedTriangles;
                report.changedBatches = report.batchRemoval.changedBatches;
                report.directRenderersDisabled = report.batchRemoval.directRenderersDisabled;
                group = new GameObject(GroupName).transform; group.SetParent(root, false);
                int index = 0;
                foreach (var candidate in plan.candidates)
                {
                    var piece = candidate.record;
                    if (piece.removedUnsupported) continue;
                    var item = (GameObject)PrefabUtility.InstantiatePrefab(plan.models[piece.model].prefab, group);
                    item.name = "Luminária pronta — " + (++index).ToString("D3") + " — " + piece.family;
                    item.transform.SetPositionAndRotation(piece.position, piece.rotation);
                    item.transform.localScale = Vector3.one * piece.scale;
                    foreach (var t in item.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
                    piece.path = GroupName + "/" + item.name;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
                }
                report.protectedAfter = ProtectedSignature(root);
                report.protectedStatePreserved = report.protectedBefore == report.protectedAfter;
                report.originalTransformsPreserved = CheckSources(root, report.sources);
                report.addedLights = group.GetComponentsInChildren<Light>(true).Length;
                report.addedColliders = group.GetComponentsInChildren<Collider>(true).Length;
                report.addedScripts = group.GetComponentsInChildren<MonoBehaviour>(true).Length;
                if (!report.protectedStatePreserved || !report.originalTransformsPreserved
                    || report.addedLights != 0 || report.addedColliders != 0 || report.addedScripts != 0)
                    throw new InvalidOperationException("Fixture visuals changed protected components or source transforms.");
                report.state = "applied"; Write(report);
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                return Audit(root);
            }
            catch (Exception error)
            {
                // Original mesh assets were never modified. Roll back scene
                // references on failure; the failed manifest remains evidence.
                if (group) UnityEngine.Object.DestroyImmediate(group.gameObject);
                if (report.batchRemoval != null)
                {
                    var all = root.GetComponentsInChildren<Transform>(true).ToDictionary(AbsolutePath);
                    foreach (var change in report.batchRemoval.batches ?? Array.Empty<SchoolBatchVisualReplacement.BatchChange>())
                        if (all.TryGetValue(change.filterPath, out var t) && t.GetComponent<MeshFilter>())
                            t.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(change.originalMeshAssetPath);
                    foreach (string path in report.batchRemoval.directRendererPaths ?? Array.Empty<string>())
                        if (all.TryGetValue(path, out var t) && t.GetComponent<MeshRenderer>()) t.GetComponent<MeshRenderer>().enabled = true;
                }
                report.state = "failed: " + error.Message; Write(report);
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                throw;
            }
        }

        public static Report Audit(Transform root)
        {
            Require(root);
            var group = root.Find(GroupName);
            if (!group || !File.Exists(ManifestPath)) throw new InvalidOperationException("Fixture group or manifest is absent.");
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(ManifestPath));
            if (report == null || report.state != "applied" || report.scene != root.gameObject.scene.path)
                throw new InvalidDataException("Fixture manifest is not an applied record of this school.");
            report.originalTransformsPreserved = CheckSources(root, report.sources);
            report.protectedAfter = ProtectedSignature(root);
            report.protectedStatePreserved = report.protectedBefore == report.protectedAfter;
            var parts = report.sources.Select(s => Resolve(root, s.hierarchy)).ToArray();
            var surfaces = ReadSurfaces(root, parts);
            var models = ReadModels();
            foreach (var piece in report.pieces.Where(p => p.mounted))
            {
                var t = root.Find(piece.path);
                if (!t || (t.position - piece.position).sqrMagnitude > .000001f
                    || Quaternion.Angle(t.rotation, piece.rotation) > .01f
                    || (t.lossyScale - Vector3.one * piece.scale).sqrMagnitude > .000001f)
                    throw new InvalidOperationException("Fixture moved or disappeared: " + piece.path);
                if (t.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.sharedMaterials.Any(m => !m
                    || m.IsKeywordEnabled("_EMISSION") || m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > .00001f)))
                    throw new InvalidOperationException("Fixture instance has a missing or powered material: " + piece.path);
                if (!MountMeetsSupport(surfaces, piece))
                    throw new InvalidOperationException("Fixture no longer meets its actual rendered support plane: " + piece.path);
                if (piece.model != "industrial_wall_lamp" && (piece.horizontalOffset.magnitude > CeilingSearchRadius + .0001f
                    || Vector3.Distance(piece.floorPoint, piece.originalCenter + piece.horizontalOffset) > .0005f
                    || Vector2.Distance(new Vector2(piece.supportPoint.x, piece.supportPoint.z), new Vector2(piece.floorPoint.x, piece.floorPoint.z)) > .0005f))
                    throw new InvalidOperationException("Fixture exceeds its bounded original-location mount search: " + piece.path);
                if (!Floor(surfaces, piece.floorPoint, out float floor)
                    || Mathf.Abs(floor - piece.floorHeight) > .006f
                    || WorldBounds(models[piece.model].bounds, t.position, t.rotation, piece.scale).min.y - floor
                        < (piece.model == "hanging_industrial_lamp" ? 2.4f : 2.1f) - .003f)
                    throw new InvalidOperationException("Fixture support floor or head clearance changed: " + piece.path);
            }
            report.remainingOriginalFaces = CountOriginalFaces(root, report.sources);
            report.addedLights = group.GetComponentsInChildren<Light>(true).Length;
            report.addedColliders = group.GetComponentsInChildren<Collider>(true).Length;
            report.addedScripts = group.GetComponentsInChildren<MonoBehaviour>(true).Length;
            report.allReplacementsMounted = true;
            report.allMeaningfulFixturesHandled = report.pieces.Length == report.fixtures
                && report.pieces.All(p => p.mounted || p.removedUnsupported);
            if (!report.protectedStatePreserved || !report.originalTransformsPreserved || report.remainingOriginalFaces != 0
                || group.childCount != report.replaced || report.addedLights != 0 || report.addedColliders != 0 || report.addedScripts != 0)
                throw new InvalidOperationException("Fixture audit failed: protected state, frozen sources, removed faces or added components changed.");
            return report;
        }

        static Plan Prepare(Transform root)
        {
            var models = ReadModels();
            var candidates = ReadCandidates(root);
            if (candidates.Length == 0) throw new InvalidDataException("No known original school fixture parts were found.");
            var parts = candidates.SelectMany(c => c.parts).Distinct().OrderBy(AbsolutePath, StringComparer.Ordinal).ToArray();
            var report = new Report { scene = root.gameObject.scene.path, state = "preview", fixtures = candidates.Length,
                sourceParts = parts.Length, protectedBefore = ProtectedSignature(root), sources = parts.Select(t => new SourceRecord {
                    name = t.name, path = AbsolutePath(t), hierarchy = Hierarchy(t, root), matrix = MatrixText(t.localToWorldMatrix),
                    primitive = Primitive(t), directRenderer = t.GetComponent<MeshRenderer>() && t.GetComponent<MeshRenderer>().enabled
                        && t.gameObject.activeInHierarchy }).ToArray() };
            var surfaces = ReadSurfaces(root, parts); report.supportTriangles = surfaces.faces.Count;
            var lights = root.GetComponentsInChildren<Light>(true).Where(l => l.type != LightType.Directional).ToArray();
            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i]; var center = candidate.anchor.position;
                var piece = new FixtureRecord { source = AbsolutePath(candidate.anchor), family = candidate.family,
                    originalCenter = center, floorPoint = center, rotation = candidate.anchor.rotation };
                candidate.record = piece;
                if (!Floor(surfaces, center, out float floor))
                    Reject(piece, "No rendered occupied floor found at least 1.7 m below the old fixture; no substitute support fabricated.");
                else
                {
                    piece.floorHeight = floor;
                    bool wall = candidate.family == "wall" || candidate.family == "photo-wall" || candidate.family == "pole";
                    bool mounted = wall ? PlanWall(surfaces, candidate, piece, models["industrial_wall_lamp"])
                        : PlanCeiling(surfaces, candidate, piece, models, i);
                    if (!mounted) Reject(piece, wall
                        ? "No actual rendered wall/pole face covers the five contact points with 2.1 m head clearance. Removed only the proven unsupported visual."
                        : "No first visible ceiling/roof face supports a model within " + piece.mountSearchRadius.ToString("F2", CultureInfo.InvariantCulture)
                            + " m of the old horizontal position. Support hits=" + piece.supportHits + ", model attempts=" + piece.mountAttempts
                            + ", coverage failures=" + piece.coverageFailures + ", clearance failures=" + piece.clearanceFailures
                            + ", pendant slope failures=" + piece.pendantSlopeFailures + ". Removed only the proven unsupported visual.");
                }
                if (lights.Length > 0)
                {
                    var light = lights.OrderBy(l => (l.transform.position - center).sqrMagnitude).First();
                    piece.nearestLight = GlobalObjectId.GetGlobalObjectIdSlow(light).ToString();
                    piece.originalLightDistance = Vector3.Distance(light.transform.position, center);
                    piece.replacementLightDistance = piece.mounted ? Vector3.Distance(light.transform.position, piece.position) : -1;
                    piece.lightDistanceChange = piece.mounted ? piece.replacementLightDistance - piece.originalLightDistance : 0;
                }
            }
            // Matching every triangle is required even for an unsupported old
            // fixture which will be removed without adding a replacement.
            report.batchRemoval = SchoolBatchVisualReplacement.Preview(root, parts);
            report.removedTriangles = report.batchRemoval.removedTriangles;
            report.changedBatches = report.batchRemoval.changedBatches;
            report.directRenderersDisabled = report.batchRemoval.directRenderersDisabled;
            report.pieces = candidates.Select(c => c.record).ToArray();
            report.replaced = report.pieces.Count(p => p.mounted);
            report.removedUnsupported = report.pieces.Count(p => p.removedUnsupported);
            report.hangingFixtures = report.pieces.Count(p => p.model == "hanging_industrial_lamp");
            report.wallFixtures = report.pieces.Count(p => p.model == "industrial_wall_lamp");
            report.ceilingFixtures = report.replaced - report.hangingFixtures - report.wallFixtures;
            report.allReplacementsMounted = true; report.allMeaningfulFixturesHandled = true;
            report.notes = new[] {
                "Every selected original Cube/Cylinder/Sphere triangle is proved before cloning and filtering static batches. Original transforms, colliders and mesh assets remain recoverable.",
                "Mounts are five-point ray intersections with actual enabled rendered triangles. Colliders and renderer bounds are never accepted as support proof. The first downward-facing roof/ceiling plane is used; wall/pole contacts use the nearest real vertical plane.",
                "Ceiling placements search at most 0.65 m around the original horizontal position, ordered by distance. Fluorescent units try both long-axis orientations to fit one side of a real roof ridge. The chosen horizontal location has its own rendered-floor proof; electrical Light objects remain fixed.",
                "Hall/canteen pendants use the actual offset ceiling plate and a separate chain-stud patch on the authored common mount plane. The 15.3373 mm anchoring screw enters the roof above that plane; both footprints must meet rendered support. Pendants require a nearly horizontal support and at least 2.4 m to the rendered floor. Other overhead models require 2.1 m.",
                "Refectory recipes contain the original body and diffuser plus an existing fixation beam only where one survives. Every selected part is proved individually; no missing source part is fabricated. Missing or unsuitable supports produce an explicit removal record, never a floating replacement.",
                "All scene MonoBehaviours, Lights and Colliders retain normalized serialized data and world matrices. Electrical Light objects and all existing story bindings are retained exactly; reported visual/light distances do not move those lights.",
                "Models have no lights, colliders, scripts or emission. Fluorescent variants include authored missing tubes. New visuals do not enable electrical power or change the nighttime preset.",
                "Audit compares geometry rather than frozen batch mesh references, allowing subsequent UV/material-only cloning while still proving the original fixture faces remain absent." };
            return new Plan { candidates = candidates, parts = parts, models = models, report = report };
        }

        static Candidate[] ReadCandidates(Transform root)
        {
            var nodes = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.gameObject.activeInHierarchy && !Below(t, GroupName) && !t.GetComponent<Light>()).ToArray();
            var candidates = nodes.Where(t => AnchorNames.Contains(t.name)).OrderBy(AbsolutePath, StringComparer.Ordinal)
                .Select(t => new Candidate { anchor = t, family = Family(t) }).ToArray();
            foreach (var c in candidates) c.parts.Add(c.anchor);
            foreach (var t in nodes.Where(t => AllVisualNames.Contains(t.name) && !AnchorNames.Contains(t.name)))
            {
                IEnumerable<Candidate> matching = Array.Empty<Candidate>();
                if (ClassParts.Contains(t.name)) matching = candidates.Where(c => c.anchor.name == "Sala — luminária fluorescente comprida" && c.anchor.parent == t.parent);
                else if (t.name == "Central — tubo fluorescente") matching = candidates.Where(c => c.anchor.name == "Central — luminária retangular" && c.anchor.parent == t.parent);
                else if (t.name == "Refeitório — difusor luminoso" || t.name == "Cantina — fixação de luminária") matching = candidates.Where(c => c.anchor.name == "Refeitório — corpo de plafon");
                else if (t.name == "Ligação refeitório/bloco — soquete") matching = candidates.Where(c => c.anchor.name == "Ligação refeitório/bloco — lâmpada sob forro" && c.anchor.parent == t.parent);
                else if (t.name == "Oeste alto — luminária oval opalina") matching = candidates.Where(c => c.anchor.name == "Oeste alto — base da luminária oval" && c.anchor.parent == t.parent);
                else if (t.name.StartsWith("Vestíbulo — ", StringComparison.Ordinal)) matching = candidates.Where(c => c.anchor.name == "Vestíbulo — carcaça de luminária sob telhado" && c.anchor.parent == t.parent);
                else if (t.name == "Difusor emissivo" || t.name == "Suspensão") matching = candidates.Where(c => c.anchor.name == "Carcaça da luminária" && c.anchor.parent == t.parent && t.parent.name == "Luminária de teto — foto");
                else if (t.name == "Pátio oeste — braço de arandela" || t.name == "Mesas de jogos — braço de arandela") matching = candidates.Where(c => c.anchor.name == "Carcaça da luminária");
                var nearest = matching.OrderBy(c => (c.anchor.position - t.position).sqrMagnitude).FirstOrDefault();
                if (nearest == null || Vector3.Distance(nearest.anchor.position, t.position) > 1.2f)
                    throw new InvalidDataException("An original fixture mesh part has no unambiguous nearby holder: " + AbsolutePath(t));
                nearest.parts.Add(t);
                if (t.name.Contains("braço de arandela")) nearest.family = "photo-wall";
            }
            foreach (var c in candidates)
            {
                int expected = c.anchor.name == "Sala — luminária fluorescente comprida" ? 12
                    : c.anchor.name == "Central — luminária retangular" || c.anchor.name == "Carcaça da luminária" ? 3
                    : c.anchor.name == "Refeitório — corpo de plafon" ? 2 + c.parts.Count(t => t.name == "Cantina — fixação de luminária")
                    : c.anchor.name == "Oeste alto — base da luminária oval" || c.anchor.name == "Ligação refeitório/bloco — lâmpada sob forro" ? 2
                    : c.anchor.name == "Vestíbulo — carcaça de luminária sob telhado" ? 3 : 1;
                if (c.family == "photo-wall") expected++;
                bool canteenRecipe = c.anchor.name != "Refeitório — corpo de plafon"
                    || c.parts.Count(t => t.name == "Refeitório — difusor luminoso") == 1
                        && c.parts.Count(t => t.name == "Cantina — fixação de luminária") <= 1
                        && c.parts.All(t => t.name == "Refeitório — corpo de plafon" || t.name == "Refeitório — difusor luminoso" || t.name == "Cantina — fixação de luminária");
                if (!canteenRecipe || c.parts.Count != expected || c.parts.Any(t => t.GetComponent<Light>() || t.GetComponent<MonoBehaviour>() || t.GetComponent<Collider>()))
                    throw new InvalidDataException("Fixture source recipe/visual-only contract changed: " + AbsolutePath(c.anchor) + " parts=" + c.parts.Count + " expected=" + expected);
            }
            return candidates;
        }

        static string Family(Transform t)
        {
            if (t.name == "Escada — luminária de parede" || t.name == "Oeste alto — base da luminária oval") return "wall";
            if (t.name == "Hall — luminária suspensa") return "hall";
            if (t.name == "Refeitório — corpo de plafon") return "canteen";
            if (t.name == "Luminária fluorescente" && t.parent.name == "Iluminação externa") return "pole";
            if (t.name == "Sala — luminária fluorescente comprida") return "classroom";
            if (t.name == "Carcaça da luminária") return "photo-ceiling";
            return "ceiling";
        }

        static bool PlanCeiling(Surfaces surfaces, Candidate source, FixtureRecord piece, Dictionary<string, Model> models, int index)
        {
            Vector3 center = source.anchor.position;
            piece.mountSearchRadius = CeilingSearchRadius;
            var preferred = source.family == "hall" || source.family == "canteen" ? "hanging_industrial_lamp" : Fluorescents[index % Fluorescents.Length];
            foreach (string id in new[] { preferred, Fluorescents[index % Fluorescents.Length] }.Distinct())
            {
                var model = models[id];
                bool pendant = id == "hanging_industrial_lamp";
                float requestedLength = source.anchor.name == "Passarela — luminária sob cobertura" ? .80f : 1.20f;
                float scale = pendant ? 1f : Mathf.Clamp(requestedLength / model.bounds.size.x, .85f, 1.4f);
                foreach (var offset in CeilingOffsets)
                {
                    var floorPoint = center + offset;
                    if (!Floor(surfaces, floorPoint, out float floor)) continue;
                    if (!surfaces.Cast(new Vector3(floorPoint.x, floor + 2.11f, floorPoint.z), Vector3.up, 7f,
                        n => n.y < -.65f, out var support)) continue;
                    piece.supportHits++;
                    // Keep a hanging chain almost vertical. Its authored plate
                    // and stud still align exactly to the actual support normal.
                    if (pendant && support.normal.y > -.995f) { piece.pendantSlopeFailures++; continue; }
                    var up = -support.normal;
                    var forward = Vector3.ProjectOnPlane(source.anchor.forward, up).normalized;
                    if (forward.sqrMagnitude < .5f) forward = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
                    var baseRotation = Quaternion.LookRotation(forward, up);
                    if (!pendant && source.anchor.name == "Hall — luminária suspensa") baseRotation *= Quaternion.Euler(0, 90, 0);
                    foreach (float yaw in pendant ? new[] { 0f } : new[] { 0f, 90f })
                    {
                        piece.mountAttempts++;
                        var rotation = baseRotation * Quaternion.Euler(0, yaw, 0);
                        var mount = pendant ? PendantPlate : Vector3.zero;
                        Vector3 position = support.point + support.normal * Gap - rotation * (mount * scale);
                        var bounds = WorldBounds(model.bounds, position, rotation, scale);
                        if (bounds.min.y - floor < (pendant ? 2.4f : 2.1f)) { piece.clearanceFailures++; continue; }
                        Vector3 contact = pendant ? PendantPlateSize * scale
                            : new Vector3(model.bounds.size.x * scale * .94f, 0, model.bounds.size.z * scale * .88f);
                        var points = ContactPoints(support.point, support.normal, rotation, contact, false);
                        if (!Contact(surfaces, points, support.normal)) { piece.coverageFailures++; continue; }
                        Vector3[] attachmentPoints = null;
                        var attachmentSupport = Vector3.zero; var attachmentTip = Vector3.zero;
                        float inset = 0;
                        if (pendant)
                        {
                            attachmentSupport = position + rotation * (PendantStud * scale) - support.normal * Gap;
                            attachmentPoints = ContactPoints(attachmentSupport, support.normal, rotation, PendantStudSize * scale, false);
                            if (!Contact(surfaces, attachmentPoints, support.normal)) { piece.coverageFailures++; continue; }
                            attachmentTip = position + rotation * (new Vector3(PendantStud.x, 0, PendantStud.z) * scale);
                            inset = -Vector3.Dot(attachmentTip - attachmentSupport, support.normal);
                            if (inset < 0 || Mathf.Abs(inset - (-PendantStud.y * scale - Gap)) > .0001f) continue;
                        }
                        Fill(piece, model, position, rotation, scale, support, bounds, contact, points);
                        piece.localMountPoint = mount; piece.attachmentPoint = attachmentTip;
                        piece.attachmentSupportPoint = attachmentSupport; piece.attachmentInset = inset;
                        piece.attachmentContactPoints = attachmentPoints;
                        piece.floorPoint = floorPoint; piece.floorHeight = floor; piece.horizontalOffset = offset;
                        return true;
                    }
                }
            }
            return false;
        }

        static bool PlanWall(Surfaces surfaces, Candidate source, FixtureRecord piece, Model model)
        {
            var center = source.anchor.position;
            if (source.family == "pole") center.y -= .18f;
            var hits = new List<Hit>();
            foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                if (surfaces.Cast(center, direction, 1.1f, n => Mathf.Abs(n.y) < .05f, out var hit)) hits.Add(hit);
            foreach (var hit in hits.OrderBy(h => h.distance))
            {
                Quaternion rotation = Quaternion.LookRotation(hit.normal, Vector3.up);
                float scale = source.family == "pole" ? .85f : source.anchor.name == "Oeste alto — base da luminária oval" ? .75f : 1f;
                var position = hit.point + hit.normal * Gap;
                var bounds = WorldBounds(model.bounds, position, rotation, scale);
                if (bounds.min.y - piece.floorHeight < 2.1f) continue;
                // The small central screw/contact patch can be backed by a
                // narrow existing pole; the lamp body may overhang that patch.
                // Broad masonry uses the complete authored back footprint.
                var contact = source.family == "pole" ? new Vector3(.035f, .16f, 0)
                    : new Vector3(model.bounds.size.x * scale * .85f, model.bounds.size.y * scale * .85f, 0);
                var points = ContactPoints(hit.point, hit.normal, rotation, contact, true);
                if (!Contact(surfaces, points, hit.normal)) continue;
                Fill(piece, model, position, rotation, scale, hit, bounds, contact, points); return true;
            }
            return false;
        }

        static void Fill(FixtureRecord piece, Model model, Vector3 position, Quaternion rotation, float scale, Hit support,
            Bounds bounds, Vector3 contact, Vector3[] points)
        {
            piece.model = model.id; piece.position = position; piece.rotation = rotation; piece.scale = scale;
            piece.supportPoint = support.point; piece.supportNormal = support.normal; piece.supportFilter = support.filter;
            piece.supportMesh = support.mesh; piece.size = bounds.size; piece.bottomHeight = bounds.min.y;
            piece.contactSize = contact; piece.contactPoints = points; piece.mounted = true;
        }
        static void Reject(FixtureRecord piece, string reason) { piece.removedUnsupported = true; piece.removalReason = reason; }
        static bool Floor(Surfaces surfaces, Vector3 center, out float height)
        {
            bool found = surfaces.Cast(center - Vector3.up * 1.70f, Vector3.down, 12f, n => n.y > .90f, out var hit);
            height = found ? hit.point.y : 0; return found;
        }
        static Vector3[] ContactPoints(Vector3 point, Vector3 normal, Quaternion rotation, Vector3 size, bool wall)
        {
            var u = Vector3.ProjectOnPlane(rotation * Vector3.right, normal).normalized;
            var v = wall ? Vector3.up : Vector3.Cross(normal, u).normalized;
            float second = wall ? size.y : size.z;
            return new[] { point, point - u * size.x * .5f - v * second * .5f, point + u * size.x * .5f - v * second * .5f,
                point + u * size.x * .5f + v * second * .5f, point - u * size.x * .5f + v * second * .5f };
        }
        static bool Contact(Surfaces surfaces, Vector3[] points, Vector3 normal)
        {
            return points != null && points.Length == 5 && points.All(p => surfaces.Cast(p + normal * .055f, -normal, .11f,
                n => Vector3.Dot(n, normal) > .995f, out var hit) && Vector3.Distance(hit.point, p) <= PlaneTolerance);
        }

        static bool MountMeetsSupport(Surfaces surfaces, FixtureRecord piece)
        {
            var mount = piece.position + piece.rotation * (piece.localMountPoint * piece.scale);
            if (!Contact(surfaces, piece.contactPoints, piece.supportNormal)
                || Mathf.Abs(Vector3.Dot(mount - piece.supportPoint, piece.supportNormal) - Gap) > .0005f) return false;
            if (piece.model != "hanging_industrial_lamp") return true;
            var stud = piece.position + piece.rotation * (PendantStud * piece.scale) - piece.supportNormal * Gap;
            var tip = piece.position + piece.rotation * (new Vector3(PendantStud.x, 0, PendantStud.z) * piece.scale);
            return piece.supportNormal.y <= -.995f && (piece.localMountPoint - PendantPlate).sqrMagnitude < .00000001f
                && Vector3.Dot(piece.rotation * Vector3.up, -piece.supportNormal) > .99999f
                && (piece.contactSize - PendantPlateSize * piece.scale).sqrMagnitude < .00000001f
                && Contact(surfaces, piece.attachmentContactPoints, piece.supportNormal)
                && Vector3.Distance(stud, piece.attachmentSupportPoint) <= .0005f
                && Vector3.Distance(tip, piece.attachmentPoint) <= .0005f
                && Mathf.Abs(piece.attachmentInset - (-PendantStud.y * piece.scale - Gap)) <= .0005f
                && Mathf.Abs(-Vector3.Dot(tip - stud, piece.supportNormal) - piece.attachmentInset) <= .0005f;
        }

        static Surfaces ReadSurfaces(Transform root, Transform[] oldParts)
        {
            var result = new Surfaces();
            var nightAliases = ReadNightStructuralAliases(root);
            var exclusions = new Dictionary<Vector2Int, List<Bounds>>();
            foreach (var part in oldParts)
            {
                var bounds = PrimitiveBounds(part); bounds.Expand(.001f);
                var lo = Cell(bounds.min); var hi = Cell(bounds.max);
                for (int x = lo.x; x <= hi.x; x++) for (int z = lo.y; z <= hi.y; z++)
                {
                    var cell = new Vector2Int(x, z);
                    if (!exclusions.TryGetValue(cell, out var list)) { list = new List<Bounds>(); exclusions.Add(cell, list); }
                    list.Add(bounds);
                }
            }
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>(); var mesh = filter.sharedMesh;
                if (!mesh || !renderer || !renderer.enabled || !filter.gameObject.activeInHierarchy || Below(filter.transform, GroupName)
                    || Below(filter.transform, "Escola — objetos Poly Haven do abandono")
                    || Below(filter.transform, "Escola — desgaste e caixas nas circulações")
                    || AllVisualNames.Contains(filter.name) || filter.GetComponentInParent<InteractiveDoor>()
                    || Below(filter.transform, "Escola — abandono estático")) continue;
                string path = AssetDatabase.GetAssetPath(mesh);
                bool batch = filter.name.StartsWith("Superfícies agrupadas — ", StringComparison.Ordinal);
                if (!batch && (path.StartsWith("Assets/models/", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("Assets/SchoolCEEP/Models/", StringComparison.OrdinalIgnoreCase))
                    || filter.name.StartsWith("Abandono —", StringComparison.Ordinal)) continue;
                var vertices = mesh.vertices; var matrix = filter.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                var materials = renderer.sharedMaterials;
                nightAliases.TryGetValue(renderer, out var aliases);
                string filterId = GlobalObjectId.GetGlobalObjectIdSlow(filter).ToString();
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    string material = sub < materials.Length && materials[sub] ? materials[sub].name.ToLowerInvariant() : "";
                    if (aliases != null && sub < aliases.Length && aliases[sub] != null) material = aliases[sub];
                    if (!Structural(filter.transform, material) || mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
                    var indices = mesh.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                        var normal = Vector3.Cross(b - a, c - a);
                        if (normal.sqrMagnitude < 1e-12f) continue;
                        var centroid = (a + b + c) / 3;
                        if (exclusions.TryGetValue(Cell(centroid), out var boxes) && boxes.Any(box => box.Contains(a) && box.Contains(b) && box.Contains(c))) continue;
                        var bounds = new Bounds(a, Vector3.zero); bounds.Encapsulate(b); bounds.Encapsulate(c);
                        result.Add(new Face { a = a, b = b, c = c, normal = normal.normalized, bounds = bounds,
                            filter = filterId, mesh = path });
                    }
                }
            }
            return result;
        }

        static Dictionary<MeshRenderer, string[]> ReadNightStructuralAliases(Transform root)
        {
            var result = new Dictionary<MeshRenderer, string[]>();
            if (!File.Exists(SchoolNightMaterialPass.JournalPath)) return result;
            var night = JsonUtility.FromJson<SchoolNightMaterialPass.Report>(File.ReadAllText(SchoolNightMaterialPass.JournalPath));
            if (night == null || night.state != "applied") return result;
            if (night.scene != root.gameObject.scene.path || night.root != root.name)
                throw new InvalidDataException("Applied Night journal belongs to a different school.");
            var bindings = new HashSet<string>((night.materials ?? Array.Empty<SchoolNightMaterialPass.MaterialRecord>())
                .Select(m => m.source + "\n" + m.replacement), StringComparer.Ordinal);
            // Night GUID names lose structural hints; accept only journal-proved renderer/slot source bindings.
            foreach (var row in night.renderers ?? Array.Empty<SchoolNightMaterialPass.RendererRecord>())
            {
                if (row.hierarchy == null) continue;
                var t = Resolve(root, row.hierarchy);
                var renderer = t ? t.GetComponent<MeshRenderer>() : null;
                var names = new Stack<string>();
                for (var p = t; p && p != root; p = p.parent) names.Push(p.name);
                if (!renderer || string.Join("/", names) != row.path || row.beforeMaterials == null || row.afterMaterials == null)
                    throw new InvalidDataException("Applied Night structural source binding changed: " + row.path);
                var current = renderer.sharedMaterials;
                if (current.Length != row.afterMaterials.Length)
                    throw new InvalidDataException("Applied Night structural material slots changed: " + row.path);
                var aliases = new string[current.Length]; bool found = false;
                for (int slot = 0; slot < current.Length && slot < row.beforeMaterials.Length; slot++)
                {
                    string source = row.beforeMaterials[slot], replacement = row.afterMaterials[slot];
                    if (source == replacement || AssetDatabase.GetAssetPath(current[slot]) != replacement
                        || !bindings.Contains(source + "\n" + replacement)) continue;
                    var original = AssetDatabase.LoadAssetAtPath<Material>(source);
                    if (!original) throw new InvalidDataException("Applied Night structural source material is missing: " + source);
                    aliases[slot] = original.name.ToLowerInvariant(); found = true;
                }
                if (found && !result.TryAdd(renderer, aliases))
                    throw new InvalidDataException("Applied Night journal repeats a structural renderer binding: " + row.path);
            }
            return result;
        }

        static bool Structural(Transform filter, string material)
        {
            if (Below(filter, "Iluminação externa")) return true;
            // The architectural finish pass splits roof/beam faces into new
            // material slots. CreateAsset uses the file's family/hash name,
            // losing the old PhotoRoofGrey/wood source-name hints. Recognize
            // only these reviewed structural families; their actual rendered
            // triangles still have to pass every normal/contact/floor test.
            if (material.Contains("finish-corrugated_iron-") || material.Contains("finish-wooden_planks-")) return true;
            string name = filter.name.ToLowerInvariant();
            if (new[] { "parede", "verga", "piso", "forro", "cobertura", "telhado", "laje", "viga", "poste", "pilar", "degrau", "landing", "rampa", "sala — painel" }.Any(name.StartsWith)) return true;
            if (new[] { " — caibro", " — tirante", " — tesoura", " — travessa", " — viga" }.Any(name.Contains)) return true;
            return new[] { "roof", "roofing", "reboco", "parede", "plaster", "masonry", "concreto", "concrete", "white", "cream", "offwhite",
                "ceiling", "teto", "forro", "pale", "yellow", "amarel", "floor", "piso", "deck", "ramp", "tile", "ceram", "central" }.Any(material.Contains);
        }

        static Dictionary<string, Model> ReadModels()
        {
            var result = new Dictionary<string, Model>(StringComparer.Ordinal);
            foreach (string id in Fluorescents.Concat(new[] { "mounted_fluorescent_lights_a", "industrial_wall_lamp", "hanging_industrial_lamp" }).Distinct())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/" + id + "/" + id + ".prefab");
                if (!prefab || prefab.GetComponentsInChildren<Light>(true).Length != 0 || prefab.GetComponentsInChildren<Collider>(true).Length != 0
                    || prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                    throw new InvalidDataException("Prepared native visual-only fixture prefab missing or invalid: " + id);
                var filters = prefab.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.StartsWith("LOD0", StringComparison.Ordinal) && f.sharedMesh).ToArray();
                if (filters.Length == 0) throw new InvalidDataException("Fixture has no original-detail native geometry: " + id);
                var bounds = new Bounds(); bool initialized = false;
                foreach (var f in filters)
                {
                    var r = f.GetComponent<MeshRenderer>();
                    if (!r || r.sharedMaterials.Any(m => !m || m.IsKeywordEnabled("_EMISSION")
                        || m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > .00001f))
                        throw new InvalidDataException("Fixture material is absent or powered: " + id);
                    foreach (var v in f.sharedMesh.vertices)
                    {
                        var point = prefab.transform.InverseTransformPoint(f.transform.TransformPoint(v));
                        if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; } else bounds.Encapsulate(point);
                    }
                }
                if (id == "industrial_wall_lamp" ? Mathf.Abs(bounds.min.z) > .0001f : Mathf.Abs(bounds.max.y) > .0001f)
                    throw new InvalidDataException("Fixture mount pivot is not the reviewed top/back origin: " + id);
                result.Add(id, new Model { id = id, prefab = prefab, bounds = bounds });
            }
            return result;
        }

        static string Primitive(Transform t)
        {
            if (t.name == "Suspensão" || t.name.Contains("braço de arandela")) return "FrontPhotoCylinder";
            if (t.name == "Refeitório — corpo de plafon" || t.name == "Refeitório — difusor luminoso") return "Cylinder";
            if (t.name == "Ligação refeitório/bloco — lâmpada sob forro" || t.name.StartsWith("Oeste alto —", StringComparison.Ordinal)) return "Sphere";
            return "Cube";
        }
        static Bounds PrimitiveBounds(Transform t)
            => WorldBounds(new Bounds(Vector3.zero, Primitive(t) == "Cylinder" ? new Vector3(1, 2, 1) : Vector3.one),
                t.position, t.rotation, t.lossyScale);
        static Bounds WorldBounds(Bounds local, Vector3 position, Quaternion rotation, float scale)
            => WorldBounds(local, position, rotation, Vector3.one * scale);
        static Bounds WorldBounds(Bounds local, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var bounds = new Bounds(position + rotation * Vector3.Scale(local.center, scale), Vector3.zero);
            foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                bounds.Encapsulate(position + rotation * Vector3.Scale(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z)), scale));
            return bounds;
        }
        static bool CheckSources(Transform root, SourceRecord[] records)
        {
            foreach (var record in records)
            {
                var t = Resolve(root, record.hierarchy);
                if (!t || t.name != record.name || MatrixText(t.localToWorldMatrix) != record.matrix
                    || record.directRenderer && (!t.GetComponent<MeshRenderer>() || t.GetComponent<MeshRenderer>().enabled)) return false;
            }
            return true;
        }

        // Audit uses vertex positions instead of exact batch asset references:
        // later UV-only/split-material clones may safely keep the filtered faces.
        static int CountOriginalFaces(Transform root, SourceRecord[] sources)
        {
            var builtins = AssetDatabase.LoadAllAssetsAtPath("Library/unity default resources").OfType<Mesh>().ToArray();
            var primitives = new Dictionary<string, Mesh> {
                { "Cube", builtins.Single(m => m.name == "Cube" && m.vertexCount == 24) },
                { "Cylinder", builtins.Single(m => m.name == "Cylinder" && m.vertexCount == 88) },
                { "Sphere", builtins.Single(m => m.name == "Sphere" && (m.bounds.size - Vector3.one).sqrMagnitude < .000001f) } };
            // Same unit-height twelve-sided recipe as SchoolFrontPhotoProps.
            var frontVertices = new Vector3[26]; var frontIndices = new int[144];
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI * 2f / 12;
                frontVertices[i] = new Vector3(Mathf.Cos(angle) * .5f, -.5f, Mathf.Sin(angle) * .5f);
                frontVertices[12 + i] = frontVertices[i] + Vector3.up; int next = (i + 1) % 12;
                int[] face = { i, 12 + i, next, next, 12 + i, 12 + next, 24, i, next, 25, 12 + next, 12 + i };
                Array.Copy(face, 0, frontIndices, i * 12, 12);
            }
            frontVertices[24] = Vector3.down * .5f; frontVertices[25] = Vector3.up * .5f;
            var frontMesh = new Mesh { name = "Front photo cylinder — audit recipe", hideFlags = HideFlags.HideAndDontSave,
                vertices = frontVertices, triangles = frontIndices };
            primitives.Add("FrontPhotoCylinder", frontMesh);
            const float cell = .02f;
            var expected = new Dictionary<Vector3Int, List<Face>>();
            Func<Vector3, Vector3Int> key = p => new Vector3Int(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell));
            foreach (var source in sources)
            {
                var t = Resolve(root, source.hierarchy); var mesh = primitives[source.primitive];
                var vertices = mesh.vertices; var matrix = t.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                var indices = mesh.GetTriangles(0);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var face = new Face { a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]] };
                    var k = key((face.a + face.b + face.c) / 3);
                    if (!expected.TryGetValue(k, out var list)) { list = new List<Face>(); expected.Add(k, list); } list.Add(face);
                }
            }
            int found = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.StartsWith("Superfícies agrupadas — ", StringComparison.Ordinal)))
            {
                var renderer = filter.GetComponent<MeshRenderer>(); var mesh = filter.sharedMesh;
                if (!mesh || !renderer || !renderer.enabled || !filter.gameObject.activeInHierarchy) continue;
                var vertices = mesh.vertices; var matrix = filter.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
                    var indices = mesh.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var face = new Face { a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]] };
                        var k = key((face.a + face.b + face.c) / 3); bool match = false;
                        for (int x = -1; x <= 1 && !match; x++) for (int y = -1; y <= 1 && !match; y++) for (int z = -1; z <= 1 && !match; z++)
                            if (expected.TryGetValue(k + new Vector3Int(x, y, z), out var list) && list.Any(f => SameFace(face, f))) match = true;
                        if (match) found++;
                    }
                }
            }
            UnityEngine.Object.DestroyImmediate(frontMesh);
            return found;
        }
        static bool SameFace(Face a, Face b)
            => new[] { b.a, b.b, b.c }.Any(v => Near(a.a, v)) && new[] { b.a, b.b, b.c }.Any(v => Near(a.b, v))
                && new[] { b.a, b.b, b.c }.Any(v => Near(a.c, v));
        static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= .0001f * .0001f;
        static bool TriangleRay(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0; var ab = b - a; var ac = c - a; var p = Vector3.Cross(direction, ac);
            float det = Vector3.Dot(ab, p); if (Mathf.Abs(det) < 1e-9f) return false;
            float inv = 1 / det; var t = origin - a; float u = Vector3.Dot(t, p) * inv;
            if (u < -.00001f || u > 1.00001f) return false;
            var q = Vector3.Cross(t, ab); float v = Vector3.Dot(direction, q) * inv;
            if (v < -.00001f || u + v > 1.00001f) return false;
            distance = Vector3.Dot(ac, q) * inv; return distance >= -.0001f;
        }
        public static string ProtectedSignature(Transform root)
        {
            var rows = new List<string>();
            var referenceIds = new Dictionary<int, string>();
            // Include every root in the school scene, including the player,
            // save/director, authored event lights and chapter-owned components.
            foreach (var sceneRoot in root.gameObject.scene.GetRootGameObjects())
                foreach (var component in sceneRoot.GetComponentsInChildren<Component>(true)
                    .Where(c => c is MonoBehaviour || c is Collider || c is Light))
                {
                    if (!component) throw new InvalidDataException("A protected scene component is missing.");
                    string json = InstanceReference.Replace(EditorJsonUtility.ToJson(component), match => {
                        int id = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                        if (!referenceIds.TryGetValue(id, out var global))
                        {
                            var referenced = id == 0 ? null : EditorUtility.EntityIdToObject((EntityId)LegacyEntityId.Invoke(null, new object[] { id }));
                            global = referenced ? GlobalObjectId.GetGlobalObjectIdSlow(referenced).ToString() : "null";
                            referenceIds.Add(id, global);
                        }
                        return "\"globalObjectID\":\"" + global + "\"";
                    });
                    rows.Add(component.GetType().FullName + "|" + GlobalObjectId.GetGlobalObjectIdSlow(component) + "|" + json
                        + "|" + MatrixText(component.transform.localToWorldMatrix));
                }
            rows.Sort(StringComparer.Ordinal);
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", rows)))).Replace("-", "");
        }
        static string MatrixText(Matrix4x4 m) => string.Join("|", Enumerable.Range(0, 16).Select(i => m[i].ToString("R", CultureInfo.InvariantCulture)));
        static bool Below(Transform t, string name) { for (var p = t; p; p = p.parent) if (p.name == name) return true; return false; }
        static Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));
        static int[] Hierarchy(Transform t, Transform root)
        {
            var indices = new Stack<int>(); for (var p = t; p && p != root; p = p.parent) indices.Push(p.GetSiblingIndex()); return indices.ToArray();
        }
        static Transform Resolve(Transform root, int[] indices)
        {
            var t = root; foreach (int index in indices ?? Array.Empty<int>()) { if (!t || index < 0 || index >= t.childCount) return null; t = t.GetChild(index); } return t;
        }
        static string AbsolutePath(Transform t)
        {
            var names = new Stack<string>(); for (var p = t; p; p = p.parent) names.Push(p.name + "[" + p.GetSiblingIndex() + "]"); return string.Join("/", names);
        }
        static void Require(Transform root)
        {
            if (!root || EditorUtility.IsPersistent(root) || root.gameObject.scene.path != SchoolIntegrationPass.ScenePath
                || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use the loaded school architecture outside Play mode.");
        }
        static void Write(Report report)
        {
            Directory.CreateDirectory(Models); Directory.CreateDirectory(Output);
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(ManifestPath, json, new UTF8Encoding(false));
            File.WriteAllText(Output + "/fixtures.json", json, new UTF8Encoding(false));
        }
    }
}
