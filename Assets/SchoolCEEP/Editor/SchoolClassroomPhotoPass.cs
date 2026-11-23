// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>One common classroom finish, scaled to registered class-room bounds. Run before static batching.</summary>
    public static class SchoolClassroomPhotoPass
    {
        public const int SourceRevision = 4;
        const float Scale = .07055555556f;
        const string Materials = "Assets/SchoolCEEP/Materials/";
        const string Meshes = "Assets/SchoolCEEP/Meshes/";
        const string GroupName = "Salas de aula — padrão das fotografias";
        static readonly HashSet<string> GenericFurniture = new HashSet<string>(new[] {
            "Carteira", "Pé da bancada", "Assento", "Encosto", "Pé da cadeira", "Quadro negro", "Luminária fluorescente" });
        static readonly HashSet<string> MasonryNames = new HashSet<string>(new[] {
            "Parede — pintura inferior", "Parede — reboco", "Peitoril", "Verga da janela", "Verga da porta" });
        static Material white, grey, floor, blue, tablet, steel, dark, green, timber, fluorescent, reflector, curtain;
        static Mesh tabletMesh, seatMesh, backMesh, curtainMesh, fanRing;
        static Transform parent;
        static Report report;
        static HashSet<Collider> nativeBlockers;
        static BoxCollider fitProbe;
        const float PlayerRadius = .32f, PlayerHeight = 1.80f;

        [Serializable] public sealed class RoomResult
        {
            public string id, name, block, boardWall;
            public int floor, seats, beams, fluorescentFixtures, wallFaces, curtains, reservedDoorApproaches, rejectedStructuralFits;
            public bool centerStandingClear, boardVisibleFromRoomCenter;
            public float[] adaptedPdfBox;
            public Vector3 center, aisleStart, aisleEnd;
            public float mainAisleWidthMeters = 1.24f, crossAisleWidthMeters = 1.10f;
            public float ceilingUndersideMeters, beamUndersideMeters;
            public string[] doorIds;
        }
        [Serializable] public sealed class Report
        {
            public int sourceRevision = SourceRevision;
            public bool dimensionsEstimated = true, structuralCollidersPreserved = true;
            public bool doorIdsAndInteractionsPreserved = true, existingLightComponentsPreserved;
            public int classrooms, seats, furnitureColliders, oldFurnitureObjectsRemoved;
            public int wallFaces, ceilingPanels, beams, fixtures, curtains, wallFans, projectors, teacherDesks;
            public int materialAssets, sharedMeshAssets, structuralCollidersBefore, structuralCollidersAfter;
            public int lightsBefore, lightsAfter, rejectedStructuralFits;
            public float playerRadiusMeters = PlayerRadius, playerHeightMeters = PlayerHeight;
            public List<RoomResult> rooms = new List<RoomResult>();
            public string[] photoSources, notes;
        }
        sealed class WallFace
        {
            public Vector3 center, inward;
            public bool vertical;
            public float start, end, bottom, top;
            public string side;
            public float Length => end - start;
        }
        sealed class Reservation
        {
            public string id;
            public Bounds swing;
            public Vector3 threshold, destination, normal, aisleProjection;
        }

        public static Report Apply(Transform architecture, Transform details, SchoolMapBuilder.Layout layout)
        {
            if (!architecture || !details || layout == null) throw new ArgumentNullException("School root, classroom group and adapted layout are required.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build classroom details outside Play mode.");
            var data = JsonUtility.FromJson<SchoolMapBuilder.Rooms>(File.ReadAllText(SchoolMapBuilder.Folder + "/Data/rooms.json"));
            SchoolArchitectureRepairPass.AdaptRoomCatalog(data);
            SchoolCampusConnectionsPass.AdaptRooms(data);
            if (data == null || data.rooms == null) throw new InvalidDataException("Missing registered classroom data.");
            var previous = details.Find(GroupName); if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var group = new GameObject(GroupName).transform; group.SetParent(details, false); parent = group;
            var initialFurniture = new[] { architecture.Find("Mobiliário inicial — 0"), architecture.Find("Mobiliário inicial — 1") }.Where(t => t).ToArray();
            var structural = architecture.GetComponentsInChildren<Collider>(true).Where(c => !c.transform.IsChildOf(details)
                && !initialFurniture.Any(t => c.transform.IsChildOf(t))).ToArray();
            var originalEnabled = structural.Select(c => c.enabled).ToArray();
            nativeBlockers = new HashSet<Collider>(structural.Where(c => c.enabled && !c.isTrigger));
            Physics.SyncTransforms();
            var lights = architecture.GetComponentsInChildren<Light>(true);
            var doors = architecture.GetComponentsInChildren<InteractiveDoor>(true);
            var doorIds = doors.Select(d => d.persistentId).OrderBy(s => s).ToArray();
            report = new Report { structuralCollidersBefore = structural.Length, lightsBefore = lights.Length,
                photoSources = new[] {
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-ab19a86f-c0b8-447b-891e-c221e7bb1313.png",
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-23deb837-5e82-4213-886b-4a387d73d650.png",
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-b8bbe2a5-51a2-4335-b5cb-147ce71d9f80.png" },
                notes = new[] {
                    "Only rooms registered as kind=class receive the common photographic classroom standard on both occupied floors. Laboratories, support, administration, library, workshop and refectory furniture remain distinct.",
                    "Blue plastic chairs with rounded pale tablet tops and tubular steel bases, a green chalkboard with a wooden tray, white/gray walls, blue curtains and worn tan/charcoal vinyl follow the user's photos. Sizes and seat counts are estimates adapted to each registered room.",
                    "White ceiling panels, deep exposed beams, raised narrow ceiling bands, louvered fluorescent housings, a wall fan and a projector preserve the existing building slab/roof elevations. No third floor or taller occupied room is invented.",
                    "Existing classroom Light components are retained. Their generic luminous fixture meshes are replaced; no gameplay, sound or extra point light is added.",
                    "The main and transverse aisles remain empty. Each native doorway reserves its actual swept leaf envelope and a direct walking strip to the room center. Furniture is omitted where those reservations overlap.",
                    "All wall, window, floor and roof colliders are retained. Thin finish, curtain, fan, ceiling and board surfaces have no colliders; each occupied chair/tablet assembly has one conservative collider, plus a teacher desk where clearance allows.",
                    "Photograph people, personal items, brands, labels and notices are not reproduced. Native materials/textures/meshes are created through Unity Editor APIs and are batched by the existing school builder." } };
            SetupMaterialsAndMeshes();
            var fitObject = new GameObject("Classroom furniture fitting probe"); fitProbe = fitObject.AddComponent<BoxCollider>(); fitProbe.isTrigger = true;
            try
            {
                int index = 0;
                foreach (var room in data.rooms.Where(r => r.kind == "class"))
                {
                    float cx = (room.box[0] + room.box[2]) * .5f, cy = (room.box[1] + room.box[3]) * .5f;
                    var region = layout.regions.FirstOrDefault(r => cx >= r.box[0] && cx <= r.box[2] && cy >= r.box[1] && cy <= r.box[3]);
                    if (region == null) throw new InvalidDataException("Classroom is outside registered regions: " + room.name);
                    var block = architecture.Find(region.name); if (!block) throw new InvalidOperationException("Missing classroom block " + region.name);
                    float[] box = SchoolMapBuilder.CorridorBox(region.name, room.level, room.box);
                    BuildRoom(architecture, block, room, box, doors, "school.classroom." + index++);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(fitObject); fitProbe = null; }
            report.classrooms = report.rooms.Count;
            report.structuralCollidersAfter = structural.Count(c => c);
            report.structuralCollidersPreserved = structural.Select((c, i) => c && c.enabled == originalEnabled[i]).All(preserved => preserved)
                && report.structuralCollidersAfter == report.structuralCollidersBefore;
            report.lightsAfter = architecture.GetComponentsInChildren<Light>(true).Length;
            report.existingLightComponentsPreserved = lights.All(l => l) && report.lightsAfter == report.lightsBefore;
            report.doorIdsAndInteractionsPreserved = doorIds.SequenceEqual(architecture.GetComponentsInChildren<InteractiveDoor>(true).Select(d => d.persistentId).OrderBy(s => s));
            if (!report.structuralCollidersPreserved || !report.existingLightComponentsPreserved || !report.doorIdsAndInteractionsPreserved)
                throw new InvalidOperationException("Classroom pass changed a structural collider, native light or persistent doorway.");
            return report;
        }

        static void BuildRoom(Transform architecture, Transform block, SchoolMapBuilder.Room source, float[] box, InteractiveDoor[] doors, string id)
        {
            var previousParent = parent;
            var roomGroup = new GameObject(source.name + " — pavimento " + source.level).transform; roomGroup.SetParent(parent, false); parent = roomGroup;
            float baseY = SchoolMapBuilder.NativeBaseHeight(block.name,source.level);
            Vector3 nw = SchoolMapBuilder.Point(box[0], box[1], baseY), se = SchoolMapBuilder.Point(box[2], box[3], baseY);
            var roomBounds = new Bounds((nw + se) * .5f + Vector3.up * 1.67f, new Vector3(se.x - nw.x, 3.34f, nw.z - se.z));
            float nativeCeiling = block.GetComponentsInChildren<MeshRenderer>().Where(r => (r.name == "Forro" || r.name == "Cobertura (sem acesso previsto)")
                && r.bounds.min.y > baseY + 2.8f).Select(r => r.bounds.min.y).DefaultIfEmpty(baseY + 3.47f).Min();
            float finishUnderside = Mathf.Min(baseY + (source.level == 0 ? 3.42f : 3.21f), nativeCeiling - .06f);
            var result = new RoomResult { id = id, name = source.name, floor = source.level, block = block.name,
                adaptedPdfBox = box.ToArray(), center = new Vector3(roomBounds.center.x, baseY + .04f, roomBounds.center.z),
                ceilingUndersideMeters = finishUnderside,
                beamUndersideMeters = finishUnderside - (source.level == 0 ? .50f : .42f) };
            var reservations = DoorReservations(doors, roomBounds, baseY, result.center);
            result.doorIds = reservations.Select(r => r.id).ToArray(); result.reservedDoorApproaches = reservations.Count;
            RemoveGenericFurniture(architecture, roomBounds, baseY);
            // Flooring is visual only; native slab height/SurfaceType and collision are untouched.
            var floorObject = Cube("Sala — piso vinílico gasto", new Vector3(roomBounds.center.x, baseY + .004f, roomBounds.center.z),
                new Vector3(roomBounds.size.x - .035f, .008f, roomBounds.size.z - .035f), floor);
            SchoolCentralPhotoPass.WorldUV(floorObject.GetComponent<MeshFilter>(), 8f, 8f);
            var faces = WallFinishes(block, roomBounds, baseY, result);
            var boardFace = faces.Where(f => f.top - f.bottom >= 2.18f && f.Length >= 1.80f && VisibleBoardWall(f, result.center, baseY))
                .OrderByDescending(f => Mathf.Min(f.Length, 3.6f) + (f.side == "north" ? .12f : 0))
                .ThenBy(f => Vector3.Distance(f.center, result.center + Vector3.up * 1.82f)).FirstOrDefault();
            // Approximate classroom boxes can extend past their actual CAD partitions.
            // If their perimeter offers no visible wall, mount on an existing interior face.
            if (boardFace == null) boardFace = InteriorBoardFaces(block, roomBounds, result.center, baseY)
                .OrderByDescending(f => Mathf.Min(f.Length, 3.6f) + (f.side == "north" ? .12f : 0))
                .ThenBy(f => Vector3.Distance(f.center, result.center + Vector3.up * 1.82f)).FirstOrDefault();
            Vector3 forward = boardFace == null ? Vector3.forward : -boardFace.inward;
            var rotation = Quaternion.LookRotation(forward); Vector3 right = rotation * Vector3.right;
            result.boardWall = boardFace == null ? "No safe solid wall span; board omitted" : boardFace.side;
            result.boardVisibleFromRoomCenter = boardFace != null;
            Physics.SyncTransforms();
            float aisleDepth = Mathf.Abs(forward.z) > .5f ? roomBounds.size.z : roomBounds.size.x;
            result.aisleStart = NativeAisleEnd(result.center, -forward, aisleDepth * .5f - .50f, baseY);
            result.aisleEnd = NativeAisleEnd(result.center, forward, aisleDepth * .5f - .50f, baseY);
            result.centerStandingClear = NativeStandingClear(result.center, baseY);
            foreach (var reservation in reservations)
            {
                float along = Vector3.Dot(reservation.threshold - result.center, forward);
                along = Mathf.Clamp(along, -Vector3.Distance(result.center, result.aisleStart), Vector3.Distance(result.center, result.aisleEnd));
                reservation.aisleProjection = result.center + forward * along;
            }
            Ceiling(roomBounds, source.level, result, rotation);
            if (boardFace != null) { Chalkboard(boardFace, baseY); WallFan(boardFace, baseY); }
            Curtains(block, roomBounds, baseY, result);
            Furniture(roomBounds, baseY, rotation, right, forward, reservations, result);
            // High fixtures are visual only and never cover the opening/standing path.
            Projector(result.center + forward * -.70f + Vector3.up * (result.ceilingUndersideMeters - baseY - .45f), rotation);
            foreach (var door in doors.Where(d => reservations.Any(r => r.id == d.persistentId)))
                foreach (var renderer in door.GetComponentsInChildren<MeshRenderer>(true))
                    if (!renderer.name.Contains("Handle")) renderer.sharedMaterial = white;
            report.rooms.Add(result); parent = previousParent;
        }

        static List<Reservation> DoorReservations(InteractiveDoor[] doors, Bounds room, float baseY, Vector3 center)
        {
            var list = new List<Reservation>();
            var association = room; association.Expand(new Vector3(1.60f, 0, 1.60f));
            foreach (var door in doors)
            {
                if (!door.leafCollider || !door.hinge) continue;
                var leaf = door.leafCollider; var closed = leaf.bounds;
                if (Mathf.Abs(closed.min.y - baseY) > .18f) continue;
                Bounds swing = closed;
                // Compute the actual scale-aware leaf sweep without rotating any native Transform.
                for (int step = 0; step <= 12; step++) for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = leaf.transform.TransformPoint(leaf.center + Vector3.Scale(leaf.size * .5f, new Vector3(x, 0, z)));
                    corner = door.hinge.position + Quaternion.AngleAxis(door.openAngle * step / 12f, door.hinge.up) * (corner - door.hinge.position);
                    swing.Encapsulate(corner);
                }
                // Room boxes are approximate: a real classroom leaf can be well inside the box, not only on its perimeter.
                bool overlapsRoom = closed.max.x >= association.min.x && closed.min.x <= association.max.x
                    && closed.max.z >= association.min.z && closed.min.z <= association.max.z;
                bool sweepOverlaps = swing.max.x >= association.min.x && swing.min.x <= association.max.x
                    && swing.max.z >= association.min.z && swing.min.z <= association.max.z;
                if (!overlapsRoom && !sweepOverlaps) continue;
                swing.Expand(new Vector3((PlayerRadius + .18f) * 2, 0, (PlayerRadius + .18f) * 2));
                var threshold = new Vector3(closed.center.x, baseY + .04f, closed.center.z);
                Vector3 normal = door.leafCollider.transform.forward; normal.y = 0; normal.Normalize();
                list.Add(new Reservation { id = door.persistentId, swing = swing, threshold = threshold, destination = center, normal = normal });
            }
            return list;
        }
        static void RemoveGenericFurniture(Transform architecture, Bounds room, float baseY)
        {
            for (int floor = 0; floor < 2; floor++)
            {
                var initial = architecture.Find("Mobiliário inicial — " + floor); if (!initial) continue;
                foreach (var renderer in initial.GetComponentsInChildren<MeshRenderer>(true).ToArray())
                {
                    if (!GenericFurniture.Contains(renderer.name)) continue;
                    Vector3 p = renderer.bounds.center;
                    if (p.x < room.min.x || p.x > room.max.x || p.z < room.min.z || p.z > room.max.z || p.y < baseY || p.y > baseY + 3.40f) continue;
                    if (renderer.GetComponent<Light>()) continue;
                    UnityEngine.Object.DestroyImmediate(renderer.gameObject); report.oldFurnitureObjectsRemoved++;
                }
            }
        }
        static List<WallFace> WallFinishes(Transform block, Bounds room, float baseY, RoomResult result)
        {
            var faces = new List<WallFace>();
            foreach (var renderer in block.GetComponentsInChildren<MeshRenderer>().Where(r => MasonryNames.Contains(r.name)).ToArray())
            {
                var b = renderer.bounds;
                if (b.min.y < baseY - .04f || b.max.y > baseY + 3.39f || b.max.y < baseY + .02f) continue;
                bool vertical = b.size.x < b.size.z; Vector3 inward; string side;
                if (vertical && Mathf.Abs(b.center.x - room.min.x) < .32f) { inward = Vector3.right; side = "west"; }
                else if (vertical && Mathf.Abs(b.center.x - room.max.x) < .32f) { inward = Vector3.left; side = "east"; }
                else if (!vertical && Mathf.Abs(b.center.z - room.max.z) < .32f) { inward = Vector3.back; side = "north"; }
                else if (!vertical && Mathf.Abs(b.center.z - room.min.z) < .32f) { inward = Vector3.forward; side = "south"; }
                else continue;
                float start = vertical ? Mathf.Max(b.min.z, room.min.z) : Mathf.Max(b.min.x, room.min.x);
                float end = vertical ? Mathf.Min(b.max.z, room.max.z) : Mathf.Min(b.max.x, room.max.x);
                if (end - start < .025f) continue;
                var center = b.center;
                if (vertical) { center.x = inward.x > 0 ? b.max.x + .009f : b.min.x - .009f; center.z = (start + end) * .5f; }
                else { center.z = inward.z > 0 ? b.max.z + .009f : b.min.z - .009f; center.x = (start + end) * .5f; }
                var face = new WallFace { center = center, inward = inward, vertical = vertical, start = start, end = end,
                    bottom = b.min.y, top = b.max.y, side = side }; faces.Add(face);
                Cube("Sala — reboco interno branco", center, vertical ? new Vector3(.012f, b.size.y, end - start) : new Vector3(end - start, b.size.y, .012f), white);
                float bandTop = Mathf.Min(baseY + 1.32f, b.max.y), bandBottom = Mathf.Max(baseY, b.min.y);
                if (bandTop > bandBottom + .01f)
                {
                    Vector3 p = center + inward * .008f; p.y = (bandTop + bandBottom) * .5f;
                    Cube("Sala — faixa inferior cinza claro", p, vertical ? new Vector3(.008f, bandTop - bandBottom, end - start) : new Vector3(end - start, bandTop - bandBottom, .008f), grey);
                }
                if (b.min.y < baseY + .03f)
                {
                    Vector3 p = center + inward * .020f; p.y = baseY + .052f;
                    Cube("Sala — rodapé marrom", p, vertical ? new Vector3(.018f, .10f, end - start) : new Vector3(end - start, .10f, .018f), timber);
                }
                report.wallFaces++; result.wallFaces++;
            }
            return faces;
        }

        static void Ceiling(Bounds room, int level, RoomResult result, Quaternion rotation)
        {
            float underside = result.ceilingUndersideMeters;
            // A raised narrow band makes the open/deeper ceiling bays legible without cutting the original roof/slab.
            float leftBand = room.min.x + room.size.x * .20f, rightBand = room.max.x - room.size.x * .20f;
            foreach (var interval in new[] { new Vector2(room.min.x + .015f, leftBand), new Vector2(leftBand + .31f, rightBand - .31f), new Vector2(rightBand, room.max.x - .015f) })
            {
                if (interval.y <= interval.x) continue;
                Cube("Sala — painel branco do teto", new Vector3((interval.x + interval.y) * .5f, underside + .023f, room.center.z),
                    new Vector3(interval.y - interval.x, .046f, room.size.z - .035f), white); report.ceilingPanels++;
            }
            foreach (float x in new[] { leftBand + .155f, rightBand - .155f })
            {
                Cube("Sala — faixa recuada do teto", new Vector3(x, underside + .021f, room.center.z), new Vector3(.31f, .015f, room.size.z - .035f), grey);
                Cube("Sala — viga longitudinal aparente", new Vector3(x - .18f, (underside + result.beamUndersideMeters) * .5f, room.center.z),
                    new Vector3(.17f, underside - result.beamUndersideMeters, room.size.z - .02f), white); report.beams++; result.beams++;
            }
            int bays = Mathf.Max(2, Mathf.CeilToInt(room.size.z / 2.15f));
            for (int i = 0; i <= bays; i++)
            {
                float z = Mathf.Lerp(room.min.z + .15f, room.max.z - .15f, (float)i / bays);
                Cube("Sala — viga transversal branca", new Vector3(room.center.x, underside - .12f, z), new Vector3(room.size.x - .03f, .24f, .18f), white);
                report.beams++; result.beams++;
                if (i < bays)
                {
                    float middle = Mathf.Lerp(room.min.z + .15f, room.max.z - .15f, (i + .5f) / bays);
                    Fixture(new Vector3(room.center.x, underside - .17f, middle)); report.fixtures++; result.fluorescentFixtures++;
                }
            }
            Pipe("Sala — conduíte do teto", new Vector3(room.max.x - .19f, underside - .08f, room.min.z + .10f),
                new Vector3(room.max.x - .19f, underside - .08f, room.max.z - .10f), .024f, steel);
        }
        static void Fixture(Vector3 c)
        {
            Cube("Sala — luminária fluorescente comprida", c, new Vector3(1.24f, .10f, .24f), white);
            Cube("Sala — refletor da luminária", c + Vector3.down * .056f, new Vector3(1.17f, .012f, .207f), reflector);
            foreach (float z in new[] { -.064f, .064f })
                Cube("Sala — tubo fluorescente", c + new Vector3(0, -.067f, z), new Vector3(1.10f, .018f, .030f), fluorescent);
            for (int i = 0; i <= 7; i++)
                Cube("Sala — aleta do refletor", c + new Vector3(-.56f + i * .16f, -.082f, 0), new Vector3(.008f, .038f, .206f), reflector);
        }
        static void Chalkboard(WallFace face, float baseY)
        {
            float span = Mathf.Min(3.65f, face.Length - .20f); if (span < 1.40f) return;
            Vector3 c = face.center + face.inward * .044f; c.y = baseY + 1.82f;
            Quaternion q = Quaternion.LookRotation(face.inward);
            OrientedCube("Sala — quadro verde", c, new Vector3(span, 1.10f, .035f), green, q);
            foreach (float side in new[] { -1f, 1f })
            {
                OrientedCube("Sala — moldura lateral do quadro", c + q * new Vector3(side * (span * .5f + .025f), 0, .013f), new Vector3(.052f, 1.15f, .062f), timber, q);
                OrientedCube("Sala — moldura horizontal do quadro", c + Vector3.up * side * .57f, new Vector3(span + .09f, .044f, .058f), timber, q);
            }
            OrientedCube("Sala — bandeja de giz em madeira", c + Vector3.down * .576f + face.inward * .059f,
                new Vector3(span + .09f, .025f, .14f), timber, q);
            for (int i = 0; i < 2; i++)
                OrientedCube("Sala — giz", c + q * new Vector3(-.27f + i * .09f, -.55f, .095f), new Vector3(.07f, .014f, .014f), white, q);
        }
        static void Curtains(Transform block, Bounds room, float baseY, RoomResult result)
        {
            foreach (var window in block.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.StartsWith("Janela —")))
            {
                var b = window.bounds; if (b.center.y < baseY + 1f || b.center.y > baseY + 2.65f) continue;
                bool vertical = b.size.x < b.size.z; Vector3 inward;
                if (vertical && Mathf.Abs(b.center.x - room.min.x) < .4f && b.center.z > room.min.z && b.center.z < room.max.z) inward = Vector3.right;
                else if (vertical && Mathf.Abs(b.center.x - room.max.x) < .4f && b.center.z > room.min.z && b.center.z < room.max.z) inward = Vector3.left;
                else if (!vertical && Mathf.Abs(b.center.z - room.max.z) < .4f && b.center.x > room.min.x && b.center.x < room.max.x) inward = Vector3.back;
                else if (!vertical && Mathf.Abs(b.center.z - room.min.z) < .4f && b.center.x > room.min.x && b.center.x < room.max.x) inward = Vector3.forward;
                else continue;
                float start = vertical ? Mathf.Max(b.min.z, room.min.z + .15f) : Mathf.Max(b.min.x, room.min.x + .15f);
                float end = vertical ? Mathf.Min(b.max.z, room.max.z - .15f) : Mathf.Min(b.max.x, room.max.x - .15f);
                float span = end - start; if (span < .85f) continue;
                var q = Quaternion.LookRotation(inward); var center = b.center + inward * .105f; center.y = baseY + 1.99f;
                if (vertical) center.z = (start + end) * .5f; else center.x = (start + end) * .5f;
                float width = Mathf.Min(.56f, span * .24f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 p = center + q * Vector3.right * side * (span * .5f - width * .5f);
                    var go = MeshObject("Sala — cortina azul plissada", curtainMesh, p, new Vector3(width, 1.14f, 1), curtain, q);
                    go.isStatic = true; report.curtains++; result.curtains++;
                }
                Pipe("Sala — haste da cortina", center + q * new Vector3(-span * .5f, .62f, 0), center + q * new Vector3(span * .5f, .62f, 0), .021f, steel);
            }
        }

        static void Furniture(Bounds room, float baseY, Quaternion rotation, Vector3 right, Vector3 forward, List<Reservation> reservations, RoomResult result)
        {
            float width = Mathf.Abs(right.x) > .5f ? room.size.x : room.size.z;
            float depth = Mathf.Abs(forward.z) > .5f ? room.size.z : room.size.x;
            float left = -width * .5f + .68f, rightEdge = width * .5f - .78f;
            float back = -depth * .5f + .69f, front = depth * .5f - 1.36f;
            int columns = Mathf.Max(1, Mathf.FloorToInt((rightEdge - left) / .99f) + 1), rows = Mathf.Max(1, Mathf.FloorToInt((front - back) / 1.08f) + 1);
            for (int row = 0; row < rows; row++) for (int col = 0; col < columns; col++)
            {
                float u = columns == 1 ? 0 : Mathf.Lerp(left, rightEdge, (float)col / (columns - 1));
                float v = rows == 1 ? 0 : Mathf.Lerp(back, front, (float)row / (rows - 1));
                Vector3 c = new Vector3(room.center.x, baseY, room.center.z) + right * u + forward * v;
                var collision = UnitBounds(c, rotation);
                // Reserve a cross at the room center, plus scale-aware open-door envelopes and actual center approach rays.
                if (Mathf.Abs(u + .12f) < result.mainAisleWidthMeters * .5f + .40f
                    || Mathf.Abs(v + .16f) < result.crossAisleWidthMeters * .5f + .49f || Obstructs(collision, reservations)) continue;
                if (!FitsNativeStructure(c + rotation * new Vector3(.12f, .43f, .16f), new Vector3(.80f, .84f, .98f), rotation, baseY))
                { report.rejectedStructuralFits++; result.rejectedStructuralFits++; continue; }
                StudentSeat(c, rotation); result.seats++; report.seats++;
            }
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 p = new Vector3(room.center.x, baseY, room.center.z) + forward * (depth * .5f - .78f) + right * side * (width * .5f - 1.15f);
                var b = RotatedBounds(p + Vector3.up * .39f, new Vector3(1.14f, .78f, .60f), rotation);
                if (Obstructs(b, reservations)) continue;
                if (!FitsNativeStructure(p + Vector3.up * .39f, new Vector3(1.14f, .78f, .60f), rotation, baseY))
                { report.rejectedStructuralFits++; result.rejectedStructuralFits++; continue; }
                TeacherDesk(p, rotation); break;
            }
        }
        static Bounds UnitBounds(Vector3 c, Quaternion q) => RotatedBounds(c + q * new Vector3(.12f, .43f, .16f), new Vector3(.80f, .84f, .98f), q);
        static Bounds RotatedBounds(Vector3 c, Vector3 size, Quaternion q)
        {
            var b = new Bounds(c, Vector3.zero);
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                b.Encapsulate(c + q * Vector3.Scale(size * .5f, new Vector3(x, y, z)));
            return b;
        }
        static bool Obstructs(Bounds b, List<Reservation> reservations)
        {
            foreach (var r in reservations)
            {
                if (b.Intersects(r.swing)) return true;
                // Reserve both full-depth threshold approaches, then the direct and projected routes to the unchanged room center.
                if (NearSegment(b, r.threshold - r.normal * 2f, r.threshold + r.normal * 2f)
                    || NearSegment(b, r.threshold, r.destination)
                    || NearSegment(b, r.threshold + r.normal * .70f, r.aisleProjection)
                    || NearSegment(b, r.threshold - r.normal * .70f, r.aisleProjection)
                    || NearSegment(b, r.aisleProjection, r.destination)) return true;
            }
            return false;
        }
        static bool NearSegment(Bounds b, Vector3 from, Vector3 to)
        {
            Vector2 p = new Vector2(b.center.x, b.center.z), a = new Vector2(from.x, from.z), c = new Vector2(to.x, to.z);
            Vector2 d = c - a; float t = d.sqrMagnitude < .0001f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude);
            return Vector2.Distance(p, a + d * t) < PlayerRadius + .23f + new Vector2(b.extents.x, b.extents.z).magnitude;
        }
        static bool StructuralBody(Collider c, float baseY) => c && nativeBlockers.Contains(c) && c.bounds.max.y > baseY + .12f
            && c.bounds.min.y < baseY + PlayerHeight + .10f;
        static bool FitsNativeStructure(Vector3 center, Vector3 size, Quaternion rotation, float baseY)
        {
            fitProbe.center = Vector3.zero; fitProbe.size = size;
            var broad = RotatedBounds(center, size, rotation);
            foreach (var other in Physics.OverlapBox(broad.center, broad.extents + Vector3.one * .007f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!StructuralBody(other, baseY)) continue;
                if (Physics.ComputePenetration(fitProbe, center, rotation, other, other.transform.position, other.transform.rotation, out _, out float depth)
                    && depth > .001f) return false;
            }
            return true;
        }
        static Vector3 NativeAisleEnd(Vector3 foot, Vector3 direction, float requestedDistance, float baseY)
        {
            float distance = Mathf.Max(0, requestedDistance);
            foreach (var hit in Physics.SphereCastAll(foot + Vector3.up * .96f, .35f, direction, distance + .12f, ~0, QueryTriggerInteraction.Ignore))
                if (StructuralBody(hit.collider, baseY)) distance = Mathf.Min(distance, Mathf.Max(0, hit.distance - .12f));
            return foot + direction * distance;
        }
        static bool NativeStandingClear(Vector3 foot, float baseY)
        {
            return !Physics.OverlapCapsule(foot + Vector3.up * (PlayerRadius + .025f),
                foot + Vector3.up * (PlayerHeight - PlayerRadius), PlayerRadius, ~0, QueryTriggerInteraction.Ignore).Any(c => StructuralBody(c, baseY));
        }
        static IEnumerable<WallFace> InteriorBoardFaces(Transform block, Bounds room, Vector3 center, float baseY)
        {
            foreach (var collider in block.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger || !MasonryNames.Contains(collider.name)) continue;
                var b = collider.bounds;
                if (b.min.y > baseY + 1.25f || b.max.y < baseY + 2.42f || b.size.y < 2.18f) continue;
                bool vertical = b.size.x < b.size.z;
                float wallAxis = vertical ? b.center.x : b.center.z;
                float roomMin = vertical ? room.min.x : room.min.z, roomMax = vertical ? room.max.x : room.max.z;
                if (wallAxis < roomMin - .32f || wallAxis > roomMax + .32f) continue;
                float start = vertical ? Mathf.Max(b.min.z, room.min.z) : Mathf.Max(b.min.x, room.min.x);
                float end = vertical ? Mathf.Min(b.max.z, room.max.z) : Mathf.Min(b.max.x, room.max.x);
                if (end - start < 1.80f) continue;
                Vector3 inward = vertical ? (center.x > b.center.x ? Vector3.right : Vector3.left)
                    : (center.z > b.center.z ? Vector3.forward : Vector3.back);
                Vector3 position = b.center;
                if (vertical) { position.x = inward.x > 0 ? b.max.x + .009f : b.min.x - .009f; position.z = (start + end) * .5f; }
                else { position.z = inward.z > 0 ? b.max.z + .009f : b.min.z - .009f; position.x = (start + end) * .5f; }
                var face = new WallFace { center = position, inward = inward, vertical = vertical, start = start, end = end,
                    bottom = b.min.y, top = b.max.y, side = vertical ? (inward.x > 0 ? "west" : "east") : (inward.z > 0 ? "south" : "north") };
                if (VisibleBoardWall(face, center, baseY)) yield return face;
            }
        }
        static bool VisibleBoardWall(WallFace face, Vector3 center, float baseY)
        {
            Vector3 from = center, to = face.center + face.inward * .070f; from.y = to.y = baseY + 1.82f;
            // CAD contains parallel masonry skins: the chalkboard must mount on the face actually visible inside the room.
            return !Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore)
                .Any(hit => StructuralBody(hit.collider, baseY));
        }
        static void StudentSeat(Vector3 position, Quaternion q)
        {
            var saved = parent; var unit = new GameObject("Sala — cadeira azul com carteira").transform; unit.SetParent(parent, false); unit.position = position; unit.rotation = q; unit.gameObject.isStatic = true; parent = unit;
            var collider = unit.gameObject.AddComponent<BoxCollider>(); collider.center = new Vector3(.12f, .43f, .16f); collider.size = new Vector3(.80f, .84f, .98f); report.furnitureColliders++;
            LocalMesh("Sala — assento plástico azul arredondado", seatMesh, new Vector3(0, .455f, 0), Vector3.one, blue, Quaternion.identity);
            LocalMesh("Sala — encosto plástico azul", backMesh, new Vector3(0, .725f, -.213f), Vector3.one, blue, Quaternion.Euler(80, 0, 0));
            LocalMesh("Sala — tampo com cantos arredondados", tabletMesh, new Vector3(.17f, .752f, .33f), Vector3.one, tablet, Quaternion.identity);
            // Welded tubular base, rear back supports and the right arm/tablet support.
            foreach (float side in new[] { -.185f, .185f })
            {
                LocalPipe("Sala — pé tubular da cadeira", new Vector3(side, .07f, -.19f), new Vector3(side, .443f, -.12f), .029f, steel);
                LocalPipe("Sala — pé tubular dianteiro", new Vector3(side, .07f, .18f), new Vector3(side, .443f, .12f), .029f, steel);
                LocalPipe("Sala — suporte do encosto", new Vector3(side, .43f, -.17f), new Vector3(side, .86f, -.25f), .024f, steel);
                LocalPipe("Sala — travessa inferior", new Vector3(side, .18f, -.17f), new Vector3(side, .18f, .18f), .024f, steel);
                foreach (float z in new[] { -.19f, .18f }) LocalCube("Sala — ponteira azul", new Vector3(side, .037f, z), new Vector3(.06f, .055f, .08f), blue);
            }
            LocalPipe("Sala — coluna da carteira", new Vector3(.29f, .045f, .28f), new Vector3(.29f, .727f, .28f), .035f, steel);
            LocalPipe("Sala — base estabilizadora da carteira", new Vector3(.08f, .043f, .28f), new Vector3(.48f, .043f, .28f), .036f, steel);
            LocalPipe("Sala — apoio do tampo", new Vector3(-.09f, .719f, .34f), new Vector3(.47f, .719f, .34f), .026f, steel);
            LocalCube("Sala — borda de madeira do tampo", new Vector3(.17f, .729f, .33f), new Vector3(.52f, .010f, .35f), timber);
            parent = saved;
        }
        static void TeacherDesk(Vector3 p, Quaternion q)
        {
            OrientedCube("Sala — mesa do professor", p + Vector3.up * .755f, new Vector3(1.14f, .048f, .60f), tablet, q, true); report.furnitureColliders++; report.teacherDesks++;
            foreach (float x in new[] { -.48f, .48f }) foreach (float z in new[] { -.23f, .23f })
                Pipe("Sala — pé da mesa do professor", p + q * new Vector3(x, .025f, z), p + q * new Vector3(x, .727f, z), .032f, steel);
            OrientedCube("Sala — saia da mesa do professor", p + q * new Vector3(0, .54f, .23f), new Vector3(1.02f, .30f, .027f), grey, q);
        }

        static void WallFan(WallFace face, float baseY)
        {
            Vector3 center = face.center + face.inward * .35f; center.y = baseY + 2.66f;
            var q = Quaternion.LookRotation(face.inward);
            center += q * Vector3.right * Mathf.Min(face.Length * .30f, 1.28f);
            OrientedCube("Sala — suporte do ventilador", center - face.inward * .21f + Vector3.down * .06f, new Vector3(.06f, .15f, .32f), steel, q);
            MeshObject("Sala — aro do ventilador", fanRing, center, Vector3.one, dark, q);
            Cylinder("Sala — motor do ventilador", center - face.inward * .07f, new Vector3(.11f, .055f, .11f), dark, q * Quaternion.Euler(90, 0, 0));
            for (int i = 0; i < 3; i++)
            {
                Quaternion bladeQ = q * Quaternion.Euler(0, 0, i * 120f);
                OrientedCube("Sala — hélice do ventilador", center + bladeQ * new Vector3(.12f, .025f, -.024f), new Vector3(.23f, .10f, .010f), dark, bladeQ);
            }
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                Pipe("Sala — grade do ventilador", center + face.inward * .027f, center + face.inward * .027f + q * new Vector3(Mathf.Sin(angle) * .25f, Mathf.Cos(angle) * .25f, 0), .009f, dark);
            }
            report.wallFans++;
        }
        static void Projector(Vector3 c, Quaternion q)
        {
            OrientedCube("Sala — projetor branco suspenso", c, new Vector3(.29f, .105f, .225f), white, q);
            OrientedCube("Sala — suporte do projetor", c + Vector3.up * .21f, new Vector3(.038f, .34f, .038f), steel, q);
            Cylinder("Sala — lente do projetor", c + q * new Vector3(0, -.009f, .12f), new Vector3(.056f, .016f, .056f), dark, q * Quaternion.Euler(90, 0, 0));
            for (int i = 0; i < 4; i++) OrientedCube("Sala — ventilação do projetor", c + q * new Vector3(.149f, -.016f + i * .012f, 0), new Vector3(.006f, .005f, .105f), dark, q);
            report.projectors++;
        }

        static void SetupMaterialsAndMeshes()
        {
            white = Mat("PhotoClassroomWhite", new Color(.78f, .78f, .75f), .09f);
            grey = Mat("PhotoClassroomLowerGrey", new Color(.60f, .62f, .61f), .11f);
            floor = Mat("PhotoClassroomWornVinyl", Color.white, .24f);
            blue = Mat("PhotoClassroomPlasticBlue", new Color(.027f, .065f, .27f), .38f);
            tablet = Mat("PhotoClassroomTabletGrey", new Color(.69f, .71f, .68f), .31f);
            steel = Mat("PhotoClassroomTubularSteel", new Color(.38f, .42f, .42f), .57f, .72f);
            dark = Mat("PhotoClassroomDarkHardware", new Color(.025f, .029f, .029f), .21f, .28f);
            green = Mat("PhotoClassroomGreenBoard", new Color(.032f, .185f, .13f), .16f);
            timber = Mat("PhotoClassroomWoodEdges", new Color(.32f, .20f, .105f), .22f);
            fluorescent = Mat("PhotoClassroomFluorescent", new Color(.77f, .80f, .75f), .23f);
            fluorescent.EnableKeyword("_EMISSION"); fluorescent.SetColor("_EmissionColor", new Color(.71f, .79f, .76f) * .84f);
            reflector = Mat("PhotoClassroomLightReflector", new Color(.47f, .50f, .49f), .77f, .87f);
            curtain = Mat("PhotoClassroomBlueCurtain", new Color(.027f, .062f, .24f), .05f);
            VinylTexture();
            tabletMesh = PersistMesh("ClassroomPhoto-v1-Tablet", () => RoundedSlab(.65f, .45f, .035f, .072f));
            seatMesh = PersistMesh("ClassroomPhoto-v1-Seat", () => RoundedSlab(.44f, .43f, .041f, .065f));
            backMesh = PersistMesh("ClassroomPhoto-v1-Back", () => RoundedSlab(.425f, .30f, .032f, .065f));
            curtainMesh = PersistMesh("ClassroomPhoto-v1-PleatedCurtain", PleatedCurtain);
            fanRing = PersistMesh("ClassroomPhoto-v1-FanRing", FanRing);
            report.materialAssets = 12; report.sharedMeshAssets = 5;
        }
        static Material Mat(string name, Color color, float smooth, float metallic = 0)
        {
            string path = Materials + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metallic); EditorUtility.SetDirty(m); return m;
        }
        static void VinylTexture()
        {
            string path = Materials + "PhotoClassroomVinylWear.asset"; var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture)
            {
                const int size = 512; texture = new Texture2D(size, size, TextureFormat.RGB24, true) { name = "Worn classroom vinyl, tan and charcoal", wrapMode = TextureWrapMode.Repeat };
                var pixels = new Color[size * size]; var random = new System.Random(710);
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float wear = Mathf.PerlinNoise(x * .0115f + 11.1f, y * .0115f + 3.7f) + Mathf.PerlinNoise(x * .038f, y * .038f) * .085f;
                    bool bare = wear > .62f; float noise = (float)random.NextDouble() * .035f - .0175f;
                    Color c = bare ? new Color(.22f, .24f, .24f) : new Color(.47f, .34f, .235f);
                    float gridX = x / (size * .30f / 8f), gridY = y / (size * .30f / 8f);
                    float edgeX = gridX - Mathf.Floor(gridX), edgeY = gridY - Mathf.Floor(gridY);
                    if (!bare && (edgeX < .028f || edgeY < .028f)) c *= .79f;
                    pixels[y * size + x] = c + new Color(noise, noise, noise);
                }
                texture.SetPixels(pixels); texture.Apply(true, false); AssetDatabase.CreateAsset(texture, path);
            }
            floor.SetTexture("_BaseMap", texture); floor.SetTextureScale("_BaseMap", Vector2.one); EditorUtility.SetDirty(floor);
        }
        static Mesh PersistMesh(string name, Func<Mesh> factory)
        {
            string path = Meshes + name + ".asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh) { mesh = factory(); mesh.name = name; AssetDatabase.CreateAsset(mesh, path); } return mesh;
        }
        static Mesh RoundedSlab(float width, float depth, float thickness, float radius)
        {
            var boundary = new List<Vector2>();
            var corners = new[] { new Vector2(width * .5f - radius, depth * .5f - radius), new Vector2(-width * .5f + radius, depth * .5f - radius), new Vector2(-width * .5f + radius, -depth * .5f + radius), new Vector2(width * .5f - radius, -depth * .5f + radius) };
            for (int corner = 0; corner < 4; corner++) for (int arc = 0; arc <= 6; arc++)
            {
                float angle = (corner * 90f + arc * 15f) * Mathf.Deg2Rad;
                boundary.Add(corners[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            foreach (int face in new[] { 1, -1 })
            {
                int first = vertices.Count; vertices.Add(new Vector3(0, face * thickness * .5f, 0)); uv.Add(new Vector2(.5f, .5f));
                foreach (var p in boundary) { vertices.Add(new Vector3(p.x, face * thickness * .5f, p.y)); uv.Add(new Vector2(p.x / width + .5f, p.y / depth + .5f)); }
                for (int i = 0; i < boundary.Count; i++)
                {
                    int a = first + i + 1, b = first + (i + 1) % boundary.Count + 1;
                    triangles.Add(first); triangles.Add(face == 1 ? b : a); triangles.Add(face == 1 ? a : b);
                }
            }
            for (int i = 0; i < boundary.Count; i++)
            {
                var a = boundary[i]; var b = boundary[(i + 1) % boundary.Count]; int first = vertices.Count;
                vertices.Add(new Vector3(a.x, -thickness * .5f, a.y)); vertices.Add(new Vector3(a.x, thickness * .5f, a.y));
                vertices.Add(new Vector3(b.x, thickness * .5f, b.y)); vertices.Add(new Vector3(b.x, -thickness * .5f, b.y));
                uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.one); uv.Add(Vector2.right);
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh PleatedCurtain()
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            const int segments = 40;
            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments, z = Mathf.Sin(u * Mathf.PI * 16) * .030f;
                vertices.Add(new Vector3(u - .5f, -.5f + Mathf.Sin(u * Mathf.PI * 8) * .006f, z)); uv.Add(new Vector2(u, 0));
                vertices.Add(new Vector3(u - .5f, .5f, z)); uv.Add(new Vector2(u, 1));
                if (i < segments) { int v = i * 2; triangles.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 }); }
            }
            // Separate back-face vertices prevent opposing triangles cancelling the curtain normals.
            int offset = vertices.Count; var frontTriangles = triangles.ToArray(); vertices.AddRange(vertices.ToArray()); uv.AddRange(uv.ToArray());
            for (int i = 0; i < frontTriangles.Length; i += 3)
                triangles.AddRange(new[] { frontTriangles[i + 2] + offset, frontTriangles[i + 1] + offset, frontTriangles[i] + offset });
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh FanRing()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>(); const int around = 36, cross = 6;
            for (int i = 0; i < around; i++) for (int j = 0; j < cross; j++)
            {
                float a = i * Mathf.PI * 2 / around, b = j * Mathf.PI * 2 / cross, radius = .257f + Mathf.Cos(b) * .011f;
                vertices.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, Mathf.Sin(b) * .011f));
            }
            for (int i = 0; i < around; i++) for (int j = 0; j < cross; j++)
            {
                int a = i * cross + j, b = ((i + 1) % around) * cross + j, c = ((i + 1) % around) * cross + (j + 1) % cross, d = i * cross + (j + 1) % cross;
                triangles.AddRange(new[] { a, b, c, a, c, d });
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        static GameObject Cube(string name, Vector3 position, Vector3 size, Material material, bool collision = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.position = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material; go.isStatic = true; if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        static GameObject OrientedCube(string name, Vector3 position, Vector3 size, Material material, Quaternion q, bool collision = false)
        { var go = Cube(name, position, size, material, collision); go.transform.rotation = q; return go; }
        static GameObject MeshObject(string name, Mesh mesh, Vector3 position, Vector3 size, Material material, Quaternion q)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position; go.transform.rotation = q; go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material; go.isStatic = true; return go;
        }
        static void LocalCube(string name, Vector3 local, Vector3 size, Material material)
        { OrientedCube(name, parent.TransformPoint(local), size, material, parent.rotation); }
        static void LocalMesh(string name, Mesh mesh, Vector3 local, Vector3 size, Material material, Quaternion q)
        { MeshObject(name, mesh, parent.TransformPoint(local), size, material, parent.rotation * q); }
        static void LocalPipe(string name, Vector3 a, Vector3 b, float diameter, Material material)
        { Pipe(name, parent.TransformPoint(a), parent.TransformPoint(b), diameter, material); }
        static void Pipe(string name, Vector3 a, Vector3 b, float diameter, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; go.transform.SetParent(parent, false); go.transform.position = (a + b) * .5f; go.transform.up = b - a;
            go.transform.localScale = new Vector3(diameter, Vector3.Distance(a, b) * .5f, diameter); go.GetComponent<Renderer>().sharedMaterial = material;
            go.isStatic = true; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        static void Cylinder(string name, Vector3 c, Vector3 scale, Material material, Quaternion q)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; go.transform.SetParent(parent, false); go.transform.position = c; go.transform.rotation = q; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material; go.isStatic = true; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
