// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>Restore short column footprints omitted by the minimum-length CAD wall extractor.</summary>
    public static class SchoolColumnRepairPass
    {
        public const int SourceRevision = 1;
        public const string Source = "verified omitted small CAD column";
        public const string ReportFile = "School-column-repairs-report.json";
        public const string ProofFile = "School-column-repairs-proof.json";
        const float Scale = .07055555556f;
        const float AxisTolerancePdf = .16f, EndpointTolerancePdf = .045f;
        static readonly int[] VerifiedWestRectangles = { 36, 43, 47, 51, 55, 59, 63, 67, 71, 78, 80 };

        [Serializable] public sealed class Column
        {
            public int level, pdfRectangleIndex, beforeWallSlot = -1, afterWallSlot = -1;
            public string block, sourcePdf, sourceGeometry;
            public bool vertical, verifiedTerminal;
            public float axisPdf, startPdf, endPdf, thicknessPdf, physicalThicknessMeters, lengthMeters;
            public float pdfStrokeWidth, pdfX0, pdfX1, pdfTop, pdfBottom, registeredX0, registeredX1, registeredTop, registeredBottom;
            public float registrationX, registrationY;
        }
        [Serializable] public sealed class Rejection
        {
            public int level, pdfRectangleIndex;
            public string block, reason;
            public bool vertical;
            public float axisPdf, startPdf, endPdf, nearbyWallAxisPdf;
        }
        [Serializable] public sealed class ApertureSnapshot
        {
            public int level;
            public SchoolMapBuilder.Span[] doors, windows;
        }
        [Serializable] public sealed class ApertureSet { public ApertureSnapshot[] levels; }
        [Serializable] public sealed class Report
        {
            public int sourceRevision = SourceRevision, rectanglesInspected, smallCadRectangles, matchedCandidates;
            public int addedColumns, newMasonrySpans, rejectedApertureCandidates, axisOffsetCandidatesSkipped, duplicateCadRectangles;
            public int doorSlotsBefore, doorSlotsAfter, windowsBefore, windowsAfter;
            public bool originalDoorArraysPreserved, originalWindowArraysPreserved, noRegisteredApertureIntersected, noUnverifiedGapFilled;
            public string apertureDigestBefore, apertureDigestAfter;
            public Column[] columns;
            public Rejection[] rejectedCandidates;
            public ApertureSnapshot[] aperturesBefore, aperturesAfter;
            public string[] notes;
        }
        [Serializable] sealed class Geometry { public CadRectangle[] rects; }
        [Serializable] sealed class CadRectangle { public float x0, x1, top, bottom, linewidth; }

        /// <summary>Run once before ApplyCorridorWidths and the campus-coordinate remap.</summary>
        public static Report Prepare(SchoolMapBuilder.Layout layout)
        {
            if (layout?.levels == null || layout.regions == null) throw new ArgumentNullException(nameof(layout));
            if (layout.levels.Any(l => l.walls.Any(w => w.source == Source)))
                throw new InvalidOperationException("CAD column repair must run once before layout coordinate adaptation.");
            var report = new Report { aperturesBefore = Snapshot(layout) };
            report.doorSlotsBefore = report.aperturesBefore.Sum(l => l.doors.Length);
            report.windowsBefore = report.aperturesBefore.Sum(l => l.windows.Length);
            var columns = new List<Column>();
            var rejected = new List<Rejection>();
            var keys = new HashSet<string>();
            foreach (var level in layout.levels.Where(l => l.index == 0 || l.index == 1))
            {
                string geometryPath = Path.GetFullPath(Path.Combine(SchoolMapBuilder.Output, "../work/SchoolCEEP/P0" + (level.index + 1) + "-1-geometry.json"));
                var geometry = JsonUtility.FromJson<Geometry>(File.ReadAllText(geometryPath));
                if (geometry?.rects == null) throw new InvalidDataException("Missing original CAD rectangles: " + geometryPath);
                float dx = level.index == 1 ? -28.08f : 0, dy = level.index == 1 ? 27.72f : 0;
                var originalWalls = level.walls;
                for (int i = 0; i < geometry.rects.Length; i++)
                {
                    var r = geometry.rects[i]; report.rectanglesInspected++;
                    float x0 = r.x0 + dx, x1 = r.x1 + dx, y0 = r.top + dy, y1 = r.bottom + dy;
                    float width = x1 - x0, length = y1 - y0;
                    if (Mathf.Abs(r.linewidth - .72f) > .025f || Mathf.Min(width, length) < .8f
                        || Mathf.Max(width, length) * Scale > .30f) continue;
                    report.smallCadRectangles++;
                    foreach (var region in layout.regions.Where(g => (level.index == 0 || g.upper)
                        && (x0 + x1) * .5f >= g.box[0] - 3 && (x0 + x1) * .5f <= g.box[2] + 3
                        && (y0 + y1) * .5f >= g.box[1] - 3 && (y0 + y1) * .5f <= g.box[3] + 3))
                    foreach (bool vertical in new[] { true, false })
                    {
                        float axis = vertical ? (x0 + x1) * .5f : (y0 + y1) * .5f;
                        float a = vertical ? y0 : x0, b = vertical ? y1 : x1;
                        float thickness = vertical ? width : length;
                        if ((b - a) * Scale > .25f) continue;
                        var sameDirection = originalWalls.Select((w, slot) => new { wall = w, slot }).Where(v =>
                            !v.wall.provisional && v.wall.block == region.name && v.wall.vertical == vertical).ToArray();
                        var before = sameDirection.Where(v => Mathf.Abs(v.wall.p - axis) < AxisTolerancePdf
                            && Mathf.Abs(v.wall.b - a) < EndpointTolerancePdf).ToArray();
                        var after = sameDirection.Where(v => Mathf.Abs(v.wall.p - axis) < AxisTolerancePdf
                            && Mathf.Abs(v.wall.a - b) < EndpointTolerancePdf).ToArray();
                        bool terminal = level.index == 0 && region.name == "west_south" && vertical
                            && (i == 36 || i == 78) && Mathf.Abs(axis - 368.92f) < .02f;
                        if ((before.Length == 0 || after.Length == 0) && !(terminal && before.Length + after.Length > 0))
                        {
                            // A column offset from a nearby wall axis is not evidence for filling that wall's gap.
                            var offsetBefore = sameDirection.Where(v => Mathf.Abs(v.wall.p - axis) < 2
                                && Mathf.Abs(v.wall.b - a) < EndpointTolerancePdf).ToArray();
                            var offsetAfter = sameDirection.Where(v => Mathf.Abs(v.wall.p - axis) < 2
                                && Mathf.Abs(v.wall.a - b) < EndpointTolerancePdf).ToArray();
                            if (offsetBefore.Length > 0 && offsetAfter.Length > 0)
                            {
                                report.axisOffsetCandidatesSkipped++;
                                rejected.Add(Reject(level.index, i, region.name, vertical, axis, a, b,
                                    "CAD rectangle center does not match the missing collinear wall axis; not an authorized generic joint fill.", offsetBefore[0].wall.p));
                            }
                            continue;
                        }
                        report.matchedCandidates++;
                        string key = level.index + "|" + region.name + "|" + vertical + "|" + Number(axis) + "|" + Number(a) + "|" + Number(b);
                        if (!keys.Add(key))
                        {
                            report.duplicateCadRectangles++;
                            rejected.Add(Reject(level.index, i, region.name, vertical, axis, a, b, "Duplicate PDF rectangle footprint."));
                            continue;
                        }
                        bool doorway = level.doors.Any(d => d.block == region.name && d.vertical == vertical
                            && Mathf.Abs(d.p - axis) < 6 && DoorEnd(d, level.index) > a && DoorStart(d, level.index) < b);
                        bool window = level.windows.Any(w => w.block == region.name && w.vertical == vertical
                            && Mathf.Abs(w.p - axis) < 4 && Mathf.Min(w.b, b) - Mathf.Max(w.a, a) > .02f);
                        if (doorway || window)
                        {
                            report.rejectedApertureCandidates++;
                            rejected.Add(Reject(level.index, i, region.name, vertical, axis, a, b, "Registered doorway/window clearance overlaps this footprint; no column added."));
                            continue;
                        }
                        if (originalWalls.Any(w => w.block == region.name && w.vertical == vertical && Mathf.Abs(w.p - axis) < AxisTolerancePdf
                            && Mathf.Min(w.b, b) - Mathf.Max(w.a, a) > .04f))
                        {
                            rejected.Add(Reject(level.index, i, region.name, vertical, axis, a, b, "Existing source masonry already owns this interval."));
                            continue;
                        }
                        columns.Add(new Column { level = level.index, block = region.name, vertical = vertical, verifiedTerminal = terminal,
                            pdfRectangleIndex = i, sourcePdf = "C:/Users/ruhtra/Downloads/EXTINTORES CEEP-P0" + (level.index + 1) + ".pdf",
                            sourceGeometry = geometryPath.Replace('\\', '/'), pdfStrokeWidth = r.linewidth,
                            axisPdf = axis, startPdf = a, endPdf = b, thicknessPdf = thickness,
                            physicalThicknessMeters = thickness * Scale, lengthMeters = (b - a) * Scale,
                            beforeWallSlot = before.Length == 0 ? -1 : before[0].slot, afterWallSlot = after.Length == 0 ? -1 : after[0].slot,
                            pdfX0 = r.x0, pdfX1 = r.x1, pdfTop = r.top, pdfBottom = r.bottom,
                            registeredX0 = x0, registeredX1 = x1, registeredTop = y0, registeredBottom = y1, registrationX = dx, registrationY = dy });
                    }
                }
            }
            foreach (int rectangle in VerifiedWestRectangles)
                if (columns.Count(c => c.level == 0 && c.block == "west_south" && c.vertical && c.pdfRectangleIndex == rectangle) != 1)
                    throw new InvalidOperationException("Confirmed west CAD column was not restored exactly once: P01 rectangle " + rectangle);
            foreach (var level in layout.levels)
                level.walls = level.walls.Concat(columns.Where(c => c.level == level.index).Select(c => new SchoolMapBuilder.Span {
                    block = c.block, vertical = c.vertical, p = c.axisPdf, a = c.startPdf, b = c.endPdf,
                    thickness = c.thicknessPdf, source = Source })).ToArray();
            report.aperturesAfter = Snapshot(layout);
            report.apertureDigestBefore = ApertureDigest(report.aperturesBefore);
            report.apertureDigestAfter = ApertureDigest(report.aperturesAfter);
            report.originalDoorArraysPreserved = report.aperturesBefore.Zip(report.aperturesAfter, (a, b) =>
                a.level == b.level && a.doors.Select(JsonUtility.ToJson).SequenceEqual(b.doors.Select(JsonUtility.ToJson))).All(v => v);
            report.originalWindowArraysPreserved = report.aperturesBefore.Zip(report.aperturesAfter, (a, b) =>
                a.level == b.level && a.windows.Select(JsonUtility.ToJson).SequenceEqual(b.windows.Select(JsonUtility.ToJson))).All(v => v);
            report.doorSlotsAfter = report.aperturesAfter.Sum(l => l.doors.Length);
            report.windowsAfter = report.aperturesAfter.Sum(l => l.windows.Length);
            report.columns = columns.ToArray(); report.rejectedCandidates = rejected.ToArray();
            report.addedColumns = report.newMasonrySpans = columns.Count;
            report.noRegisteredApertureIntersected = true; report.noUnverifiedGapFilled = true;
            report.notes = new[] {
                "All additions have an actual closed 0.72-stroke rectangle in the original native PDF. No small gap is filled solely because perpendicular walls or an approximate room box suggest closure.",
                "The extractor ignored individual CAD edges shorter than seven PDF points. Eleven P01 west_south column rectangles are 2.76/2.88 points long (0.19473/0.20320m), leaving real facade holes between serialized wall spans.",
                "Each added span follows the rectangle center, endpoints and transverse face separation. The two terminal columns are independently confirmed P01 rectangles36/78; other additions require matching non-provisional wall endpoints on both sides.",
                "All source coordinates are the original registered PDF frame. P02 registration is x_add=-28.08/y_add=27.72; the normal corridor and campus passes adapt additions with the original walls exactly once.",
                "Registered doors are checked using at least the current 0.90/1.10m clear width plus jamb allowance. Registered windows are preserved. The two identical P02 proposed_east_teaching rectangles are rejected because an existing aperture owns the interval.",
                "Small rectangles with nearby but shifted wall axes are recorded as diagnostic candidates and retained unchanged. This pass does not infer a full-height missing boundary from those stepped or offset footprints.",
                "Masonry receives the existing floor height, full 3.35m wall profile, lower paint and skirting through the normal native wall builder. P01/P02 plan rectangles establish footprint; vertical dimensions reuse the surrounding wall convention." };
            if (!report.originalDoorArraysPreserved || !report.originalWindowArraysPreserved || report.apertureDigestBefore != report.apertureDigestAfter)
                throw new InvalidOperationException("CAD column repair changed an original registered aperture array.");
            Directory.CreateDirectory(SchoolMapBuilder.Output);
            File.WriteAllText(SchoolMapBuilder.Output + "/" + ReportFile, JsonUtility.ToJson(report, true));
            return report;
        }

        static Rejection Reject(int level, int rectangle, string block, bool vertical, float axis, float a, float b, string reason, float nearby = 0) =>
            new Rejection { level = level, pdfRectangleIndex = rectangle, block = block, vertical = vertical,
                axisPdf = axis, startPdf = a, endPdf = b, reason = reason, nearbyWallAxisPdf = nearby };
        static float DoorHalf(SchoolMapBuilder.Span d, int level) => Mathf.Max(d.b - d.a, (level == 1 ? 1.10f : .90f) / Scale) * .5f + .065f / Scale + 1.5f;
        static float DoorStart(SchoolMapBuilder.Span d, int level) => (d.a + d.b) * .5f - DoorHalf(d, level);
        static float DoorEnd(SchoolMapBuilder.Span d, int level) => (d.a + d.b) * .5f + DoorHalf(d, level);
        static string Number(float n) => n.ToString("F3", CultureInfo.InvariantCulture);
        static ApertureSnapshot[] Snapshot(SchoolMapBuilder.Layout layout) => layout.levels.OrderBy(l => l.index).Select(l =>
            JsonUtility.FromJson<ApertureSnapshot>(JsonUtility.ToJson(new ApertureSnapshot { level = l.index, doors = l.doors, windows = l.windows }))).ToArray();
        public static string ApertureDigest(ApertureSnapshot[] levels)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new ApertureSet { levels = levels })))).Replace("-", "").ToLowerInvariant();
        }
    }
}
