// Verifica SetUp, TearDown, TwoCallsRequireAnswerThenEquipmentAndExit, PauseDuringCallFreezesDialogueAndRestoresCutsceneState, CallQueuedDuringPauseLocksControlOnlyAfterResume, EquipmentCollectedDuringNotePauseWaitsForResume, RestoredCallsDoNotReplayAndEquipmentGateSurvives, MissingRoadMapKeepsOfficeProgressAndAllowsMenuState, WorldEventRestoresSilentlyWithoutReplayingScare.

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("GameplayCompletion")]
    public sealed class NarrativeRuntimeTests
    {
        Scene scene;
        GameObject root;
        StoryBook book;
        GameSession game;
        NarrativeDirector director;
        Inventory inventory;
        PrologueFlow flow;
        StoryTelephone telephone;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Entirely isolated runtime scene: no office, doors, map or save is touched.
            scene = SceneManager.CreateScene("Narrative-runtime-test");
            SceneManager.SetActiveScene(scene);
            // Preserve the Test Framework's runner scene. Unloading it destroys its coroutine host.
            foreach (var existing in Object.FindObjectsByType<GameSession>()) Object.Destroy(existing.gameObject);
            yield return null;
            Time.timeScale = 1; AudioListener.pause = false;
            root = new GameObject("Isolated narrative fixture");
            root.SetActive(false);
            game = root.AddComponent<GameSession>();
            game.enabled = false; // Deliberately skip scene initialization/save.
            inventory = root.AddComponent<Inventory>();
            director = root.AddComponent<NarrativeDirector>();
            book = ScriptableObject.CreateInstance<StoryBook>(); StoryContent.Populate(book);
            foreach (var sequence in book.sequences) sequence.lines = new[] { new StoryLine { text = sequence.id, seconds = .12f } };
            director.book = book; director.game = game; director.inventory = inventory;
            game.narrative = director;
            flow = root.AddComponent<PrologueFlow>(); flow.director = director; flow.firstCallDelay = .05f; flow.secondCallDelay = .05f;
            telephone = root.AddComponent<StoryTelephone>(); telephone.flow = flow; flow.telephone = telephone;
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
            yield return null;
            var blank = SceneManager.GetSceneByName("Narrative-test-cleanup");
            if (!blank.IsValid() || !blank.isLoaded) blank = SceneManager.CreateScene("Narrative-test-cleanup");
            SceneManager.SetActiveScene(blank);
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
        IEnumerator Until(System.Func<bool> predicate, float timeout = 2)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "Narrative condition did not complete before its deadline.");
        }
        [UnityTest]
        public IEnumerator TwoCallsRequireAnswerThenEquipmentAndExit()
        {
            Assert.That(director.HasSignal("prologue.first-call"), Is.False);
            yield return Until(() => flow.Ringing);
            Assert.That(flow.Answer(), Is.True);
            Assert.That(flow.Answer(), Is.False);
            yield return Until(() => director.HasSequence("prologue.call-one"));
            yield return Until(() => flow.Ringing);
            Assert.That(flow.AvailableSequence, Is.EqualTo("prologue.call-two"));
            Assert.That(flow.Answer(), Is.True);
            yield return Until(() => director.HasSequence("prologue.call-two"));
            Assert.That(director.Signal("prologue.exit"), Is.False);
            Assert.That(director.Signal("prologue.equipment"), Is.False);
            inventory.Add(new InventoryItem { id = "office_equipment", title = "Equipamentos" });
            yield return Until(() => director.HasSignal("prologue.equipment"));
            Assert.That(flow.Ringing, Is.False);
        }
        [UnityTest]
        public IEnumerator PauseDuringCallFreezesDialogueAndRestoresCutsceneState()
        {
            book.Sequence("prologue.call-one").lines[0].seconds = .4f;
            Assert.That(director.PlaySequence("prologue.call-one"), Is.True);
            yield return Until(() => game.State == GameState.Cutscene);
            game.Pause();
            Assert.That(game.State, Is.EqualTo(GameState.Paused));
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(director.HasSignal("prologue.first-call"), Is.False);
            game.Resume();
            Assert.That(game.State, Is.EqualTo(GameState.Cutscene));
            Assert.That(game.CanControl, Is.False);
            yield return Until(() => director.HasSequence("prologue.call-one"));
            yield return null;
            Assert.That(game.State, Is.EqualTo(GameState.Playing));
        }
        [UnityTest]
        public IEnumerator CallQueuedDuringPauseLocksControlOnlyAfterResume()
        {
            game.Pause();
            Assert.That(director.PlaySequence("prologue.call-one"), Is.True);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(director.ActiveSequenceId, Is.Empty);
            Assert.That(director.HasSignal("prologue.first-call"), Is.False);
            Assert.That(game.State, Is.EqualTo(GameState.Paused));
            game.Resume();
            yield return Until(() => director.ActiveSequenceId == "prologue.call-one");
            Assert.That(game.State, Is.EqualTo(GameState.Cutscene));
            Assert.That(game.CanControl, Is.False);
            yield return Until(() => director.HasSequence("prologue.call-one"));
        }
        [UnityTest]
        public IEnumerator EquipmentCollectedDuringNotePauseWaitsForResume()
        {
            flow.enabled = false;
            director.Signal("prologue.first-call"); director.Signal("prologue.second-call");
            game.Pause();
            inventory.Add(new InventoryItem { id = "office_equipment" });
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(director.HasSignal("prologue.equipment"), Is.False);
            Assert.That(game.State, Is.EqualTo(GameState.Paused));
            Assert.That(director.Signal("prologue.equipment"), Is.False);
            game.Resume();
            yield return Until(() => director.HasSignal("prologue.equipment"));
            Assert.That(game.State, Is.EqualTo(GameState.Playing));
        }
        [UnityTest]
        public IEnumerator RestoredCallsDoNotReplayAndEquipmentGateSurvives()
        {
            var progress = new NarrativeProgress { chapterId = "prologue" };
            Assert.That(progress.TryComplete(book, "prologue.first-call", null), Is.True);
            progress.CompleteSequence("prologue.call-one");
            Assert.That(progress.TryComplete(book, "prologue.second-call", null), Is.True);
            progress.CompleteSequence("prologue.call-two");
            director.RestoreProgress(progress); director.InitializeAfterSession(true);
            yield return new WaitForSeconds(.15f);
            Assert.That(flow.Ringing, Is.False);
            Assert.That(director.PlaySequence("prologue.call-one"), Is.False);
            inventory.Add(new InventoryItem { id = "office_equipment" });
            yield return Until(() => director.HasSignal("prologue.equipment"));
        }
        [UnityTest]
        public IEnumerator MissingRoadMapKeepsOfficeProgressAndAllowsMenuState()
        {
            flow.enabled = false;
            Assert.That(director.Signal("prologue.first-call"), Is.True);
            Assert.That(director.Signal("prologue.second-call"), Is.True);
            inventory.Add(new InventoryItem { id = "office_equipment" });
            yield return Until(() => director.HasSignal("prologue.equipment"));
            Assert.That(director.Signal("prologue.exit"), Is.True);
            yield return Until(() => game.State == GameState.StoryScreen);
            Assert.That(game.State, Is.EqualTo(GameState.StoryScreen));
            Assert.That(director.Progress.chapterId, Is.EqualTo("prologue"));
            Assert.That(director.Progress.pendingChapterId, Is.EqualTo("road"));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            Assert.That(game.CanControl, Is.False);
            Assert.That(Cursor.visible, Is.True);
        }
        [UnityTest]
        public IEnumerator WorldEventRestoresSilentlyWithoutReplayingScare()
        {
            int triggered = 0, restored = 0;
            var child = new GameObject("Map hook"); child.transform.SetParent(root.transform); child.SetActive(false);
            var worldEvent = child.AddComponent<StoryWorldEvent>();
            worldEvent.director = director; worldEvent.signalId = "prologue.first-call";
            worldEvent.onTriggered.AddListener(() => triggered++);
            worldEvent.onRestoreCompleted.AddListener(() => restored++);
            child.SetActive(true);
            director.Signal("prologue.first-call");
            Assert.That(triggered, Is.EqualTo(1));
            var snapshot = director.CaptureProgress();
            director.RestoreProgress(snapshot); director.InitializeAfterSession(true);
            Assert.That(restored, Is.EqualTo(1));
            Assert.That(triggered, Is.EqualTo(1));
            child.SetActive(false);
            director.Signal("prologue.second-call");
            yield return null;
            Assert.That(triggered, Is.EqualTo(1));
        }
    }
}
