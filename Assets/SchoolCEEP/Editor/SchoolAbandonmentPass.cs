// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SchoolCEEP.Editor
{
    /// <summary>
    /// Visual decay only. The saved school's colliders resolve the final floors and
    /// wall faces; the adapted room catalogue merely supplies horizontal search areas.
    /// No architecture, interaction, furniture, light or audio component is changed.
    /// </summary>
    public static class SchoolAbandonmentPass
    {
        public const string GroupName = "Escola — abandono do capítulo 3";
        public const string AssetFolder = "Assets/SchoolCEEP/Materials/ChapterThree";
        const float WallOffset = .039f, FloorOffset = .022f, ChunkSize = 24f;

        [Serializable] public sealed class RoomResult
        {
            public string name, block;
            public int floor, wallPatches, floorPatches, papers, leaves, webs;
            public float measuredFloor;
            public bool supported;
        }

        [Serializable] public sealed class Report
        {
            public int sourceRevision = 1;
            public int roomsRequested, roomsDressed, upperRooms, unsupportedRooms, inaccessibleRoomsSkipped;
            public int moldPatches, peelingPatches, cracks, floorGrime, dustyPapers, deadLeaves;
            public int cobwebs, oldBags, notebooks, stoppedClocks, fadedDrawings;
            public int boardedWindows, glassShards, corridorSites, rejectedUnsupportedSurfaces;
            public int triangles, meshBatches, materials, textureAssets, addedColliders;
            public int structuralCollidersBefore, structuralCollidersAfter;
            public bool architecturePreserved, lightsPreserved, doorsPreserved;
            public RoomResult[] rooms;
            public string[] notes;
        }

        sealed class RoomArea
        {
            public SchoolMapBuilder.Room source;
            public string block;
            public Bounds bounds;
            public float expectedFloor;
            public int seed;
        }

        sealed class Bucket
        {
            public Material material;
            public string key;
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> indices = new List<int>();
        }

        sealed class Context
        {
            public Transform architecture, group;
            public HashSet<Collider> native;
            public Collider[] floors;
            public readonly Dictionary<string, Bucket> buckets = new Dictionary<string, Bucket>();
            public readonly List<RoomArea> areas = new List<RoomArea>();
            public readonly List<Vector3> clues = new List<Vector3>();
            public readonly List<RoomResult> results = new List<RoomResult>();
            public readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
            public Report report = new Report();
        }

        public static Report Apply(Transform architecture)
        {
            if (!architecture) throw new ArgumentNullException(nameof(architecture));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply abandonment outside Play mode.");
            if (!Shader.Find("Universal Render Pipeline/Lit"))
                throw new InvalidOperationException("URP Lit shader is required.");

            var data = JsonUtility.FromJson<SchoolMapBuilder.Rooms>(File.ReadAllText(SchoolMapBuilder.Folder + "/Data/rooms.json"));
            var layout = JsonUtility.FromJson<SchoolMapBuilder.Layout>(File.ReadAllText(SchoolMapBuilder.Folder + "/Data/school-layout.json"));
            SchoolArchitectureRepairPass.AdaptRoomCatalog(data);
            SchoolCampusConnectionsPass.AdaptRooms(data);
            SchoolWorkshopPhotoPass.Prepare(layout);
            SchoolCentralEnclosurePass.Prepare(layout);
            SchoolUserPlanPass.Prepare(layout);
            SchoolCampusConnectionsPass.Prepare(layout);
            if (data?.rooms == null || layout?.regions == null)
                throw new InvalidDataException("Adapted school room catalogue is missing.");

            // Only our prior visual group is replaceable. Capture the ready school first.
            Physics.SyncTransforms();
            var previous = architecture.Find(GroupName);
            var colliders = architecture.GetComponentsInChildren<Collider>(true)
                .Where(c => !previous || !c.transform.IsChildOf(previous)).ToArray();
            var bounds = colliders.Select(c => c.bounds).ToArray();
            var enabled = colliders.Select(c => c.enabled).ToArray();
            var transforms = architecture.GetComponentsInChildren<Transform>(true)
                .Where(t => !previous || !t.IsChildOf(previous)).ToArray();
            var matrices = transforms.Select(t => t.localToWorldMatrix).ToArray();
            var lights = architecture.GetComponentsInChildren<Light>(true).ToArray();
            var lightStates = lights.Select(LightSignature).ToArray();
            var doors = architecture.GetComponentsInChildren<InteractiveDoor>(true).ToArray();
            var doorIds = doors.Select(d => d.persistentId).ToArray();

            if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var group = new GameObject(GroupName).transform;
            group.SetParent(architecture, false);
            var cxt = new Context { architecture = architecture, group = group,
                native = new HashSet<Collider>(colliders.Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger)),
                floors = colliders.Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && IsFloor(c)).ToArray() };
            cxt.report.structuralCollidersBefore = colliders.Length;
            cxt.report.roomsRequested = data.rooms.Length;
            Physics.SyncTransforms();

            // Protect the three investigative props and the changing blackboard, even
            // when their renderers are disabled by the current narrative state.
            foreach (var t in transforms)
                if (t.GetComponents<MonoBehaviour>().Any(b => b && b.GetType().Name == "SchoolOpeningClue")
                    || t.name == "Sala 02 — ponto de leitura do quadro" || t.name == "Sala 03 — aparição infantil")
                    cxt.clues.Add(t.position);
            // These fallbacks protect the same anchors if dressing runs before opening setup.
            cxt.clues.Add(SchoolMapBuilder.Point(1303.6f, 555.7f, .018f) + Vector3.back * .50f);
            cxt.clues.Add(new Vector3(-7.7187f, 1.82f, 72.5128f));

            SetupMaterials(cxt);
            int index = 0;
            foreach (var source in data.rooms)
            {
                if (SchoolPlayableBoundsPass.IsDeliberatelyInaccessible(source.name))
                { cxt.report.inaccessibleRoomsSkipped++; continue; }
                float cx = (source.box[0] + source.box[2]) * .5f, cy = (source.box[1] + source.box[3]) * .5f;
                var region = layout.regions.FirstOrDefault(r => cx >= r.box[0] && cx <= r.box[2] && cy >= r.box[1] && cy <= r.box[3]);
                if (region == null) { cxt.report.unsupportedRooms++; continue; }
                var box = SchoolMapBuilder.CorridorBox(region.name, source.level, source.box);
                var nw = SchoolMapBuilder.Point(box[0], box[1]);
                var se = SchoolMapBuilder.Point(box[2], box[3]);
                var area = new RoomArea { source = source, block = region.name,
                    bounds = new Bounds((nw + se) * .5f, new Vector3(Mathf.Abs(se.x - nw.x), 0, Mathf.Abs(nw.z - se.z))),
                    expectedFloor = SchoolMapBuilder.OccupiedFloorHeight(region.name, source.level), seed = 709 + index++ * 53 };
                cxt.areas.Add(area);
            }
            foreach (var area in cxt.areas) DressRoom(cxt, area);
            DressCorridors(cxt, colliders);
            BoardExteriorWindows(cxt, colliders);
            EmitBatches(cxt);

            var report = cxt.report;
            report.rooms = cxt.results.ToArray();
            report.materials = cxt.materials.Count;
            report.addedColliders = group.GetComponentsInChildren<Collider>(true).Length;
            report.structuralCollidersAfter = architecture.GetComponentsInChildren<Collider>(true).Length;
            report.architecturePreserved = report.addedColliders == 0 && colliders.Length == report.structuralCollidersAfter
                && colliders.Select((c, i) => c && c.enabled == enabled[i] && c.bounds.center == bounds[i].center && c.bounds.size == bounds[i].size).All(v => v)
                && transforms.Select((t, i) => t && t.localToWorldMatrix == matrices[i]).All(v => v);
            report.lightsPreserved = lights.Length == architecture.GetComponentsInChildren<Light>(true).Length
                && lights.Select((l, i) => l && LightSignature(l) == lightStates[i]).All(v => v);
            report.doorsPreserved = doors.Length == architecture.GetComponentsInChildren<InteractiveDoor>(true).Length
                && doors.Select((d, i) => d && d.persistentId == doorIds[i]).All(v => v);
            report.notes = new[] {
                "Purely static visual dressing: no colliders, enemies, event scripts, lights or audio are added. The native architecture and existing clues retain their poses and interactions.",
                "Adapted registered room boxes determine horizontal search areas only. Every decorated floor and wall is supported by raycasts against the current saved school's native colliders. Unsupported surfaces are skipped and counted.",
                "Mold, peeling plaster, cracks, floor grime, leaves, papers, webs, bags, notebooks, stopped clocks and faded child drawings use native meshes and 128x128 procedural textures. Upstairs and the old west wing receive more decay.",
                "Geometry is grouped by material and 24m spatial sector, retaining local frustum culling. No per-leaf renderer or giant whole-school material mesh is created.",
                "Only a few confirmed exterior windows receive loose visual boards and shards. Existing glass/window collision is retained. These are scenery, not a new opening or blocked gameplay route.",
                "The deliberately inaccessible Automotive room remains empty. Objects over 3cm high are placed near room edges, away from native obstacles, door thresholds and investigative anchors.",
                "No school electric light or emissive fixture is changed here; the Chapter 3 integration owns the loss of power. This pass neither saves the scene nor loads another scene." };
            if (!report.architecturePreserved || !report.lightsPreserved || !report.doorsPreserved)
                throw new InvalidOperationException("Abandonment changed protected school architecture, lights or interactions.");
            return report;
        }

        static string LightSignature(Light l) => JsonUtility.ToJson(new LightState {
            enabled = l.enabled, intensity = l.intensity, range = l.range, color = l.color, type = (int)l.type,
            position = l.transform.position, rotation = l.transform.rotation });
        [Serializable] sealed class LightState
        { public bool enabled; public float intensity, range; public Color color; public int type; public Vector3 position; public Quaternion rotation; }

        static bool IsFloor(Collider c)
        {
            string n = c.name.ToLowerInvariant();
            return c.bounds.size.y < 1.0f && c.bounds.size.x > .5f && c.bounds.size.z > .5f
                && (n.Contains("piso") || n.Contains("pavimento") || n.Contains("laje")
                    || n.Contains("patamar") || n.Contains("plataforma") || n.Contains("passeio"))
                && !n.Contains("cobertura") && !n.Contains("forro");
        }
        static bool IsWall(Collider c) => c.name.StartsWith("Parede", StringComparison.Ordinal)
            || c.name == "Peitoril" || c.name.StartsWith("Verga", StringComparison.Ordinal);

        static bool TryFloor(Context c, Vector3 xz, float expected, out Vector3 point)
        {
            point = default;
            var origin = new Vector3(xz.x, expected + 1.15f, xz.z);
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 2.50f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (!c.native.Contains(hit.collider) || !IsFloor(hit.collider) || hit.normal.y < .96f
                    || Mathf.Abs(hit.point.y - expected) > .95f) continue;
                point = hit.point + Vector3.up * FloorOffset;
                return true;
            }
            return false;
        }

        static bool AwayFromClues(Context c, Vector3 p, float radius = 1.15f)
            => c.clues.All(q => Mathf.Abs(q.y - p.y) > 2.0f || new Vector2(q.x - p.x, q.z - p.z).sqrMagnitude > radius * radius);

        // Large visual props are confined to the perimeter. Their expanded volumes
        // must be clear; this also excludes cabinets, chair legs and swept door leaves.
        static bool ClearProp(Context c, Vector3 p, Vector3 size)
        {
            if (!AwayFromClues(c, p, 1.45f)) return false;
            var center = p + Vector3.up * (size.y * .5f + .032f);
            foreach (var other in Physics.OverlapBox(center, size * .5f + new Vector3(.12f, .012f, .12f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (c.native.Contains(other) && !IsFloor(other)) return false;
            foreach (var door in c.architecture.GetComponentsInChildren<InteractiveDoor>(true))
                if (Mathf.Abs(door.transform.position.y - p.y) < 2.5f
                    && new Vector2(door.transform.position.x - p.x, door.transform.position.z - p.z).sqrMagnitude < 2.25f) return false;
            return true;
        }

        static void DressRoom(Context c, RoomArea room)
        {
            var rng = new System.Random(room.seed);
            var result = new RoomResult { name = room.source.name, block = room.block, floor = room.source.level };
            c.results.Add(result);
            Vector3 resolved;
            bool supported = TryFloor(c, room.bounds.center, room.expectedFloor, out resolved);
            if (!supported)
                foreach (var p in new[] { Sample(room, .20f, .20f), Sample(room, .80f, .20f), Sample(room, .20f, .80f), Sample(room, .80f, .80f) })
                    if (TryFloor(c, p, room.expectedFloor, out resolved)) { supported = true; break; }
            if (!supported) { c.report.unsupportedRooms++; return; }
            result.supported = true;
            result.measuredFloor = resolved.y - FloorOffset;
            c.report.roomsDressed++;
            if (room.source.level > 0) c.report.upperRooms++;
            bool focal = room.source.name == "Secretaria" || room.source.name == "Sala 02" || room.source.name == "Sala 03";
            bool old = room.source.level > 0 || room.block.StartsWith("west", StringComparison.Ordinal);
            int wallAttempts = focal ? 22 : old ? 16 : 12;
            for (int i = 0; i < wallAttempts; i++)
            {
                int side = i % 4;
                float along = Range(rng, .14f, .86f);
                float height = i % 3 == 0 ? Range(rng, .28f, .85f) : Range(rng, 1.45f, 2.75f);
                float width = Range(rng, .48f, old ? 1.65f : 1.30f), tall = Range(rng, .28f, .65f);
                string material = i % 3 == 0 ? "Mofo" : i % 3 == 1 ? "Reboco descascado" : "Rachaduras";
                if (!TryRoomWall(c, room, result.measuredFloor + height, side, along, new Vector2(width, tall), out var hit))
                { c.report.rejectedUnsupportedSurfaces++; continue; }
                WallQuad(c, material, hit.point + hit.normal * WallOffset, hit.normal, width, tall);
                result.wallPatches++;
                if (material == "Mofo") c.report.moldPatches++;
                else if (material == "Rachaduras") c.report.cracks++;
                else c.report.peelingPatches++;
            }

            int floorCount = focal ? 6 : old ? 4 : 3;
            for (int i = 0; i < floorCount; i++)
            {
                var p = Sample(room, Range(rng, .12f, .88f), Range(rng, .12f, .88f));
                if (!TryFloor(c, p, result.measuredFloor, out p) || !AwayFromClues(c, p)) continue;
                float diameter = Range(rng, .42f, old ? 1.7f : 1.15f);
                if (!FloorQuad(c, "Sujeira no piso", p, diameter, diameter * Range(rng, .5f, .9f), Range(rng, 0, 180))) continue;
                c.report.floorGrime++; result.floorPatches++;
            }
            for (int i = 0; i < (focal ? 5 : 2); i++)
            {
                var p = Sample(room, Range(rng, .12f, .88f), i % 2 == 0 ? .13f : .86f);
                if (!TryFloor(c, p, result.measuredFloor, out p) || !AwayFromClues(c, p)) continue;
                if (!FloorQuad(c, "Papel empoeirado", p + Vector3.up * .002f, .18f, .25f, Range(rng, 0, 180))) continue;
                c.report.dustyPapers++; result.papers++;
            }
            for (int i = 0; i < (old || focal ? 7 : 4); i++)
            {
                var p = Sample(room, Range(rng, .08f, .92f), i % 2 == 0 ? .11f : .89f);
                if (!TryFloor(c, p, result.measuredFloor, out p) || !AwayFromClues(c, p)) continue;
                if (!Leaf(c, p + Vector3.up * .004f, Range(rng, .07f, .15f), Range(rng, 0, 360))) continue;
                c.report.deadLeaves++; result.leaves++;
            }
            if (focal || room.seed % 3 == 0)
                for (int side = 0; side < 4; side++)
                    if (TryRoomWall(c, room, result.measuredFloor + 2.76f, side, .17f, new Vector2(.48f, .48f), out var webHit))
                    { Web(c, webHit.point + webHit.normal * (WallOffset + .004f), webHit.normal); c.report.cobwebs++; result.webs++; break; }

            if (focal || room.seed % 7 == 0)
            {
                var p = Sample(room, .16f, .18f);
                if (TryFloor(c, p, result.measuredFloor, out p) && ClearProp(c, p, new Vector3(.46f, .42f, .28f)))
                { Bag(c, p); c.report.oldBags++; }
            }
            if (focal || room.seed % 4 == 0)
            {
                var p = Sample(room, .81f, .16f);
                if (TryFloor(c, p, result.measuredFloor, out p) && ClearProp(c, p, new Vector3(.28f, .06f, .33f)))
                    for (int i = 0; i < 2; i++)
                    { Notebook(c, p + Vector3.up * i * .025f + Vector3.right * i * .025f, i * 12); c.report.notebooks++; }
            }
            if (focal || room.seed % 11 == 0)
                for (int side = 0; side < 4; side++)
                    if (TryRoomWall(c, room, result.measuredFloor + 2.24f, side, .72f, new Vector2(.34f, .34f), out var clock))
                    { Clock(c, clock.point + clock.normal * (WallOffset + .009f), clock.normal); c.report.stoppedClocks++; break; }
            if (focal || (room.source.kind == "class" && room.seed % 2 == 0))
                for (int side = 0; side < 4; side++)
                    if (TryRoomWall(c, room, result.measuredFloor + 1.55f, side, .24f, new Vector2(.26f, .35f), out var drawing))
                    { WallQuad(c, "Desenho infantil desbotado", drawing.point + drawing.normal * (WallOffset + .006f), drawing.normal, .26f, .35f); c.report.fadedDrawings++; break; }
        }

        static Vector3 Sample(RoomArea room, float x, float z) => new Vector3(
            Mathf.Lerp(room.bounds.min.x, room.bounds.max.x, x), room.expectedFloor, Mathf.Lerp(room.bounds.min.z, room.bounds.max.z, z));

        static bool TryRoomWall(Context c, RoomArea room, float y, int side, float along, Vector2 size, out RaycastHit result)
        {
            Vector3 origin = room.bounds.center; origin.y = y;
            Vector3 dir = side == 0 ? Vector3.left : side == 1 ? Vector3.right : side == 2 ? Vector3.back : Vector3.forward;
            if (side < 2) origin.z = Mathf.Lerp(room.bounds.min.z, room.bounds.max.z, along);
            else origin.x = Mathf.Lerp(room.bounds.min.x, room.bounds.max.x, along);
            float distance = (side < 2 ? room.bounds.size.x : room.bounds.size.z) * .5f + .42f;
            foreach (var hit in Physics.RaycastAll(origin, dir, distance, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (!c.native.Contains(hit.collider)) continue;
                // A desk, door, cupboard or window between the sample and a wall is
                // a genuine occlusion: do not decorate a hidden face behind it.
                if (!IsWall(hit.collider)) { result = default; return false; }
                if (Mathf.Abs(hit.normal.y) > .08f || !AwayFromClues(c, hit.point, 1.45f)) break;
                if (!SupportedWallQuad(c, hit, size)) break;
                result = hit; return true;
            }
            result = default; return false;
        }

        static bool SupportedWallQuad(Context c, RaycastHit center, Vector2 size)
        {
            var u = Vector3.Cross(Vector3.up, center.normal).normalized;
            foreach (float a in new[] { -.5f, .5f }) foreach (float b in new[] { -.5f, .5f })
            {
                var p = center.point + u * (a * size.x) + Vector3.up * (b * size.y);
                if (!Physics.Raycast(p + center.normal * .20f, -center.normal, out var hit, .35f, ~0, QueryTriggerInteraction.Ignore)
                    || !c.native.Contains(hit.collider) || !IsWall(hit.collider)
                    || Vector3.Dot(hit.normal, center.normal) < .99f || Mathf.Abs(Vector3.Dot(hit.point - center.point, center.normal)) > .018f)
                    return false;
            }
            return true;
        }

        static void DressCorridors(Context c, Collider[] colliders)
        {
            int sites = 0;
            foreach (var wall in colliders.Where(w => w.enabled && w.gameObject.activeInHierarchy && !w.isTrigger
                && w.name == "Parede — pintura inferior" && Mathf.Max(w.bounds.size.x, w.bounds.size.z) > 2f)
                .OrderBy(w => w.bounds.center.x).ThenBy(w => w.bounds.center.z).ThenBy(w => w.bounds.center.y))
            {
                var b = wall.bounds;
                var normal = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
                foreach (float sign in new[] { -1f, 1f })
                {
                    var p = b.center + normal * sign * .52f;
                    if (!TryFloor(c, p, b.min.y, out var floor) || !AwayFromClues(c, floor)) continue;
                    bool insideRoom = c.areas.Any(a => Mathf.Abs(a.expectedFloor - floor.y) < 1f
                        && floor.x > a.bounds.min.x && floor.x < a.bounds.max.x && floor.z > a.bounds.min.z && floor.z < a.bounds.max.z);
                    if (insideRoom) continue;
                    if (++sites > 64) { c.report.corridorSites = 64; return; }
                    c.report.corridorSites++;
                    if (FloorQuad(c, "Sujeira no piso", floor, .82f, .41f, sites * 23)) c.report.floorGrime++;
                    if (sites % 2 == 0)
                    { if (FloorQuad(c, "Papel empoeirado", floor + Vector3.up * .003f + Vector3.right * .19f, .16f, .23f, sites * 31)) c.report.dustyPapers++; }
                    var origin = floor + Vector3.up * .54f;
                    if (Physics.Raycast(origin, -normal * sign, out var hit, .85f, ~0, QueryTriggerInteraction.Ignore)
                        && c.native.Contains(hit.collider) && IsWall(hit.collider)
                        && SupportedWallQuad(c, hit, new Vector2(.65f, .45f)))
                    { WallQuad(c, "Mofo", hit.point + hit.normal * WallOffset, hit.normal, .65f, .45f); c.report.moldPatches++; }
                }
            }
        }

        static void BoardExteriorWindows(Context c, Collider[] colliders)
        {
            int candidate = 0;
            foreach (var window in colliders.Where(w => w.enabled && w.gameObject.activeInHierarchy && !w.isTrigger
                && w.name.StartsWith("Janela —", StringComparison.Ordinal)).OrderBy(w => w.bounds.center.x).ThenBy(w => w.bounds.center.z))
            {
                if (c.report.boardedWindows >= 6) return;
                var b = window.bounds;
                if (b.size.y < .8f || b.center.y > 3.6f || !AwayFromClues(c, b.center, 2f)) continue;
                var block = window.transform;
                while (block.parent && block.parent != c.architecture) block = block.parent;
                var floors = c.floors.Where(f => f.transform.IsChildOf(block) && Mathf.Abs(f.bounds.max.y - (b.min.y - 1.05f)) < 1.2f).ToArray();
                if (floors.Length == 0) continue;
                var envelope = floors[0].bounds;
                foreach (var floor in floors.Skip(1)) envelope.Encapsulate(floor.bounds);
                bool vertical = b.size.x < b.size.z;
                float lowDistance = vertical ? Mathf.Abs(b.center.x - envelope.min.x) : Mathf.Abs(b.center.z - envelope.min.z);
                float highDistance = vertical ? Mathf.Abs(b.center.x - envelope.max.x) : Mathf.Abs(b.center.z - envelope.max.z);
                if (Mathf.Min(lowDistance, highDistance) > .48f) continue;
                // Sparse boarded windows leave the existing moonlight and sight lines.
                if (candidate++ % 5 != 0) continue;
                var outward = (vertical ? Vector3.right : Vector3.forward) * (lowDistance < highDistance ? -1f : 1f);
                var center = b.center + outward * .10f;
                float span = Mathf.Min(1.70f, (vertical ? b.size.z : b.size.x) - .08f);
                if (span < .70f) continue;
                for (int i = 0; i < 2; i++)
                {
                    var q = Quaternion.LookRotation(outward) * Quaternion.AngleAxis(i == 0 ? -13f : 10f, Vector3.forward);
                    Box(c, "Madeira apodrecida", center + Vector3.up * (i == 0 ? -.25f : .26f), new Vector3(span, .14f, .046f), q);
                    for (int nail = -1; nail <= 1; nail += 2)
                        Box(c, "Ferrugem", center + Vector3.up * (i == 0 ? -.25f : .26f) + q * new Vector3(nail * (span * .5f - .10f), 0, .026f), new Vector3(.018f, .018f, .008f), q);
                }
                c.report.boardedWindows++;
                var inside = b.center - outward * .45f;
                if (TryFloor(c, inside, envelope.max.y, out var ground) && AwayFromClues(c, ground))
                    for (int i = 0; i < 4; i++)
                    {
                        var p = ground + (vertical ? Vector3.forward : Vector3.right) * (i * .15f - .225f);
                        Triangle(c, "Vidro sujo", p + new Vector3(-.04f, .004f, -.03f), p + new Vector3(0, .004f, .06f), p + new Vector3(.07f, .004f, -.015f));
                        c.report.glassShards++;
                    }
            }
        }

        static void Web(Context c, Vector3 center, Vector3 normal)
        {
            var u = Vector3.Cross(Vector3.up, normal).normalized;
            const int rays = 6;
            for (int i = 0; i < rays; i++)
            {
                float a = i * Mathf.PI * 2 / rays;
                var direction = u * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a);
                Ribbon(c, "Teia opaca", center, center + direction * .23f, normal, .0026f);
                float next = (i + 1) * Mathf.PI * 2 / rays;
                var to = u * Mathf.Cos(next) + Vector3.up * Mathf.Sin(next);
                foreach (float radius in new[] { .10f, .20f })
                    Ribbon(c, "Teia opaca", center + direction * radius, center + to * radius, normal, .0022f);
            }
        }
        static bool Leaf(Context c, Vector3 p, float length, float angle)
            => FloorQuad(c, "Folhas secas", p, length * .50f, length, angle);
        static void Bag(Context c, Vector3 p)
        {
            Box(c, "Tecido esquecido", p + Vector3.up * .19f, new Vector3(.38f, .36f, .23f), Quaternion.identity);
            Box(c, "Tecido esquecido", p + new Vector3(0, .15f, .128f), new Vector3(.27f, .17f, .04f), Quaternion.identity);
            Box(c, "Ferrugem", p + new Vector3(0, .31f, .142f), new Vector3(.27f, .014f, .008f), Quaternion.identity);
            Ribbon(c, "Tecido esquecido", p + new Vector3(-.11f, .38f, 0), p + new Vector3(-.08f, .43f, 0), Vector3.forward, .028f);
            Ribbon(c, "Tecido esquecido", p + new Vector3(-.08f, .43f, 0), p + new Vector3(.08f, .43f, 0), Vector3.forward, .028f);
            Ribbon(c, "Tecido esquecido", p + new Vector3(.08f, .43f, 0), p + new Vector3(.11f, .38f, 0), Vector3.forward, .028f);
        }
        static void Notebook(Context c, Vector3 p, float yaw)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            Box(c, "Papel empoeirado", p + Vector3.up * .013f, new Vector3(.20f, .020f, .27f), q);
            Box(c, "Tecido esquecido", p + Vector3.up * .025f, new Vector3(.216f, .006f, .278f), q);
            Box(c, "Madeira apodrecida", p + q * new Vector3(-.107f, .014f, 0), new Vector3(.014f, .028f, .278f), q);
        }
        static void Clock(Context c, Vector3 p, Vector3 normal)
        {
            WallQuad(c, "Relógio parado", p, normal, .32f, .32f);
            var right = Vector3.Cross(Vector3.up, normal).normalized;
            Ribbon(c, "Ferrugem", p + normal * .004f, p + normal * .004f + right * .082f + Vector3.up * .051f, normal, .008f);
            Ribbon(c, "Ferrugem", p + normal * .005f, p + normal * .005f - right * .043f + Vector3.up * .055f, normal, .013f);
        }

        static bool FloorQuad(Context c, string mat, Vector3 p, float width, float depth, float yaw)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            foreach (float x in new[] { -.5f, .5f }) foreach (float z in new[] { -.5f, .5f })
            {
                var corner = p + q * new Vector3(x * width, 0, z * depth);
                if (!TryFloor(c, corner, p.y - FloorOffset, out var support) || Mathf.Abs(support.y - p.y) > .025f)
                { c.report.rejectedUnsupportedSurfaces++; return false; }
            }
            Quad(c, mat, p, q * Vector3.right * width, q * Vector3.back * depth);
            return true;
        }
        static void WallQuad(Context c, string mat, Vector3 p, Vector3 normal, float width, float height)
            => Quad(c, mat, p, Vector3.Cross(Vector3.up, normal).normalized * width, Vector3.up * height);
        static void Ribbon(Context c, string mat, Vector3 from, Vector3 to, Vector3 normal, float width)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < .000001f) return;
            Quad(c, mat, (from + to) * .5f, delta, Vector3.Cross(normal, delta).normalized * width);
        }
        static void Box(Context c, string mat, Vector3 center, Vector3 size, Quaternion q)
        {
            Quad(c, mat, center + q * Vector3.forward * size.z * .5f, q * Vector3.right * size.x, q * Vector3.up * size.y);
            Quad(c, mat, center - q * Vector3.forward * size.z * .5f, q * Vector3.left * size.x, q * Vector3.up * size.y);
            Quad(c, mat, center + q * Vector3.right * size.x * .5f, q * Vector3.back * size.z, q * Vector3.up * size.y);
            Quad(c, mat, center - q * Vector3.right * size.x * .5f, q * Vector3.forward * size.z, q * Vector3.up * size.y);
            Quad(c, mat, center + q * Vector3.up * size.y * .5f, q * Vector3.right * size.x, q * Vector3.back * size.z);
            Quad(c, mat, center - q * Vector3.up * size.y * .5f, q * Vector3.right * size.x, q * Vector3.forward * size.z);
        }
        static Bucket Batch(Context c, string material, Vector3 center)
        {
            string key = material + "_" + Mathf.FloorToInt(center.x / ChunkSize) + "_" + Mathf.FloorToInt(center.z / ChunkSize) + "_" + Mathf.FloorToInt(center.y / 3.5f);
            if (!c.buckets.TryGetValue(key, out var bucket))
            { bucket = new Bucket { key = key, material = c.materials[material] }; c.buckets.Add(key, bucket); }
            return bucket;
        }
        static void Quad(Context c, string material, Vector3 center, Vector3 u, Vector3 v)
        {
            var b = Batch(c, material, center); int offset = b.vertices.Count;
            var inverse = c.group.worldToLocalMatrix;
            b.vertices.Add(inverse.MultiplyPoint3x4(center - u * .5f - v * .5f));
            b.vertices.Add(inverse.MultiplyPoint3x4(center + u * .5f - v * .5f));
            b.vertices.Add(inverse.MultiplyPoint3x4(center + u * .5f + v * .5f));
            b.vertices.Add(inverse.MultiplyPoint3x4(center - u * .5f + v * .5f));
            b.uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
            b.indices.AddRange(new[] { offset, offset + 1, offset + 2, offset, offset + 2, offset + 3 });
        }
        static void Triangle(Context c, string material, Vector3 a, Vector3 b, Vector3 d)
        {
            var bucket = Batch(c, material, (a + b + d) / 3); int i = bucket.vertices.Count;
            var inverse = c.group.worldToLocalMatrix;
            bucket.vertices.Add(inverse.MultiplyPoint3x4(a)); bucket.vertices.Add(inverse.MultiplyPoint3x4(b)); bucket.vertices.Add(inverse.MultiplyPoint3x4(d));
            bucket.uv.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.right }); bucket.indices.AddRange(new[] { i, i + 1, i + 2 });
        }

        static void EmitBatches(Context c)
        {
            EnsureFolder(AssetFolder + "/Meshes");
            foreach (var b in c.buckets.Values.OrderBy(b => b.key))
            {
                string path = AssetFolder + "/Meshes/" + b.key + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!mesh) { mesh = new Mesh { name = "Abandono — " + b.key }; AssetDatabase.CreateAsset(mesh, path); }
                else mesh.Clear();
                if (b.vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(b.vertices); mesh.SetUVs(0, b.uv); mesh.SetTriangles(b.indices, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh);
                var go = new GameObject("Abandono — " + b.key);
                go.transform.SetParent(c.group, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = b.material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                go.isStatic = true;
                c.report.meshBatches++; c.report.triangles += b.indices.Count / 3;
            }
        }

        static void SetupMaterials(Context c)
        {
            EnsureFolder(AssetFolder);
            foreach (var name in new[] { "Mofo", "Reboco descascado", "Rachaduras", "Sujeira no piso", "Papel empoeirado", "Desenho infantil desbotado", "Relógio parado",
                "Folhas secas", "Madeira apodrecida", "Ferrugem", "Tecido esquecido", "Teia opaca", "Vidro sujo" })
            {
                string path = AssetFolder + "/" + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(mat, path); }
                mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Smoothness", name == "Vidro sujo" ? .32f : .04f);
                mat.SetFloat("_Metallic", name == "Ferrugem" ? .32f : 0f);
                mat.SetFloat("_Cull", (float)CullMode.Off); mat.SetFloat("_Surface", 0);
                mat.SetFloat("_ZWrite", 1); mat.SetFloat("_SrcBlend", (float)BlendMode.One); mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.DisableKeyword("_EMISSION");
                bool clip = name == "Mofo" || name == "Reboco descascado" || name == "Rachaduras" || name == "Sujeira no piso" || name == "Relógio parado" || name == "Folhas secas";
                mat.SetFloat("_AlphaClip", clip ? 1 : 0); mat.SetFloat("_Cutoff", .14f);
                if (clip) mat.EnableKeyword("_ALPHATEST_ON"); else mat.DisableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", clip ? "TransparentCutout" : "Opaque"); mat.renderQueue = clip ? 2450 : 2000;
                mat.SetShaderPassEnabled("ShadowCaster", false);
                var texture = Texture(name);
                mat.SetTexture("_BaseMap", texture);
                // Stains blend into the original surface; opaque cutouts read like loose foliage.
                if(name=="Mofo"||name=="Sujeira no piso")
                {
                    mat.SetColor("_BaseColor",new Color(.82f,.80f,.74f,.62f));
                    mat.SetFloat("_Surface",1);mat.SetFloat("_AlphaClip",0);mat.SetFloat("_ZWrite",0);
                    mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                    mat.DisableKeyword("_ALPHATEST_ON");mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.SetOverrideTag("RenderType","Transparent");mat.renderQueue=3000;
                }
                EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);
                c.materials.Add(name, mat); c.report.textureAssets++;
            }
        }

        static Texture2D Texture(string kind)
        {
            string path = AssetFolder + "/" + kind + " — 128.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture) { texture = new Texture2D(128, 128, TextureFormat.RGBA32, false) { name = kind + " — 128" }; AssetDatabase.CreateAsset(texture, path); }
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float u = x / 127f, v = y / 127f;
                float noise = Mathf.PerlinNoise(u * 14.7f + 19.3f, v * 17.2f + 5.8f);
                float broad = Mathf.PerlinNoise(u * 5.9f + 2.3f, v * 6.7f + 27f);
                float radial = new Vector2((u - .5f) * 1.7f, (v - .5f) * 2).magnitude;
                Color color;
                switch (kind)
                {
                    case "Mofo": color = Color.Lerp(new Color(.105f, .135f, .075f), new Color(.28f, .31f, .19f), noise); color.a = Mathf.Clamp01((broad - radial * .56f - .12f) * 4); break;
                    case "Reboco descascado": color = Color.Lerp(new Color(.37f, .35f, .29f), new Color(.66f, .64f, .53f), noise); color.a = broad > .43f && radial < .84f ? .85f : 0; break;
                    case "Sujeira no piso": color = Color.Lerp(new Color(.13f, .115f, .075f), new Color(.29f, .25f, .18f), noise); color.a = Mathf.Clamp01((.70f - radial + (broad - .5f) * .65f) * 2); break;
                    case "Rachaduras":
                        float line = Mathf.Abs(u - (.47f + .16f * Mathf.Sin(v * 13) + .035f * Mathf.Sin(v * 53)));
                        float branch = Mathf.Abs(v - (.29f + .40f * u + .026f * Mathf.Sin(u * 42)));
                        color = new Color(.10f, .10f, .075f, (line < .011f || (u > .45f && branch < .007f)) && v > .07f && v < .92f ? .95f : 0); break;
                    case "Papel empoeirado":
                    case "Desenho infantil desbotado":
                        color = Color.Lerp(new Color(.43f, .40f, .30f), new Color(.67f, .62f, .48f), noise);
                        if (kind == "Papel empoeirado" && u > .13f && u < .88f && v > .14f && v < .88f && (y % 13 == 0)) color *= .66f;
                        if (kind == "Desenho infantil desbotado") color = ChildDrawing(color, u, v);
                        break;
                    case "Relógio parado":
                        float radius = new Vector2(u - .5f, v - .5f).magnitude;
                        color = Color.Lerp(new Color(.38f, .35f, .25f), new Color(.60f, .56f, .44f), noise);
                        if (radius > .43f) color = new Color(.12f, .115f, .08f);
                        if (radius > .485f) color.a = 0;
                        else if (radius > .35f && radius < .405f && Mathf.Abs(Mathf.Sin(Mathf.Atan2(v - .5f, u - .5f) * 6)) < .10f) color *= .22f;
                        break;
                    case "Madeira apodrecida": color = Color.Lerp(new Color(.17f, .145f, .095f), new Color(.39f, .34f, .24f), Mathf.PerlinNoise(u * 3 + 1, v * 47 + 6)); break;
                    case "Ferrugem": color = Color.Lerp(new Color(.16f, .125f, .09f), new Color(.35f, .22f, .12f), noise); break;
                    case "Tecido esquecido": color = Color.Lerp(new Color(.13f, .16f, .13f), new Color(.25f, .28f, .22f), noise); if ((x % 4 == 0) || (y % 4 == 0)) color *= .83f; break;
                    case "Folhas secas":
                        color = Color.Lerp(new Color(.25f, .16f, .075f), new Color(.46f, .31f, .14f), noise);
                        if (Mathf.Abs(u - .5f) < .035f) color *= .70f;
                        color.a = Mathf.Abs(u - .5f) < Mathf.Sin(v * Mathf.PI) * (.46f + (noise - .5f) * .08f) ? 1 : 0;
                        break;
                    case "Teia opaca": color = new Color(.42f, .43f, .38f); break;
                    default: color = Color.Lerp(new Color(.15f, .22f, .21f), new Color(.29f, .37f, .33f), noise); break;
                }
                pixels[y * 128 + x] = color;
            }
            texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels(pixels); texture.Apply(false, false);
            EditorUtility.SetDirty(texture); AssetDatabase.SaveAssetIfDirty(texture);
            return texture;
        }
        static Color ChildDrawing(Color paper, float u, float v)
        {
            var p = new Vector2(u, v);
            var brown = new Color(.34f, .24f, .17f);
            bool house = Segment(p, new Vector2(.20f, .24f), new Vector2(.20f, .52f), .009f)
                || Segment(p, new Vector2(.20f, .52f), new Vector2(.39f, .69f), .009f)
                || Segment(p, new Vector2(.39f, .69f), new Vector2(.57f, .52f), .009f)
                || Segment(p, new Vector2(.57f, .52f), new Vector2(.57f, .24f), .009f)
                || Segment(p, new Vector2(.20f, .24f), new Vector2(.57f, .24f), .009f);
            bool child = Mathf.Abs(Vector2.Distance(p, new Vector2(.73f, .51f)) - .04f) < .008f
                || Segment(p, new Vector2(.73f, .47f), new Vector2(.73f, .30f), .008f)
                || Segment(p, new Vector2(.65f, .43f), new Vector2(.82f, .43f), .008f)
                || Segment(p, new Vector2(.73f, .30f), new Vector2(.66f, .21f), .008f)
                || Segment(p, new Vector2(.73f, .30f), new Vector2(.80f, .21f), .008f);
            if (house || child) return Color.Lerp(paper, brown, .72f);
            if (Vector2.Distance(p, new Vector2(.77f, .79f)) < .055f) return Color.Lerp(paper, new Color(.52f, .38f, .12f), .60f);
            return paper;
        }
        static bool Segment(Vector2 p, Vector2 a, Vector2 b, float thickness)
        {
            var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).sqrMagnitude < thickness * thickness;
        }
        static float Range(System.Random random, float low, float high) => Mathf.Lerp(low, high, (float)random.NextDouble());
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int index = path.LastIndexOf('/'); string parent = path.Substring(0, index);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(index + 1));
        }
    }
}
