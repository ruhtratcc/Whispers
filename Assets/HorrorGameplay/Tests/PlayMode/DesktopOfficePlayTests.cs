// Verifica .

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HorrorGame.Desktop;

namespace HorrorGame.Tests
{
    /// <summary>Real authored office and real virtual-pointer dispatch; not a walking-route or human visual review.</summary>
    [Category("CRTDesktop")]
    public sealed partial class DesktopOfficePlayTests
    {
        const string ScenePath = "Assets/ChapterOneOffice/Scenes/Chapter1Office.unity";
        const string Prefix = "BeforeDemolition.";
        const string VisualDirectory = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/PCGames20261029/Visual";
        readonly Dictionary<string, (bool had, int value)> preferences = new Dictionary<string, (bool, int)>();
        readonly List<string> readFlags = new List<string>();
        bool guarded, hadSaveScene, background, audioPaused, cursorVisible;
        string savedScene, temporaryDirectory, previousDirectory;
        float timeScale;
        CursorLockMode cursorLock;
        InputSettings.BackgroundBehavior inputBackground;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        Keyboard keyboard, previousKeyboard;
        GameSession game;
        BeforeDemolitionOfficeFlow office;
        RetroComputer pc;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guarded = false;
            preferences.Clear(); readFlags.Clear();
            Assert.That(UnityEngine.Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None), Is.Empty,
                "Start from a blank scene; never let a personal profile Awake before the override.");
            AssertAuthoredBeforeAwake();
            previousDirectory = SaveSystem.TestSaveDirectory;
            timeScale = Time.timeScale; audioPaused = AudioListener.pause;
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            background = Application.runInBackground;
            inputBackground = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousKeyboard = Keyboard.current;
            foreach (string suffix in new[] { "ResumeRequested", "StoryCarryRequested" })
                preferences[suffix] = (PlayerPrefs.HasKey(Prefix + suffix), PlayerPrefs.GetInt(Prefix + suffix));
            hadSaveScene = PlayerPrefs.HasKey(Prefix + "SaveScene");
            savedScene = PlayerPrefs.GetString(Prefix + "SaveScene", "");
            guarded = true;
            temporaryDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CRTDesktop-" + Guid.NewGuid().ToString("N")));
            SaveSystem.TestSaveDirectory = temporaryDirectory;
            SaveSystem.ClearChapterCarry();
            PlayerPrefs.SetInt(Prefix + "ResumeRequested", 0);
            PlayerPrefs.SetInt(Prefix + "StoryCarryRequested", 0);
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Time.timeScale = 1; AudioListener.pause = false;
            keyboard = InputSystem.AddDevice<Keyboard>();
            SceneManager.sceneLoaded += PrepareLoaded;
            yield return LoadOffice();
        }

        static void AssertAuthoredBeforeAwake()
        {
            string serialized = File.ReadAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ScenePath));
            foreach (string script in new[] { "Desktop/RetroComputer", "Narrative/BeforeDemolitionOfficeFlow" })
            {
                string guid = AssetDatabase.AssetPathToGUID("Assets/HorrorGameplay/Scripts/" + script + ".cs");
                Assert.That(!string.IsNullOrEmpty(guid) && serialized.Contains("guid: " + guid), Is.True,
                    "Stop before Awake: required authored component missing: " + script);
            }
            bool bound = AssetDatabase.FindAssets("t:StoryBook").Any(guid =>
            {
                var book = AssetDatabase.LoadAssetAtPath<StoryBook>(AssetDatabase.GUIDToAssetPath(guid));
                if (!book || !BeforeDemolitionCampaign.Is(book) || book.revision != BeforeDemolitionCampaign.Revision) return false;
                string escaped = Regex.Escape(guid);
                return Regex.IsMatch(serialized, @"(?m)^\s*book:\s*\{fileID:\s*\d+,\s*guid:\s*" + escaped + ",")
                    || Regex.IsMatch(serialized, @"(?m)^\s*propertyPath:\s*book\s*\r?\n\s*value:[^\r\n]*\r?\n\s*objectReference:\s*\{fileID:\s*\d+,\s*guid:\s*" + escaped + ",");
            });
            Assert.That(bound, Is.True, "Stop before Awake: office must bind the current isolated campaign.");
        }

        void PrepareLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var session in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameSession>(true)))
            {
                Assert.That(session.narrative && BeforeDemolitionCampaign.Is(session.narrative.book), Is.True);
                if (session.input && session.input.Actions) session.input.Actions.devices = new InputDevice[] { keyboard };
                session.narrative.autoLoadNextChapter = false;
                var computer = session.GetComponent<RetroComputer>();
                Assert.That(computer, Is.Not.Null);
                computer.transitionSeconds = .06f; // Runtime-only duration; authored values remain unchanged.
                if (computer.inbox) computer.inbox.ReadFlagRaised += RecordRead;
            }
        }

        void RecordRead(string flag) => readFlags.Add(flag);

        IEnumerator LoadOffice()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return Until(() => GameSession.Instance && GameSession.Instance.narrative.IsInitialized
                && GameSession.Instance.CanControl, "office initialization");
            game = GameSession.Instance; office = game.GetComponent<BeforeDemolitionOfficeFlow>(); pc = game.GetComponent<RetroComputer>();
            Assert.That(office && office.IsActiveOffice && pc && pc.content && pc.seatCameraPose && pc.screenRenderer, Is.True);
            Assert.That(pc.office, Is.SameAs(office)); Assert.That(pc.inbox, Is.SameAs(office.inbox));
            Assert.That(Path.GetDirectoryName(game.save.SavePath), Is.EqualTo(temporaryDirectory).IgnoreCase);
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo(BeforeDemolitionCampaign.OfficeChapterId));
            yield return null;
        }

        IEnumerator Enter()
        {
            Assert.That(pc.Open(), Is.True);
            float deadline=Time.realtimeSinceStartup+25;
            while(!pc.Ready&&Time.realtimeSinceStartup<deadline){if(pc.State.visits>1)pc.TrySkipBoot();yield return null;}
            Assert.That(pc.Ready,Is.True,"The seated desktop must become ready.");
            yield return null;
            Assert.That(pc.Windows,Is.Empty,"Each visit must start with an empty desktop.");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator AuthoredComputerAndChairEnterTheSameLockedFourByThreeDesktop()
        {
            var proxies = UnityEngine.Object.FindObjectsByType<RetroComputerInteraction>(FindObjectsSortMode.None);
            Assert.That(proxies.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(proxies.All(proxy => proxy.computer == pc), Is.True);
            Assert.That(proxies.Any(proxy => proxy.name.IndexOf("cadeira", StringComparison.OrdinalIgnoreCase) >= 0
                || proxy.transform.parent && proxy.transform.parent.name.IndexOf("cadeira", StringComparison.OrdinalIgnoreCase) >= 0), Is.True);
            var physicalPC=UnityEngine.Object.FindObjectsByType<BeforeDemolitionOfficeInteraction>(FindObjectsSortMode.None)
                .Single(item=>item.action==BeforeDemolitionOfficeAction.Computer);
            Assert.That(physicalPC.isActiveAndEnabled,Is.True,"The existing PC interaction remains the physical entry point.");
            physicalPC.Interact(game.interaction);
            yield return Until(()=>pc.Ready,"existing PC interaction entering");
            pc.Close();yield return Until(()=>!pc.IsSeated&&game.CanControl,"leave first world interaction");
            var target = proxies.First(proxy => proxy.CanInteract(game.interaction));
            target.Interact(game.interaction);
            yield return Until(() => pc.Ready, "world proxy entering");
            Assert.That(game.State, Is.EqualTo(GameState.Inspecting));
            Assert.That(game.CanControl || game.player.CanMove, Is.False);
            Assert.That(pc.inbox.IsOpen && pc.inbox.View.gameObject.activeInHierarchy, Is.True);
            Assert.That(pc.inbox.View.GetComponent<Canvas>().enabled, Is.False);
            Assert.That(pc.inbox.View.GetComponent<GraphicRaycaster>().enabled, Is.False);
            Assert.That(pc.ScreenTexture.width, Is.EqualTo(1024)); Assert.That(pc.ScreenTexture.height, Is.EqualTo(768));
            Assert.That(pc.ScreenTexture.antiAliasing, Is.EqualTo(1));
            Assert.That(pc.ScreenCamera.enabled, Is.True); Assert.That(pc.ScreenCamera.allowMSAA, Is.False);
            Assert.That(pc.ScreenCamera.cullingMask, Is.EqualTo(1 << LayerMask.NameToLayer("PCScreen")));
            Assert.That(pc.State.visits, Is.EqualTo(2));
            Assert.That(pc.Windows,Is.Empty);yield return Capture("desktop-empty", true);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator VirtualPointerReadsCanonicalMailRestoresTrashAndOpensExistingEvidence()
        {
            yield return Enter();
            Assert.That(Text(pc.OpenApp("mail")), Does.Contain("6 não lidas"));
            foreach (string id in new[] { "email.01", "email.06", "email.07", "email.04" }) yield return ReadMail(id);
            Assert.That(office.DrawerUnlocked, Is.True);
            Assert.That(office.CanExit, Is.False, "Reading cannot replace collecting the physical whistle.");
            Assert.That(pc.State.Has("desktop.wallpaper"), Is.True);
            Assert.That(pc.Windows.ContainsKey("error.receipt"), Is.True);
            pc.CloseApp("error.receipt");
            var trash = pc.OpenApp("trash"); yield return Click(Button(trash, "Restaurar mensagem"));
            Assert.That(pc.State.Has("trash.email.02") && pc.inbox.State.IsRead("email.02"), Is.True);
            Assert.That(readFlags.Count(flag => flag == "email.02.read"), Is.EqualTo(1));
            Assert.That(pc.inbox.catalog.messages.Length, Is.EqualTo(8));
            Assert.That(pc.inbox.State.deliveredIds.Count, Is.EqualTo(7));
            Assert.That(pc.inbox.State.IsDelivered("email.08"), Is.False);
            var report = pc.content.files.First(file => file.kind == DesktopFileKind.Report);
            Assert.That(report.text, Is.Not.Empty);
            var documents = pc.OpenApp("documents"); yield return Click(Button(documents, report.title));
            Assert.That(Text(documents), Does.Contain(report.text));
            var photo = pc.inbox.catalog.Find("email.07").attachments.Single(attachment => attachment.fileName == "foto_escritorio.jpg");
            Assert.That(photo.preview, Is.Not.Null);
            var images = pc.OpenApp("images", photo.fileName); yield return null;
            Assert.That(images.body.GetComponentsInChildren<RawImage>().Any(image => image.texture == photo.preview), Is.True);
            documents = pc.OpenApp("documents");
            var closedCase = pc.content.files.First(file => file.kind == DesktopFileKind.LockedCase);
            yield return Click(Button(documents, closedCase.title));
            Assert.That(pc.Windows.ContainsKey("documents"), Is.False);
            Assert.That(pc.Windows.ContainsKey("error.case"), Is.True);
            foreach (string id in new[] { "email.01", "email.02", "email.04", "email.06", "email.07" })
                Assert.That(game.interaction.inventory.items.Count(item => item.id == "bd.note." + id), Is.EqualTo(1));
            Assert.That(game.narrative.Progress.sequences, Is.Empty, "Desktop events must not pollute canonical story sequences.");
            yield return Capture("desktop-evidence", false);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator AllEightAppsAndWindowChromeUseTheVirtualPointerAndBrowserStaysLocal()
        {
            yield return Enter();
            string[] ids = { "mail", "documents", "images", "notepad", "media", "browser", "trash", "mines" };
            foreach (string id in ids){Assert.That(pc.OpenApp(id), Is.Not.Null, id);yield return Capture("app-"+id,false);}
            foreach (string id in ids) Assert.That(pc.Windows[id].App, Is.Not.Null, id);
            Assert.That(ids.Select(id => ((RectTransform)pc.Windows[id].TaskButton.transform).anchoredPosition.x)
                .Distinct().Count(), Is.EqualTo(ids.Length), "All eight apps need separate reachable task buttons.");
            var mines = pc.OpenApp("mines");
            Assert.That(mines.IsFocused,Is.True);Assert.That(pc.Windows.Values.Count(window=>window.IsFocused),Is.EqualTo(1));
            var mineButton = mines.body.GetComponentsInChildren<Button>().First(button => ((RectTransform)button.transform).rect.width < 40);
            yield return Click(mineButton, true);
            Assert.That(((DesktopMinesApp)mines.App).Board.Flags, Is.EqualTo(1));
            var mail = pc.OpenApp("mail");
            Assert.That(mail.IsFocused&&!mines.IsFocused,Is.True);Assert.That(mines.transform.Find("Barra de título").GetComponent<Image>().sprite,Is.SameAs(pc.content.inactiveTitleSprite));
            Vector2 originalSize = mail.Rect.sizeDelta;
            yield return Click(Button(mail, "□")); Assert.That(mail.Maximized, Is.True);
            yield return Click(Button(mail, "□")); Assert.That(mail.Maximized, Is.False);
            Assert.That(mail.Rect.sizeDelta, Is.EqualTo(originalSize));
            yield return Click(Button(mail, "_")); Assert.That(mail.Minimized, Is.True);
            yield return Click(mail.TaskButton); Assert.That(mail.Minimized, Is.False);
            Vector2 before = mail.Rect.anchoredPosition;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(pc.ScreenCamera, mail.Rect.TransformPoint(new Vector3(180, -18, 0)));
            pc.Pointer.SetPosition(point); pc.Pointer.Down(false); pc.Pointer.SetPosition(point + new Vector2(18, 8)); pc.Pointer.Up(false);
            Assert.That(Vector2.Distance(before, mail.Rect.anchoredPosition), Is.GreaterThan(1));
            before=mail.Rect.anchoredPosition;point=RectTransformUtility.WorldToScreenPoint(pc.ScreenCamera,mail.Rect.TransformPoint(new Vector3(180,-18,0)));
            pc.Pointer.SetPosition(point);pc.Pointer.Down(false);pc.Pointer.SetPosition(point-new Vector2(6,0));
            for(int step=1;step<=10;step++)pc.Pointer.SetPosition(point-new Vector2(6+step*.2f,0));pc.Pointer.Up(false);
            Assert.That(mail.Rect.anchoredPosition.x,Is.EqualTo(Mathf.Round(before.x-8)),"Pixel snapping must accumulate slow pointer motion instead of losing each subpixel delta.");
            yield return Click(Button(mail, "×")); Assert.That(pc.Windows.ContainsKey("mail"), Is.False);
            var browser = pc.OpenApp("browser"); var app = (DesktopBrowserApp)browser.App;
            yield return Click(Button(browser, "Quatro alunos"));
            Assert.That(app.CurrentAddress, Is.EqualTo("ceep.local/arquivo/quatro-alunos"));
            yield return Click(Button(browser, "Demolição"));
            yield return Click(Button(browser, "<")); Assert.That(app.CurrentAddress, Does.EndWith("quatro-alunos"));
            yield return Click(Button(browser, ">")); Assert.That(app.CurrentAddress, Does.EndWith("demolicao"));
            int history = app.HistoryCount; string address = app.CurrentAddress;
            Assert.That(app.Navigate("https://example.invalid/"), Is.False);
            Assert.That(app.HistoryCount, Is.EqualTo(history)); Assert.That(app.CurrentAddress, Is.EqualTo(address));
            Assert.That(pc.Windows.ContainsKey("error.disk"), Is.True);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator FiveActualVisitsSelectEditableNotesAndUnlockTheSecondMediaTrackOnce()
        {
            var events = new List<string>(); pc.DesktopEvent += events.Add; DesktopMediaApp previousMedia=null;
            for (int visit = 1; visit <= 5; visit++)
            {
                yield return Enter(); Assert.That(pc.State.visits, Is.EqualTo(visit));
                if(previousMedia!=null)Assert.That(previousMedia.IsPlaying,Is.False,"Leaving the computer must stop playback before a clean desktop is shown.");
                var note = pc.OpenApp("notepad"); string text = Text(note);
                foreach (var paragraph in pc.content.notes.Single(item => item.visit == visit).paragraphs)
                    Assert.That(text, Does.Contain(paragraph.text));
                foreach (var label in note.body.GetComponentsInChildren<TMP_Text>().Where(label => label.font == pc.content.childFont))
                    Assert.That(label.richText, Is.False);
                var mediaWindow = pc.OpenApp("media"); var media = (DesktopMediaApp)mediaWindow.App;
                Assert.That(media.PlaylistCount, Is.EqualTo(visit == 1 ? 1 : 2));
                if (visit == 2)
                {
                    media.TogglePlay(); yield return new WaitForSecondsRealtime(.08f);
                    Assert.That(media.PositionSeconds, Is.GreaterThan(0));
                    media.Seek(.5f); Assert.That(media.PositionSeconds, Is.EqualTo(media.DurationSeconds * .5f).Within(.1f));
                    pc.Pause(); float position = media.PositionSeconds; yield return new WaitForSecondsRealtime(.08f);
                    Assert.That(media.PositionSeconds, Is.EqualTo(position).Within(.01f));
                    game.Resume(); yield return null; media.Stop(); Assert.That(media.PositionSeconds, Is.Zero);
                }
                if (visit == 2) yield return new WaitForSecondsRealtime(1.05f);
                previousMedia=media;if(visit==1)media.TogglePlay();
                pc.Close(); yield return Until(() => !pc.IsSeated && game.CanControl, "leave visit " + visit);
            }
            Assert.That(events.Count(id => id == "desktop.cursor"), Is.EqualTo(1));
            Assert.That(events.Count(id => id == "desktop.note"), Is.Zero,"A new visit must not open Notepad automatically.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PauseEscapeRestoresInspectionAndCompositeCheckpointReloadKeepsVisitsAndReads()
        {
            Vector3 bodyPosition = game.player.transform.position;
            yield return Enter(); yield return ReadMail("email.06");
            var trash = pc.OpenApp("trash"); yield return Click(Button(trash, "Restaurar mensagem"));
            yield return Press(Key.F10); Assert.That(game.State, Is.EqualTo(GameState.Paused));
            Assert.That(pc.IsSeated, Is.True); Assert.That(pc.ScreenCamera.enabled, Is.False);
            yield return Press(Key.Escape);
            Assert.That(game.State, Is.EqualTo(GameState.Inspecting), "Escape must resume PC before any backend Escape handler.");
            Assert.That(pc.IsSeated && pc.Ready && pc.inbox.IsOpen && pc.ScreenCamera.enabled, Is.True);
            yield return Press(Key.Escape); yield return Until(() => !pc.IsSeated && game.CanControl, "Escape stand-up");
            Assert.That(pc.ScreenCamera.enabled, Is.False);
            Assert.That(game.player.transform.position, Is.EqualTo(bodyPosition));
            Assert.That(game.player.view.enabled, Is.True);
            Assert.That(game.player.view.playerCamera.fieldOfView, Is.EqualTo(49).Within(.02f));
            Assert.That(game.save.Save(), Is.True);
            Assert.That(SaveSystem.TryReadCheckpoint(game.save.SavePath, out var snapshot, "Chapter1Office", BeforeDemolitionCampaign.StoryId), Is.True);
            string pcJson = snapshot.objects.Single(item => item.id == RetroComputer.StateId).json;
            Assert.That(snapshot.objects.Count(item => item.id == BeforeDemolitionCampaign.FlowStateId), Is.EqualTo(1));
            readFlags.Clear(); PlayerPrefs.SetInt(Prefix + "ResumeRequested", 1);
            yield return LoadOffice();
            Assert.That(pc.CaptureState(), Is.EqualTo(pcJson)); Assert.That(pc.State.visits, Is.EqualTo(1));
            Assert.That(pc.State.Has("trash.email.02") && pc.State.Has("desktop.wallpaper"), Is.True);
            CollectionAssert.AreEquivalent(new[] { "email.02", "email.06" }, pc.inbox.State.readIds);
            Assert.That(pc.inbox.State.IsDelivered("email.08"), Is.False);
            Assert.That(readFlags, Is.Empty, "Reload cannot replay first-read events.");
            Assert.That(pc.IsSeated, Is.False);
            yield return Enter(); Assert.That(pc.State.visits, Is.EqualTo(2));
        }

        IEnumerator ReadMail(string id)
        {
            var mail = pc.OpenApp("mail");
            string subject = pc.inbox.catalog.Find(id).subject;
            yield return Click(mail.body.GetComponentsInChildren<Button>().Single(button =>
                button.GetComponentInChildren<TMP_Text>() && button.GetComponentInChildren<TMP_Text>().text.Split('\n').Contains(subject)));
            Assert.That(pc.inbox.State.IsRead(id), Is.True, id);
            Assert.That(readFlags.Count(flag => flag == id + ".read"), Is.EqualTo(1), id);
        }

        static Button Button(DesktopWindow window, string text) => window.GetComponentsInChildren<Button>(true)
            .Single(button => button.gameObject.activeInHierarchy && button.GetComponentInChildren<TMP_Text>()
                && button.GetComponentInChildren<TMP_Text>().text == text);
        static string Text(DesktopWindow window) => string.Join("\n", window.GetComponentsInChildren<TMP_Text>()
            .Select(label => label.text));

        IEnumerator Click(Button button, bool right = false)
        {
            Canvas.ForceUpdateCanvases(); yield return null;
            var rect = (RectTransform)button.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(pc.ScreenCamera, rect.TransformPoint(rect.rect.center));
            Assert.That(point.x, Is.InRange(0, 1023)); Assert.That(point.y, Is.InRange(0, 767));
            pc.Pointer.SetPosition(point);
            Assert.That(pc.Pointer.Hovered && pc.Pointer.Hovered.GetComponentInParent<Button>() == button, Is.True,
                "The virtual UI raycast must reach the actual visible button: " + button.name);
            pc.Pointer.ClickAt(point, right); yield return null; yield return null;
        }

        IEnumerator Press(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }

        static IEnumerator Until(Func<bool> condition, string description, float seconds = 25)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Timed out: " + description);
        }

        IEnumerator Capture(string name, bool physical)
        {
            Directory.CreateDirectory(VisualDirectory); yield return null; yield return null;
            var previous = RenderTexture.active;
            var image = new Texture2D(1024, 768, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = pc.ScreenTexture; image.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(VisualDirectory, name + ".png"), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(image); }
            if (physical)
            {
                string path = Path.Combine(VisualDirectory, "physical-crt.png");
                if (File.Exists(path)) File.Delete(path);
                ScreenCapture.CaptureScreenshot(path);
                yield return Until(() => File.Exists(path) && new FileInfo(path).Length > 0, "physical CRT capture", 10);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!guarded) yield break;
            SceneManager.sceneLoaded -= PrepareLoaded;
            Time.timeScale = 1; AudioListener.pause = false;
            var loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var blank = SceneManager.CreateScene("CRTDesktopCleanup-" + Guid.NewGuid().ToString("N")); SceneManager.SetActiveScene(blank);
            foreach (var scene in loaded) if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            SaveSystem.ClearChapterCarry(); SaveSystem.TestSaveDirectory = previousDirectory;
            foreach (var pair in preferences)
                if (pair.Value.had) PlayerPrefs.SetInt(Prefix + pair.Key, pair.Value.value); else PlayerPrefs.DeleteKey(Prefix + pair.Key);
            if (hadSaveScene) PlayerPrefs.SetString(Prefix + "SaveScene", savedScene); else PlayerPrefs.DeleteKey(Prefix + "SaveScene");
            PlayerPrefs.Save();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior = inputBackground; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background; Time.timeScale = timeScale; AudioListener.pause = audioPaused;
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
            Assert.That(Path.GetDirectoryName(temporaryDirectory), Is.EqualTo(Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(temporaryDirectory), Does.StartWith("CRTDesktop-"));
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string path = Path.GetFullPath(Path.Combine(temporaryDirectory, BeforeDemolitionCampaign.CheckpointFile) + suffix);
                Assert.That(Path.GetDirectoryName(path), Is.EqualTo(temporaryDirectory).IgnoreCase);
                if (File.Exists(path)) File.Delete(path);
            }
            if (Directory.Exists(temporaryDirectory) && !Directory.EnumerateFileSystemEntries(temporaryDirectory).Any()) Directory.Delete(temporaryDirectory, false);
        }
    }
}
#endif
