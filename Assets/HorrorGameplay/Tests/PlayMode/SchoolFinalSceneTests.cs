// Verifica SetUp, TearDown, ActualFiveHandoffRoofDoorAndDescentContinueInPlaceToTheFinalChapter, TerracePauseAndBothManualCheckpointsPreserveDismissedPresenceAndOnlyPendingSignals, HeldPhoneRequiresFreshInputItsCheckpointResumesAndOccludedWindowCannotEndTheGame, TerraceBlackoutReturnsToLatestCheckpointWithProgressAndCollectedSupply, FinalBlackoutReturnsToLatestCheckpointWithProgressAndCollectedSupply, ContinuousInputClimbsBothNewFlightsCrossesTheirLandingAndDescendsWithoutIntermediateWarp.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    /// <summary>Real SchoolCEEP flow, interaction, presence and checkpoint coverage.
    /// Supported warps stage these checks; separate route tests own continuous walking.</summary>
    [Category("SchoolFinalScene"), Explicit("Historical seven-chapter scene. Replaced by SchoolSimplifiedSceneTests for the installed six-chapter campaign.")]
    public sealed class SchoolFinalSceneTests
    {
        GameSession game;
        SchoolTerraceFlow terrace;
        SchoolLastExitFlow exit;
        SchoolFinalPhone phone;
        SchoolMemoriesFlow memories;
        SchoolOpeningFlow opening;
        SchoolSanityController sanity;
        Keyboard keyboard;
        Mouse mouse;
        InputSettings.BackgroundBehavior originalBackground;
        InputSettings.EditorInputBehaviorInPlayMode originalEditorInput;
        bool originalRunInBackground, hadSavedScene, hadResume, hadCarry;
        string testSaveName, savedScene;
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
            testSaveName = "school-final-scene-fixture-" + Guid.NewGuid().ToString("N") + ".json";
            originalBackground = InputSystem.settings.backgroundBehavior;
            originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            originalRunInBackground = Application.runInBackground;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true; Time.timeScale = 1; AudioListener.pause = false;
            SceneManager.sceneLoaded += RedirectInitialSave;
            yield return SceneManager.LoadSceneAsync("SchoolCEEP");
            yield return Until(() => GameSession.Instance && GameSession.Instance.narrative && GameSession.Instance.narrative.IsInitialized, 18);
            BindScene();
            Assert.That(terrace, Is.Not.Null); Assert.That(exit, Is.Not.Null); Assert.That(phone, Is.Not.Null);
            Assert.That(memories, Is.Not.Null); Assert.That(opening, Is.Not.Null); Assert.That(sanity, Is.Not.Null);
            Assert.That(terrace.director, Is.SameAs(game.narrative)); Assert.That(exit.director, Is.SameAs(game.narrative));
            Assert.That(phone.flow, Is.SameAs(exit)); Assert.That(game.save.fileName, Is.EqualTo(testSaveName));
            keyboard = InputSystem.AddDevice<Keyboard>(); game.input.Actions.devices = new InputDevice[] { keyboard };
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            ConfigureRuntimeTimings(); yield return null; yield return null;
        }
        void BindScene()
        {
            game = GameSession.Instance;
            terrace = UnityEngine.Object.FindAnyObjectByType<SchoolTerraceFlow>();
            exit = UnityEngine.Object.FindAnyObjectByType<SchoolLastExitFlow>();
            phone = UnityEngine.Object.FindAnyObjectByType<SchoolFinalPhone>(FindObjectsInactive.Include);
            memories = UnityEngine.Object.FindAnyObjectByType<SchoolMemoriesFlow>();
            opening = UnityEngine.Object.FindAnyObjectByType<SchoolOpeningFlow>();
            sanity = UnityEngine.Object.FindAnyObjectByType<SchoolSanityController>();
            if (keyboard != null && keyboard.added) game.input.Actions.devices = new InputDevice[] { keyboard };
        }
        void ConfigureRuntimeTimings()
        {
            game.player.view.bobEnabled = false; game.player.view.swayEnabled = false;
            game.narrative.transitionView.fadeSeconds = .12f; game.narrative.transitionView.cardSeconds = .20f;
            sanity.darknessLossPerSecond = sanity.flashlightRecoveryPerSecond = sanity.lightRecoveryPerSecond = 0;
            if (clonedSessions.Add(game))
            {
                var clone = UnityEngine.Object.Instantiate(game.narrative.book);
                clone.name = game.narrative.book.name + " — Final scene fixture";
                clone.hideFlags = HideFlags.DontSave; clonedBooks.Add(clone); game.narrative.book = clone;
            }
            foreach (var sequence in game.narrative.book.sequences)
            {
                foreach (var line in sequence.lines ?? Array.Empty<StoryLine>()) if (line != null) line.seconds = .12f;
                if (sequence.lines == null || sequence.lines.Length == 0 || sequence.lines[0] == null) continue;
                if (sequence.id == "terrace.truth") sequence.lines[0].seconds = 3.2f;
                if (sequence.id == "terrace.voices" || sequence.id == "terrace.unravel" || sequence.id == "last-exit.final-call")
                    sequence.lines[0].seconds = .9f;
            }
            Assert.That(game.narrative.book.Chapter("terrace").sceneName, Is.EqualTo("SchoolCEEP"));
            Assert.That(game.narrative.book.Chapter("last-exit").sceneName, Is.EqualTo("SchoolCEEP"));
        }
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
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = originalBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
            Application.runInBackground = originalRunInBackground;
            var loaded = SceneManager.GetSceneByName("SchoolCEEP");
            var blank = SceneManager.GetSceneByName("School-final-cleanup");
            if (!blank.IsValid() || !blank.isLoaded) blank = SceneManager.CreateScene("School-final-cleanup");
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
        IEnumerator Until(Func<bool> condition, float seconds = 15)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            var session = game ? game : GameSession.Instance;
            string detail = session && session.narrative ? session.narrative.NextSignal + ", state=" + session.State
                + ", sequence=" + session.narrative.ActiveSequenceId : "initializing";
            Assert.That(condition(), Is.True, "Final scene deadline: " + detail);
        }
        static List<InventoryItem> IncomingItems() => new List<InventoryItem>
        {
            new InventoryItem { id = "office_equipment", title = "Equipamento" },
            new InventoryItem { id = "office_photo", title = "Foto de casa", note = true },
            new InventoryItem { id = "school_documents", title = "Registro", note = true },
            new InventoryItem { id = "school_photograph", title = "Turma", note = true },
            new InventoryItem { id = "school_drawing", title = "Não suba", note = true },
            new InventoryItem { id = "old_disappearance_report", title = "Relatório", note = true },
            new InventoryItem { id = "old_student_photo", title = "João — aluno", note = true },
            new InventoryItem { id = "old_key", title = "Chave antiga" },
            new InventoryItem { id = "memory_recording_one", title = "Fita 1", note = true },
            new InventoryItem { id = "memory_recording_two", title = "Fita 2", note = true },
            new InventoryItem { id = "memories_group_photo", title = "A noite esquecida", note = true }
        };
        NarrativeProgress PriorChapters(string chapterId)
        {
            var progress = new NarrativeProgress { chapterId = chapterId };
            foreach (var chapter in game.narrative.book.chapters.Take(game.narrative.book.ChapterIndex(chapterId)))
            {
                progress.completedChapters.Add(chapter.id);
                foreach (var milestone in chapter.milestones) progress.milestones.Add(milestone.id);
            }
            foreach (var sequence in game.narrative.book.sequences)
                if (!string.IsNullOrEmpty(sequence.completionSignal) && progress.HasSignal(sequence.completionSignal)) progress.sequences.Add(sequence.id);
            return progress;
        }
        void StageChapter(string chapterId)
        {
            // Current gates use the installed flow; only incoming chapters are completed directly.
            game.interaction.inventory.Restore(IncomingItems()); game.save.flashlight.Restore(137, true); sanity.sanity = 90;
            WarpAway(FeetInVolume(terrace.descentVolume), exit.windowLook);
            game.narrative.RestoreProgress(PriorChapters(chapterId)); game.narrative.InitializeAfterSession(true);
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo(chapterId));
            Assert.That(sanity.PrepareChapterRestart(out var baseline), Is.True);
            Assert.That(baseline.narrative.chapterId, Is.EqualTo(chapterId));
            Assert.That(baseline.inventory.Any(item => item.id == "memories_group_photo"), Is.True);
        }
        bool CapsuleClear(Vector3 feet)
        {
            float radius = game.player.Controller.radius, height = game.player.standingHeight;
            return !Physics.CheckCapsule(feet + Vector3.up * (radius + .02f), feet + Vector3.up * (height - radius - .02f),
                radius, WorldMask, QueryTriggerInteraction.Ignore);
        }
        List<Vector3> VolumeFeet(BoxCollider volume)
        {
            Assert.That(volume, Is.Not.Null); Physics.SyncTransforms();
            var result = new List<Vector3>(); var bounds = volume.bounds;
            foreach (float x in new[] { 0f, -.35f, .35f, -.7f, .7f }) foreach (float z in new[] { 0f, -.35f, .35f, -.7f, .7f })
            {
                Vector3 probe = new Vector3(bounds.center.x + bounds.extents.x * x, bounds.max.y - .1f,
                    bounds.center.z + bounds.extents.z * z);
                if (!Physics.Raycast(probe, Vector3.down, out var hit, bounds.size.y + 2, WorldMask, QueryTriggerInteraction.Ignore)
                    || hit.normal.y < .75f) continue;
                Vector3 feet = hit.point + Vector3.up * .045f;
                if (volume.bounds.Contains(feet + Vector3.up) && CapsuleClear(feet)) result.Add(feet);
            }
            Assert.That(result.Count, Is.GreaterThan(0), "An actual supported capsule must fit " + volume.name);
            return result;
        }
        Vector3 FeetInVolume(BoxCollider volume, bool farFromRoofDoor = false)
        {
            var feet = VolumeFeet(volume);
            if (farFromRoofDoor)
            {
                var hinge = terrace.terraceDoor.hinge ? terrace.terraceDoor.hinge : terrace.terraceDoor.transform;
                return feet.OrderByDescending(point => Vector3.ProjectOnPlane(point - hinge.position, Vector3.up).sqrMagnitude).First();
            }
            return feet.OrderBy(point => Vector3.ProjectOnPlane(point - volume.bounds.center, Vector3.up).sqrMagnitude).First();
        }
        Vector3 DoorFeet(InteractiveDoor door)
        {
            Vector3 target = door.leafCollider.bounds.center;
            var positions = new List<Vector3>();
            foreach (float distance in new[] { 1f, 1.3f, 1.6f, 1.9f }) for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI / 16;
                Vector3 probe = target + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance + Vector3.up;
                if (!Physics.Raycast(probe, Vector3.down, out var floor, 4, WorldMask, QueryTriggerInteraction.Ignore)
                    || floor.normal.y < .75f) continue;
                Vector3 feet = floor.point + Vector3.up * .045f;
                if (!CapsuleClear(feet)) continue;
                Vector3 eye = feet + Vector3.up * game.player.EyeHeight, delta = target - eye;
                if (delta.magnitude <= 2.5f && Physics.Raycast(eye, delta.normalized, out var hit, delta.magnitude + .15f,
                    WorldMask, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<InteractiveDoor>() == door) positions.Add(feet);
            }
            Assert.That(positions.Count, Is.GreaterThan(0), "The actual door must be reachable by the interaction ray.");
            return positions.OrderBy(point => Vector3.Distance(point, target)).First();
        }
        void WarpAim(Vector3 feet, Vector3 target)
        {
            Vector3 delta = target - (feet + Vector3.up * game.player.EyeHeight);
            game.player.Warp(feet, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, -Mathf.Asin(delta.normalized.y) * Mathf.Rad2Deg);
            Physics.SyncTransforms();
        }
        void WarpAway(Vector3 feet, Transform target)
        {
            Vector3 eye = feet + Vector3.up * game.player.EyeHeight;
            WarpAim(feet, target ? eye - (target.position - eye) : eye + Vector3.back * 10);
        }
        IEnumerator EnterAscent()
        {
            yield return Until(() => game.CanControl && !game.narrative.SequencePlaying);
            Assert.That(terrace.terraceDoor.locked, Is.True);
            Assert.That(terrace.terraceDoor.IsOpen, Is.False);
            terrace.terraceDoor.Interact(game.interaction);
            Assert.That(terrace.terraceDoor.locked, Is.True, "Earlier keys cannot unlock the roof story gate.");
            WarpAway(FeetInVolume(terrace.ascentVolume), terrace.demonLook);
            yield return Until(() => game.narrative.ActiveSequenceId == "terrace.voices");
            Assert.That(terrace.terraceDoor.locked, Is.True); Assert.That(game.narrative.HasSignal("terrace.entered"), Is.False);
            yield return Until(() => game.narrative.HasSequence("terrace.voices") && game.CanControl && !game.narrative.SequencePlaying);
            Assert.That(terrace.terraceDoor.locked, Is.False); Assert.That(game.narrative.NextSignal, Is.EqualTo("terrace.entered"));
        }
        IEnumerator OpenActualRoofDoor()
        {
            WarpAim(DoorFeet(terrace.terraceDoor), terrace.terraceDoor.leafCollider.bounds.center);
            yield return null; yield return null;
            Assert.That(game.interaction.Target, Is.SameAs(terrace.terraceDoor));
            game.interaction.Target.Interact(game.interaction);
            Assert.That(terrace.terraceDoor.IsOpen, Is.True);
            // Opening the door alone must not skip physical entry.
            Assert.That(game.narrative.HasSignal("terrace.entered"), Is.False);
        }
        IEnumerator EnterRoofTruth()
        {
            yield return EnterAscent(); yield return OpenActualRoofDoor();
            WarpAim(FeetInVolume(terrace.terraceVolume, true), terrace.demonLook.position);
            yield return Until(() => game.narrative.ActiveSequenceId == "terrace.truth");
            Assert.That(game.narrative.HasSignal("terrace.entered"), Is.True);
            Assert.That(game.narrative.HasSignal("terrace.door-closed"), Is.True);
            Assert.That(terrace.terraceDoor.locked, Is.True); Assert.That(terrace.terraceDoor.IsOpen, Is.False);
            Assert.That(CapsuleClear(game.player.transform.position), Is.True, "The closing sweep cannot trap the player capsule.");
        }
        IEnumerator FinishTerrace()
        {
            yield return Until(() => game.narrative.HasSequence("terrace.unravel") && game.CanControl && !game.narrative.SequencePlaying);
            Assert.That(terrace.terraceDoor.locked, Is.False); Assert.That(terrace.terraceDoor.IsOpen, Is.True);
            Assert.That(terrace.ghostGuide.activeSelf, Is.True);
            WarpAway(FeetInVolume(terrace.descentVolume), exit.windowLook);
            yield return Until(() => game.narrative.Progress.chapterId == "last-exit" && game.CanControl);
            Assert.That(terrace.IsChapterActive, Is.False); Assert.That(exit.IsChapterActive, Is.True);
            Assert.That(terrace.demonVisual.activeSelf || terrace.childrenVisuals.activeSelf || terrace.ghostGuide.activeSelf, Is.False);
            Assert.That(terrace.eventLight.enabled, Is.False); Assert.That(terrace.memoryAnchor.activeSelf, Is.False);
            Assert.That(exit.memoryAnchor.activeSelf, Is.True);
        }
        IEnumerator ReachPhone()
        {
            yield return Until(() => game.CanControl && !game.narrative.SequencePlaying);
            WarpAway(FeetInVolume(exit.entranceVolume), exit.windowLook);
            yield return Until(() => game.narrative.HasSignal("last-exit.entrance-reached"));
            Assert.That(exit.entranceDoors.All(door => door && !door.locked), Is.True);
            foreach (var door in exit.entranceDoors) if (!door.IsOpen) door.Interact(game.interaction);
            Assert.That(exit.entranceDoors.All(door => door.IsOpen), Is.True);
            WarpAway(FeetInVolume(exit.outsideVolume), exit.windowLook);
            yield return Until(() => game.narrative.HasSignal("last-exit.outside"));
            WarpAway(FeetInVolume(exit.phoneVolume), exit.windowLook);
            // E pressed while entering this volume must not answer a phone that has just appeared.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Until(() => game.narrative.HasSignal("last-exit.phone-ringing"));
            Assert.That(game.narrative.HasSignal("last-exit.final-call"), Is.False);
            Assert.That(game.narrative.ActiveSequenceId, Is.Not.EqualTo("last-exit.final-call"));
            Assert.That(exit.phoneVisual.activeSelf, Is.True);
            Assert.That(phone.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled), Is.True,
                "The hand phone must not block doors or observation rays.");
            yield return Until(() => phone.ReadyToAnswer);
            Assert.That(phone.CanInteract(game.interaction), Is.True);
            yield return null;
            Vector3 expected = exit.playerCamera.transform.TransformPoint(exit.phoneHandOffset);
            Assert.That(Vector3.Distance(exit.phoneVisual.transform.position, expected), Is.LessThan(.03f));
        }
        IEnumerator PressPhoneE()
        {
            // Let the regular dynamic input update deliver the edge to MonoBehaviour.Update.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Until(() => game.narrative.ActiveSequenceId == "last-exit.final-call");
            Assert.That(phone.CanInteract(game.interaction), Is.False);
        }
        Vector3 WindowFeet()
        {
            // The administration facade occludes the telephone location. The left garden is the
            // authored, physically supported observation point; do not move the window for the test.
            var positions = VolumeFeet(exit.outsideVolume);
            foreach (float x in new[] { -26f, -25f, -27f }) foreach (float z in new[] { 15f, 14f, 16f })
            {
                if (!Physics.Raycast(new Vector3(x, 1.2f, z), Vector3.down, out var hit, 4, WorldMask, QueryTriggerInteraction.Ignore)
                    || hit.normal.y < .75f) continue;
                Vector3 feet = hit.point + Vector3.up * .045f;
                if (CapsuleClear(feet)) positions.Add(feet);
            }
            foreach (Vector3 feet in positions)
            {
                WarpAim(feet, exit.windowLook.position);
                if (exit.Observes(exit.windowLook, exit.observationDistance)) return feet;
            }
            Assert.Fail("A supported exterior position must see the authored last window without a wall crossing the ray.");
            return Vector3.zero;
        }
        static void AssertImmaterial(GameObject visual)
        {
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled), Is.True);
            Assert.That(visual.GetComponentsInChildren<Rigidbody>(true).All(body => body.isKinematic && !body.detectCollisions), Is.True);
        }

        [UnityTest]
        public IEnumerator ActualFiveHandoffRoofDoorAndDescentContinueInPlaceToTheFinalChapter()
        {
            StageChapter("memories");
            var progress = PriorChapters("memories");
            foreach (var milestone in game.narrative.book.Chapter("memories").milestones.TakeWhile(item => item.id != "memories.staircase-indicated"))
                progress.milestones.Add(milestone.id);
            foreach (var sequence in game.narrative.book.sequences)
                if (sequence.id != "memories.ghost" && progress.HasSignal(sequence.completionSignal)
                    && !progress.sequences.Contains(sequence.id)) progress.sequences.Add(sequence.id);
            Vector3 position = game.player.transform.position; var session = game;
            game.narrative.RestoreProgress(progress); game.narrative.InitializeAfterSession(true);
            Assert.That(game.narrative.NextSignal, Is.EqualTo("memories.staircase-indicated"));
            yield return Until(() => game.narrative.Progress.chapterId == "terrace" && game.CanControl);
            Assert.That(game, Is.SameAs(session)); Assert.That(Vector3.Distance(game.player.transform.position, position), Is.LessThan(.1f));
            Assert.That(game.narrative.NextSignal, Is.EqualTo("terrace.ascent-started"));
            Assert.That(memories.memoryVisuals.activeSelf || memories.ghostGuide.activeSelf, Is.False);
            Assert.That(memories.memoryLight.enabled, Is.False);
            foreach (var item in IncomingItems()) Assert.That(game.interaction.inventory.Contains(item.id), Is.True);
            yield return EnterRoofTruth(); yield return FinishTerrace();
            Assert.That(game.narrative.Progress.milestones.Count(id => id.StartsWith("terrace.", StringComparison.Ordinal)), Is.EqualTo(7));
            Assert.That(game.narrative.HasSignal("terrace.final-chase"), Is.True, "This legacy ID means only environmental escape.");
            Assert.That(UnityEngine.Object.FindObjectsByType<EnemyBrain>().Length, Is.Zero);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("SchoolCEEP"));
        }

        [UnityTest]
        public IEnumerator TerracePauseAndBothManualCheckpointsPreserveDismissedPresenceAndOnlyPendingSignals()
        {
            StageChapter("terrace"); yield return EnterRoofTruth();
            Assert.That(game.CanControl, Is.True, "João can turn and explore while the roof memory is narrated.");
            Assert.That(terrace.demonVisual.activeSelf, Is.True); Assert.That(terrace.childrenVisuals.activeSelf, Is.True);
            AssertImmaterial(terrace.demonVisual); AssertImmaterial(terrace.childrenVisuals); AssertImmaterial(terrace.ghostGuide);
            Vector3 demonPosition = terrace.demonVisual.transform.position; Quaternion demonRotation = terrace.demonVisual.transform.rotation;
            Assert.That(terrace.Observes(terrace.demonLook, terrace.observationDistance), Is.True);
            terrace.AdvanceFlow(.05f); string beforePause = terrace.CaptureState();
            game.Pause(); terrace.AdvanceFlow(90);
            Assert.That(terrace.demonVisual.activeSelf || terrace.childrenVisuals.activeSelf || terrace.distortionVisuals.activeSelf, Is.False);
            Assert.That(terrace.eventLight.enabled, Is.False); Assert.That(terrace.CaptureState(), Is.EqualTo(beforePause));
            game.Resume(); yield return Until(() => terrace.demonVisual.activeSelf);
            WarpAway(game.player.transform.position, terrace.demonLook); terrace.AdvanceFlow(.01f);
            Assert.That(terrace.demonVisual.activeSelf, Is.False);
            Assert.That(terrace.demonVisual.transform.position, Is.EqualTo(demonPosition));
            Assert.That(terrace.demonVisual.transform.rotation, Is.EqualTo(demonRotation));
            Assert.That(game.narrative.HasSignal("terrace.cycle-revelation"), Is.False);
            var accepted = new List<string>(); Action<string> handler = id => accepted.Add(id);
            game.narrative.SignalAccepted += handler;
            try
            {
                game.Pause(); float savedSanity = sanity.sanity;
                Assert.That(game.save.Save(), Is.True); Assert.That(SaveSystem.TryRead(game.save.SavePath, out var checkpoint), Is.True);
                Assert.That(checkpoint.objects.Any(item => item.id == terrace.persistentId), Is.True);
                Assert.That(game.save.LoadCurrent(), Is.True); game.narrative.InitializeAfterSession(true);
                Assert.That(accepted, Is.Empty); Assert.That(sanity.sanity, Is.EqualTo(savedSanity).Within(.01f));
                Assert.That(terrace.demonVisual.activeSelf, Is.False); Assert.That(terrace.eventLight.enabled, Is.False);
                game.Resume(); yield return Until(() => game.narrative.ActiveSequenceId == "terrace.truth");
                Assert.That(terrace.demonVisual.activeSelf, Is.False, "A dismissed presence cannot replay during resumed narration.");
                yield return Until(() => game.narrative.ActiveSequenceId == "terrace.unravel");
                Assert.That(accepted, Is.EquivalentTo(new[] { "terrace.cycle-revelation" }));
                accepted.Clear(); game.Pause(); savedSanity = sanity.sanity;
                Assert.That(game.save.Save(), Is.True); Assert.That(game.save.LoadCurrent(), Is.True);
                game.narrative.InitializeAfterSession(true);
                Assert.That(accepted, Is.Empty); Assert.That(sanity.sanity, Is.EqualTo(savedSanity).Within(.01f));
                Assert.That(game.narrative.NextSignal, Is.EqualTo("terrace.final-chase"));
                game.Resume(); yield return Until(() => game.narrative.HasSequence("terrace.unravel") && game.CanControl && !game.narrative.SequencePlaying);
                CollectionAssert.AreEquivalent(new[] { "terrace.final-chase" }, accepted);
                Assert.That(terrace.terraceDoor.IsOpen, Is.True); Assert.That(terrace.terraceDoor.locked, Is.False);
                Assert.That(terrace.eventLight.enabled, Is.False);
                foreach (string id in new[] { "terrace.cycle-revelation", "terrace.final-chase" })
                    Assert.That(game.narrative.Progress.milestones.Count(signal => signal == id), Is.EqualTo(1));
            }
            finally { if (game && game.narrative) game.narrative.SignalAccepted -= handler; }
        }
        [UnityTest]
        public IEnumerator HeldPhoneRequiresFreshInputItsCheckpointResumesAndOccludedWindowCannotEndTheGame()
        {
            StageChapter("last-exit"); yield return ReachPhone();
            Assert.That(game.narrative.SequencePlaying, Is.False, "Waiting near the phone never answers it automatically.");
            Assert.That(phone.ringSound, Is.Not.Null, "The hand phone reuses an authored ringing SoundSet.");
            yield return Until(() => phone.RingPlaying);
            float phoneAge = phone.VisibleAge;
            game.Pause(); phone.AdvanceInput(90, true);
            Assert.That(phone.RingPlaying, Is.False, "Pause must stop only the phone's owned ringing source.");
            Assert.That(phone.VisibleAge, Is.EqualTo(phoneAge));
            Assert.That(game.narrative.ActiveSequenceId, Is.Not.EqualTo("last-exit.final-call"));
            game.Resume(); yield return Until(() => phone.RingPlaying);
            yield return PressPhoneE();
            Assert.That(phone.RingPlaying, Is.False, "Answering stops the ring before beginning the caption-only call.");
            game.Pause(); float savedSanity = sanity.sanity;
            Assert.That(game.save.Save(), Is.True);
            var accepted = new List<string>(); Action<string> handler = id => accepted.Add(id);
            game.narrative.SignalAccepted += handler;
            try
            {
                Assert.That(game.save.LoadCurrent(), Is.True); game.narrative.InitializeAfterSession(true);
                Assert.That(accepted, Is.Empty); Assert.That(sanity.sanity, Is.EqualTo(savedSanity).Within(.01f));
                Assert.That(exit.entranceDoors.All(door => !door.locked && door.IsOpen), Is.True,
                    "Chapter 3 must not close or relock the saved exit after the final chapter owns it.");
                Assert.That(exit.windowSilhouette.activeSelf, Is.False); Assert.That(exit.windowLight.enabled, Is.False);
                game.Resume(); yield return Until(() => game.narrative.ActiveSequenceId == "last-exit.final-call");
                yield return Until(() => game.narrative.HasSequence("last-exit.final-call") && game.CanControl && !game.narrative.SequencePlaying);
                CollectionAssert.AreEqual(new[] { "last-exit.final-call" }, accepted);
                Assert.That(exit.phoneVisual.activeSelf, Is.False); Assert.That(exit.windowLight.enabled, Is.True);
                Assert.That(exit.windowSilhouette.activeSelf, Is.True); AssertImmaterial(exit.windowSilhouette);
                Vector3 feet = WindowFeet(), eye = feet + Vector3.up * game.player.EyeHeight;
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.name = "Temporary observation obstruction";
                blocker.transform.position = eye + (exit.windowLook.position - eye).normalized * 1.3f;
                blocker.transform.localScale = Vector3.one * .65f;
                Physics.SyncTransforms();
                try
                {
                    Assert.That(exit.Observes(exit.windowLook, exit.observationDistance), Is.False);
                    exit.AdvanceFlow(2);
                    Assert.That(game.narrative.HasSignal("last-exit.last-window"), Is.False);
                    game.Pause(); exit.AdvanceFlow(90);
                    Assert.That(exit.windowLight.enabled, Is.False); Assert.That(exit.windowSilhouette.activeSelf, Is.False);
                    Assert.That(game.narrative.HasSignal("last-exit.last-window"), Is.False);
                }
                finally { UnityEngine.Object.Destroy(blocker); }
                yield return null; Physics.SyncTransforms(); game.Resume();
                yield return Until(() => game.narrative.Progress.endingSeen && game.State == GameState.StoryScreen);
                Assert.That(game.narrative.Progress.pendingChapterId, Is.Empty);
                Assert.That(game.narrative.Progress.completedChapters.Contains("last-exit"), Is.True);
                foreach (string id in new[] { "last-exit.final-call", "last-exit.last-window", "last-exit.ending" })
                    Assert.That(game.narrative.Progress.milestones.Count(signal => signal == id), Is.EqualTo(1));
                Assert.That(UnityEngine.Object.FindObjectsByType<EnemyBrain>().Length, Is.Zero);
            }
            finally { if (game && game.narrative) game.narrative.SignalAccepted -= handler; }
        }

        IEnumerator RecoverLatestCheckpoint(string chapterId)
        {
            StageChapter(chapterId); Assert.That(sanity.PrepareChapterRestart(out var baseline), Is.True);
            if (chapterId == "terrace")
            {
                yield return EnterRoofTruth();
                yield return Until(() => game.narrative.HasSequence("terrace.unravel") && game.CanControl && !game.narrative.SequencePlaying);
            }
            else
            {
                yield return ReachPhone(); yield return PressPhoneE();
                yield return Until(() => game.narrative.HasSequence("last-exit.final-call") && game.CanControl && !game.narrative.SequencePlaying);
            }
            var supply = (chapterId == "terrace" ? terrace.supplies : exit.supplies).FirstOrDefault(item => item && item.CanInteract(null));
            Assert.That(supply, Is.Not.Null, "Each long chapter supplies a usable battery reserve.");
            string supplyId = supply.persistentId; supply.Interact(game.interaction);
            Assert.That(supply.CanInteract(null), Is.False); Assert.That(supply.gameObject.activeSelf, Is.False);
            if (chapterId == "terrace") terrace.RestoreWorldSilently(); else exit.RestoreWorldSilently();
            Assert.That(supply.gameObject.activeSelf, Is.False, "Ownership restoration cannot revive a collected supply.");
            Assert.That(game.save.Save(), Is.True);
            Assert.That(SaveSystem.TryRead(game.save.SavePath, out var checkpoint), Is.True);
            string nextSignal = game.narrative.NextSignal;
            bool demonVisible = terrace.demonVisual.activeSelf, childrenVisible = terrace.childrenVisuals.activeSelf;
            bool eventLight = terrace.eventLight.enabled, windowLight = exit.windowLight.enabled, phoneVisible = exit.phoneVisual.activeSelf;
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
            Assert.That(game.save.fileName, Is.EqualTo(testSaveName)); Assert.That(game.narrative.Progress.chapterId, Is.EqualTo(chapterId));
            Assert.That(JsonUtility.ToJson(game.narrative.CaptureProgress()), Is.EqualTo(JsonUtility.ToJson(checkpoint.narrative)));
            Assert.That(game.narrative.NextSignal, Is.EqualTo(nextSignal));
            Assert.That(game.narrative.HasSignal("memories.staircase-indicated"), Is.True);
            if (chapterId == "last-exit") Assert.That(game.narrative.HasSignal("terrace.descent-exit"), Is.True);
            foreach (var item in IncomingItems()) Assert.That(game.interaction.inventory.Contains(item.id), Is.True);
            Assert.That(game.save.flashlight.BatterySeconds, Is.EqualTo(checkpoint.battery).Within(1));
            Assert.That(Vector3.Distance(game.player.transform.position, checkpoint.position), Is.LessThan(.1f));
            Assert.That(sanity.sanity, Is.EqualTo(60).Within(.2f));
            var restoredSupply = (chapterId == "terrace" ? terrace.supplies : exit.supplies).Single(item => item.persistentId == supplyId);
            Assert.That(restoredSupply.gameObject.activeSelf, Is.False); Assert.That(restoredSupply.CanInteract(null), Is.False);
            Assert.That(terrace.demonVisual.activeSelf, Is.EqualTo(demonVisible));
            Assert.That(terrace.childrenVisuals.activeSelf, Is.EqualTo(childrenVisible));
            Assert.That(terrace.eventLight.enabled, Is.EqualTo(eventLight)); Assert.That(exit.windowLight.enabled, Is.EqualTo(windowLight));
            Assert.That(exit.phoneVisual.activeSelf, Is.EqualTo(phoneVisible));
        }
        [UnityTest] [Category("BlackoutCheckpoint")] public IEnumerator TerraceBlackoutReturnsToLatestCheckpointWithProgressAndCollectedSupply() { yield return RecoverLatestCheckpoint("terrace"); }
        [UnityTest] [Category("BlackoutCheckpoint")] public IEnumerator FinalBlackoutReturnsToLatestCheckpointWithProgressAndCollectedSupply() { yield return RecoverLatestCheckpoint("last-exit"); }

        IEnumerator WalkKeys(Key key, Func<Vector3, bool> reached, string segment, float deadline)
        {
            float segmentDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 12);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            try
            {
                while (!reached(game.player.transform.position) && Time.realtimeSinceStartup < segmentDeadline)
                {
                    Assert.That(game.CanControl, Is.True, "Control was interrupted during " + segment);
                    if (game.player.IsCrouched)
                    {
                        var motor = game.player;
                        float radius = Mathf.Max(.05f, motor.Controller.radius - .025f);
                        Vector3 lower = motor.transform.position + Vector3.up * Mathf.Max(motor.Controller.height - radius, radius);
                        Vector3 upper = motor.transform.position + Vector3.up * (motor.standingHeight - radius);
                        string blockers = string.Join(", ", Physics.OverlapCapsule(lower, upper, radius, WorldMask, QueryTriggerInteraction.Ignore)
                            .Where(c => !c.transform.IsChildOf(motor.transform)).Select(c => c.name + " " + c.bounds));
                        Assert.Fail("Default standing headroom is required during " + segment + "; position="
                            + motor.transform.position.ToString("F4") + "; blockers=" + blockers);
                    }
                    Assert.That(game.player.Controller.height, Is.GreaterThan(game.player.standingHeight - .05f));
                    Assert.That(game.player.transform.position.y, Is.GreaterThan(3.5f), "The new stair route cannot drop through its floor.");
                    yield return null;
                }
            }
            finally { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); }
            Assert.That(reached(game.player.transform.position), Is.True, "Continuous input failed to reach " + segment
                + "; position=" + game.player.transform.position);
            // Let ordinary deceleration settle; never snap the body back onto a nominal centreline.
            yield return Until(() => game.player.HorizontalSpeed < .04f, 2);
            Assert.That(CapsuleClear(game.player.transform.position + Vector3.up * .045f), Is.True, segment);
            Assert.That(Physics.Raycast(game.player.transform.position + Vector3.up * .3f, Vector3.down, out var floor,
                .7f, WorldMask, QueryTriggerInteraction.Ignore), Is.True, "Supported floor required after " + segment);
            Assert.That(floor.normal.y, Is.GreaterThan(.75f), segment);
        }
        IEnumerator TurnByMouse(float heading)
        {
            float sensitivity = (float)typeof(FirstPersonMotor).GetField("sensitivity", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.player);
            float scale = sensitivity * PlayerPrefs.GetFloat("HorrorMenu.Sensitivity", 1);
            Assert.That(scale, Is.GreaterThan(0));
            // A single delta changes the real controller's target yaw; normal smoothing finishes the turn.
            float delta = Mathf.DeltaAngle(game.player.transform.eulerAngles.y, heading);
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(delta / scale, 0));
            yield return Until(() => Mathf.Abs(Mathf.DeltaAngle(game.player.transform.eulerAngles.y, heading)) < .15f, 3);
        }
        [UnityTest]
        public IEnumerator ContinuousInputClimbsBothNewFlightsCrossesTheirLandingAndDescendsWithoutIntermediateWarp()
        {
            StageChapter("terrace"); yield return EnterAscent();
            mouse = InputSystem.AddDevice<Mouse>(); game.input.Actions.devices = new InputDevice[] { keyboard, mouse };
            var start = new Vector3(-28.9531765f, 3.855f, 57.10f);
            Assert.That(CapsuleClear(start), Is.True);
            // This is the only route staging warp. Every subsequent position comes from FirstPersonMotor input.
            game.player.Warp(start, 0, 0); Physics.SyncTransforms(); yield return null; yield return null;
            float deadline = Time.realtimeSinceStartup + 60;
            yield return WalkKeys(Key.W, p => p.z >= 63.20f && p.y >= 5.55f, "west flight upward", deadline);
            Assert.That(game.player.transform.position.y, Is.EqualTo(5.60f).Within(.12f));
            yield return WalkKeys(Key.D, p => p.x >= -26.70f, "north return landing eastward", deadline);
            yield return TurnByMouse(180);
            yield return WalkKeys(Key.W, p => p.z <= 58.60f && p.y >= 7.08f, "east flight upward to the door", deadline);
            yield return null; yield return null;
            Assert.That(game.interaction.Target, Is.SameAs(terrace.terraceDoor), "The roof door must be reached and opened through the actual forward ray.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Until(() => terrace.terraceDoor.IsOpen);
            yield return new WaitForSeconds(1.1f);
            // The normal entry trigger may accept the roof and start its caption centimetres before 56.20.
            // Treat that real threshold as reached; never disable story flow to force movement through a cutscene.
            yield return WalkKeys(Key.W, p => p.y >= 7.30f && (p.z <= 56.20f
                || (p.z <= 56.40f && game.narrative.HasSignal("terrace.entered"))), "last step and actual roof threshold", deadline);
            Assert.That(game.player.transform.position.y, Is.EqualTo(7.36f).Within(.12f));
            yield return Until(() => game.narrative.HasSequence("terrace.unravel") && game.CanControl && !game.narrative.SequencePlaying);
            yield return TurnByMouse(0);
            yield return WalkKeys(Key.W, p => p.z >= 63.20f && p.y <= 5.67f, "east flight downward", deadline);
            yield return WalkKeys(Key.A, p => p.x <= -28.94f, "north return landing westward", deadline);
            yield return TurnByMouse(180);
            yield return WalkKeys(Key.W, p => p.z <= 57.10f && p.y <= 3.94f, "west flight downward to the original landing", deadline);
            Assert.That(game.player.transform.position.y, Is.EqualTo(3.855f).Within(.12f));
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "The supported route must complete within its 60-second fixture budget.");
        }
    }
}
