// Verifica SetUp, FreshProfileCalibratesInMenuAndRetainsOptionsAfterFirstEscape, PauseOptionsKeepCaptionsBatteryAndSanityWhileEscapeClosesOneLayerAtATime, GammaIsNeutralAtOneAndBrightensRealUrpPixelsMonotonically, TearDown.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("PresentationOptions")]
    public sealed class PresentationOptionsTests
    {
        readonly Dictionary<string, (bool had, float value)> floats = new Dictionary<string, (bool, float)>();
        readonly Dictionary<string, (bool had, int value)> ints = new Dictionary<string, (bool, int)>();
        readonly List<StoryBook> books = new List<StoryBook>();
        Keyboard keyboard;
        string disposable;
        bool background;
        InputSettings.BackgroundBehavior inputBackground;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        [SetUp]
        public void SetUp()
        {
            foreach (string key in new[] { "Gamma", "SubtitleSize", "SubtitleBackground", "Music" })
            { string full = "HorrorMenu." + key; floats[full] = (PlayerPrefs.HasKey(full), PlayerPrefs.GetFloat(full)); PlayerPrefs.DeleteKey(full); }
            foreach (string full in new[] { "HorrorMenu.ReduceEffects", "HorrorMenu.Calibrated", "Game.ResumeRequested", "Game.StoryCarryRequested" })
            { ints[full] = (PlayerPrefs.HasKey(full), PlayerPrefs.GetInt(full)); PlayerPrefs.SetInt(full, 0); }
            inputBackground = InputSystem.settings.backgroundBehavior; editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            background = Application.runInBackground; Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            disposable = "presentation-" + Guid.NewGuid().ToString("N") + ".json";
            SceneManager.sceneLoaded += Prepare;
            PresentationSettings.Reload(); Time.timeScale = 1; AudioListener.pause = false;
        }
        void Prepare(Scene scene, LoadSceneMode mode)
        {
            foreach (var session in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameSession>(true)))
            {
                session.save.fileName = disposable;
                session.input.Actions.devices = new InputDevice[] { keyboard };
                var book = UnityEngine.Object.Instantiate(session.narrative.book); books.Add(book);
                foreach (var sequence in book.sequences) foreach (var line in sequence.lines) line.seconds = .08f;
                session.narrative.book = book;
            }
        }
        static IEnumerator Until(Func<bool> predicate, string why, float seconds = 25)
        {
            float limit = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That(predicate(), Is.True, why);
        }
        IEnumerator Esc()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }
        static void Capture(string name, AccessibilityPanel panel, Camera source, int width, int height)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("HorrorGame.Editor.PresentationCapture")).First(value => value != null);
            string directory = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RemainingPhases20261026/Phase6/Visual";
            Directory.CreateDirectory(directory);
            Action<Camera> inspect = camera => {
                foreach (var rect in new[] { (RectTransform)panel.gamma.transform, (RectTransform)panel.subtitleSize.transform, (RectTransform)panel.subtitleBackground.transform, (RectTransform)panel.back.transform })
                {
                    var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = camera.WorldToScreenPoint(corner);
                        Assert.That(point.x, Is.InRange(-1f, width + 1f)); Assert.That(point.y, Is.InRange(-1f, height + 1f));
                    }
                }
            };
            type.GetMethod("Capture").Invoke(null, new object[] { SceneManager.GetActiveScene(), source, directory + "/" + name + "-" + width + "x" + height + ".png", width, height, inspect, true });
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator FreshProfileCalibratesInMenuAndRetainsOptionsAfterFirstEscape()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return Until(() => UnityEngine.Object.FindAnyObjectByType<AccessibilityPanel>(), "Fresh profiles open calibration after the intro.");
            var panel = UnityEngine.Object.FindAnyObjectByType<AccessibilityPanel>();
            Assert.That(PresentationSettings.Gamma, Is.EqualTo(1));
            float fov = Camera.main.fieldOfView;
            panel.gamma.value = 1.15f; panel.subtitleSize.value = 1.4f; panel.subtitleBackground.value = .75f; panel.reduceEffects.isOn = true;
            yield return null;
            Assert.That(PlayerPrefs.GetFloat("HorrorMenu.Gamma"), Is.EqualTo(1.15f).Within(.001f));
            Assert.That(UnityEngine.Object.FindAnyObjectByType<DisplayAccessibility>().GammaVector.w, Is.EqualTo(.15f).Within(.001f));
            Assert.That(Camera.main.fieldOfView, Is.EqualTo(fov));
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1280, 960), new Vector2Int(2560, 1080) })
                Capture("Menu-accessibility", panel, Camera.main, size.x, size.y);
            yield return Esc();
            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(PlayerPrefs.GetInt("HorrorMenu.Calibrated"), Is.EqualTo(1));
            var menuType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("SchoolMenu.MainMenuController")).First(value => value != null);
            var menu = Resources.FindObjectsOfTypeAll(menuType).Cast<Component>().Single(value => value.gameObject.scene.name == "MainMenu");
            var options = (GameObject)menuType.GetField("settingsPanel").GetValue(menu);
            Assert.That(options.activeSelf, Is.True, "The first Esc closes only accessibility.");
            yield return Esc(); Assert.That(options.activeSelf, Is.False);
            menuType.GetMethod("OpenSettings").Invoke(menu, new object[] { true });
            panel.entry.onClick.Invoke(); yield return null;
            Assert.That(panel.gamma.value, Is.EqualTo(1.15f).Within(.001f));
            Assert.That(panel.subtitleSize.value, Is.EqualTo(1.4f).Within(.001f));
            Assert.That(panel.reduceEffects.isOn, Is.True);
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator PauseOptionsKeepCaptionsBatteryAndSanityWhileEscapeClosesOneLayerAtATime()
        {
            PlayerPrefs.SetInt("HorrorMenu.Calibrated", 1);
            yield return SceneManager.LoadSceneAsync("SchoolCEEP");
            yield return Until(() => GameSession.Instance && GameSession.Instance.narrative.IsInitialized && GameSession.Instance.CanControl
                && !GameSession.Instance.narrative.SequencePlaying, "School controls must be ready.");
            var game = GameSession.Instance; var sanity = UnityEngine.Object.FindAnyObjectByType<SchoolSanityController>();
            game.hud.ShowSubtitle("João: Eu acabei de ver alguém ali.", 100); yield return new WaitForSeconds(.2f);
            float authored = game.hud.subtitle.fontSize;
            yield return Esc(); Assert.That(game.State, Is.EqualTo(GameState.Paused));
            game.hud.optionsButton.onClick.Invoke(); yield return null; yield return null;
            Assert.That(game.hud.options.music.value, Is.Zero);
            AudioManager.Instance.ApplySettings();
            Assert.That(AudioManager.Instance.mixer.GetFloat("MusicVolume", out var muted), Is.True);
            Assert.That(muted, Is.EqualTo(-80).Within(.1f));
            PlayerPrefs.SetFloat("HorrorMenu.Music", .4f); AudioManager.Instance.ApplySettings(); game.hud.options.Refresh();
            Assert.That(game.hud.options.music.value, Is.EqualTo(.4f).Within(.001f));
            Assert.That(AudioManager.Instance.mixer.GetFloat("MusicVolume", out var audible), Is.True);
            Assert.That(audible, Is.EqualTo(20 * Mathf.Log10(.4f)).Within(.1f));
            var panel = game.hud.GetComponentInParent<Canvas>().GetComponentsInChildren<AccessibilityPanel>(true).Single();
            panel.entry.onClick.Invoke();
            float battery = game.save.flashlight.BatterySeconds, health = sanity.sanity;
            panel.subtitleSize.value = 1.5f; panel.subtitleBackground.value = .75f; panel.reduceEffects.isOn = true;
            yield return null; yield return null;
            Assert.That(game.hud.subtitle.fontSize, Is.EqualTo(authored * 1.5f).Within(.01f));
            Assert.That(game.hud.subtitle.text, Does.Contain("Eu acabei de ver alguém ali."));
            var plate = game.hud.subtitle.transform.parent.Find("Subtitle background").GetComponent<UnityEngine.UI.Image>();
            Assert.That(plate.color.a, Is.EqualTo(.75f).Within(.001f));
            Assert.That(game.save.flashlight.BatterySeconds, Is.EqualTo(battery)); Assert.That(sanity.sanity, Is.EqualTo(health));
            Assert.That(game.player.view.playerCamera.fieldOfView, Is.EqualTo(49).Within(.01f));
            var vintage = game.player.view.playerCamera.GetComponent<VintageCameraEffect>();
            var parameters = (MaterialPropertyBlock)typeof(VintageCameraEffect).GetMethod("GetParameters", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(vintage, new object[] { 1280, 720 });
            Assert.That(parameters.GetVector(Shader.PropertyToID("_VintageSignal")).y, Is.EqualTo(vintage.grain * .2f).Within(.0001f));
            Capture("Pause-accessibility", panel, game.player.view.playerCamera, 1280, 720);
            yield return Esc(); Assert.That(game.State, Is.EqualTo(GameState.Paused)); Assert.That(game.hud.optionsPanel.activeSelf, Is.True);
            yield return Esc(); Assert.That(game.State, Is.EqualTo(GameState.Paused)); Assert.That(game.hud.pausePanel.activeSelf, Is.True);
            Assert.That(Time.timeScale, Is.Zero); Assert.That(AudioListener.pause, Is.True);
            yield return Esc(); Assert.That(game.State, Is.EqualTo(GameState.Playing)); Assert.That(Time.timeScale, Is.EqualTo(1));
        }
        [UnityTest, Timeout(60000)]
        public IEnumerator GammaIsNeutralAtOneAndBrightensRealUrpPixelsMonotonically()
        {
            var scene = SceneManager.CreateScene("Gamma GPU fixture " + Guid.NewGuid().ToString("N")); SceneManager.SetActiveScene(scene);
            var cameraRoot = new GameObject("Gamma camera"); var camera = cameraRoot.AddComponent<Camera>();
            camera.enabled = false; camera.transform.position = new Vector3(0, 0, -2); camera.orthographic = true; camera.orthographicSize = 1;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.cullingMask = 1 << 31;
            var data = cameraRoot.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true; data.volumeLayerMask = 1 << 30;
            var root = new GameObject("Isolated gamma volume"); root.layer = 30; var display = root.AddComponent<DisplayAccessibility>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.SetColor("_BaseColor", new Color(.18f, .18f, .18f));
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.layer = 31; quad.transform.localScale = Vector3.one * 2; quad.GetComponent<Renderer>().sharedMaterial = material;
            var target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
            var pixels = new Texture2D(64, 64, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            float Read()
            {
                VolumeManager.instance.Update(VolumeManager.instance.stack, camera.transform, data.volumeLayerMask);
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target, isPreview = false });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); pixels.Apply();
                var colour = pixels.GetPixel(32, 32); return (colour.r + colour.g + colour.b) / 3;
            }
            try
            {
                yield return null;
                PresentationSettings.SetGamma(1); var volume = root.GetComponent<Volume>(); volume.sharedProfile.TryGet<LiftGammaGain>(out var grade);
                grade.gamma.overrideState = false; float baseline = Read(); grade.gamma.overrideState = true; float neutral = Read();
                Assert.That(neutral, Is.GreaterThan(.05f)); Assert.That(neutral, Is.EqualTo(baseline).Within(.008f));
                PresentationSettings.SetGamma(.8f); float dark = Read(); PresentationSettings.SetGamma(1.2f); float bright = Read();
                Assert.That(dark, Is.LessThan(neutral - .01f)); Assert.That(bright, Is.GreaterThan(neutral + .01f));
                PresentationSettings.SetGamma(float.NaN); Assert.That(PresentationSettings.Gamma, Is.EqualTo(1));
                const string directory = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RemainingPhases20261026/Phase6"; Directory.CreateDirectory(directory);
                File.WriteAllText(directory + "/gamma-gpu.json", JsonUtility.ToJson(new GammaResult { baseline = baseline, neutral = neutral, dark = dark, bright = bright }, true));
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(material); }
        }
        [Serializable] sealed class GammaResult { public float baseline, neutral, dark, bright; }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= Prepare; Time.timeScale = 1; AudioListener.pause = false;
            var loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var blank = SceneManager.CreateScene("Presentation cleanup " + Guid.NewGuid().ToString("N")); SceneManager.SetActiveScene(blank);
            foreach (var scene in loaded) if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach (var book in books) if (book) UnityEngine.Object.Destroy(book);
            foreach (var entry in floats) { if (entry.Value.had) PlayerPrefs.SetFloat(entry.Key, entry.Value.value); else PlayerPrefs.DeleteKey(entry.Key); }
            foreach (var entry in ints) { if (entry.Value.had) PlayerPrefs.SetInt(entry.Key, entry.Value.value); else PlayerPrefs.DeleteKey(entry.Key); }
            PlayerPrefs.Save(); PresentationSettings.Reload();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            foreach (string suffix in new[] { "", ".bak", ".tmp" }) { string path = Path.Combine(Application.persistentDataPath, disposable + suffix); if (File.Exists(path)) File.Delete(path); }
            InputSystem.settings.backgroundBehavior = inputBackground; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background;
        }
    }
}
