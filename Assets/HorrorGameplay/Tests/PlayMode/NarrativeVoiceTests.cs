// Verifica SetUp, LongerVoiceFinishesBeforeCompletionAndPausePreservesItsTime, InspectingHoldsVoiceAndSequenceUntilTheItemIsClosed, RecordedVoiceUsesRealPlaybackTimeWhenTheGameClockIsSlower, RestoringAnOldCheckpointSkipsPastReactionsButResumesStoryLines, BlackoutAndDisableReleaseVoiceWithoutCompletingTheSequence, VoicedCaptionUsesClipDurationAndMediaFiltersResetForJoao, TearDown.

using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("GameplayPolish")]
    public sealed class NarrativeVoiceTests
    {
        Scene scene;
        GameObject root;
        StoryBook book;
        AudioClip clip;
        GameSession game;
        NarrativeDirector director;
        AudioSource Voice => root.transform.Find("Dialogue voice").GetComponent<AudioSource>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            scene = SceneManager.CreateScene("Voice fixture " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(scene);
            foreach (var existing in UnityEngine.Object.FindObjectsByType<GameSession>())
                UnityEngine.Object.Destroy(existing.gameObject);
            yield return null;
            root = new GameObject("Isolated dialogue fixture");
            root.SetActive(false);
            game = root.AddComponent<GameSession>();
            game.enabled = false;
            director = root.AddComponent<NarrativeDirector>();
            clip = AudioClip.Create("Longer than caption", 22050, 1, 44100, false);
            book = ScriptableObject.CreateInstance<StoryBook>();
            book.chapters = new[] { new StoryChapterDefinition {
                id = "voice", milestones = new[] { new StoryMilestoneDefinition { id = "remain" } }
            } };
            book.sequences = new[] { new StorySequenceDefinition {
                id = "voice.line", lockPlayer = false,
                lines = new[] { new StoryLine { speaker = "João", text = "Teste.", seconds = .1f, voice = clip } }
            } };
            director.book = book;
            director.game = game;
            director.startingChapterId = "voice";
            game.narrative = director;
            root.SetActive(true);
            director.InitializeAfterSession(false);
            yield return null;
            Assert.That(director.PlaySequence("voice.line"), Is.True);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator LongerVoiceFinishesBeforeCompletionAndPausePreservesItsTime()
        {
            var source = Voice;
            Assert.That(source.clip, Is.EqualTo(clip));
            Assert.That(source.spatialBlend, Is.EqualTo(.2f));
            Assert.That(source.ignoreListenerPause, Is.False);
            Assert.That(book.sequences[0].lines[0].Duration, Is.EqualTo(.7f).Within(.001f));
            yield return new WaitForSeconds(.15f);
            Assert.That(director.HasSequence("voice.line"), Is.False);
            game.Pause();
            yield return new WaitForSecondsRealtime(.75f);
            Assert.That(director.HasSequence("voice.line"), Is.False);
            game.Resume();
            float deadline = Time.realtimeSinceStartup + 2;
            while (director.SequencePlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.HasSequence("voice.line"), Is.True);
            Assert.That(source.clip, Is.Null);
        }

        [UnityTest]
        public IEnumerator InspectingHoldsVoiceAndSequenceUntilTheItemIsClosed()
        {
            director.SuspendForBlackout();
            var body = new GameObject("Inspection player");
            body.transform.SetParent(root.transform);
            game.player = body.AddComponent<FirstPersonMotor>();
            game.player.enabled = false;
            Assert.That(game.BeginInspection(), Is.True);
            Assert.That(director.PlaySequence("voice.line"), Is.True);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(director.HasSequence("voice.line"), Is.False);
            Assert.That(Voice.clip, Is.Null);
            Assert.That(Voice.isPlaying, Is.False);
            game.EndInspection();
            float deadline = Time.realtimeSinceStartup + 2;
            while (director.SequencePlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.HasSequence("voice.line"), Is.True);
        }

        [UnityTest]
        public IEnumerator RecordedVoiceUsesRealPlaybackTimeWhenTheGameClockIsSlower()
        {
            Time.timeScale = .1f;
            yield return new WaitForSecondsRealtime(.9f);
            Assert.That(director.HasSequence("voice.line"), Is.True);
            Assert.That(Voice.clip, Is.Null);
        }

        [UnityTest]
        public IEnumerator RestoringAnOldCheckpointSkipsPastReactionsButResumesStoryLines()
        {
            director.SuspendForBlackout();
            book.chapters[0].milestones = new[] {
                new StoryMilestoneDefinition { id = "past.reaction", automaticSequenceId = "voice.past" },
                new StoryMilestoneDefinition { id = "past.story", automaticSequenceId = "story.past" },
                new StoryMilestoneDefinition { id = "remain" }
            };
            book.sequences = new[] {
                new StorySequenceDefinition { id = "voice.past", lockPlayer = false, lines = new[] { new StoryLine { text = "Reação passada", voice = clip } } },
                new StorySequenceDefinition { id = "story.past", lockPlayer = false, lines = new[] { new StoryLine { text = "História", seconds = .1f } } }
            };
            var saved = new NarrativeProgress { chapterId = "voice" };
            saved.milestones.Add("past.reaction"); saved.milestones.Add("past.story");
            director.RestoreProgress(saved);
            director.InitializeAfterSession(true);
            Assert.That(director.HasSequence("voice.past"), Is.True);
            Assert.That(director.HasSequence("story.past"), Is.False);
            yield return new WaitForSeconds(.3f);
            Assert.That(director.HasSequence("story.past"), Is.True);
            Assert.That(Voice.clip, Is.Null);
        }

        [UnityTest]
        public IEnumerator BlackoutAndDisableReleaseVoiceWithoutCompletingTheSequence()
        {
            var source = Voice;
            director.SuspendForBlackout();
            Assert.That(source.clip, Is.Null);
            Assert.That(director.SequencePlaying, Is.False);
            Assert.That(director.HasSequence("voice.line"), Is.False);
            Assert.That(director.PlaySequence("voice.line"), Is.True);
            yield return null;
            yield return null;
            Assert.That(source.clip, Is.EqualTo(clip));
            director.enabled = false;
            Assert.That(source.clip, Is.Null);
            Assert.That(director.HasSequence("voice.line"), Is.False);
        }

        [UnityTest]
        public IEnumerator VoicedCaptionUsesClipDurationAndMediaFiltersResetForJoao()
        {
            director.SuspendForBlackout();
            var line = book.sequences[0].lines[0];
            line.seconds = 12;
            Assert.That(line.Duration, Is.EqualTo(.7f).Within(.001f), "An old text reading estimate must not hold a completed recording.");
            line.speaker = "Rádio";
            Assert.That(director.PlaySequence("voice.line"), Is.True);
            yield return null;
            Assert.That(Voice.GetComponent<AudioHighPassFilter>().enabled, Is.True);
            Assert.That(Voice.GetComponent<AudioLowPassFilter>().cutoffFrequency, Is.EqualTo(3800).Within(.1f));
            Assert.That(Voice.GetComponent<AudioDistortionFilter>().enabled, Is.True);
            var noise = Voice.transform.Find("Media hiss")?.GetComponent<AudioSource>();
            if (Resources.Load<SoundSet>("VoiceRadioStatic"))
            {
                yield return null;
                Assert.That(noise && noise.isPlaying, Is.True);
                Assert.That(noise.volume, Is.LessThanOrEqualTo(.022f));
                game.Pause();
                yield return new WaitForSecondsRealtime(.1f);
                Assert.That(noise.isPlaying, Is.False, "Radio noise must pause with speech.");
                game.Resume();
            }
            yield return new WaitForSecondsRealtime(.9f);
            Assert.That(director.HasSequence("voice.line"), Is.True);
            director.RestoreProgress(new NarrativeProgress { chapterId = "voice" });
            director.InitializeAfterSession(true);
            line.speaker = "João";
            Assert.That(director.PlaySequence("voice.line"), Is.True);
            yield return null;
            Assert.That(Voice.GetComponent<AudioHighPassFilter>().enabled, Is.False);
            Assert.That(Voice.GetComponent<AudioLowPassFilter>().enabled, Is.False);
            Assert.That(Voice.GetComponent<AudioDistortionFilter>().enabled, Is.False, "A preceding radio must not colour João's voice.");
            if (noise) Assert.That(noise.isPlaying, Is.False, "Radio noise must not continue under João.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            if (root) UnityEngine.Object.Destroy(root);
            if (book) UnityEngine.Object.Destroy(book);
            if (clip) UnityEngine.Object.Destroy(clip);
            yield return null;
            var blank = SceneManager.GetSceneByName("Voice fixture cleanup");
            if (!blank.IsValid() || !blank.isLoaded) blank = SceneManager.CreateScene("Voice fixture cleanup");
            SceneManager.SetActiveScene(blank);
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
