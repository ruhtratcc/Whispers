// Verifica SetUp, TearDown, OptInKeepsWorldGearPositionAndNotifiesAfterWorldRestoration, DefaultsKeepTheExistingCompletionCardAndPendingChapter, RetrySnapshotResetsOnlyChapterFourAndKeepsItsIncomingWorldAndSanity, RestoredChapterFourBaselineDoesNotMoveToTheLaterCheckpoint, UniqueChapterFourShocksAndFinalCardNeverStartChapterFive, ExistingVersionOneSchoolBaselineStillRestartsAtFullSanity.

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
    [Category("SchoolChapterFourShared")]
    public sealed class SchoolChapterContinuationTests
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

        [Serializable] sealed class LegacySanityData
        {
            public int version = 1;
            public float sanity, darknessSeconds;
            public List<string> feltSignals = new List<string>();
            public SaveData chapterStart;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (var old in UnityEngine.Object.FindObjectsByType<GameSession>()) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            Time.timeScale = 1; AudioListener.pause = false;
            hadSaveScene = PlayerPrefs.HasKey("Game.SaveScene");
            savedScene = PlayerPrefs.GetString("Game.SaveScene", "");
            scene = SceneManager.CreateScene("School-continuation-isolated-" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(scene);
            root = new GameObject("Isolated school continuation"); root.SetActive(false);
            game = root.AddComponent<GameSession>(); game.enabled = false;
            inventory = root.AddComponent<Inventory>();
            director = root.AddComponent<NarrativeDirector>();
            book = ScriptableObject.CreateInstance<StoryBook>(); StoryContent.Populate(book);
            // These tests prove shared state/transition behavior; scene tests own
            // the duration and ordering of each chapter's authored captions.
            foreach (var chapter in book.chapters)
                foreach (var milestone in chapter.milestones) milestone.automaticSequenceId = "";
            book.Chapter("school").sceneName = scene.name;
            book.Chapter("left-behind").sceneName = scene.name;
            book.Chapter("memories").sceneName = "";
            director.book = book; director.game = game; director.inventory = inventory;
            director.startingChapterId = "school"; director.autoLoadNextChapter = false;
            game.narrative = director;
            player = Child("Player").AddComponent<FirstPersonMotor>(); player.enabled = false;
            player.transform.position = new Vector3(3, .1f, 2);
            var camera = Child("Camera", player.transform).AddComponent<Camera>(); camera.enabled = false;
            camera.transform.localPosition = Vector3.up * 1.5f;
            // Warp explicitly calls Snap even when the camera component is disabled.
            // Keep the view on its camera child, as in gameplay, so Snap cannot move the body.
            player.view = camera.gameObject.AddComponent<FirstPersonCamera>();
            player.view.motor = player; player.view.playerCamera = camera; player.view.enabled = false;
            var lamp = Child("Lamp", camera.transform).AddComponent<Light>();
            flashlight = player.gameObject.AddComponent<FlashlightController>();
            flashlight.view = camera; flashlight.lamp = lamp; flashlight.startsOn = false; flashlight.enabled = false;
            game.player = player;
            save = root.AddComponent<SaveSystem>(); save.player = player; save.inventory = inventory;
            save.flashlight = flashlight; save.narrative = director;
            save.fileName = "chapter-continuation-test-" + Guid.NewGuid().ToString("N") + ".json";
            // No automatic saves. The explicit snapshot test owns a disposable file.
            game.save = null;
            door = Child("Persistent door").AddComponent<InteractiveDoor>();
            door.persistentId = "continuation.fixture.door"; door.requiredKey = "";
            door.hinge = Child("Hinge", door.transform).transform;
            door.leafCollider = Child("Leaf", door.hinge).AddComponent<BoxCollider>();
            door.leafCollider.size = new Vector3(1, 2, .05f);
            sanity = root.AddComponent<SchoolSanityController>();
            sanity.persistentId = "continuation.fixture.sanity";
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

        void CompletedOpening()
        {
            var progress = new NarrativeProgress { chapterId = "school" };
            progress.completedChapters.AddRange(new[] { "prologue", "road" });
            progress.milestones.Add("road.school-gate"); progress.sequences.Add("road.arrival-call");
            foreach (var signal in book.Chapter("school").milestones) progress.milestones.Add(signal.id);
            progress.sequences.AddRange(new[] { "school.photo-inscription", "school.blackboard-inscription", "school.drawing-inscription" });
            director.RestoreProgress(progress);
            director.InitializeAfterSession(true);
        }

        IEnumerator ContinueOpening()
        {
            director.continueSameSceneChapters = true;
            CompletedOpening();
            yield return Until(() => director.Progress.chapterId == "left-behind" && game.CanControl);
        }

        IEnumerator Until(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 4;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "In-place continuation did not finish.");
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
            var blank = SceneManager.GetSceneByName("School-continuation-cleanup");
            if (!blank.IsValid() || !blank.isLoaded) blank = SceneManager.CreateScene("School-continuation-cleanup");
            SceneManager.SetActiveScene(blank);
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator OptInKeepsWorldGearPositionAndNotifiesAfterWorldRestoration()
        {
            player.Warp(new Vector3(18, .3f, 27), 72, -11);
            inventory.Add(new InventoryItem { id = "office_equipment" });
            inventory.Add(new InventoryItem { id = "school_documents", note = true });
            flashlight.Restore(123, true); sanity.sanity = 64;
            door.SetLocked(false); door.SetOpen(true, true);
            director.Initialized += () => { if (director.Progress.chapterId == "left-behind") door.SetLocked(true); };
            int entered = 0;
            director.ChapterInitialized += id => { if (id == "left-behind") entered++; };
            yield return ContinueOpening();
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            Assert.That(player.transform.position, Is.EqualTo(new Vector3(18, .3f, 27)));
            Assert.That(player.transform.eulerAngles.y, Is.EqualTo(72).Within(.01f));
            Assert.That(player.Pitch, Is.EqualTo(-11).Within(.01f));
            flashlight.enabled = true;
            Assert.That(flashlight.BatterySeconds, Is.EqualTo(123)); Assert.That(flashlight.IsOn, Is.True);
            flashlight.enabled = false;
            Assert.That(door.locked && door.IsOpen, Is.True);
            Assert.That(inventory.Contains("school_documents"), Is.True);
            Assert.That(sanity.sanity, Is.EqualTo(64)); Assert.That(entered, Is.EqualTo(1));
            Assert.That(sanity.PrepareChapterRestart(out var start), Is.True);
            StringAssert.Contains("\"locked\":true", start.objects.Single(i => i.id == door.persistentId).json,
                "The chapter baseline must be captured after Initialized restores its world.");
            CollectionAssert.Contains(director.Progress.completedChapters, "school");
            Assert.That(director.Progress.pendingChapterId, Is.Empty);
            Assert.That(director.NextSignal, Is.EqualTo("left-behind.documents-found"));
        }

        [UnityTest]
        public IEnumerator DefaultsKeepTheExistingCompletionCardAndPendingChapter()
        {
            Assert.That(director.continueSameSceneChapters, Is.False);
            CompletedOpening(); yield return null; yield return null;
            Assert.That(director.Progress.chapterId, Is.EqualTo("school"));
            Assert.That(director.Progress.pendingChapterId, Is.EqualTo("left-behind"));
            Assert.That(game.State, Is.EqualTo(GameState.StoryScreen));
            Assert.That(game.CanControl, Is.False); Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
        }

        [UnityTest]
        public IEnumerator RetrySnapshotResetsOnlyChapterFourAndKeepsItsIncomingWorldAndSanity()
        {
            inventory.Add(new InventoryItem { id = "office_equipment" });
            inventory.Add(new InventoryItem { id = "school_photograph", note = true });
            player.Warp(new Vector3(12, .1f, 19), 30); flashlight.Restore(91, true);
            sanity.sanity = 68; door.SetOpen(true, true);
            yield return ContinueOpening();
            inventory.Add(new InventoryItem { id = "left-behind-report", note = true });
            inventory.Add(new InventoryItem { id = "old_key" });
            director.Signal("left-behind.documents-found"); director.Signal("left-behind.photograph");
            director.Signal("left-behind.key-found");
            player.Warp(new Vector3(70, .1f, 80), 150); flashlight.Restore(4, false);
            sanity.sanity = 8; door.SetOpen(false, true);
            Assert.That(sanity.PrepareChapterRestart(out var restart), Is.True);
            Assert.That(restart.narrative.chapterId, Is.EqualTo("left-behind"));
            Assert.That(restart.narrative.Next(book).id, Is.EqualTo("left-behind.documents-found"));
            Assert.That(restart.narrative.HasSignal("school.blackout"), Is.True);
            Assert.That(restart.narrative.HasSequence("school.photo-inscription"), Is.True);
            CollectionAssert.Contains(restart.narrative.completedChapters, "school");
            Assert.That(restart.position, Is.EqualTo(new Vector3(12, .1f, 19)));
            Assert.That(restart.battery, Is.EqualTo(91)); Assert.That(restart.flashlightOn, Is.True);
            Assert.That(restart.inventory.Any(i => i.id == "school_photograph"), Is.True);
            Assert.That(restart.inventory.Any(i => i.id == "old_key" || i.id == "left-behind-report"), Is.False);
            StringAssert.Contains("\"open\":true", restart.objects.Single(i => i.id == door.persistentId).json);
            sanity.RestoreState(restart.objects.Single(i => i.id == sanity.persistentId).json);
            Assert.That(sanity.sanity, Is.EqualTo(68));
            Assert.That(save.SaveSnapshot(restart), Is.True);
            Assert.That(SaveSystem.TryRead(save.SavePath, out var roundtrip), Is.True);
            Assert.That(roundtrip.version, Is.EqualTo(2));
            Assert.That(roundtrip.narrative.chapterId, Is.EqualTo("left-behind"));
        }

        [UnityTest]
        public IEnumerator RestoredChapterFourBaselineDoesNotMoveToTheLaterCheckpoint()
        {
            player.Warp(new Vector3(11, .1f, 22), 13); sanity.sanity = 70;
            yield return ContinueOpening();
            director.Signal("left-behind.documents-found");
            var progress = director.CaptureProgress(); string state = sanity.CaptureState();
            player.Warp(new Vector3(80, .1f, 90), 90); sanity.sanity = 20;
            sanity.RestoreState(state); director.RestoreProgress(progress); director.InitializeAfterSession(true);
            Assert.That(sanity.PrepareChapterRestart(out var restart), Is.True);
            Assert.That(restart.position, Is.EqualTo(new Vector3(11, .1f, 22)));
            Assert.That(director.HasSignal("left-behind.documents-found"), Is.True);
            float restored = sanity.sanity; sanity.ReactToSignal("left-behind.documents-found");
            Assert.That(sanity.sanity, Is.EqualTo(restored), "Loading must not apply a completed shock again.");
        }

        [UnityTest]
        public IEnumerator UniqueChapterFourShocksAndFinalCardNeverStartChapterFive()
        {
            yield return ContinueOpening(); sanity.sanity = 100;
            Assert.That(director.Signal("left-behind.documents-found"), Is.True);
            Assert.That(director.Signal("left-behind.photograph"), Is.True);
            inventory.Add(new InventoryItem { id = "old_key" });
            Assert.That(director.Signal("left-behind.key-found"), Is.True);
            Assert.That(director.Signal("left-behind.demon-appeared"), Is.True);
            Assert.That(director.Signal("left-behind.chase-started"), Is.True);
            Assert.That(director.Signal("left-behind.safe-place"), Is.True);
            Assert.That(director.Signal("left-behind.wall-message"), Is.True);
            Assert.That(sanity.sanity, Is.EqualTo(52));
            sanity.ReactToSignal("left-behind.demon-appeared"); Assert.That(sanity.sanity, Is.EqualTo(52));
            Assert.That(director.Signal("left-behind.locked-area-opened"), Is.True);
            yield return null; yield return null;
            Assert.That(game.State, Is.EqualTo(GameState.StoryScreen));
            Assert.That(director.Progress.chapterId, Is.EqualTo("left-behind"));
            Assert.That(director.Progress.pendingChapterId, Is.EqualTo("memories"));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
        }

        [Test]
        public void ExistingVersionOneSchoolBaselineStillRestartsAtFullSanity()
        {
            Assert.That(sanity.PrepareChapterRestart(out var opening), Is.True);
            opening.objects.RemoveAll(i => i.id == sanity.persistentId);
            var old = new LegacySanityData { sanity = 36, darknessSeconds = 25, chapterStart = opening };
            old.feltSignals.Add("school.photograph"); sanity.RestoreState(JsonUtility.ToJson(old));
            director.InitializeAfterSession(true);
            Assert.That(sanity.sanity, Is.EqualTo(36));
            Assert.That(sanity.PrepareChapterRestart(out var retry), Is.True);
            sanity.RestoreState(retry.objects.Single(i => i.id == sanity.persistentId).json);
            Assert.That(sanity.sanity, Is.EqualTo(100)); Assert.That(sanity.DarknessSeconds, Is.Zero);
            StringAssert.Contains("\"version\":1", sanity.CaptureState());
        }
    }
}
