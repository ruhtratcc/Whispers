// Verifica SetUp, TearDown, CatalogueUsesAuthorizedChapterOrderAndTitles, OnlyTheThreeAuthorizedChaptersHaveAvailableScenes, ContentHasNoInvalidReferencesAndKeepsAuthorizedCompletionPhrases, MainPhoneCallsAndRecordingsPreserveScriptCaptions, EnvironmentalInscriptionsPreserveTheWrittenEvidence, MilestonesRequireOrderAndEquipmentWithoutDuplicates, SequenceCompletionIsRecordedOnlyOnce, CopyPreservesStateAndOwnsIndependentLists, CompletedPrologueQueuesRoadAndEntersItOnlyOnRequest, FinalChapterRecordsEndingWithoutQueuingAnotherChapter, PopulateRejectsNullAndDestroyedUnityObjects.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HorrorGame.Tests
{
    [Category("GameplayCompletion")]
    public sealed class NarrativeProgressTests
    {
        private StoryBook book;

        [SetUp]
        public void SetUp()
        {
            book = ScriptableObject.CreateInstance<StoryBook>();
            StoryContent.Populate(book);
        }

        [TearDown]
        public void TearDown()
        {
            if (book) UnityEngine.Object.DestroyImmediate(book);
        }

        [Test]
        public void CatalogueUsesAuthorizedChapterOrderAndTitles()
        {
            CollectionAssert.AreEqual(new[]
            {
                "prologue", "road", "school", "left-behind", "memories", "terrace", "last-exit"
            }, book.chapters.Select(chapter => chapter.id).ToArray());
            CollectionAssert.AreEqual(new[]
            {
                "O Telefone", "A Chegada", "A Escola Abandonada",
                "O Que Foi Deixado para Trás", "As Memórias", "O Terraço", "A Última Saída"
            }, book.chapters.Select(chapter => chapter.title).ToArray());
            CollectionAssert.AreEqual(new[]
            {
                "CAPÍTULO 1", "CAPÍTULO 2", "CAPÍTULO 3", "CAPÍTULO 4",
                "CAPÍTULO 5", "CAPÍTULO 6", "CAPÍTULO FINAL"
            }, book.chapters.Select(chapter => chapter.heading).ToArray());
        }

        [Test]
        public void OnlyTheThreeAuthorizedChaptersHaveAvailableScenes()
        {
            Assert.That(book.Chapter("prologue").sceneName, Is.EqualTo("Chapter1Office"));
            Assert.That(book.Chapter("road").sceneName, Is.EqualTo("RoadJourney"));
            Assert.That(book.Chapter("school").sceneName, Is.EqualTo("SchoolCEEP"));
            Assert.That(book.chapters.Skip(3).All(chapter => string.IsNullOrEmpty(chapter.sceneName)), Is.True);
        }

        [Test]
        public void ContentHasNoInvalidReferencesAndKeepsAuthorizedCompletionPhrases()
        {
            Assert.That(book.ValidateContent(), Is.Empty);
            Assert.That(book.Chapter("school").completionPhrase,
                Is.EqualTo("A escola não está tão vazia quanto parece."));
            Assert.That(book.Chapter("road").completionPhrase, Is.EqualTo("A porta estava esperando por ele."));
            Assert.That(book.chapters.Where(chapter => chapter.id != "school" && chapter.id != "road")
                .All(chapter => string.IsNullOrEmpty(chapter.completionPhrase)), Is.True);
        }

        [Test]
        public void MainPhoneCallsAndRecordingsPreserveScriptCaptions()
        {
            AssertCaptions("prologue.call-one",
                "Senhor Claus... meu filho Daniel sumiu naquela escola há trinta anos.",
                "Hoje ele me ligou. Disse que ainda está lá dentro, no Bloco Hospício.",
                "Por que você ligou para mim?",
                "Ele disse que só você sabe qual porta abrir.",
                "Anotei o endereço.",
                "Espere. Como Daniel conseguiu este número?",
                "[A ligação cai.]");
            Assert.That(book.Sequence("prologue.call-one").lines[0].speaker, Is.EqualTo("Homem idoso"));
            AssertCaptions("prologue.call-two", "João...");
            AssertCaptions("road.arrival-call", "[Sons de crianças brincando.]", "Você demorou.");
            AssertCaptions("memories.recording-one",
                "Se alguém encontrar isso, não suba.", "Ele fica esperando lá em cima.", "Quem?");
            AssertCaptions("memories.recording-two", "João prometeu que voltaria.");
            AssertCaptions("memories.ghost", "Você prometeu.");
            AssertCaptions("terrace.voices", "João...", "Não vai embora...", "Você voltou...", "Ele está esperando.");
            AssertCaptions("last-exit.final-call", "Obrigado por voltar.", "Quem é você?", "Você sabe.");
            AssertCaptions("ending", "WHISPERS OF PAST", "FIM");
            Assert.That(book.Sequence("prologue.call-one").completionSignal, Is.EqualTo("prologue.first-call"));
            Assert.That(book.Sequence("prologue.call-two").completionSignal, Is.EqualTo("prologue.second-call"));
            Assert.That(book.Sequence("ending").completionSignal, Is.EqualTo("last-exit.ending"));
            Assert.That(book.Sequence("prologue.call-one").lines.Any(line => line.text == "" && line.seconds > 0f), Is.True);
        }

        [Test]
        public void EnvironmentalInscriptionsPreserveTheWrittenEvidence()
        {
            AssertCaptions("school.photo-inscription", "Alguns nunca foram embora.");
            AssertCaptions("school.blackboard-inscription", "JOÃO, VOCÊ VOLTOU.");
            AssertCaptions("school.drawing-inscription", "NÃO SUBA.");
            AssertCaptions("left-behind.photo-inscription", "João de Claus — aluno.");
            AssertCaptions("left-behind.wall-inscription", "ELE LEMBROU DE VOCÊ.");
        }

        [Test]
        public void MilestonesRequireOrderAndEquipmentWithoutDuplicates()
        {
            var progress = new NarrativeProgress { chapterId = "prologue" };
            Assert.That(progress.TryComplete(book, "prologue.second-call", null), Is.False);
            Assert.That(progress.TryComplete(book, "unknown", null), Is.False);
            Assert.That(progress.milestones, Is.Empty);

            Assert.That(progress.TryComplete(book, "prologue.first-call", null), Is.True);
            Assert.That(progress.TryComplete(book, "prologue.first-call", null), Is.False);
            Assert.That(progress.TryComplete(book, "prologue.exit", item => true), Is.False);
            Assert.That(progress.TryComplete(book, "prologue.second-call", null), Is.True);

            Assert.That(progress.TryComplete(book, "prologue.equipment", null), Is.False);
            Assert.That(progress.TryComplete(book, "prologue.equipment", item => false), Is.False);
            Assert.That(progress.TryComplete(book, "prologue.equipment", item => item == "unrelated_item"), Is.False);
            Assert.That(progress.Next(book).id, Is.EqualTo("prologue.equipment"));
            Assert.That(progress.TryComplete(book, "prologue.equipment", item => item == "office_equipment"), Is.True);
            Assert.That(progress.TryComplete(book, "prologue.equipment", item => true), Is.False);
            Assert.That(progress.TryComplete(book, "prologue.exit", null), Is.True);
            Assert.That(progress.TryComplete(book, "prologue.exit", null), Is.False);

            CollectionAssert.AreEqual(new[]
            {
                "prologue.first-call", "prologue.second-call", "prologue.equipment", "prologue.exit"
            }, progress.milestones);
            Assert.That(progress.IsChapterComplete(book), Is.True);
            Assert.That(progress.Next(book), Is.Null);
        }

        [Test]
        public void SequenceCompletionIsRecordedOnlyOnce()
        {
            var progress = new NarrativeProgress { chapterId = "prologue" };
            Assert.That(progress.CompleteSequence(null), Is.False);
            Assert.That(progress.CompleteSequence(""), Is.False);
            Assert.That(progress.CompleteSequence("prologue.call-one"), Is.True);
            Assert.That(progress.CompleteSequence("prologue.call-one"), Is.False);
            Assert.That(progress.HasSequence("prologue.call-one"), Is.True);
            CollectionAssert.AreEqual(new[] { "prologue.call-one" }, progress.sequences);
        }

        [Test]
        public void CopyPreservesStateAndOwnsIndependentLists()
        {
            var original = new NarrativeProgress
            {
                chapterId = "prologue",
                pendingChapterId = "road",
                endingSeen = false,
                milestones = new List<string> { "prologue.first-call" },
                sequences = new List<string> { "prologue.call-one" },
                completedChapters = new List<string> { "prologue" }
            };
            var copy = original.Copy();
            Assert.That(copy, Is.Not.SameAs(original));
            Assert.That(copy.chapterId, Is.EqualTo(original.chapterId));
            Assert.That(copy.pendingChapterId, Is.EqualTo(original.pendingChapterId));
            Assert.That(copy.endingSeen, Is.EqualTo(original.endingSeen));
            CollectionAssert.AreEqual(original.milestones, copy.milestones);
            CollectionAssert.AreEqual(original.sequences, copy.sequences);
            CollectionAssert.AreEqual(original.completedChapters, copy.completedChapters);
            Assert.That(copy.milestones, Is.Not.SameAs(original.milestones));
            Assert.That(copy.sequences, Is.Not.SameAs(original.sequences));
            Assert.That(copy.completedChapters, Is.Not.SameAs(original.completedChapters));

            copy.chapterId = "road";
            copy.pendingChapterId = "";
            copy.endingSeen = true;
            copy.milestones.Add("prologue.second-call");
            copy.sequences.Clear();
            copy.completedChapters.Add("road");
            Assert.That(original.chapterId, Is.EqualTo("prologue"));
            Assert.That(original.pendingChapterId, Is.EqualTo("road"));
            Assert.That(original.endingSeen, Is.False);
            CollectionAssert.AreEqual(new[] { "prologue.first-call" }, original.milestones);
            CollectionAssert.AreEqual(new[] { "prologue.call-one" }, original.sequences);
            CollectionAssert.AreEqual(new[] { "prologue" }, original.completedChapters);

            original.milestones.Clear();
            Assert.That(copy.HasSignal("prologue.first-call"), Is.True);
        }

        [Test]
        public void CompletedPrologueQueuesRoadAndEntersItOnlyOnRequest()
        {
            var progress = new NarrativeProgress { chapterId = "prologue" };
            Assert.That(progress.EnterPendingChapter(book), Is.False);
            Assert.That(progress.CompleteChapter(book), Is.False);
            foreach (var milestone in book.Chapter("prologue").milestones)
                Assert.That(progress.TryComplete(book, milestone.id, item => item == "office_equipment"), Is.True);

            Assert.That(progress.CompleteChapter(book), Is.True);
            Assert.That(progress.chapterId, Is.EqualTo("prologue"));
            Assert.That(progress.pendingChapterId, Is.EqualTo("road"));
            Assert.That(progress.endingSeen, Is.False);
            Assert.That(progress.CompleteChapter(book), Is.False);
            CollectionAssert.AreEqual(new[] { "prologue" }, progress.completedChapters);

            Assert.That(progress.EnterPendingChapter(book), Is.True);
            Assert.That(progress.chapterId, Is.EqualTo("road"));
            Assert.That(progress.pendingChapterId, Is.Empty);
            Assert.That(progress.Next(book).id, Is.EqualTo("road.context-shown"));
            Assert.That(progress.IsChapterComplete(book), Is.False);
            Assert.That(progress.EnterPendingChapter(book), Is.False);
            Assert.That(progress.HasSignal("prologue.exit"), Is.True);
        }

        [Test]
        public void FinalChapterRecordsEndingWithoutQueuingAnotherChapter()
        {
            var progress = new NarrativeProgress { chapterId = "last-exit" };
            foreach (var milestone in book.Chapter("last-exit").milestones)
                Assert.That(progress.TryComplete(book, milestone.id, null), Is.True);
            Assert.That(progress.endingSeen, Is.False);

            Assert.That(progress.CompleteChapter(book), Is.True);
            Assert.That(progress.endingSeen, Is.True);
            Assert.That(progress.pendingChapterId, Is.Empty);
            Assert.That(progress.chapterId, Is.EqualTo("last-exit"));
            Assert.That(progress.EnterPendingChapter(book), Is.False);
            Assert.That(progress.CompleteChapter(book), Is.False);
            CollectionAssert.AreEqual(new[] { "last-exit" }, progress.completedChapters);
        }

        [Test]
        public void PopulateRejectsNullAndDestroyedUnityObjects()
        {
            Assert.Throws<ArgumentNullException>(() => StoryContent.Populate(null));
            var destroyedBook = ScriptableObject.CreateInstance<StoryBook>();
            UnityEngine.Object.DestroyImmediate(destroyedBook);
            Assert.Throws<ArgumentNullException>(() => StoryContent.Populate(destroyedBook));
        }

        private void AssertCaptions(string sequenceId, params string[] expected)
        {
            var sequence = book.Sequence(sequenceId);
            Assert.That(sequence, Is.Not.Null, sequenceId);
            var actual = sequence.lines.Where(line => !string.IsNullOrEmpty(line.text))
                .Select(line => line.text).ToArray();
            CollectionAssert.AreEqual(expected, actual, sequenceId);
        }
    }
}
