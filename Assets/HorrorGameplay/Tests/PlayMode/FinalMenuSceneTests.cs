// Verifica SetUp, TearDown, NoSaveHidesContinueAndMainButtonsSupportPointerAndKeyboard, SettingsMouseClickSlidersEscapeAndFocusWorkWithoutDisplayChanges, NewGamePresentsTheLoreAndAnyKeySkipsToTheOffice, NewGameKeepsChapterConfirmationAndStartsExistingOffice, ContinueLoadsSavedOfficePositionAndInventoryInsteadOfNewGame, ContinueWithOnlyHealthySchoolBackupRestoresSchoolInventoryAndPose, ContinueUsesHealthySchoolSnapshotWhenSavedScenePreferenceIsMissing, ContinueUsesHealthySchoolSnapshotInsteadOfWrongOfficePreference, ContinueWithCorruptPrimaryRestoresHealthySchoolBackup, ContinueWithNoHealthyCandidateStaysInMenuAndPreservesInvalidFiles, ContinueLosingFilesDuringMenuFadeDoesNotStartOrSaveNewGame, ContinueLosingFilesBeforeSessionStartBlocksStartupSavesAndReturnsToMenu, ExplicitNewGameClearsStaleStoryCarryInsteadOfResumingSchoolInventory.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    /// <summary>The saved menu, real UI input and its two entry paths; never changes display settings.</summary>
    [Category("FinalMenu")]
    public sealed class FinalMenuSceneTests
    {
        readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
        readonly Dictionary<string, (bool had, object value)> prefs = new Dictionary<string, (bool, object)>();
        readonly Dictionary<AudioMixer, Dictionary<string, float>> mixers = new Dictionary<AudioMixer, Dictionary<string, float>>();
        readonly string[] integerPrefs = { "Game.ResumeRequested", "Game.StoryCarryRequested", "HorrorMenu.Width", "HorrorMenu.Height", "HorrorMenu.Fullscreen", "HorrorMenu.Quality", "HorrorMenu.InvertY", "HorrorMenu.LanguagePreview", "HorrorMenu.Calibrated" };
        readonly string[] floatPrefs = { "HorrorMenu.Master", "HorrorMenu.Music", "HorrorMenu.SFX", "HorrorMenu.Sensitivity" };
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        bool runInBackground, listenerPaused, cursorVisible;
        float timeScale, listenerVolume;
        CursorLockMode cursorLock;
        UnityEngine.Random.State randomState;
        Keyboard keyboard;
        Mouse mouse;
        Component menu;
        string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string path = SavePath + suffix;
                files[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
                if (File.Exists(path)) File.Delete(path);
            }
            prefs["Game.SaveScene"] = (PlayerPrefs.HasKey("Game.SaveScene"), PlayerPrefs.GetString("Game.SaveScene", ""));
            foreach (string key in integerPrefs) prefs[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key));
            foreach (string key in floatPrefs) prefs[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetFloat(key));
            // Settings.Start applies stored display preferences. Do not exercise those platform operations here.
            foreach (string key in new[] { "HorrorMenu.Width", "HorrorMenu.Height", "HorrorMenu.Fullscreen", "HorrorMenu.Quality" }) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetInt("Game.ResumeRequested", 0);
            PlayerPrefs.SetInt("Game.StoryCarryRequested", 0);
            PlayerPrefs.SetInt("HorrorMenu.Calibrated", 1); // Navigation tests use an already-calibrated profile; first-run has its own test.
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            runInBackground = Application.runInBackground;
            listenerPaused = AudioListener.pause; listenerVolume = AudioListener.volume;
            timeScale = Time.timeScale; cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            randomState = UnityEngine.Random.state;
            CaptureMixers();
            SceneManager.sceneLoaded += SceneLoaded;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true;
            Time.timeScale = 1; AudioListener.pause = false;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            yield return LoadMenu();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            // Unload running controllers before restoring data, so their Start cannot overwrite restored preferences.
            var loaded = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++) loaded.Add(SceneManager.GetSceneAt(i));
            Scene cleanup = SceneManager.CreateScene("FinalMenuCleanup-" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(cleanup);
            foreach (Scene scene in loaded)
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            foreach (var file in files)
                if (file.Value != null) File.WriteAllBytes(file.Key, file.Value);
                else if (File.Exists(file.Key)) File.Delete(file.Key);
            foreach (var pref in prefs)
            {
                if (!pref.Value.had) PlayerPrefs.DeleteKey(pref.Key);
                else if (pref.Value.value is string text) PlayerPrefs.SetString(pref.Key, text);
                else if (pref.Value.value is int integer) PlayerPrefs.SetInt(pref.Key, integer);
                else PlayerPrefs.SetFloat(pref.Key, (float)pref.Value.value);
            }
            PlayerPrefs.Save();
            foreach (var mixer in mixers)
                if (mixer.Key) foreach (var parameter in mixer.Value) mixer.Key.SetFloat(parameter.Key, parameter.Value);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = runInBackground;
            Time.timeScale = timeScale; AudioListener.pause = listenerPaused; AudioListener.volume = listenerVolume;
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
            UnityEngine.Random.state = randomState;
            files.Clear(); prefs.Clear(); mixers.Clear();
        }

        void SceneLoaded(Scene scene, LoadSceneMode mode) => CaptureMixers();
        void CaptureMixers()
        {
            foreach (var mixer in Resources.FindObjectsOfTypeAll<AudioMixer>())
            {
                if (mixers.ContainsKey(mixer)) continue;
                var values = new Dictionary<string, float>();
                foreach (string name in new[] { "MusicVolume", "SFXVolume", "AmbienceVolume", "VoiceVolume" })
                    if (mixer.GetFloat(name, out float value)) values[name] = value;
                mixers[mixer] = values;
            }
        }

        static Component FindMenu() => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(component => component.GetType().FullName == "SchoolMenu.MainMenuController");
        static T Field<T>(Component component, string name) => (T)component.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(component);
        static bool Busy(Component component) => Field<bool>(component, "busy");
        UnityEngine.UI.Button Button(string field) => Field<UnityEngine.UI.Button>(menu, field);
        Component Chapters => Field<Component>(menu, "chapterSelection");
        bool ChaptersOpen => (bool)Chapters.GetType().GetProperty("IsOpen").GetValue(Chapters);
        static IEnumerator Until(Func<bool> condition, string message, float seconds = 25)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
        IEnumerator LoadMenu()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            menu = FindMenu();
            Assert.That(menu, Is.Not.Null, "Saved MainMenu must retain its controller.");
            var module = UnityEngine.Object.FindAnyObjectByType<InputSystemUIInputModule>();
            Assert.That(module && module.actionsAsset, Is.True);
            module.actionsAsset.devices = new InputDevice[] { keyboard, mouse };
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(1, 1) });
            var curtain = Field<CanvasGroup>(Field<Component>(menu, "fader"), "curtain");
            // Start is an IEnumerator: Awake's initial busy=false does not mean its intro has finished.
            yield return null;
            yield return Until(() => !Busy(menu) && !curtain.blocksRaycasts, "Menu intro never released UI input.");
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(curtain.blocksRaycasts, Is.False);
        }
        Vector2 Target(UnityEngine.UI.Selectable selectable)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)selectable.transform;
            var canvas = selectable.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var hits = new List<RaycastResult>();
            var pointer = new PointerEventData(EventSystem.current) { position = point };
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), selectable.name + " has no pointer target.");
            Assert.That(hits[0].module, Is.TypeOf<UnityEngine.UI.GraphicRaycaster>());
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(selectable.gameObject),
                selectable.name + " is hidden behind " + hits[0].gameObject.name);
            return point;
        }
        IEnumerator Click(UnityEngine.UI.Button button)
        {
            Vector2 position = Target(button);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, true));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
        }
        IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
        }
        IEnumerator OfficeReady()
        {
            yield return Until(() => SceneManager.GetActiveScene().name == "Chapter1Office" && GameSession.Instance
                && GameSession.Instance.narrative && GameSession.Instance.narrative.IsInitialized,
                "Menu entry did not reach the existing office.", 40);
            yield return null;
            GameSession.Instance.input.Actions.devices = Array.Empty<InputDevice>();
        }

        SaveData SeedSchoolCheckpoint(string suffix = "")
        {
            var checkpoint = new SaveData
            {
                scene = "SchoolCEEP", position = new Vector3(.90739f, .689f, 21.25f), heading = 123, pitch = 7,
                stamina = 4, battery = 151, flashlightOn = true, tension = .17f,
                narrative = new NarrativeProgress { storyRevision = 2, chapterId = "school" }
            };
            checkpoint.inventory.Add(new InventoryItem { id = "save-resume-school-" + Guid.NewGuid().ToString("N"), title = "Prova de checkpoint" });
            File.WriteAllText(SavePath + suffix, JsonUtility.ToJson(checkpoint, true));
            Assert.That(SaveSystem.TryRead(SavePath + suffix, out _), Is.True, "The staged school checkpoint must be valid before menu entry.");
            return checkpoint;
        }

        IEnumerator AssertSchoolResumed(SaveData checkpoint)
        {
            yield return Until(() => SceneManager.GetActiveScene().name == "SchoolCEEP" && GameSession.Instance
                && GameSession.Instance.narrative && GameSession.Instance.narrative.IsInitialized,
                "Continue did not restore the saved school scene.", 40);
            var game = GameSession.Instance;
            game.input.Actions.devices = Array.Empty<InputDevice>();
            Assert.That(game.interaction.inventory.Contains(checkpoint.inventory[0].id), Is.True, "Continue lost the checkpoint's inventory marker.");
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo("school"));
            Assert.That(game.narrative.Progress.storyRevision, Is.EqualTo(2));
            Assert.That(game.narrative.NextSignal, Is.EqualTo("school.entered"), "Continue must preserve the saved objective.");
            Assert.That(Vector3.Distance(game.player.transform.position, checkpoint.position), Is.LessThan(.15f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(game.player.transform.eulerAngles.y, checkpoint.heading)), Is.LessThan(.5f));
            Assert.That(Mathf.Abs(game.player.Pitch - checkpoint.pitch), Is.LessThan(.5f));
            Assert.That(PlayerPrefs.GetInt("Game.ResumeRequested"), Is.Zero);
        }

        void DeleteCheckpointCandidates()
        {
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(SavePath + suffix)) File.Delete(SavePath + suffix);
        }

        void AssertNoCheckpointWritten()
        {
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                Assert.That(File.Exists(SavePath + suffix), Is.False, "Failed Continue created " + suffix + " checkpoint data.");
        }

        IEnumerator FailedResumeReturnsToMenu()
        {
            yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu" && FindMenu() && !Busy(FindMenu()),
                "A failed Continue must stay in or return to the menu, without starting a new game.", 40);
            menu = FindMenu();
            yield return null; yield return null;
            Assert.That(GameSession.Instance, Is.Null, "Failed Continue left a gameplay session running.");
            Assert.That(ChaptersOpen, Is.False, "Failed Continue must not open New Game chapter confirmation.");
            Assert.That(PlayerPrefs.GetInt("Game.ResumeRequested"), Is.Zero);
            Assert.That(PlayerPrefs.GetInt("Game.StoryCarryRequested"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator NoSaveHidesContinueAndMainButtonsSupportPointerAndKeyboard()
        {
            Assert.That(Button("continueButton").gameObject.activeSelf, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("newGameButton").gameObject));
            foreach (string field in new[] { "newGameButton", "settingsButton", "quitButton" }) Target(Button(field));
            yield return Press(Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("settingsButton").gameObject));
            yield return Press(Key.UpArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("newGameButton").gameObject));
            Assert.That(File.Exists(SavePath), Is.False, "Browsing the menu must not create a checkpoint.");
        }

        [UnityTest]
        public IEnumerator SettingsMouseClickSlidersEscapeAndFocusWorkWithoutDisplayChanges()
        {
            int width = Screen.width, height = Screen.height, quality = QualitySettings.GetQualityLevel();
            FullScreenMode mode = Screen.fullScreenMode;
            yield return Click(Button("settingsButton"));
            Assert.That(Field<GameObject>(menu, "settingsPanel").activeSelf, Is.True);
            Assert.That(Field<GameObject>(menu, "mainPanel").activeSelf, Is.False);
            var settings = Field<Component>(menu, "settingsManager");
            var master = Field<UnityEngine.UI.Slider>(settings, "master");
            var music = Field<UnityEngine.UI.Slider>(settings, "music");
            var sensitivity = Field<UnityEngine.UI.Slider>(settings, "sensitivity");
            // Opening beneath a stationary pointer legitimately focuses the newly hovered row.
            var pointerFocus = EventSystem.current.currentSelectedGameObject;
            Assert.That(pointerFocus && pointerFocus.activeInHierarchy, Is.True);
            Assert.That(pointerFocus.transform.IsChildOf(Field<GameObject>(menu, "settingsPanel").transform), Is.True);
            Assert.That(pointerFocus.GetComponent<UnityEngine.UI.Selectable>().IsInteractable(), Is.True);
            yield return Press(Key.Escape);
            Assert.That(Field<GameObject>(menu, "settingsPanel").activeSelf, Is.False);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(1, 1) });
            yield return null; yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("settingsButton").gameObject));
            yield return Press(Key.Enter);
            Assert.That(Field<GameObject>(menu, "settingsPanel").activeSelf, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(master.gameObject));
            master.value = master.value > .5f ? .37f : .73f;
            sensitivity.value = Mathf.Lerp(sensitivity.minValue, sensitivity.maxValue, .6f);
            Assert.That(PlayerPrefs.GetFloat("HorrorMenu.Master"), Is.EqualTo(master.value).Within(.001f));
            Assert.That(PlayerPrefs.GetFloat("HorrorMenu.Sensitivity"), Is.EqualTo(sensitivity.value).Within(.001f));
            yield return Press(Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(music.gameObject));
            yield return Press(Key.UpArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(master.gameObject));
            Target(Button("backButton"));
            yield return Press(Key.Escape);
            Assert.That(Field<GameObject>(menu, "settingsPanel").activeSelf, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("settingsButton").gameObject));
            Assert.That(Screen.width, Is.EqualTo(width)); Assert.That(Screen.height, Is.EqualTo(height));
            Assert.That(Screen.fullScreenMode, Is.EqualTo(mode)); Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(quality));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator NewGamePresentsTheLoreAndAnyKeySkipsToTheOffice()
        {
            yield return Until(() => !SceneTransitionService.ControlsBlocked, "A transição anterior liberou o menu.", 70);
            yield return Click(Button("newGameButton"));
            yield return Until(() => ChaptersOpen && !Busy(menu), "Novo Jogo abre a confirmação do capítulo.");
            yield return Click(Field<UnityEngine.UI.Button>(Chapters, "chapterButton"));
            var fader = Field<Component>(menu, "fader");
            var line = Field<TMPro.TMP_Text>(fader, "line");
            yield return Until(() => line.text.Contains("Quatro alunos"), "O primeiro cartão aparece.");
            Assert.That(Field<CanvasGroup>(fader, "curtain").alpha, Is.EqualTo(1f));
            yield return Until(() => line.text.Contains("demolida"), "O segundo cartão avança sem entrada.");
            yield return Until(() => line.text.Contains("João de Claus"), "O terceiro cartão avança sem entrada.");
            yield return Press(Key.Q);
            yield return Until(() => !line || string.IsNullOrEmpty(line.text),
                "Uma tecla sem ação no menu encerra a introdução.", 3);
            yield return OfficeReady();
            Assert.That(GameSession.Instance.narrative.Progress.chapterId, Is.EqualTo(BeforeDemolitionCampaign.OfficeChapterId));
        }

        [UnityTest]
        public IEnumerator NewGameKeepsChapterConfirmationAndStartsExistingOffice()
        {
            yield return Click(Button("newGameButton"));
            yield return Until(() => ChaptersOpen && !Busy(menu), "New game skipped or stuck in chapter confirmation.");
            var chapter = Field<UnityEngine.UI.Button>(Chapters, "chapterButton");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(chapter.gameObject));
            Target(chapter);
            Target(Field<UnityEngine.UI.Button>(Chapters, "returnButton"));
            yield return Press(Key.Escape);
            yield return Until(() => !ChaptersOpen && !Busy(menu), "Chapter return did not restore the menu.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("newGameButton").gameObject));
            yield return Press(Key.Enter);
            yield return Until(() => ChaptersOpen && !Busy(menu), "Keyboard submit did not open chapter confirmation.");
            yield return Click(Field<UnityEngine.UI.Button>(Chapters, "chapterButton"));
            yield return OfficeReady();
            Assert.That(GameSession.Instance.narrative.Progress.chapterId, Is.EqualTo("prologue"));
            Assert.That(GameSession.Instance.save.flashlight.IsAvailable, Is.False);
        }

        [UnityTest]
        public IEnumerator ContinueLoadsSavedOfficePositionAndInventoryInsteadOfNewGame()
        {
            yield return SceneManager.LoadSceneAsync("Chapter1Office");
            yield return OfficeReady();
            var original = GameSession.Instance;
            Assert.That(original.save.Save(), Is.True);
            Assert.That(SaveSystem.TryRead(SavePath, out var checkpoint), Is.True);
            checkpoint.heading = 123;
            checkpoint.inventory.Add(new InventoryItem { id = "final-menu-resume-proof", title = "Teste de checkpoint" });
            Assert.That(original.save.SaveSnapshot(checkpoint), Is.True);
            yield return LoadMenu();
            Assert.That(Button("continueButton").gameObject.activeSelf, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("continueButton").gameObject));
            yield return Click(Button("continueButton"));
            yield return OfficeReady();
            Assert.That(GameSession.Instance, Is.Not.EqualTo(original));
            Assert.That(GameSession.Instance.interaction.inventory.Contains("final-menu-resume-proof"), Is.True);
            Assert.That(Vector3.Distance(GameSession.Instance.player.transform.position, checkpoint.position), Is.LessThan(.15f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(GameSession.Instance.player.transform.eulerAngles.y, checkpoint.heading)), Is.LessThan(.5f));
            Assert.That(GameSession.Instance.narrative.Progress.chapterId, Is.EqualTo("prologue"));
            Assert.That(PlayerPrefs.GetInt("Game.ResumeRequested"), Is.Zero);
        }

        [UnityTest, Category("SaveResume")]
        public IEnumerator ContinueWithOnlyHealthySchoolBackupRestoresSchoolInventoryAndPose()
        {
            var checkpoint = SeedSchoolCheckpoint(".bak");
            PlayerPrefs.SetString("Game.SaveScene", "Chapter1Office");
            yield return LoadMenu();
            Assert.That(File.Exists(SavePath), Is.False);
            Assert.That(Button("continueButton").gameObject.activeSelf, Is.True, "A healthy backup must make Continue available.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("continueButton").gameObject));
            yield return Click(Button("continueButton"));
            yield return AssertSchoolResumed(checkpoint);
        }

        [UnityTest, Category("SaveResume")]
        public IEnumerator ContinueUsesHealthySchoolSnapshotWhenSavedScenePreferenceIsMissing()
        {
            var checkpoint = SeedSchoolCheckpoint();
            PlayerPrefs.DeleteKey("Game.SaveScene");
            yield return LoadMenu();
            Assert.That(PlayerPrefs.HasKey("Game.SaveScene"), Is.False);
            Assert.That(Button("continueButton").gameObject.activeSelf, Is.True);
            yield return Click(Button("continueButton"));
            yield return AssertSchoolResumed(checkpoint);
        }

        [UnityTest, Category("SaveResume")]
        public IEnumerator ContinueUsesHealthySchoolSnapshotInsteadOfWrongOfficePreference()
        {
            var checkpoint = SeedSchoolCheckpoint();
            PlayerPrefs.SetString("Game.SaveScene", "Chapter1Office");
            yield return LoadMenu();
            Assert.That(PlayerPrefs.GetString("Game.SaveScene"), Is.EqualTo("Chapter1Office"));
            Assert.That(Button("continueButton").gameObject.activeSelf, Is.True);
            yield return Click(Button("continueButton"));
            yield return AssertSchoolResumed(checkpoint);
        }

        [UnityTest, Category("SaveResume")]
        public IEnumerator ContinueWithCorruptPrimaryRestoresHealthySchoolBackup()
        {
            var checkpoint = SeedSchoolCheckpoint(".bak");
            File.WriteAllText(SavePath, "{corrupt-checkpoint");
            PlayerPrefs.SetString("Game.SaveScene", "Chapter1Office");
            yield return LoadMenu();
            Assert.That(SaveSystem.TryRead(SavePath, out _), Is.False);
            Assert.That(Button("continueButton").gameObject.activeSelf, Is.True, "A corrupt primary must not hide a healthy backup.");
            yield return Click(Button("continueButton"));
            yield return AssertSchoolResumed(checkpoint);
        }

        [UnityTest, Category("SaveResume")]
        public IEnumerator ContinueWithNoHealthyCandidateStaysInMenuAndPreservesInvalidFiles()
        {
            SeedSchoolCheckpoint();
            yield return LoadMenu();
            Assert.That(Button("continueButton").gameObject.activeSelf, Is.True);
            // Keep the visible button stale so real UI input also exercises validation at entry.
            File.WriteAllText(SavePath, "{corrupt-primary");
            File.WriteAllText(SavePath + ".bak", "{}");
            byte[] invalidPrimary = File.ReadAllBytes(SavePath), invalidBackup = File.ReadAllBytes(SavePath + ".bak");
            yield return Click(Button("continueButton"));
            yield return FailedResumeReturnsToMenu();
            Assert.That(File.ReadAllBytes(SavePath), Is.EqualTo(invalidPrimary), "Invalid Continue must not replace the primary with a new game.");
            Assert.That(File.ReadAllBytes(SavePath + ".bak"), Is.EqualTo(invalidBackup));
            Assert.That(File.Exists(SavePath + ".tmp"), Is.False);
            yield return LoadMenu();
            Assert.That(Button("continueButton").gameObject.activeSelf, Is.False, "No healthy checkpoint must hide Continue.");
            DeleteCheckpointCandidates();
            yield return LoadMenu();
            Assert.That(Button("continueButton").gameObject.activeSelf, Is.False);
            AssertNoCheckpointWritten();
        }

        [UnityTest, Category("SaveResume")]
        public IEnumerator ContinueLosingFilesDuringMenuFadeDoesNotStartOrSaveNewGame()
        {
            SeedSchoolCheckpoint();
            yield return LoadMenu();
            Vector2 position = Target(Button("continueButton"));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, true));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return Until(() => menu && Busy(menu), "The real Continue click did not start its transition.");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"), "The fixture must remove files while menu entry is in progress.");
            DeleteCheckpointCandidates();
            yield return FailedResumeReturnsToMenu();
            AssertNoCheckpointWritten();
        }

        [UnityTest, Category("SaveResume")]
        public IEnumerator ContinueLosingFilesBeforeSessionStartBlocksStartupSavesAndReturnsToMenu()
        {
            SeedSchoolCheckpoint();
            yield return LoadMenu();
            bool schoolLoaded = false, startupGuardArmed = false, startupSaveRejected = false;
            void RemoveBeforeStart(Scene loaded, LoadSceneMode mode)
            {
                if (loaded.name != "SchoolCEEP") return;
                schoolLoaded = true;
                var game = loaded.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameSession>(true)).FirstOrDefault();
                startupGuardArmed = game && game.IsRestoringCheckpoint && game.narrative && !game.narrative.IsInitialized;
                DeleteCheckpointCandidates();
                startupSaveRejected = game && game.save && !game.save.Save();
            }
            SceneManager.sceneLoaded += RemoveBeforeStart;
            try
            {
                yield return Click(Button("continueButton"));
                yield return FailedResumeReturnsToMenu();
            }
            finally { SceneManager.sceneLoaded -= RemoveBeforeStart; }
            Assert.That(schoolLoaded, Is.True, "The fixture must remove candidates after the destination scene is loaded.");
            Assert.That(startupGuardArmed, Is.True, "Resume must suspend saves before GameSession.Start can initialize new progress.");
            Assert.That(startupSaveRejected, Is.True, "An event save during checkpoint restoration must be rejected.");
            AssertNoCheckpointWritten();
        }
        [UnityTest, Category("SaveResume")]
        public IEnumerator ExplicitNewGameClearsStaleStoryCarryInsteadOfResumingSchoolInventory()
        {
            var checkpoint = SeedSchoolCheckpoint();
            yield return LoadMenu();
            Assert.That(Button("continueButton").gameObject.activeSelf, Is.True, "The fixture must retain a healthy school checkpoint.");
            PlayerPrefs.SetInt("Game.StoryCarryRequested", 1);
            yield return Click(Button("newGameButton"));
            yield return Until(() => ChaptersOpen && !Busy(menu), "Explicit New Game must retain chapter confirmation.");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            yield return Click(Field<UnityEngine.UI.Button>(Chapters, "chapterButton"));
            yield return OfficeReady();
            Assert.That(GameSession.Instance.narrative.Progress.chapterId, Is.EqualTo("prologue"));
            Assert.That(PlayerPrefs.GetInt("Game.StoryCarryRequested"), Is.Zero);
            Assert.That(GameSession.Instance.interaction.inventory.Contains(checkpoint.inventory[0].id), Is.False,
                "Explicit New Game must not carry the school checkpoint's inventory marker into the prologue.");
        }

    }
}
