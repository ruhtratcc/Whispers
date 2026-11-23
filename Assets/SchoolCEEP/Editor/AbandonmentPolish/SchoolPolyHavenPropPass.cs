// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HorrorGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SchoolCEEP.Editor
{
    /// <summary>
    /// Dress the saved school with four shared Poly Haven props, without rebuilding
    /// rooms, changing physics/story, or enabling electricity. Old source assets
    /// and holders remain available; repeated Apply calls audit the existing group.
    /// </summary>
    public static class SchoolPolyHavenPropPass
    {
        public const string GroupName = "Escola — objetos Poly Haven do abandono";
        public const string AssetFolder = "Assets/SchoolCEEP/Models/AbandonmentPolish";
        const string Output = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/AbandonmentPolish";
        const float FanHeight = .51634336f, FanDiameter = 1.4629171f;
        const string SurfaceGroup = "Escola — desgaste e caixas nas circulações";
        static readonly string[] Ids = { "ceiling_fan", "wall_clock", "metal_trash_can", "plastic_broom" };
        static readonly HashSet<string> FanNames = new HashSet<string> {
            "Sala — suporte do ventilador", "Sala — aro do ventilador", "Sala — motor do ventilador",
            "Sala — hélice do ventilador", "Sala — grade do ventilador" };

        [Serializable] public sealed class Report
        {
            public int clocks, clockBatchesDisabled, ceilingFans, originalFanParts, originalFanTrianglesRemoved;
            public int bins, brooms, rejectedGroundCandidates, addedColliders, addedLights, addedScripts, lodGroups;
            public bool architecturePreserved, storyPreserved, allGroundPropsSupported, allFansMounted, alreadyApplied;
            public bool authorizedPresentationOnly;
            public string architectureBefore, architectureAfter, storyBefore, storyAfter;
            public string presentationNormalizedStoryAfter;
            public string[] clockBatchPaths, fanSourcePaths, notes;
            public PieceRecord[] pieces;
        }
        [Serializable] public sealed class PieceRecord
        {
            public string id, name, path, room;
            public Vector3 position, size, supportPoint, supportNormal;
            public Quaternion rotation;
            public float scale;
        }
        class Plan
        {
            public Transform sourceRoom;
            public string id, name;
            public Vector3 position, support, normal = Vector3.up;
            public Quaternion rotation = Quaternion.identity;
            public float scale = 1;
            public Vector3 size;
        }
        sealed class ClockPlan : Plan { public MeshRenderer original; }
        sealed class Room
        {
            public Transform original;
            public Bounds bounds;
            public float floor;
            public Vector3 mainStart, mainEnd, crossStart, crossEnd;
            public readonly List<Bounds> doorSweeps = new List<Bounds>();
        }

        public static Report Apply(Transform root)
        {
            RequireScene(root);
            var existing = root.Find(GroupName);
            if (existing) { var prior = Audit(root); prior.alreadyApplied = true; return prior; }
            if (root.gameObject.scene.isDirty)
                throw new InvalidOperationException("Apply the Poly Haven prop pass before wall changes, from a saved clean scene.");
            foreach (string id in Ids) ValidatePrefab(id);
            Physics.SyncTransforms();
            var report = new Report { architectureBefore = PhysicalSignature(root), storyBefore = StorySignature(root) };
            var clocks = ReadClocks(root);
            var fans = root.GetComponentsInChildren<Transform>(true).Where(t => FanNames.Contains(t.name)).ToArray();
            var rings = fans.Where(t => t.name == "Sala — aro do ventilador").ToArray();
            if (rings.Length == 0 || fans.Length != rings.Length * 14)
                throw new InvalidDataException("Each original classroom fan must have 14 proven source parts.");
            var fanPlans = rings.Select(PlanFan).ToArray();
            // Pure proof precedes every scene/mesh mutation. Failure leaves the
            // scene untouched, including old clock batch visibility.
            SchoolBatchVisualReplacement.Preview(root, fans);
            var rooms = ReadRooms(root, rings);
            var ground = PlanGround(root, rooms, report);
            string folder = AssetFolder + "/FanReplacement-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            var replacement = SchoolBatchVisualReplacement.Replace(root, fans, folder);
            report.originalFanParts = fans.Length;
            report.originalFanTrianglesRemoved = replacement.removedTriangles;

            var group = new GameObject(GroupName).transform; group.SetParent(root, false);
            var records = new List<PieceRecord>();
            foreach (var clock in clocks) records.Add(Place(clock, group));
            foreach (var renderer in clocks.Select(c => c.original).Distinct())
            { renderer.enabled = false; EditorUtility.SetDirty(renderer); }
            foreach (var fan in fanPlans) records.Add(Place(fan, group));
            foreach (var plan in ground) records.Add(Place(plan, group));
            report.clocks = clocks.Count; report.clockBatchesDisabled = clocks.Select(c => c.original).Distinct().Count();
            report.ceilingFans = fanPlans.Length; report.bins = ground.Count(p => p.id == "metal_trash_can"); report.brooms = ground.Count(p => p.id == "plastic_broom");
            report.clockBatchPaths = clocks.Select(c => PathOf(c.original.transform, root)).Distinct().ToArray();
            report.fanSourcePaths = fans.Select(t => PathOf(t, root)).ToArray();
            report.pieces = records.ToArray();
            report.architectureAfter = PhysicalSignature(root); report.architecturePreserved = report.architectureBefore == report.architectureAfter;
            report.storyAfter = StorySignature(root); report.storyPreserved = report.storyBefore == report.storyAfter;
            report.addedColliders = group.GetComponentsInChildren<Collider>(true).Length;
            report.addedLights = group.GetComponentsInChildren<Light>(true).Length;
            report.addedScripts = group.GetComponentsInChildren<MonoBehaviour>(true).Length;
            report.lodGroups = group.GetComponentsInChildren<LODGroup>(true).Length;
            report.allGroundPropsSupported = ground.All(p => GroundSupport(root, WorldBounds(p), p.support.y));
            report.allFansMounted = fanPlans.All(p => Mathf.Abs(p.position.y + FanHeight * p.scale - p.support.y) < .001f);
            report.notes = new[] {
                "Four publisher-verified CC0 Poly Haven models. No runtime importer, electricity, animation, collider or story component added.",
                "Dedicated flat clock batches are hidden, not deleted. New clocks are backed against the original visible clock plane, outside the wall finish/decals; old shared rust hand marks remain behind the volumetric dial.",
                "Every original fan face is proved before being removed from cloned static batches. Original holders/assets and replacement journal remain recoverable.",
                "Ceiling fan mounts use existing coplanar classroom panel surfaces, proven at five footprint points. Uniform scales 1/.82/.70/.62 fit their actual bays without altering ceilings, beams, luminaires or projectors.",
                "A small number of bins and brooms occupy supported room-edge footprints, outside saved aisles, door sweeps, clues and existing physical furniture.",
                "Repeat application audits the existing group; it does not add duplicate props. Actual gameplay darkness and appearance require the caller's render inspection." };
            if (!report.architecturePreserved || !report.storyPreserved || !report.allGroundPropsSupported || !report.allFansMounted
                || report.addedColliders != 0 || report.addedLights != 0 || report.addedScripts != 0)
                throw new InvalidOperationException("Poly Haven dressing changed protected state or has unsupported props. Reload the saved scene before retry.");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/polyhaven-props.json", JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            File.WriteAllText(Output + "/fan-batch-replacement.json", JsonUtility.ToJson(replacement, true), new UTF8Encoding(false));
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            return report;
        }

        public static Report Audit(Transform root)
        {
            RequireScene(root); var group = root.Find(GroupName);
            if (!group || !File.Exists(Output + "/polyhaven-props.json")) throw new InvalidOperationException("Poly Haven prop group/report absent.");
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(Output + "/polyhaven-props.json"));
            foreach (var piece in report.pieces)
            {
                var t = root.Find(piece.path);
                if (!t || (t.position - piece.position).sqrMagnitude > .000001f || Quaternion.Angle(t.rotation, piece.rotation) > .01f
                    || (t.localScale - Vector3.one * piece.scale).sqrMagnitude > .000001f)
                    throw new InvalidOperationException("A Poly Haven prop moved or disappeared: " + piece.path);
            }
            foreach (string path in report.clockBatchPaths)
                if (!root.Find(path) || root.Find(path).GetComponent<MeshRenderer>().enabled)
                    throw new InvalidOperationException("A procedural flat clock batch was restored: " + path);
            if (group.childCount != report.pieces.Length) throw new InvalidOperationException("Duplicate or missing Poly Haven props.");
            report.architectureAfter = PhysicalSignature(root); report.architecturePreserved = report.architectureBefore == report.architectureAfter;
            report.storyAfter = StorySignature(root); report.storyPreserved = report.storyBefore == report.storyAfter;
            CheckAuthorizedPresentation(root, report);
            report.addedColliders = group.GetComponentsInChildren<Collider>(true).Length;
            report.addedLights = group.GetComponentsInChildren<Light>(true).Length; report.addedScripts = group.GetComponentsInChildren<MonoBehaviour>(true).Length;
            if (!report.architecturePreserved || !(report.storyPreserved || report.authorizedPresentationOnly)
                || report.addedColliders != 0 || report.addedLights != 0 || report.addedScripts != 0)
                throw new InvalidOperationException("Protected state changed since the prop dressing pass.");
            return report;
        }

        /// <summary>
        /// Correct existing clock visuals from their retained original quads.
        /// This computes an absolute pose, so repeating it adds no offset and
        /// does not recreate props, materials, colliders or story components.
        /// </summary>
        public static Report RetuneClocks(Transform root)
        {
            var report = Audit(root);
            Physics.SyncTransforms();
            string physicalBefore = PhysicalSignature(root), storyBefore = StorySignature(root);
            var plans = ReadClocks(root, true).ToDictionary(p => p.name, StringComparer.Ordinal);
            var pieces = report.pieces.Where(p => p.id == "wall_clock").ToArray();
            if (pieces.Length != report.clocks || plans.Count != pieces.Length)
                throw new InvalidDataException("Retained original clock quads differ from the applied clock manifest.");
            var targets = new List<Tuple<Transform, PieceRecord, ClockPlan>>();
            foreach (var piece in pieces)
            {
                if (!plans.TryGetValue(piece.name, out var plan)
                    || !report.clockBatchPaths.Contains(PathOf(plan.original.transform, root))
                    || plan.original.enabled || (piece.supportNormal - plan.normal).sqrMagnitude > .000001f
                    || Quaternion.Angle(piece.rotation, plan.rotation) > .01f || (piece.size - plan.size).sqrMagnitude > .000001f)
                    throw new InvalidDataException("Clock retune source or orientation changed: " + piece.name);
                targets.Add(Tuple.Create(root.Find(piece.path), piece, plan));
            }
            bool changed = false;
            foreach (var target in targets)
            {
                var t = target.Item1; var piece = target.Item2; var plan = target.Item3;
                if ((t.position - plan.position).sqrMagnitude > .00000001f)
                { t.position = plan.position; EditorUtility.SetDirty(t); changed = true; }
                piece.position = plan.position; piece.supportPoint = plan.support;
            }
            report.architectureAfter = PhysicalSignature(root); report.storyAfter = StorySignature(root);
            report.architecturePreserved = report.architectureAfter == physicalBefore && report.architectureAfter == report.architectureBefore;
            report.storyPreserved = report.storyAfter == storyBefore && report.storyAfter == report.storyBefore;
            CheckAuthorizedPresentation(root, report);
            if (!report.architecturePreserved || report.storyAfter != storyBefore
                || !(report.storyPreserved || report.authorizedPresentationOnly))
                throw new InvalidOperationException("Clock visual retune changed protected state. Reload the saved scene before retry.");
            const string note = "Clock backs use the nearest proven visible wall finish at the retained clock position, minus 2 mm, leaving the dial beyond wall finish/decals. Absolute source-derived poses make RetuneClocks idempotent.";
            report.notes = (report.notes ?? Array.Empty<string>()).Concat(new[] { note }).Distinct().ToArray();
            File.WriteAllText(Output + "/polyhaven-props.json", JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            if (changed) EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            return Audit(root);
        }

        static List<ClockPlan> ReadClocks(Transform root, bool includeDisabled = false)
        {
            var result = new List<ClockPlan>();
            var decay = root.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == "Escola — abandono do capítulo 3");
            if (!decay) throw new InvalidDataException("Original abandonment decoration absent.");
            foreach (var renderer in decay.GetComponentsInChildren<MeshRenderer>(true).Where(r => (r.enabled || includeDisabled) && r.sharedMaterial && r.sharedMaterial.name == "Relógio parado"))
            {
                var filter = renderer.GetComponent<MeshFilter>(); var mesh = filter ? filter.sharedMesh : null;
                if (!mesh || mesh.subMeshCount != 1 || mesh.vertexCount % 4 != 0 || mesh.triangles.Length != mesh.vertexCount / 4 * 6)
                    throw new InvalidDataException("Clock material is not its dedicated quad batch.");
                var vertices = mesh.vertices; var triangles = mesh.triangles;
                for (int i = 0; i < vertices.Length; i += 4)
                {
                    int q = i / 4 * 6; var expected = new[] { i, i + 1, i + 2, i, i + 2, i + 3 };
                    if (!triangles.Skip(q).Take(6).SequenceEqual(expected)) throw new InvalidDataException("Clock quad topology differs from the original recipe.");
                    var a = filter.transform.TransformPoint(vertices[i]); var b = filter.transform.TransformPoint(vertices[i + 1]);
                    var c = filter.transform.TransformPoint(vertices[i + 2]); var d = filter.transform.TransformPoint(vertices[i + 3]);
                    var normal = Vector3.Cross(b - a, c - a).normalized; var center = (a + b + c + d) * .25f;
                    if (Mathf.Abs(normal.y) > .05f || Mathf.Abs(Vector3.Distance(a, b) - .32f) > .002f || Mathf.Abs(Vector3.Distance(b, c) - .32f) > .002f)
                        throw new InvalidDataException("Stopped clock quad dimensions/orientation changed.");
                    var hit = Physics.RaycastAll(center + normal * .12f, -normal, .5f, ~0, QueryTriggerInteraction.Ignore)
                        .Where(h => h.collider.transform.IsChildOf(root) && Vector3.Dot(h.normal, normal) > .85f).OrderBy(h => h.distance).FirstOrDefault();
                    if (!hit.collider) throw new InvalidOperationException("No existing supporting wall behind stopped clock at " + center);
                    var rotation = Quaternion.LookRotation(-normal, Vector3.up);
                    var clockSize = new Vector3(.32049099f, .32049099f, .04722310f);
                    // The collision wall is behind photographic finishes and
                    // abandonment decals. Use the retained visible clock plane
                    // for the rear support; otherwise those faces cover the dial.
                    // Pivot is bottom-center and front is -Z after conversion.
                    var visualSupport = VisibleWallPoint(root, center, normal) - normal * .002f;
                    var p = visualSupport + normal * clockSize.z * .5f - Vector3.up * clockSize.y * .5f;
                    result.Add(new ClockPlan { id = "wall_clock", name = "Relógio parado Poly Haven " + result.Count.ToString("D2"),
                        original = renderer, position = p, support = visualSupport, normal = normal, rotation = rotation, size = clockSize });
                }
            }
            if (result.Count == 0) throw new InvalidDataException("No original stopped clock quads found.");
            return result;
        }

        // Collision shells and the old decal quads can sit behind the authored
        // photographic wall finish. Support the new clock on the nearest actual
        // visible wall triangle, rather than guessing another constant offset.
        static Vector3 VisibleWallPoint(Transform root, Vector3 center, Vector3 normal)
        {
            var ray = new Ray(center + normal * .22f, -normal);
            float nearest = .22f;
            var point = center;
            var owned = root.Find(GroupName);
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy
                    || (owned && renderer.transform.IsChildOf(owned)) || !renderer.bounds.IntersectRay(ray)) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                var mesh = filter.sharedMesh; var vertices = mesh.vertices; var materials = renderer.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
                {
                    var mat = materials[sub]; if (!mat) continue;
                    string name = mat.name;
                    if (!name.StartsWith("Abandoned-", StringComparison.Ordinal)
                        && name.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("White", StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("Plaster", StringComparison.OrdinalIgnoreCase) < 0
                        && name != "Reboco envelhecido" && name != "Pintura verde antiga") continue;
                    var indices = mesh.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var a = filter.transform.TransformPoint(vertices[indices[i]]);
                        var b = filter.transform.TransformPoint(vertices[indices[i + 1]]);
                        var c = filter.transform.TransformPoint(vertices[indices[i + 2]]);
                        if (Mathf.Abs(Vector3.Dot(Vector3.Cross(b-a,c-a).normalized, normal)) < .85f) continue;
                        if (RayTriangle(ray, a, b, c, out float distance) && distance >= 0 && distance < nearest)
                        { nearest = distance; point = ray.GetPoint(distance); }
                    }
                }
            }
            return point;
        }

        static bool RayTriangle(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0;
            var ab = b-a; var ac = c-a; var p = Vector3.Cross(ray.direction, ac);
            float det = Vector3.Dot(ab, p); if (Mathf.Abs(det) < 1e-8f) return false;
            float inverse = 1 / det; var t = ray.origin-a; float u = Vector3.Dot(t,p)*inverse;
            if (u < -1e-5f || u > 1.00001f) return false;
            var q = Vector3.Cross(t,ab); float v = Vector3.Dot(ray.direction,q)*inverse;
            if (v < -1e-5f || u+v > 1.00001f) return false;
            distance = Vector3.Dot(ac,q)*inverse; return distance >= 0;
        }

        static Plan PlanFan(Transform ring)
        {
            var room = ring.parent;
            if (!room || !room.name.Contains(" — pavimento ")) throw new InvalidDataException("Original fan is outside its classroom holder.");
            var panels = room.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Sala — painel branco do teto").Select(CubeBounds).ToArray();
            var blockers = room.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Sala — viga", StringComparison.Ordinal)
                || t.name.Contains("luminária") || t.name.Contains("tubo fluorescente") || t.name.Contains("refletor") || t.name.Contains("projetor")
                || t.name.Contains("conduíte do teto")).Select(OriginalBounds).ToArray();
            // The photographic ceiling has narrow side panels and longitudinal
            // recess strips. Prove the union of coplanar panel footprints, then
            // shrink only the prop if an unscaled fixture cannot fit the bay.
            foreach (float scale in new[] { 1f, .82f, .70f, .62f })
            {
                var candidates = new List<Vector3>(); float margin = FanDiameter * scale * .5f + .02f;
                foreach (var level in panels.GroupBy(p => Mathf.RoundToInt(p.min.y * 1000)))
                {
                    var sameLevel = level.ToArray(); var union = sameLevel[0];
                    foreach (var panel in sameLevel.Skip(1)) union.Encapsulate(panel);
                    if (union.size.x < margin * 2 || union.size.z < margin * 2) continue;
                    for (int x = 0; x < 23; x++) for (int z = 0; z < 47; z++)
                    {
                        var top = new Vector3(Mathf.Lerp(union.min.x + margin, union.max.x - margin, x / 22f), sameLevel[0].min.y,
                            Mathf.Lerp(union.min.z + margin, union.max.z - margin, z / 46f));
                        if (!SupportedCeilingFootprint(top, margin, sameLevel)) continue;
                        float height = FanHeight * scale;
                        var bounds = new Bounds(top + Vector3.down * height * .5f,
                            new Vector3(FanDiameter * scale, height - .003f, FanDiameter * scale));
                        if (!blockers.Any(b => b.Intersects(bounds))) candidates.Add(top);
                    }
                }
                if (candidates.Count == 0) continue;
                var chosen = candidates.OrderBy(p => (new Vector2(p.x, p.z) - new Vector2(ring.position.x, ring.position.z)).sqrMagnitude).First();
                return new Plan { id = "ceiling_fan", name = "Ventilador parado Poly Haven — " + room.name,
                    sourceRoom = room, position = chosen - Vector3.up * FanHeight * scale, support = chosen,
                    size = new Vector3(FanDiameter, FanHeight, FanDiameter), normal = Vector3.down, scale = scale };
            }
            throw new InvalidOperationException("No actual ceiling panel bay fits even the minimum stopped fan scale: " + room.name);
        }

        static bool SupportedCeilingFootprint(Vector3 top, float radius, Bounds[] panels)
        {
            foreach (var offset in new[] { Vector2.zero, new Vector2(-radius, -radius), new Vector2(radius, -radius),
                new Vector2(-radius, radius), new Vector2(radius, radius) })
            {
                var p = top + new Vector3(offset.x, 0, offset.y);
                if (!panels.Any(b => Mathf.Abs(b.min.y - top.y) < .002f && p.x >= b.min.x - .001f && p.x <= b.max.x + .001f
                    && p.z >= b.min.z - .001f && p.z <= b.max.z + .001f)) return false;
            }
            return true;
        }

        static Room[] ReadRooms(Transform root, Transform[] rings)
        {
            string path = SchoolMapBuilder.Output + "/School-classroom-photo-report.json";
            var catalogue = JsonUtility.FromJson<SchoolClassroomPhotoPass.Report>(File.ReadAllText(path));
            var doors = root.GetComponentsInChildren<InteractiveDoor>(true); var rooms = new List<Room>();
            foreach (var holder in rings.Select(r => r.parent).Distinct())
            {
                var source = catalogue.rooms.Single(r => holder.name == r.name + " — pavimento " + r.floor);
                var floor = holder.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Sala — piso vinílico gasto");
                var bounds = CubeBounds(floor); float y = bounds.max.y;
                var room = new Room { original = holder, bounds = bounds, floor = y, mainStart = source.aisleStart, mainEnd = source.aisleEnd };
                var direction = room.mainEnd - room.mainStart; direction.y = 0; direction.Normalize(); var cross = new Vector3(direction.z, 0, -direction.x);
                float half = Mathf.Abs(cross.x) > .5f ? bounds.extents.x : bounds.extents.z;
                room.crossStart = bounds.center - cross * half; room.crossEnd = bounds.center + cross * half;
                foreach (var door in doors.Where(d => source.doorIds.Contains(d.persistentId)))
                {
                    if (!door.leafCollider || !door.hinge) throw new InvalidDataException("Native door leaf absent.");
                    var leaf = door.leafCollider; var sweep = leaf.bounds;
                    for (int step = 0; step <= 12; step++) foreach (float x in new[] { -.5f, .5f }) foreach (float z in new[] { -.5f, .5f })
                    {
                        var p = leaf.transform.TransformPoint(leaf.center + Vector3.Scale(leaf.size, new Vector3(x, 0, z)));
                        sweep.Encapsulate(door.hinge.position + Quaternion.AngleAxis(door.openAngle * step / 12f, door.hinge.up) * (p - door.hinge.position));
                    }
                    sweep.Expand(.45f); room.doorSweeps.Add(sweep);
                }
                rooms.Add(room);
            }
            return rooms.OrderBy(r => r.original.name, StringComparer.Ordinal).ToArray();
        }

        static List<Plan> PlanGround(Transform root, Room[] rooms, Report report)
        {
            var accepted = new List<Plan>();
            var clues = root.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b && (b.GetType().Name.Contains("Clue")
                || b.GetType().Name.Contains("Anchor") || b.GetType().Name.Contains("Pickup"))).Select(b => b.transform.position).ToArray();
            foreach (string id in new[] { "metal_trash_can", "plastic_broom" })
            {
                int target = id == "metal_trash_can" ? 5 : 3;
                foreach (var room in rooms)
                {
                    if (accepted.Count(p => p.id == id) >= target) break;
                    float scale = id == "metal_trash_can" ? .66f : .8f;
                    var size = id == "metal_trash_can" ? new Vector3(.6453573f, .9061574f, .5486063f) : new Vector3(.4163926f, 1.1975233f, .5665930f);
                    var candidates = new List<Plan>(); float margin = .47f;
                    foreach (float x in new[] { room.bounds.min.x + margin, room.bounds.max.x - margin })
                        foreach (float z in new[] { room.bounds.min.z + margin, room.bounds.max.z - margin, room.bounds.center.z })
                            candidates.Add(new Plan { id = id, name = id + " — esquecido — " + room.original.name, sourceRoom = room.original,
                                position = new Vector3(x, room.floor + .008f, z), support = new Vector3(x, room.floor, z), size = size, scale = scale });
                    foreach (var candidate in candidates)
                    {
                        var b = WorldBounds(candidate); var padding = new Vector3(.02f, .02f, .02f); b.Expand(padding);
                        if (b.min.x < room.bounds.min.x + .025f || b.max.x > room.bounds.max.x - .025f || b.min.z < room.bounds.min.z + .025f || b.max.z > room.bounds.max.z - .025f
                            || DistanceXZ(b.center, room.mainStart, room.mainEnd) < .7f + Mathf.Max(b.extents.x, b.extents.z)
                            || DistanceXZ(b.center, room.crossStart, room.crossEnd) < .65f + Mathf.Max(b.extents.x, b.extents.z)
                            || room.doorSweeps.Any(s => s.Intersects(b)) || clues.Any(p => HorizontalDistance(p, b.center) < 1.0f && Mathf.Abs(p.y - b.center.y) < 2)
                            || accepted.Any(p => WorldBounds(p).Intersects(b)) || !GroundSupport(root, b, room.floor)
                            || Physics.OverlapBox(b.center, new Vector3(b.extents.x, b.extents.y - .025f, b.extents.z), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                                .Any(c => c.enabled && c.gameObject.activeInHierarchy && c.bounds.max.y > room.floor + .06f))
                        { report.rejectedGroundCandidates++; continue; }
                        accepted.Add(candidate); break;
                    }
                }
            }
            return accepted;
        }

        static PieceRecord Place(Plan plan, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(plan.id));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent); var t = go.transform;
            t.name = plan.name; t.position = plan.position; t.rotation = plan.rotation; t.localScale = Vector3.one * plan.scale;
            go.isStatic = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.receiveShadows = true; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off; }
            return new PieceRecord { id = plan.id, name = plan.name, path = PathOf(t, parent.parent), room = plan.sourceRoom ? plan.sourceRoom.name : "",
                position = plan.position, rotation = plan.rotation, scale = plan.scale, size = plan.size, supportPoint = plan.support, supportNormal = plan.normal };
        }
        static Bounds WorldBounds(Plan p) => new Bounds(p.position + Vector3.up * p.size.y * p.scale * .5f, p.size * p.scale);
        static bool GroundSupport(Transform root, Bounds bounds, float floor)
        {
            foreach (float x in new[] { -.94f, .94f }) foreach (float z in new[] { -.94f, .94f })
            {
                var p = new Vector3(bounds.center.x + bounds.extents.x * x, floor + .12f, bounds.center.z + bounds.extents.z * z);
                if (!Physics.RaycastAll(p, Vector3.down, .25f, ~0, QueryTriggerInteraction.Ignore).Any(h => h.collider.transform.IsChildOf(root)
                    && h.normal.y > .85f && Mathf.Abs(h.point.y - floor) < .035f)) return false;
            }
            return true;
        }
        static float HorizontalDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        static float DistanceXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            p.y = a.y = b.y = 0; var d = b - a; float t = d.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector3.Dot(p - a, d) / d.sqrMagnitude) : 0;
            return Vector3.Distance(p, a + d * t);
        }
        static Bounds CubeBounds(Transform t) => PrimitiveBounds(t, Vector3.one);
        static Bounds OriginalBounds(Transform t)
        {
            var f = t.GetComponent<MeshFilter>();
            if (f && f.sharedMesh) return TransformedBounds(t, f.sharedMesh.bounds);
            bool cylinder = t.name.Contains("conduíte") || t.name.Contains("lente");
            return PrimitiveBounds(t, cylinder ? new Vector3(1, 2, 1) : Vector3.one);
        }
        static Bounds PrimitiveBounds(Transform t, Vector3 size) => TransformedBounds(t, new Bounds(Vector3.zero, size));
        static Bounds TransformedBounds(Transform t, Bounds local)
        {
            var result = new Bounds(t.TransformPoint(local.center), Vector3.zero);
            foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                result.Encapsulate(t.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
            return result;
        }
        static void ValidatePrefab(string id)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(id));
            if (!prefab || prefab.GetComponentsInChildren<Collider>(true).Length != 0 || prefab.GetComponentsInChildren<Light>(true).Length != 0
                || prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 || prefab.GetComponentsInChildren<MeshFilter>(true).Any(f => !f.sharedMesh))
                throw new InvalidDataException("Imported static Poly Haven prefab missing or contains physics/scripts/lights: " + id);
        }
        static string PrefabPath(string id) => AssetFolder + "/" + id + "/" + id + ".prefab";
        static void RequireScene(Transform root)
        {
            if (!root || EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(root) || root.gameObject.scene.path != SchoolIntegrationPass.ScenePath)
                throw new InvalidOperationException("Dress the existing loaded SchoolCEEP scene outside Play.");
        }
        static void CheckAuthorizedPresentation(Transform root, Report report)
        {
            report.authorizedPresentationOnly = false;
            report.presentationNormalizedStoryAfter = null;
            if (report.storyPreserved) return;
            var sessions = root.gameObject.scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<GameSession>(true)).ToArray();
            if (sessions.Length != 1 || !sessions[0].save || !sessions[0].save.flashlight) return;
            var flashlight = sessions[0].save.flashlight;
            var lamp = flashlight.lamp;
            var data = lamp ? lamp.GetComponent<UniversalAdditionalLightData>() : null;
            var serialized = new SerializedObject(flashlight);
            // Only the explicitly approved school flashlight adaptation is
            // normalized. The original full-script hash remains in the report;
            // every other component and serialized byte must match its baseline.
            if (!lamp || !data || data.usePipelineSettings || !flashlight.nearSurfaceDimming
                || Mathf.Abs(serialized.FindProperty("exposureDistance").floatValue - 5.8f) > .000001f
                || Mathf.Abs(serialized.FindProperty("minimumExposure").floatValue - .045f) > .000001f
                || Mathf.Abs(lamp.shadowBias - .015f) > .000001f
                || Mathf.Abs(lamp.shadowNormalBias - .04f) > .000001f
                || Mathf.Abs(lamp.shadowNearPlane - .1f) > .000001f
                || Mathf.Abs(lamp.range - 17f) > .000001f
                || Mathf.Abs(lamp.intensity - 90f) > .000001f) return;
            report.presentationNormalizedStoryAfter = StorySignature(root, flashlight, data);
            report.authorizedPresentationOnly = report.presentationNormalizedStoryAfter == report.storyBefore;
        }

        static string StorySignature(Transform root, FlashlightController approvedFlashlight = null,
            UniversalAdditionalLightData approvedLightData = null)
        {
            var values = root.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(b => b).Select(b => {
                    string json = EditorJsonUtility.ToJson(b);
                    if (b == approvedFlashlight)
                        json = Regex.Replace(json, @",\""nearSurfaceDimming\"":(?:true|false),\""exposureDistance\"":[0-9.eE+-]+,\""minimumExposure\"":[0-9.eE+-]+", "");
                    if (b == approvedLightData)
                        json = json.Replace("\"m_UsePipelineSettings\":false", "\"m_UsePipelineSettings\":true");
                    return PathOf(b.transform, null) + "|" + b.GetType().FullName + "|" + json;
                }).OrderBy(s => s, StringComparer.Ordinal);
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", values)))).Replace("-", "");
        }
        static string PhysicalSignature(Transform root)
        {
            // The later authorized surface pass owns additional corridor boxes.
            // Their own audit checks them; they must not invalidate this pass's
            // snapshot of all pre-existing physics and source room geometry.
            var owned = root.Find(SurfaceGroup);
            var rows = root.GetComponentsInChildren<Collider>(true).Where(c => !owned || !c.transform.IsChildOf(owned))
                .Select(c => PathOf(c.transform, root) + "|" + c.GetType().FullName + "|" + EditorJsonUtility.ToJson(c)
                    + "|" + c.transform.localToWorldMatrix.ToString("R", CultureInfo.InvariantCulture)).OrderBy(s => s, StringComparer.Ordinal);
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", rows)))).Replace("-", "");
        }
        static string PathOf(Transform t, Transform root)
        {
            var names = new Stack<string>(); for (var p = t; p && p != root; p = p.parent) names.Push(p.name); return string.Join("/", names);
        }
    }
}
