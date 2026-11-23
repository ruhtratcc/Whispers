// Verifica SetUp, TearDown, SixChaptersHaveValidReferencesAndTheNewDanielOfficeEnding, OlderSchoolCheckpointsKeepTheirEvidenceAndReceiveOnlyReachedChapterKeys, OfficeAndRoadCheckpointsKeepTheirExactPositionInTheStory, APreviouslyFinishedEndingStaysFinishedAndMissingLegacyListsAreAccepted.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HorrorGame.Tests
{
    [Category("SimplifiedStory")]
    public sealed class SimplifiedStoryContentTests
    {
        StoryBook book;
        GameObject root;
        Inventory inventory;

        [SetUp]
        public void SetUp()
        {
            book = ScriptableObject.CreateInstance<StoryBook>();
            SimplifiedStoryContent.Populate(book);
            root = new GameObject("Story migration inventory fixture");
            inventory = root.AddComponent<Inventory>();
            inventory.Add(new InventoryItem { id = "old_key", title = "Chave antiga", text = "Evidência já encontrada.", note = true });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(book);
        }

        [Test]
        public void SixChaptersHaveValidReferencesAndTheNewDanielOfficeEnding()
        {
            Assert.That(book.revision, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { "prologue", "road", "school", "left-behind", "memories", "last-exit" },
                book.chapters.Select(chapter => chapter.id));
            CollectionAssert.AreEqual(new[] { "O Telefone", "A Chegada", "A Biblioteca", "Bloco Hospício — 1º andar", "Sala 113", "A Última Saída" },
                book.chapters.Select(chapter => chapter.title));
            CollectionAssert.AreEqual(Enumerable.Range(1, 6).Select(number => "CAPÍTULO " + number),
                book.chapters.Select(chapter => chapter.heading));
            Assert.That(book.ValidateContent(), Is.Empty);
            Assert.That(book.chapters.All(chapter => !string.IsNullOrWhiteSpace(chapter.initialObjective)), Is.True);
            Assert.That(book.Sequence("prologue.call-one").lines.Any(line => line.text.Contains("Daniel") && line.text.Contains("trinta anos")), Is.True);
            Assert.That(book.Sequence("simplified.office-awake").completionSignal, Is.EqualTo("last-exit.awake"));
            Assert.That(book.Sequence("simplified.office-ring").completionSignal, Is.EqualTo("last-exit.phone-ringing"));
            Assert.That(book.Sequence("ending").completionSignal, Is.EqualTo("last-exit.ending"));
            Assert.That(book.sequences.Any(sequence => sequence.id.Contains("demon") || sequence.id.Contains("ghost")), Is.False);
        }

        [TestCase("left-behind", "left-behind", false)]
        [TestCase("memories", "memories", true)]
        [TestCase("terrace", "last-exit", true)]
        [TestCase("last-exit", "last-exit", true)]
        public void OlderSchoolCheckpointsKeepTheirEvidenceAndReceiveOnlyReachedChapterKeys(string oldChapter, string newChapter, bool hasRoomKey)
        {
            var progress = Legacy(oldChapter);
            var oldEvidence = inventory.items[0];
            Assert.That(SimplifiedStoryContent.Migrate(progress, inventory), Is.True);
            Assert.That(progress.chapterId, Is.EqualTo(newChapter));
            Assert.That(progress.storyRevision, Is.EqualTo(2));
            Assert.That(progress.pendingChapterId, Is.Empty);
            Assert.That(progress.HasSignal("legacy.visited-room"), Is.True);
            Assert.That(progress.HasSequence("legacy.listened-recording"), Is.True);
            Assert.That(progress.completedChapters, Does.Contain("prologue"));
            Assert.That(inventory.items[0], Is.SameAs(oldEvidence));
            Assert.That(oldEvidence.text, Is.EqualTo("Evidência já encontrada."));
            Assert.That(inventory.Contains("old_key"), Is.True);
            Assert.That(inventory.Contains("iron_key"), Is.True);
            Assert.That(inventory.Contains("room_113_key"), Is.EqualTo(hasRoomKey));
            Assert.That(inventory.Contains("hospicio_recorder"), Is.EqualTo(hasRoomKey));
            Assert.That(progress.HasSignal("library.passage-opened"), Is.True);
            Assert.That(progress.HasSignal("hospicio.room-key"), Is.EqualTo(hasRoomKey));
            int itemCount = inventory.items.Count, markCount = progress.milestones.Count;
            Assert.That(SimplifiedStoryContent.Migrate(progress, inventory), Is.False);
            Assert.That(inventory.items.Count, Is.EqualTo(itemCount));
            Assert.That(progress.milestones.Count, Is.EqualTo(markCount));
        }

        [TestCase("prologue")]
        [TestCase("road")]
        public void OfficeAndRoadCheckpointsKeepTheirExactPositionInTheStory(string chapter)
        {
            var progress = Legacy(chapter);
            string[] marks = progress.milestones.ToArray(), sequences = progress.sequences.ToArray();
            Assert.That(SimplifiedStoryContent.Migrate(progress, inventory), Is.False);
            Assert.That(progress.chapterId, Is.EqualTo(chapter));
            Assert.That(progress.pendingChapterId, Is.EqualTo("legacy.pending"));
            CollectionAssert.AreEqual(marks, progress.milestones);
            CollectionAssert.AreEqual(sequences, progress.sequences);
            Assert.That(inventory.items.Count, Is.EqualTo(1));
        }

        [Test]
        public void APreviouslyFinishedEndingStaysFinishedAndMissingLegacyListsAreAccepted()
        {
            var finished = new NarrativeProgress { chapterId = "last-exit", endingSeen = true, milestones = null };
            Assert.That(SimplifiedStoryContent.Migrate(finished, inventory), Is.True);
            Assert.That(finished.endingSeen, Is.True);
            Assert.That(finished.IsChapterComplete(book), Is.True);
            Assert.That(finished.HasSignal("last-exit.ending"), Is.True);
            Assert.That(SimplifiedStoryContent.Migrate(null, inventory), Is.False);
        }

        static NarrativeProgress Legacy(string chapter) => new NarrativeProgress {
            chapterId = chapter, pendingChapterId = "legacy.pending", storyRevision = 0,
            milestones = new List<string> { "legacy.visited-room" },
            sequences = new List<string> { "legacy.listened-recording" },
            completedChapters = new List<string> { "prologue", "road" }
        };
    }
}
