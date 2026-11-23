// Verifica SetUp, TearDown, ActualLastChapterFourDoorHandsOffInPlaceWithItsWorldAndGear, ActualMemoriesRecordingsNotebookAndImmaterialGuideFinishOnlyChapterFive, InterruptedPhotographRevelationResumesFromItsUniqueNoteWithoutReplayingEarlierShocks, InterruptedRecordingCheckpointAndAutomaticBlackoutPreserveLatestDiscoveries.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    /// <summary>Production SchoolCEEP interactions and chapter-five persistence. Supported warps
    /// stage the investigation; the local corridor check alone claims continuous walking.
    /// Test saves, preferences, input configuration and temporary books are restored.</summary>
    [Category("SchoolChapterFiveScene"), Explicit("Historical seven-chapter scene. Replaced by SchoolSimplifiedSceneTests for the installed six-chapter campaign.")]
    public sealed class SchoolChapterFiveSceneTests
    {
        GameSession game;
        SchoolMemoriesFlow flow;
        SchoolLeftBehindFlow previousFlow;
        SchoolOpeningFlow opening;
        SchoolSanityController sanity;
        Keyboard keyboard;
        InputSettings.BackgroundBehavior originalBackground;
        InputSettings.EditorInputBehaviorInPlayMode originalEditorInput;
        bool originalRunInBackground;
        string testSaveName, savedScene;
        bool hadSavedScene, hadResume, hadCarry;
        int resume, carry;
        byte[] save, backup, temporary;
        readonly List<StoryBook> clonedBooks = new List<StoryBook>();
        readonly HashSet<GameSession> clonedSessions = new HashSet<GameSession>();
        string RealSavePath => Path.Combine(Application.persistentDataPath, "save.json");
        int WorldMask => ~LayerMask.GetMask("Player");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            save = ReadFile(RealSavePath); backup = ReadFile(RealSavePath + ".bak"); temporary = ReadFile(RealSavePath + ".tmp");
            hadSavedScene = PlayerPrefs.HasKey("Game.SaveScene"); hadResume = PlayerPrefs.HasKey("Game.ResumeRequested");
            hadCarry = PlayerPrefs.HasKey("Game.StoryCarryRequested");
            savedScene = PlayerPrefs.GetString("Game.SaveScene", ""); resume = PlayerPrefs.GetInt("Game.ResumeRequested", 0);
            carry = PlayerPrefs.GetInt("Game.StoryCarryRequested", 0);
            PlayerPrefs.SetInt("Game.ResumeRequested", 0); PlayerPrefs.SetInt("Game.StoryCarryRequested", 0);
            testSaveName = "school-chapter-five-fixture-" + Guid.NewGuid().ToString("N") + ".json";
            originalBackground = InputSystem.settings.backgroundBehavior;
            originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            originalRunInBackground = Application.runInBackground;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true; Time.timeScale = 1; AudioListener.pause = false;
            SceneManager.sceneLoaded += RedirectInitialSave;
            yield return SceneManager.LoadSceneAsync("SchoolCEEP");
            yield return Until(() => GameSession.Instance && GameSession.Instance.narrative && GameSession.Instance.narrative.IsInitialized, 15);
            BindScene();
            Assert.That(flow, Is.Not.Null, "Chapter 5 must be installed in the saved school.");
            Assert.That(previousFlow, Is.Not.Null, "The Chapter 4 handoff must remain available.");
            Assert.That(flow.director, Is.SameAs(game.narrative));
            Assert.That(game.save.fileName, Is.EqualTo(testSaveName));
            keyboard = InputSystem.AddDevice<Keyboard>(); game.input.Actions.devices = new InputDevice[] { keyboard };
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            ConfigureRuntimeTimings();
            game.interaction.inventory.Restore(IncomingItems()); game.save.flashlight.Restore(137, true);
            yield return null; yield return null;
        }

        void BindScene()
        {
            game = GameSession.Instance;
            flow = UnityEngine.Object.FindAnyObjectByType<SchoolMemoriesFlow>();
            previousFlow = UnityEngine.Object.FindAnyObjectByType<SchoolLeftBehindFlow>();
            opening = UnityEngine.Object.FindAnyObjectByType<SchoolOpeningFlow>();
            sanity = UnityEngine.Object.FindAnyObjectByType<SchoolSanityController>();
        }
        void ConfigureRuntimeTimings()
        {
            game.player.view.bobEnabled = false; game.player.view.swayEnabled = false;
            game.narrative.transitionView.fadeSeconds = .12f; game.narrative.transitionView.cardSeconds = .20f;
            // Captions are shortened on an owned runtime clone, never on the authored asset.
            if (clonedSessions.Add(game))
            {
                var clone = UnityEngine.Object.Instantiate(game.narrative.book);
                clone.name = game.narrative.book.name + " — ChapterFive scene fixture";
                clone.hideFlags = HideFlags.DontSave; clonedBooks.Add(clone); game.narrative.book = clone;
                // This fixture owns chapter five only; authorized final content stays on the source asset.
                clone.Chapter("terrace").sceneName = "";
            }
            foreach (var sequence in game.narrative.book.sequences)
            {
                foreach (var line in sequence.lines ?? Array.Empty<StoryLine>()) if (line != null) line.seconds = .12f;
                // Leave the actual 2.4-second event alive long enough to prove pause and expiration.
                if ((sequence.id == "memories.past-echo" || sequence.id == "memories.revelation")
                    && sequence.lines != null && sequence.lines.Length > 0 && sequence.lines[0] != null)
                    sequence.lines[0].seconds = 3.2f;
            }
            Assert.That(game.narrative.book.Chapter("memories").sceneName, Is.EqualTo("SchoolCEEP"));
            Assert.That(game.narrative.book.Chapter("terrace").sceneName, Is.Empty);
        }
        static List<InventoryItem> IncomingItems() => new List<InventoryItem>
        {
            new InventoryItem { id = "office_equipment", title = "Equipamento do escritório" },
            new InventoryItem { id = "office_photo", title = "Foto de casa", text = "Uma lembrança anterior.", note = true },
            new InventoryItem { id = "school_documents", title = "Registro do capítulo 3", note = true },
            new InventoryItem { id = "school_photograph", title = "Turma do capítulo 3", note = true },
            new InventoryItem { id = "school_drawing", title = "Desenho do capítulo 3", note = true },
            new InventoryItem { id = "old_disappearance_report", title = "Relatório do capítulo 4", note = true },
            new InventoryItem { id = "old_student_photo", title = "Foto do capítulo 4", note = true },
            new InventoryItem { id = "old_key", title = "Chave antiga" }
        };
        void RedirectInitialSave(Scene loaded, LoadSceneMode mode)
        {
            if (loaded.name != "SchoolCEEP") return;
            foreach (var system in loaded.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SaveSystem>(true)))
                system.fileName = testSaveName;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= RedirectInitialSave; Time.timeScale = 1; AudioListener.pause = false;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = originalBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
            Application.runInBackground = originalRunInBackground;
            var loaded = SceneManager.GetSceneByName("SchoolCEEP");
            var blank = SceneManager.GetSceneByName("School-chapter-five-cleanup");
            if (!blank.IsValid() || !blank.isLoaded) blank = SceneManager.CreateScene("School-chapter-five-cleanup");
            SceneManager.SetActiveScene(blank); if (loaded.isLoaded) yield return SceneManager.UnloadSceneAsync(loaded);
            foreach (var clone in clonedBooks) if (clone) UnityEngine.Object.Destroy(clone);
            clonedBooks.Clear(); clonedSessions.Clear();
            RestoreFile(RealSavePath, save); RestoreFile(RealSavePath + ".bak", backup); RestoreFile(RealSavePath + ".tmp", temporary);
            if (!string.IsNullOrEmpty(testSaveName)) foreach (string suffix in new[] { "", ".bak", ".tmp" })
            { string path = Path.Combine(Application.persistentDataPath, testSaveName + suffix); if (File.Exists(path)) File.Delete(path); }
            if (hadSavedScene) PlayerPrefs.SetString("Game.SaveScene", savedScene); else PlayerPrefs.DeleteKey("Game.SaveScene");
            if (hadResume) PlayerPrefs.SetInt("Game.ResumeRequested", resume); else PlayerPrefs.DeleteKey("Game.ResumeRequested");
            if (hadCarry) PlayerPrefs.SetInt("Game.StoryCarryRequested", carry); else PlayerPrefs.DeleteKey("Game.StoryCarryRequested");
            PlayerPrefs.Save();
        }
        static byte[] ReadFile(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
        static void RestoreFile(string path, byte[] original)
        { if (original != null) File.WriteAllBytes(path, original); else if (File.Exists(path)) File.Delete(path); }
        IEnumerator Until(Func<bool> condition, float seconds = 12)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            var session = game ? game : GameSession.Instance;
            string state = session && session.narrative ? session.narrative.NextSignal + ", state=" + session.State
                + ", control=" + session.CanControl + ", sequence=" + session.narrative.ActiveSequenceId : "initializing";
            Assert.That(condition(), Is.True, "Chapter 5 interaction deadline: " + state);
        }

        NarrativeProgress PreviousChapters()
        {
            var progress = new NarrativeProgress { chapterId = "memories" };
            foreach (var chapter in game.narrative.book.chapters.Take(4))
            {
                foreach (var milestone in chapter.milestones) progress.milestones.Add(milestone.id);
                progress.completedChapters.Add(chapter.id);
            }
            progress.sequences.AddRange(new[] { "road.arrival-call", "school.photo-inscription", "school.blackboard-inscription",
                "school.drawing-inscription", "left-behind.photo-inscription", "left-behind.wall-inscription" });
            return progress;
        }
        void OpenInvestigationDoors()
        {
            foreach (var door in UnityEngine.Object.FindObjectsByType<InteractiveDoor>())
                if (door != opening.entryDoor && door != opening.entryDoor.pairedLeaf)
                { door.SetLocked(false); door.SetOpen(true, true); }
            opening.entryDoor.SetLocked(true); opening.entryDoor.SetOpen(false, true);
            opening.entryDoor.pairedLeaf.SetLocked(true); opening.entryDoor.pairedLeaf.SetOpen(false, true);
            Physics.SyncTransforms();
        }
        void StageChapterFive()
        {
            // Only completed prior chapters are staged. All current discoveries use production interactions.
            OpenInvestigationDoors();
            WarpAim(SupportedFeet(new Vector3(-65.62372f, .6f, 43.65583f)), new Vector3(-65.62372f, 2.2f, 30));
            game.interaction.inventory.Restore(IncomingItems()); game.save.flashlight.Restore(137, true); sanity.sanity = 87;
            game.narrative.RestoreProgress(PreviousChapters()); game.narrative.InitializeAfterSession(true);
            Assert.That(game.narrative.NextSignal, Is.EqualTo("memories.past-echoes"));
            Assert.That(flow.IsChapterActive, Is.True);
            Assert.That(sanity.anchors.Length, Is.EqualTo(6));
            Assert.That(previousFlow.memoryAnchor.activeSelf, Is.False);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<SchoolTerraceFlow>().memoryAnchor.activeSelf, Is.False);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<SchoolLastExitFlow>().memoryAnchor.activeSelf, Is.False);
            Assert.That(sanity.PrepareChapterRestart(out var start), Is.True);
            Assert.That(start.narrative.chapterId, Is.EqualTo("memories"));
            Assert.That(start.inventory.Any(item => item.id == "old_key"), Is.True);
        }

        Vector3 SupportedFeet(Vector3 point)
        {
            Assert.That(Physics.Raycast(point + Vector3.up * 1.1f, Vector3.down, out var hit, 4, WorldMask, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(hit.normal.y, Is.GreaterThan(.75f));
            Vector3 feet = hit.point + Vector3.up * .045f;
            Assert.That(CapsuleClear(feet), Is.True, "Staged feet must fit the actual player volume: " + feet);
            return feet;
        }
        bool CapsuleClear(Vector3 feet)
        {
            float radius = game.player.Controller.radius, height = game.player.standingHeight;
            return !Physics.CheckCapsule(feet + Vector3.up * (radius + .02f), feet + Vector3.up * (height - radius - .02f),
                radius, WorldMask, QueryTriggerInteraction.Ignore);
        }
        bool ClearRay(Vector3 eye, Vector3 target, Component clue = null)
        {
            Vector3 direction = target - eye;
            if (!Physics.Raycast(eye, direction.normalized, out var hit, direction.magnitude, WorldMask, QueryTriggerInteraction.Ignore)) return clue == null;
            if (clue is SchoolMemoriesClue memory) return hit.collider.GetComponentInParent<SchoolMemoriesClue>() == memory;
            return hit.distance >= direction.magnitude - .15f;
        }
        Vector3 SafeFeet(Vector3 target, float[] radii, Component clue = null)
        {
            var candidates = new List<Vector3>();
            foreach (float distance in radii) for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI / 16;
                Vector3 candidate = target + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
                if (!Physics.Raycast(candidate + Vector3.up * .75f, Vector3.down, out var floor, 4, WorldMask, QueryTriggerInteraction.Ignore)
                    || floor.normal.y < .75f) continue;
                Vector3 feet = floor.point + Vector3.up * .045f; if (!CapsuleClear(feet)) continue;
                Vector3 eye = feet + Vector3.up * game.player.EyeHeight;
                if (clue && Vector3.Distance(eye, target) > 2.5f) continue;
                if (ClearRay(eye, target, clue)) candidates.Add(feet);
            }
            Assert.That(candidates.Count, Is.GreaterThan(0), "No supported player position sees " + (clue ? clue.name : target.ToString()));
            float ground = candidates.Min(point => point.y);
            return candidates.Where(point => point.y < ground + .12f).OrderBy(point => Vector3.Distance(point, target)).First();
        }
        void WarpAim(Vector3 feet, Vector3 target)
        {
            Vector3 delta = target - (feet + Vector3.up * game.player.EyeHeight);
            game.player.Warp(feet, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, -Mathf.Asin(delta.normalized.y) * Mathf.Rad2Deg);
            Physics.SyncTransforms();
        }
        IEnumerator ReadActualClue(SchoolMemoriesClue clue)
        {
            yield return Until(() => game.CanControl && !game.narrative.SequencePlaying);
            Assert.That(clue.CanInteract(game.interaction), Is.True, "Narrative must permit " + clue.name);
            WarpAim(SafeFeet(clue.transform.position, new[] { 1.2f, 1.5f, 1.8f }, clue), clue.transform.position);
            yield return null; yield return null;
            Assert.That(game.interaction.Target, Is.SameAs(clue), "The actual player raycast must hit " + clue.name);
            game.interaction.Target.Interact(game.interaction); yield return null;
            var inspection=UnityEngine.Object.FindAnyObjectByType<InspectionManager>();
            Assert.That(inspection && inspection.IsOpen,Is.True,"The actual clue opens its 3D inspection.");
            inspection.Close();
            yield return Until(()=>!inspection.IsOpen && !inspection.IsCompleting);
        }
        IEnumerator EnterArchive()
        {
            Vector3 point = flow.archiveVolume.transform.TransformPoint(flow.archiveVolume.center);
            WarpAim(SupportedFeet(point), flow.firstRecording.transform.position);
            yield return Until(() => game.narrative.HasSignal("memories.past-echoes") && game.CanControl && !game.narrative.SequencePlaying);
            Assert.That(game.narrative.NextSignal, Is.EqualTo("memories.recording-one-found"));
        }
        IEnumerator ReadRecordingsAndPhoto()
        {
            yield return ReadActualClue(flow.firstRecording);
            yield return Until(() => game.narrative.HasSequence("memories.recording-one") && game.CanControl && !game.narrative.SequencePlaying);
            yield return ReadActualClue(flow.secondRecording);
            yield return Until(() => game.narrative.HasSequence("memories.recording-two") && game.CanControl && !game.narrative.SequencePlaying);
            yield return ReadActualClue(flow.photograph);
            yield return Until(() => game.narrative.HasSequence("memories.revelation") && game.CanControl && !game.narrative.SequencePlaying);
            Assert.That(game.narrative.NextSignal, Is.EqualTo("memories.ghost-appeared"));
        }
        IEnumerator WalkShortCorridor()
        {
            // The straight hospício corridor, with chapter-four presences inactive.
            // This local span is separate from supported clue staging.
            Vector3 start = SupportedFeet(new Vector3(-65.62372f, .6f, 40));
            WarpAim(start, start + Vector3.forward * 20 + Vector3.up * game.player.EyeHeight);
            yield return Until(() => game.CanControl && !game.narrative.SequencePlaying);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); InputSystem.Update();
            float deadline = Time.realtimeSinceStartup + 15;
            try
            {
                while (game.player.transform.position.z < start.z + 2 && Time.realtimeSinceStartup < deadline)
                {
                    Assert.That(Mathf.Abs(game.player.transform.position.x - start.x), Is.LessThan(.25f));
                    yield return null;
                }
            }
            finally { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); }
            Vector3 end = game.player.transform.position;
            Assert.That(end.z - start.z, Is.GreaterThanOrEqualTo(2), "W input must travel this unobstructed corridor span.");
            Assert.That(Mathf.Abs(end.x - start.x), Is.LessThan(.25f));
            Assert.That(CapsuleClear(end + Vector3.up * .045f), Is.True);
            Assert.That(Physics.Raycast(end + Vector3.up * .3f, Vector3.down, out var floor, .7f, WorldMask, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(floor.normal.y, Is.GreaterThan(.75f));
        }

        [UnityTest]
        public IEnumerator ActualLastChapterFourDoorHandsOffInPlaceWithItsWorldAndGear()
        {
            OpenInvestigationDoors();
            var progress = PreviousChapters(); progress.chapterId = "left-behind";
            progress.completedChapters.Remove("left-behind"); progress.milestones.Remove("left-behind.locked-area-opened");
            // Replay the production wall-caption completion before its final real door interaction.
            progress.milestones.Remove("left-behind.wall-message"); progress.sequences.Remove("left-behind.wall-inscription");
            game.interaction.inventory.Restore(IncomingItems()); game.save.flashlight.Restore(137, true); sanity.sanity = 87;
            game.narrative.RestoreProgress(progress); game.narrative.InitializeAfterSession(true);
            WarpAim(SafeFeet(previousFlow.wallAnchor.position, new[] { 1.5f, 2f, 2.5f }), previousFlow.wallAnchor.position);
            yield return Until(() => game.narrative.HasSequence("left-behind.wall-inscription") && game.CanControl && !game.narrative.SequencePlaying);
            Assert.That(game.narrative.NextSignal, Is.EqualTo("left-behind.locked-area-opened"));
            var door = previousFlow.destinationDoor;
            WarpAim(SupportedFeet(new Vector3(-65.62372f, .6f, 43.65583f)), door.leafCollider.bounds.center);
            yield return null; yield return null;
            Assert.That(game.interaction.Target, Is.SameAs(door));
            var session = game; Vector3 position = game.player.transform.position; float battery = game.save.flashlight.BatterySeconds;
            game.interaction.Target.Interact(game.interaction);
            yield return Until(() => game.narrative.Progress.chapterId == "memories" && game.CanControl, 15);
            Assert.That(GameSession.Instance, Is.SameAs(session));
            Assert.That(Vector3.Distance(game.player.transform.position, position), Is.LessThan(.10f));
            Assert.That(game.save.flashlight.BatterySeconds, Is.EqualTo(battery).Within(4));
            Assert.That(door.IsOpen, Is.True); Assert.That(door.locked, Is.False);
            Assert.That(flow.IsChapterActive, Is.True); Assert.That(previousFlow.IsChapterActive, Is.False);
            Assert.That(game.narrative.NextSignal, Is.EqualTo("memories.past-echoes"));
            foreach (string id in new[] { "school", "left-behind" })
                Assert.That(game.narrative.Progress.completedChapters.Contains(id), Is.True);
            foreach (var item in IncomingItems()) Assert.That(game.interaction.inventory.Contains(item.id), Is.True, item.id);
            Assert.That(sanity.PrepareChapterRestart(out var start), Is.True);
            Assert.That(start.narrative.chapterId, Is.EqualTo("memories"));
            Assert.That(Vector3.Distance(start.position, position), Is.LessThan(.10f));
            Assert.That(start.inventory.Any(item => item.id == "old_key"), Is.True);
        }

        [UnityTest]
        public IEnumerator ActualMemoriesRecordingsNotebookAndImmaterialGuideFinishOnlyChapterFive()
        {
            StageChapterFive();
            Assert.That(flow.memoryDuration, Is.EqualTo(2.4f).Within(.001f));
            Assert.That(flow.memoryVisuals.GetComponentsInChildren<Collider>(true).Length, Is.Zero);
            Assert.That(flow.memoryVisuals.GetComponentsInChildren<EnemyBrain>(true).Length, Is.Zero);
            Assert.That(flow.ghostGuide.GetComponentsInChildren<Collider>(true).Length, Is.Zero);
            Assert.That(flow.ghostGuide.GetComponentsInChildren<EnemyBrain>(true).Length, Is.Zero);
            Assert.That(sanity.recoveryLights.Contains(flow.memoryLight), Is.False, "A disturbing memory is not a healing lamp.");
            Vector3 point = flow.archiveVolume.transform.TransformPoint(flow.archiveVolume.center);
            WarpAim(SupportedFeet(point), flow.firstRecording.transform.position);
            yield return Until(() => flow.memoryVisuals.activeSelf && flow.memoryLight.enabled);
            Assert.That(game.narrative.ActiveSequenceId, Is.EqualTo("memories.past-echo"));
            Assert.That(opening.schoolLights.All(light => !light || !light.enabled), Is.True, "Only the own temporary memory light may turn on.");
            float pausedSanity = sanity.sanity;
            game.Pause(); flow.AdvanceFlow(90);
            Assert.That(flow.memoryVisuals.activeSelf, Is.False); Assert.That(flow.memoryLight.enabled, Is.False);
            Assert.That(game.narrative.HasSignal("memories.past-echoes"), Is.False);
            yield return null; yield return null;
            Assert.That(sanity.sanity, Is.EqualTo(pausedSanity).Within(.01f));
            game.Resume();
            yield return Until(() => flow.memoryVisuals.activeSelf && flow.memoryLight.enabled);
            yield return Until(() => !flow.memoryVisuals.activeSelf && !flow.memoryLight.enabled);
            Assert.That(game.narrative.SequencePlaying, Is.True, "The 2.4-second flash expires before the temporary 3.2-second caption.");
            yield return Until(() => game.narrative.HasSignal("memories.past-echoes") && game.CanControl && !game.narrative.SequencePlaying);
            yield return WalkShortCorridor();
            yield return ReadRecordingsAndPhoto();
            foreach (string id in new[] { "memory_recording_one", "memory_recording_two", "memories_group_photo" })
                Assert.That(game.interaction.inventory.Contains(id), Is.True, "Discoveries must remain in the notebook: " + id);
            Assert.That(game.interaction.inventory.items.Single(item => item.id == "memories_group_photo").text, Does.Contain("João"));
            game.hud.ShowInventory(); yield return null; yield return null;
            Assert.That(game.State, Is.EqualTo(GameState.Paused));
            Assert.That(game.hud.inventoryPanel.activeSelf, Is.True);
            StringAssert.Contains("OBJETIVO", game.hud.inventoryText.text);
            StringAssert.Contains(game.hud.objective.text, game.hud.inventoryText.text);
            StringAssert.Contains("João", game.hud.inventoryText.text);
            game.Resume(); yield return Until(() => game.CanControl);
            Assert.That(flow.firstRecording.CanInteract(game.interaction), Is.False);
            Assert.That(flow.secondRecording.CanInteract(game.interaction), Is.False);
            Assert.That(flow.photograph.CanInteract(game.interaction), Is.False);
            Assert.That(flow.ghostGuide.activeSelf, Is.True);
            // Stage a supported observation position without changing the authored guide or gaze time.
            WarpAim(SafeFeet(flow.ghostLook.position, new[] { 3f, 4f, 5f }), flow.ghostLook.position);
            yield return Until(() => game.narrative.HasSignal("memories.staircase-indicated") && game.State == GameState.StoryScreen);
            // The saved button can start active behind an invisible canvas. Wait
            // for the final card, not that button alone while Present is fading in.
            yield return Until(() => game.narrative.transitionView.returnButton.gameObject.activeSelf
                && game.narrative.transitionView.body.text.Contains("CAPÍTULO 6")
                && game.narrative.transitionView.heading.text.Contains("CAPÍTULO 5 CONCLUÍDO"));
            Assert.That(game.narrative.Progress.milestones.Count(id => id.StartsWith("memories.")), Is.EqualTo(8));
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo("memories"));
            Assert.That(game.narrative.Progress.pendingChapterId, Is.EqualTo("terrace"));
            Assert.That(game.narrative.book.Chapter("terrace").sceneName, Is.Empty);
            StringAssert.Contains("CAPÍTULO 5 CONCLUÍDO", game.narrative.transitionView.heading.text);
            StringAssert.Contains("CAPÍTULO 6", game.narrative.transitionView.body.text);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("SchoolCEEP"));
            Assert.That(UnityEngine.Object.FindObjectsByType<EnemyBrain>().Length, Is.Zero);
            Assert.That(flow.memoryLight.enabled, Is.False); Assert.That(flow.memoryVisuals.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator InterruptedPhotographRevelationResumesFromItsUniqueNoteWithoutReplayingEarlierShocks()
        {
            StageChapterFive(); yield return EnterArchive();
            yield return ReadActualClue(flow.firstRecording);
            yield return Until(() => game.narrative.HasSequence("memories.recording-one") && game.CanControl && !game.narrative.SequencePlaying);
            yield return ReadActualClue(flow.secondRecording);
            yield return Until(() => game.narrative.HasSequence("memories.recording-two") && game.CanControl && !game.narrative.SequencePlaying);
            yield return ReadActualClue(flow.photograph);
            yield return Until(() => game.narrative.ActiveSequenceId == "memories.revelation");
            Assert.That(game.narrative.HasSignal("memories.childhood-recognized"), Is.False,
                "The manual photograph revelation accepts its milestone only after the final line.");
            Assert.That(game.narrative.HasSequence("memories.revelation"), Is.False);
            Assert.That(game.interaction.inventory.items.Count(item => item.id == "memories_group_photo"), Is.EqualTo(1));
            game.Pause(); float savedSanity = sanity.sanity;
            Assert.That(game.save.Save(), Is.True);
            Assert.That(SaveSystem.TryRead(game.save.SavePath, out var checkpoint), Is.True);
            Assert.That(checkpoint.inventory.Count(item => item.id == "memories_group_photo"), Is.EqualTo(1));
            Assert.That(checkpoint.narrative.HasSignal("memories.childhood-recognized"), Is.False);
            var accepted = new List<string>(); Action<string> handler = id => accepted.Add(id);
            game.narrative.SignalAccepted += handler;
            try
            {
                Assert.That(game.save.LoadCurrent(), Is.True); game.narrative.InitializeAfterSession(true);
                Assert.That(accepted, Is.Empty, "Restoring the unfinished photograph must not issue a revelation or an earlier recording shock.");
                Assert.That(sanity.sanity, Is.EqualTo(savedSanity).Within(.01f));
                Assert.That(game.narrative.NextSignal, Is.EqualTo("memories.childhood-recognized"));
                Assert.That(game.narrative.HasSequence("memories.recording-one"), Is.True);
                Assert.That(game.narrative.HasSequence("memories.recording-two"), Is.True);
                Assert.That(game.interaction.inventory.items.Count(item => item.id == "memories_group_photo"), Is.EqualTo(1));
                Assert.That(flow.photograph.CanInteract(game.interaction), Is.False);
                Assert.That(flow.photograph.visual.activeSelf, Is.False, "The durable note owns the pending manual revelation after load.");
                Assert.That(flow.memoryVisuals.activeSelf, Is.False); Assert.That(flow.memoryLight.enabled, Is.False);
                game.Resume();
                yield return Until(() => game.narrative.ActiveSequenceId == "memories.revelation");
                Assert.That(game.narrative.HasSignal("memories.childhood-recognized"), Is.False);
                yield return Until(() => game.narrative.HasSequence("memories.revelation") && game.CanControl && !game.narrative.SequencePlaying);
                CollectionAssert.AreEqual(new[] { "memories.childhood-recognized" }, accepted,
                    "Resuming the manual sequence must accept only its one pending revelation.");
                Assert.That(game.narrative.Progress.milestones.Count(id => id == "memories.childhood-recognized"), Is.EqualTo(1));
                Assert.That(game.narrative.Progress.sequences.Count(id => id == "memories.revelation"), Is.EqualTo(1));
                Assert.That(game.interaction.inventory.items.Count(item => item.id == "memories_group_photo"), Is.EqualTo(1));
                Assert.That(sanity.sanity, Is.EqualTo(savedSanity - 15).Within(.3f),
                    "Only the pending childhood revelation may reduce restored sanity.");
                Assert.That(game.narrative.NextSignal, Is.EqualTo("memories.ghost-appeared"));
                Assert.That(flow.ghostGuide.activeSelf, Is.True);
                Assert.That(flow.memoryVisuals.activeSelf, Is.False); Assert.That(flow.memoryLight.enabled, Is.False);
            }
            finally { if (game && game.narrative) game.narrative.SignalAccepted -= handler; }
        }

        [UnityTest] [Category("BlackoutCheckpoint"), Category("BlackoutInspection")]
        public IEnumerator InterruptedRecordingCheckpointAndAutomaticBlackoutPreserveLatestDiscoveries()
        {
            StageChapterFive(); Assert.That(sanity.PrepareChapterRestart(out var baseline), Is.True);
            yield return EnterArchive();
            yield return ReadActualClue(flow.firstRecording);
            yield return Until(() => game.narrative.ActiveSequenceId == "memories.recording-one");
            Assert.That(game.narrative.HasSignal("memories.recording-one-found"), Is.True);
            Assert.That(game.narrative.HasSequence("memories.recording-one"), Is.False);
            game.Pause(); float savedSanity = sanity.sanity;
            Assert.That(game.save.Save(), Is.True);
            Assert.That(SaveSystem.TryRead(game.save.SavePath, out var checkpoint), Is.True);
            Assert.That(checkpoint.objects.Any(item => item.id == flow.persistentId), Is.True);
            Assert.That(checkpoint.inventory.Any(item => item.id == "memory_recording_one"), Is.True);
            int accepted = 0; Action<string> handler = id => accepted++;
            game.narrative.SignalAccepted += handler;
            Assert.That(game.save.LoadCurrent(), Is.True); game.narrative.InitializeAfterSession(true);
            Assert.That(accepted, Is.Zero, "Checkpoint restoration must not replay revelations or shocks.");
            Assert.That(sanity.sanity, Is.EqualTo(savedSanity).Within(.01f));
            Assert.That(game.narrative.NextSignal, Is.EqualTo("memories.recording-one"));
            Assert.That(flow.firstRecording.CanInteract(game.interaction), Is.False);
            Assert.That(game.interaction.inventory.Contains("memory_recording_one"), Is.True);
            game.narrative.SignalAccepted -= handler;
            game.Resume();
            yield return Until(() => game.narrative.HasSequence("memories.recording-one") && game.CanControl && !game.narrative.SequencePlaying);
            Assert.That(game.narrative.Progress.milestones.Count(id => id == "memories.recording-one-found"), Is.EqualTo(1));
            Assert.That(game.narrative.Progress.milestones.Count(id => id == "memories.recording-one"), Is.EqualTo(1));
            Assert.That(game.save.Save(), Is.True);
            Assert.That(SaveSystem.TryRead(game.save.SavePath, out checkpoint), Is.True);
            var session = game;
            sanity.sanity = 0; sanity.AdvanceSanity(.1f, false);
            Assert.That(sanity.BlackingOut, Is.True);
            Assert.That(game.State, Is.EqualTo(GameState.BlackingOut));
            Assert.That(game.save.Save(), Is.False);
            sanity.AdvanceSanity(sanity.blackoutSeconds + .1f, false);
            StringAssert.Contains("Você nunca deveria ter voltado", game.hud.subtitle.text);
            yield return Until(() => game.State == GameState.Playing && !sanity.BlackingOut, 8);
            Assert.That(GameSession.Instance, Is.SameAs(session));
            Assert.That(game.hud.gameOverPanel.activeSelf, Is.False);
            Assert.That(game.save.fileName, Is.EqualTo(testSaveName));
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo("memories"));
            Assert.That(game.narrative.NextSignal, Is.EqualTo("memories.recording-two-found"));
            Assert.That(JsonUtility.ToJson(game.narrative.CaptureProgress()), Is.EqualTo(JsonUtility.ToJson(checkpoint.narrative)));
            Assert.That(game.narrative.HasSignal("school.blackout"), Is.True);
            Assert.That(game.narrative.HasSignal("left-behind.locked-area-opened"), Is.True);
            Assert.That(game.narrative.HasSequence("school.drawing-inscription"), Is.True);
            Assert.That(game.narrative.HasSequence("left-behind.wall-inscription"), Is.True);
            foreach (string id in new[] { "school", "left-behind" }) Assert.That(game.narrative.Progress.completedChapters.Contains(id), Is.True);
            foreach (var item in IncomingItems()) Assert.That(game.interaction.inventory.Contains(item.id), Is.True, item.id);
            Assert.That(game.interaction.inventory.Contains("memory_recording_one"), Is.True);
            foreach (string id in new[] { "memory_recording_two", "memories_group_photo" })
                Assert.That(game.interaction.inventory.Contains(id), Is.False, "Blackout cannot invent unsaved discoveries: " + id);
            Assert.That(game.save.flashlight.BatterySeconds, Is.EqualTo(checkpoint.battery).Within(1));
            Assert.That(Vector3.Distance(game.player.transform.position, checkpoint.position), Is.LessThan(.10f));
            Assert.That(sanity.sanity, Is.EqualTo(60).Within(.2f));
            Assert.That(flow.memoryVisuals.activeSelf, Is.False); Assert.That(flow.memoryLight.enabled, Is.False);
            Assert.That(flow.ghostGuide.activeSelf, Is.False);
            Assert.That(flow.firstRecording.visual.activeSelf, Is.False);
        }
    }
}
