// Verifica SetUp, TearDown, BothCallsPlayOnePickupAndOneHangupWithoutInteractionDuplicates, PauseCannotPrematurelyHangUpTheReceiver, DeathCancelsTheCallWithoutFakingHangup, RestoringInterruptedCallResetsReceiverSilentlyAndAllowsFreshPickup.

using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("OfficeInteractionPolish")]
    public sealed class TelephoneFoleyTests
    {
        Scene scene;
        GameObject root;
        StoryBook book;
        GameSession game;
        NarrativeDirector director;
        PrologueFlow flow;
        StoryTelephone phone;
        SoundSet pickup, hangup;
        AudioClip pickupClip, hangupClip;
        int lifted, placed;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            scene = SceneManager.CreateScene("Telephone-Foley-test");
            SceneManager.SetActiveScene(scene);
            foreach (var existing in Object.FindObjectsByType<GameSession>()) Object.Destroy(existing.gameObject);
            yield return null;
            Time.timeScale = 1; AudioListener.pause = false;
            root = new GameObject("Isolated telephone Foley fixture");
            root.SetActive(false);
            game = root.AddComponent<GameSession>(); game.enabled = false;
            root.AddComponent<AudioManager>();
            director = root.AddComponent<NarrativeDirector>();
            book = ScriptableObject.CreateInstance<StoryBook>(); StoryContent.Populate(book);
            foreach (var sequence in book.sequences)
                sequence.lines = new[] { new StoryLine { text = sequence.id, seconds = .18f } };
            director.book = book; director.game = game; game.narrative = director;
            flow = root.AddComponent<PrologueFlow>(); flow.director = director;
            flow.firstCallDelay = .04f; flow.secondCallDelay = .04f;
            phone = root.AddComponent<StoryTelephone>(); phone.flow = flow; flow.telephone = phone;
            pickupClip = AudioClip.Create("Telephone pickup test clip", 44100, 1, 22050, false);
            hangupClip = AudioClip.Create("Telephone hangup test clip", 44100, 1, 22050, false);
            pickup = ScriptableObject.CreateInstance<SoundSet>(); pickup.clips = new[] { pickupClip };
            hangup = ScriptableObject.CreateInstance<SoundSet>(); hangup.clips = new[] { hangupClip };
            phone.pickupSound = pickup; phone.hangupSound = hangup;
            lifted = placed = 0;
            phone.onReceiverLifted.AddListener(() => lifted++);
            phone.onReceiverPlaced.AddListener(() => placed++);
            root.SetActive(true);
            director.InitializeAfterSession(false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1; AudioListener.pause = false;
            if (root) Object.Destroy(root);
            if (book) Object.Destroy(book);
            if (pickup) Object.Destroy(pickup);
            if (hangup) Object.Destroy(hangup);
            if (pickupClip) Object.Destroy(pickupClip);
            if (hangupClip) Object.Destroy(hangupClip);
            yield return null;
            var cleanup = SceneManager.GetSceneByName("Narrative-test-cleanup");
            if (!cleanup.IsValid() || !cleanup.isLoaded) cleanup = SceneManager.CreateScene("Narrative-test-cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        IEnumerator Until(System.Func<bool> condition, float timeout = 2)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Telephone Foley condition timed out.");
        }

        AudioSource SourceFor(AudioClip clip) => root.GetComponentsInChildren<AudioSource>().FirstOrDefault(source => source.clip == clip);

        [UnityTest]
        public IEnumerator BothCallsPlayOnePickupAndOneHangupWithoutInteractionDuplicates()
        {
            yield return Until(() => flow.Ringing);
            phone.Interact(null);
            Assert.That(lifted, Is.EqualTo(1));
            Assert.That(phone.ReceiverLifted, Is.True);
            var source = SourceFor(pickupClip);
            Assert.That(source, Is.Not.Null);
            Assert.That(source.spatialBlend, Is.EqualTo(1));
            Assert.That(source.ignoreListenerPause, Is.False);
            phone.Interact(null);
            Assert.That(lifted, Is.EqualTo(1), "Rejected duplicate interaction must stay silent.");
            yield return Until(() => director.HasSequence("prologue.call-one"));
            Assert.That(placed, Is.EqualTo(1));
            Assert.That(phone.ReceiverLifted, Is.False);
            Assert.That(SourceFor(hangupClip), Is.Not.Null);
            yield return Until(() => flow.Ringing);
            // The lower-level answer entry point also receives Foley when its sequence starts.
            Assert.That(flow.Answer(), Is.True);
            yield return Until(() => director.HasSequence("prologue.call-two"));
            Assert.That(lifted, Is.EqualTo(2));
            Assert.That(placed, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator PauseCannotPrematurelyHangUpTheReceiver()
        {
            book.Sequence("prologue.call-one").lines[0].seconds = .4f;
            yield return Until(() => flow.Ringing);
            phone.Interact(null);
            yield return Until(() => game.State == GameState.Cutscene);
            game.Pause();
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(lifted, Is.EqualTo(1));
            Assert.That(placed, Is.Zero);
            Assert.That(phone.ReceiverLifted, Is.True);
            Assert.That(SourceFor(hangupClip), Is.Null);
            game.Resume();
            yield return Until(() => director.HasSequence("prologue.call-one"));
            Assert.That(placed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DeathCancelsTheCallWithoutFakingHangup()
        {
            book.Sequence("prologue.call-one").lines[0].seconds = 3;
            yield return Until(() => flow.Ringing);
            phone.Interact(null);
            yield return Until(() => game.State == GameState.Cutscene);
            game.Kill();
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(lifted, Is.EqualTo(1));
            Assert.That(placed, Is.Zero);
            Assert.That(phone.ReceiverLifted, Is.False);
            Assert.That(SourceFor(hangupClip), Is.Null);
        }

        [UnityTest]
        public IEnumerator RestoringInterruptedCallResetsReceiverSilentlyAndAllowsFreshPickup()
        {
            book.Sequence("prologue.call-one").lines[0].seconds = 1;
            yield return Until(() => flow.Ringing);
            phone.Interact(null);
            yield return Until(() => game.State == GameState.Cutscene);
            director.RestoreProgress(new NarrativeProgress { chapterId = "prologue" });
            director.InitializeAfterSession(true);
            Assert.That(placed, Is.Zero);
            Assert.That(phone.ReceiverLifted, Is.False);
            Assert.That(SourceFor(hangupClip), Is.Null);
            book.Sequence("prologue.call-one").lines[0].seconds = .18f;
            yield return Until(() => flow.Ringing);
            phone.Interact(null);
            yield return Until(() => director.HasSequence("prologue.call-one"));
            Assert.That(lifted, Is.EqualTo(2));
            Assert.That(placed, Is.EqualTo(1));
        }
    }
}
