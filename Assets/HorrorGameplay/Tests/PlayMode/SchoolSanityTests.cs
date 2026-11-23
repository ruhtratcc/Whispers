// Verifica SetUp, TearDown, EntryPauseAndAuthoredCaptionsSuspendDarkness, GraceFlashlightAndFiniteMemoryAnchorRecoverGradually, EmptyBatteryDarknessEscalatesAfterGraceAndPauseOrLightPreventsDeath, DarknessRetryOnlyRepairsItsOwnSanityAndKeepsTheSavedWorldAndEmptyBattery, ARevelationStillCausesTheLegacyBlackoutEvenWhenTheLampIsEmpty, ExhaustedDarknessFadesIntoDeathWithoutOverwritingTheLastCheckpoint, AcceptedNarrativeShocksAreUniqueAndRestorationIsSilent, ChapterStartSnapshotKeepsIncomingGearAndEarlierProgressWithFreshScenery, AutomaticBlackoutPreservesLatestCheckpointAndBlocksPendingSaves, LegacyAndBackupCheckpointsRejectAnotherSceneAndRetainInspectionMetadata, LegacyCheckpointMissingNewObjectRecordsRestoresDefaultsAndNarrativeShocks, SchoolOptInAndLampPresentationRestoreTheBatteryBaseline, SnapshotWriterRejectsWrongSceneAndNonfiniteDataWithoutWriting.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HorrorGame.Tests
{
    [DefaultExecutionOrder(500)]
    public sealed class SchoolLampProbe : MonoBehaviour
    {
        public Light lamp;
        public float observed;
        void LateUpdate() { if(lamp) observed=lamp.intensity; }
    }
    [Category("SchoolSanity")]
    [Category("GameplayPolish")]
    public sealed class SchoolSanityTests
    {
        Scene scene;
        GameObject root;
        GameSession game;
        NarrativeDirector director;
        StoryBook book;
        Inventory inventory;
        FirstPersonMotor player;
        FlashlightController flashlight;
        SaveSystem save;
        SchoolSanityController sanity;
        InteractiveDoor door;
        bool hadSaveScene;
        string originalSaveScene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (var old in UnityEngine.Object.FindObjectsByType<GameSession>()) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            Time.timeScale = 1; AudioListener.pause = false;
            hadSaveScene = PlayerPrefs.HasKey("Game.SaveScene");
            originalSaveScene = PlayerPrefs.GetString("Game.SaveScene", "");
            scene = SceneManager.CreateScene("School-sanity-isolated-" + Guid.NewGuid());
            SceneManager.SetActiveScene(scene);
            root = new GameObject("School sanity isolated fixture"); root.SetActive(false);
            game = root.AddComponent<GameSession>(); game.enabled = false;
            inventory = root.AddComponent<Inventory>();
            director = root.AddComponent<NarrativeDirector>();
            book = ScriptableObject.CreateInstance<StoryBook>(); StoryContent.Populate(book);
            book.Chapter("school").sceneName = scene.name;
            director.book = book; director.game = game; director.inventory = inventory; director.startingChapterId = "school";
            game.narrative = director;
            var playerObject = Child("Player"); playerObject.transform.position = new Vector3(3, .1f, 2);
            player = playerObject.AddComponent<FirstPersonMotor>(); player.enabled = false;
            var camera = Child("Camera", playerObject.transform).AddComponent<Camera>(); camera.enabled = false;
            camera.gameObject.AddComponent<AudioListener>();
            camera.transform.localPosition = Vector3.up * 1.5f;
            player.view = camera.gameObject.AddComponent<FirstPersonCamera>(); player.view.motor = player; player.view.playerCamera = camera;
            player.view.enabled = false;
            var lamp = Child("Lamp", camera.transform).AddComponent<Light>(); lamp.intensity = 10;
            flashlight = playerObject.AddComponent<FlashlightController>(); flashlight.view = camera; flashlight.lamp = lamp;
            flashlight.startsOn = false;
            game.player = player;
            save = root.AddComponent<SaveSystem>(); save.player = player; save.inventory = inventory;
            save.flashlight = flashlight; save.narrative = director;
            save.fileName = "school-sanity-test-" + Guid.NewGuid().ToString("N") + ".json";
            // Narrative auto-save stays disconnected; only the explicit snapshot test touches its disposable file.
            game.save = null;
            var doorObject = Child("Door"); door = doorObject.AddComponent<InteractiveDoor>();
            door.persistentId = "sanity.fixture.door"; door.requiredKey = "";
            door.hinge = Child("Hinge", doorObject.transform).transform;
            door.leafCollider = Child("Leaf", door.hinge).AddComponent<BoxCollider>();
            door.leafCollider.center = new Vector3(.5f, 1, 0); door.leafCollider.size = new Vector3(1, 2, .05f);
            sanity = root.AddComponent<SchoolSanityController>(); sanity.persistentId = "sanity.fixture.controller";
            sanity.enabledForSchool = true; sanity.schoolSceneName = scene.name;
            sanity.game = game; sanity.director = director; sanity.flashlight = flashlight;
            sanity.darknessLossPerSecond = 0; // Tests step the clock explicitly; incidental Unity frames never alter the assertions.
            root.SetActive(true);
            director.InitializeAfterSession(false);
            yield return null;
        }

        GameObject Child(string name, Transform parent = null)
        {
            var result = new GameObject(name); result.transform.SetParent(parent ? parent : root.transform, false); return result;
        }

        void Enter() { Assert.That(director.Signal("school.entered"), Is.True); }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1; AudioListener.pause = false;
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (save && File.Exists(save.SavePath + suffix)) File.Delete(save.SavePath + suffix);
            if (hadSaveScene) PlayerPrefs.SetString("Game.SaveScene", originalSaveScene); else PlayerPrefs.DeleteKey("Game.SaveScene");
            PlayerPrefs.Save();
            if (root) UnityEngine.Object.Destroy(root);
            if (book) UnityEngine.Object.Destroy(book);
            yield return null;
            var blank = SceneManager.GetSceneByName("School-sanity-cleanup");
            if (!blank.IsValid() || !blank.isLoaded) blank = SceneManager.CreateScene("School-sanity-cleanup");
            SceneManager.SetActiveScene(blank);
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator EntryPauseAndAuthoredCaptionsSuspendDarkness()
        {
            sanity.darknessLossPerSecond = .22f;
            sanity.AdvanceSanity(80, false);
            Assert.That(sanity.sanity, Is.EqualTo(100)); Assert.That(sanity.DarknessSeconds, Is.Zero);
            Enter(); game.Pause(); sanity.AdvanceSanity(80, false);
            Assert.That(sanity.sanity, Is.EqualTo(100));
            game.Resume(); yield return null;
            Assert.That(director.Signal("school.entry-locked"), Is.True);
            Assert.That(director.Signal("school.documents-found"), Is.True);
            float beforeCaption = sanity.DarknessSeconds;
            foreach (var line in book.Sequence("school.photo-inscription").lines) line.seconds = 30;
            Assert.That(director.PlaySequence("school.photo-inscription"), Is.True);
            yield return null;
            sanity.AdvanceSanity(80, false);
            Assert.That(sanity.DarknessSeconds, Is.EqualTo(beforeCaption)); Assert.That(sanity.sanity, Is.EqualTo(100));
        }

        [Test]
        public void GraceFlashlightAndFiniteMemoryAnchorRecoverGradually()
        {
            Enter(); sanity.sanity = 70; sanity.darknessLossPerSecond = .22f;
            sanity.AdvanceSanity(20, false); Assert.That(sanity.sanity, Is.EqualTo(70).Within(.001f));
            sanity.AdvanceSanity(10, false); Assert.That(sanity.sanity, Is.EqualTo(67.8f).Within(.001f));
            flashlight.Restore(100, true); sanity.AdvanceSanity(10, false);
            Assert.That(sanity.sanity, Is.EqualTo(69.2f).Within(.001f));
            flashlight.Restore(100, false);
            var anchor = Child("Memory anchor").AddComponent<SchoolMemoryAnchor>();
            anchor.transform.position = player.transform.position; anchor.recoveryPerSecond = .5f;
            sanity.anchors = new[] { anchor }; sanity.AdvanceSanity(10, false);
            Assert.That(sanity.sanity, Is.EqualTo(74.2f).Within(.001f));
            anchor.transform.position += Vector3.right * 100; sanity.AdvanceSanity(25, false);
            Assert.That(sanity.sanity, Is.EqualTo(73.1f).Within(.001f));
            anchor.requiresLight = true; anchor.transform.position = player.transform.position;
            Assert.That(anchor.Contains(player.transform.position), Is.False);
        }

        [Test]
        public void EmptyBatteryDarknessEscalatesAfterGraceAndPauseOrLightPreventsDeath()
        {
            Enter(); sanity.sanity = 100;
            flashlight.Restore(0, false);
            sanity.AdvanceSanity(20, false);
            Assert.That(sanity.sanity, Is.EqualTo(100));
            Assert.That(sanity.DarknessDanger01, Is.Zero);
            sanity.AdvanceSanity(30, false);
            Assert.That(sanity.sanity, Is.EqualTo(67).Within(.001f));
            Assert.That(sanity.DarknessDanger01, Is.EqualTo(.33f).Within(.001f));
            sanity.AdvanceSanity(120, true);
            Assert.That(sanity.sanity, Is.EqualTo(67).Within(.001f));
            var anchor = Child("Safe illuminated anchor").AddComponent<SchoolMemoryAnchor>();
            anchor.transform.position = player.transform.position; anchor.recoveryPerSecond = .5f;
            sanity.anchors = new[] { anchor };
            sanity.AdvanceSanity(10, false);
            Assert.That(sanity.sanity, Is.EqualTo(72).Within(.001f));
            Assert.That(sanity.DarknessDanger01, Is.LessThan(1f / 3));
            flashlight.Restore(90, true);
            Assert.That(sanity.DarknessDanger01, Is.Zero);
            Assert.That(sanity.BlackingOut, Is.False);
        }

        [UnityTest]
        public IEnumerator DarknessRetryOnlyRepairsItsOwnSanityAndKeepsTheSavedWorldAndEmptyBattery()
        {
            game.save = save; Enter();
            inventory.Add(new InventoryItem { id = "school_documents", title = "Documento", note = true,
                inspectionId = "school_documents", inspectionSeen = true, inspectionCount = 2 });
            door.SetOpen(true, true); flashlight.Restore(0, false);
            sanity.AdvanceSanity(65, false); sanity.sanity = 10;
            Assert.That(save.Save(), Is.True);
            Assert.That(save.TryReadCurrent(out var original), Is.True);
            string unchanged = JsonUtility.ToJson(original);
            string liveState = sanity.CaptureState();
            var retry = sanity.PrepareDarknessRetrySnapshot(original);
            Assert.That(retry, Is.Not.Null);
            Assert.That(JsonUtility.ToJson(original), Is.EqualTo(unchanged), "Preparing retry must not mutate the saved checkpoint.");
            Assert.That(sanity.CaptureState(), Is.EqualTo(liveState), "Preparing retry must not change the live world.");
            Assert.That(retry.battery, Is.Zero); Assert.That(retry.flashlightOn, Is.False);
            Assert.That(retry.position, Is.EqualTo(original.position));
            Assert.That(JsonUtility.ToJson(retry.narrative), Is.EqualTo(JsonUtility.ToJson(original.narrative)));
            Assert.That(retry.inventory.Single().inspectionCount, Is.EqualTo(2));
            CollectionAssert.AreEqual(original.objects.Where(x => x.id != sanity.persistentId).Select(x => x.id + x.json).ToArray(),
                retry.objects.Where(x => x.id != sanity.persistentId).Select(x => x.id + x.json).ToArray());
            Assert.That(save.SaveSnapshot(retry), Is.True);
            sanity.RestoreState(retry.objects.Single(x => x.id == sanity.persistentId).json);
            Assert.That(sanity.sanity, Is.EqualTo(60)); Assert.That(sanity.DarknessSeconds, Is.Zero);
            Assert.That(sanity.HasChapterStart, Is.True);
            sanity.AdvanceSanity(20, false);
            Assert.That(sanity.sanity, Is.EqualTo(60), "The full grace period must be available after retry.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ARevelationStillCausesTheLegacyBlackoutEvenWhenTheLampIsEmpty()
        {
            Enter(); flashlight.Restore(0, false);
            sanity.AdvanceSanity(30, false);
            sanity.sanity = 3;
            sanity.ReactToSignal("school.photograph");
            Assert.That(sanity.sanity, Is.Zero);
            sanity.AdvanceSanity(.1f, false);
            sanity.AdvanceSanity(2, false);
            sanity.AdvanceSanity(1, false);
            Assert.That(game.State, Is.EqualTo(GameState.Playing));
            Assert.That(sanity.sanity, Is.EqualTo(60));
            Assert.That(game.IsBlackingOut, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExhaustedDarknessFadesIntoDeathWithoutOverwritingTheLastCheckpoint()
        {
            game.save = save; Enter(); sanity.sanity = 100;
            flashlight.Restore(90, true);
            Assert.That(save.Save(), Is.True);
            string healthy = File.ReadAllText(save.SavePath);
            flashlight.Restore(0, false);
            sanity.AdvanceSanity(112, false);
            Assert.That(sanity.sanity, Is.Zero);
            Assert.That(sanity.BlackingOut, Is.True);
            Assert.That(game.State, Is.EqualTo(GameState.BlackingOut));
            Assert.That(save.Save(), Is.False);
            sanity.AdvanceSanity(2, false);
            Assert.That(game.State, Is.EqualTo(GameState.GameOver));
            Assert.That(game.IsBlackingOut, Is.False, "Retry must remain available after the lethal fade.");
            Assert.That(File.ReadAllText(save.SavePath), Is.EqualTo(healthy));
            Assert.That(save.TryReadCurrent(out var checkpoint), Is.True);
            Assert.That(checkpoint.battery, Is.EqualTo(90));
            Assert.That(checkpoint.narrative.chapterId, Is.EqualTo("school"));
            yield return null;
        }

        [Test]
        public void AcceptedNarrativeShocksAreUniqueAndRestorationIsSilent()
        {
            Enter(); director.Signal("school.entry-locked"); director.Signal("school.documents-found");
            Assert.That(director.Signal("school.photograph"), Is.True);
            Assert.That(director.Signal("school.blackboard-seen"), Is.True);
            director.Signal("school.blackboard-vanished"); Assert.That(director.Signal("school.ghost-approached"), Is.True);
            inventory.Add(new InventoryItem { id = "school_drawing", title = "Desenho" });
            Assert.That(director.Signal("school.drawing"), Is.True);
            Assert.That(sanity.sanity, Is.EqualTo(70));
            string saved = sanity.CaptureState(); var progress = director.CaptureProgress();
            sanity.sanity = 10; sanity.RestoreState(saved); director.RestoreProgress(progress); director.InitializeAfterSession(true);
            sanity.ReactToSignal("school.photograph"); sanity.ReactToSignal("school.ghost-approached");
            Assert.That(sanity.sanity, Is.EqualTo(70)); Assert.That(sanity.Sanity01, Is.EqualTo(.7f).Within(.0001f));
        }

        [Test]
        public void ChapterStartSnapshotKeepsIncomingGearAndEarlierProgressWithFreshScenery()
        {
            sanity.RestoreState("{\"version\":1,\"sanity\":100,\"darknessSeconds\":0}");
            inventory.Add(new InventoryItem { id = "office_equipment", title = "Equipamentos" });
            flashlight.Restore(137, true);
            var start = new NarrativeProgress { chapterId = "school" };
            start.completedChapters.AddRange(new[] { "prologue", "road" });
            start.milestones.AddRange(new[] { "prologue.equipment", "road.school-gate" });
            start.sequences.Add("road.arrival-call");
            director.RestoreProgress(start); director.InitializeAfterSession(true);
            Enter(); director.Signal("school.entry-locked"); director.Signal("school.documents-found");
            inventory.Add(new InventoryItem { id = "school_documents", title = "Documentos", note = true });
            player.Warp(new Vector3(90, 1, 90), 90); flashlight.Restore(3, false); door.SetOpen(true, true);
            sanity.sanity = 12;
            Assert.That(sanity.PrepareChapterRestart(out var snapshot), Is.True);
            Assert.That(snapshot.position, Is.EqualTo(new Vector3(3, .1f, 2)));
            Assert.That(snapshot.battery, Is.EqualTo(137)); Assert.That(snapshot.flashlightOn, Is.True);
            Assert.That(snapshot.inventory.Any(item => item.id == "office_equipment"), Is.True);
            Assert.That(snapshot.inventory.Any(item => item.id == "school_documents"), Is.False);
            CollectionAssert.AreEqual(new[] { "prologue", "road" }, snapshot.narrative.completedChapters);
            Assert.That(snapshot.narrative.HasSignal("road.school-gate"), Is.True);
            Assert.That(snapshot.narrative.HasSequence("road.arrival-call"), Is.True);
            Assert.That(snapshot.narrative.HasSignal("school.entered"), Is.False);
            StringAssert.Contains("\"open\":false", snapshot.objects.Single(item => item.id == door.persistentId).json);
            var controllerState = snapshot.objects.Single(item => item.id == sanity.persistentId).json;
            sanity.RestoreState(controllerState); Assert.That(sanity.sanity, Is.EqualTo(100));
            Assert.That(sanity.HasChapterStart, Is.True);
            Assert.That(save.SaveSnapshot(snapshot), Is.True);
            Assert.That(SaveSystem.TryRead(save.SavePath, out var roundtrip), Is.True);
            Assert.That(roundtrip.version, Is.EqualTo(2)); Assert.That(roundtrip.battery, Is.EqualTo(137));
            Assert.That(roundtrip.objects.Count(item => item.id == sanity.persistentId), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AutomaticBlackoutPreservesLatestCheckpointAndBlocksPendingSaves()
        {
            var hud = Child("HUD").AddComponent<GameHUD>(); hud.enabled = false; hud.game = game;
            hud.pausePanel = Child("Pause"); hud.optionsPanel = Child("Options");
            hud.notePanel = Child("Notes"); hud.inventoryPanel = Child("Inventory"); hud.gameOverPanel = Child("Game over");
            hud.deathCurtain = Child("Curtain").AddComponent<CanvasGroup>();
            hud.subtitleGroup = Child("Subtitle group").AddComponent<CanvasGroup>();
            hud.subtitle = Child("Subtitle", hud.subtitleGroup.transform).AddComponent<TextMeshProUGUI>();
            hud.notification = Child("Notification").AddComponent<TextMeshProUGUI>();
            hud.objective = Child("Objective").AddComponent<TextMeshProUGUI>();
            hud.restartButton = Child("Retry").AddComponent<Button>(); hud.resumeButton = Child("Resume").AddComponent<Button>();
            game.hud = hud;
            game.save = save;
            Enter();
            inventory.Add(new InventoryItem { id = "school_documents", title = "Documento", note = true,
                inspectionId = "school_documents", inspectionSeen = true, inspectionCount = 2 });
            door.SetOpen(true, true); sanity.sanity = 72;
            Assert.That(save.Save(), Is.True);
            string healthySave = File.ReadAllText(save.SavePath);
            var progress = director.CaptureProgress(); Vector3 checkpointPosition = player.transform.position;
            player.Warp(new Vector3(90, 1, 90), 90); door.SetOpen(false, true);
            director.ScheduleSave();
            sanity.sanity = 0;
            sanity.AdvanceSanity(.1f, false); Assert.That(sanity.BlackingOut, Is.True); Assert.That(game.State, Is.EqualTo(GameState.BlackingOut));
            Assert.That(save.Save(), Is.False);
            Assert.That(save.SaveSnapshot(new SaveData { scene = scene.name }), Is.False);
            Assert.That(director.Signal("school.entry-locked"), Is.False);
            Assert.That(director.PlaySequence("school.photo-inscription"), Is.False);
            yield return null; yield return null;
            Assert.That(File.ReadAllText(save.SavePath), Is.EqualTo(healthySave), "A pending save must not overwrite the healthy checkpoint.");
            Assert.That(player.view.playerCamera.GetComponent<AudioLowPassFilter>().enabled, Is.True);
            // Incidental frames have advanced the fade, so compare the paused clock against its current value.
            sanity.AdvanceSanity(.7f, false); Assert.That(sanity.Blackout01, Is.GreaterThan(0));
            float pausedFade = sanity.Blackout01;
            game.Pause(); sanity.AdvanceSanity(10, false); Assert.That(sanity.Blackout01, Is.EqualTo(pausedFade));
            game.Resume(); sanity.AdvanceSanity(2, false);
            Assert.That(sanity.BlackingOut, Is.True); Assert.That(game.State, Is.EqualTo(GameState.BlackingOut));
            Assert.That(player.transform.position, Is.EqualTo(checkpointPosition));
            StringAssert.Contains("\"open\":true", door.CaptureState());
            Assert.That(sanity.sanity, Is.EqualTo(60));
            Assert.That(save.Save(), Is.False, "Wake fade must remain locked.");
            Assert.That(hud.subtitle.text, Is.EqualTo("Voz infantil: Você nunca deveria ter voltado."));
            sanity.AdvanceSanity(1, false);
            Assert.That(game.State, Is.EqualTo(GameState.Playing)); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(hud.gameOverPanel.activeSelf, Is.False);
            Assert.That(JsonUtility.ToJson(director.CaptureProgress()), Is.EqualTo(JsonUtility.ToJson(progress)));
            Assert.That(inventory.items.Single(x => x.id == "school_documents").inspectionCount, Is.EqualTo(2));
            Assert.That(inventory.items.Single(x => x.id == "school_documents").inspectionSeen, Is.True);
            Assert.That(inventory.items.Single(x => x.id == "school_documents").note, Is.True);
            Assert.That(hud.deathCurtain.alpha, Is.Zero);
            yield return null;
            Assert.That(game.CanControl, Is.True);
            Assert.That(player.view.playerCamera.GetComponent<AudioLowPassFilter>(), Is.Null);
            Assert.That(SaveSystem.TryRead(save.SavePath, out var recovered), Is.True);
            StringAssert.Contains("\"sanity\":60", recovered.objects.Single(x => x.id == sanity.persistentId).json);
            sanity.sanity = 0; sanity.AdvanceSanity(.1f, false);
            sanity.AdvanceSanity(2, false); sanity.AdvanceSanity(1, false);
            Assert.That(game.State, Is.EqualTo(GameState.Playing)); Assert.That(sanity.sanity, Is.EqualTo(60));
        }

        [Test]
        public void LegacyAndBackupCheckpointsRejectAnotherSceneAndRetainInspectionMetadata()
        {
            var old = new SaveData { version = 1, scene = scene.name,
                inventory = new System.Collections.Generic.List<InventoryItem> { new InventoryItem { id = "old_note", note = true } } };
            Assert.That(save.SaveSnapshot(old), Is.True);
            Assert.That(save.SaveSnapshot(new SaveData { scene = scene.name, battery = 137 }), Is.True);
            File.WriteAllText(save.SavePath, JsonUtility.ToJson(new SaveData { scene = "AnotherScene" }));
            Assert.That(save.TryReadCurrent(out var backup), Is.True);
            Assert.That(backup.version, Is.EqualTo(1)); Assert.That(backup.inventory.Single().id, Is.EqualTo("old_note"));
            Assert.That(backup.inventory.Single().inspectionSeen, Is.False);
            File.WriteAllText(save.SavePath, "corrupt");
            Assert.That(save.TryReadCurrent(out backup), Is.True); Assert.That(backup.version, Is.EqualTo(1));
            File.WriteAllText(save.SavePath + ".bak", "corrupt");
            Assert.That(save.TryReadCurrent(out var cached), Is.True); Assert.That(cached.battery, Is.EqualTo(137));
        }

        [UnityTest]
        public IEnumerator LegacyCheckpointMissingNewObjectRecordsRestoresDefaultsAndNarrativeShocks()
        {
            game.save = save; Enter();
            var legacy = new SaveData { version = 1, scene = scene.name, position = player.transform.position,
                narrative = director.CaptureProgress(), battery = 100, stamina = player.Stamina };
            Assert.That(save.SaveSnapshot(legacy), Is.True);
            door.SetOpen(true, true);
            sanity.ReactToSignal("school.photograph");
            sanity.sanity = 0; sanity.AdvanceSanity(.1f, false);
            sanity.AdvanceSanity(2, false); sanity.AdvanceSanity(1, false);
            Assert.That(game.State, Is.EqualTo(GameState.Playing));
            Assert.That(door.IsOpen, Is.False, "A legacy checkpoint cannot retain a later object's changed state.");
            Assert.That(sanity.sanity, Is.EqualTo(60));
            sanity.ReactToSignal("school.photograph");
            Assert.That(sanity.sanity, Is.EqualTo(56), "An unsaved shock must not survive a legacy checkpoint rewind.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SchoolOptInAndLampPresentationRestoreTheBatteryBaseline()
        {
            Enter(); sanity.sanity = 10; sanity.darknessLossPerSecond = .22f;
            sanity.enabledForSchool = false; sanity.AdvanceSanity(90, false); sanity.Disturb(.8f);
            Assert.That(sanity.sanity, Is.EqualTo(10));
            sanity.enabledForSchool = true; sanity.schoolSceneName = "AnotherScene";
            sanity.AdvanceSanity(90, false); Assert.That(sanity.sanity, Is.EqualTo(10));
            sanity.schoolSceneName = scene.name; sanity.darknessLossPerSecond = 0;
            var probe=root.AddComponent<SchoolLampProbe>();probe.lamp=flashlight.lamp;
            flashlight.enabled = true; flashlight.Restore(137, true);
            yield return null;
            yield return null;
            Assert.That(probe.observed, Is.LessThan(10));
            sanity.enabled = false; Assert.That(flashlight.lamp.intensity, Is.EqualTo(10).Within(.001f));
        }

        [Test]
        public void SnapshotWriterRejectsWrongSceneAndNonfiniteDataWithoutWriting()
        {
            var snapshot = new SaveData { scene = "AnotherScene" };
            Assert.That(save.SaveSnapshot(snapshot), Is.False);
            snapshot.scene = scene.name; snapshot.battery = float.NaN;
            Assert.That(save.SaveSnapshot(snapshot), Is.False); Assert.That(File.Exists(save.SavePath), Is.False);
        }
    }
}
