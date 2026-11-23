// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// One-time, deterministic dressing of the finished school with its approved CC0
    /// furniture. It never rebuilds architecture or moves story objects. Source visual
    /// holders and collision envelopes remain disabled in the scene for recovery.
    /// </summary>
    public static class SchoolFurnitureRefinementPass
    {
        public const string GroupName = "Escola — mobiliário abandonado refinado";
        public const string AssetFolder = "Assets/SchoolCEEP/Meshes/Refinement";
        const float MainClearance = .62f, CrossClearance = .55f;
        const string UnitName = "Sala — cadeira azul com carteira";
        const string HolderName = "Mobiliário importado — sala";
        const string ChairPath = "Assets/models/mobiliario/SchoolChair_01/SchoolChair_01.prefab";
        const string DeskPath = "Assets/models/mobiliario/SchoolDesk_01/SchoolDesk_01.prefab";
        const string BoxPath = "Assets/models/decoracao/cardboard_box_01/cardboard_box_01.prefab";

        [Serializable] public sealed class RoomResult
        {
            public string name;
            public int sourceUnits, desks, chairs, fallenDesks, fallenChairs, omittedUnits, boxes, papers, clearanceSamples;
            public float measuredFloor, reservedMainWidth = MainClearance * 2, reservedCrossWidth = CrossClearance * 2;
        }
        [Serializable] public sealed class Report
        {
            public int revision = 1;
            public int rooms, sourceUnits, desks, chairs, fallenDesks, fallenChairs, omittedUnits, boxes, papers;
            public int disabledOriginalEnvelopes, physicalPieces, clearanceSamples, rejectedPlacements;
            public string protectedBefore, protectedAfter;
            public bool protectedArchitectureAndStoryPreserved, allPiecesSupported, allReservedRoutesClear;
            public RoomResult[] roomResults;
            public string[] notes;
        }
        sealed class Room
        {
            public SchoolClassroomPhotoPass.RoomResult source;
            public Transform original;
            public Bounds bounds;
            public Vector3 center, mainStart, mainEnd, crossStart, crossEnd;
            public float floor;
            public readonly List<Reservation> doors = new List<Reservation>();
            public RoomResult result;
        }
        sealed class Reservation { public Bounds sweep; public Vector3 threshold, outside, inside; }
        sealed class Context
        {
            public Transform root, group;
            public Collider[] native;
            public HashSet<Collider> originalEnvelopes;
            public Vector3[] cluePoints;
            public readonly Dictionary<string, Vector3> sourceSizes = new Dictionary<string, Vector3>();
            public readonly List<Room> rooms = new List<Room>();
            public readonly List<Collider> placed = new List<Collider>();
            public Report report = new Report();
            public Mesh paperMesh;
            public Material paperMaterial;
        }

        public static Report Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != SchoolIntegrationPass.ScenePath)
                throw new InvalidOperationException("Open SchoolCEEP before furniture refinement.");
            var root = scene.GetRootGameObjects().Single(g => g.name.StartsWith("CEEP", StringComparison.Ordinal)).transform;
            var report = Apply(root);
            EditorSceneManager.MarkSceneDirty(scene);
            return report; // The caller saves after its complete refinement/audit transaction.
        }

        public static Report Apply(Transform root)
        {
            if (!root || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Refine a finished school outside Play mode.");
            if (root.Find(GroupName)) return Audit(root);
            if (!root.Find(SchoolReadyAssetPass.GroupName))
                throw new InvalidOperationException("Install the already-approved SchoolReadyAssetPass first.");
            var units = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == UnitName).ToArray();
            var holders = root.Find(SchoolReadyAssetPass.GroupName).Cast<Transform>().Where(t => t.name == HolderName).ToArray();
            if (units.Length == 0 || holders.Length != units.Length)
                throw new InvalidOperationException("Every original classroom envelope must have one imported visual holder.");
            var pairings = new Dictionary<Transform, Transform>();
            foreach (var unit in units)
            {
                var matches = holders.Where(h => (h.position - unit.position).sqrMagnitude < .000001f).ToArray();
                if (matches.Length != 1 || pairings.Values.Contains(matches[0]))
                    throw new InvalidOperationException("Classroom source/visual pairing is not unambiguous.");
                pairings.Add(unit, matches[0]);
            }
            var bodies = units.Select(t => t.GetComponent<BoxCollider>()).ToArray();
            if (bodies.Any(c => !c || !c.enabled)) throw new InvalidOperationException("Expected intact original classroom collision envelopes.");
            Physics.SyncTransforms();
            var c = new Context { root = root, native = root.GetComponentsInChildren<Collider>(true),
                originalEnvelopes = new HashSet<Collider>(bodies),
                cluePoints = root.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(b => b && (b.GetType().Name.Contains("Clue") || b.GetType().Name.Contains("Anchor") || b.GetType().Name.Contains("Pickup")))
                    .Select(b => b.transform.position).ToArray() };
            c.report.protectedBefore = ProtectedSignature(root, c.originalEnvelopes, null);
            c.report.sourceUnits = units.Length;
            ReadRooms(c, units);
            // Resolve everything before touching furniture. A missing imported model or
            // room catalogue fails without changing source poses/enabled states.
            foreach (var path in new[] { ChairPath, DeskPath, BoxPath })
            {
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) throw new InvalidDataException("Missing approved prefab: " + path);
                var data = JsonUtility.FromJson<SchoolReadyAssetPass.MeshData>(File.ReadAllText(Path.GetDirectoryName(path) + "/MeshData.json"));
                c.sourceSizes.Add(path, data.size);
            }
            ResolvePaper(c);
            c.group = new GameObject(GroupName).transform; c.group.SetParent(root, false);
            foreach (var body in bodies) body.enabled = false;
            foreach (var holder in holders) holder.gameObject.SetActive(false);
            c.report.disabledOriginalEnvelopes = bodies.Length;
            Physics.SyncTransforms();
            try
            {
                foreach (var room in c.rooms)
                {
                    var roomGroup = new GameObject(room.result.name).transform; roomGroup.SetParent(c.group, false);
                    var rng = new System.Random(StableSeed(room.result.name));
                    var roomUnits = units.Where(u => u.parent == room.original).OrderBy(u => u.position.x).ThenBy(u => u.position.z).ToArray();
                    room.result.sourceUnits = roomUnits.Length;
                    foreach (var unit in roomUnits)
                    {
                        // Empty cells break the uniform grid and reduce both render and
                        // collision load. Remaining positions/angles are visibly irregular.
                        if (rng.NextDouble() < .16) { room.result.omittedUnits++; continue; }
                        bool fallenDesk = rng.NextDouble() < .16, fallenChair = rng.NextDouble() < .38;
                        float yaw = Range(rng, -27, 27);
                        Vector3 original = unit.position;
                        var desk = TryPiece(c, room, roomGroup, DeskPath, new Vector3(.66f, .775f, .48f),
                            original + unit.rotation * new Vector3(.10f, 0, .36f), unit.rotation,
                            fallenDesk ? new Vector3(0, yaw, rng.Next(2) == 0 ? 90 : -90) : new Vector3(0, yaw, 0), rng);
                        if (desk) { room.result.desks++; if (fallenDesk) room.result.fallenDesks++; }
                        var chair = TryPiece(c, room, roomGroup, ChairPath, new Vector3(.47f, .82f, .55f),
                            original + unit.rotation * new Vector3(-.04f, 0, -.09f), unit.rotation * Quaternion.Euler(0, 180, 0),
                            fallenChair ? new Vector3(rng.Next(2) == 0 ? 90 : -90, Range(rng, -70, 70), 0) : new Vector3(0, Range(rng, -55, 55), 0), rng);
                        if (chair) { room.result.chairs++; if (fallenChair) room.result.fallenChairs++; }
                        if (!desk && !chair) room.result.omittedUnits++;
                    }
                    DressPerimeter(c, room, roomGroup, rng);
                }
                Physics.SyncTransforms();
                var report = Finish(c);
                if (!report.protectedArchitectureAndStoryPreserved || !report.allPiecesSupported || !report.allReservedRoutesClear)
                    throw new InvalidOperationException("Refined furniture failed protected-state, support or route clearance proof.");
                if (report.fallenDesks + report.fallenChairs == 0)
                    throw new InvalidOperationException("No safe fallen furniture could be placed; do not claim abandoned-room dressing.");
                return report;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(c.group.gameObject);
                foreach (var body in bodies) if (body) body.enabled = true;
                foreach (var holder in holders) if (holder) holder.gameObject.SetActive(true);
                Physics.SyncTransforms();
                throw;
            }
        }

        static void ReadRooms(Context c, Transform[] units)
        {
            string path = SchoolMapBuilder.Output + "/School-classroom-photo-report.json";
            if (!File.Exists(path)) throw new InvalidDataException("Classroom layout/clearance report is required: " + path);
            var catalogue = JsonUtility.FromJson<SchoolClassroomPhotoPass.Report>(File.ReadAllText(path));
            var doors = c.root.GetComponentsInChildren<InteractiveDoor>(true);
            foreach (var source in catalogue.rooms)
            {
                string name = source.name + " — pavimento " + source.floor;
                var roomUnits = units.Where(u => u.parent && u.parent.name == name).ToArray();
                if (roomUnits.Length == 0) continue;
                if (roomUnits.Select(u => u.parent).Distinct().Count() != 1) throw new InvalidDataException("Duplicate source room: " + name);
                float floor = roomUnits.Select(u => u.position.y).OrderBy(y => y).ElementAt(roomUnits.Length / 2);
                var box = source.adaptedPdfBox;
                var nw = SchoolMapBuilder.Point(box[0], box[1], floor);
                var se = SchoolMapBuilder.Point(box[2], box[3], floor);
                var room = new Room { source = source, original = roomUnits[0].parent, floor = floor,
                    bounds = new Bounds((nw + se) * .5f, new Vector3(se.x - nw.x, 2.4f, nw.z - se.z)),
                    center = new Vector3(source.center.x, floor, source.center.z),
                    mainStart = new Vector3(source.aisleStart.x, floor, source.aisleStart.z),
                    mainEnd = new Vector3(source.aisleEnd.x, floor, source.aisleEnd.z),
                    result = new RoomResult { name = name, measuredFloor = floor } };
                Vector3 direction = (room.mainEnd - room.mainStart).normalized;
                if (direction.sqrMagnitude < .9f) throw new InvalidDataException("Invalid saved classroom aisle: " + name);
                var cross = new Vector3(direction.z, 0, -direction.x);
                float half = Mathf.Abs(cross.x) > .5f ? room.bounds.extents.x : room.bounds.extents.z;
                room.crossStart = room.center - cross * half; room.crossEnd = room.center + cross * half;
                foreach (var door in doors.Where(d => source.doorIds.Contains(d.persistentId)))
                {
                    if (!door.hinge || !door.leafCollider) throw new InvalidDataException("Door has no real leaf: " + door.persistentId);
                    var leaf = door.leafCollider; var sweep = leaf.bounds;
                    for (int step = 0; step <= 18; step++) foreach (float x in new[] { -.5f, .5f }) foreach (float z in new[] { -.5f, .5f })
                    {
                        var p = leaf.transform.TransformPoint(leaf.center + Vector3.Scale(leaf.size, new Vector3(x, 0, z)));
                        sweep.Encapsulate(door.hinge.position + Quaternion.AngleAxis(door.openAngle * step / 18f, door.hinge.up) * (p - door.hinge.position));
                    }
                    sweep.Expand(new Vector3(.28f, 0, .28f));
                    var threshold = new Vector3(leaf.bounds.center.x, floor, leaf.bounds.center.z);
                    var normal = leaf.transform.forward; normal.y = 0; normal.Normalize();
                    room.doors.Add(new Reservation { sweep = sweep, threshold = threshold, outside = threshold - normal * 1.7f, inside = threshold + normal * 1.7f });
                }
                c.rooms.Add(room);
            }
            if (c.rooms.Sum(r => units.Count(u => u.parent == r.original)) != units.Length)
                throw new InvalidOperationException("An original furniture unit is outside the validated classroom catalogue.");
        }

        static Transform TryPiece(Context c, Room room, Transform parent, string path, Vector3 size,
            Vector3 initial, Quaternion basis, Vector3 euler, System.Random rng)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
            var t = go.transform; var raw = c.sourceSizes[path];
            t.localScale = new Vector3(size.x / raw.x, size.y / raw.y, size.z / raw.z);
            t.rotation = basis * Quaternion.Euler(euler); t.position = initial;
            var local = LocalMeshBounds(t);
            var body = go.AddComponent<BoxCollider>(); body.center = local.center; body.size = local.size; body.enabled = false;
            for (int attempt = 0; attempt < 14; attempt++)
            {
                var p = initial + new Vector3(Range(rng, -.46f, .46f), 0, Range(rng, -.46f, .46f));
                if (!TryFloor(c, p, room.floor, out float floor)) { c.report.rejectedPlacements++; continue; }
                t.position = new Vector3(p.x, floor, p.z);
                var bounds = WorldBounds(t, local);
                t.position += Vector3.up * (floor + .012f - bounds.min.y);
                bounds = WorldBounds(t, local);
                if (!WithinRoom(room, bounds) || !ReservedClear(c, room, bounds) || !Supported(c, room, bounds)
                    || !CollisionClear(c, body, bounds)) { c.report.rejectedPlacements++; continue; }
                body.enabled = true; c.placed.Add(body);
                go.name = Path.GetFileNameWithoutExtension(path) + (Mathf.Abs(euler.x) + Mathf.Abs(euler.z) > 45 ? " — tombada" : " — desalinhada");
                go.isStatic = true; Physics.SyncTransforms(); return t;
            }
            UnityEngine.Object.DestroyImmediate(go); return null;
        }

        static void DressPerimeter(Context c, Room room, Transform parent, System.Random rng)
        {
            int boxAttempts = 3 + rng.Next(3);
            for (int i = 0; i < boxAttempts; i++)
            {
                var p = EdgePoint(room, rng, .57f);
                if (TryPiece(c, room, parent, BoxPath, new Vector3(.36f, .30f, .40f), p, Quaternion.identity,
                    new Vector3(0, Range(rng, -180, 180), 0), rng)) room.result.boxes++;
            }
            // Paper is the existing chapter-3 paper surface, re-used as one shared
            // mesh/material. It is visual-only and has no physical obstacle.
            for (int i = 0; i < 14; i++)
            {
                var p = EdgePoint(room, rng, Range(rng, .18f, 1.35f));
                if (!TryFloor(c, p, room.floor, out float floor)) continue;
                var q = Quaternion.Euler(0, Range(rng, -180, 180), 0);
                p.y = floor + .023f;
                var sourceBounds = c.paperMesh.bounds;
                var b = new Bounds(p + q * sourceBounds.center, Vector3.zero);
                foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                    b.Encapsulate(p + q * (sourceBounds.center + Vector3.Scale(sourceBounds.extents, new Vector3(x, 0, z))));
                if (!WithinRoom(room, b) || !ReservedClear(c, room, b) || !Supported(c, room, b)) continue;
                var paper = new GameObject("Papel antigo — espalhado"); paper.transform.SetParent(parent, false);
                paper.transform.position = p; paper.transform.rotation = q;
                paper.AddComponent<MeshFilter>().sharedMesh = c.paperMesh;
                var r = paper.AddComponent<MeshRenderer>(); r.sharedMaterial = c.paperMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = true; paper.isStatic = true;
                room.result.papers++;
            }
        }

        static Vector3 EdgePoint(Room room, System.Random rng, float margin)
        {
            bool xEdge = rng.Next(2) == 0, high = rng.Next(2) == 0;
            return new Vector3(xEdge ? (high ? room.bounds.max.x - margin : room.bounds.min.x + margin)
                : Range(rng, room.bounds.min.x + .35f, room.bounds.max.x - .35f), room.floor,
                !xEdge ? (high ? room.bounds.max.z - margin : room.bounds.min.z + margin)
                : Range(rng, room.bounds.min.z + .35f, room.bounds.max.z - .35f));
        }
        static bool WithinRoom(Room room, Bounds b) => b.min.x >= room.bounds.min.x + .10f && b.max.x <= room.bounds.max.x - .10f
            && b.min.z >= room.bounds.min.z + .10f && b.max.z <= room.bounds.max.z - .10f;
        static bool ReservedClear(Context c, Room room, Bounds b)
        {
            if (NearSegment(b, room.mainStart, room.mainEnd, MainClearance)
                || NearSegment(b, room.crossStart, room.crossEnd, CrossClearance)) return false;
            foreach (var d in room.doors)
                if (b.Intersects(d.sweep) || NearSegment(b, d.outside, d.inside, .64f)
                    || NearSegment(b, d.threshold, room.center, .64f)) return false;
            foreach (var point in c.cluePoints)
                if (Mathf.Abs(point.y - b.center.y) < 2.2f && HorizontalDistance(point, b) < 1.10f) return false;
            return true;
        }
        static float HorizontalDistance(Vector3 p, Bounds b)
        { float x = Mathf.Max(b.min.x - p.x, 0, p.x - b.max.x), z = Mathf.Max(b.min.z - p.z, 0, p.z - b.max.z); return Mathf.Sqrt(x * x + z * z); }
        static bool NearSegment(Bounds b, Vector3 a, Vector3 d, float clearance)
        {
            // Liang-Barsky intersection against an expanded XZ rectangle. A radius
            // reservation remains the same width, independent of furniture yaw.
            float begin = 0, end = 1; var delta = d - a;
            return Clip(-delta.x, a.x - b.min.x + clearance, ref begin, ref end)
                && Clip(delta.x, b.max.x + clearance - a.x, ref begin, ref end)
                && Clip(-delta.z, a.z - b.min.z + clearance, ref begin, ref end)
                && Clip(delta.z, b.max.z + clearance - a.z, ref begin, ref end);
        }
        static bool Clip(float p, float q, ref float begin, ref float end)
        {
            if (Mathf.Abs(p) < .00001f) return q >= 0;
            float r = q / p;
            if (p < 0) { if (r > end) return false; begin = Mathf.Max(begin, r); }
            else { if (r < begin) return false; end = Mathf.Min(end, r); }
            return true;
        }
        static bool IsFloor(Collider body)
        {
            string n = body.name.ToLowerInvariant(); var b = body.bounds;
            return b.size.y < 1f && b.size.x > .5f && b.size.z > .5f
                && (n.Contains("piso") || n.Contains("pavimento") || n.Contains("laje") || n.Contains("patamar") || n.Contains("plataforma") || n.Contains("passeio"))
                && !n.Contains("cobertura") && !n.Contains("forro");
        }
        static bool TryFloor(Context c, Vector3 p, float expected, out float y)
        {
            foreach (var hit in Physics.RaycastAll(new Vector3(p.x, expected + .4f, p.z), Vector3.down, 1.15f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (c.native.Contains(hit.collider) && IsFloor(hit.collider) && hit.normal.y > .97f && Mathf.Abs(hit.point.y - expected) < .22f)
                { y = hit.point.y; return true; }
            y = 0; return false;
        }
        static bool Supported(Context c, Room room, Bounds b)
        {
            if (!TryFloor(c, b.center, room.floor, out float center)) return false;
            foreach (float x in new[] { b.min.x, b.max.x }) foreach (float z in new[] { b.min.z, b.max.z })
                if (!TryFloor(c, new Vector3(x, room.floor, z), room.floor, out float y) || Mathf.Abs(y - center) > .035f) return false;
            return b.min.y >= center + .005f && b.min.y <= center + .030f;
        }
        static bool CollisionClear(Context c, BoxCollider body, Bounds b)
        {
            foreach (var other in Physics.OverlapBox(b.center, b.extents + Vector3.one * .012f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (other == body || !other.enabled || IsFloor(other) || c.originalEnvelopes.Contains(other)) continue;
                if (Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                    other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > .003f) return false;
            }
            return true;
        }
        static Bounds LocalMeshBounds(Transform root)
        {
            bool found = false; var result = new Bounds();
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!f.sharedMesh) continue; var local = f.sharedMesh.bounds;
                foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                {
                    var p = root.InverseTransformPoint(f.transform.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
                    if (!found) { result = new Bounds(p, Vector3.zero); found = true; } else result.Encapsulate(p);
                }
            }
            if (!found) throw new InvalidDataException("Approved prefab has no mesh bounds."); return result;
        }
        static Bounds WorldBounds(Transform root, Bounds local)
        {
            var result = new Bounds(root.TransformPoint(local.center), Vector3.zero);
            foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                result.Encapsulate(root.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
            return result;
        }
        static void ResolvePaper(Context c)
        {
            c.paperMaterial = AssetDatabase.LoadAssetAtPath<Material>(SchoolAbandonmentPass.AssetFolder + "/Papel empoeirado.mat");
            if (!c.paperMaterial) throw new InvalidDataException("The existing chapter-3 paper material is required.");
            string path = AssetFolder + "/ExistingPaperSurface.asset";
            c.paperMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (c.paperMesh) return;
            EnsureFolder(AssetFolder);
            Vector3[] surface = null; Vector2[] surfaceUV = null;
            // Some paper material batches also contain notebook side faces. Select
            // an actual horizontal loose page rather than assuming the first quad.
            foreach (var renderer in c.root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.sharedMaterial == c.paperMaterial))
            {
                var filter = renderer.GetComponent<MeshFilter>(); if (!filter || !filter.sharedMesh) continue;
                var vertices = filter.sharedMesh.vertices; var uv = filter.sharedMesh.uv;
                for (int start = 0; start + 3 < vertices.Length && start + 3 < uv.Length; start += 4)
                {
                    var points = vertices.Skip(start).Take(4).Select(v => renderer.transform.TransformPoint(v)).ToArray();
                    if (points.Max(p => p.y) - points.Min(p => p.y) > .003f) continue;
                    float width = points.Max(p => p.x) - points.Min(p => p.x), depth = points.Max(p => p.z) - points.Min(p => p.z);
                    if (width < .08f || width > .35f || depth < .08f || depth > .40f) continue;
                    var center = points.Aggregate(Vector3.zero, (sum, p) => sum + p) * .25f;
                    surface = points.Select(p => new Vector3(p.x - center.x, 0, p.z - center.z)).ToArray();
                    surfaceUV = uv.Skip(start).Take(4).ToArray(); break;
                }
                if (surface != null) break;
            }
            if (surface == null) throw new InvalidDataException("No horizontal loose page found in the existing chapter-3 paper batches.");
            c.paperMesh = new Mesh { name = "Papel existente — superfície compartilhada" };
            c.paperMesh.vertices = surface; c.paperMesh.uv = surfaceUV; c.paperMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            c.paperMesh.RecalculateNormals(); c.paperMesh.RecalculateBounds();
            AssetDatabase.CreateAsset(c.paperMesh, path); AssetDatabase.SaveAssetIfDirty(c.paperMesh);
        }
        static Report Finish(Context c)
        {
            foreach (var room in c.rooms)
            {
                var group = c.group.Find(room.result.name);
                foreach (var body in group.GetComponentsInChildren<BoxCollider>(true))
                {
                    // collider.bounds is a world AABB of the actual rotated piece.
                    if (!ReservedClear(c, room, body.bounds) || !Supported(c, room, body.bounds))
                        throw new InvalidOperationException("Furniture violates support/reserved route: " + body.name);
                }
                // Sample both center aisles and each door-to-center approach with a
                // standing capsule. Only new furniture is compared here: pre-existing
                // walls/teacher desks are explicitly outside this pass's ownership.
                SampleRoute(c, room, room.mainStart, room.mainEnd);
                SampleRoute(c, room, room.crossStart, room.crossEnd);
                foreach (var door in room.doors) SampleRoute(c, room, door.threshold, room.center);
            }
            var r = c.report; r.roomResults = c.rooms.Select(room => room.result).ToArray(); r.rooms = r.roomResults.Length;
            r.desks = r.roomResults.Sum(v => v.desks); r.chairs = r.roomResults.Sum(v => v.chairs);
            r.fallenDesks = r.roomResults.Sum(v => v.fallenDesks); r.fallenChairs = r.roomResults.Sum(v => v.fallenChairs);
            r.omittedUnits = r.roomResults.Sum(v => v.omittedUnits); r.boxes = r.roomResults.Sum(v => v.boxes); r.papers = r.roomResults.Sum(v => v.papers);
            r.physicalPieces = c.group.GetComponentsInChildren<Collider>(true).Length;
            r.protectedAfter = ProtectedSignature(c.root, c.originalEnvelopes, c.group);
            r.protectedArchitectureAndStoryPreserved = r.protectedBefore == r.protectedAfter;
            r.allPiecesSupported = r.allReservedRoutesClear = true;
            r.notes = new[] {
                "Approved CC0 SchoolDesk_01, SchoolChair_01 and cardboard_box_01 only; shared imported materials/meshes/LODs. No download, imported script, new light or enemy.",
                "Each finished classroom receives a stable seed, missing units, varied yaw/position and supported fallen pieces. Old source holders/collision envelopes remain disabled for scene-backup recovery.",
                "New per-piece BoxColliders are fitted to imported mesh bounds and rotate with their visible furniture. No collider remains at the original grid positions.",
                "Minimum reserved aisle widths 1.24 m/1.10 m; real door-leaf sweep, threshold approaches, door-to-center route and 1.10 m clue clearance excluded. Five-point native-floor support and physical penetration reject unsafe candidates.",
                "Existing story chair/events, persistent IDs, clue positions, lights, door components and non-classroom furniture colliders are protected by serialized signature. This pass does not certify pre-existing map wall gaps.",
                "Extra paper reuses the existing chapter-3 paper quad/material and has no collision. Boxes remain at room edges; no prop is placed in campus corridors." };
            return r;
        }
        static void SampleRoute(Context c, Room room, Vector3 a, Vector3 b)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .24f));
            for (int i = 0; i <= samples; i++)
            {
                var p = Vector3.Lerp(a, b, (float)i / samples); p.y = room.floor;
                foreach (var hit in Physics.OverlapCapsule(p + Vector3.up * .36f, p + Vector3.up * 1.44f, .33f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.transform.IsChildOf(c.group)) throw new InvalidOperationException("New furniture blocks reserved standing route in " + room.result.name);
                c.report.clearanceSamples++; room.result.clearanceSamples++;
            }
        }
        public static Report Audit(Transform root)
        {
            var group = root.Find(GroupName); if (!group) throw new InvalidOperationException("No furniture refinement to audit.");
            var units = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == UnitName).ToArray();
            var bodies = units.Select(u => u.GetComponent<BoxCollider>()).ToArray();
            if (bodies.Any(b => !b || b.enabled)) throw new InvalidOperationException("Old furniture collision envelope was re-enabled.");
            if (root.Find(SchoolReadyAssetPass.GroupName).Cast<Transform>().Any(t => t.name == HolderName && t.gameObject.activeSelf))
                throw new InvalidOperationException("An original grid visual holder was re-enabled.");
            var c = new Context { root = root, group = group, native = root.GetComponentsInChildren<Collider>(true).Where(b => !b.transform.IsChildOf(group)).ToArray(),
                originalEnvelopes = new HashSet<Collider>(bodies),
                cluePoints = root.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b && (b.GetType().Name.Contains("Clue") || b.GetType().Name.Contains("Anchor") || b.GetType().Name.Contains("Pickup"))).Select(b => b.transform.position).ToArray() };
            ReadRooms(c, units);
            c.report.sourceUnits = units.Length; c.report.disabledOriginalEnvelopes = bodies.Length;
            c.report.protectedBefore = ProtectedSignature(root, c.originalEnvelopes, group);
            foreach (var room in c.rooms)
            {
                var roomGroup = group.Find(room.result.name); if (!roomGroup) throw new InvalidOperationException("Missing refined room: " + room.result.name);
                var children = roomGroup.Cast<Transform>().ToArray();
                room.result.sourceUnits = units.Count(u => u.parent == room.original);
                room.result.desks = children.Count(t => t.name.StartsWith("SchoolDesk_01", StringComparison.Ordinal));
                room.result.chairs = children.Count(t => t.name.StartsWith("SchoolChair_01", StringComparison.Ordinal));
                room.result.fallenDesks = children.Count(t => t.name.StartsWith("SchoolDesk_01", StringComparison.Ordinal) && t.name.Contains("tombada"));
                room.result.fallenChairs = children.Count(t => t.name.StartsWith("SchoolChair_01", StringComparison.Ordinal) && t.name.Contains("tombada"));
                room.result.boxes = children.Count(t => t.name.StartsWith("cardboard_box_01", StringComparison.Ordinal));
                room.result.papers = children.Count(t => t.name.StartsWith("Papel antigo", StringComparison.Ordinal));
            }
            Physics.SyncTransforms(); return Finish(c);
        }
        public static Report Audit()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return Audit(scene.GetRootGameObjects().Single(g => g.name.StartsWith("CEEP", StringComparison.Ordinal)).transform);
        }
        /// <summary>Reversible ownership: only this pass's group and its known source
        /// envelopes/visual holders are affected. Architecture and story remain intact.</summary>
        public static string RestoreOriginalFurniture(Transform root)
        {
            if (!root || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Restore outside Play mode.");
            var group = root.Find(GroupName); if (!group) return "No refinement group to restore.";
            foreach (var unit in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == UnitName))
            {
                var body = unit.GetComponent<BoxCollider>();
                if (!body) throw new InvalidOperationException("An original collision envelope is missing.");
            }
            UnityEngine.Object.DestroyImmediate(group.gameObject);
            foreach (var unit in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == UnitName)) unit.GetComponent<BoxCollider>().enabled = true;
            foreach (var holder in root.Find(SchoolReadyAssetPass.GroupName).Cast<Transform>().Where(t => t.name == HolderName)) holder.gameObject.SetActive(true);
            Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            return "Original furniture poses, active visual holders and physical envelopes restored; no map or story rebuild.";
        }
        static string ProtectedSignature(Transform root, HashSet<Collider> envelopes, Transform group)
        {
            var rows = new List<string>();
            foreach (var collider in root.GetComponentsInChildren<Collider>(true).Where(v => !envelopes.Contains(v) && (!group || !v.transform.IsChildOf(group))))
                rows.Add("C|" + collider.GetEntityId() + "|" + EditorJsonUtility.ToJson(collider) + "|" + collider.transform.localToWorldMatrix.ToString("R", CultureInfo.InvariantCulture));
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true).Where(v => v && (!group || !v.transform.IsChildOf(group))))
                rows.Add("S|" + script.GetEntityId() + "|" + EditorJsonUtility.ToJson(script) + "|" + script.transform.localToWorldMatrix.ToString("R", CultureInfo.InvariantCulture));
            foreach (var light in root.GetComponentsInChildren<Light>(true)) rows.Add("L|" + light.GetEntityId() + "|" + EditorJsonUtility.ToJson(light));
            rows.Sort(StringComparer.Ordinal);
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", rows)))).Replace("-", "");
        }
        static void EnsureFolder(string path)
        { if (AssetDatabase.IsValidFolder(path)) return; string parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1)); }
        static int StableSeed(string value) { unchecked { int hash = 709; foreach (char ch in value) hash = hash * 31 + ch; return hash & int.MaxValue; } }
        static float Range(System.Random rng, float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
    }
}
