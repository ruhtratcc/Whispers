// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>
    /// Plan-based circulation geometry only. Call from the scene builder; this helper does
    /// not open/save scenes, change assets, or infer upper-floor routes at the small stairs.
    /// PDF coordinates are P01 registered, top-left origin. Parent should have unit scale.
    /// </summary>
    public static class SchoolCirculation
    {
        public const float PdfScale = .0705555556f;
        public const float FirstFloor = 3.81f;
        const float OriginX = 1413f, OriginY = 1628f;
        const float MaxRiser = .20f, Deck = .16f, RailHeight = 1.05f;
        const float RouteSpacing = .12f;

        [Serializable]
        public sealed class Route
        {
            public string name;
            public Vector3[] feet;
            public bool requireAscending = true;
        }

        [Serializable]
        public sealed class FlightReport
        {
            public string name;
            public bool ramp;
            public float width, run, rise, riser, slopeDegrees;
            public int risers;
        }

        [Serializable]
        public sealed class BuildReport
        {
            public FlightReport[] flights;
            public Route[] routes;
            public float maximumStairRiser, minimumClearWidth;
            public string[] notes;
        }

        public static Route[] Routes { get; private set; } = Array.Empty<Route>();
        public static BuildReport LastReport { get; private set; }

        public static Vector3 PdfPoint(float x, float y, float elevation = 0f) =>
            new Vector3((x - OriginX) * PdfScale, elevation, (OriginY - y) * PdfScale);

        public static BuildReport Build(Transform parent, Material concrete, Material railing)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            if (!concrete || !railing) throw new ArgumentException("Provide both circulation materials.");
            var root = Group(parent, "School_Circulation");
            var flights = new List<FlightReport>();
            var routes = new List<Route>();
            BuildMain(root, concrete, railing, flights, routes);
            BuildEast(root, concrete, railing, flights, routes);
            BuildRamp(root, concrete, railing, flights, routes);
            Routes = routes.ToArray();
            LastReport = new BuildReport
            {
                flights = flights.ToArray(), routes = Routes,
                maximumStairRiser = FirstFloor / 20f,
                minimumClearWidth = (989f - 971.92f) * PdfScale - .12f,
                notes = new[]
                {
                    "Two full-height U stairs: 20 risers each, 0.1905m riser; 1.676m and 1.693m flight widths.",
                    "Stair runs extended inside the overall plan footprints for safe playable treads; drawn tread count is not used as elevation evidence.",
                    "Central ramp: four 8.001m runs, 0.9525m rise each, about11.90% incline; playable geometry, not an accessibility certification.",
                    "Ramp starts and finishes north; the upper connector continues from PDFy892.16 to851 at elevation3.81m.",
                    "Small north stairs and other grade ramps are intentionally outside this helper: endpoint heights are unspecified.",
                    "Routes contain real tread-top heights and ramp-surface heights, with horizontal spacing <=0.12m; vertical stair jumps equal the riser.",
                    "Root scene builder must leave the stair/ramp floor openings and connector access clear of floors, walls, roofs and props."
                }
            };
            return LastReport;
        }

        static void BuildMain(Transform parent, Material deck, Material rail,
            List<FlightReport> reports, List<Route> routes)
        {
            var group = Group(parent, "Main_U_Stair");
            const float westMin = 990.76f, westMax = 1014.52f;
            const float eastMin = 1022.96f, eastMax = 1046.68f;
            const float bottomY = 809.24f, turnY = 739.12f, northY = 722.12f;
            float wx = (westMin + westMax) / 2f, ex = (eastMin + eastMax) / 2f;
            float widthA = (westMax - westMin) * PdfScale, widthB = (eastMax - eastMin) * PdfScale;
            Vector3 a0 = PdfPoint(wx, bottomY), a1 = PdfPoint(wx, turnY, FirstFloor / 2f);
            Vector3 b0 = PdfPoint(ex, turnY, FirstFloor / 2f), b1 = PdfPoint(ex, bottomY, FirstFloor);
            StairFlight(group, "West_Flight", a0, a1, widthA, deck, rail, reports);
            StairFlight(group, "East_Flight", b0, b1, widthB, deck, rail, reports);
            Landing(group, "North_Mid_Landing", westMin, northY, eastMax, turnY, FirstFloor / 2f, deck);
            Guard(group, "North_Mid_Edge", PdfPoint(westMin, northY, FirstFloor / 2f),
                PdfPoint(eastMax, northY, FirstFloor / 2f), rail);
            Guard(group, "North_West_Edge", PdfPoint(westMin, northY, FirstFloor / 2f),
                PdfPoint(westMin, turnY, FirstFloor / 2f), rail);
            Guard(group, "North_East_Edge", PdfPoint(eastMax, northY, FirstFloor / 2f),
                PdfPoint(eastMax, turnY, FirstFloor / 2f), rail);
            Landing(group, "Ground_South_Approach", westMin, bottomY, westMax, 823.42f, 0f, deck);
            Landing(group, "Upper_South_Exit", eastMin, bottomY, eastMax, 823.42f, FirstFloor, deck);
            SideGuards(group, "Upper_Exit", b1, PdfPoint(ex, 823.42f, FirstFloor), widthB, rail);
            var feet = new List<Vector3>();
            Line(feet, PdfPoint(wx, 821.42f), BeforeFlight(a0, a1));
            StairRoute(feet, a0, a1);
            Line(feet, a1, PdfPoint(wx, 730.62f, FirstFloor / 2f));
            Line(feet, PdfPoint(wx, 730.62f, FirstFloor / 2f), PdfPoint(ex, 730.62f, FirstFloor / 2f));
            Line(feet, PdfPoint(ex, 730.62f, FirstFloor / 2f), BeforeFlight(b0, b1));
            StairRoute(feet, b0, b1);
            Line(feet, b1, PdfPoint(ex, 821.42f, FirstFloor));
            routes.Add(WorldRoute(parent, "MainU_GroundSouth_To_UpperSouth", feet));
        }

        static void BuildEast(Transform parent, Material deck, Material rail,
            List<FlightReport> reports, List<Route> routes)
        {
            var group = Group(parent, "East_Teaching_U_Stair");
            const float westX = 2290.84f, turnX = 2307.84f, eastX = 2393.08f;
            const float northMin = 775.28f, northMax = 799.4f, southMin = 800.84f, southMax = 824.84f;
            float sy = (southMin + southMax) / 2f, ny = (northMin + northMax) / 2f;
            float sw = (southMax - southMin) * PdfScale, nw = (northMax - northMin) * PdfScale;
            Vector3 a0 = PdfPoint(eastX, sy), a1 = PdfPoint(turnX, sy, FirstFloor / 2f);
            Vector3 b0 = PdfPoint(turnX, ny, FirstFloor / 2f), b1 = PdfPoint(eastX, ny, FirstFloor);
            StairFlight(group, "South_Westbound_Flight", a0, a1, sw, deck, rail, reports);
            StairFlight(group, "North_Eastbound_Flight", b0, b1, nw, deck, rail, reports);
            Landing(group, "West_Mid_Landing", westX, northMin, turnX, southMax, FirstFloor / 2f, deck);
            Guard(group, "West_Mid_Edge", PdfPoint(westX, northMin, FirstFloor / 2f),
                PdfPoint(westX, southMax, FirstFloor / 2f), rail);
            Guard(group, "West_North_Edge", PdfPoint(westX, northMin, FirstFloor / 2f),
                PdfPoint(turnX, northMin, FirstFloor / 2f), rail);
            Guard(group, "West_South_Edge", PdfPoint(westX, southMax, FirstFloor / 2f),
                PdfPoint(turnX, southMax, FirstFloor / 2f), rail);
            Landing(group, "Ground_East_Approach", eastX, southMin, 2410.08f, southMax, 0f, deck);
            Landing(group, "Upper_East_Exit", eastX, northMin, 2410.08f, northMax, FirstFloor, deck);
            SideGuards(group, "Upper_Exit", b1, PdfPoint(2410.08f, ny, FirstFloor), nw, rail);
            var feet = new List<Vector3>();
            Line(feet, PdfPoint(2408.08f, sy), BeforeFlight(a0, a1));
            StairRoute(feet, a0, a1);
            Line(feet, a1, PdfPoint(2299.34f, sy, FirstFloor / 2f));
            Line(feet, PdfPoint(2299.34f, sy, FirstFloor / 2f), PdfPoint(2299.34f, ny, FirstFloor / 2f));
            Line(feet, PdfPoint(2299.34f, ny, FirstFloor / 2f), BeforeFlight(b0, b1));
            StairRoute(feet, b0, b1);
            Line(feet, b1, PdfPoint(2408.08f, ny, FirstFloor));
            routes.Add(WorldRoute(parent, "EastU_GroundEast_To_UpperEast", feet));
        }

        static void BuildRamp(Transform parent, Material deck, Material rail,
            List<FlightReport> reports, List<Route> routes)
        {
            var group = Group(parent, "Central_Four_Lane_Ramp");
            float[] edges = { 971.92f, 989f, 1010.24f, 1031.44f, 1052.08f };
            const float north = 920.6f, south = 1034f, landingNorth = 892.16f, landingSouth = 1062.28f;
            float quarter = FirstFloor / 4f;
            var feet = new List<Vector3>();
            float firstX = (edges[0] + edges[1]) / 2f;
            Landing(group, "Ground_North_Entry", edges[0], landingNorth, edges[1], north, 0f, deck);
            Line(feet, PdfPoint(firstX, landingNorth + 3f), PdfPoint(firstX, north));
            for (int lane = 0; lane < 4; lane++)
            {
                float x = (edges[lane] + edges[lane + 1]) / 2f;
                bool southbound = lane % 2 == 0;
                float startY = southbound ? north : south, endY = southbound ? south : north;
                Vector3 start = PdfPoint(x, startY, lane * quarter);
                Vector3 end = PdfPoint(x, endY, (lane + 1) * quarter);
                float width = (edges[lane + 1] - edges[lane]) * PdfScale;
                InclinedDeck(group, "Inclined_Lane_" + (lane + 1), start, end, width, deck);
                SideGuards(group, "Lane_" + (lane + 1), start, end, width, rail);
                reports.Add(new FlightReport
                {
                    name = "CentralRamp_Lane" + (lane + 1), ramp = true, width = width,
                    run = Horizontal(end - start), rise = quarter, riser = 0f, risers = 0,
                    slopeDegrees = Mathf.Atan2(quarter, Horizontal(end - start)) * Mathf.Rad2Deg
                });
                Line(feet, start, end);
                if (lane == 3) break;
                float height = (lane + 1) * quarter;
                float nextX = (edges[lane + 1] + edges[lane + 2]) / 2f;
                float turnY = southbound ? (south + landingSouth) / 2f : (north + landingNorth) / 2f;
                Landing(group, "Turn_" + (lane + 1), edges[lane], southbound ? south : landingNorth,
                    edges[lane + 2], southbound ? landingSouth : north, height, deck);
                Guard(group, "Turn_Outer_" + (lane + 1),
                    PdfPoint(edges[lane], southbound ? landingSouth : landingNorth, height),
                    PdfPoint(edges[lane + 2], southbound ? landingSouth : landingNorth, height), rail);
                Guard(group, "Turn_West_" + (lane + 1),
                    PdfPoint(edges[lane], southbound ? south : landingNorth, height),
                    PdfPoint(edges[lane], southbound ? landingSouth : north, height), rail);
                Guard(group, "Turn_East_" + (lane + 1),
                    PdfPoint(edges[lane + 2], southbound ? south : landingNorth, height),
                    PdfPoint(edges[lane + 2], southbound ? landingSouth : north, height), rail);
                Line(feet, end, PdfPoint(x, turnY, height));
                Line(feet, PdfPoint(x, turnY, height), PdfPoint(nextX, turnY, height));
                Line(feet, PdfPoint(nextX, turnY, height), PdfPoint(nextX, endY, height));
            }
            float lastX = (edges[3] + edges[4]) / 2f;
            Landing(group, "Upper_North_Landing", edges[3], landingNorth, edges[4], north, FirstFloor, deck);
            Landing(group, "Upper_Hall_Connector", edges[3], 851f, edges[4], landingNorth, FirstFloor, deck);
            Vector3 exitStart = PdfPoint(lastX, north, FirstFloor), exitEnd = PdfPoint(lastX, 851f, FirstFloor);
            SideGuards(group, "Upper_Hall_Connector", exitStart, exitEnd, (edges[4] - edges[3]) * PdfScale, rail);
            Line(feet, exitStart, PdfPoint(lastX, 854f, FirstFloor));
            routes.Add(WorldRoute(parent, "CentralRamp_GroundNorth_To_UpperHallNorth", feet));
        }

        static void StairFlight(Transform parent, string name, Vector3 start, Vector3 end,
            float width, Material deck, Material rail, List<FlightReport> reports)
        {
            int count = Mathf.CeilToInt((end.y - start.y) / MaxRiser);
            float rise = (end.y - start.y) / count, run = Horizontal(end - start), tread = run / count;
            Vector3 forward = end - start; forward.y = 0f; forward.Normalize();
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            var root = Group(parent, name);
            for (int i = 0; i < count; i++)
            {
                float top = start.y + rise * (i + 1), thickness = top - start.y + .08f;
                Vector3 center = start + forward * (tread * (i + .5f));
                center.y = top - thickness / 2f;
                Cube(root, "Tread_" + (i + 1).ToString("D2"), center,
                    new Vector3(width, thickness, tread), rotation, deck);
            }
            SideGuards(root, "Flight", start, end, width, rail);
            reports.Add(new FlightReport { name = name, ramp = false, width = width,
                run = run, rise = end.y - start.y, riser = rise, risers = count,
                slopeDegrees = Mathf.Atan2(end.y - start.y, run) * Mathf.Rad2Deg });
        }

        static void StairRoute(List<Vector3> feet, Vector3 start, Vector3 end)
        {
            int risers = Mathf.CeilToInt((end.y - start.y) / MaxRiser);
            int samples = Mathf.CeilToInt(Horizontal(end - start) / RouteSpacing);
            Vector3 forward = end - start; forward.y = 0f; forward.Normalize();
            Vector3 first = start + forward * .001f;
            first.y += (end.y - start.y) / risers;
            Add(feet, first);
            for (int i = 1; i <= samples; i++)
            {
                float t = (float)i / samples;
                Vector3 point = Vector3.Lerp(start, end, t);
                int step = Mathf.Min(risers, Mathf.FloorToInt(t * risers) + 1);
                point.y = start.y + step * (end.y - start.y) / risers;
                Add(feet, point);
            }
        }

        static Vector3 BeforeFlight(Vector3 start, Vector3 end)
        {
            Vector3 direction = end - start; direction.y = 0f; direction.Normalize();
            return start - direction * .10f;
        }

        static void InclinedDeck(Transform parent, string name, Vector3 start, Vector3 end,
            float width, Material material)
        {
            Quaternion rotation = Quaternion.LookRotation(end - start, Vector3.up);
            Vector3 center = (start + end) / 2f - rotation * Vector3.up * (Deck / 2f);
            Cube(parent, name, center, new Vector3(width, Deck, Vector3.Distance(start, end)), rotation, material);
        }

        static void Landing(Transform parent, string name, float xmin, float ymin, float xmax,
            float ymax, float height, Material material)
        {
            Cube(parent, name, PdfPoint((xmin + xmax) / 2f, (ymin + ymax) / 2f, height - Deck / 2f),
                new Vector3((xmax - xmin) * PdfScale, Deck, (ymax - ymin) * PdfScale), Quaternion.identity, material);
        }

        static void SideGuards(Transform parent, string name, Vector3 start, Vector3 end,
            float width, Material material)
        {
            Vector3 direction = end - start; direction.y = 0f; direction.Normalize();
            Vector3 offset = Vector3.Cross(Vector3.up, direction) * (width / 2f - .025f);
            Guard(parent, name + "_Left", start - offset, end - offset, material);
            Guard(parent, name + "_Right", start + offset, end + offset, material);
        }

        static void Guard(Transform parent, string name, Vector3 start, Vector3 end, Material material)
        {
            var group = Group(parent, name);
            Beam(group, "Handrail", start + Vector3.up * RailHeight, end + Vector3.up * RailHeight, .055f, material);
            Beam(group, "Midrail", start + Vector3.up * .50f, end + Vector3.up * .50f, .04f, material);
            Beam(group, "Toe_Guard", start + Vector3.up * .075f, end + Vector3.up * .075f, .075f, material);
            int posts = Mathf.Max(1, Mathf.CeilToInt(Horizontal(end - start) / 1f));
            for (int i = 0; i <= posts; i++)
            {
                Vector3 foot = Vector3.Lerp(start, end, (float)i / posts);
                Cube(group, "Post_" + i.ToString("D2"), foot + Vector3.up * (RailHeight / 2f),
                    new Vector3(.055f, RailHeight, .055f), Quaternion.identity, material);
            }
        }

        static void Beam(Transform parent, string name, Vector3 start, Vector3 end, float width, Material material) =>
            Cube(parent, name, (start + end) / 2f, new Vector3(width, width, Vector3.Distance(start, end)),
                Quaternion.LookRotation(end - start, Vector3.up), material);

        static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale,
            Quaternion rotation, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localRotation = rotation;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        static Transform Group(Transform parent, string name)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            return item.transform;
        }

        static float Horizontal(Vector3 value) => new Vector2(value.x, value.z).magnitude;

        static void Line(List<Vector3> feet, Vector3 start, Vector3 end)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(Horizontal(end - start) / RouteSpacing));
            for (int i = 0; i <= samples; i++) Add(feet, Vector3.Lerp(start, end, (float)i / samples));
        }

        static void Add(List<Vector3> feet, Vector3 value)
        {
            if (feet.Count == 0 || (feet[feet.Count - 1] - value).sqrMagnitude > .00000001f) feet.Add(value);
        }

        static Route WorldRoute(Transform parent, string name, List<Vector3> localFeet)
        {
            var worldFeet = new Vector3[localFeet.Count];
            for (int i = 0; i < worldFeet.Length; i++) worldFeet[i] = parent.TransformPoint(localFeet[i]);
            return new Route { name = name, feet = worldFeet, requireAscending = true };
        }
    }
}
