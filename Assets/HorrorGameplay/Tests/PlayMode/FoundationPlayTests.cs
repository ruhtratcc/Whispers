// Verifica SetUp, TearDown, SprintConsumesStaminaAndRecovers, ExhaustionRecoversQuicklyResumesAtTwentyPercentAndPausePreservesStamina, CrouchCannotStandThroughCeiling, CollisionsStopAtCornersAndStepsAreClimbable, OptionalJumpLandsAndProducesNoise, KeyUnlockAndCheckpointRestoreWorldAndInventory, CheckpointRestoresCrouchUnderCeiling, HoldingThrowingAndAutomaticDropRestoreCollision, BatteryRechargePauseAndDeathUiWork, HidingSuppressesVisionAndSupportsHoldingBreath, EnemyHearsNoiseAndVisionIsOccluded.

using System.Collections;
using System.IO;
using System.Linq;
using HorrorGame;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    public sealed class FoundationPlayTests
    {
        GameSession game;
        Keyboard keyboard;
        InputSettings.BackgroundBehavior originalBackground;
        InputSettings.EditorInputBehaviorInPlayMode originalEditorInput;
        bool originalRunInBackground;
        EnemyBrain enemy;
        byte[] originalSave, originalBackup;
        string originalScene;
        int originalResume;
        string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalSave = File.Exists(SavePath) ? File.ReadAllBytes(SavePath) : null;
            originalBackup = File.Exists(SavePath + ".bak") ? File.ReadAllBytes(SavePath + ".bak") : null;
            originalScene = PlayerPrefs.GetString("Game.SaveScene", "");
            originalResume = PlayerPrefs.GetInt("Game.ResumeRequested", 0);
            PlayerPrefs.SetInt("Game.ResumeRequested", 0);
            originalBackground = InputSystem.settings.backgroundBehavior;
            originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            originalRunInBackground = Application.runInBackground;
            // Virtual test devices must survive editor focus changes during automated runs.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true;
            Time.timeScale = 1;
            AudioListener.pause = false;
            // The utility map stays available to Editor QA without shipping in the release profile.
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/HorrorGameplay/Scenes/TestScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("TestScene");
#endif
            for (int i = 0; i < 4; i++)
                yield return null;
            game = GameSession.Instance;
            Assert.NotNull(game);
            enemy = Object.FindAnyObjectByType<EnemyBrain>();
            enemy.enabled = false;
            enemy.GetComponent<NavMeshAgent>().isStopped = true;
            keyboard = InputSystem.AddDevice<Keyboard>();
            game.input.Actions.devices = new InputDevice[]
            {
                keyboard
            };
            InputSystem.Update(); // Resolve the newly restricted virtual device before delivering test input.
            Press();
            for (int i = 0; i < 3; i++)
                yield return null;
        }

        void Press(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = originalBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
            Application.runInBackground = originalRunInBackground;
            yield return SceneManager.LoadSceneAsync("MainMenu");
            RestoreFile(SavePath, originalSave);
            RestoreFile(SavePath + ".bak", originalBackup);
            PlayerPrefs.SetString("Game.SaveScene", originalScene);
            PlayerPrefs.SetInt("Game.ResumeRequested", originalResume);
            PlayerPrefs.Save();
        }

        void RestoreFile(string path, byte[] contents)
        {
            if (contents != null)
                File.WriteAllBytes(path, contents);
            else if (File.Exists(path))
                File.Delete(path);
        }

        [UnityTest]
        public IEnumerator SprintConsumesStaminaAndRecovers()
        {
            game.player.staminaCapacity = .8f;
            game.player.RestoreStamina(.8f);
            Vector3 start = game.player.transform.position;
            Press(Key.W, Key.LeftShift);
            yield return new WaitForSeconds(.35f);
            Assert.IsTrue(game.player.IsSprinting, $"input={game.input.Move}/{game.input.Sprint} can={game.CanControl} frame={Time.frameCount} ground={game.player.Grounded} time={Time.time}");
            Assert.Less(game.player.Stamina, .65f);
            Assert.That(game.player.view.playerCamera.fieldOfView, Is.EqualTo(49).Within(.01f));
            yield return new WaitForSeconds(.65f);
            Assert.IsFalse(game.player.IsSprinting);
            Assert.LessOrEqual(game.player.Stamina, .05f);
            Assert.Greater(game.player.transform.position.z - start.z, 2);
            Press();
            yield return new WaitForSeconds(2.7f);
            Assert.Greater(game.player.Stamina, .7f);
        }

        [UnityTest]
        public IEnumerator ExhaustionRecoversQuicklyResumesAtTwentyPercentAndPausePreservesStamina()
        {
            var motor = game.player;
            Assert.That(motor.staminaRecovery, Is.EqualTo(1.6f).Within(.001f));
            Assert.That(motor.recoveryDelay, Is.EqualTo(.8f).Within(.001f));
            motor.staminaCapacity = 7.2f;
            motor.staminaDrain = 24;
            motor.RestoreStamina(motor.staminaCapacity);
            Press(Key.W, Key.LeftShift);
            float deadline = Time.time + 1.5f;
            while (motor.Stamina > 0 && Time.time < deadline)
                yield return null;
            Assert.That(motor.Stamina, Is.Zero);
            Assert.That(motor.IsSprinting, Is.False);

            game.Pause();
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(motor.Stamina, Is.Zero, "Pausing must not regenerate stamina.");
            game.Resume();
            Press();
            yield return null;
            motor.staminaDrain = 0;
            Press(Key.W, Key.LeftShift);
            float recoveredAt = Time.time;
            deadline = recoveredAt + 2.3f;
            while (Time.time < deadline)
            {
                if (motor.Stamina01 < .19f)
                    Assert.That(motor.IsSprinting, Is.False);
                if (motor.IsSprinting)
                    break;
                yield return null;
            }
            Assert.That(motor.IsSprinting, Is.True, $"Holding sprint must resume after 20% recovery. stamina={motor.Stamina01} input={game.input.Move}/{game.input.Sprint} grounded={motor.Grounded} state={game.State}");
            Assert.That(motor.Stamina01, Is.InRange(.2f, .27f), "A 30% release threshold would still block sprint here.");
            Assert.That(Time.time - recoveredAt, Is.LessThan(2.1f), "The shorter delay and doubled recovery must recover 20% promptly.");
            Press();
        }

        [UnityTest]
        public IEnumerator CrouchCannotStandThroughCeiling()
        {
            game.player.Warp(new Vector3(-4, .05f, -5), 0);
            Press(Key.LeftCtrl);
            yield return new WaitForSeconds(.3f);
            game.player.Warp(new Vector3(-4, .05f, -2), 0);
            Press();
            yield return new WaitForSeconds(.3f);
            Assert.IsTrue(game.player.IsCrouched);
            Assert.Less(game.player.Controller.height, 1.15f);
            game.player.Warp(new Vector3(-4, .05f, -5), 0);
            yield return new WaitForSeconds(.3f);
            Assert.IsFalse(game.player.IsCrouched);
            Assert.Greater(game.player.Controller.height, 1.7f);
        }

        [UnityTest]
        public IEnumerator CollisionsStopAtCornersAndStepsAreClimbable()
        {
            game.player.Warp(new Vector3(13, .05f, 13), 0);
            Press(Key.W, Key.D);
            yield return new WaitForSeconds(1.4f);
            Assert.LessOrEqual(game.player.transform.position.x, 14.57f);
            Assert.LessOrEqual(game.player.transform.position.z, 14.57f);
            Press();
            yield return null;
            game.player.Warp(new Vector3(7, .05f, -3.8f), 0);
            Press(Key.W);
            yield return new WaitForSeconds(1.3f);
            Assert.Greater(game.player.transform.position.y, .45f);
            Assert.Greater(game.player.transform.position.z, -2);
        }

        [UnityTest]
        public IEnumerator OptionalJumpLandsAndProducesNoise()
        {
            int landings = 0;
            System.Action<NoiseEvent> listener = n =>
            {
                if (n.kind == NoiseKind.Landing)
                    landings++;
            };
            NoiseSystem.Emitted += listener;
            try
            {
                game.player.allowJump = true;
                Press(Key.Space);
                yield return new WaitForSeconds(.2f);
                Assert.Greater(game.player.transform.position.y, .25f);
                Press();
                yield return new WaitForSeconds(.9f);
                Assert.IsTrue(game.player.Grounded);
                Assert.GreaterOrEqual(landings, 1);
            }
            finally
            {
                NoiseSystem.Emitted -= listener;
            }
        }

        [UnityTest]
        public IEnumerator KeyUnlockAndCheckpointRestoreWorldAndInventory()
        {
            var door = Object.FindObjectsByType<InteractiveDoor>().First(d => d.locked);
            door.Interact(game.interaction);
            Assert.IsTrue(door.locked);
            Assert.IsFalse(door.IsOpen);
            var key = Object.FindObjectsByType<CollectibleItem>().First(i => i.kind == CollectibleKind.Key);
            key.Interact(game.interaction);
            Assert.IsTrue(game.interaction.inventory.Contains("school_key"));
            Assert.IsFalse(key.gameObject.activeSelf);
            door.Interact(game.interaction);
            yield return new WaitForSeconds(.4f);
            Assert.IsFalse(door.locked);
            Assert.IsTrue(door.IsOpen);
            Vector3 position = game.player.transform.position;
            Assert.IsTrue(game.save.Save());
            game.interaction.inventory.items.Clear();
            door.locked = true;
            door.Interact(game.interaction);
            key.gameObject.SetActive(true);
            game.player.Warp(new Vector3(3, .05f, -8), 20);
            Assert.IsTrue(game.save.LoadCurrent());
            Assert.IsTrue(game.interaction.inventory.Contains("school_key"));
            Assert.IsFalse(door.locked);
            Assert.IsTrue(door.IsOpen);
            Assert.IsFalse(key.gameObject.activeSelf);
            Assert.Less(Vector3.Distance(position, game.player.transform.position), .05f);
        }

        [UnityTest]
        public IEnumerator CheckpointRestoresCrouchUnderCeiling()
        {
            game.player.Warp(new Vector3(-4, .05f, -5), 0);
            Press(Key.LeftCtrl);
            yield return new WaitForSeconds(.3f);
            game.player.Warp(new Vector3(-4, .05f, -2), 0);
            yield return new WaitForSeconds(.1f);
            Assert.IsTrue(game.save.Save());
            game.player.Warp(new Vector3(-4, .05f, -5), 0);
            Press();
            yield return new WaitForSeconds(.3f);
            Assert.IsTrue(game.save.LoadCurrent());
            Assert.IsTrue(game.player.IsCrouched);
            Assert.Less(game.player.Controller.height, 1.15f);
            yield return new WaitForSeconds(.2f);
            Assert.IsTrue(game.player.IsCrouched);
        }

        [UnityTest]
        public IEnumerator HoldingThrowingAndAutomaticDropRestoreCollision()
        {
            var body = Object.FindObjectsByType<PhysicsPickup>().First(p => p.GetComponent<Rigidbody>().mass == 6).GetComponent<Rigidbody>();
            game.player.Warp(new Vector3(-2, .05f, -6), 0);
            yield return null;
            var grab = game.interaction.grabber;
            Assert.IsTrue(grab.PickUp(body));
            Assert.IsTrue(Physics.GetIgnoreCollision(game.player.Controller, body.GetComponent<Collider>()));
            yield return new WaitForSeconds(.65f);
            Assert.NotNull(grab.Held);
            Assert.Greater(body.position.y, .8f);
            grab.Throw();
            Assert.IsNull(grab.Held);
            Assert.IsFalse(Physics.GetIgnoreCollision(game.player.Controller, body.GetComponent<Collider>()));
            yield return new WaitForFixedUpdate();
            Assert.Greater(body.linearVelocity.z, 0);
            Assert.IsTrue(grab.PickUp(body));
            body.position = game.player.view.transform.position + Vector3.forward * 5;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsNull(grab.Held);
        }

        [UnityTest]
        public IEnumerator BatteryRechargePauseAndDeathUiWork()
        {
            var flashlight = game.player.GetComponent<FlashlightController>();
            float fullIntensity = flashlight.lamp.intensity;
            flashlight.Restore(1, true);
            yield return new WaitForSeconds(.12f);
            Assert.Less(flashlight.lamp.intensity, fullIntensity * .5f);
            var battery = Object.FindObjectsByType<CollectibleItem>().First(i => i.kind == CollectibleKind.Battery);
            battery.Interact(game.interaction);
            Assert.Greater(flashlight.BatterySeconds, 80);
            game.Pause();
            float charge = flashlight.BatterySeconds;
            Assert.AreEqual(0, Time.timeScale);
            Assert.IsTrue(AudioListener.pause);
            Assert.IsTrue(game.hud.pausePanel.activeSelf);
            Assert.IsFalse(game.input.Actions["Gameplay/Move"].enabled);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.AreEqual(charge, flashlight.BatterySeconds, .01f);
            game.hud.resumeButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
            game.Kill();
            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.IsTrue(game.hud.gameOverPanel.activeSelf);
            Assert.AreEqual(1, game.hud.deathCurtain.alpha);
        }

        [UnityTest]
        public IEnumerator HidingSuppressesVisionAndSupportsHoldingBreath()
        {
            var hide = Object.FindObjectsByType<HidingSpot>().First(h => h.displayName == "armário");
            game.player.Warp(hide.exitPose.position, 0);
            hide.Interact(game.interaction);
            yield return new WaitForSeconds(.2f);
            Assert.IsTrue(game.player.IsHidden);
            Assert.IsFalse(game.player.Controller.enabled);
            Assert.IsFalse(enemy.CanSeePlayer());
            Vector3 hidden = game.player.transform.position;
            Press(Key.LeftAlt, Key.A);
            yield return new WaitForSeconds(.3f);
            Assert.IsTrue(game.player.HoldingBreath);
            Assert.Less(hide.Breath01, 1);
            Assert.Less(Vector3.Distance(hidden, game.player.transform.position), .01f);
            Press();
            Assert.IsTrue(hide.Exit());
            yield return null;
            Assert.IsFalse(game.player.IsHidden);
            Assert.IsTrue(game.player.Controller.enabled);
        }

        [UnityTest]
        public IEnumerator EnemyHearsNoiseAndVisionIsOccluded()
        {
            var agent = enemy.GetComponent<NavMeshAgent>();
            Assert.IsTrue(agent.isOnNavMesh);
            agent.Warp(new Vector3(8, 0, 10));
            enemy.transform.rotation = Quaternion.identity;
            enemy.enabled = true;
            yield return null;
            NoiseSystem.Emit(new Vector3(6, 0, 11), 15, game.player.gameObject, NoiseKind.Impact);
            Assert.AreEqual(EnemyState.Investigate, enemy.State);
            yield return new WaitForSeconds(.2f);
            Assert.IsTrue(agent.hasPath);
            game.player.Warp(new Vector3(8, .05f, 13), 180);
            enemy.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            Assert.IsTrue(enemy.CanSeePlayer());
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = LayerMask.NameToLayer("World");
            wall.transform.position = new Vector3(8, 1, 11.5f);
            wall.transform.localScale = new Vector3(3, 3, .2f);
            Physics.SyncTransforms();
            Assert.IsFalse(enemy.CanSeePlayer());
            Object.Destroy(wall);
            enemy.enabled = false;
            yield return null;
        }
    }
}


