// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    /// <summary>Photographed grade connection at the western end of the long main walkway.</summary>
    public static class SchoolWestBlockAccessPass
    {
        public const int SourceRevision = 7;
        public const float HighFloor = .60f, LowFloor = -.60f;
        public const string GroupName = "Blocos oeste — rampa e escada de ligação";
        public const string ReportName = "School-west-block-access-report.json";
        public const string RoofMaterialPath = "Assets/SchoolCEEP/Materials/WestBlockAccessRoofGrey.mat";
        const float Scale = .07055555556f, FootClearance = .05f, Spacing = .10f;
        const string MeshRoot = "Assets/SchoolCEEP/Meshes/WestBlockAccess";
        const float RunWest = 385.96f, RunEast = 470.92f;
        const float NorthLane = 767.42f, MiddleLane = 788.72f, SouthLane = 810.02f;
        public const float StairAdvanceMeters = .60f;
        const float StairShift = StairAdvanceMeters / Scale;
        const float StairLeft = 505f + StairShift, StairRight = 553.12f + StairShift;
        const float ApproachAxis = 845.48f - 1.8f / Scale;
        static Transform group;
        static Material concrete, blue, roofing, lamp;
        static string meshFolder;
        static BuildReport report;
        static readonly List<SchoolCirculation.Route> routes = new List<SchoolCirculation.Route>();
        static readonly List<SchoolCirculation.FlightReport> flights = new List<SchoolCirculation.FlightReport>();
        static readonly List<Vector3> postBases = new List<Vector3>();

        [Serializable] public sealed class BuildReport
        {
            public int sourceRevision = SourceRevision;
            public string highBlock = "west_south", lowBlock = "west_north", meshFolder, roofMaterial;
            public float highFloor = HighFloor, lowFloor = LowFloor, estimatedRise = HighFloor-LowFloor;
            public bool dimensionsEstimated = true, identitiesPreserved, nativeBlockCollidersPreserved;
            public bool nativeDoorInteractionsPreserved, canopySinglePitch = true;
            public int raisedBlockTransforms, raisedFurnitureTransforms, raisedPhotoTransforms;
            public int rampMeshColliders, solidTreads, slabColliders, edgeGuardColliders, roofColliders, roofSupports;
            public int railRuns, decorativeRoofFixtures;
            public int nativeHighCollidersBefore, nativeHighCollidersAfter;
            public float rampRunMeters, rampLaneWidthMeters, inclinePercent, maximumRiser;
            public float roofNorthHeight = 3.25f, roofSouthHeight = 4.50f;
            public string[] doorIdsBefore, doorIdsAfter, raisedDoorIds, shiftedPhotoObjects, photoSources, notes;
            public SchoolCirculation.FlightReport[] flights;
            public SchoolCirculation.Route[] routes;
            public SchoolValidationPass.WalkProbe[] probes;
        }

        public static BuildReport LastReport { get; private set; }

        public static BuildReport Apply(Transform architecture, Transform parent)
        {
            if (!architecture || !parent) throw new ArgumentNullException("Provide school architecture and an empty access group.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build western access outside Play mode.");
            if (parent.childCount != 0) throw new InvalidOperationException("Build western access once on a freshly built group.");
            var high = architecture.Find("west_south");
            var low = architecture.Find("west_north");
            if (!high || !low) throw new InvalidOperationException("Missing registered western blocks.");
            if (Mathf.Abs(high.position.y) > .01f) throw new InvalidOperationException("Western high block has already been raised.");
            group = parent; routes.Clear(); flights.Clear(); postBases.Clear();
            var doors = architecture.GetComponentsInChildren<InteractiveDoor>(true);
            var ids = doors.Select(d => d.persistentId).OrderBy(s => s).ToArray();
            var nativeColliders = high.GetComponentsInChildren<Collider>(true);
            var doorState = doors.Select(d => new DoorState(d)).ToArray();
            report = new BuildReport {
                nativeHighCollidersBefore = nativeColliders.Length, doorIdsBefore = ids,
                rampRunMeters = (RunEast - RunWest) * Scale,
                rampLaneWidthMeters = (778.04f - 756.8f) * Scale,
                inclinePercent = ((HighFloor-LowFloor)/3f) / ((RunEast - RunWest) * Scale) * 100f,
                maximumRiser = (HighFloor-LowFloor) / 8f,
                raisedDoorIds = high.GetComponentsInChildren<InteractiveDoor>(true).Select(d => d.persistentId).OrderBy(s => s).ToArray(),
                photoSources = new[] {
                    "work/SchoolCEEP/Opposite-court-block-reference/Reference-3.png",
                    "work/SchoolCEEP/Opposite-court-block-reference/Reference-4.png",
                    "work/SchoolCEEP/Opposite-court-block-reference/Reference-5.png",
                    "work/SchoolCEEP/Opposite-court-block-reference/Reference-6.png" },
                notes = new[] {
                    "The user's directions establish west_south as the higher block on the left after walking west along the long main walkway; west_north is the lower external corridor block.",
                    "1.50m is a local estimated grade difference, derived from the plan's three approximately6m ramp runs and8.33% annotation, consistent with the photographed eight-riser short stair. These blocks remain ground-level occupied wings, not new3.81m storeys.",
                    "Every native high-block wall/floor/roof/door transform is translated together. Existing collider identities, persistent doorway IDs, paired leaves and interaction settings are retained.",
                    "Only high-block generic room furniture, its existing lights and photo-owned objects centered inside the registered high footprint are translated. Other regions, central circulation, hall grade and the lower wing remain in place.",
                    "Ramp route starts at the east end of the north lane, returns at the west and east sides, and exits through the west high landing to the shared southern platform. The platform is cut around the adjacent staircase, so it cannot cover the last treads.",
                    "One blue guard follows each exposed/shared boundary; redundant opposing guards and crossing stair/landing profiles are omitted. Coarse physical guards sit outside the walking deck edges. The canopy is one low metal pitch, with columns outside all ramp/stair/corridor routes.",
                    "The shared platform joins the high corridor north of its existing paired door. The lower landing joins the long walkway and the lower wing corridor. No door array slot, room footprint, gameplay event or audio setting is changed.",
                    "All new native mesh assets are persisted through Unity Editor APIs before scene batching. Routes use actual deck/tread heights and full native player collision during explicit validation." }
            };
            SetupAssets();
            high.position += Vector3.up * HighFloor;
            report.raisedBlockTransforms = high.GetComponentsInChildren<Transform>(true).Length;
            RaiseGenericFurniture(architecture);
            RaisePhotoDetails(architecture, high);
            Slab("WestBlock_Foundation_High", 351.88f, 845.48f, 626.32f, 1380.68f, HighFloor - .18f);
            AccessDecks();
            RaisedMainApproach();
            BuildJointPosts();
            Canopy();
            Ramp("WestBlock_MainWalkway_GradeRamp",P(720,ApproachAxis,HighFloor),P(810,837,0),3.6f,false);
            Physics.SyncTransforms();
            report.nativeHighCollidersAfter = high.GetComponentsInChildren<Collider>(true).Length;
            report.nativeBlockCollidersPreserved = report.nativeHighCollidersBefore == report.nativeHighCollidersAfter
                && nativeColliders.All(c => c && c.transform.IsChildOf(high));
            report.doorIdsAfter = architecture.GetComponentsInChildren<InteractiveDoor>(true).Select(d => d.persistentId).OrderBy(s => s).ToArray();
            report.identitiesPreserved = ids.SequenceEqual(report.doorIdsAfter);
            report.nativeDoorInteractionsPreserved = doorState.All(s => s.Preserved());
            if (!report.identitiesPreserved || !report.nativeBlockCollidersPreserved || !report.nativeDoorInteractionsPreserved)
                throw new InvalidOperationException("Western grade changed a native collider, persistent doorway or interaction setting.");
            report.meshFolder = meshFolder; report.roofMaterial = RoofMaterialPath;
            report.flights = flights.ToArray(); report.routes = routes.ToArray();
            report.probes = new[] {
                Probe("WestBlock lower ramp entrance", 540, 767, LowFloor),
                Probe("WestBlock main corridor upper connection",650,ApproachAxis,HighFloor),
                Probe("WestBlock main corridor grade transition",961,837,.044f),
                Probe("WestBlock north ramp midpoint", 428.44f, NorthLane, Grade(.25f)),
                Probe("WestBlock west intermediate return", 368.92f, MiddleLane, Grade(.50f)),
                Probe("WestBlock middle ramp midpoint", 428.44f, MiddleLane, Grade(.75f)),
                Probe("WestBlock east intermediate return", 487.96f, SouthLane, Grade(1f)),
                Probe("WestBlock south ramp midpoint", 428.44f, SouthLane, Grade(1.25f)),
                Probe("WestBlock high platform", 483.64f, 837, HighFloor),
                Probe("WestBlock high corridor entry", 483.64f, 860, HighFloor),
                Probe("WestBlock lower corridor entry", 531.16f, 740, LowFloor) };
            LastReport = report;
            Directory.CreateDirectory(SchoolMapBuilder.Output);
            File.WriteAllText(SchoolMapBuilder.Output + "/" + ReportName, JsonUtility.ToJson(report, true));
            return report;
        }

        sealed class DoorState
        {
            readonly InteractiveDoor door, pair;
            readonly Transform hinge;
            readonly Collider leaf;
            readonly string id;
            readonly float angle;
            readonly bool locked;
            public DoorState(InteractiveDoor d) { door = d; pair = d.pairedLeaf; hinge = d.hinge; leaf = d.leafCollider; id = d.persistentId; angle = d.openAngle; locked = d.locked; }
            public bool Preserved() => door && door.pairedLeaf == pair && door.hinge == hinge && door.leafCollider == leaf
                && door.persistentId == id && door.openAngle == angle && door.locked == locked;
        }

        static bool InHigh(Vector3 p) {
            float x = 1413 + p.x / Scale, y = 1628 - p.z / Scale;
            return x >= 351.88f && x <= 626.32f && y >= 845.48f && y <= 1380.68f;
        }
        static void RaiseGenericFurniture(Transform architecture)
        {
            var furniture = architecture.Find("Mobiliário inicial — 0");
            if (!furniture) return;
            foreach (Transform child in furniture) if (InHigh(child.position)) {
                child.position += Vector3.up * HighFloor; report.raisedFurnitureTransforms++;
            }
        }
        static void RaisePhotoDetails(Transform architecture, Transform high)
        {
            var names = new HashSet<string>(new[] { "Arquitetura das fotos", "Detalhes das fotos", "Detalhes das novas fotos" });
            var candidates = architecture.Cast<Transform>().Where(t => names.Contains(t.name))
                .SelectMany(t => t.GetComponentsInChildren<Transform>(true))
                .Where(t => InHigh(t.position) && !t.IsChildOf(high)
                    && (t.GetComponent<Renderer>() || t.GetComponent<Collider>() || t.GetComponent<Light>())).ToArray();
            var selected = candidates.Where(t => !candidates.Any(a => a != t && t.IsChildOf(a))).ToArray();
            var shifted = new List<string>();
            foreach (var child in selected) { shifted.Add(child.name); child.position += Vector3.up * HighFloor; report.raisedPhotoTransforms++; }
            report.shiftedPhotoObjects = shifted.ToArray();
        }

        static void AccessDecks()
        {
            Slab("WestBlock_Landing_Low", RunEast, 749, 505, 778.04f, LowFloor);
            Slab("WestBlock_Low_Walkway_Connector", 505, 749, StairRight, 794.36f, LowFloor);
            Slab("WestBlock_Landing_West_050", 351.88f, 756.8f, RunWest, 799.4f, Grade(.50f));
            Slab("WestBlock_Landing_East_100", RunEast, 778.04f, 505, 820.64f, Grade(1f));
            Slab("WestBlock_Landing_West_High", 351.88f, 799.4f, RunWest, 820.64f, HighFloor);
            Slab("WestBlock_Platform_High", 351.88f, 820.64f, 505, 845.48f, HighFloor);
            Slab("WestBlock_Platform_Stair_Bridge",505,820.64f,StairLeft,845.48f,HighFloor);
            Slab("WestBlock_Platform_Stair_Exit", StairLeft, 829.16f, StairRight, 845.48f, HighFloor);
            Ramp("WestBlock_Ramp_Lane1", P(RunEast, NorthLane), P(RunWest, NorthLane,Grade(.50f)), (778.04f - 756.8f) * Scale);
            Ramp("WestBlock_Ramp_Lane2", P(RunWest, MiddleLane,Grade(.50f)), P(RunEast, MiddleLane,Grade(1f)), (799.4f - 778.04f) * Scale);
            Ramp("WestBlock_Ramp_Lane3", P(RunEast, SouthLane,Grade(1f)), P(RunWest, SouthLane, HighFloor), (820.64f - 799.4f) * Scale);
            const float outside = .04f / Scale;
            // One fence per shared edge. Flat platform borders own the adjacent higher level.
            Guard("WestBlock ramp outer north",P(RunWest,756.8f-outside,Grade(.5f)),P(RunEast,756.8f-outside,LowFloor));
            Guard("WestBlock divider north",P(RunWest,778.07f,Grade(.5f)),P(RunEast,778.07f,Grade(1f)));
            Guard("WestBlock divider south",P(RunWest,799.40f,Grade(1.5f)),P(RunEast,799.40f,Grade(1f)));
            Guard("WestBlock west intermediate edge", P(351.88f - outside, 756.8f - outside,Grade(.50f)), P(351.88f - outside, 799.4f,Grade(.50f)));
            Guard("WestBlock west intermediate north edge", P(351.88f - outside, 756.8f - outside,Grade(.50f)), P(RunWest, 756.8f - outside,Grade(.50f)));
            Guard("WestBlock west high north edge",P(351.88f-outside,799.4f,Grade(1.5f)),P(RunWest,799.4f,Grade(1.5f)));
            Guard("WestBlock east intermediate edge", P(505 + outside, 778.07f,Grade(1f)), P(505 + outside, 820.64f-.06f/Scale,Grade(1f)));
            Guard("WestBlock east intermediate north edge", P(RunEast, 778.07f,Grade(1f)), P(505 + outside, 778.07f,Grade(1f)));

            Guard("WestBlock west high edge", P(351.88f - outside, 799.4f, HighFloor), P(351.88f - outside, 845.48f, HighFloor));
            Guard("WestBlock platform north edge", P(RunWest, 820.64f, HighFloor), P(StairLeft - outside, 820.64f, HighFloor));
            // Front-room walls own the platform's south edge; leave the corridor's mouth unobstructed.
            Stairs();
            var rampRoute = new List<Vector3>();
            Line(rampRoute, Foot(P(540, NorthLane)), Foot(P(RunEast, NorthLane)));
            Line(rampRoute, Foot(P(RunEast, NorthLane)), Foot(P(RunWest, NorthLane,Grade(.50f))));
            Line(rampRoute, Foot(P(RunWest, NorthLane,Grade(.50f))), Foot(P(368.92f, NorthLane,Grade(.50f))));
            Line(rampRoute, Foot(P(368.92f, NorthLane,Grade(.50f))), Foot(P(368.92f, MiddleLane,Grade(.50f))));
            Line(rampRoute, Foot(P(368.92f, MiddleLane,Grade(.50f))), Foot(P(RunWest, MiddleLane,Grade(.50f))));
            Line(rampRoute, Foot(P(RunWest, MiddleLane,Grade(.50f))), Foot(P(RunEast, MiddleLane,Grade(1f))));
            Line(rampRoute, Foot(P(RunEast, MiddleLane,Grade(1f))), Foot(P(487.96f, MiddleLane,Grade(1f))));
            Line(rampRoute, Foot(P(487.96f, MiddleLane,Grade(1f))), Foot(P(487.96f, SouthLane,Grade(1f))));
            Line(rampRoute, Foot(P(487.96f, SouthLane,Grade(1f))), Foot(P(RunEast, SouthLane,Grade(1f))));
            Line(rampRoute, Foot(P(RunEast, SouthLane,Grade(1f))), Foot(P(RunWest, SouthLane, HighFloor)));
            Line(rampRoute, Foot(P(RunWest, SouthLane, HighFloor)), Foot(P(368.92f, SouthLane, HighFloor)));
            Line(rampRoute, Foot(P(368.92f, SouthLane, HighFloor)), Foot(P(368.92f, 837, HighFloor)));
            Line(rampRoute, Foot(P(368.92f, 837, HighFloor)), Foot(P(483.64f, 837, HighFloor)));
            Line(rampRoute, Foot(P(483.64f, 837, HighFloor)), Foot(P(483.64f, 860, HighFloor)));
            routes.Add(new SchoolCirculation.Route { name = "WestBlock_FullThreeLaneRamp_LowToHigh", feet = rampRoute.ToArray(), requireAscending = true });
            SimpleRoute("WestBlock_MainWalkway_ToUpperLanding",P(961,837,.044f),P(867,837,.044f),P(852,837,0),P(810,837,0),P(720,ApproachAxis,HighFloor),P(StairRight+8,ApproachAxis,HighFloor),P(StairRight+8,837,HighFloor),P(529.06f+StairShift,837,HighFloor));
            SimpleRoute("WestBlock_LowLanding_ToLowerWing", P(531.16f, 767), P(531.16f, 740));
            SimpleRoute("WestBlock_HighPlatform_ToCorridor", P(483.64f, 837, HighFloor), P(483.64f, 860, HighFloor), P(483.64f, 881, HighFloor));
        }

        static void Ramp(string name, Vector3 start, Vector3 end, float width, bool guards=false)
        {
            Vector3 direction = end - start; direction.y = 0; float run = direction.magnitude; direction /= run;
            float half = width * .5f;
            var vertices = new[] {
                new Vector3(-half, start.y, 0), new Vector3(half, start.y, 0), new Vector3(-half, end.y, run), new Vector3(half, end.y, run),
                new Vector3(-half, Mathf.Min(start.y,end.y)-.18f, 0), new Vector3(half, Mathf.Min(start.y,end.y)-.18f, 0), new Vector3(-half, Mathf.Min(start.y,end.y)-.18f, run), new Vector3(half, Mathf.Min(start.y,end.y)-.18f, run) };
            MeshSolid(name, vertices, new Vector3(start.x, 0, start.z), Quaternion.LookRotation(direction), concrete, true);
            report.rampMeshColliders++;
            flights.Add(new SchoolCirculation.FlightReport { name = name, ramp = true, width = width, run = run,
                rise = end.y - start.y, slopeDegrees = Mathf.Atan2(end.y - start.y, run) * Mathf.Rad2Deg });
            var feet = new List<Vector3>(); Line(feet, Foot(start), Foot(end));
            routes.Add(new SchoolCirculation.Route { name = name + "_Route", feet = feet.ToArray(), requireAscending = end.y-start.y>.10f });
            Vector3 offset = Vector3.Cross(Vector3.up, direction) * (width * .5f + .04f);
            if(guards){Guard(name + " — edge A", start - offset, end - offset);Guard(name + " — edge B", start + offset, end + offset);}
        }

        static void Stairs()
        {
            const int count = 8;
            const float north = 794.36f, south = 829.16f, x = 529.06f + StairShift;
            Vector3 start = P(x, north,LowFloor), end = P(x, south, HighFloor), direction = end - start;
            direction.y = 0; float run = direction.magnitude; direction /= run;
            float width = (553.12f - 505) * Scale, tread = run / count;
            var feet = new List<Vector3>();
            Line(feet, Foot(P(x, 780)), Foot(start - direction * .002f));
            for (int i = 0; i < count; i++) {
                float top = LowFloor+(HighFloor-LowFloor) * (i + 1) / count;
                Vector3 center = start + direction * (tread * (i + .5f)); center.y = (top+LowFloor-.07f) * .5f;
                Cube("WestBlock_Step_" + (i + 1).ToString("D2"), center, new Vector3(width, top-LowFloor+.07f, tread), Quaternion.LookRotation(direction), concrete, true).AddComponent<SurfaceType>().surface = GroundSurface.Concrete;
                report.solidTreads++;
                Vector3 a = start + direction * (i * tread + .002f), b = start + direction * ((i + 1) * tread - .002f);
                a.y = b.y = top; Line(feet, Foot(a), Foot(b));
            }
            Line(feet, Foot(end), Foot(P(x, 837, HighFloor)));
            Line(feet, Foot(P(x, 837, HighFloor)), Foot(P(483.64f, 837, HighFloor)));
            Line(feet, Foot(P(483.64f, 837, HighFloor)), Foot(P(483.64f, 860, HighFloor)));
            routes.Add(new SchoolCirculation.Route { name = "WestBlock_EightStepStair_LowToHigh", feet = feet.ToArray(), requireAscending = true });
            flights.Add(new SchoolCirculation.FlightReport { name = "WestBlock_EightStepStair", ramp = false, width = width,
                run = run, rise = HighFloor-LowFloor, riser = (HighFloor-LowFloor) / count, risers = count, slopeDegrees = Mathf.Atan2(HighFloor-LowFloor, run) * Mathf.Rad2Deg });
            var offset = Vector3.Cross(Vector3.up, direction) * (width * .5f + .04f);
            // The stair profile begins at the landing junction, so horizontal rails meet a post
            // instead of intersecting the interior of a sloping tube at different rail heights.
            float jointY=820.64f-.06f/Scale;
            float jointHeight=LowFloor+(HighFloor-LowFloor)*(jointY-north)/(south-north);
            Guard("WestBlock stair common edge",P(StairLeft-.04f/Scale,north,LowFloor),P(StairLeft-.04f/Scale,jointY,jointHeight));
            Guard("WestBlock platform beside stair",P(StairLeft-.04f/Scale,820.64f,HighFloor),P(StairLeft-.04f/Scale,829.16f,HighFloor));
            // The adjoining raised approach owns this edge at its higher level.
            // A second inclined fence here would cross its horizontal tubes.
        }

        static void Guard(string name, Vector3 start, Vector3 end)
        {
            if (Vector3.Distance(start, end) < .02f) return;
            if(!name.Contains("stair")&&!name.Contains("beside stair"))return;
            // The lowest stair tube must clear the discrete riser tops along its sloping profile.
            float lowTube=name.Contains("stair")?.30f:name=="WestBlock east intermediate edge"?.433f:.12f;
            foreach (float height in new[] { 1f, .70f, lowTube }) {
                RoundRail(name + " — tubo azul", start + Vector3.up * height, end + Vector3.up * height, height == 1f ? .043f : .032f);
                report.railRuns++;
            }
            float length = new Vector2(end.x - start.x, end.z - start.z).magnitude;
            int count = Mathf.Max(1, Mathf.CeilToInt(length / 1.20f));
            for (int i = 0; i <= count; i++) {
                Vector3 basePoint = Vector3.Lerp(start, end, i / (float)count);
                postBases.Add(basePoint);
            }
            // A closed vertical prism follows the sloping rail without putting coarse cubes into a route.
            Vector3 direction = end - start; direction.y = 0; direction.Normalize();
            Vector3 sideways = Vector3.Cross(Vector3.up, direction) * .018f;
            var v = new[] { start - sideways + Vector3.up, start + sideways + Vector3.up, end - sideways + Vector3.up, end + sideways + Vector3.up,
                start - sideways, start + sideways, end - sideways, end + sideways };
            var mesh = PersistMesh(name + "_Guard", v, new[] { 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 4, 0, 5, 5, 0, 1, 6, 7, 2, 7, 3, 2, 4, 6, 0, 6, 2, 0, 5, 1, 7, 7, 1, 3 });
            var go = new GameObject(name + " — proteção física"); go.transform.SetParent(group, false); go.isStatic = true;
            go.AddComponent<MeshCollider>().sharedMesh = mesh; report.edgeGuardColliders++;
        }

        static void RaisedMainApproach()
        {
            ApproachSection("WestBlock_UpperApproach_Throat",StairRight,626.32f,3.6f,3.6f,HighFloor,HighFloor,false,true,ApproachAxis,ApproachAxis);
            ApproachSection("WestBlock_UpperApproach_Wide",626.32f,720f,3.6f,3.6f,HighFloor,HighFloor,false,true,ApproachAxis,ApproachAxis);
            ApproachSection("WestBlock_UpperApproach_Grade",720f,810f,3.6f,3.6f,HighFloor,0,true,false,ApproachAxis,837);
            ApproachSection("WestBlock_UpperApproach_LowConnectionDoor",810f,828f,3.6f,3.6f,0,0,false);
            ApproachSection("WestBlock_UpperApproach_PortalFloor",828f,852f,3.6f,3.6f,0,0,false,true,837,837,false);
            ApproachSection("WestBlock_UpperApproach_FloorTransition",852f,867f,3.6f,3.6f,0,.044f,false,true,837,837,false);
            ApproachSection("WestBlock_UpperApproach_LowConnection",867f,961f,3.6f,3.6f,.044f,.044f,false,true,837,837,false);
            Guard("WestBlock approach north",P(StairRight+.04f/Scale,ApproachAxis,HighFloor)+Vector3.forward*1.84f,P(720,ApproachAxis,HighFloor)+Vector3.forward*1.84f);
            // The native high-block facade owns the southern edge until its corner.
            Guard("WestBlock approach south",P(626.32f,ApproachAxis,HighFloor)+Vector3.back*1.84f,P(720,ApproachAxis,HighFloor)+Vector3.back*1.84f);
            Guard("WestBlock approach stair return",P(StairRight+.04f/Scale,ApproachAxis,HighFloor)+Vector3.forward*1.84f,P(StairRight+.04f/Scale,829.16f,HighFloor));
        }
        static void ApproachSection(string name,float x0,float x1,float w0,float w1,float h0,float h1,bool guards,bool deck=true,float y0=837,float y1=837,bool cover=true)
        {
            Vector3 a=P(x0,y0,h0),b=P(x1,y1,h1),side=Vector3.back;
            var top=new[]{a-side*w0*.5f,a+side*w0*.5f,b-side*w1*.5f,b+side*w1*.5f};
            if(deck){var v=top.Concat(top.Select(p=>new Vector3(p.x,-.07f,p.z))).ToArray();MeshSolid(name,v,Vector3.zero,Quaternion.identity,concrete,true);report.slabColliders++;}
            if(cover){
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/SchoolCEEP/Materials/PhotoRoofGrey.mat");
                Vector3 ra=a+Vector3.right*(Mathf.Max(0,568-x0)*Scale);
                var rv=new[]{ra-side*(w0*.5f+.10f)+Vector3.up*2.85f,ra+Vector3.up*3.35f,ra+side*(w0*.5f+.10f)+Vector3.up*2.85f,
                    b-side*(w1*.5f+.10f)+Vector3.up*2.85f,b+Vector3.up*3.35f,b+side*(w1*.5f+.10f)+Vector3.up*2.85f};
                var mesh=PersistMesh(name+"_Roof",rv.Concat(rv).ToArray(),new[]{0,3,1,1,3,4,1,4,2,2,4,5,6,7,9,7,10,9,7,8,10,8,11,10});
                var canopy=new GameObject(name+" — telhado do corredor");canopy.transform.SetParent(group,false);canopy.isStatic=true;
                canopy.AddComponent<MeshFilter>().sharedMesh=mesh;canopy.AddComponent<MeshRenderer>().sharedMaterial=material;
                int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(ra,b)/3.6f));
                for(int i=0;i<=n;i++){
                    var c=Vector3.Lerp(ra,b,i/(float)n);float w=Mathf.Lerp(w0,w1,i/(float)n)*.5f+.10f;
                    Beam(name+" — tesoura",c-side*w+Vector3.up*2.78f,c+Vector3.up*3.28f,.055f,blue);
                    Beam(name+" — tesoura",c+Vector3.up*3.28f,c+side*w+Vector3.up*2.78f,.055f,blue);
                }
            }
            if(!guards)return;
            foreach(int s in new[]{-1,1})Guard(name+" — bordo "+s,a+side*(s*(w0*.5f+.04f)),b+side*(s*(w1*.5f+.04f)));
        }

        static void BuildJointPosts()
        {
            var joints=postBases.GroupBy(p=>Mathf.RoundToInt(p.x*1000)+":"+Mathf.RoundToInt(p.z*1000));
            foreach(var joint in joints)
            {
                var p=joint.First();float bottom=joint.Min(v=>v.y),top=joint.Max(v=>v.y)+1f;
                RoundRail("WestBlock guard — montante único",new Vector3(p.x,bottom+.04f,p.z),new Vector3(p.x,top,p.z),.038f);
                Cube("WestBlock guard — sapata única",new Vector3(p.x,bottom+.025f,p.z),new Vector3(.11f,.05f,.11f),Quaternion.identity,blue,false);
            }
        }

        static void Canopy()
        {
            const float west = 345, east = 562, north = 743, south = 847;
            float hn = report.roofNorthHeight, hs = report.roofSouthHeight;
            Vector3 nw = P(west, north, hn), ne = P(east, north, hn), sw = P(west, south, hs), se = P(east, south, hs);
            var v = new[] { sw, se, nw, ne, sw - Vector3.up * .065f, se - Vector3.up * .065f, nw - Vector3.up * .065f, ne - Vector3.up * .065f };
            MeshSolid("WestBlock_Canopy_SinglePitch", v, Vector3.zero, Quaternion.identity, roofing, true);
            report.roofColliders++;
            int ribs = Mathf.CeilToInt((east - west) * Scale / .23f);
            for (int i = 0; i <= ribs; i++) {
                float x = Mathf.Lerp(west, east, i / (float)ribs);
                RoundRail("WestBlock canopy — ondulação", P(x, north, hn + .012f), P(x, south, hs + .012f), .014f, roofing);
            }
            foreach (float y in new[] { 746f, 792f, 841f }) {
                float roofY = Mathf.Lerp(hn, hs, (y - north) / (south - north)) - .11f;
                Beam("WestBlock canopy — travessa", P(west, y, roofY), P(east, y, roofY), .08f, blue);
                foreach (float x in new[] { west, east }) {
                    float supportY=x==east&&y==841f?849f:y;
                    float supportHeight=Mathf.Lerp(hn,hs,(supportY-north)/(south-north))-.11f;
                    float baseHeight=supportY<829.2f?LowFloor:0;
                    Cube("WestBlock canopy — apoio fora da circulação", P(x, supportY, (supportHeight+baseHeight) * .5f), new Vector3(.095f, supportHeight-baseHeight, .095f), Quaternion.identity, blue, true);
                    Cube("WestBlock canopy — sapata de apoio", P(x, supportY, baseHeight+.035f), new Vector3(.27f, .09f, .27f), Quaternion.identity, concrete, true);
                    if(baseHeight<0)Cube("WestBlock canopy — fundação rebaixada",P(x,supportY,baseHeight-.12f),new Vector3(.45f,.24f,.45f),Quaternion.identity,concrete,true);
                    report.roofSupports++;
                }
            }
            Beam("WestBlock canopy — borda oeste", nw - Vector3.up * .10f, sw - Vector3.up * .10f, .08f, blue);
            Beam("WestBlock canopy — borda leste", ne - Vector3.up * .10f, se - Vector3.up * .10f, .08f, blue);
            foreach (float y in new[] { 758f, 799f, 835f }) {
                float roofY = Mathf.Lerp(hn, hs, (y - north) / (south - north)) - .20f;
                Cube("WestBlock canopy — luminária fluorescente", P(527, y, roofY), new Vector3(1.1f, .07f, .19f), Quaternion.identity, lamp, false);
                report.decorativeRoofFixtures++;
            }
        }

        static void Slab(string name, float x0, float y0, float x1, float y1, float top)
        {
            float bottom = Mathf.Min(-.07f,top-.18f);
            Cube(name, P((x0 + x1) * .5f, (y0 + y1) * .5f, (top + bottom) * .5f),
                new Vector3((x1 - x0) * Scale, top - bottom, (y1 - y0) * Scale), Quaternion.identity, concrete, true).AddComponent<SurfaceType>().surface = GroundSurface.Concrete;
            report.slabColliders++;
        }
        static GameObject MeshSolid(string name, Vector3[] vertices, Vector3 position, Quaternion rotation, Material material, bool collision)
        {
            var mesh = PersistMesh(name, vertices, new[] { 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 4, 0, 5, 5, 0, 1, 6, 7, 2, 7, 3, 2, 4, 6, 0, 6, 2, 0, 5, 1, 7, 7, 1, 3 });
            var go = new GameObject(name); go.transform.SetParent(group, false); go.transform.position = position; go.transform.rotation = rotation; go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collision) { go.AddComponent<MeshCollider>().sharedMesh = mesh; go.AddComponent<SurfaceType>().surface = GroundSurface.Concrete; }
            return go;
        }
        static Mesh PersistMesh(string name, Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            // Faceted normals are intentional for structural solid wedges. World-space UVs
            // keep procedural concrete/roof textures at the same scale through batching.
            mesh.uv = vertices.Select(v => new Vector2(v.x * .35f, v.z * .35f)).ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string clean = new string(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
            string path = meshFolder + "/" + clean + ".asset";
            AssetDatabase.CreateAsset(mesh, path); return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        static GameObject Cube(string name, Vector3 position, Vector3 scale, Quaternion rotation, Material material, bool collision)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(group, false);
            go.transform.position = position; go.transform.rotation = rotation; go.transform.localScale = scale; go.isStatic = true;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        static void RoundRail(string name, Vector3 a, Vector3 b, float diameter, Material material = null)
        {
            float length = Vector3.Distance(a, b); if (length < .002f) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; go.transform.SetParent(group, false); go.isStatic = true;
            go.transform.position = (a + b) * .5f; go.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            go.transform.localScale = new Vector3(diameter, length * .5f, diameter); go.GetComponent<MeshRenderer>().sharedMaterial = material ? material : blue;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        static void Beam(string name, Vector3 a, Vector3 b, float thickness, Material material) =>
            Cube(name, (a + b) * .5f, new Vector3(thickness, thickness, Vector3.Distance(a, b)), Quaternion.LookRotation(b - a), material, false);
        static void SetupAssets()
        {
            EnsureFolder(MeshRoot);
            string revision = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            AssetDatabase.CreateFolder(MeshRoot, revision); meshFolder = MeshRoot + "/" + revision;
            concrete = AssetDatabase.LoadAssetAtPath<Material>("Assets/SchoolCEEP/Materials/PhotoConcreteFloor.mat");
            if (!concrete) throw new InvalidOperationException("Build SchoolPhotoPass before the western access.");
            blue = MaterialAsset("WestBlockAccessBlueSteel", new Color(.025f, .12f, .35f), .22f, .45f);
            roofing = MaterialAsset("WestBlockAccessRoofGrey", new Color(.44f, .45f, .43f), .20f, .30f);
            lamp = MaterialAsset("WestBlockAccessFluorescent", new Color(.70f, .73f, .69f), .15f, 0);
            lamp.EnableKeyword("_EMISSION"); lamp.SetColor("_EmissionColor", new Color(.70f, .78f, .72f) * 1.8f); EditorUtility.SetDirty(lamp);
        }
        static Material MaterialAsset(string name, Color color, float smooth, float metal)
        {
            string path = "Assets/SchoolCEEP/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal); EditorUtility.SetDirty(m); return m;
        }
        static void EnsureFolder(string path)
        {
            var parts = path.Split('/'); string parent = parts[0];
            for (int i = 1; i < parts.Length; i++) { string next = parent + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]); parent = next; }
        }
        static float Grade(float old)=>LowFloor+old/1.5f*(HighFloor-LowFloor);
        static Vector3 P(float x, float y, float h = LowFloor) => SchoolMapBuilder.Point(x, y, h);
        static Vector3 Foot(Vector3 p) => p + Vector3.up * FootClearance;
        static SchoolValidationPass.WalkProbe Probe(string name, float x, float y, float h) => new SchoolValidationPass.WalkProbe { name = name, footPosition = Foot(P(x, y, h)) };
        static void SimpleRoute(string name, params Vector3[] points)
        {
            var feet = new List<Vector3>();
            for (int i = 0; i < points.Length - 1; i++) Line(feet, Foot(points[i]), Foot(points[i + 1]));
            routes.Add(new SchoolCirculation.Route { name = name, feet = feet.ToArray(), requireAscending = false });
        }
        static void Line(List<Vector3> feet, Vector3 a, Vector3 b)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(new Vector2(b.x - a.x, b.z - a.z).magnitude / Spacing));
            for (int i = 0; i <= count; i++) { var p = Vector3.Lerp(a, b, i / (float)count); if (feet.Count == 0 || (feet[feet.Count - 1] - p).sqrMagnitude > .00000001f) feet.Add(p); }
        }
        public static SchoolValidationPass.ValidationReport Validate()
        {
            var saved = JsonUtility.FromJson<BuildReport>(File.ReadAllText(SchoolMapBuilder.Output + "/" + ReportName));
            if (saved == null || saved.sourceRevision != SourceRevision || saved.routes == null) throw new InvalidDataException("Missing current western access report.");
            return SchoolValidationPass.Validate(SceneManager.GetActiveScene(), P(1413, 1588, .05f), saved.probes,
                saved.routes.Select(r => new SchoolValidationPass.TraversalPath { name = r.name, footPositions = r.feet, isStairRoute = r.requireAscending }).ToArray());
        }
    }
}
