// Verifica SetUp, EarlyKeyRemainsCollectibleButCannotOpenEitherGateAndRestoreClosesAnOldOpenGate, RecordedTapeAndKeyOnlyUnlockAfterRecorderRegisterAndFigureMilestones, MigratedMemoriesCheckpointKeepsItsKeyAndOpenDoorsAccessible, TearDown.

using System;
using System.Collections;
using HorrorGame;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("SchoolProgressionGate")]
    public sealed class SchoolProgressionGateTests
    {
        Scene scene;
        GameObject root;
        StoryBook book;
        GameSession game;
        NarrativeDirector director;
        Inventory inventory;
        PlayerInteraction interaction;
        SchoolSimplifiedFlow flow;
        SimplifiedSchoolClue key;
        bool hadResume, hadCarry, listenerPaused, cursorVisible;
        int resume, carry;
        float timeScale;
        CursorLockMode cursorLock;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hadResume = PlayerPrefs.HasKey("Game.ResumeRequested"); resume = PlayerPrefs.GetInt("Game.ResumeRequested");
            hadCarry = PlayerPrefs.HasKey("Game.StoryCarryRequested"); carry = PlayerPrefs.GetInt("Game.StoryCarryRequested");
            timeScale = Time.timeScale; listenerPaused = AudioListener.pause;
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            PlayerPrefs.SetInt("Game.ResumeRequested", 0); PlayerPrefs.SetInt("Game.StoryCarryRequested", 0);
            foreach (var previous in UnityEngine.Object.FindObjectsByType<GameSession>()) UnityEngine.Object.Destroy(previous.gameObject);
            yield return null;
            scene = SceneManager.CreateScene("SchoolGate-" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(scene);
            root = new GameObject("Isolated school gate fixture"); root.SetActive(false);
            game = root.AddComponent<GameSession>(); game.enabled = false; // No session Start, SaveSystem, disk write or authored scene.
            inventory = root.AddComponent<Inventory>();
            interaction = root.AddComponent<PlayerInteraction>(); interaction.inventory = inventory; interaction.enabled = false;
            game.interaction = interaction;
            director = root.AddComponent<NarrativeDirector>();
            book = ScriptableObject.CreateInstance<StoryBook>(); SimplifiedStoryContent.Populate(book);
            foreach (var chapter in book.chapters)
            {
                chapter.sceneName = scene.name;
                foreach (var milestone in chapter.milestones) milestone.automaticSequenceId = "";
            }
            book.sequences = Array.Empty<StorySequenceDefinition>(); // This fixture tests gating; authored voice/figure presentation has scene coverage.
            director.book = book; director.game = game; director.inventory = inventory;
            director.startingChapterId = "left-behind"; director.continueSameSceneChapters = true; director.autoLoadNextChapter = false;
            game.narrative = director;
            flow = root.AddComponent<SchoolSimplifiedFlow>(); flow.director = director;
            flow.stairGate = Door("Stair gate", "gate.fixture.stairs");
            flow.room113Door = Door("Room 113", "gate.fixture.room113");
            var clue = new GameObject("Early room key"); clue.transform.SetParent(root.transform, false);
            key = clue.AddComponent<SimplifiedSchoolClue>(); key.flow = flow; key.chapterId = "left-behind";
            key.itemId = "room_113_key"; key.title = "Chave da Sala 113";
            Time.timeScale = 1; AudioListener.pause = false;
            root.SetActive(true); director.InitializeAfterSession(false);
            yield return null;
        }

        InteractiveDoor Door(string name, string id)
        {
            var child = new GameObject(name); child.transform.SetParent(root.transform, false);
            var door = child.AddComponent<InteractiveDoor>(); door.persistentId = id;
            var hinge = new GameObject("Hinge"); hinge.transform.SetParent(child.transform, false); door.hinge = hinge.transform;
            return door;
        }
        static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 4;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "The isolated gate progression did not complete.");
        }
        void AssertClosedEvenWithKey()
        {
            foreach (var door in new[] { flow.stairGate, flow.room113Door })
            {
                Assert.That(door.locked, Is.True);
                door.Interact(interaction);
                Assert.That(door.locked && !door.IsOpen, Is.True,
                    "Possessing room_113_key cannot bypass the story prerequisite on " + door.name);
            }
        }

        [UnityTest]
        public IEnumerator EarlyKeyRemainsCollectibleButCannotOpenEitherGateAndRestoreClosesAnOldOpenGate()
        {
            Assert.That(flow.CanInvestigate(key), Is.True, "The fix must retain early collection.");
            key.Interact(interaction);
            Assert.That(inventory.Contains("room_113_key"), Is.True);
            AssertClosedEvenWithKey();
            foreach (var door in new[] { flow.stairGate, flow.room113Door }) door.SetOpen(true, true);
            flow.RestoreWorldSilently();
            AssertClosedEvenWithKey();
            Assert.That(director.Progress.chapterId, Is.EqualTo("left-behind"));
            Assert.That(director.HasSignal("hospicio.director-tape"), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RecordedTapeAndKeyOnlyUnlockAfterRecorderRegisterAndFigureMilestones()
        {
            key.Interact(interaction);
            Assert.That(director.Signal("hospicio.director-tape"), Is.False, "A later signal cannot skip the authored order.");
            Assert.That(director.Signal("hospicio.arrived"), Is.True);
            inventory.Add(new InventoryItem { id = "hospicio_recorder" });
            yield return Until(() => director.HasSignal("hospicio.recorder"));
            inventory.Add(new InventoryItem { id = "hospicio_director_tape" });
            flow.RecordListening("hospicio_director_tape");
            Assert.That(director.HasSequence("hospicio_director_tape.listened"), Is.True);
            Assert.That(director.HasSignal("hospicio.director-tape"), Is.False, "An early recording alone cannot unlock the stairs.");
            AssertClosedEvenWithKey();
            inventory.Add(new InventoryItem { id = "hospicio_child_tape" });
            flow.RecordListening("hospicio_child_tape");
            yield return Until(() => director.HasSignal("hospicio.child-tape"));
            inventory.Add(new InventoryItem { id = "hospicio_register" });
            yield return Until(() => director.HasSignal("hospicio.register"));
            AssertClosedEvenWithKey();
            Assert.That(director.Signal("hospicio.figure"), Is.True);
            Assert.That(director.HasSignal("hospicio.director-tape"), Is.False, "The tape milestone must still be accepted, not inferred from owning it.");
            AssertClosedEvenWithKey();
            yield return Until(() => director.HasSignal("hospicio.director-tape") && director.Progress.chapterId == "memories" && game.CanControl);
            Assert.That(director.HasSignal("hospicio.room-key"), Is.True);
            foreach (var door in new[] { flow.stairGate, flow.room113Door })
            {
                Assert.That(door.locked, Is.False);
                door.Interact(interaction);
                Assert.That(door.IsOpen, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator MigratedMemoriesCheckpointKeepsItsKeyAndOpenDoorsAccessible()
        {
            director.RestoreProgress(new NarrativeProgress { chapterId = "memories", storyRevision = 1 });
            director.InitializeAfterSession(true);
            Assert.That(director.MigratedStory, Is.True);
            Assert.That(inventory.Contains("room_113_key"), Is.True);
            Assert.That(director.HasSignal("hospicio.director-tape"), Is.True);
            foreach (var door in new[] { flow.stairGate, flow.room113Door }) door.SetOpen(true, true);
            flow.RestoreWorldSilently();
            foreach (var door in new[] { flow.stairGate, flow.room113Door })
            {
                Assert.That(door.locked, Is.False);
                Assert.That(door.IsOpen, Is.True, "Restoring an advanced checkpoint must not re-close a legitimately opened passage.");
            }
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1; AudioListener.pause = false;
            if (root) UnityEngine.Object.Destroy(root);
            if (book) UnityEngine.Object.Destroy(book);
            yield return null;
            var blank = SceneManager.CreateScene("SchoolGateCleanup-" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(blank);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (hadResume) PlayerPrefs.SetInt("Game.ResumeRequested", resume); else PlayerPrefs.DeleteKey("Game.ResumeRequested");
            if (hadCarry) PlayerPrefs.SetInt("Game.StoryCarryRequested", carry); else PlayerPrefs.DeleteKey("Game.StoryCarryRequested");
            Time.timeScale = timeScale; AudioListener.pause = listenerPaused;
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
        }
    }
}
