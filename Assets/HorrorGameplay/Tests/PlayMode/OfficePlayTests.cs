// Verifica SetUp, TearDown, OfficeWallsDeskAndDoorAreTraversableCorrectly, OfficeDrawerNotesFlashlightSaveAndPauseWork.

using System.Collections;
using System.IO;
using System.Linq;
using HorrorGame;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    public sealed class OfficePlayTests
    {
        GameSession game;
        Keyboard keyboard;
        InputSettings.BackgroundBehavior originalBackground;
        InputSettings.EditorInputBehaviorInPlayMode originalEditorInput;
        bool originalRunInBackground;
        byte[] save, backup;
        string savedScene;
        int resume;
        string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            save = File.Exists(SavePath) ? File.ReadAllBytes(SavePath) : null;
            backup = File.Exists(SavePath + ".bak") ? File.ReadAllBytes(SavePath + ".bak") : null;
            savedScene = PlayerPrefs.GetString("Game.SaveScene", "");
            resume = PlayerPrefs.GetInt("Game.ResumeRequested", 0);
            PlayerPrefs.SetInt("Game.ResumeRequested", 0);
            originalBackground = InputSystem.settings.backgroundBehavior;
            originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            originalRunInBackground = Application.runInBackground;
            // Keep virtual devices active when the automated run loses editor focus.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true;
            Time.timeScale = 1;
            AudioListener.pause = false;
            yield return SceneManager.LoadSceneAsync("Chapter1Office");
            for (int i = 0; i < 4; i++) yield return null;
            game = GameSession.Instance;
            Assert.NotNull(game);
            keyboard = InputSystem.AddDevice<Keyboard>();
            game.input.Actions.devices = new InputDevice[] { keyboard };
            InputSystem.Update();
            Press();
            yield return null;
        }
        void Press(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = originalBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
            Application.runInBackground = originalRunInBackground;
            yield return SceneManager.LoadSceneAsync("MainMenu");
            Restore(SavePath, save);
            Restore(SavePath + ".bak", backup);
            PlayerPrefs.SetString("Game.SaveScene", savedScene);
            PlayerPrefs.SetInt("Game.ResumeRequested", resume);
            PlayerPrefs.Save();
        }
        void Restore(string path, byte[] data)
        {
            if (data != null) File.WriteAllBytes(path, data);
            else if (File.Exists(path)) File.Delete(path);
        }

        [UnityTest]
        public IEnumerator OfficeWallsDeskAndDoorAreTraversableCorrectly()
        {
            game.player.Warp(new Vector3(3.2f, .05f, 2.2f), 90);
            Press(Key.W);
            yield return new WaitForSeconds(.8f);
            Assert.Less(game.player.transform.position.x, 3.7f, "The side wall must stop the player.");
            Press();
            yield return null;
            game.player.Warp(new Vector3(1.8f, .05f, -.3f), 0);
            Press(Key.W);
            yield return new WaitForSeconds(1.35f);
            Assert.Greater(game.player.transform.position.z, 2.1f, "The aisle beside the desk must be walkable.");
            Assert.IsTrue(game.player.Grounded);
            Press();
            yield return null;
            game.player.Warp(new Vector3(-2.08f, .05f, -1.65f), 180);
            Press(Key.W);
            yield return new WaitForSeconds(.85f);
            Assert.Greater(game.player.transform.position.z, -3.17f, "Closed door must block movement.");
            Press();
            yield return null;
            game.player.Warp(new Vector3(-1.2f, .05f, -2.4f), 180);
            var door = Object.FindAnyObjectByType<InteractiveDoor>();
            door.Interact(game.interaction);
            yield return new WaitForSeconds(1.45f);
            Assert.IsTrue(door.IsOpen);
            Assert.Greater(Quaternion.Angle(door.hinge.localRotation, Quaternion.identity), 85);
            game.player.Warp(new Vector3(-2.08f, .05f, -2.55f), 180);
            Press(Key.W);
            yield return new WaitForSeconds(.8f);
            Assert.Less(game.player.transform.position.z, -3.75f, "The open doorway must lead into the vestibule.");
            var exit = Object.FindAnyObjectByType<StoryExit>();
            Assert.That(exit, Is.Not.Null);
            var exitTarget = exit.GetComponent<BoxCollider>();
            Assert.That(exitTarget && !exitTarget.isTrigger, Is.True, "The interaction ray ignores triggers.");
            Assert.That(exitTarget.bounds.max.z, Is.LessThan(-5.05f), "The solid exit target belongs at the back of the vestibule, outside the open doorway.");
            StringAssert.Contains("Sair", exit.Prompt(game.interaction));
        }

        [UnityTest]
        public IEnumerator OfficeDrawerNotesFlashlightSaveAndPauseWork()
        {
            var drawer = Object.FindAnyObjectByType<InteractiveDrawer>();
            Vector3 closed = drawer.drawer.localPosition;
            drawer.Interact(game.interaction);
            yield return new WaitForSeconds(1.1f);
            Assert.Greater(Vector3.Distance(closed, drawer.drawer.localPosition), .3f);
            var note = Object.FindObjectsByType<CollectibleItem>().First(n => n.kind == CollectibleKind.Note);
            note.Interact(game.interaction);
            Assert.IsTrue(game.interaction.inventory.Contains(note.itemId));
            Assert.IsFalse(note.gameObject.activeSelf);
            Assert.AreEqual(GameState.Paused, game.State);
            game.Resume();
            var lamp = game.player.GetComponent<FlashlightController>();
            Assert.IsFalse(lamp.IsOn);
            float charge = lamp.BatterySeconds;
            Press(Key.F);
            yield return null;
            yield return null;
            Assert.IsFalse(lamp.IsAvailable, "The flashlight is collected later in the car.");
            Assert.IsFalse(lamp.IsOn, "F must not activate unavailable office equipment.");
            Assert.IsFalse(lamp.lamp.enabled);
            Assert.IsFalse(lamp.handRig.gameObject.activeInHierarchy);
            Assert.AreEqual(charge, lamp.BatterySeconds, "Unavailable equipment does not drain its battery.");
            Press();
            Assert.IsTrue(game.save.Save());
            Assert.AreEqual("Chapter1Office", PlayerPrefs.GetString("Game.SaveScene"));
            game.Pause();
            Assert.AreEqual(0, Time.timeScale);
            Assert.IsTrue(AudioListener.pause);
            Assert.IsTrue(game.hud.pausePanel.activeSelf);
            game.ReturnToMenu();
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
        }
    }
}
