// Verifica SetUp, TearDown, JourneyUsesPhysicalPhoneFlashlightAndSeparateDoorThenEndsAtGate, MovingSeatCameraTracksEveryLateFrameWithoutWorldLag, RadioCaptionsDoNotStopTheCarOrSeatedCamera, DrivingAndWindowTimersFreezeWhilePaused, WindowRequiresClearSightAndCannotSkipArrivalCall, SeatedCheckpointIsWritableAndRestoresParkedProgressWithoutReplayingEvents, RestoredWalkKeepsSavedPositionAndDoesNotDragPlayerBackToTheCar, HeldFlashlightCheckpointStaysSeatedUntilDoorInteraction, LegacyWalkingCheckpointAddsExitSilentlyAndKeepsOutsidePosition, LegacyCompletedChapterRemainsCompleteWithNewDoorMilestone.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("RoadJourney")]
    public sealed class RoadJourneyFlowTests
    {
        Scene scene;
        GameObject root;
        StoryBook book;
        GameSession game;
        NarrativeDirector director;
        FirstPersonMotor motor;
        PlayerInteraction interaction;
        Inventory inventory;
        RoadJourneyFlow flow;
        SaveSystem save;
        string disposableSave;
        bool hadSavePreference;
        string savedPreference;
        int parkedEvents, answeredEvents, windowEvents, exitedEvents;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hadSavePreference = PlayerPrefs.HasKey("Game.SaveScene");
            savedPreference = PlayerPrefs.GetString("Game.SaveScene", "");
            scene = SceneManager.CreateScene("Road journey fixture " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(scene);
            foreach (var existing in UnityEngine.Object.FindObjectsByType<GameSession>())
                UnityEngine.Object.Destroy(existing.gameObject);
            yield return null;
            root = new GameObject("Road journey isolated fixture");
            root.SetActive(false);
            game = root.AddComponent<GameSession>();
            game.enabled = false; // Never run Start or load an external scene/checkpoint.
            inventory = root.AddComponent<Inventory>();
            director = root.AddComponent<NarrativeDirector>();
            director.game = game;
            director.inventory = inventory;
            director.startingChapterId = "road";
            game.narrative = director;
            book = ScriptableObject.CreateInstance<StoryBook>();
            StoryContent.Populate(book);
            if (book.Sequence("road.context") == null)
                book.sequences = book.sequences.Concat(new[] { new StorySequenceDefinition {
                    id = "road.context", completionSignal = "road.context-shown", lockPlayer = false,
                    lines = new[] { new StoryLine { speaker = "Rádio", text = "Os desaparecimentos continuam sem explicação.", seconds = .12f } }
                } }).ToArray();
            foreach (var sequence in book.sequences)
                foreach (var line in sequence.lines) line.seconds = .12f;
            var road = book.Chapter("road");
            if (!road.milestones.Any(milestone => milestone.id == "road.car-exited"))
                road.milestones = road.milestones.SelectMany(milestone => milestone.id == "road.school-gate"
                    ? new[] { new StoryMilestoneDefinition { id = "road.car-exited", objectiveAfter = "Caminhar até a entrada da escola." }, milestone }
                    : new[] { milestone }).ToArray();
            // No integrated scene is loaded by this isolated fixture.
            book.Chapter("school").sceneName = "";
            director.book = book;

            var playerObject = Child("Player", Vector3.zero);
            var controller = playerObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = .32f;
            controller.center = Vector3.up * .9f;
            motor = playerObject.AddComponent<FirstPersonMotor>();
            var cameraObject = Child("Camera", Vector3.up * 1.64f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            var view = cameraObject.AddComponent<FirstPersonCamera>();
            view.motor = motor;
            view.playerCamera = camera;
            motor.view = view;
            interaction = playerObject.AddComponent<PlayerInteraction>();
            interaction.view = camera;
            interaction.motor = motor;
            interaction.inventory = inventory;
            game.player = motor;
            game.interaction = interaction;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Supported road and pedestrian area";
            floor.transform.SetParent(root.transform);
            floor.transform.position = new Vector3(0, -.1f, 12);
            floor.transform.localScale = new Vector3(20, .2f, 40);
            flow = root.AddComponent<RoadJourneyFlow>();
            flow.director = director;
            flow.player = motor;
            flow.playerCamera = camera;
            flow.carRoot = Child("Car", Vector3.zero).transform;
            var shell = flow.carRoot.gameObject.AddComponent<BoxCollider>();
            shell.center = Vector3.up * .82f;
            shell.size = new Vector3(1.8f, 1.64f, 3.95f);
            flow.driveStart = Child("Drive start", Vector3.zero).transform;
            flow.driveEnd = Child("Drive end", Vector3.forward * 12).transform;
            flow.seatedPose = Child("Seat eye pose", new Vector3(0, 1.1f, 0)).transform;
            flow.seatedPose.SetParent(flow.carRoot, true);
            flow.exitPose = Child("Safe exit feet pose", new Vector3(-2, .04f, 12)).transform;
            flow.windowAnchor = Child("School window", new Vector3(0, 2, 22)).transform;
            flow.windowVisual = Child("Window glow", flow.windowAnchor.position);
            flow.windowLight = flow.windowVisual.AddComponent<Light>();
            flow.driveDuration = 1;
            flow.windowObservationSeconds = .12f;
            flow.windowLightSeconds = .3f;
            flow.telephone = Interaction("Phone", RoadJourneyInteractionKind.AnswerPhone, new Vector3(.1f, .9f, .65f));
            flow.telephone.transform.SetParent(flow.carRoot, true);
            flow.flashlightPickup = Interaction("Dashboard flashlight", RoadJourneyInteractionKind.TakeFlashlight,
                new Vector3(-.3f, .85f, .7f));
            flow.flashlightPickup.transform.SetParent(flow.carRoot, true);
            flow.driverDoor = Interaction("Driver door handle", RoadJourneyInteractionKind.ExitCar, new Vector3(-.75f, .8f, .2f));
            flow.driverDoor.transform.SetParent(flow.carRoot, true);
            flow.gate = Interaction("Gate", RoadJourneyInteractionKind.SchoolGate, new Vector3(0, 1, 15));
            var flashlightObject = Child("Player flashlight", Vector3.zero);
            var flashlight = flashlightObject.AddComponent<FlashlightController>();
            flashlight.startsOn = false;
            flashlight.view = camera;
            flashlight.lamp = flashlightObject.AddComponent<Light>();
            flashlight.handRig = Child("Hand rig", Vector3.zero).transform;
            flow.flashlight = flashlight;
            save = root.AddComponent<SaveSystem>();
            save.enabled = false;
            save.player = motor;
            save.inventory = inventory;
            save.flashlight = flashlight;
            save.narrative = director;
            // The only checkpoint written here has a disposable unique filename.
            save.fileName = "road-flow-test-" + Guid.NewGuid().ToString("N") + ".json";
            disposableSave = save.SavePath;
            parkedEvents = answeredEvents = windowEvents = exitedEvents = 0;
            flow.onParked.AddListener(() => parkedEvents++);
            flow.onCallAnswered.AddListener(() => answeredEvents++);
            flow.onWindowSeen.AddListener(() => windowEvents++);
            flow.onExitedCar.AddListener(() => exitedEvents++);
            root.SetActive(true);
            director.InitializeAfterSession(false);
            Physics.SyncTransforms();
            yield return null;
        }

        GameObject Child(string name, Vector3 position)
        {
            var result = new GameObject(name);
            result.transform.SetParent(root.transform);
            result.transform.position = position;
            return result;
        }

        RoadJourneyInteraction Interaction(string name, RoadJourneyInteractionKind kind, Vector3 position)
        {
            var result = Child(name, position);
            var collider = result.AddComponent<BoxCollider>();
            collider.size = Vector3.one * .25f;
            var interactable = result.AddComponent<RoadJourneyInteraction>();
            interactable.flow = flow;
            interactable.kind = kind;
            return interactable;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            if (root) UnityEngine.Object.Destroy(root);
            if (book) UnityEngine.Object.Destroy(book);
            yield return null;
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (!string.IsNullOrEmpty(disposableSave) && File.Exists(disposableSave + suffix))
                    File.Delete(disposableSave + suffix);
            if (hadSavePreference) PlayerPrefs.SetString("Game.SaveScene", savedPreference);
            else PlayerPrefs.DeleteKey("Game.SaveScene");
            PlayerPrefs.Save();
            var cleanup = SceneManager.GetSceneByName("Road journey test cleanup");
            if (!cleanup.IsValid() || !cleanup.isLoaded) cleanup = SceneManager.CreateScene("Road journey test cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        IEnumerator Until(Func<bool> condition, float seconds = 4)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Road condition timed out. Next=" + director.NextSignal + ", state=" + game.State);
        }

        IEnumerator AimAndInteract(RoadJourneyInteraction target)
        {
            flow.seatedPose.rotation = Quaternion.LookRotation(target.transform.position - flow.seatedPose.position);
            yield return Until(() => ReferenceEquals(interaction.Target, target));
            interaction.Target.Interact(interaction);
        }

        void RestoreThrough(string finalSignal, bool keepPlayerPosition = false)
        {
            var progress = new NarrativeProgress { chapterId = "road" };
            foreach (var milestone in book.Chapter("road").milestones)
            {
                Assert.That(progress.TryComplete(book, milestone.id, inventory.Contains), Is.True);
                foreach (var sequence in book.sequences)
                    if (sequence.completionSignal == milestone.id) progress.CompleteSequence(sequence.id);
                if (milestone.id == finalSignal) break;
            }
            if (!keepPlayerPosition && progress.HasSignal("road.flashlight-taken") && !progress.HasSignal("road.car-exited"))
            {
                flow.carRoot.position = flow.driveEnd.position;
                motor.Warp(flow.seatedPose.position - Vector3.up * motor.EyeHeight, 0);
            }
            director.RestoreProgress(progress);
            director.InitializeAfterSession(true);
        }

        [UnityTest]
        public IEnumerator JourneyUsesPhysicalPhoneFlashlightAndSeparateDoorThenEndsAtGate()
        {
            Assert.That(flow.Seated, Is.True);
            Assert.That(motor.IsHidden, Is.False);
            Assert.That(motor.Controller.enabled, Is.False);
            Assert.That(flow.flashlightPickup.CanInteract(interaction), Is.False);
            Assert.That(flow.flashlightPickup.GetComponent<Collider>().enabled, Is.True, "The uncollected flashlight stays visible and physically present on the seat.");
            Assert.That(flow.driverDoor.CanInteract(interaction), Is.False);
            Assert.That(flow.gate.CanInteract(interaction), Is.False);
            yield return Until(() => flow.IsRinging);
            Assert.That(flow.carRoot.position, Is.EqualTo(flow.driveEnd.position));
            Assert.That(parkedEvents, Is.EqualTo(1));
            yield return AimAndInteract(flow.telephone);
            Assert.That(answeredEvents, Is.EqualTo(1));
            yield return Until(() => director.HasSequence("road.arrival-call") && game.State == GameState.Playing);
            yield return null;
            Assert.That(motor.view.HasOverride, Is.True, "Finishing a locked caption must retain the seat camera until João exits.");
            flow.seatedPose.rotation = Quaternion.LookRotation(flow.windowAnchor.position - flow.seatedPose.position);
            yield return Until(() => director.HasSignal("road.window-lit"));
            Assert.That(windowEvents, Is.EqualTo(1));
            Vector3 seatedBody = motor.transform.position;
            yield return AimAndInteract(flow.flashlightPickup);
            Assert.That(flow.Seated, Is.True, "Collecting the flashlight must not eject João.");
            Assert.That(motor.Controller.enabled || motor.enabled, Is.False);
            Assert.That(motor.transform.position, Is.EqualTo(seatedBody));
            Assert.That(flow.flashlight.IsOn, Is.True);
            Assert.That(exitedEvents, Is.Zero);
            Assert.That(director.NextSignal, Is.EqualTo("road.car-exited"));
            Assert.That(flow.flashlightPickup.GetComponent<Collider>().enabled, Is.False);
            Quaternion closedDoorPose = flow.driverDoor.transform.localRotation;
            yield return AimAndInteract(flow.driverDoor);
            Assert.That(flow.Seated, Is.False);
            Assert.That(motor.Controller.enabled && motor.enabled, Is.True);
            Assert.That(flow.flashlight.IsOn, Is.True);
            Assert.That(exitedEvents, Is.EqualTo(1));
            Assert.That(flow.Interact(flow.gate), Is.False, "The gate cannot be completed remotely from the car exit.");
            motor.Warp(new Vector3(0, .04f, 13.2f), 0, 20);
            yield return Until(() => ReferenceEquals(interaction.Target, flow.gate));
            interaction.Target.Interact(interaction);
            yield return Until(() => game.State == GameState.StoryScreen);
            Assert.That(flow.driverDoor.transform.localRotation, Is.EqualTo(closedDoorPose), "Exiting must not animate the door.");
            Assert.That(director.Progress.milestones.Count, Is.EqualTo(7));
            Assert.That(director.Progress.pendingChapterId, Is.EqualTo("school"));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            Assert.That(flow.Interact(flow.gate), Is.False);
        }

        [UnityTest]
        public IEnumerator MovingSeatCameraTracksEveryLateFrameWithoutWorldLag()
        {
            var probe = root.AddComponent<RoadSeatFrameProbe>();
            probe.flow = flow;
            yield return Until(() => flow.Drive01 >= 1 && probe.samples > 12);
            Assert.That(probe.maxPositionError, Is.LessThan(.15f), "The camera must remain mounted during the fast middle of the drive.");
            Assert.That(probe.maxPositionErrorWhileFast, Is.LessThan(.15f));
            Assert.That(probe.fastSamples, Is.GreaterThan(3));
            Assert.That(motor.view.HasOverride && flow.Seated, Is.True);
        }

        [UnityTest]
        public IEnumerator RadioCaptionsDoNotStopTheCarOrSeatedCamera()
        {
            book.Sequence("road.context").lines[0].seconds = .8f;
            director.RestoreProgress(new NarrativeProgress { chapterId = "road" });
            director.InitializeAfterSession(true);
            yield return Until(() => director.ActiveSequenceId == "road.context" && flow.Drive01 > .12f);
            Assert.That(director.SequencePlaying, Is.True);
            Assert.That(director.NextSignal, Is.EqualTo("road.context-shown"));
            Assert.That(flow.carRoot.position.z, Is.GreaterThan(flow.driveStart.position.z));
            Assert.That(flow.Seated && motor.view.HasOverride, Is.True);
            Assert.That(game.CanControl, Is.True, "Nonlocking radio captions keep seated look input available.");
            Assert.That(flow.telephone.CanInteract(interaction), Is.False);
            yield return Until(() => flow.IsRinging);
            Assert.That(parkedEvents, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DrivingAndWindowTimersFreezeWhilePaused()
        {
            yield return Until(() => director.HasSignal("road.context-shown") && flow.Drive01 > .1f);
            game.Pause();
            Vector3 position = flow.carRoot.position;
            float progress = flow.Drive01;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(flow.carRoot.position, Is.EqualTo(position));
            Assert.That(flow.Drive01, Is.EqualTo(progress));
            Assert.That(flow.Interact(flow.telephone), Is.False);
            game.Resume();
            yield return Until(() => flow.IsRinging);
            RestoreThrough("road.arrival-call");
            flow.seatedPose.rotation = Quaternion.LookRotation(flow.windowAnchor.position - flow.seatedPose.position);
            game.Pause();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(director.HasSignal("road.window-lit"), Is.False);
            game.Resume();
            yield return Until(() => director.HasSignal("road.window-lit"));
            Assert.That(windowEvents, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WindowRequiresClearSightAndCannotSkipArrivalCall()
        {
            RestoreThrough("road.car-parked");
            flow.seatedPose.rotation = Quaternion.LookRotation(flow.windowAnchor.position - flow.seatedPose.position);
            yield return new WaitForSeconds(.25f);
            Assert.That(director.HasSignal("road.window-lit"), Is.False);
            RestoreThrough("road.arrival-call");
            flow.seatedPose.rotation = Quaternion.LookRotation(flow.windowAnchor.position - flow.seatedPose.position);
            var blocker = Child("Opaque wall between João and the window", new Vector3(0, 2, 17));
            blocker.AddComponent<BoxCollider>().size = new Vector3(4, 4, .2f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.25f);
            Assert.That(flow.ObservesWindow(), Is.False);
            Assert.That(director.HasSignal("road.window-lit"), Is.False);
            UnityEngine.Object.Destroy(blocker);
            yield return Until(() => director.HasSignal("road.window-lit"));
        }

        [UnityTest]
        public IEnumerator SeatedCheckpointIsWritableAndRestoresParkedProgressWithoutReplayingEvents()
        {
            RestoreThrough("road.window-lit");
            Assert.That(save.Save(), Is.True, "Sitting in the car must not use the hidden-player save prohibition.");
            Assert.That(SaveSystem.TryRead(disposableSave, out var data), Is.True);
            Assert.That(data.narrative.chapterId, Is.EqualTo("road"));
            Assert.That(data.narrative.HasSignal("road.window-lit"), Is.True);
            Assert.That(data.flashlightOn, Is.False);
            var snapshot = director.CaptureProgress();
            flow.carRoot.position = Vector3.zero;
            director.RestoreProgress(snapshot);
            director.InitializeAfterSession(true);
            yield return null;
            Assert.That(flow.carRoot.position, Is.EqualTo(flow.driveEnd.position));
            Assert.That(flow.Seated, Is.True);
            Assert.That(flow.flashlightPickup.CanInteract(interaction), Is.True);
            Assert.That(flow.windowLight.enabled, Is.False, "An already seen window event is not replayed on reload.");
            Assert.That(parkedEvents + answeredEvents + windowEvents + exitedEvents, Is.Zero);
            Assert.That(flow.telephone.CanInteract(interaction), Is.False);
        }

        [UnityTest]
        public IEnumerator RestoredWalkKeepsSavedPositionAndDoesNotDragPlayerBackToTheCar()
        {
            motor.Warp(new Vector3(1, .04f, 14), 32, 0);
            Vector3 savedPosition = motor.transform.position;
            RestoreThrough("road.car-exited", true);
            Assert.That(flow.Seated, Is.False);
            Assert.That(motor.enabled && motor.Controller.enabled, Is.True);
            Assert.That(motor.transform.position, Is.EqualTo(savedPosition));
            Assert.That(flow.flashlightPickup.CanInteract(interaction), Is.False);
            Assert.That(motor.view.HasOverride, Is.False);
            flow.enabled = false;
            yield return null;
            Assert.That(motor.enabled && motor.Controller.enabled, Is.True);
            Assert.That(exitedEvents, Is.Zero);
        }

        [UnityTest]
        public IEnumerator HeldFlashlightCheckpointStaysSeatedUntilDoorInteraction()
        {
            RestoreThrough("road.window-lit");
            yield return null;
            Assert.That(flow.Interact(flow.flashlightPickup), Is.True);
            Assert.That(flow.Seated && motor.view.HasOverride, Is.True);
            Assert.That(flow.flashlight.IsOn, Is.True);
            Assert.That(save.Save(), Is.True);
            Assert.That(SaveSystem.TryRead(disposableSave, out var data), Is.True);
            Assert.That(data.narrative.HasSignal("road.flashlight-taken"), Is.True);
            Assert.That(data.narrative.HasSignal("road.car-exited"), Is.False);
            Assert.That(data.flashlightOn, Is.True);
            director.RestoreProgress(data.narrative);
            motor.Warp(data.position, data.heading, data.pitch);
            director.InitializeAfterSession(true);
            yield return null;
            Assert.That(flow.Seated && motor.view.HasOverride, Is.True);
            Assert.That(motor.enabled || motor.Controller.enabled, Is.False);
            Assert.That(flow.driverDoor.CanInteract(interaction), Is.True);
            Assert.That(flow.gate.CanInteract(interaction), Is.False);
            Assert.That(exitedEvents, Is.Zero);
            Assert.That(flow.Interact(flow.driverDoor), Is.True);
            Assert.That(motor.transform.position, Is.EqualTo(flow.exitPose.position), "The door interaction must teleport directly to the exit point.");
            Assert.That(flow.Seated, Is.False);
            Assert.That(director.HasSignal("road.car-exited"), Is.True);
            Assert.That(exitedEvents, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LegacyWalkingCheckpointAddsExitSilentlyAndKeepsOutsidePosition()
        {
            motor.Warp(new Vector3(-2, .04f, 12), 25);
            Vector3 outside = motor.transform.position;
            RestoreThrough("road.flashlight-taken", true);
            yield return null;
            Assert.That(director.HasSignal("road.car-exited"), Is.True);
            Assert.That(director.NextSignal, Is.EqualTo("road.school-gate"));
            Assert.That(flow.Seated || motor.view.HasOverride, Is.False);
            Assert.That(motor.transform.position.x, Is.EqualTo(outside.x).Within(.01f));
            Assert.That(motor.transform.position.z, Is.EqualTo(outside.z).Within(.01f));
            Assert.That(flow.driverDoor.CanInteract(interaction), Is.False);
            Assert.That(flow.gate.CanInteract(interaction), Is.True);
            Assert.That(parkedEvents + answeredEvents + windowEvents + exitedEvents, Is.Zero);
            Quaternion closedPose = flow.driverDoor.transform.localRotation;
            flow.enabled = false;
            flow.enabled = true;
            Assert.That(flow.driverDoor.transform.localRotation, Is.EqualTo(closedPose), "Reloading must keep the static door pose.");
        }

        [UnityTest]
        public IEnumerator LegacyCompletedChapterRemainsCompleteWithNewDoorMilestone()
        {
            RestoreThrough("road.window-lit");
            var legacy = director.CaptureProgress();
            Assert.That(legacy.TryComplete(book, "road.flashlight-taken", inventory.Contains), Is.True);
            legacy.milestones.Add("road.school-gate"); // Serialized before car-exited existed.
            legacy.completedChapters.Add("road");
            legacy.pendingChapterId = "school";
            motor.Warp(new Vector3(0, .04f, 13.2f), 0);
            director.RestoreProgress(legacy);
            director.InitializeAfterSession(true);
            yield return Until(() => game.State == GameState.StoryScreen);
            Assert.That(director.HasSignal("road.car-exited"), Is.True);
            Assert.That(director.Progress.IsChapterComplete(book), Is.True);
            Assert.That(director.Progress.pendingChapterId, Is.EqualTo("school"));
            Assert.That(flow.Seated || motor.view.HasOverride, Is.False);
            Assert.That(parkedEvents + answeredEvents + windowEvents + exitedEvents, Is.Zero);
        }
    }

    // Sampling after the camera's LateUpdate catches the previous world-space smoothing bug.
    [DefaultExecutionOrder(999)]
    public sealed class RoadSeatFrameProbe : MonoBehaviour
    {
        public RoadJourneyFlow flow;
        public int samples, fastSamples;
        public float maxPositionError, maxPositionErrorWhileFast;
        void LateUpdate()
        {
            if (!flow || !flow.Seated || !flow.playerCamera || !flow.seatedPose) return;
            float error = Vector3.Distance(flow.playerCamera.transform.position, flow.seatedPose.position);
            samples++;
            maxPositionError = Mathf.Max(maxPositionError, error);
            if (flow.Drive01 > .25f && flow.Drive01 < .75f)
            {
                fastSamples++;
                maxPositionErrorWhileFast = Mathf.Max(maxPositionErrorWhileFast, error);
            }
        }
    }
}
