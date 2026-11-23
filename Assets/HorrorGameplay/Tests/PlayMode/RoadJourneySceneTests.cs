// Verifica SetUp, TearDown, RealRoadHasReachableCabinItemsWindowAndContinuousWalkToGate, CompletedOfficeCheckpointCarriesEquipmentIntoRealRoadSpawn.

using System;
using System.Collections;
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
    // Sample after the real camera and moving seat have both updated, including fast test frames.
    [DefaultExecutionOrder(999)]
    public sealed class RoadSeatedCameraProbe : MonoBehaviour
    {
        public RoadJourneyFlow flow;
        public float maximumError;
        public int movingFrames;
        void LateUpdate()
        {
            if(!flow || !flow.Seated || !flow.playerCamera || !flow.seatedPose) return;
            maximumError=Mathf.Max(maximumError,Vector3.Distance(flow.playerCamera.transform.position,flow.seatedPose.position));
            if(flow.Drive01 > .02f && flow.Drive01 < .98f) movingFrames++;
        }
    }

    /// <summary>Exercises the assembled road with physical aim, E and a continuous pedestrian walk.</summary>
    [Category("RoadJourneyScene")]
    [Category("GameplayPolish")]
    public sealed class RoadJourneySceneTests
    {
        GameSession game;
        RoadJourneyFlow flow;
        Keyboard keyboard;
        Mouse mouse;
        InputSettings.BackgroundBehavior originalBackground;
        InputSettings.EditorInputBehaviorInPlayMode originalEditorInput;
        bool originalRunInBackground, hadSaveScene, hadResume, hadCarry;
        string savedScene, testSaveName;
        int resume, carry;
        byte[] save, backup, temporary;
        string RealSavePath => Path.Combine(Application.persistentDataPath, "save.json");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            save = ReadFile(RealSavePath);
            backup = ReadFile(RealSavePath + ".bak");
            temporary = ReadFile(RealSavePath + ".tmp");
            hadSaveScene = PlayerPrefs.HasKey("Game.SaveScene");
            hadResume = PlayerPrefs.HasKey("Game.ResumeRequested");
            hadCarry = PlayerPrefs.HasKey("Game.StoryCarryRequested");
            savedScene = PlayerPrefs.GetString("Game.SaveScene", "");
            resume = PlayerPrefs.GetInt("Game.ResumeRequested", 0);
            carry = PlayerPrefs.GetInt("Game.StoryCarryRequested", 0);
            PlayerPrefs.SetInt("Game.ResumeRequested", 0);
            PlayerPrefs.SetInt("Game.StoryCarryRequested", 0);
            testSaveName = "road-scene-fixture-" + Guid.NewGuid().ToString("N") + ".json";
            originalBackground = InputSystem.settings.backgroundBehavior;
            originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            originalRunInBackground = Application.runInBackground;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true;
            Time.timeScale = 1;
            AudioListener.pause = false;
            // This callback runs before Start, so even the first automatic checkpoint is disposable.
            SceneManager.sceneLoaded += RedirectInitialSave;
            yield return SceneManager.LoadSceneAsync("RoadJourney");
            yield return Until(() => GameSession.Instance && GameSession.Instance.narrative
                && GameSession.Instance.narrative.IsInitialized, 12);
            game = GameSession.Instance;
            flow = UnityEngine.Object.FindAnyObjectByType<RoadJourneyFlow>();
            Assert.That(flow, Is.Not.Null);
            Assert.That(flow.director, Is.SameAs(game.narrative));
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo("road"));
            Assert.That(game.save.fileName, Is.EqualTo(testSaveName));
            Assert.That(game.narrative.autoLoadNextChapter, Is.True, "The authorized school chapter is now available.");
            // These driving fixtures deliberately keep their completion card for inspection.
            // The installed campaign uses the six-chapter StoryBook; school progression has its own scene test.
            game.narrative.autoLoadNextChapter=false;
            Assert.That((flow.observationMask.value & LayerMask.GetMask("Player")), Is.Zero);
            Assert.That(UnityEngine.Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include), Is.Empty);
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            game.input.Actions.devices = new InputDevice[] { keyboard, mouse };
            InputSystem.Update();
            Press();
            game.player.view.bobEnabled = false;
            game.player.view.swayEnabled = false;
            // Only the scene instance changes. StoryBook caption timings and all assets remain untouched.
            flow.driveDuration = 1.5f;
            yield return null;
            yield return null;
        }

        void RedirectInitialSave(Scene loaded, LoadSceneMode mode)
        {
            // Also protect a mistakenly auto-loaded next scene when diagnosing a transition regression.
            foreach (var system in loaded.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SaveSystem>(true)))
                system.fileName = testSaveName;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= RedirectInitialSave;
            Time.timeScale = 1;
            AudioListener.pause = false;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = originalBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
            Application.runInBackground = originalRunInBackground;
            var gameplayScenes = UnityEngine.Object.FindObjectsByType<GameSession>(FindObjectsInactive.Include)
                .Select(session => session.gameObject.scene).Where(scene => scene.IsValid() && scene.isLoaded).Distinct().ToArray();
            var blank = SceneManager.GetSceneByName("Road-journey-scene-cleanup");
            if (!blank.IsValid() || !blank.isLoaded) blank = SceneManager.CreateScene("Road-journey-scene-cleanup");
            SceneManager.SetActiveScene(blank);
            // Unload before restoring user data; deferred narrative saves cannot write afterward.
            foreach (var loaded in gameplayScenes)
                if (loaded.isLoaded && loaded != blank) yield return SceneManager.UnloadSceneAsync(loaded);
            RestoreFile(RealSavePath, save);
            RestoreFile(RealSavePath + ".bak", backup);
            RestoreFile(RealSavePath + ".tmp", temporary);
            if (!string.IsNullOrEmpty(testSaveName))
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                {
                    string path = Path.Combine(Application.persistentDataPath, testSaveName + suffix);
                    if (File.Exists(path)) File.Delete(path);
                }
            if (hadSaveScene) PlayerPrefs.SetString("Game.SaveScene", savedScene); else PlayerPrefs.DeleteKey("Game.SaveScene");
            if (hadResume) PlayerPrefs.SetInt("Game.ResumeRequested", resume); else PlayerPrefs.DeleteKey("Game.ResumeRequested");
            if (hadCarry) PlayerPrefs.SetInt("Game.StoryCarryRequested", carry); else PlayerPrefs.DeleteKey("Game.StoryCarryRequested");
            PlayerPrefs.Save();
        }

        static byte[] ReadFile(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
        static void RestoreFile(string path, byte[] original)
        { if (original != null) File.WriteAllBytes(path, original); else if (File.Exists(path)) File.Delete(path); }
        void Press(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        IEnumerator ClickDoor()
        {
            yield return Until(() => ReferenceEquals(game.interaction.Target, flow.driverDoor));
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left, true));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
        }

        IEnumerator Until(Func<bool> condition, float seconds = 8)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            var session = game ? game : GameSession.Instance;
            string state = session && session.narrative ? session.narrative.NextSignal + ", state=" + session.State
                + ", control=" + session.CanControl + ", sequence=" + session.narrative.ActiveSequenceId : "initializing";
            string sight = flow && flow.playerCamera ? ", seated=" + flow.Seated + ", eye=" + flow.playerCamera.transform.position
                + ", forward=" + flow.playerCamera.transform.forward + ", window=" + flow.ObservesWindow() : "";
            Assert.That(condition(), Is.True, "Road scene condition timed out: " + state + sight);
        }

        void AimFromSeat(Vector3 target)
        {
            Vector3 direction = flow.carRoot.InverseTransformDirection(target - flow.seatedPose.position);
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            // Rotating the test anchor must never bypass the player's authored look limits.
            Assert.That(Mathf.Abs(yaw), Is.LessThanOrEqualTo(flow.seatedYawLimit), "Target is behind the driver's allowed view.");
            Assert.That(Mathf.Abs(pitch), Is.LessThanOrEqualTo(flow.seatedPitchLimit), "Target exceeds the seated pitch limit.");
            flow.seatedPose.localRotation = Quaternion.LookRotation(direction);
        }

        IEnumerator PressInteract(RoadJourneyInteraction target)
        {
            yield return Until(() => ReferenceEquals(game.interaction.Target, target), 4);
            Press(Key.E);
            yield return null;
            yield return null;
            Press();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RealRoadHasReachableCabinItemsWindowAndContinuousWalkToGate()
        {
            var cameraProbe=flow.gameObject.AddComponent<RoadSeatedCameraProbe>();cameraProbe.flow=flow;
            Assert.That(flow.Seated, Is.True);
            Assert.That(game.player.enabled || game.player.Controller.enabled, Is.False);
            Assert.That(game.player.IsHidden, Is.False);
            Assert.That(game.player.view.HasOverride, Is.True);
            Assert.That(flow.flashlightPickup.CanInteract(game.interaction), Is.False);
            Assert.That(flow.gate.CanInteract(game.interaction), Is.False);
            Vector3 driveStart = flow.driveStart.position;
            yield return Until(() => game.narrative.ActiveSequenceId == "road.context" && flow.Drive01 > .1f, 8);
            Assert.That(game.narrative.SequencePlaying, Is.True);
            Assert.That(Vector3.Distance(flow.carRoot.position, driveStart), Is.GreaterThan(1));
            Assert.That(flow.Seated && game.player.view.HasOverride && game.CanControl, Is.True);
            yield return Until(() => flow.IsRinging, 20);
            Assert.That(cameraProbe.movingFrames, Is.GreaterThan(3));
            Assert.That(cameraProbe.maximumError, Is.LessThan(.15f), "The moving car must carry the camera inside its cabin every frame.");
            Assert.That(Vector3.Distance(flow.carRoot.position, flow.driveEnd.position), Is.LessThan(.01f));
            Assert.That(flow.ringSound, Is.Not.Null);
            Assert.That(flow.receiverSound, Is.Not.Null);
            AimFromSeat(flow.telephone.transform.position);
            yield return PressInteract(flow.telephone);
            yield return Until(() => game.narrative.HasSequence("road.arrival-call") && game.State == GameState.Playing, 12);
            yield return null;
            Assert.That(flow.Seated && game.player.view.HasOverride, Is.True, "The call must not release the seat camera.");
            Assert.That(flow.windowVisual.activeSelf && flow.windowLight.enabled, Is.True);
            AimFromSeat(flow.windowAnchor.position);
            yield return Until(() => flow.ObservesWindow(), 4);
            yield return Until(() => game.narrative.HasSignal("road.window-lit"), 4);
            Assert.That(flow.flashlightPickup.CanInteract(game.interaction), Is.True);
            AimFromSeat(flow.flashlightPickup.transform.position);
            yield return PressInteract(flow.flashlightPickup);
            Assert.That(flow.Seated && !game.player.enabled && !game.player.Controller.enabled, Is.True,
                "Taking the flashlight must not automatically leave the car.");
            Assert.That(game.narrative.HasSignal("road.car-exited"), Is.False);
            Assert.That(flow.driverDoor.CanInteract(game.interaction), Is.True);
            Assert.That(flow.flashlight.IsOn, Is.True);
            Assert.That(game.save.Save(), Is.True);
            Assert.That(SaveSystem.TryRead(game.save.SavePath,out var seatedSave), Is.True);
            Assert.That(seatedSave.narrative.HasSignal("road.flashlight-taken"), Is.True);
            Assert.That(seatedSave.narrative.HasSignal("road.car-exited"), Is.False);
            AimFromSeat(flow.driverDoor.transform.position);
            Quaternion closedDoorPose = flow.driverDoor.transform.localRotation;
            yield return ClickDoor();
            yield return Until(() => !flow.Seated && game.player.enabled && game.player.Controller.enabled, 4);
            Assert.That(flow.driverDoor.transform.localRotation, Is.EqualTo(closedDoorPose), "A left click exits without opening the door.");
            Assert.That(game.player.transform.position.x, Is.EqualTo(flow.exitPose.position.x).Within(.01f));
            Assert.That(game.player.transform.position.z, Is.EqualTo(flow.exitPose.position.z).Within(.01f));
            Assert.That(game.narrative.HasSignal("road.car-exited"), Is.True);
            Assert.That(game.player.view.HasOverride, Is.False);
            Assert.That(flow.flashlight.IsOn && flow.flashlight.lamp.enabled, Is.True);
            Assert.That(flow.Interact(flow.gate), Is.False, "João cannot finish the chapter remotely from the parked car.");
            Vector3 exit = game.player.transform.position;
            Assert.That(exit.y, Is.InRange(-.15f, .2f), "The car still exits onto the flat approach before the real entrance steps.");
            Assert.That(Physics.Raycast(exit+Vector3.up, Vector3.back, out var rearLimit, 8,
                ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(rearLimit.collider.isTrigger, Is.False);
            Assert.That(rearLimit.collider.GetComponentsInChildren<Renderer>(), Is.Empty,
                "The route boundary behind the car must not show a wall.");
            Assert.That(Physics.Raycast(exit + Vector3.up, Vector3.down, out var support, 2,
                ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(support.normal.y, Is.GreaterThan(.8f));
            Vector3 toGate = flow.gate.transform.position - exit;
            float heading = Mathf.Atan2(toGate.x, toGate.z) * Mathf.Rad2Deg;
            // Change facing at the same position, then walk the complete supported route with W.
            game.player.Warp(exit, heading);
            Press(Key.W);
            // Editor background throttling can stretch wall time without changing the simulated walk.
            yield return Until(() => ReferenceEquals(game.interaction.Target, flow.gate), 60);
            Press();
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(game.player.transform.position, exit), Is.GreaterThan(10));
            // The rounded capsule can touch the next tread before its centre is
            // above it. Check support across its footprint, not just one ray.
            Assert.That(game.player.Grounded, Is.True);
            var capsule = game.player.GetComponent<CharacterController>();
            Vector3 foot = game.player.transform.position;
            float radius = capsule.radius * .8f;
            var footprint = new[] { Vector3.zero, Vector3.forward * radius, Vector3.back * radius,
                Vector3.left * radius, Vector3.right * radius };
            bool supportedAtGate = footprint.Any(offset =>
                Physics.Raycast(foot + offset + Vector3.up * .25f, Vector3.down,
                    out var hit, .6f, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore)
                && hit.normal.y > .95f && foot.y - hit.point.y >= -.08f && foot.y - hit.point.y <= .10f);
            Assert.That(supportedAtGate, Is.True, "A tread must support the grounded capsule footprint near the gate.");
            // The refined arrival uses the literal school's four entrance treads.
            // Prove the additional rise by input from the supported gate approach.
            var copiedEntrance = SceneManager.GetActiveScene().GetRootGameObjects()
                .Single(root => root.name == "Chegada — entrada literal da escola");
            var entranceTreads = copiedEntrance.GetComponentsInChildren<BoxCollider>(true)
                .Where(c => c.name.StartsWith("Entrada_Hall — degrau ", StringComparison.Ordinal))
                .OrderBy(c => c.bounds.center.z).ToArray();
            Assert.That(entranceTreads.Length, Is.EqualTo(4));
            Assert.That(entranceTreads.Last().bounds.max.y, Is.EqualTo(flow.gate.transform.position.y).Within(.003f));
            for (int i = 1; i < entranceTreads.Length; i++)
            {
                Assert.That(entranceTreads[i].bounds.min.z, Is.EqualTo(entranceTreads[i - 1].bounds.max.z).Within(.004f));
                Assert.That(entranceTreads[i].bounds.max.y - entranceTreads[i - 1].bounds.max.y, Is.InRange(.14f, .17f));
            }
            Vector3 stairApproach = game.player.transform.position;
            Press(Key.W);
            yield return Until(() => game.player.Grounded && game.player.transform.position.y >= flow.gate.transform.position.y - .065f
                && game.player.transform.position.z > entranceTreads.Last().bounds.min.z + .005f, 20);
            Press();
            yield return null;
            Assert.That(game.player.transform.position.z, Is.GreaterThan(stairApproach.z + .03f));
            Assert.That(game.player.transform.position.y, Is.EqualTo(entranceTreads.Last().bounds.max.y).Within(.07f));
            Assert.That(game.player.IsCrouched, Is.False, "The copied portal must retain standing headroom.");
            Assert.That(ReferenceEquals(game.interaction.Target, flow.gate), Is.True);
            yield return PressInteract(flow.gate);
            yield return Until(() => game.State == GameState.StoryScreen, 4);
            var view = game.narrative.transitionView;
            yield return Until(() => view && view.returnButton && view.returnButton.gameObject.activeSelf, 10);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("RoadJourney"), "This chapter must stop for review, not load the ready school.");
            Assert.That(game.narrative.Progress.pendingChapterId, Is.EqualTo("school"));
            Assert.That(game.narrative.Progress.milestones.Count(id => id.StartsWith("road.")), Is.EqualTo(7));
            Assert.That(view.heading.text, Is.EqualTo("CAPÍTULO 2 CONCLUÍDO"));
            Assert.That(view.body.text, Does.Contain("CAPÍTULO 3").And.Contain("A Biblioteca"));
            Assert.That(game.save.Save(), Is.True);
            Assert.That(SaveSystem.TryRead(game.save.SavePath, out var completed), Is.True);
            Assert.That(completed.scene, Is.EqualTo("RoadJourney"));
            Assert.That(completed.narrative.HasSignal("road.school-gate"), Is.True);
            Assert.That(completed.narrative.completedChapters, Does.Contain("road"));
            Assert.That(completed.narrative.pendingChapterId, Is.EqualTo("school"));
            game.narrative.autoLoadNextChapter = true;
            game.ResumeStoryChapter();
            Assert.That(game.narrative.TryAdvanceChapter(), Is.True);
            yield return Until(() => SceneManager.GetActiveScene().name == "SchoolCEEP" && GameSession.Instance
                && GameSession.Instance.narrative.IsInitialized, 20);
            game = GameSession.Instance;
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo("school"));
            Assert.That(game.narrative.book.chapters.Length, Is.EqualTo(6));
            Assert.That(game.narrative.HasSignal("road.school-gate"), Is.True);
            Assert.That(game.narrative.NextSignal, Is.EqualTo("school.entered"));
            Assert.That(game.save.flashlight.BatterySeconds, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator CompletedOfficeCheckpointCarriesEquipmentIntoRealRoadSpawn()
        {
            // Model the office's completed save without loading or modifying the ready office scene.
            var book = game.narrative.book;
            var progress = new NarrativeProgress { chapterId = "prologue" };
            foreach (var milestone in book.Chapter("prologue").milestones)
                Assert.That(progress.TryComplete(book, milestone.id, item => item == "office_equipment"), Is.True);
            progress.CompleteSequence("prologue.call-one");
            progress.CompleteSequence("prologue.call-two");
            Assert.That(progress.CompleteChapter(book), Is.True);
            Assert.That(progress.pendingChapterId, Is.EqualTo("road"));
            Assert.That(book.Chapter("road").sceneName, Is.EqualTo("RoadJourney"));
            Vector3 oldOfficePosition = Vector3.one * 1000;
            var officeSave = new SaveData
            {
                scene = "Chapter1Office",
                position = oldOfficePosition,
                heading = 123,
                pitch = 31,
                stamina = 1,
                battery = 55,
                flashlightOn = false,
                crouched = true,
                tension = .15f,
                inventory = new[] { new InventoryItem { id = "office_equipment", title = "Equipamentos de investigação" } }.ToList(),
                narrative = progress
            };
            // Stop pending saves in the old runtime before replacing this disposable checkpoint.
            game.Pause();
            flow.enabled = false;
            game.narrative.enabled = false;
            game.enabled = false;
            File.WriteAllText(game.save.SavePath, JsonUtility.ToJson(officeSave, true));
            PlayerPrefs.SetInt("Game.ResumeRequested", 0);
            PlayerPrefs.SetInt("Game.StoryCarryRequested", 1);
            PlayerPrefs.Save();
            yield return SceneManager.LoadSceneAsync("RoadJourney");
            yield return Until(() => GameSession.Instance && GameSession.Instance.narrative
                && GameSession.Instance.narrative.IsInitialized, 12);
            game = GameSession.Instance;
            flow = UnityEngine.Object.FindAnyObjectByType<RoadJourneyFlow>();
            Assert.That(flow, Is.Not.Null);
            game.input.Actions.devices = new InputDevice[] { keyboard };
            InputSystem.Update();
            Press();
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("RoadJourney"));
            Assert.That(game.save.fileName, Is.EqualTo(testSaveName));
            Assert.That(game.narrative.book.Chapter("road").sceneName, Is.EqualTo("RoadJourney"));
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo("road"));
            Assert.That(game.narrative.Progress.pendingChapterId, Is.Empty);
            Assert.That(game.narrative.HasSignal("prologue.exit"), Is.True);
            Assert.That(game.narrative.Progress.completedChapters, Does.Contain("prologue"));
            Assert.That(game.interaction.inventory.Contains("office_equipment"), Is.True);
            Assert.That(game.narrative.NextSignal, Is.EqualTo("road.context-shown").Or.EqualTo("road.car-parked"));
            Assert.That(flow.Seated && game.player.view.HasOverride, Is.True);
            Assert.That(game.player.enabled || game.player.Controller.enabled, Is.False);
            Assert.That(Vector3.Distance(game.player.transform.position, oldOfficePosition), Is.GreaterThan(1000),
                "Chapter carry must use the new road's spawn rather than the previous map's transform.");
            Assert.That(Vector3.Distance(game.player.transform.position, flow.seatedPose.position - Vector3.up * game.player.EyeHeight),
                Is.LessThan(.1f));
            Assert.That(flow.flashlight.BatterySeconds, Is.EqualTo(55).Within(.01f));
            Assert.That(PlayerPrefs.GetInt("Game.ResumeRequested", 0), Is.Zero);
            Assert.That(PlayerPrefs.GetInt("Game.StoryCarryRequested", 0), Is.Zero);
            Assert.That(SaveSystem.TryRead(game.save.SavePath, out var carriedSave), Is.True);
            Assert.That(carriedSave.scene, Is.EqualTo("RoadJourney"));
            Assert.That(carriedSave.narrative.chapterId, Is.EqualTo("road"));
            Assert.That(carriedSave.inventory.Any(item => item.id == "office_equipment"), Is.True);
        }
    }
}
