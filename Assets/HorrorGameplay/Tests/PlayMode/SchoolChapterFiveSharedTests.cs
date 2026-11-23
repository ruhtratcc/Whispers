// Verifica SetUp, TearDown, MemoriesExposureUsesTheExistingLightAndPauseRules, ChapterFiveShocksAreUniqueAndLoadingDoesNotRepeatThem, RetryKeepsChaptersThreeAndFourAndOnlyResetsCurrentDiscoveries, SavedChapterFiveBaselineStaysAtEntryInsteadOfTheLaterCheckpoint, ChapterFiveCompletionStopsAtTheExistingCardWithoutDevelopingSix.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    /// <summary>Chapter-five coverage for the shared continuation, sanity and save seams.
    /// The integrated scene tests own clues, captions, route and apparition presentation.</summary>
    [Category("SchoolChapterFiveShared")]
    public sealed class SchoolChapterFiveSharedTests
    {
        Scene scene;
        GameObject root;
        StoryBook book;
        GameSession game;
        NarrativeDirector director;
        Inventory inventory;
        FirstPersonMotor player;
        FlashlightController flashlight;
        SaveSystem save;
        SchoolSanityController sanity;
        InteractiveDoor door;
        bool hadSaveScene;
        string savedScene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (var old in UnityEngine.Object.FindObjectsByType<GameSession>()) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            Time.timeScale = 1; AudioListener.pause = false;
            hadSaveScene = PlayerPrefs.HasKey("Game.SaveScene");
            savedScene = PlayerPrefs.GetString("Game.SaveScene", "");
            scene = SceneManager.CreateScene("School-memories-isolated-" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(scene);
            root = new GameObject("Isolated memories continuation"); root.SetActive(false);
            game = root.AddComponent<GameSession>(); game.enabled = false;
            inventory = root.AddComponent<Inventory>();
            director = root.AddComponent<NarrativeDirector>();
            book = ScriptableObject.CreateInstance<StoryBook>(); StoryContent.Populate(book);
            // Timing and caption completion belong to the actual scene fixture.
            foreach (var chapter in book.chapters)
                foreach (var milestone in chapter.milestones) milestone.automaticSequenceId = "";
            book.Chapter("left-behind").sceneName = scene.name;
            book.Chapter("memories").sceneName = scene.name;
            book.Chapter("terrace").sceneName = "";
            director.book = book; director.game = game; director.inventory = inventory;
            director.startingChapterId = "left-behind"; director.autoLoadNextChapter = false;
            director.continueSameSceneChapters = true;
            game.narrative = director;
            player = Child("Player").AddComponent<FirstPersonMotor>(); player.enabled = false;
            player.transform.position = new Vector3(3, .1f, 2);
            var camera = Child("Camera", player.transform).AddComponent<Camera>(); camera.enabled = false;
            camera.transform.localPosition = Vector3.up * 1.5f;
            // Warp calls Snap even on a disabled view. Keep the component on its camera child.
            player.view = camera.gameObject.AddComponent<FirstPersonCamera>();
            player.view.motor = player; player.view.playerCamera = camera; player.view.enabled = false;
            var lamp = Child("Lamp", camera.transform).AddComponent<Light>();
            flashlight = player.gameObject.AddComponent<FlashlightController>();
            flashlight.view = camera; flashlight.lamp = lamp; flashlight.startsOn = false; flashlight.enabled = false;
            game.player = player;
            save = root.AddComponent<SaveSystem>(); save.player = player; save.inventory = inventory;
            save.flashlight = flashlight; save.narrative = director;
            save.fileName = "chapter-five-shared-test-" + Guid.NewGuid().ToString("N") + ".json";
            // Only the explicit snapshot test writes its own disposable save.
            game.save = null;
            door = Child("Persistent door").AddComponent<InteractiveDoor>();
            door.persistentId = "memories.fixture.door"; door.requiredKey = "";
            door.hinge = Child("Hinge", door.transform).transform;
            door.leafCollider = Child("Leaf", door.hinge).AddComponent<BoxCollider>();
            door.leafCollider.size = new Vector3(1, 2, .05f);
            sanity = root.AddComponent<SchoolSanityController>();
            sanity.persistentId = "memories.fixture.sanity";
            sanity.enabledForSchool = true; sanity.schoolSceneName = scene.name;
            sanity.game = game; sanity.director = director; sanity.flashlight = flashlight;
            sanity.darknessLossPerSecond = 0; sanity.flashlightRecoveryPerSecond = 0;
            root.SetActive(true); director.InitializeAfterSession(false);
            // Baselines observe available equipment; freeze Update outside that synchronous capture.
            director.Initialized += () => flashlight.enabled = true;
            director.ChapterInitialized += _ => flashlight.enabled = false;
            yield return null;
        }

        GameObject Child(string name, Transform parent = null)
        {
            var go = new GameObject(name); go.transform.SetParent(parent ? parent : root.transform, false); return go;
        }

        IEnumerator ContinueChapterFour()
        {
            var progress = new NarrativeProgress { chapterId = "left-behind" };
            progress.completedChapters.AddRange(new[] { "prologue", "road", "school" });
            progress.milestones.AddRange(new[] { "prologue.equipment", "road.school-gate" });
            foreach (string id in new[] { "school", "left-behind" })
                foreach (var milestone in book.Chapter(id).milestones) progress.milestones.Add(milestone.id);
            progress.sequences.AddRange(new[] { "road.arrival-call", "school.photo-inscription",
                "school.drawing-inscription", "left-behind.photo-inscription", "left-behind.wall-inscription" });
            director.RestoreProgress(progress); director.InitializeAfterSession(true);
            yield return Until(() => director.Progress.chapterId == "memories" && game.CanControl);
        }

        IEnumerator Until(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 4;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "The in-place chapter-five continuation did not finish.");
        }

        void RevealMemories()
        {
            foreach (string signal in new[] { "memories.past-echoes", "memories.recording-one-found",
                "memories.recording-one", "memories.recording-two-found", "memories.recording-two",
                "memories.childhood-recognized", "memories.ghost-appeared" })
                Assert.That(director.Signal(signal), Is.True, signal);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1; AudioListener.pause = false;
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (save && File.Exists(save.SavePath + suffix)) File.Delete(save.SavePath + suffix);
            if (hadSaveScene) PlayerPrefs.SetString("Game.SaveScene", savedScene); else PlayerPrefs.DeleteKey("Game.SaveScene");
            PlayerPrefs.Save();
            if (root) UnityEngine.Object.Destroy(root);
            if (book) UnityEngine.Object.Destroy(book);
            yield return null;
            var blank = SceneManager.GetSceneByName("School-memories-cleanup");
            if (!blank.IsValid() || !blank.isLoaded) blank = SceneManager.CreateScene("School-memories-cleanup");
            SceneManager.SetActiveScene(blank);
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator MemoriesExposureUsesTheExistingLightAndPauseRules()
        {
            yield return ContinueChapterFour();
            Assert.That(sanity.IsSchoolActive, Is.True);
            sanity.RestoreState("{\"version\":1,\"sanity\":80,\"darknessSeconds\":0}");
            sanity.darknessLossPerSecond = .22f;
            sanity.AdvanceSanity(20, false); Assert.That(sanity.sanity, Is.EqualTo(80).Within(.001f));
            sanity.AdvanceSanity(10, false); Assert.That(sanity.sanity, Is.EqualTo(77.8f).Within(.001f));
            float dark = sanity.DarknessSeconds;
            game.Pause(); sanity.AdvanceSanity(100, false); sanity.ReactToSignal("memories.past-echoes");
            Assert.That(sanity.sanity, Is.EqualTo(77.8f).Within(.001f));
            Assert.That(sanity.DarknessSeconds, Is.EqualTo(dark));
            game.Resume();
            // Resume intentionally suppresses gameplay for its first frame.
            sanity.darknessLossPerSecond = 0;
            yield return Until(() => game.CanControl);
            sanity.RestoreState("{\"version\":1,\"sanity\":77.8,\"darknessSeconds\":30}");
            flashlight.enabled = true;
            sanity.flashlightRecoveryPerSecond = .14f; flashlight.Restore(100, true);
            sanity.AdvanceSanity(10, false); Assert.That(sanity.sanity, Is.EqualTo(79.2f).Within(.001f));
            flashlight.enabled = false;
            sanity.enabledForSchool = false; sanity.AdvanceSanity(100, false);
            Assert.That(sanity.sanity, Is.EqualTo(79.2f).Within(.001f));
        }

        [UnityTest]
        public IEnumerator ChapterFiveShocksAreUniqueAndLoadingDoesNotRepeatThem()
        {
            yield return ContinueChapterFour(); sanity.sanity = 100;
            RevealMemories(); Assert.That(sanity.sanity, Is.EqualTo(64));
            foreach (string id in new[] { "memories.past-echoes", "memories.recording-one", "memories.recording-two",
                "memories.childhood-recognized", "memories.ghost-appeared" }) sanity.ReactToSignal(id);
            Assert.That(sanity.sanity, Is.EqualTo(64));
            var progress = director.CaptureProgress(); string checkpoint = sanity.CaptureState();
            sanity.sanity = 1; sanity.RestoreState(checkpoint);
            director.RestoreProgress(progress); director.InitializeAfterSession(true);
            sanity.ReactToSignal("memories.childhood-recognized");
            Assert.That(sanity.sanity, Is.EqualTo(64));
            StringAssert.Contains("\"version\":1", sanity.CaptureState());
        }

        [UnityTest]
        public IEnumerator RetryKeepsChaptersThreeAndFourAndOnlyResetsCurrentDiscoveries()
        {
            inventory.Add(new InventoryItem { id = "office_equipment" });
            inventory.Add(new InventoryItem { id = "school_photograph", note = true });
            inventory.Add(new InventoryItem { id = "old_key" });
            inventory.Add(new InventoryItem { id = "old_disappearance_report", note = true });
            player.Warp(new Vector3(12, .6f, 19), 30, -8); flashlight.Restore(91, true);
            sanity.sanity = 68; door.SetOpen(true, true);
            yield return ContinueChapterFour();
            RevealMemories();
            inventory.Add(new InventoryItem { id = "memories_recording_one", note = true });
            inventory.Add(new InventoryItem { id = "memories_childhood_photo", note = true });
            director.Progress.sequences.AddRange(new[] { "memories.recording-one", "memories.recording-two" });
            player.Warp(new Vector3(70, .6f, 80), 150); flashlight.Restore(4, false);
            sanity.sanity = 8; door.SetOpen(false, true);
            Assert.That(sanity.PrepareChapterRestart(out var restart), Is.True);
            Assert.That(restart.narrative.chapterId, Is.EqualTo("memories"));
            Assert.That(restart.narrative.Next(book).id, Is.EqualTo("memories.past-echoes"));
            Assert.That(restart.narrative.milestones.Any(id => id.StartsWith("memories.", StringComparison.Ordinal)), Is.False);
            Assert.That(restart.narrative.sequences.Any(id => id.StartsWith("memories.", StringComparison.Ordinal)), Is.False);
            CollectionAssert.AreEqual(new[] { "prologue", "road", "school", "left-behind" }, restart.narrative.completedChapters);
            Assert.That(restart.narrative.HasSignal("school.blackout"), Is.True);
            Assert.That(restart.narrative.HasSignal("left-behind.locked-area-opened"), Is.True);
            Assert.That(restart.narrative.HasSequence("school.photo-inscription"), Is.True);
            Assert.That(restart.narrative.HasSequence("left-behind.wall-inscription"), Is.True);
            Assert.That(restart.position, Is.EqualTo(new Vector3(12, .6f, 19)));
            Assert.That(restart.heading, Is.EqualTo(30).Within(.01f)); Assert.That(restart.pitch, Is.EqualTo(-8).Within(.01f));
            Assert.That(restart.battery, Is.EqualTo(91)); Assert.That(restart.flashlightOn, Is.True);
            CollectionAssert.AreEquivalent(new[] { "office_equipment", "school_photograph", "old_key", "old_disappearance_report" },
                restart.inventory.Select(item => item.id));
            StringAssert.Contains("\"open\":true", restart.objects.Single(item => item.id == door.persistentId).json);
            sanity.RestoreState(restart.objects.Single(item => item.id == sanity.persistentId).json);
            Assert.That(sanity.sanity, Is.EqualTo(68));
        }

        [UnityTest]
        public IEnumerator SavedChapterFiveBaselineStaysAtEntryInsteadOfTheLaterCheckpoint()
        {
            player.Warp(new Vector3(11, .6f, 22), 13); flashlight.Restore(123, true);
            sanity.RestoreState("{\"version\":1,\"sanity\":70,\"darknessSeconds\":12,\"feltSignals\":[\"left-behind.demon-appeared\"]}");
            yield return ContinueChapterFour();
            RevealMemories();
            Assert.That(sanity.PrepareChapterRestart(out var entry), Is.True);
            string state = sanity.CaptureState(); var progress = director.CaptureProgress();
            var checkpoint = new SaveData { scene = scene.name, position = new Vector3(80, .6f, 90),
                battery = 5, narrative = progress, inventory = new List<InventoryItem>() };
            checkpoint.objects.Add(new SavedObject { id = sanity.persistentId, json = state });
            Assert.That(save.SaveSnapshot(checkpoint), Is.True);
            Assert.That(SaveSystem.TryRead(save.SavePath, out var roundtrip), Is.True);
            Assert.That(roundtrip.version, Is.EqualTo(2));
            player.Warp(roundtrip.position, 90); flashlight.Restore(roundtrip.battery, false); sanity.sanity = 2;
            sanity.RestoreState(roundtrip.objects.Single(item => item.id == sanity.persistentId).json);
            director.RestoreProgress(roundtrip.narrative); director.InitializeAfterSession(true);
            Assert.That(sanity.PrepareChapterRestart(out var restart), Is.True);
            Assert.That(restart.position, Is.EqualTo(new Vector3(11, .6f, 22)));
            Assert.That(restart.battery, Is.EqualTo(123)); Assert.That(restart.flashlightOn, Is.True);
            Assert.That(director.HasSignal("memories.childhood-recognized"), Is.True);
            sanity.ReactToSignal("memories.childhood-recognized"); Assert.That(sanity.sanity, Is.EqualTo(34));
            sanity.RestoreState(restart.objects.Single(item => item.id == sanity.persistentId).json);
            Assert.That(sanity.sanity, Is.EqualTo(70));
            // Returning to this chapter reuses earlier felt-shock history without applying it again.
            sanity.ReactToSignal("left-behind.demon-appeared"); Assert.That(sanity.sanity, Is.EqualTo(70));
            StringAssert.Contains("\"version\":1", restart.objects.Single(item => item.id == sanity.persistentId).json);
            Assert.That(restart.objects.Single(item => item.id == sanity.persistentId).json,
                Is.EqualTo(entry.objects.Single(item => item.id == sanity.persistentId).json));
        }

        [UnityTest]
        public IEnumerator ChapterFiveCompletionStopsAtTheExistingCardWithoutDevelopingSix()
        {
            yield return ContinueChapterFour(); RevealMemories();
            Assert.That(director.Signal("memories.staircase-indicated"), Is.True);
            yield return null; yield return null;
            Assert.That(game.State, Is.EqualTo(GameState.StoryScreen));
            Assert.That(game.CanControl, Is.False);
            Assert.That(director.Progress.chapterId, Is.EqualTo("memories"));
            Assert.That(director.Progress.pendingChapterId, Is.EqualTo("terrace"));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            Assert.That(sanity.PrepareChapterRestart(out var restart), Is.True);
            Assert.That(restart.narrative.completedChapters.Contains("memories"), Is.False);
            Assert.That(restart.narrative.Next(book).id, Is.EqualTo("memories.past-echoes"));
        }
    }
}
