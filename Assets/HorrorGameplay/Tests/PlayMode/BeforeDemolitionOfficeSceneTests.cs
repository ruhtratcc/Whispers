// Verifica .

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HorrorGame.Desktop;

namespace HorrorGame.Tests
{
    /// <summary>Focused native-scene input and checkpoint tests; positioning is not a spawn-to-exit walking proof.</summary>
    [Category("BeforeDemolitionOffice")]
    public sealed partial class BeforeDemolitionOfficeSceneTests
    {
        const string ScenePath = "Assets/ChapterOneOffice/Scenes/Chapter1Office.unity";
        const string MenuScenePath = "Assets/HorrorMenu/Scenes/MainMenu.unity";
        const string PreferencePrefix = "BeforeDemolition.";
        const string VisualDirectory = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/JungDesktop20261029/OfficeRegression";
        readonly Dictionary<string, (bool had, int value)> integerPreferences = new Dictionary<string, (bool, int)>();
        readonly List<string> readFlags = new List<string>();
        bool hadSaveScene, guardStarted, background, audioPaused, cursorVisible;
        string saveScene, temporaryDirectory, previousSaveDirectory;
        float timeScale;
        CursorLockMode cursorLock;
        InputSettings.BackgroundBehavior inputBackground;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        Keyboard keyboard, previousKeyboard;
        GameSession game;
        BeforeDemolitionOfficeFlow flow;
        RetroComputer Computer => game ? game.GetComponent<RetroComputer>() : null;
        Quaternion initialClosetRotation;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guardStarted = false;
            integerPreferences.Clear();
            readFlags.Clear();
            Assert.That(UnityEngine.Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None), Is.Empty,
                "Run this suite from a blank scene, before any campaign GameSession.Awake.");
            AssertAuthoredCampaignBeforeLoading();
            previousSaveDirectory = SaveSystem.TestSaveDirectory;
            timeScale = Time.timeScale;
            audioPaused = AudioListener.pause;
            cursorLock = Cursor.lockState;
            cursorVisible = Cursor.visible;
            background = Application.runInBackground;
            inputBackground = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousKeyboard = Keyboard.current;
            foreach (string suffix in new[] { "ResumeRequested", "StoryCarryRequested" })
            {
                string key = PreferencePrefix + suffix;
                integerPreferences[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key, 0));
            }
            hadSaveScene = PlayerPrefs.HasKey(PreferencePrefix + "SaveScene");
            saveScene = PlayerPrefs.GetString(PreferencePrefix + "SaveScene", "");
            guardStarted = true;
            temporaryDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "BeforeDemolitionOffice-" + Guid.NewGuid().ToString("N")));
            // Redirect every checkpoint before loading a scene can invoke GameSession.Awake.
            SaveSystem.TestSaveDirectory = temporaryDirectory; TutorialPreferences.Set(TutorialMode.Off);
            SaveSystem.ClearChapterCarry();
            PlayerPrefs.SetInt(PreferencePrefix + "ResumeRequested", 0);
            PlayerPrefs.SetInt(PreferencePrefix + "StoryCarryRequested", 0);
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Time.timeScale = 1;
            AudioListener.pause = false;
            keyboard = InputSystem.AddDevice<Keyboard>();
            Press();
            SceneManager.sceneLoaded += PrepareLoadedScene;
            yield return LoadOffice();
            initialClosetRotation = flow.closetDoor.localRotation;
        }

        static void AssertAuthoredCampaignBeforeLoading()
        {
            string serialized = File.ReadAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ScenePath));
            string flowGuid = AssetDatabase.AssetPathToGUID("Assets/HorrorGameplay/Scripts/Narrative/BeforeDemolitionOfficeFlow.cs");
            Assert.That(!string.IsNullOrEmpty(flowGuid) && serialized.Contains("guid: " + flowGuid), Is.True,
                "Stop before Awake: the office must contain the new authored flow.");
            bool bound = AssetDatabase.FindAssets("t:StoryBook").Any(assetGuid =>
            {
                var book = AssetDatabase.LoadAssetAtPath<StoryBook>(AssetDatabase.GUIDToAssetPath(assetGuid));
                if (!book || !BeforeDemolitionCampaign.Is(book) || book.revision != BeforeDemolitionCampaign.Revision
                    || book.ChapterIndex(BeforeDemolitionCampaign.OfficeChapterId) < 0) return false;
                string guid = Regex.Escape(assetGuid);
                return Regex.IsMatch(serialized, @"(?m)^\s*book:\s*\{fileID:\s*\d+,\s*guid:\s*" + guid + ",")
                    || Regex.IsMatch(serialized, @"(?m)^\s*propertyPath:\s*book\s*\r?\n\s*value:[^\r\n]*\r?\n\s*objectReference:\s*\{fileID:\s*\d+,\s*guid:\s*" + guid + ",");
            });
            Assert.That(bound, Is.True, "Stop before Awake: no serialized revision-3 campaign book binding was found.");
        }

        void PrepareLoadedScene(Scene scene, LoadSceneMode mode)
        {
            foreach (var session in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameSession>(true)))
            {
                Assert.That(session.narrative && BeforeDemolitionCampaign.Is(session.narrative.book), Is.True);
                if (session.input && session.input.Actions) session.input.Actions.devices = new InputDevice[] { keyboard };
                session.narrative.autoLoadNextChapter = false;
                if (session.narrative.transitionView)
                {
                    session.narrative.transitionView.fadeSeconds = .03f;
                    session.narrative.transitionView.cardSeconds = .05f;
                }
                var office = session.GetComponent<BeforeDemolitionOfficeFlow>();
                if (office && office.inbox) office.inbox.ReadFlagRaised += RecordReadFlag;
                var computer = session.GetComponent<RetroComputer>();
                if (computer) computer.transitionSeconds = .06f;
            }
        }

        void RecordReadFlag(string flag) => readFlags.Add(flag);

        IEnumerator LoadOffice()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return Until(() => GameSession.Instance && GameSession.Instance.narrative
                && GameSession.Instance.narrative.IsInitialized && GameSession.Instance.CanControl, "New office initialization", 25);
            game = GameSession.Instance;
            flow = game.GetComponent<BeforeDemolitionOfficeFlow>();
            Assert.That(flow && flow.IsActiveOffice, Is.True);
            Assert.That(game.narrative.Progress.storyId, Is.EqualTo(BeforeDemolitionCampaign.StoryId));
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo(BeforeDemolitionCampaign.OfficeChapterId));
            Assert.That(Path.GetDirectoryName(game.save.SavePath), Is.EqualTo(temporaryDirectory).IgnoreCase);
            Assert.That(Path.GetFileName(game.save.SavePath), Is.EqualTo(BeforeDemolitionCampaign.CheckpointFile));
            Assert.That(flow.inbox && flow.secondDrawer && flow.closetDoor && flow.whistleVisual && flow.whistleData, Is.True);
            game.player.view.bobEnabled = game.player.view.swayEnabled = false;
            yield return Until(() => InspectionManager.Instance && InspectionManager.Instance.CameraController,
                "Native inspection installation");
            yield return null;
        }

        void Press(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        IEnumerator KeyPress(Key key)
        {
            Press(key);
            yield return null;
            yield return null;
            Press();
            yield return null;
            yield return null;
        }

        static IEnumerator Until(Func<bool> condition, string message, float seconds = 8)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }

        BeforeDemolitionOfficeInteraction Interaction(BeforeDemolitionOfficeAction action) =>
            UnityEngine.Object.FindObjectsByType<BeforeDemolitionOfficeInteraction>(FindObjectsSortMode.None)
                .Single(value => value.flow == flow && value.action == action);

        static Collider TargetCollider(BeforeDemolitionOfficeInteraction target)
        {
            var own = target.GetComponent<Collider>();
            return own ? own : target.GetComponentsInChildren<Collider>()
                .First(value => value.enabled && !value.isTrigger
                    && value.GetComponentInParent<BeforeDemolitionOfficeInteraction>() == target);
        }

        IEnumerator AimAt(BeforeDemolitionOfficeAction action, Vector3 feet)
        {
            var target = Interaction(action);
            var collider = TargetCollider(target);
            Assert.That(collider && collider.enabled && !collider.isTrigger, Is.True);
            Vector3 delta = (collider.bounds.center - (feet + Vector3.up * game.player.EyeHeight)).normalized;
            game.player.Warp(feet, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, -Mathf.Asin(delta.y) * Mathf.Rad2Deg);
            Physics.SyncTransforms();
            yield return Until(() =>
            {
                var selected = game.interaction.Target as MonoBehaviour;
                return selected && selected.GetComponentInParent<BeforeDemolitionOfficeInteraction>() == target;
            }, "Normal ray must select " + action);
        }

        IEnumerator OpenComputerWithE()
        {
            yield return AimAt(BeforeDemolitionOfficeAction.Computer, new Vector3(.85f, .05f, 2.35f));
            yield return KeyPress(Key.E);
            yield return Until(() => Computer && Computer.Ready && flow.inbox.IsOpen && game.State == GameState.Inspecting,
                "E seats the player at the native CRT desktop");
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(game.CanControl, Is.False);
        }

        static void PointerClick(Button button)
        {
            Assert.That(button && button.IsActive() && button.IsInteractable(), Is.True);
            Assert.That(EventSystem.current, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
                eligibleForClick = true
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            var selected = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(selected, Is.EqualTo(button.gameObject), "The authored UI row is occluded.");
            ExecuteEvents.Execute(selected, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(selected, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(ExecuteEvents.Execute(selected, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        IEnumerator ReadEmail(string id)
        {
            var inbox = flow.inbox;
            if(inbox.ChronologicalOfficeReading&&id.StartsWith("email.0")&&int.TryParse(id.Substring(7),out int number))
                for(int previous=1;previous<number;previous++)if(!inbox.State.IsRead("email.0"+previous))yield return ReadEmail("email.0"+previous);
            Assert.That(inbox.IsOpen, Is.True);
            Assert.That(Computer && Computer.Ready, Is.True);
            Assert.That(inbox.State.IsDelivered(id), Is.True);
            var window = Computer.OpenApp("mail");
            string subject = inbox.catalog.Find(id).subject;
            var button = window.body.GetComponentsInChildren<Button>().Single(value =>
                value.GetComponentsInChildren<TMP_Text>().Any(label=>label.text.Split('\n').Contains(subject)));
            var scroll = button.GetComponentInParent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            var rows = scroll.content.GetComponentsInChildren<Button>();
            int index = Array.IndexOf(rows, button);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            scroll.verticalNormalizedPosition = 1 - (float)index / Mathf.Max(1, rows.Length - 1);
            scroll.StopMovement();
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return ComputerPointerClick(button);
            Assert.That(inbox.CurrentMessageId, Is.EqualTo(id));
            if(Computer.Windows.TryGetValue("error.receipt",out var receipt)&&receipt)
                yield return ComputerPointerClick(receipt.body.GetComponentsInChildren<Button>().First());
            yield return Until(()=>inbox.State.IsRead(id),"Visible email finishes minimum reading time",6);
        }

        IEnumerator ComputerPointerClick(Button button)
        {
            Assert.That(button && button.IsActive() && button.IsInteractable(), Is.True);
            Assert.That(Computer && Computer.Ready && Computer.ScreenCamera, Is.True);
            Canvas.ForceUpdateCanvases();
            yield return null;
            var rect = (RectTransform)button.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(Computer.ScreenCamera, rect.TransformPoint(rect.rect.center));
            Assert.That(point.x, Is.InRange(0, 1023));
            Assert.That(point.y, Is.InRange(0, 767));
            Computer.Pointer.SetPosition(point);
            Assert.That(Computer.Pointer.Hovered && Computer.Pointer.Hovered.GetComponentInParent<Button>() == button,
                Is.True, "The native CRT virtual raycast must reach the visible row or attachment.");
            Computer.Pointer.ClickAt(point);
            yield return null;
            yield return null;
        }

        IEnumerator CloseInboxWithEscape()
        {
            Assert.That(Computer && Computer.IsSeated, Is.True);
            yield return KeyPress(Key.Escape);
            yield return Until(() => !Computer.IsSeated && !flow.inbox.IsOpen && game.CanControl,
                "Escape completes the seat return blend and restores control");
        }

        void Capture(string state)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("HorrorGame.Editor.PresentationCapture"))
                .FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, "Use the existing native URP presentation capture helper.");
            var method = type.GetMethod("Capture", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            string path = Path.Combine(VisualDirectory, "office-" + state + "-1280x720.png");
            method.Invoke(null, new object[] { SceneManager.GetActiveScene(), game.player.view.playerCamera,
                path, 1280, 720, null, true });
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(1000));
        }

        IEnumerator CaptureEmailSevenAndRealAttachment()
        {
            var inbox = flow.inbox;
            Assert.That(inbox.CurrentMessageId, Is.EqualTo("email.07"));
            yield return null;
            Canvas.ForceUpdateCanvases();
            Capture("email-07-reader");
            var mail = Computer.Windows["mail"];
            string firstParagraph = inbox.catalog.Find("email.07").paragraphs[0].text;
            var bodyScroll = mail.body.GetComponentsInChildren<ScrollRect>().Single(scroll =>
                scroll.content.GetComponentsInChildren<TMP_Text>().Any(label => label.text == firstParagraph));
            bodyScroll.verticalNormalizedPosition = 0;
            bodyScroll.StopMovement();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Capture("email-07-reader-bottom");
            var photo = inbox.catalog.Find("email.07").attachments.Single(attachment => attachment.fileName == "foto_escritorio.jpg");
            Assert.That(!photo.unavailable && photo.preview, Is.True, "The authored closet photo must be a real available image.");
            var button = mail.body.GetComponentsInChildren<Button>()
                .Single(value => value.GetComponentInChildren<TMP_Text>(true).text == photo.fileName);
            yield return ComputerPointerClick(button);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(Computer.Windows.ContainsKey("images"), Is.True);
            var image = Computer.Windows["images"].body.GetComponentsInChildren<RawImage>()
                .Single(value => value.texture == photo.preview);
            Assert.That(image.gameObject.activeInHierarchy, Is.True);
            Assert.That(image.texture, Is.SameAs(photo.preview));
            Assert.That(image.GetComponent<AspectRatioFitter>().aspectRatio,
                Is.EqualTo((float)photo.preview.width / photo.preview.height).Within(.001f));
            Capture("email-07-closet-photo");
        }

        IEnumerator CaptureWhistleInspectionGpu(InspectionManager inspector)
        {
            yield return null;
            yield return null;
            var target = inspector.Texture;
            Assert.That(target && target.IsCreated(), Is.True, "The real inspection camera must render to its texture.");
            var previous = RenderTexture.active;
            Texture2D pixels = null;
            int visiblePixels = 0;
            int totalPixels = target.width * target.height;
            string texturePath = Path.Combine(VisualDirectory, "whistle-inspection-texture.png");
            string screenshotPath = Path.Combine(VisualDirectory, "whistle-inspection-screen.png");
            try
            {
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                visiblePixels = pixels.GetPixels32().Count(pixel => pixel.a > 25);
                Directory.CreateDirectory(VisualDirectory);
                File.WriteAllBytes(texturePath, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
            }
            Debug.Log("Whistle inspection GPU pixels with alpha > 0.1: " + visiblePixels + "/" + totalPixels);
            if (File.Exists(screenshotPath)) File.Delete(screenshotPath);
            ScreenCapture.CaptureScreenshot(screenshotPath);
            // End-of-frame yield instructions can hang when the Editor loses focus.
            for (int frame = 0; frame < 10 && !File.Exists(screenshotPath); frame++) yield return null;
            Assert.That(File.Exists(screenshotPath), Is.True, "The Game View screenshot must finish within ten frames.");
            Assert.That(new FileInfo(screenshotPath).Length, Is.GreaterThan(1000));
            Assert.That(visiblePixels, Is.GreaterThan(totalPixels * .01f),
                "The whistle must cover more than 1% of the actual inspection texture; visible pixels: " + visiblePixels + "/" + totalPixels);
        }

        IEnumerator OpenDrawerAndInspectWhistleWithE(bool capture = false, bool inspectPixels = false)
        {
            Vector3 feet = new Vector3(flow.secondDrawer.drawer.position.x, .05f, 2.40f);
            yield return AimAt(BeforeDemolitionOfficeAction.SecondDrawer, feet);
            yield return KeyPress(Key.E);
            yield return Until(() => flow.secondDrawer.IsOpen && flow.secondDrawer.OpenFraction >= .95f, "E opens the second native drawer");
            yield return AimAt(BeforeDemolitionOfficeAction.Whistle, feet);
            if (capture)
            {
                yield return Until(() => flow.secondDrawer.OpenFraction >= .999f, "Capture the fully opened second drawer");
                Capture("drawer-open-before-whistle");
            }
            yield return KeyPress(Key.E);
            var inspector = InspectionManager.Instance;
            yield return Until(() => inspector.IsOpen && inspector.Alpha > .99f, "E starts the real whistle inspector");
            Assert.That(inspector.CurrentItem.itemId, Is.EqualTo(BeforeDemolitionCampaign.WhistleItemId));
            Assert.That(flow.HasWhistle, Is.False, "Starting inspection alone cannot collect the whistle.");
            if (inspectPixels) yield return CaptureWhistleInspectionGpu(inspector);
            if (capture) Capture("whistle-inspection");
            yield return KeyPress(Key.E);
            yield return Until(() => !inspector.IsOpen && !inspector.IsCompleting && game.CanControl, "E completes inspection and restores control");
            Assert.That(flow.HasWhistle, Is.True);
            Assert.That(flow.whistleVisual.activeInHierarchy, Is.False);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator NativeComputerEAndEmailSixUnlockOnlyTheChosenSecondDrawer()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<PrologueFlow>(FindObjectsSortMode.None)
                .Where(value => value.isActiveAndEnabled), Is.Empty);
            Assert.That(UnityEngine.Object.FindObjectsByType<OfficeEndingFlow>(FindObjectsSortMode.None)
                .Where(value => value.isActiveAndEnabled), Is.Empty);
            Assert.That(flow.secondDrawer.drawer.name, Is.EqualTo("Gaveta 0.8/1"));
            Assert.That(flow.inbox.IsOpen, Is.False);
            Assert.That(flow.inbox.State.deliveredIds.Count, Is.EqualTo(7));
            Assert.That(flow.inbox.State.IsDelivered("email.08"), Is.False);
            yield return AimAt(BeforeDemolitionOfficeAction.SecondDrawer, new Vector3(-.8f, .05f, 2.40f));
            yield return KeyPress(Key.E);
            Assert.That(flow.secondDrawer.IsOpen, Is.False);
            Assert.That(game.hud.notification.text, Does.Contain("computador"));
            yield return OpenComputerWithE();
            yield return ReadEmail("email.04");
            Assert.That(flow.DrawerUnlocked, Is.False, "Email 4 must not unlock the drawer.");
            yield return ReadEmail("email.06");
            Assert.That(flow.DrawerUnlocked, Is.True);
            Assert.That(Quaternion.Angle(initialClosetRotation, flow.closetDoor.localRotation), Is.LessThan(.01f));
            yield return ReadEmail("email.07");
            yield return CloseInboxWithEscape();
            var objectives = game.GetComponent<ObjectiveService>();
            Assert.That(objectives, Is.Not.Null);
            yield return Until(() => objectives.Visible && objectives.CurrentObjectiveId == "bd.office.drawer",
                "The persistent objective changes after reading the emails and leaving the PC.");
            Assert.That(objectives.CurrentTitle, Is.EqualTo("Pegue o que estiver dentro da segunda gaveta da mesa."));
            Assert.That(game.hud.objective.text, Is.EqualTo(objectives.CurrentTitle));
            yield return AimAt(BeforeDemolitionOfficeAction.SecondDrawer, new Vector3(-.8f, .05f, 2.40f));
            yield return KeyPress(Key.E);
            yield return Until(() => flow.secondDrawer.OpenFraction >= .65f, "The selected middle drawer actually moves");
            Assert.That(flow.CanExit, Is.False);
            Assert.That(game.interaction.inventory.items.Count(item => item.id == "bd.note.email.06"), Is.EqualTo(1));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator WhistleRequiresTheActualDrawerOpeningAndRealInspectionCompletion()
        {
            yield return OpenComputerWithE();
            yield return ReadEmail("email.06");
            yield return CloseInboxWithEscape();
            Assert.That(flow.CanCollectWhistle, Is.False);
            Assert.That(flow.CollectWhistle(game.interaction), Is.False, "A closed drawer cannot grant the whistle through a direct call either.");
            yield return OpenDrawerAndInspectWhistleWithE(inspectPixels: true);
            var item = game.interaction.inventory.items.Single(value => value.id == BeforeDemolitionCampaign.WhistleItemId);
            Assert.That(item.inspectionId, Is.EqualTo(flow.whistleData.itemId));
            Assert.That(item.inspectionCount, Is.EqualTo(1));
            Assert.That(flow.CollectWhistle(game.interaction), Is.False);
            Assert.That(game.interaction.inventory.Contains("office_equipment"), Is.False);
            Assert.That(game.save.flashlight.IsAvailable, Is.False);
            Assert.That(game.narrative.HasSignal(BeforeDemolitionCampaign.ReadySignal), Is.False);
            Assert.That(game.save.Save(), Is.True);
            Assert.That(File.Exists(game.save.SavePath), Is.True);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator OnlyReadsOneSixSevenAndWhistlePermitAnExplicitExitE()
        {
            int ready = 0, exited = 0;
            game.narrative.SignalAccepted += signal =>
            {
                if (signal == BeforeDemolitionCampaign.ReadySignal) ready++;
                if (signal == BeforeDemolitionCampaign.ExitSignal) exited++;
            };
            yield return OpenComputerWithE();
            yield return ReadEmail("email.06");
            yield return CloseInboxWithEscape();
            yield return OpenDrawerAndInspectWhistleWithE(true);
            yield return OpenComputerWithE();
            yield return ReadEmail("email.07");
            Assert.That(Quaternion.Angle(initialClosetRotation, flow.closetDoor.localRotation), Is.EqualTo(18).Within(.1f));
            yield return CaptureEmailSevenAndRealAttachment();
            yield return CloseInboxWithEscape();
            yield return AimAt(BeforeDemolitionOfficeAction.Exit, new Vector3(-2.08f, .05f, -3.85f));
            yield return KeyPress(Key.E);
            Assert.That(exited, Is.Zero);
            Assert.That(game.hud.notification.text, Does.Contain("emails"));
            yield return OpenComputerWithE();
            yield return ReadEmail("email.01");
            yield return CloseInboxWithEscape();
            yield return null;
            yield return null;
            Assert.That(flow.CanExit, Is.True);
            Assert.That(ready, Is.EqualTo(1));
            Assert.That(exited, Is.Zero, "Reading and pickup must not complete the exit automatically.");
            foreach (string optional in new[] { "email.02", "email.03", "email.04", "email.05" })
                Assert.That(flow.inbox.State.IsRead(optional), Is.False);
            Assert.That(game.narrative.NextSignal, Is.EqualTo(BeforeDemolitionCampaign.ExitSignal));
            yield return AimAt(BeforeDemolitionOfficeAction.Exit, new Vector3(-2.08f, .05f, -3.85f));
            game.narrative.autoLoadNextChapter = true;
            yield return KeyPress(Key.E);
            yield return Until(() => game.narrative.HasSignal(BeforeDemolitionCampaign.ExitSignal), "Only E accepts the explicit exit");
            string path = game.save.SavePath;
            yield return Until(() => GameSession.Instance && GameSession.Instance.narrative.IsInitialized
                && GameSession.Instance.narrative.Progress.chapterId == BeforeDemolitionCampaign.RoadChapterId
                && SceneManager.GetActiveScene().name == "RoadJourney", "The approved next chapter loads after explicit E", 25);
            game = GameSession.Instance; flow = null;
            Assert.That(game.narrative.Progress.endingSeen, Is.False);
            Assert.That(game.GetComponent<BeforeDemolitionRoadFlow>(), Is.Not.Null);
            Assert.That(game.interaction.inventory.Contains(BeforeDemolitionCampaign.WhistleItemId), Is.True);
            Assert.That(SaveSystem.TryReadCheckpoint(path, out var exitedCheckpoint,
                "Chapter1Office", BeforeDemolitionCampaign.StoryId), Is.True, "The completed office must have a valid on-disk checkpoint.");
            Assert.That(exitedCheckpoint.narrative.HasSignal(BeforeDemolitionCampaign.ExitSignal), Is.True);
            Assert.That(exitedCheckpoint.narrative.completedChapters, Does.Contain(BeforeDemolitionCampaign.OfficeChapterId));
            Assert.That(exitedCheckpoint.narrative.pendingChapterId, Is.EqualTo(BeforeDemolitionCampaign.RoadChapterId));
            Assert.That(exitedCheckpoint.narrative.endingSeen, Is.False);
            Capture("road-after-explicit-office-exit");
            Assert.That(exited, Is.EqualTo(1));
            Assert.That(game.narrative.Progress.completedChapters, Does.Contain(BeforeDemolitionCampaign.OfficeChapterId));
            Assert.That(game.narrative.Progress.pendingChapterId, Is.Empty);
            Assert.That(game.narrative.Progress.endingSeen, Is.False);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("RoadJourney"));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CompositeCheckpointReloadAndRepeatedRestoreKeepWorldAndDiaryWithoutReadReplay()
        {
            yield return OpenComputerWithE();
            yield return ReadEmail("email.06");
            yield return CloseInboxWithEscape();
            yield return OpenDrawerAndInspectWhistleWithE();
            yield return OpenComputerWithE();
            yield return ReadEmail("email.07");
            yield return CloseInboxWithEscape();
            Assert.That(game.save.Save(), Is.True);
            Assert.That(SaveSystem.TryReadCheckpoint(game.save.SavePath, out var snapshot, "Chapter1Office", BeforeDemolitionCampaign.StoryId), Is.True);
            string savedFlow = snapshot.objects.Single(value => value.id == BeforeDemolitionCampaign.FlowStateId).json;
            readFlags.Clear();
            PlayerPrefs.SetInt(PreferencePrefix + "ResumeRequested", 1);
            yield return LoadOffice();
            Assert.That(flow.CaptureState(), Is.EqualTo(savedFlow));
            for (int restore = 0; restore < 2; restore++)
            {
                Assert.That(game.save.RestoreSnapshot(snapshot), Is.True);
                game.narrative.InitializeAfterSession(true);
                yield return null;
                Assert.That(flow.DrawerUnlocked && flow.secondDrawer.IsOpen && flow.HasWhistle, Is.True);
                Assert.That(flow.secondDrawer.OpenFraction, Is.EqualTo(1).Within(.01f));
                Assert.That(flow.whistleVisual.activeInHierarchy, Is.False);
                Assert.That(Quaternion.Angle(initialClosetRotation, flow.closetDoor.localRotation), Is.EqualTo(18).Within(.1f));
                Assert.That(flow.CanExit, Is.False, "Email 1 is still unread after restoring.");
                foreach (string id in new[] { "bd.note.email.06", "bd.note.email.07", BeforeDemolitionCampaign.WhistleItemId })
                    Assert.That(game.interaction.inventory.items.Count(item => item.id == id), Is.EqualTo(1));
                Assert.That(game.interaction.inventory.items.Single(item => item.id == "bd.note.email.07").text,
                    Is.EqualTo(flow.inbox.catalog.Find("email.07").PlainText));
            }
            Assert.That(readFlags, Is.Empty, "Restoring state cannot replay first-read events.");
            Assert.That(game.narrative.HasSignal(BeforeDemolitionCampaign.ExitSignal), Is.False);
            CollectionAssert.AreEquivalent(new[] { "email.06", "email.07" }, flow.inbox.State.readIds);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator MainMenuContinueUsesTheNewCatalogAndResumesTheIsolatedOfficeCheckpoint()
        {
            // Fail before the menu's Awake can select storage from a missing catalog.
            string menuSource = File.ReadAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, MenuScenePath));
            var catalogBinding = Regex.Matches(menuSource, @"(?m)^\s*gameplayScenes:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32}),");
            Assert.That(catalogBinding.Count, Is.EqualTo(1), "The saved menu must bind its campaign catalog before it can be opened.");
            var authoredCatalog = AssetDatabase.LoadAssetAtPath<SceneCatalog>(
                AssetDatabase.GUIDToAssetPath(catalogBinding[0].Groups[1].Value));
            Assert.That(authoredCatalog && authoredCatalog.storyBook && BeforeDemolitionCampaign.Is(authoredCatalog.storyBook), Is.True);
            Assert.That(authoredCatalog.storyBook.revision, Is.EqualTo(BeforeDemolitionCampaign.Revision));
            Assert.That(authoredCatalog.mainMenuScene, Is.EqualTo("MainMenu"));
            Assert.That(authoredCatalog.gameplayScene, Is.EqualTo("Chapter1Office"));
            Assert.That(game.scenes && game.scenes.mainMenuScene == authoredCatalog.mainMenuScene, Is.True);

            yield return OpenComputerWithE();
            yield return ReadEmail("email.06");
            yield return ReadEmail("email.07");
            yield return CloseInboxWithEscape();
            Assert.That(flow.HasWhistle || flow.CanExit, Is.False);
            Assert.That(game.save.Save(), Is.True);
            string checkpointPath = game.save.SavePath;
            Assert.That(SaveSystem.TryReadCheckpoint(checkpointPath, out _, "Chapter1Office", BeforeDemolitionCampaign.StoryId), Is.True);
            bool calibratedHad = PlayerPrefs.HasKey("HorrorMenu.Calibrated");
            int calibratedValue = PlayerPrefs.GetInt("HorrorMenu.Calibrated", 0);
            try
            {
                // This regression exercises Continue independently of the first-run brightness panel.
                PlayerPrefs.SetInt("HorrorMenu.Calibrated", 1);
                game.ReturnToMenu();
                yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu" && !GameSession.Instance,
                    "The real office ReturnToMenu must unload its GameSession", 25);
                var menu = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                    .Single(component => component.GetType().FullName == "SchoolMenu.MainMenuController");
                var serializedMenu = new SerializedObject(menu);
                var catalogProperty = serializedMenu.FindProperty("gameplayScenes");
                Assert.That(catalogProperty, Is.Not.Null);
                var liveCatalog = catalogProperty.objectReferenceValue as SceneCatalog;
                Assert.That(liveCatalog && liveCatalog.storyBook, Is.True, "The live menu must retain its catalog after scene loading.");
                Assert.That(liveCatalog.storyBook.storyId, Is.EqualTo(BeforeDemolitionCampaign.StoryId));
                Assert.That(liveCatalog.storyBook.revision, Is.EqualTo(BeforeDemolitionCampaign.Revision));
                Assert.That(SaveSystem.TestSaveDirectory, Is.EqualTo(temporaryDirectory).IgnoreCase);
                var continueButton = serializedMenu.FindProperty("continueButton").objectReferenceValue as Button;
                Assert.That(continueButton, Is.Not.Null);
                var busy = menu.GetType().GetField("busy", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(busy, Is.Not.Null);
                // Start is a coroutine; Awake's initial busy=false is not the finished intro.
                yield return null;
                yield return Until(() => !(bool)busy.GetValue(menu), "Menu introduction must release its controls", 15);
                Assert.That(continueButton.gameObject.activeInHierarchy, Is.True, "Continue must recognize the isolated new-story checkpoint.");
                readFlags.Clear();
                PointerClick(continueButton);
                yield return Until(() => SceneManager.GetActiveScene().name == "Chapter1Office" && GameSession.Instance
                    && GameSession.Instance.narrative && GameSession.Instance.narrative.IsInitialized && GameSession.Instance.CanControl,
                    "The real Continue button must reload the saved office", 25);
                game = GameSession.Instance;
                flow = game.GetComponent<BeforeDemolitionOfficeFlow>();
                Assert.That(flow && flow.IsActiveOffice, Is.True);
                Assert.That(game.narrative.Progress.storyId, Is.EqualTo(BeforeDemolitionCampaign.StoryId));
                Assert.That(game.narrative.Progress.chapterId, Is.EqualTo(BeforeDemolitionCampaign.OfficeChapterId));
                Assert.That(SaveSystem.TestSaveDirectory, Is.EqualTo(temporaryDirectory).IgnoreCase);
                Assert.That(game.save.SavePath, Is.EqualTo(checkpointPath).IgnoreCase);
                CollectionAssert.AreEquivalent(new[] { "email.06", "email.07" }, flow.inbox.State.readIds);
                Assert.That(flow.HasWhistle || flow.CanExit, Is.False);
                Assert.That(game.interaction.inventory.items.Count(item => item.id.StartsWith("bd.note.email.", StringComparison.Ordinal)), Is.EqualTo(2));
                foreach (string id in new[] { "bd.note.email.06", "bd.note.email.07" })
                    Assert.That(game.interaction.inventory.items.Count(item => item.id == id), Is.EqualTo(1));
                Assert.That(readFlags, Is.Empty, "Continue restores read state without replaying read events.");
            }
            finally
            {
                if (calibratedHad) PlayerPrefs.SetInt("HorrorMenu.Calibrated", calibratedValue);
                else PlayerPrefs.DeleteKey("HorrorMenu.Calibrated");
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator OfficeHasNoPortableLightBeforeCollectingTheEquipment()
        {
            yield return null;
            var lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var report=lights.Select(l=>AnimationUtility.CalculateTransformPath(l.transform,null)+" | enabled="+l.enabled+" active="+l.gameObject.activeInHierarchy+" intensity="+l.intensity+" range="+l.range+" type="+l.type+" pos="+l.transform.position).ToArray();
            Directory.CreateDirectory("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/work/OfficeRoadFixes20261030");
            File.WriteAllLines("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/work/OfficeRoadFixes20261030/office-runtime-lights.txt",report);
            Assert.That(game.save.flashlight.IsAvailable,Is.False);
            Assert.That(game.save.flashlight.lamp.enabled&&game.save.flashlight.lamp.gameObject.activeInHierarchy,Is.False);
            Assert.That(game.save.flashlight.handRig.gameObject.activeInHierarchy,Is.False);
            game.player.view.enabled=false;game.player.Warp(new Vector3(-1.3f,.04f,2.1f),90,30);
            game.player.view.playerCamera.transform.LookAt(new Vector3(0,.6f,2.2f));yield return null;yield return null;
            ScreenCapture.CaptureScreenshot("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/work/OfficeRoadFixes20261030/office-chair-fixed.png");yield return null;yield return null;
            var chair=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(t=>t.name=="Cadeira do PC — Poly Haven");
            Assert.That(chair.gameObject.activeInHierarchy,Is.True);
            var retired=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(t=>t.name=="Modelo Sketchfab — stylish-office-chair-game-ready-asset");
            Assert.That(retired.gameObject.activeSelf,Is.False,"The chair responsible for the under-seat glare is retired.");
        }

        [UnityTest]
        public IEnumerator OfficeExitStaysClosedEvenWhenAnOlderCheckpointHadOpenedIt()
        {
            var door=flow.exitDoor;Assert.That(door,Is.Not.Null);
            door.RestoreState("{\"open\":true,\"locked\":false}");flow.RestoreWorldSilently();yield return null;
            Assert.That(door.IsOpen,Is.False);Assert.That(door.enabled,Is.False);
            var exit=door.leafCollider.GetComponent<BeforeDemolitionOfficeInteraction>();
            Assert.That(exit&&exit.action==BeforeDemolitionOfficeAction.Exit,Is.True,"The actual leaf ray target exits directly instead of swinging.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!guardStarted) yield break;
            SceneManager.sceneLoaded -= PrepareLoadedScene;
            if (flow && flow.inbox) flow.inbox.Close();
            var loaded = new List<Scene>();
            for (int index = 0; index < SceneManager.sceneCount; index++) loaded.Add(SceneManager.GetSceneAt(index));
            var blank = SceneManager.CreateScene("BeforeDemolitionOfficeCleanup-" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(blank);
            Time.timeScale = 1;
            AudioListener.pause = false;
            foreach (var scene in loaded) if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            SaveSystem.ClearChapterCarry();
            SaveSystem.TestSaveDirectory = previousSaveDirectory;
            foreach (var preference in integerPreferences)
                if (preference.Value.had) PlayerPrefs.SetInt(preference.Key, preference.Value.value);
                else PlayerPrefs.DeleteKey(preference.Key);
            if (hadSaveScene) PlayerPrefs.SetString(PreferencePrefix + "SaveScene", saveScene);
            else PlayerPrefs.DeleteKey(PreferencePrefix + "SaveScene");
            PlayerPrefs.Save();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior = inputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background;
            Time.timeScale = timeScale;
            AudioListener.pause = audioPaused;
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
            Assert.That(Path.GetDirectoryName(temporaryDirectory), Is.EqualTo(Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(temporaryDirectory), Does.StartWith("BeforeDemolitionOffice-"));
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string path = Path.GetFullPath(Path.Combine(temporaryDirectory, BeforeDemolitionCampaign.CheckpointFile) + suffix);
                Assert.That(Path.GetDirectoryName(path), Is.EqualTo(temporaryDirectory).IgnoreCase);
                if (File.Exists(path)) File.Delete(path);
            }
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, false);
        }
    }
}
#endif
