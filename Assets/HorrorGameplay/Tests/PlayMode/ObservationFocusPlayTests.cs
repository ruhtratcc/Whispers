// Verifica SetUp, ZoomFramingIsNowTheDefaultWithOrWithoutObservation, ZoomFramingStaysConsistentInCarHidingSprintInspectionAndPause, ExistingFocusBindingsArePreservedInsteadOfAddingAnotherAction, TearDown.

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("ReferencePresentation")]
    public sealed class ObservationFocusPlayTests
    {
        GameObject root;
        InputActionAsset sourceAsset;
        InputReader input;
        FirstPersonMotor motor;
        FirstPersonCamera view;
        Camera camera;
        GameSession game;
        Keyboard keyboard;
        float previousScale;
        bool previousPause, previousRunInBackground, previousCursorVisible;
        CursorLockMode previousCursorLock;
        InputSettings.BackgroundBehavior previousBackground;
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousScale = Time.timeScale; previousPause = AudioListener.pause;
            previousCursorLock = Cursor.lockState; previousCursorVisible = Cursor.visible;
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousRunInBackground = Application.runInBackground;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true;
            Assert.That(GameSession.Instance, Is.Null, "This fixture must run without a live campaign session.");
            sourceAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            sourceAsset.AddActionMap(new InputActionMap("Gameplay"));
            sourceAsset.AddActionMap(new InputActionMap("Always"));
            sourceAsset.AddActionMap(new InputActionMap("UI"));
            root = new GameObject("Observation focus fixture"); root.SetActive(false);
            root.transform.position = Vector3.one * 2100;
            input = root.AddComponent<InputReader>(); input.asset = sourceAsset;
            motor = root.AddComponent<FirstPersonMotor>(); motor.enabled = false; motor.input = input;
            game = root.AddComponent<GameSession>(); game.enabled = false; game.input = input; game.player = motor;
            var cameraObject = new GameObject("Observation camera"); cameraObject.transform.SetParent(root.transform, false);
            camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.fieldOfView = 49;
            view = cameraObject.AddComponent<FirstPersonCamera>(); view.motor = motor; view.playerCamera = camera;
            view.bobEnabled = view.swayEnabled = false; motor.view = view;
            root.SetActive(true);
            // Awake owns the clone; skip Start's unrelated UI module wiring in this isolated fixture.
            input.enabled = false;
            keyboard = InputSystem.AddDevice<Keyboard>(); input.Actions.devices = new InputDevice[] { keyboard };
            SetKeys();
            yield return null;
        }

        void SetKeys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        [UnityTest]
        public IEnumerator ZoomFramingIsNowTheDefaultWithOrWithoutObservation()
        {
            Assert.That(sourceAsset.FindAction("Gameplay/Focus"), Is.Null, "The authored input asset stays unchanged.");
            Assert.That(input.Actions.FindAction("Gameplay/Focus").bindings[0].path, Is.EqualTo("<Keyboard>/z"));
            SetKeys(Key.Z);
            Assert.That(input.Focus, Is.True);
            yield return null;
            Assert.That(camera.fieldOfView, Is.EqualTo(49).Within(.01f));
            yield return new WaitForSeconds(1);
            Assert.That(camera.fieldOfView, Is.EqualTo(49).Within(.01f));
            SetKeys(); yield return new WaitForSeconds(.9f);
            Assert.That(camera.fieldOfView, Is.EqualTo(49).Within(.01f));
            SetKeys(Key.Z); yield return new WaitForSeconds(.9f);
            input.EnableGameplay(false);
            Assert.That(input.Focus, Is.False);
            yield return new WaitForSeconds(.9f);
            Assert.That(camera.fieldOfView, Is.EqualTo(49).Within(.01f));
            SetKeys(); input.EnableGameplay(true); yield return null;
            Assert.That(input.Focus, Is.False, "Re-enabling controls must not latch the previous hold.");
        }

        [UnityTest]
        public IEnumerator ZoomFramingStaysConsistentInCarHidingSprintInspectionAndPause()
        {
            SetKeys(Key.Z); yield return new WaitForSeconds(1);
            Assert.That(camera.fieldOfView, Is.EqualTo(49).Within(.01f));
            var pose = new GameObject("Car seat override"); pose.transform.SetParent(root.transform, false);
            view.OverridePose(pose.transform, true); yield return new WaitForSeconds(.9f);
            Assert.That(camera.fieldOfView, Is.EqualTo(49).Within(.01f));
            Assert.That(view.HasOverride, Is.True);
            view.ClearOverride();
            motor.SetHidden(true); yield return new WaitForSeconds(.2f);
            Assert.That(camera.fieldOfView, Is.EqualTo(49).Within(.01f));
            motor.SetHidden(false);
            typeof(FirstPersonMotor).GetProperty("IsSprinting").SetValue(motor, true);
            yield return new WaitForSeconds(.9f);
            Assert.That(camera.fieldOfView, Is.EqualTo(49).Within(.01f), "Sprint keeps the requested zoom framing.");
            typeof(FirstPersonMotor).GetProperty("IsSprinting").SetValue(motor, false);
            Assert.That(game.BeginInspection(), Is.True); yield return new WaitForSeconds(.9f);
            Assert.That(camera.fieldOfView, Is.EqualTo(49).Within(.01f));
            Assert.That(input.Focus, Is.False);
            SetKeys(); game.EndInspection(); yield return null;
            SetKeys(Key.Z); yield return new WaitForSeconds(1);
            Assert.That(camera.fieldOfView, Is.EqualTo(49).Within(.01f));
            game.Pause(); var frozenPosition = view.transform.position;
            yield return new WaitForSecondsRealtime(1);
            Assert.That(camera.fieldOfView, Is.EqualTo(49).Within(.01f), "Pausing keeps the same framing.");
            Assert.That(view.transform.position, Is.EqualTo(frozenPosition), "Only the FOV release runs while paused.");
            Assert.That(input.Focus, Is.False);
        }

        [UnityTest]
        public IEnumerator ExistingFocusBindingsArePreservedInsteadOfAddingAnotherAction()
        {
            var existing = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Gameplay"); map.AddAction("Focus", InputActionType.Button, "<Keyboard>/x");
            existing.AddActionMap(map); existing.AddActionMap(new InputActionMap("Always")); existing.AddActionMap(new InputActionMap("UI"));
            var other = new GameObject("Existing focus binding fixture"); other.SetActive(false);
            try
            {
                var reader = other.AddComponent<InputReader>(); reader.asset = existing;
                other.SetActive(true); reader.enabled = false;
                var action = reader.Actions.FindAction("Gameplay/Focus");
                Assert.That(action.bindings.Count, Is.EqualTo(1));
                Assert.That(action.bindings[0].path, Is.EqualTo("<Keyboard>/x"));
                Assert.That(existing.FindAction("Gameplay/Focus").enabled, Is.False);
            }
            finally { Object.Destroy(other); Object.Destroy(existing); }
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root) Object.Destroy(root);
            if (sourceAsset) Object.Destroy(sourceAsset);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            yield return null;
            Time.timeScale = previousScale; AudioListener.pause = previousPause;
            Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible;
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            Application.runInBackground = previousRunInBackground;
        }
    }
}
