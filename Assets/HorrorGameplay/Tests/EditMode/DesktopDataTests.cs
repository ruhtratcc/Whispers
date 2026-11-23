// Verifica AdditiveStateRoundTripsAndRejectsIncompleteOrCorruptRecords, MinesFirstClickFloodsSafelyAndRareBoardKeepsThe0307Glyphs, EditableDefaultsHaveFiveVisitsThreeLocalPagesSixErrorsAndNoFabricatedVoice.

using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HorrorGame.Desktop;

namespace HorrorGame.Tests
{
    [Category("CRTDesktopData")]
    public sealed class DesktopDataTests
    {
        [Test]
        public void AdditiveStateRoundTripsAndRejectsIncompleteOrCorruptRecords()
        {
            var state = new DesktopState { visits = 5, minesGames = 16 };
            Assert.That(state.Unlock("trash.email.02"), Is.True);
            Assert.That(state.Unlock("trash.email.02"), Is.False);
            Assert.That(state.Unlock(" "), Is.False);
            state.eventIds.Add("desktop.cursor");
            var restored = DesktopState.Parse(JsonUtility.ToJson(state));
            Assert.That(restored.visits, Is.EqualTo(5));
            Assert.That(restored.minesGames, Is.EqualTo(16));
            Assert.That(restored.Has("trash.email.02") && restored.Has("desktop.cursor"), Is.True);
            Assert.That(restored.Unlock("desktop.cursor"), Is.False);
            restored.Unlock("report");
            Assert.That(state.Has("report"), Is.False, "Restored lists must not alias the original state.");
            foreach (string invalid in new[]
            {
                "", "{}", "{broken", "{\"version\":2,\"unlockedFiles\":[],\"eventIds\":[]}",
                "{\"version\":1,\"visits\":-1,\"unlockedFiles\":[],\"eventIds\":[]}",
                "{\"version\":1,\"visits\":100001,\"unlockedFiles\":[],\"eventIds\":[]}",
                "{\"version\":1,\"minesGames\":-1,\"unlockedFiles\":[],\"eventIds\":[]}",
                "{\"version\":1,\"unlockedFiles\":null,\"eventIds\":[]}",
                "{\"version\":1,\"unlockedFiles\":[\"x\",\"x\"],\"eventIds\":[]}",
                "{\"version\":1,\"unlockedFiles\":[],\"eventIds\":[\"\"]}",
                "{\"version\":1,\"unlockedFiles\":[],\"eventIds\":[\"x\",\"x\"]}"
            }) Assert.Throws<ArgumentException>(() => DesktopState.Parse(invalid), invalid);
        }

        [Test]
        public void MinesFirstClickFloodsSafelyAndRareBoardKeepsThe0307Glyphs()
        {
            var normal = new MinesBoard(407);
            Assert.That(normal.Reveal(3, 3), Is.True);
            Assert.That(normal.HasLost, Is.False);
            Assert.That(normal.AdjacentMines(3, 3), Is.Zero);
            int revealed = 0, mines = 0;
            for (int y = 0; y < normal.Height; y++)
                for (int x = 0; x < normal.Width; x++)
                {
                    if (normal.IsRevealed(x, y)) revealed++;
                    if (normal.IsMine(x, y)) mines++;
                    Assert.That(normal.IsRevealed(x, y) && normal.IsMine(x, y), Is.False);
                }
            Assert.That(revealed, Is.GreaterThanOrEqualTo(9));
            Assert.That(mines, Is.EqualTo(10));
            Assert.That(normal.Reveal(-1, 3), Is.False);
            Assert.That(normal.ToggleFlag(3, 3), Is.False, "A revealed square cannot receive a flag.");
            var rare = new MinesBoard(407, true);
            string[] expected =
            {
                "0000000000000000", "1110111011101110", "1010001010100010",
                "1010111010100010", "1010001010100010", "1110111011100010", "0000000000000000"
            };
            string[] before = Enumerable.Range(0, rare.Height).Select(y => Row(rare, y)).ToArray();
            CollectionAssert.AreEqual(expected, before);
            rare.Reveal(0, 0);
            CollectionAssert.AreEqual(before, Enumerable.Range(0, rare.Height).Select(y => Row(rare, y)).ToArray(),
                "First-click safety must not reshape the rare 0307 arrangement.");
            Assert.That(rare.HasLost, Is.False);
        }

        static string Row(MinesBoard board, int y) => new string(Enumerable.Range(0, board.Width)
            .Select(x => board.IsMine(x, y) ? '1' : '0').ToArray());

        [Test]
        public void EditableDefaultsHaveFiveVisitsThreeLocalPagesSixErrorsAndNoFabricatedVoice()
        {
            var content = ScriptableObject.CreateInstance<DesktopContent>();
            try
            {
                DesktopContentDefaults.Populate(content);
                CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, content.notes.Select(note => note.visit).ToArray());
                Assert.That(content.notes.All(note => note.paragraphs.Any(paragraph => paragraph.child)), Is.True);
                Assert.That(content.pages.Length, Is.EqualTo(3));
                Assert.That(content.pages.Select(page => page.address).Distinct().Count(), Is.EqualTo(3));
                Assert.That(content.pages.All(page => page.address.StartsWith("seepe.local/", StringComparison.Ordinal)), Is.True);
                Assert.That(content.pages.Single(page => page.address.EndsWith("quatro-alunos")).title,
                    Is.EqualTo("Quatro alunos desaparecem na SEEPE"));
                string demolition = string.Join("\n", content.pages.Single(page => page.address.EndsWith("demolicao"))
                    .paragraphs.Select(paragraph => paragraph.text));
                Assert.That(demolition, Does.Contain("27 de outubro de 2026").And.Contain("8h"));
                CollectionAssert.AreEquivalent(new[] { "case", "attachment", "receipt", "memory", "clock", "disk" },
                    content.errors.Select(error => error.id).ToArray());
                Assert.That(content.errors.Single(error => error.id == "receipt").message, Does.Contain("03:07"));
                var canonical = EmailCatalog.BeforeDemolitionContent();
                CollectionAssert.AreEqual(Enumerable.Range(1, 8).Select(number => "email.0" + number).ToArray(),
                    canonical.Select(email => email.id).ToArray());
                var canonicalLines = canonical.SelectMany(email => email.paragraphs).Select(paragraph => paragraph.text).ToArray();
                foreach (var paragraph in content.notes.SelectMany(note => note.paragraphs).Where(paragraph => paragraph.child))
                    CollectionAssert.Contains(canonicalLines, paragraph.text, "Child quotations must come from existing emails.");
                Assert.That(content.tracks.Length, Is.EqualTo(2));
                Assert.That(content.tracks[0].unlockVisit, Is.EqualTo(1));
                Assert.That(content.tracks[1].unlockVisit, Is.EqualTo(2));
                Assert.That(content.tracks.All(track => !track.clip && track.placeholderSeconds > 0), Is.True,
                    "Missing voices remain explicit silent placeholders; defaults must not synthesize speech.");
            }
            finally
            {
                foreach (var note in content.notes) if (note) UnityEngine.Object.DestroyImmediate(note);
                foreach (var page in content.pages) if (page) UnityEngine.Object.DestroyImmediate(page);
                foreach (var error in content.errors) if (error) UnityEngine.Object.DestroyImmediate(error);
                UnityEngine.Object.DestroyImmediate(content);
            }
        }
    }
}
