// Classifica documentos, itens e pistas pelo registro conhecido.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame
{
    public enum ArchiveCategory { Documents, Objects, Clues, Media }

    public static class ArchiveRecords
    {
        public readonly struct Details
        {
            public readonly ArchiveCategory category;
            public readonly string chapter, location;
            public readonly string[] related;

            public Details(ArchiveCategory category, string chapter, string location, params string[] related)
            {
                this.category = category;
                this.chapter = chapter;
                this.location = location;
                this.related = related;
            }
        }

        static readonly Dictionary<string, Details> authored = new Dictionary<string, Details>(StringComparer.Ordinal)
        {
            ["bd.whistle"] = new Details(ArchiveCategory.Objects, "bd.office", "Segunda gaveta, escritório"),
            ["bd.library.key"] = new Details(ArchiveCategory.Objects, "bd.secretary-library", "Secretaria"),
            ["bd.lunchbox"] = new Details(ArchiveCategory.Objects, "bd.secretary-library", "Secretaria"),
            ["bd.report.2006"] = new Details(ArchiveCategory.Documents, "bd.secretary-library", "Biblioteca"),
            ["bd.helena.investigation"] = new Details(ArchiveCategory.Documents, "bd.secretary-library", "Biblioteca"),
            ["bd.whistle.lore.0"] = new Details(ArchiveCategory.Documents, "bd.secretary-library", "Mural da biblioteca"),
            ["bd.whistle.lore.1"] = new Details(ArchiveCategory.Documents, "bd.secretary-library", "Mural da biblioteca"),
            ["bd.whistle.lore.2"] = new Details(ArchiveCategory.Documents, "bd.secretary-library", "Mural da biblioteca"),
            ["bd.cups"] = new Details(ArchiveCategory.Clues, "bd.secretary-library", "Piso da secretaria", "bd.note.email.03"),
            ["bd.tape.01"] = new Details(ArchiveCategory.Media, "bd.secretary-library", "Biblioteca"),
            ["bd.phone.welcome"] = new Details(ArchiveCategory.Media, "bd.secretary-library", "Celular"),
            ["bd.marta.notebook"] = new Details(ArchiveCategory.Documents, "bd.four-students", "Sala de artes"),
            ["bd.caio.cap"] = new Details(ArchiveCategory.Objects, "bd.four-students", "Sanitário masculino"),
            ["bd.luan.lunch"] = new Details(ArchiveCategory.Objects, "bd.four-students", "Refeitório"),
            ["bd.bia.tape"] = new Details(ArchiveCategory.Media, "bd.four-students", "Informática A"),
            ["bd.stairs.key"] = new Details(ArchiveCategory.Objects, "bd.four-students", "Refeitório"),
            ["bd.helena.tape"] = new Details(ArchiveCategory.Media, "bd.two-voices", "Sala lateral do corredor"),
            ["bd.case.report"] = new Details(ArchiveCategory.Documents, "bd.mirror-report", null),
            ["school_documents"] = new Details(ArchiveCategory.Documents, "school", null),
            ["school_photograph"] = new Details(ArchiveCategory.Media, "school", null),
            ["school_drawing"] = new Details(ArchiveCategory.Documents, "school", null),
            ["office_photo"] = new Details(ArchiveCategory.Media, null, "Escritório"),
            ["old_student_photo"] = new Details(ArchiveCategory.Media, "left-behind", null),
            ["old_disappearance_report"] = new Details(ArchiveCategory.Documents, "left-behind", null),
            ["memories_group_photo"] = new Details(ArchiveCategory.Media, "memories", null),
            ["hospicio_recorder"] = new Details(ArchiveCategory.Objects, null, "Hospício")
        };
        static readonly HashSet<string> internalRecords = new HashSet<string>(StringComparer.Ordinal)
        {
            "bd.email.delivery.08", "bd.event.bia-preview", "bd.tape.01.heard", "bd.bia.tape.heard", "bd.helena.tape.heard"
        };

        public static bool IsEmail(InventoryItem item) => item.id.StartsWith("bd.note.email.", StringComparison.Ordinal);

        public static bool Visible(InventoryItem item) => item != null && !string.IsNullOrWhiteSpace(item.id)
            && !string.IsNullOrWhiteSpace(item.title) && !internalRecords.Contains(item.id)
            && !item.id.StartsWith("bd.event.", StringComparison.Ordinal) && !item.id.EndsWith(".heard", StringComparison.Ordinal);

        public static Details Get(InventoryItem item, InspectionItemData definition)
        {
            if (authored.TryGetValue(item.id, out var details)) return details;
            if (IsEmail(item))
                return new Details(ArchiveCategory.Documents, item.id == "bd.note.email.08" ? "bd.road" : "bd.office", "Correio");
            return new Details(definition && definition.canPlay ? ArchiveCategory.Media
                : item.note ? ArchiveCategory.Documents : ArchiveCategory.Objects, item.chapterId, null);
        }

        public static bool IsNew(InventoryItem item) => !item.inspectionSeen && !IsEmail(item);

        public static Texture2D OriginalImage(InspectionItemData definition)
        {
            if (!definition) return null;
            if (definition.frontTexture) return definition.frontTexture;
            if (definition.pages != null)
                foreach (var page in definition.pages) if (page != null && page.image) return page.image;
            return definition.backTexture;
        }
    }
}

