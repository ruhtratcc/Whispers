// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>Restore interior glazing present in the PDFs but excluded by the facade-only sash extractor.</summary>
    public static class SchoolRoomRepairsPass
    {
        public const int SourceRevision = 4;
        public const string Source = "verified interior CAD sash repair";
        public const string SolidSource = "verified omitted CAD room boundary";
        public const string ReportFile = "School-room-repairs-report.json";
        public const string ProofFile = "School-room-repairs-proof.json";
        const string Block = "main_laboratories_north";
        const float Scale = .07055555556f;

        [Serializable] public sealed class Panel
        {
            public int level, pdfRectangleIndex;
            public string block = Block, sourcePdf, sourceGeometry, side;
            public float axisPdf, startPdf, endPdf, masonryThicknessPdf, physicalThicknessMeters;
            public float pdfStrokeWidth = .12f, spanMeters, estimatedSillMeters = 1.05f, estimatedHeadMeters = 2.58f;
            public bool heightsEstimated = true;
        }
        [Serializable] public sealed class Report
        {
            public int sourceRevision = SourceRevision, newInteriorPanels, newMasonrySpans, doorSlotsBefore, doorSlotsAfter;
            public int windowsBefore, windowsAfter, newSolidBoundaries, removedFurnitureBoundaries;
            public bool originalDoorArraysPreserved, originalWindowPrefixesPreserved, noRegisteredDoorwayIntersected;
            public Panel[] panels;
            public SolidBoundary[] solidBoundaries;
            public FurnitureBoundary[] removedBoundaries;
            public string[] notes;
        }
        [Serializable] public sealed class SolidBoundary
        {
            public int level = 1, pdfRectangleIndex = 859, pdfDoorCurveIndex = 5676;
            public string block = Block, room = "Laboratorio 25", sourcePdf = "C:/Users/ruhtra/Downloads/EXTINTORES CEEP-P02.pdf";
            public string sourceGeometry = "work/SchoolCEEP/P02-1-geometry.json";
            public float axisPdf = 831.64f, startPdf = 340.52f, endPdf = 409.64f, masonryThicknessPdf = 1.44f;
            public float pdfStrokeWidth = .72f, physicalThicknessMeters = 1.44f * Scale, nativeMinimumThicknessMeters = .14f;
            public float openStartPdf = 409.64f, openEndPdf = 421.40f;
            public bool noLeafAdded = true;
        }
        [Serializable] public sealed class FurnitureBoundary
        {
            public int level = 1, rawLayoutWallSlot = 210, preparedSourceWallSlot, sourceCurveIndex = -1, sourceRectangleIndex = -1, sourceLineIndex = -1;
            public string block = Block, room, sourcePdf = "C:/Users/ruhtra/Downloads/EXTINTORES CEEP-P02.pdf";
            public string sourceGeometry = "work/SchoolCEEP/P02-1-geometry.json";
            public float axisPdf = 823.84f, startPdf = 340.52f, endPdf = 380.24f, oldMasonryThicknessPdf = 2.13f;
            public string reason = "The native PDF outlines a laboratory workbench or sink; it is not a full-height room partition.";
        }

        /// <summary>Run before ApplyCorridorWidths; registered source axes are mapped exactly once with all original spans.</summary>
        public static Report Prepare(SchoolMapBuilder.Layout layout)
        {
            if (layout?.levels == null) throw new ArgumentNullException(nameof(layout));
            if (layout.levels.Any(l => l.windows.Any(w => w.source == Source) || l.walls.Any(w => w.source == SolidSource)))
                throw new InvalidOperationException("Interior sash repair must run once on the original registered layout.");
            var panels = new List<Panel>();
            foreach (int level in new[] { 0, 1 })
            {
                panels.Add(NewPanel(level, 862.30f, 471.20f, 551.96f, 3.24f, level == 0 ? 981 : 56, "east room bank"));
                panels.Add(NewPanel(level, 862.30f, 600.32f, 681.08f, 3.24f, level == 0 ? 980 : 55, "east room bank"));
            }
            panels.Add(NewPanel(0, 829.60f, 682.88f, 721.76f, 4.08f, 976, "west room bank"));
            panels.Add(NewPanel(0, 829.60f, 554.12f, 592.88f, 4.08f, 977, "west room bank"));
            panels.Add(NewPanel(0, 829.60f, 468.44f, 507.32f, 4.08f, 978, "west room bank"));
            var doorBefore = layout.levels.ToDictionary(l => l.index, l => JsonUtility.ToJson(new SchoolMapBuilder.Level { index = l.index, doors = l.doors }));
            var windowsBefore = layout.levels.ToDictionary(l => l.index, l => l.windows.Select(w => JsonUtility.ToJson(w)).ToArray());
            var report = new Report { doorSlotsBefore = layout.levels.Sum(l => l.doors.Length), windowsBefore = layout.levels.Sum(l => l.windows.Length) };
            var removed = new[] {
                Bench("Laboratorio 25", 210, 823.84f, 340.52f, 380.24f, 2.13f, curve:5684),
                Bench("Laboratorio 24", 208, 820.24f, 447.32f, 507.80f, 2.13f, line:4590),
                Bench("Laboratorio 23", 209, 820.96f, 510.68f, 577.28f, 2.13f, line:4577),
                Bench("Laboratorio 24", 204, 654.16f, 425.84f, 507.56f, 2.13f, rectangle:885),
                Bench("Laboratorio 23", 205, 654.16f, 510.92f, 592.52f, 2.13f, rectangle:886),
                Bench("Sala de Preparos Quimicos", 206, 654.16f, 596.00f, 635.12f, 2.13f, rectangle:887),
                Bench("Laboratorio 22", 202, 653.62f, 638.24f, 721.88f, 2.13f, rectangle:880),
                Bench("Laboratorio 22 - sink inset", 203, 653.62f, 692.96f, 700.04f, 1.80f, rectangle:881) };
            foreach (var boundary in removed)
            {
                var upperSource = layout.levels.Single(l => l.index == boundary.level);
                var falseBoundary = upperSource.walls.Select((w, i) => new { wall = w, index = i }).Where(v =>
                    v.wall.block == Block && v.wall.vertical && Mathf.Abs(v.wall.p - boundary.axisPdf) < .02f
                    && Mathf.Abs(v.wall.a - boundary.startPdf) < .02f && Mathf.Abs(v.wall.b - boundary.endPdf) < .02f
                    && Mathf.Abs(v.wall.thickness - boundary.oldMasonryThicknessPdf) < .02f).ToArray();
                if (falseBoundary.Length != 1) throw new InvalidOperationException("Verified workbench boundary must match one original source span: " + boundary.room);
                boundary.preparedSourceWallSlot = falseBoundary[0].index;
                upperSource.walls = upperSource.walls.Where(w => w != falseBoundary[0].wall).ToArray();
            }
            foreach (var panel in panels)
            {
                var level = layout.levels.Single(l => l.index == panel.level);
                bool registered = level.windows.Any(w => w.block == Block && w.vertical && Mathf.Abs(w.p - panel.axisPdf) < 4
                    && Mathf.Abs(w.a - panel.startPdf) < .5f && Mathf.Abs(w.b - panel.endPdf) < .5f);
                if (registered) throw new InvalidOperationException("Interior sash already registered: " + panel.level + "/" + panel.axisPdf + "/" + panel.startPdf);
                // Doors are preserved in place. No replacement panel may cover a registered doorway, including its widened clearance.
                if (level.doors.Any(d => d.block == Block && d.vertical && Mathf.Abs(d.p - panel.axisPdf) < 6
                    && d.b + 1.5f > panel.startPdf && d.a - 1.5f < panel.endPdf))
                    throw new InvalidOperationException("Interior sash repair intersects a registered doorway.");
                // This is a missing sash, not permission to remove another masonry interval.
                // Existing intervals end at these jambs. Abort if a source wall actually owns the interior.
                if (level.walls.Any(w => w.block == Block && w.vertical && Mathf.Abs(w.p - panel.axisPdf) < 4
                    && Mathf.Min(w.b, panel.endPdf) - Mathf.Max(w.a, panel.startPdf) > .10f))
                    throw new InvalidOperationException("Interior sash repair overlaps existing source masonry.");
                level.windows = level.windows.Concat(new[] { new SchoolMapBuilder.Span {
                    block = Block, vertical = true, p = panel.axisPdf, a = panel.startPdf, b = panel.endPdf,
                    thickness = panel.masonryThicknessPdf, source = Source } }).ToArray();
                // BuildWall cuts this span using the appended sash and creates only the native sill/lintel.
                level.walls = level.walls.Concat(new[] { new SchoolMapBuilder.Span {
                    block = Block, vertical = true, p = panel.axisPdf, a = panel.startPdf, b = panel.endPdf,
                    thickness = panel.masonryThicknessPdf, source = Source } }).ToArray();
            }
            var solid = new SolidBoundary();
            var upper = layout.levels.Single(l => l.index == solid.level);
            if (upper.walls.Any(w => w.block == Block && w.vertical && Mathf.Abs(w.p - solid.axisPdf) < 4
                && Mathf.Min(w.b, solid.endPdf) - Mathf.Max(w.a, solid.startPdf) > .10f))
                throw new InvalidOperationException("Verified room boundary already has source masonry.");
            if (upper.doors.Any(d => d.block == Block && d.vertical && Mathf.Abs(d.p - solid.axisPdf) < 6
                && d.b > solid.startPdf && d.a < solid.endPdf))
                throw new InvalidOperationException("Verified room boundary intersects a registered doorway.");
            upper.walls = upper.walls.Concat(new[] { new SchoolMapBuilder.Span {
                block = Block, vertical = true, p = solid.axisPdf, a = solid.startPdf, b = solid.endPdf,
                thickness = solid.masonryThicknessPdf, source = SolidSource } }).ToArray();
            report.newInteriorPanels = panels.Count;
            report.newSolidBoundaries = 1;
            report.removedFurnitureBoundaries = removed.Length;
            report.newMasonrySpans = panels.Count + 1;
            report.panels = panels.ToArray();
            report.solidBoundaries = new[] { solid };
            report.removedBoundaries = removed;
            report.doorSlotsAfter = layout.levels.Sum(l => l.doors.Length);
            report.windowsAfter = layout.levels.Sum(l => l.windows.Length);
            report.originalDoorArraysPreserved = layout.levels.All(l => doorBefore[l.index] == JsonUtility.ToJson(new SchoolMapBuilder.Level { index = l.index, doors = l.doors }));
            report.originalWindowPrefixesPreserved = layout.levels.All(l => windowsBefore[l.index].SequenceEqual(l.windows.Take(windowsBefore[l.index].Length).Select(w => JsonUtility.ToJson(w))));
            report.noRegisteredDoorwayIntersected = true;
            report.notes = new[] {
                "The native PDFs contain interior sash rectangles and parallel thin glazing strokes. The previous extractor required near_facade, so it omitted these seven panels and their sill/lintel masonry.",
                "P02 coordinates are registered to P01 with x_add=-28.08 and y_add=27.72. All axes listed here remain original registered PDF coordinates and are adapted with the other corridor spans exactly once.",
                "The 0.12 value is the PDF line stroke weight, not physical wall thickness. The physical thickness follows the measured 3.24/4.08 point distance between the rectangle faces (0.2286/0.28787 metres).",
                "Glazing sill/head heights reuse the current 1.05/2.58m classroom convention and remain estimated; the PDF establishes the plan footprint, not these vertical dimensions.",
                "Original door arrays and window prefixes retain every value and order. New windows are appended; no persistent doorway slot is added or removed.",
                "No closed wall is invented from an approximate room box. Existing adjoining jambs, classroom boundaries and door clearances remain intact." };
            report.notes = report.notes.Concat(new[] {
                "P02 rectangle 859 (0.72 PDF stroke; faces x830.92/832.36 and y340.52/409.64 after registration) is the thin Lab25 corridor boundary, excluded by the extractor's broad furniture rectangle filter. Its adjacent quarter-swing door curve 5676 is also omitted because that filter removed the supporting wall.",
                "The Lab25 opening remains physically open at y409.64..421.40. No new leaf or persistent door ID is needed. Existing door 1-0 belongs to main_classrooms_north (x1174.521/y352.119..361.997), not Lab25; its record remains untouched.",
                "The repaired solid boundary follows the 1.44 PDF face separation (0.1016m). The existing native wall builder applies its 0.14m minimum collider/render thickness. Perpendicular north-wall junctions are intentional; no existing parallel source interval is removed or overlapped." }).ToArray();
            report.notes = report.notes.Concat(new[] {
                "The old upper Lab25 span x823.84/y340.52..380.24 (2.13 PDF thickness; raw layout slot210) came from the vertical return of workbench curve5684. Visual PDF review confirms the L-shaped bench; the real corridor rectangle859 is restored separately.",
                "Seven further upper laboratory bench/sink edges became floor-to-ceiling walls: source lines4590/4577 on the east side, rectangles885/886/887/880 on the west side, plus sink inset rectangle881. Visual review shows sink fixtures within these bench strips. The actual exterior masonry/glazing x643.84 and actual corridor walls remain; only eight exact source intervals are removed before normalization.",
                "No source rectangle is removed wholesale: every removed item is an exact serialized wall interval with its measured axis, endpoints and thickness. Ambiguous corridor-side parallel strokes x827.08/y638.24..677.84 are retained." }).ToArray();
            if (!report.originalDoorArraysPreserved || !report.originalWindowPrefixesPreserved)
                throw new InvalidOperationException("Interior glazing repair changed an original registered aperture.");
            Directory.CreateDirectory(SchoolMapBuilder.Output);
            File.WriteAllText(SchoolMapBuilder.Output + "/" + ReportFile, JsonUtility.ToJson(report, true));
            return report;
        }
        static Panel NewPanel(int level, float axis, float start, float end, float thickness, int rectangle, string side) => new Panel {
            level = level, axisPdf = axis, startPdf = start, endPdf = end, masonryThicknessPdf = thickness,
            physicalThicknessMeters = thickness * Scale, spanMeters = (end - start) * Scale,
            pdfRectangleIndex = rectangle, side = side,
            sourcePdf = "C:/Users/ruhtra/Downloads/EXTINTORES CEEP-P0" + (level + 1) + ".pdf",
            sourceGeometry = "work/SchoolCEEP/P0" + (level + 1) + "-1-geometry.json" };
        static FurnitureBoundary Bench(string room, int rawSlot, float axis, float start, float end, float thickness,
            int curve = -1, int rectangle = -1, int line = -1) => new FurnitureBoundary {
                room = room, rawLayoutWallSlot = rawSlot, axisPdf = axis, startPdf = start, endPdf = end,
                oldMasonryThicknessPdf = thickness, sourceCurveIndex = curve, sourceRectangleIndex = rectangle, sourceLineIndex = line };
    }
}
