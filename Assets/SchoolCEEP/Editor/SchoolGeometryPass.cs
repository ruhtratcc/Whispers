// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    /// <summary>Exact collinear union after authorized layout corrections. Never invents an opening or room boundary.</summary>
    public static class SchoolGeometryPass
    {
        public const int SourceRevision = 1;
        public const string BaselineFile = "School-geometry-baseline.json";
        public const string ReportFile = "School-geometry-report.json";
        public const string ProofFile = "School-geometry-proof.json";
        const double Scale = .07055555556;
        const string HighWindow = "photo west high window strip";

        [Serializable] public sealed class Merge
        {
            public int level, beforeCount, afterCount;
            public string block, source;
            public bool vertical;
            public float p, thickness;
            public float[] beforeIntervals, afterIntervals;
        }
        [Serializable] public sealed class Overlap
        {
            public int level, firstIndex, secondIndex;
            public string firstBlock, secondBlock, kind;
            public bool firstVertical, secondVertical;
            public float firstAxis, secondAxis, overlapXMeters, overlapZMeters;
        }
        [Serializable] public sealed class BuildReport
        {
            public int sourceRevision = SourceRevision, spansBefore, spansAfter, redundantSpansRemoved;
            public int collinearOverlapPairsBefore, collinearOverlapPairsAfter, parallelOverlapPairsRemaining, perpendicularJunctionPairs;
            public int doorSlotsBefore, doorSlotsAfter, windowsBefore, windowsAfter, rejectedProfileChanges;
            public bool exactSolidCoveragePreserved, doorSlotsAndWindowsUnchanged;
            public double differingProfileVolumeCubicMeters;
            public string baselineFile, originalWallDigest, normalizedWallDigest, apertureDigest;
            public Merge[] merges;
            public Overlap[] overlapsRemaining;
            public string[] notes;
        }
        [Serializable] public sealed class Baseline
        {
            public int sourceRevision = SourceRevision;
            public string scope, apertureDigest;
            public SchoolMapBuilder.Layout before, after;
            public BuildReport build;
        }
        [Serializable] public sealed class NativeGap
        {
            public int level, spanIndex;
            public string block, detail;
            public Vector3 point;
        }
        [Serializable] public sealed class RoomEdge
        {
            public string side;
            public int samples, supportedBoundarySamples, registeredApertureSamples, unresolvedSamples;
            public Vector3[] unresolvedPoints;
        }
        [Serializable] public sealed class InteriorMasonry
        {
            public string hierarchyPath;
            public Vector3 center, size;
        }
        [Serializable] public sealed class CapsuleSettings
        {
            public float radiusMeters, heightMeters, skinWidthMeters, stepOffsetMeters, slopeLimitDegrees;
            public Vector3 localCenter, worldCenterOffset, sourceScale;
        }
        [Serializable] public sealed class RoomEvidence
        {
            public string name, kind, block, interpretation;
            public int level, interiorMasonryCandidates;
            public float[] adaptedPdfBox;
            public bool clearSupportedStandingPoint;
            public Vector3 standingPoint;
            public RoomEdge[] edges;
            public InteriorMasonry[] interiorMasonry;
        }
        [Serializable] public sealed class ProofReport
        {
            public int sourceRevision = SourceRevision;
            public string scene, baselineFile;
            public bool passed, normalizationPassed, editorPhysicsOnly = true, gameplayExecuted, checkpointTouched;
            public bool apertureArraysPreserved, exactSolidCoveragePreserved;
            public int redundantSpansRemoved, nativeMasonrySamples, nativeMasonryGaps, rooms, roomsWithoutClearSupport, roomBoundaryCandidates;
            public double independentlyMeasuredProfileDifferenceCubicMeters;
            public NativeGap[] missingNativeMasonry;
            public RoomEvidence[] roomEvidence;
            public CapsuleSettings capsule;
            public string[] notes;
        }
        sealed class IndexedSpan { public int index; public SchoolMapBuilder.Span span; }
        sealed class Solid { public double a, b, low, high; }

        /// <summary>Call once after every Prepare/remap/filter, immediately before generating the scene.</summary>
        public static BuildReport Prepare(SchoolMapBuilder.Layout layout)
        {
            if (layout?.levels == null) throw new ArgumentNullException(nameof(layout));
            var before = Clone(layout);
            string apertureBefore = ApertureDigest(before);
            var changes = new List<Merge>();
            int rejected = 0;
            foreach (var level in layout.levels)
            {
                var originals = level.walls.Select((s, i) => new IndexedSpan { index = i, span = s }).ToArray();
                var output = new List<IndexedSpan>();
                // Exact axis/thickness keys. Nearby parallel CAD faces remain separate.
                foreach (var group in originals.GroupBy(s => Key(s.span)))
                {
                    var input = group.OrderBy(s => s.span.a).ThenBy(s => s.span.b).ToArray();
                    var merged = new List<SchoolMapBuilder.Span>();
                    foreach (var item in input)
                    {
                        var s = item.span;
                        // A positive gap, however small, is never filled by this pass.
                        if (merged.Count > 0 && s.a <= merged[merged.Count - 1].b)
                            merged[merged.Count - 1].b = Mathf.Max(merged[merged.Count - 1].b, s.b);
                        else merged.Add(Copy(s));
                    }
                    bool reduced = merged.Count < input.Length;
                    // Independent solid-profile comparison also accounts for BuildWall's
                    // 0.08 PDF cutoff and the two different registered window heights.
                    if (reduced && ProfileDifference(input.Select(s => s.span).ToArray(), merged.ToArray(), level.windows) > 1e-10)
                    {
                        output.AddRange(input); rejected++; continue;
                    }
                    int order = input.Min(s => s.index);
                    foreach (var s in merged) output.Add(new IndexedSpan { index = order, span = s });
                    if (reduced)
                    {
                        var s = input[0].span;
                        changes.Add(new Merge { level = level.index, block = s.block, vertical = s.vertical, p = s.p,
                            thickness = s.thickness, source = string.Join("; ", input.Select(x => x.span.source).Distinct()),
                            beforeCount = input.Length, afterCount = merged.Count,
                            beforeIntervals = input.SelectMany(x => new[] { x.span.a, x.span.b }).ToArray(),
                            afterIntervals = merged.SelectMany(x => new[] { x.a, x.b }).ToArray() });
                    }
                }
                level.walls = output.OrderBy(s => s.index).Select(s => s.span).ToArray();
            }
            var after = Clone(layout);
            var priorOverlaps = Overlaps(before);
            var remaining = Overlaps(after);
            double difference = LayoutProfileDifference(before, after);
            var report = new BuildReport {
                spansBefore = before.levels.Sum(l => l.walls.Length), spansAfter = after.levels.Sum(l => l.walls.Length),
                collinearOverlapPairsBefore = priorOverlaps.Count(o => o.kind == "same-axis overlap"),
                collinearOverlapPairsAfter = remaining.Count(o => o.kind == "same-axis overlap"),
                parallelOverlapPairsRemaining = remaining.Count(o => o.kind == "parallel-face overlap candidate"),
                perpendicularJunctionPairs = remaining.Count(o => o.kind == "perpendicular junction"),
                doorSlotsBefore = before.levels.Sum(l => l.doors.Length), doorSlotsAfter = after.levels.Sum(l => l.doors.Length),
                windowsBefore = before.levels.Sum(l => l.windows.Length), windowsAfter = after.levels.Sum(l => l.windows.Length),
                rejectedProfileChanges = rejected, exactSolidCoveragePreserved = difference < 1e-10,
                differingProfileVolumeCubicMeters = difference, doorSlotsAndWindowsUnchanged = apertureBefore == ApertureDigest(after),
                baselineFile = SchoolMapBuilder.Output + "/" + BaselineFile,
                originalWallDigest = WallDigest(before), normalizedWallDigest = WallDigest(after), apertureDigest = apertureBefore,
                merges = changes.ToArray(), overlapsRemaining = remaining.ToArray(), notes = new[] {
                    "Original baseline is the prepared layout after authorized removals, apertures and coordinate adaptations, before normalization.",
                    "Only exactly equal block, floor, orientation, axis, thickness and provisional state can merge. Positive gaps remain gaps.",
                    "Per-axis generated solid profiles are equal before/after, including window heads/sills and the renderer's short-segment cutoff.",
                    "Door/window arrays retain their original order, values and persistent door slots. Parallel faces and perpendicular joints are reported, not removed.",
                    "Overlap candidates alone are not defects: paired CAD faces, structural joints and independently registered apertures require native review." } };
            report.redundantSpansRemoved = report.spansBefore - report.spansAfter;
            if (!report.exactSolidCoveragePreserved || !report.doorSlotsAndWindowsUnchanged)
                throw new InvalidOperationException("Collinear normalization changed occupied coverage or a registered aperture.");
            var baseline = new Baseline { scope = "Prepared layout, before exact wall union. Reference plans on disk are unchanged.",
                apertureDigest = apertureBefore, before = before, after = after, build = report };
            Directory.CreateDirectory(SchoolMapBuilder.Output);
            File.WriteAllText(report.baselineFile, JsonUtility.ToJson(baseline, true));
            File.WriteAllText(SchoolMapBuilder.Output + "/" + ReportFile, JsonUtility.ToJson(report, true));
            return report;
        }

        static SchoolMapBuilder.Layout Clone(SchoolMapBuilder.Layout source) =>
            JsonUtility.FromJson<SchoolMapBuilder.Layout>(JsonUtility.ToJson(source));
        static SchoolMapBuilder.Span Copy(SchoolMapBuilder.Span s) => new SchoolMapBuilder.Span {
            block = s.block, source = s.source, vertical = s.vertical, provisional = s.provisional,
            p = s.p, a = s.a, b = s.b, thickness = s.thickness, width = s.width };
        static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        static string Key(SchoolMapBuilder.Span s) => s.block + "|" + s.vertical + "|" + Number(s.p) + "|" + Number(s.thickness) + "|" + s.provisional;
        static string Digest(string value)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
        static string WallDigest(SchoolMapBuilder.Layout l) => Digest(string.Join("\n", l.levels.SelectMany(level =>
            level.walls.Select(w => level.index + "|" + Key(w) + "|" + Number(w.a) + "|" + Number(w.b)))));
        static string ApertureDigest(SchoolMapBuilder.Layout l) => Digest(string.Join("\n", l.levels.Select(level =>
            JsonUtility.ToJson(new SchoolMapBuilder.Level { index = level.index, doors = level.doors, windows = level.windows }))));

        static Solid[] Solids(SchoolMapBuilder.Span s, SchoolMapBuilder.Span[] windows)
        {
            var solids = new List<Solid>();
            Action<double, double, double, double> add = (a, b, low, high) => {
                if (b - a >= .08 && high - low >= .02) solids.Add(new Solid { a = a, b = b, low = low, high = high }); };
            double cursor = s.a;
            foreach (var window in windows.Where(w => w.block == s.block && w.vertical == s.vertical && Mathf.Abs(w.p - s.p) < 4
                && w.b > s.a && w.a < s.b).OrderBy(w => w.a))
            {
                double a = Math.Max(cursor, window.a), b = Math.Min(s.b, window.b);
                if (b <= a) continue;
                add(cursor, a, 0, 3.35);
                bool high = window.source == HighWindow;
                add(a, b, 0, high ? 2.35 : 1.05);
                add(a, b, high ? 3.08 : 2.58, 3.35);
                cursor = b;
            }
            add(cursor, s.b, 0, 3.35);
            return solids.ToArray();
        }
        // Endpoint-cell integration is independent of the interval-union implementation.
        static double ProfileDifference(SchoolMapBuilder.Span[] before, SchoolMapBuilder.Span[] after, SchoolMapBuilder.Span[] windows)
        {
            var left = before.SelectMany(s => Solids(s, windows)).ToArray();
            var right = after.SelectMany(s => Solids(s, windows)).ToArray();
            var all = left.Concat(right).ToArray();
            if (all.Length == 0) return 0;
            var ys = all.SelectMany(s => new[] { s.low, s.high }).Distinct().OrderBy(v => v).ToArray();
            var xs = all.SelectMany(s => new[] { s.a, s.b }).Distinct().OrderBy(v => v).ToArray();
            double difference = 0, thickness = Math.Max(.14, (before.FirstOrDefault() ?? after[0]).thickness * Scale);
            for (int y = 1; y < ys.Length; y++) for (int x = 1; x < xs.Length; x++)
            {
                double h = (ys[y - 1] + ys[y]) * .5, a = (xs[x - 1] + xs[x]) * .5;
                bool was = left.Any(s => a > s.a && a < s.b && h > s.low && h < s.high);
                bool now = right.Any(s => a > s.a && a < s.b && h > s.low && h < s.high);
                if (was != now) difference += (xs[x] - xs[x - 1]) * Scale * thickness * (ys[y] - ys[y - 1]);
            }
            return difference;
        }
        static double LayoutProfileDifference(SchoolMapBuilder.Layout before, SchoolMapBuilder.Layout after)
        {
            double difference = 0;
            foreach (var level in before.levels)
            {
                var other = after.levels.Single(l => l.index == level.index);
                var keys = level.walls.Concat(other.walls).Select(Key).Distinct();
                foreach (string key in keys) difference += ProfileDifference(level.walls.Where(s => Key(s) == key).ToArray(),
                    other.walls.Where(s => Key(s) == key).ToArray(), level.windows);
            }
            return difference;
        }
        static List<Overlap> Overlaps(SchoolMapBuilder.Layout layout)
        {
            var found = new List<Overlap>();
            foreach (var level in layout.levels) for (int i = 0; i < level.walls.Length; i++) for (int j = i + 1; j < level.walls.Length; j++)
            {
                var a = level.walls[i]; var b = level.walls[j];
                double ta = Math.Max(.14 / Scale, a.thickness) * .5, tb = Math.Max(.14 / Scale, b.thickness) * .5;
                double ax0 = a.vertical ? a.p - ta : a.a, ax1 = a.vertical ? a.p + ta : a.b;
                double ay0 = a.vertical ? a.a : a.p - ta, ay1 = a.vertical ? a.b : a.p + ta;
                double bx0 = b.vertical ? b.p - tb : b.a, bx1 = b.vertical ? b.p + tb : b.b;
                double by0 = b.vertical ? b.a : b.p - tb, by1 = b.vertical ? b.b : b.p + tb;
                double ox = Math.Min(ax1, bx1) - Math.Max(ax0, bx0), oy = Math.Min(ay1, by1) - Math.Max(ay0, by0);
                if (ox <= .001 || oy <= .001) continue;
                found.Add(new Overlap { level = level.index, firstIndex = i, secondIndex = j, firstBlock = a.block, secondBlock = b.block,
                    firstVertical = a.vertical, secondVertical = b.vertical, firstAxis = a.p, secondAxis = b.p,
                    overlapXMeters = (float)(ox * Scale), overlapZMeters = (float)(oy * Scale),
                    kind = a.vertical != b.vertical ? "perpendicular junction" : a.block == b.block && a.p == b.p ?
                        "same-axis overlap" : "parallel-face overlap candidate" });
            }
            return found;
        }

        /// <summary>Read-only native evidence; snapshots from Prepare are independently loaded from disk.</summary>
        public static ProofReport Inspect(Scene scene)
        {
            if (scene.path != SchoolIntegrationPass.ScenePath) throw new InvalidOperationException("Inspect the saved school scene.");
            var file = SchoolMapBuilder.Output + "/" + BaselineFile;
            var baseline = JsonUtility.FromJson<Baseline>(File.ReadAllText(file));
            if (baseline?.before?.levels == null || baseline.after?.levels == null || baseline.build == null)
                throw new InvalidDataException("Missing prepared-layout normalization baseline.");
            Physics.SyncTransforms();
            var player = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HorrorGame.FirstPersonMotor>(true)).Single();
            var controller = player.GetComponent<CharacterController>();
            if (!controller) throw new InvalidOperationException("The scene player has no CharacterController.");
            var scale = controller.transform.lossyScale;
            float radialScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var capsule = new CapsuleSettings {
                radiusMeters = controller.radius * radialScale,
                heightMeters = Mathf.Max(controller.height * Mathf.Abs(scale.y), controller.radius * radialScale * 2),
                skinWidthMeters = controller.skinWidth * Mathf.Max(radialScale, Mathf.Abs(scale.y)),
                stepOffsetMeters = controller.stepOffset * Mathf.Abs(scale.y), slopeLimitDegrees = controller.slopeLimit,
                localCenter = controller.center, worldCenterOffset = controller.transform.TransformVector(controller.center), sourceScale = scale };
            var colliders = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Collider>(true))
                .Where(c => Relevant(c, scene)).ToArray();
            var masonry = colliders.Where(Masonry).ToArray();
            var gaps = new List<NativeGap>();
            int samples = 0;
            foreach (var level in baseline.after.levels) for (int index = 0; index < level.walls.Length; index++)
            {
                var wall = level.walls[index];
                float floor = SchoolMapBuilder.OccupiedFloorHeight(wall.block, level.index);
                foreach (var solid in Solids(wall, level.windows))
                {
                    float height = (float)((solid.low + solid.high) * .5);
                    int n = Math.Max(1, (int)Math.Ceiling((solid.b - solid.a) * Scale / .75));
                    for (int i = 0; i < n; i++)
                    {
                        float along = (float)(solid.a + (solid.b - solid.a) * (i + .5) / n);
                        var point = wall.vertical ? SchoolMapBuilder.Point(wall.p, along, floor + height) : SchoolMapBuilder.Point(along, wall.p, floor + height);
                        if (RegisteredAperture(baseline.after, level.index, wall.block, point, .16f, true)) continue;
                        samples++;
                        var normal = wall.vertical ? Vector3.right : Vector3.forward;
                        float reach = Mathf.Max(.14f, wall.thickness * (float)Scale) * .5f + .03f;
                        if (!masonry.Any(c => ContainsMasonry(c, point, normal, reach)))
                            gaps.Add(new NativeGap { level = level.index, spanIndex = index, block = wall.block, point = point,
                                detail = "Prepared source expects solid masonry here; native collider evidence is absent. Review authorized later replacements before repairing." });
                    }
                }
            }
            var data = JsonUtility.FromJson<SchoolMapBuilder.Rooms>(File.ReadAllText(SchoolMapBuilder.Folder + "/Data/rooms.json"));
            SchoolCampusConnectionsPass.AdaptRooms(data);
            var rooms = data.rooms.Select(r => InspectRoom(r, baseline.after, scene, masonry, capsule)).ToArray();
            double difference = LayoutProfileDifference(baseline.before, baseline.after);
            bool apertures = ApertureDigest(baseline.before) == baseline.apertureDigest && ApertureDigest(baseline.after) == baseline.apertureDigest;
            bool normalization = difference < 1e-10 && apertures && WallDigest(baseline.before) == baseline.build.originalWallDigest
                && WallDigest(baseline.after) == baseline.build.normalizedWallDigest;
            var report = new ProofReport { scene = scene.path, baselineFile = file, normalizationPassed = normalization,
                apertureArraysPreserved = apertures, exactSolidCoveragePreserved = difference < 1e-10,
                independentlyMeasuredProfileDifferenceCubicMeters = difference, redundantSpansRemoved = baseline.build.redundantSpansRemoved,
                nativeMasonrySamples = samples, nativeMasonryGaps = gaps.Count, missingNativeMasonry = gaps.ToArray(),
                rooms = rooms.Length, roomsWithoutClearSupport = rooms.Count(r => !r.clearSupportedStandingPoint), roomEvidence = rooms, capsule = capsule,
                roomBoundaryCandidates = rooms.Sum(r => r.edges.Sum(e => e.unresolvedSamples)),
                notes = new[] {
                    "Coverage proof reloads both prepared original and normalized snapshots; endpoint-cell occupancy integration does not use the merging algorithm.",
                    "Native masonry probes exclude registered door/window apertures and compare against actual enabled physical colliders, without moving doors or geometry.",
                    "Room boxes are approximate. Unresolved perimeter samples and interior walls are evidence for plan review, not permission to invent an enclosure or remove a wall.",
                    "Known open workshop fronts/lateral access, roof-only regions and corridor connections are not assumed to be closed rooms.",
                    "This report does not replace capsule traversal, door swing, double-door synchronization or the established whole-map route audit.",
                    "No Play, gameplay, checkpoint, door pose, lighting or permanent scene change." } };
            report.passed = normalization && gaps.Count == 0 && report.roomsWithoutClearSupport == 0;
            File.WriteAllText(SchoolMapBuilder.Output + "/" + ProofFile, JsonUtility.ToJson(report, true));
            return report;
        }
        static bool Relevant(Collider c, Scene scene) => c && c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy
            && c.gameObject.scene == scene && !c.GetComponentInParent<CharacterController>();
        static bool Masonry(Collider c) => c.name.StartsWith("Parede") || c.name == "Peitoril" || c.name.StartsWith("Verga")
            || c.name.IndexOf("alvenaria", StringComparison.OrdinalIgnoreCase) >= 0;
        static bool ContainsMasonry(Collider c, Vector3 point, Vector3 normal, float reach)
        {
            if (!c.bounds.Contains(point)) return false;
            // ClosestPoint is unsupported on a non-convex MeshCollider. Probe its
            // actual surface across the wall thickness instead of its AABB alone.
            if (c is MeshCollider mesh && !mesh.convex)
                return c.Raycast(new Ray(point - normal * reach, normal), out _, reach * 2)
                    || c.Raycast(new Ray(point + normal * reach, -normal), out _, reach * 2);
            return (c.ClosestPoint(point) - point).sqrMagnitude < .000001f;
        }
        static bool RegisteredAperture(SchoolMapBuilder.Layout layout, int level, string block, Vector3 point, float margin, bool doorsOnly = false)
        {
            var floor = layout.levels.Single(l => l.index == level);
            var spans = doorsOnly ? floor.doors : floor.doors.Concat(floor.windows);
            foreach (var span in spans)
            {
                if (span.block != block) continue;
                var a = span.vertical ? SchoolMapBuilder.Point(span.p, span.a) : SchoolMapBuilder.Point(span.a, span.p);
                var b = span.vertical ? SchoolMapBuilder.Point(span.p, span.b) : SchoolMapBuilder.Point(span.b, span.p);
                var q = new Vector3(point.x, 0, point.z);
                float t = Mathf.Clamp01(Vector3.Dot(q - a, b - a) / Mathf.Max(.0001f, (b - a).sqrMagnitude));
                if (Vector3.Distance(q, Vector3.Lerp(a, b, t)) <= margin) return true;
            }
            return false;
        }
        static RoomEvidence InspectRoom(SchoolMapBuilder.Room room, SchoolMapBuilder.Layout layout, Scene scene, Collider[] masonry, CapsuleSettings capsule)
        {
            float cx = (room.box[0] + room.box[2]) * .5f, cy = (room.box[1] + room.box[3]) * .5f;
            var region = layout.regions.FirstOrDefault(r => cx >= r.box[0] && cx <= r.box[2] && cy >= r.box[1] && cy <= r.box[3]);
            string block = region?.name ?? "unregistered";
            var box = region == null ? room.box : SchoolMapBuilder.CorridorBox(block, room.level, room.box);
            float floor = SchoolMapBuilder.OccupiedFloorHeight(block, room.level);
            var nw = SchoolMapBuilder.Point(box[0], box[1], floor);
            var se = SchoolMapBuilder.Point(box[2], box[3], floor);
            var bounds = new Bounds((nw + se) * .5f, new Vector3(se.x - nw.x, 0, nw.z - se.z));
            var result = new RoomEvidence { name = room.name, level = room.level, kind = room.kind, block = block, adaptedPdfBox = box,
                interpretation = room.name.StartsWith("Oficina") || room.name.StartsWith("Ala técnica") ?
                    "Coarse workshop/laboratory box; compare named CAD partitions and open sides before changing geometry." :
                    "Approximate registered room box; boundary probes distinguish native masonry from registered apertures." };
            foreach (float x in new[] { .5f, .3f, .7f }) foreach (float y in new[] { .5f, .3f, .7f })
            {
                if (result.clearSupportedStandingPoint) continue;
                var foot = SchoolMapBuilder.Point(Mathf.Lerp(box[0], box[2], x), Mathf.Lerp(box[1], box[3], y), floor + .04f);
                var support = Physics.RaycastAll(foot + Vector3.up * .3f, Vector3.down, .65f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => Relevant(h.collider, scene) && Vector3.Angle(h.normal, Vector3.up) <= capsule.slopeLimitDegrees + 1
                        && Mathf.Abs(h.point.y - floor) < .27f).OrderBy(h => h.distance).ToArray();
                if (support.Length == 0) continue;
                // Position the actual controller origin above this support, accounting
                // for its center/bottom offset. Skin is not subtracted from its radius.
                float clearance = Mathf.Clamp(capsule.skinWidthMeters * .5f, .015f, .04f);
                foot.y = support[0].point.y + clearance - (capsule.worldCenterOffset.y - capsule.heightMeters * .5f);
                var center = foot + capsule.worldCenterOffset;
                float cylinderHalf = Mathf.Max(0, capsule.heightMeters * .5f - capsule.radiusMeters);
                if (Physics.OverlapCapsule(center - Vector3.up * cylinderHalf, center + Vector3.up * cylinderHalf,
                    capsule.radiusMeters, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => Relevant(c, scene))) continue;
                result.clearSupportedStandingPoint = true; result.standingPoint = foot;
            }
            var edges = new List<RoomEdge>();
            foreach (string side in new[] { "north", "south", "west", "east" })
            {
                bool horizontal = side == "north" || side == "south";
                var outward = side == "north" ? Vector3.forward : side == "south" ? Vector3.back : side == "west" ? Vector3.left : Vector3.right;
                float length = horizontal ? bounds.size.x : bounds.size.z;
                int n = Mathf.Max(1, Mathf.CeilToInt(length / .45f));
                var edge = new RoomEdge { side = side, samples = n };
                var unresolved = new List<Vector3>();
                for (int i = 0; i < n; i++)
                {
                    float t = (i + .5f) / n;
                    var point = new Vector3(horizontal ? Mathf.Lerp(nw.x, se.x, t) : side == "west" ? nw.x : se.x,
                        floor + 1.55f, horizontal ? side == "north" ? nw.z : se.z : Mathf.Lerp(nw.z, se.z, t));
                    if (RegisteredAperture(layout, room.level, block, point, .65f)) { edge.registeredApertureSamples++; continue; }
                    var start = point - outward * .75f;
                    if (Physics.RaycastAll(start, outward, 1.5f, ~0, QueryTriggerInteraction.Ignore).Any(h => Relevant(h.collider, scene) && Masonry(h.collider)))
                        edge.supportedBoundarySamples++;
                    else { edge.unresolvedSamples++; unresolved.Add(point); }
                }
                edge.unresolvedPoints = unresolved.ToArray();
                edges.Add(edge);
            }
            result.edges = edges.ToArray();
            // Only compare actual masonry against the interior of the approximate box.
            var interior = bounds; interior.Expand(new Vector3(-1f, 0, -1f));
            result.interiorMasonry = masonry.Where(c => {
                var b = c.bounds;
                if (b.min.y > floor + 1.6f || b.max.y < floor + 1.6f) return false;
                bool v = b.size.z > b.size.x;
                float axis = v ? b.center.x : b.center.z;
                float lo = v ? interior.min.x : interior.min.z, hi = v ? interior.max.x : interior.max.z;
                float overlap = v ? Mathf.Min(b.max.z, interior.max.z) - Mathf.Max(b.min.z, interior.min.z) :
                    Mathf.Min(b.max.x, interior.max.x) - Mathf.Max(b.min.x, interior.min.x);
                return axis > lo && axis < hi && overlap > .8f;
            }).Select(c => new InteriorMasonry { hierarchyPath = HierarchyPath(c.transform), center = c.bounds.center, size = c.bounds.size }).ToArray();
            result.interiorMasonryCandidates = result.interiorMasonry.Length;
            return result;
        }
        static string HierarchyPath(Transform t)
        {
            var parts = new List<string>();
            while (t) { parts.Add(t.name); t = t.parent; }
            parts.Reverse(); return string.Join("/", parts);
        }
    }
}
