// Verifica InstalledBooksKeepSixChaptersAndDelayTheIdentityUntil113, TapeRecordAndPersonalObjectsUseTheApprovedTextAndHumanAudio.

using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.Tests
{
    [Category("StoryRefinement")]
    public sealed class StoryRefinementTests
    {
        [TestCase("Assets/HorrorGameplay/Data/WhispersStory.asset")]
        [TestCase("Assets/RoadChapter/Data/RoadStory.asset")]
        [TestCase("Assets/SchoolCEEP/Data/ChapterThree/ChapterThreeStory.asset")]
        [TestCase("Assets/SchoolCEEP/Data/ChapterFour/SchoolStoryThroughChapterFour.asset")]
        [TestCase("Assets/SchoolCEEP/Data/ChapterFive/SchoolStoryThroughChapterFive.asset")]
        [TestCase("Assets/SchoolCEEP/Data/FinalChapters/SchoolCompleteStory.asset")]
        public void InstalledBooksKeepSixChaptersAndDelayTheIdentityUntil113(string path)
        {
            var book = AssetDatabase.LoadAssetAtPath<StoryBook>(path);
            Assert.That(book && book.revision == 2 && book.chapters.Length == 6, Is.True);
            Assert.That(book.ValidateContent(), Is.Empty);
            var reaction = book.Sequence("voice.hospicio.director-tape").lines.Single();
            Assert.That(reaction.text, Is.EqualTo("Eu vim atrás de uma criança desaparecida. E agora encontro meu nome em tudo."));
            Assert.That(reaction.voice && reaction.voice.name == "JOA-43", Is.True);
            Assert.That(book.Sequence("prologue.call-one").lines[0].speaker, Is.EqualTo("Homem idoso"));
            Assert.That(book.Sequence("prologue.call-one").lines[4].text, Is.EqualTo("Anotei o endereço."));
            Assert.That(book.Sequence("prologue.call-one").lines[4].voice, Is.Not.Null);
            Assert.That(book.Chapter("prologue").milestones.Single(mark => mark.id == "prologue.first-call").objectiveAfter, Does.Contain("biblioteca"));
            Assert.That(book.Sequence("hospicio.figure-seen-caption").lines.Single().text, Is.EqualTo("Ei... você está perdido?"));
            Assert.That(book.Sequence("hospicio.figure-caption").lines.Single().text, Is.EqualTo("Eu acabei de ver alguém ali."));
            Assert.That(book.Sequence("ending").lines.All(line => string.IsNullOrEmpty(line.text)), Is.True,
                "The transition view owns the only visible title/FIM, not a second subtitle sequence.");
            Assert.That(book.Sequence("simplified.office-ring").lines.All(line => string.IsNullOrEmpty(line.text)), Is.True,
                "A real paper clue replaces the imaginary phone display caption.");
        }

        [Test]
        public void TapeRecordAndPersonalObjectsUseTheApprovedTextAndHumanAudio()
        {
            const string directory = "Assets/SchoolCEEP/Data/SimplifiedStory20261014/";
            var tape = AssetDatabase.LoadAssetAtPath<InspectionItemData>(directory + "hospicio_director_tape.asset");
            Assert.That(tape.recordingText, Does.Contain("Daniel.").And.Contain("cento e treze"));
            Assert.That(tape.recordingText, Does.Not.Contain("João").And.Not.Contain("Também atende"));
            Assert.That(tape.recording.clips.Single().name, Is.EqualTo("hospicio_director_tape_sem_revelacao"));
            Assert.That(tape.recording.clips.Single().length, Is.EqualTo(15.22f).Within(.05f));
            var record = AssetDatabase.LoadAssetAtPath<InspectionItemData>(directory + "student_113_record.asset");
            Assert.That(record.frontText, Does.Contain("JOÃO DE CLAUS").And.Contain("DANIEL"));
            Assert.That(record.backText, Does.Not.Contain("prometer"));
            Assert.That(record.backText, Does.Contain("caneta"));
            const string props = "Assets/HorrorGameplay/Data/StoryRefinement20261026/";
            var note = AssetDatabase.LoadAssetAtPath<InspectionItemData>(props + "office_case_note.asset");
            Assert.That(note.frontText, Does.Contain("Rua das Acácias, 113").And.Contain("biblioteca"));
            Assert.That(note.frontTexture, Is.Not.Null, "The lead must also exist on the physical note.");
            var finalNote = AssetDatabase.LoadAssetAtPath<InspectionItemData>(props + "office_case_note_final.asset");
            Assert.That(finalNote.frontText, Does.Contain("LIGAÇÃO RECEBIDA").And.Contain("0000-0113"));
            var pen = AssetDatabase.LoadAssetAtPath<GameObject>(props + "PersonalPen.prefab");
            Assert.That(pen && pen.transform.Find("Risco junto à tampa"), Is.True);
            Assert.That(pen.GetComponentsInChildren<Collider>(true), Is.Empty, "The inspection model is visual only.");
            Assert.That(pen.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
        }
    }
}
