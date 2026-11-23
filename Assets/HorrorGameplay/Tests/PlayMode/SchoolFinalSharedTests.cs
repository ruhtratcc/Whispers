// Verifica SetUp, TearDown, BothSameSceneHandoffsCaptureTheirBaselineAfterWorldRestorationAndKeepSchoolExposure, AcceptedShocksInEachFinalChapterAreUniquePausedAndSilentOnRestore, TerraceRetryResetsOnlySixAndKeepsEveryIncomingChapterFiveDiscovery, LastExitRetryKeepsCompletedSixAndResetsOnlyTheCurrentFinalChapter, SavedTerraceBaselineDoesNotMoveToTheLaterCheckpoint, SavedLastExitBaselineDoesNotMoveToTheLaterCheckpoint, BuiltInEndingCompletesTheFinalStoryWithoutAnotherSceneOrChapter.

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
    /// <summary>Shared continuation, sanity, retry and final-story semantics.
    /// Production scene tests own terrace geometry, clues, door rays and presentation.</summary>
    [Category("SchoolFinalShared")]
    public sealed class SchoolFinalSharedTests
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
            hadSaveScene = PlayerPrefs.HasKey("Game.SaveScene"); savedScene = PlayerPrefs.GetString("Game.SaveScene", "");
            scene = SceneManager.CreateScene("School-final-isolated-" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(scene);
            root = new GameObject("Isolated final-school continuation"); root.SetActive(false);
            game = root.AddComponent<GameSession>(); game.enabled = false;
            inventory = root.AddComponent<Inventory>(); director = root.AddComponent<NarrativeDirector>();
            book = ScriptableObject.CreateInstance<StoryBook>(); StoryContent.Populate(book);
            // Scene coverage owns automatic environmental/caption ordering. The ending test
            // explicitly runs the existing built-in final sequence with its completion signal.
            foreach (var chapter in book.chapters)
                foreach (var milestone in chapter.milestones) milestone.automaticSequenceId = "";
            foreach (var sequence in book.sequences)
                foreach (var line in sequence.lines ?? Array.Empty<StoryLine>()) if (line != null) line.seconds = .12f;
            foreach (string id in new[] { "memories", "terrace", "last-exit" }) book.Chapter(id).sceneName = scene.name;
            director.book = book; director.game = game; director.inventory = inventory;
            director.startingChapterId = "memories"; director.autoLoadNextChapter = false;
            director.continueSameSceneChapters = true; game.narrative = director;
            player = Child("Player").AddComponent<FirstPersonMotor>(); player.enabled = false;
            player.transform.position = new Vector3(3, .1f, 2);
            var camera = Child("Camera", player.transform).AddComponent<Camera>(); camera.enabled = false;
            camera.transform.localPosition = Vector3.up * 1.5f;
            // Warp invokes Snap even on a disabled view; the body must remain a separate transform.
            player.view = camera.gameObject.AddComponent<FirstPersonCamera>();
            player.view.motor = player; player.view.playerCamera = camera; player.view.enabled = false;
            var lamp = Child("Lamp", camera.transform).AddComponent<Light>();
            flashlight = player.gameObject.AddComponent<FlashlightController>(); flashlight.view = camera;
            flashlight.lamp = lamp; flashlight.startsOn = false; flashlight.enabled = false; game.player = player;
            save = root.AddComponent<SaveSystem>(); save.player = player; save.inventory = inventory;
            save.flashlight = flashlight; save.narrative = director;
            save.fileName = "school-final-shared-test-" + Guid.NewGuid().ToString("N") + ".json";
            game.save = null; // Only explicit operations may write this fixture's disposable save.
            door = Child("Persistent door").AddComponent<InteractiveDoor>();
            door.persistentId = "final.fixture.door"; door.requiredKey = "";
            door.hinge = Child("Hinge", door.transform).transform;
            door.leafCollider = Child("Leaf", door.hinge).AddComponent<BoxCollider>();
            door.leafCollider.size = new Vector3(1, 2, .05f);
            sanity = root.AddComponent<SchoolSanityController>(); sanity.persistentId = "final.fixture.sanity";
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
        { var go = new GameObject(name); go.transform.SetParent(parent ? parent : root.transform, false); return go; }
        static List<InventoryItem> IncomingItems() => new List<InventoryItem>
        {
            new InventoryItem { id = "office_equipment", title = "Equipamento" },
            new InventoryItem { id = "school_photograph", title = "Turma antiga", note = true },
            new InventoryItem { id = "old_key", title = "Chave antiga" },
            new InventoryItem { id = "old_student_photo", title = "João — aluno", note = true },
            new InventoryItem { id = "memory_recording_one", title = "Fita 1", note = true },
            new InventoryItem { id = "memory_recording_two", title = "Fita 2", note = true },
            new InventoryItem { id = "memories_group_photo", title = "A noite esquecida", note = true }
        };
        IEnumerator ContinueMemories()
        {
            var progress = new NarrativeProgress { chapterId = "memories" };
            foreach (var chapter in book.chapters.Take(book.ChapterIndex("memories") + 1))
            {
                foreach (var milestone in chapter.milestones) progress.milestones.Add(milestone.id);
                if (chapter.id != "memories") progress.completedChapters.Add(chapter.id);
            }
            progress.sequences.AddRange(new[] { "road.arrival-call", "school.photo-inscription", "school.drawing-inscription",
                "left-behind.photo-inscription", "left-behind.wall-inscription", "memories.recording-one", "memories.recording-two", "memories.ghost" });
            director.RestoreProgress(progress); director.InitializeAfterSession(true);
            yield return Until(() => director.Progress.chapterId == "terrace" && game.CanControl);
        }
        void TerraceBeforeDescent()
        {
            foreach (var milestone in book.Chapter("terrace").milestones.Take(book.Chapter("terrace").milestones.Length - 1))
                Assert.That(director.Signal(milestone.id), Is.True, milestone.id);
        }
        IEnumerator FinishTerrace()
        {
            Assert.That(director.Signal("terrace.descent-exit"), Is.True);
            yield return Until(() => director.Progress.chapterId == "last-exit" && game.CanControl);
        }
        void FinalBeforeEnding()
        {
            foreach (var milestone in book.Chapter("last-exit").milestones.Take(book.Chapter("last-exit").milestones.Length - 1))
                Assert.That(director.Signal(milestone.id), Is.True, milestone.id);
        }
        IEnumerator Until(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "Final chapter continuation/presentation did not finish. Next: " + director.NextSignal);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1; AudioListener.pause = false;
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (save && File.Exists(save.SavePath + suffix)) File.Delete(save.SavePath + suffix);
            if (hadSaveScene) PlayerPrefs.SetString("Game.SaveScene", savedScene); else PlayerPrefs.DeleteKey("Game.SaveScene");
            PlayerPrefs.Save();
            if (root) UnityEngine.Object.Destroy(root); if (book) UnityEngine.Object.Destroy(book);
            yield return null;
            var blank = SceneManager.GetSceneByName("School-final-cleanup");
            if (!blank.IsValid() || !blank.isLoaded) blank = SceneManager.CreateScene("School-final-cleanup");
            SceneManager.SetActiveScene(blank); if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator BothSameSceneHandoffsCaptureTheirBaselineAfterWorldRestorationAndKeepSchoolExposure()
        {
            inventory.Restore(IncomingItems()); player.Warp(new Vector3(18, .6f, 27), 72, -11);
            flashlight.Restore(123, true); sanity.sanity = 70; door.SetOpen(true, true);
            int terraceEntries = 0, finalEntries = 0;
            director.Initialized += () =>
            {
                if (director.Progress.chapterId == "terrace") door.SetLocked(true);
                if (director.Progress.chapterId == "last-exit") door.SetLocked(false);
            };
            director.ChapterInitialized += id => { if (id == "terrace") terraceEntries++; if (id == "last-exit") finalEntries++; };
            yield return ContinueMemories();
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene)); Assert.That(sanity.IsSchoolActive, Is.True);
            Assert.That(terraceEntries, Is.EqualTo(1)); Assert.That(sanity.PrepareChapterRestart(out var terraceStart), Is.True);
            Assert.That(terraceStart.narrative.chapterId, Is.EqualTo("terrace"));
            Assert.That(terraceStart.position, Is.EqualTo(new Vector3(18, .6f, 27)));
            Assert.That(terraceStart.battery, Is.EqualTo(123)); Assert.That(terraceStart.flashlightOn, Is.True);
            StringAssert.Contains("\"locked\":true", terraceStart.objects.Single(item => item.id == door.persistentId).json);
            Assert.That(terraceStart.narrative.completedChapters.Contains("memories"), Is.True);
            sanity.darknessGraceSeconds = 0; sanity.darknessLossPerSecond = .22f;
            float before = sanity.sanity; sanity.AdvanceSanity(10, false);
            Assert.That(sanity.sanity, Is.EqualTo(before - 2.2f).Within(.001f)); sanity.darknessLossPerSecond = 0;
            TerraceBeforeDescent();
            inventory.Add(new InventoryItem { id = "terrace_revelation", note = true });
            player.Warp(new Vector3(20, .6f, 40), 35, -5); flashlight.Restore(61, false); sanity.sanity = 62;
            yield return FinishTerrace();
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene)); Assert.That(sanity.IsSchoolActive, Is.True);
            Assert.That(finalEntries, Is.EqualTo(1)); Assert.That(sanity.PrepareChapterRestart(out var finalStart), Is.True);
            Assert.That(finalStart.narrative.chapterId, Is.EqualTo("last-exit"));
            Assert.That(finalStart.position, Is.EqualTo(new Vector3(20, .6f, 40)));
            Assert.That(finalStart.battery, Is.EqualTo(61)); Assert.That(finalStart.flashlightOn, Is.False);
            Assert.That(finalStart.inventory.Any(item => item.id == "terrace_revelation"), Is.True);
            StringAssert.Contains("\"locked\":false", finalStart.objects.Single(item => item.id == door.persistentId).json);
            Assert.That(finalStart.narrative.completedChapters.Contains("terrace"), Is.True);
            sanity.darknessLossPerSecond = .22f; before = sanity.sanity; sanity.AdvanceSanity(10, false);
            Assert.That(sanity.sanity, Is.EqualTo(before - 2.2f).Within(.001f)); sanity.darknessLossPerSecond = 0;
            game.Pause(); before = sanity.sanity; sanity.AdvanceSanity(80, false);
            Assert.That(sanity.sanity, Is.EqualTo(before)); game.Resume();
            yield return Until(() => game.CanControl);
        }

        [UnityTest]
        public IEnumerator AcceptedShocksInEachFinalChapterAreUniquePausedAndSilentOnRestore()
        {
            yield return ContinueMemories(); sanity.sanity = 100;
            game.Pause(); sanity.AdvanceSanity(90, false);
            foreach (string signal in new[] { "terrace.voices-finished", "terrace.cycle-revelation", "terrace.final-chase" }) sanity.ReactToSignal(signal);
            Assert.That(sanity.sanity, Is.EqualTo(100)); game.Resume(); yield return Until(() => game.CanControl);
            TerraceBeforeDescent(); Assert.That(sanity.sanity, Is.EqualTo(80));
            foreach (string signal in new[] { "terrace.voices-finished", "terrace.cycle-revelation", "terrace.final-chase" }) sanity.ReactToSignal(signal);
            Assert.That(sanity.sanity, Is.EqualTo(80));
            string state = sanity.CaptureState(); var progress = director.CaptureProgress();
            sanity.sanity = 1; sanity.RestoreState(state); director.RestoreProgress(progress); director.InitializeAfterSession(true);
            sanity.ReactToSignal("terrace.cycle-revelation"); Assert.That(sanity.sanity, Is.EqualTo(80));
            yield return FinishTerrace(); sanity.sanity = 100;
            game.Pause();
            foreach (string signal in new[] { "last-exit.phone-ringing", "last-exit.final-call", "last-exit.last-window" }) sanity.ReactToSignal(signal);
            Assert.That(sanity.sanity, Is.EqualTo(100)); game.Resume(); yield return Until(() => game.CanControl);
            FinalBeforeEnding(); Assert.That(sanity.sanity, Is.EqualTo(90));
            foreach (string signal in new[] { "last-exit.phone-ringing", "last-exit.final-call", "last-exit.last-window" }) sanity.ReactToSignal(signal);
            Assert.That(sanity.sanity, Is.EqualTo(90));
            state = sanity.CaptureState(); progress = director.CaptureProgress();
            sanity.sanity = 1; sanity.RestoreState(state); director.RestoreProgress(progress); director.InitializeAfterSession(true);
            sanity.ReactToSignal("last-exit.final-call"); Assert.That(sanity.sanity, Is.EqualTo(90));
            StringAssert.Contains("\"version\":1", sanity.CaptureState());
        }

        [UnityTest]
        public IEnumerator TerraceRetryResetsOnlySixAndKeepsEveryIncomingChapterFiveDiscovery()
        {
            inventory.Restore(IncomingItems()); player.Warp(new Vector3(12, .6f, 19), 30, -8);
            flashlight.Restore(91, true); sanity.sanity = 68; door.SetOpen(true, true);
            yield return ContinueMemories(); TerraceBeforeDescent();
            director.Progress.sequences.Add("terrace.voices"); inventory.Add(new InventoryItem { id = "terrace_revelation", note = true });
            player.Warp(new Vector3(70, .6f, 80), 150); flashlight.Restore(4, false); sanity.sanity = 3; door.SetOpen(false, true);
            Assert.That(sanity.PrepareChapterRestart(out var restart), Is.True);
            Assert.That(restart.narrative.chapterId, Is.EqualTo("terrace"));
            Assert.That(restart.narrative.Next(book).id, Is.EqualTo("terrace.ascent-started"));
            Assert.That(restart.narrative.milestones.Any(id => id.StartsWith("terrace.", StringComparison.Ordinal)), Is.False);
            Assert.That(restart.narrative.sequences.Any(id => id.StartsWith("terrace.", StringComparison.Ordinal)), Is.False);
            CollectionAssert.AreEqual(new[] { "prologue", "road", "school", "left-behind", "memories" }, restart.narrative.completedChapters);
            Assert.That(restart.narrative.HasSignal("memories.childhood-recognized"), Is.True);
            Assert.That(restart.narrative.HasSequence("memories.recording-two"), Is.True);
            Assert.That(restart.position, Is.EqualTo(new Vector3(12, .6f, 19)));
            Assert.That(restart.heading, Is.EqualTo(30).Within(.01f)); Assert.That(restart.pitch, Is.EqualTo(-8).Within(.01f));
            Assert.That(restart.battery, Is.EqualTo(91)); Assert.That(restart.flashlightOn, Is.True);
            CollectionAssert.AreEquivalent(IncomingItems().Select(item => item.id), restart.inventory.Select(item => item.id));
            StringAssert.Contains("\"open\":true", restart.objects.Single(item => item.id == door.persistentId).json);
            sanity.RestoreState(restart.objects.Single(item => item.id == sanity.persistentId).json); Assert.That(sanity.sanity, Is.EqualTo(68));
            Assert.That(save.SaveSnapshot(restart), Is.True); Assert.That(SaveSystem.TryRead(save.SavePath, out var roundtrip), Is.True);
            Assert.That(roundtrip.version, Is.EqualTo(2)); Assert.That(roundtrip.narrative.chapterId, Is.EqualTo("terrace"));
        }

        [UnityTest]
        public IEnumerator LastExitRetryKeepsCompletedSixAndResetsOnlyTheCurrentFinalChapter()
        {
            inventory.Restore(IncomingItems()); yield return ContinueMemories(); TerraceBeforeDescent();
            director.Progress.sequences.Add("terrace.voices"); inventory.Add(new InventoryItem { id = "terrace_revelation", note = true });
            player.Warp(new Vector3(13, .6f, 41), 33, -9); flashlight.Restore(72, false); sanity.sanity = 62; door.SetOpen(true, true);
            yield return FinishTerrace(); FinalBeforeEnding();
            director.Progress.sequences.Add("last-exit.final-call"); inventory.Add(new InventoryItem { id = "last_exit_phone_note", note = true });
            player.Warp(new Vector3(90, .6f, 90), 120); flashlight.Restore(0, true); sanity.sanity = 2; door.SetOpen(false, true);
            Assert.That(sanity.PrepareChapterRestart(out var restart), Is.True);
            Assert.That(restart.narrative.chapterId, Is.EqualTo("last-exit"));
            Assert.That(restart.narrative.Next(book).id, Is.EqualTo("last-exit.entrance-reached"));
            Assert.That(restart.narrative.pendingChapterId, Is.Empty); Assert.That(restart.narrative.endingSeen, Is.False);
            Assert.That(restart.narrative.milestones.Any(id => id.StartsWith("last-exit.", StringComparison.Ordinal)), Is.False);
            Assert.That(restart.narrative.sequences.Any(id => id.StartsWith("last-exit.", StringComparison.Ordinal)), Is.False);
            CollectionAssert.AreEqual(new[] { "prologue", "road", "school", "left-behind", "memories", "terrace" }, restart.narrative.completedChapters);
            Assert.That(restart.narrative.HasSignal("terrace.descent-exit"), Is.True);
            Assert.That(restart.narrative.HasSequence("terrace.voices"), Is.True);
            Assert.That(restart.position, Is.EqualTo(new Vector3(13, .6f, 41)));
            Assert.That(restart.battery, Is.EqualTo(72)); Assert.That(restart.flashlightOn, Is.False);
            foreach (var item in IncomingItems()) Assert.That(restart.inventory.Any(saved => saved.id == item.id), Is.True, item.id);
            Assert.That(restart.inventory.Any(item => item.id == "terrace_revelation"), Is.True);
            Assert.That(restart.inventory.Any(item => item.id == "last_exit_phone_note"), Is.False);
            StringAssert.Contains("\"open\":true", restart.objects.Single(item => item.id == door.persistentId).json);
            sanity.RestoreState(restart.objects.Single(item => item.id == sanity.persistentId).json); Assert.That(sanity.sanity, Is.EqualTo(62));
        }

        IEnumerator SavedBaselineStaysAtEntry(string chapterId)
        {
            inventory.Restore(IncomingItems()); player.Warp(new Vector3(11, .6f, 22), 13);
            flashlight.Restore(123, true); sanity.sanity = 70;
            yield return ContinueMemories();
            if (chapterId == "last-exit") { TerraceBeforeDescent(); sanity.sanity = 70; yield return FinishTerrace(); }
            Assert.That(sanity.PrepareChapterRestart(out var entry), Is.True);
            if (chapterId == "terrace")
            {
                Assert.That(director.Signal("terrace.ascent-started"), Is.True);
                Assert.That(director.Signal("terrace.voices-finished"), Is.True);
            }
            else
            {
                Assert.That(director.Signal("last-exit.entrance-reached"), Is.True);
                Assert.That(director.Signal("last-exit.outside"), Is.True);
                Assert.That(director.Signal("last-exit.phone-ringing"), Is.True);
            }
            var checkpointPosition = new Vector3(80, .6f, 90); player.Warp(checkpointPosition, 90);
            flashlight.Restore(5, false); float checkpointSanity = sanity.sanity;
            Assert.That(save.Save(), Is.True); Assert.That(SaveSystem.TryRead(save.SavePath, out var roundtrip), Is.True);
            Assert.That(roundtrip.version, Is.EqualTo(2));
            player.Warp(new Vector3(200, 1, 200), 170); flashlight.Restore(0, false); sanity.sanity = 2;
            Assert.That(save.LoadCurrent(), Is.True); director.InitializeAfterSession(true);
            Assert.That(player.transform.position, Is.EqualTo(checkpointPosition)); Assert.That(flashlight.BatterySeconds, Is.EqualTo(5));
            Assert.That(sanity.sanity, Is.EqualTo(checkpointSanity));
            Assert.That(sanity.PrepareChapterRestart(out var restart), Is.True);
            Assert.That(restart.position, Is.EqualTo(new Vector3(11, .6f, 22))); Assert.That(restart.battery, Is.EqualTo(123));
            Assert.That(restart.narrative.chapterId, Is.EqualTo(chapterId));
            Assert.That(restart.objects.Single(item => item.id == sanity.persistentId).json,
                Is.EqualTo(entry.objects.Single(item => item.id == sanity.persistentId).json));
            float restoredSanity = sanity.sanity;
            sanity.ReactToSignal(chapterId == "terrace" ? "terrace.voices-finished" : "last-exit.phone-ringing");
            Assert.That(sanity.sanity, Is.EqualTo(restoredSanity), "Reloading cannot apply this checkpoint's completed shock twice.");
            sanity.RestoreState(restart.objects.Single(item => item.id == sanity.persistentId).json);
            Assert.That(sanity.sanity, Is.EqualTo(70)); StringAssert.Contains("\"version\":1", sanity.CaptureState());
        }
        [UnityTest]
        public IEnumerator SavedTerraceBaselineDoesNotMoveToTheLaterCheckpoint()
        { yield return SavedBaselineStaysAtEntry("terrace"); }
        [UnityTest]
        public IEnumerator SavedLastExitBaselineDoesNotMoveToTheLaterCheckpoint()
        { yield return SavedBaselineStaysAtEntry("last-exit"); }

        [UnityTest]
        public IEnumerator BuiltInEndingCompletesTheFinalStoryWithoutAnotherSceneOrChapter()
        {
            inventory.Restore(IncomingItems()); yield return ContinueMemories(); TerraceBeforeDescent();
            yield return FinishTerrace(); FinalBeforeEnding();
            Assert.That(director.NextSignal, Is.EqualTo("last-exit.ending"));
            Assert.That(director.PlaySequence("ending"), Is.True);
            yield return Until(() => director.HasSequence("ending") && game.State == GameState.StoryScreen);
            yield return null; yield return null;
            Assert.That(director.Progress.chapterId, Is.EqualTo("last-exit")); Assert.That(director.Progress.pendingChapterId, Is.Empty);
            Assert.That(director.Progress.endingSeen, Is.True); Assert.That(director.Progress.completedChapters.Contains("last-exit"), Is.True);
            Assert.That(director.Progress.milestones.Count(id => id == "last-exit.ending"), Is.EqualTo(1));
            Assert.That(director.Progress.sequences.Count(id => id == "ending"), Is.EqualTo(1));
            Assert.That(game.CanControl, Is.False); Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            float finishedSanity = sanity.sanity; sanity.AdvanceSanity(1000, false); Assert.That(sanity.sanity, Is.EqualTo(finishedSanity));
            Assert.That(sanity.PrepareChapterRestart(out var restart), Is.True);
            Assert.That(restart.narrative.chapterId, Is.EqualTo("last-exit")); Assert.That(restart.narrative.endingSeen, Is.False);
            Assert.That(restart.narrative.Next(book).id, Is.EqualTo("last-exit.entrance-reached"));
        }
    }
}
