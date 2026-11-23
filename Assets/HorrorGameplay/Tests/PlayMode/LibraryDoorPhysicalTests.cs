// Verifica SetUp, LibraryPairHasPhysicalClosedBasesAndClearsThePlayerAfterUnlockAndOldRestore, TearDown.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    /// <summary>Uses the saved doorway and actual player capsule; never writes the real checkpoint.</summary>
    [Category("GameplayPolish")]
    public sealed class LibraryDoorPhysicalTests
    {
        GameSession game;
        SchoolSimplifiedFlow flow;
        string disposable;
        readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
        readonly Dictionary<string, (bool had, string value)> prefs = new Dictionary<string, (bool, string)>();
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        bool runInBackground;
        float startedAt;
        string lastTestPhase;
        readonly List<string> progress = new List<string>();
        const string ProgressPath = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/GameplayPolish20261023/door-test-progress.json";
        string RealSave => Path.Combine(Application.persistentDataPath, "save.json");

        [Serializable] sealed class ProgressSnapshot
        {
            public string phase, lastTestPhase, utc, state, firstPose, secondPose, player;
            public float realtime, elapsed, gameTime, timeScale;
            public bool firstOpen, secondOpen, firstLocked, secondLocked;
        }
        void Trace(string phase, bool testPhase = true)
        {
            if (testPhase) lastTestPhase = phase;
            var first = flow ? flow.libraryDoor : null;
            var second = first ? first.pairedLeaf : null;
            var snapshot = new ProgressSnapshot
            {
                phase = phase, lastTestPhase = lastTestPhase, utc = DateTime.UtcNow.ToString("O"),
                realtime = Time.realtimeSinceStartup, elapsed = Time.realtimeSinceStartup - startedAt,
                gameTime = Time.time, timeScale = Time.timeScale, state = game ? game.State.ToString() : "loading",
                firstPose = first && first.hinge ? first.hinge.eulerAngles.ToString("F3") : "",
                secondPose = second && second.hinge ? second.hinge.eulerAngles.ToString("F3") : "",
                player = game && game.player ? game.player.transform.position.ToString("F3") : "",
                firstOpen = first && first.IsOpen, secondOpen = second && second.IsOpen,
                firstLocked = first && first.locked, secondLocked = second && second.locked
            };
            Directory.CreateDirectory(Path.GetDirectoryName(ProgressPath));
            string json = JsonUtility.ToJson(snapshot, true);
            progress.Add(json);
            File.WriteAllText(ProgressPath, "{\"latest\":" + json + ",\"history\":[" + string.Join(",", progress) + "]}");
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            startedAt = Time.realtimeSinceStartup;
            progress.Clear();
            Trace("setup: protect checkpoint and input");
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                files[RealSave + suffix] = File.Exists(RealSave + suffix) ? File.ReadAllBytes(RealSave + suffix) : null;
            foreach (string key in new[] { "Game.SaveScene", "Game.ResumeRequested", "Game.StoryCarryRequested" })
                prefs[key] = (PlayerPrefs.HasKey(key), key == "Game.SaveScene" ? PlayerPrefs.GetString(key) : PlayerPrefs.GetInt(key).ToString());
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            runInBackground = Application.runInBackground;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true;
            disposable = "library-door-" + Guid.NewGuid().ToString("N") + ".json";
            PlayerPrefs.SetInt("Game.ResumeRequested", 0);
            PlayerPrefs.SetInt("Game.StoryCarryRequested", 0);
            SceneManager.sceneLoaded += RedirectSave;
            Time.timeScale = 1; AudioListener.pause = false;
            Trace("setup: load SchoolCEEP");
            yield return SceneManager.LoadSceneAsync("SchoolCEEP");
            Trace("setup: wait for narrative initialization");
            yield return Until(() => GameSession.Instance && GameSession.Instance.narrative && GameSession.Instance.narrative.IsInitialized, 20);
            game = GameSession.Instance;
            flow = UnityEngine.Object.FindAnyObjectByType<SchoolSimplifiedFlow>();
            Assert.That(flow && flow.libraryDoor && flow.libraryDoor.pairedLeaf, Is.True);
            Assert.That(game.save.fileName, Is.EqualTo(disposable));
            game.input.Actions.devices = Array.Empty<InputDevice>();
            Trace("setup complete");
        }

        void RedirectSave(Scene scene, LoadSceneMode mode)
        {
            foreach (var save in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SaveSystem>(true)))
                save.fileName = disposable;
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            if (condition()) yield break;
            string failedPhase = lastTestPhase;
            Trace("timed out: " + failedPhase);
            Assert.Fail("Library door timed out after " + seconds + " real seconds at " + failedPhase
                + "; fixture elapsed=" + (Time.realtimeSinceStartup - startedAt).ToString("F2")
                + ", game time=" + Time.time.ToString("F2") + ", scale=" + Time.timeScale
                + ", first=" + (flow && flow.libraryDoor ? flow.libraryDoor.hinge.eulerAngles.ToString("F2") : "unavailable")
                + ", second=" + (flow && flow.libraryDoor && flow.libraryDoor.pairedLeaf ? flow.libraryDoor.pairedLeaf.hinge.eulerAngles.ToString("F2") : "unavailable")
                + ", player=" + (game && game.player ? game.player.transform.position.ToString("F2") : "unavailable"));
        }
        static void At(InteractiveDoor door, float yaw)
        {
            Assert.That(Quaternion.Angle(door.hinge.rotation, Quaternion.Euler(0, yaw, 0)), Is.LessThan(.15f), door.persistentId + " has the wrong physical hinge pose.");
        }
        Vector3 SupportedFeet(Vector3 location)
        {
            int mask = ~(LayerMask.GetMask("Player") | (1 << 31));
            Assert.That(Physics.Raycast(location + Vector3.up * 1.5f, Vector3.down, out var floor, 3, mask, QueryTriggerInteraction.Ignore), Is.True,
                "No supported approach to the actual library door.");
            Assert.That(floor.normal.y, Is.GreaterThan(.8f));
            return new Vector3(location.x, floor.point.y + .045f, location.z);
        }
        bool CapsuleHitsLibrary(InteractiveDoor first, InteractiveDoor second, float distance)
        {
            var controller = game.player.Controller;
            Vector3 feet = game.player.transform.position;
            float radius = controller.radius;
            var hits = Physics.CapsuleCastAll(feet + Vector3.up * (radius + .01f),
                feet + Vector3.up * (controller.height - radius - .01f), radius,
                Vector3.back, distance, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore);
            return hits.Any(hit => hit.collider.GetComponentInParent<InteractiveDoor>() == first
                || hit.collider.GetComponentInParent<InteractiveDoor>() == second);
        }
        IEnumerator Walk(Vector3 direction)
        {
            // Small real Controller.Move steps cannot tunnel through the closed leaf.
            for (int i = 0; i < 32; i++)
            {
                game.player.Controller.Move(direction * .1f + Vector3.down * .025f);
                if (i % 4 == 3) yield return null;
            }
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator LibraryPairHasPhysicalClosedBasesAndClearsThePlayerAfterUnlockAndOldRestore()
        {
            Trace("test: verify fresh-game open poses");
            var first = flow.libraryDoor;
            var second = first.pairedLeaf;
            Assert.That(first.persistentId, Is.EqualTo("school.door.0.46"));
            Assert.That(second.persistentId, Is.EqualTo("school.door.0.47"));
            Assert.That(second.pairedLeaf, Is.SameAs(first));
            // A fresh game opens once from the serialized 0/180 bases, not twice from an open preview.
            At(first, 95); At(second, 85);
            flow.enabled = false;
            game.narrative.enabled = false;
            if (flow.sanity) flow.sanity.enabled = false;
            game.player.enabled = false;
            game.interaction.enabled = false;
            game.player.RestoreStance(false);

            Trace("test: set physical closed pair and conflicting old secondary save");
            Vector3 center = (first.hinge.position + second.hinge.position) * .5f;
            Vector3 approach = SupportedFeet(center + Vector3.forward * 1.6f);
            game.player.Warp(approach, 180);
            first.requiredKey = second.requiredKey = "iron_key";
            first.RestoreState("{\"open\":false,\"locked\":true}");
            // An older checkpoint's conflicting secondary leaf cannot override the stable primary record.
            second.RestoreState("{\"open\":true,\"locked\":false}");
            At(first, 0); At(second, 180);
            Assert.That(first.locked && second.locked, Is.True);
            Assert.That(first.IsOpen || second.IsOpen, Is.False);
            Assert.That(game.interaction.inventory.Contains("iron_key"), Is.False);
            second.Interact(game.interaction);
            Assert.That(first.locked && second.locked, Is.True, "A missing key must not open either physical leaf.");
            Physics.SyncTransforms();
            Assert.That(CapsuleHitsLibrary(first, second, 3.2f), Is.True, "The closed physical pair must seal the center of the doorway.");
            Trace("test: walk toward locked closed leaves");
            yield return Walk(Vector3.back);
            Trace("test: check closed-leaf collision");
            Assert.That(game.player.transform.position.z, Is.GreaterThan(center.z + .2f), "The actual player controller crossed a closed library door.");
            Assert.That(game.player.transform.position.z, Is.LessThan(approach.z - .5f), "The approach was blocked before reaching the library leaf.");

            game.player.Warp(approach, 180);
            game.interaction.inventory.Add(new InventoryItem { id = "iron_key", title = "Chave de Ferro" });
            second.Interact(game.interaction);
            Assert.That(first.locked || second.locked, Is.False);
            Trace("test: wait for physical opening animation");
            yield return Until(() => Quaternion.Angle(first.hinge.rotation, Quaternion.Euler(0, 95, 0)) < .15f
                && Quaternion.Angle(second.hinge.rotation, Quaternion.Euler(0, 85, 0)) < .15f, 10);
            Trace("test: restore authoritative open checkpoint over conflicting secondary record");
            string saved = first.CaptureState();
            first.RestoreState("{\"open\":false,\"locked\":true}");
            second.RestoreState("{\"open\":true,\"locked\":false}");
            At(first, 0); At(second, 180);
            first.RestoreState(saved);
            // Loading the old secondary record last must leave the restored open primary authoritative.
            second.RestoreState("{\"open\":false,\"locked\":true}");
            At(first, 95); At(second, 85);
            Assert.That(first.IsOpen && second.IsOpen, Is.True);
            Assert.That(first.locked || second.locked, Is.False);
            Physics.SyncTransforms();
            Assert.That(CapsuleHitsLibrary(first, second, 3.2f), Is.False, "Open leaves still cross the physical walking line.");
            Trace("test: walk inside through open pair");
            yield return Walk(Vector3.back);
            Trace("test: verify entrance clearance");
            Assert.That(game.player.transform.position.z, Is.LessThan(center.z - 1.1f), "The actual player controller could not enter through the open pair.");
            Trace("test: walk outside through open pair");
            yield return Walk(Vector3.forward);
            Trace("test: verify exit clearance");
            Assert.That(game.player.transform.position.z, Is.GreaterThan(center.z + 1.1f), "The actual player controller could not leave through the open pair.");
            Trace("test passed");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Trace("cleanup: unload school", false);
            SceneManager.sceneLoaded -= RedirectSave;
            Time.timeScale = 1; AudioListener.pause = false;
            var blank = SceneManager.CreateScene("Library door cleanup " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(blank);
            var school = SceneManager.GetSceneByName("SchoolCEEP");
            if (school.isLoaded) yield return SceneManager.UnloadSceneAsync(school);
            foreach (var entry in files)
                if (entry.Value != null) File.WriteAllBytes(entry.Key, entry.Value);
                else if (File.Exists(entry.Key)) File.Delete(entry.Key);
            if (!string.IsNullOrEmpty(disposable)) foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string path = Path.Combine(Application.persistentDataPath, disposable + suffix);
                if (File.Exists(path)) File.Delete(path);
            }
            foreach (var entry in prefs)
                if (!entry.Value.had) PlayerPrefs.DeleteKey(entry.Key);
                else if (entry.Key == "Game.SaveScene") PlayerPrefs.SetString(entry.Key, entry.Value.value);
                else PlayerPrefs.SetInt(entry.Key, int.Parse(entry.Value.value));
            PlayerPrefs.Save();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = runInBackground;
            Trace("cleanup complete: checkpoint, preferences and input restored", false);
        }
    }
}
