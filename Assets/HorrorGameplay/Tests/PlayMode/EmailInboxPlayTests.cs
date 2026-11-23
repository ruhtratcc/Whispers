// Verifica SetUp, PointerReadsOutOfOrderOnceAndDeliveryAndRestartKeepTheSameInbox, EscapeClosesOneLayerAndBothDevicesPreserveCallerTimeAndCursor, UnavailablePdfsKeepTheReaderAndDiagnosticImageKeepsItsAspectRatio, LongChildWritingUsesTheApprovedFontAndScrollsWithoutChangingText, DesktopAndPhoneFitThreeResolutionsWithNativeCaptures, ImmediateReopenAndExternalDisableReleaseCursorAndInspectionOwnership, FailedWriteKeepsReadFlagsAndRetryButtonSavesAfterUnlock, TearDown.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorGame.Tests
{
    [Category("BeforeDemolitionEmailUI")]
    public sealed class EmailInboxPlayTests
    {
        const string VisualDirectory = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/BeforeDemolitionPhaseB20261026/Visual";
        EmailCatalog catalog;
        TMP_FontAsset normalFont, childFont;
        EmailInboxController inbox;
        Texture2D image;
        Camera camera;
        Scene fixtureScene, previousScene;
        Keyboard keyboard, previousKeyboard;
        string temporaryDirectory, progressPath;
        float timeScale;
        bool background, cursorVisible, audioPaused;
        CursorLockMode cursorLock;
        InputSettings.BackgroundBehavior inputBackground;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;

        [SetUp]
        public void SetUp()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None), Is.Empty,
                "Run this isolated UI suite from a blank scene; it must not start a campaign session.");
            timeScale = Time.timeScale; audioPaused = AudioListener.pause;
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            background = Application.runInBackground;
            inputBackground = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousKeyboard = Keyboard.current;
#if UNITY_EDITOR
            var source = AssetDatabase.LoadAssetAtPath<EmailCatalog>("Assets/HorrorGameplay/Email/BeforeDemolitionEmails.asset");
            normalFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/HorrorMenu/TMP/Resources/Fonts & Materials/LiberationSans SDF.asset");
            childFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/HorrorGameplay/Email/Fonts/PatrickHand SDF.asset");
            Assert.That(source, Is.Not.Null, "Create the approved native email catalog before running this suite.");
            Assert.That(normalFont, Is.Not.Null);
            Assert.That(childFont, Is.Not.Null);
            Assert.That(childFont, Is.Not.SameAs(normalFont), "Child writing requires its own handwriting font.");
            catalog = UnityEngine.Object.Instantiate(source);
#else
            Assert.Fail("These native-asset presentation tests run in Editor PlayMode.");
#endif
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            Time.timeScale = 1;
            temporaryDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "BeforeDemolitionEmailUI-" + Guid.NewGuid().ToString("N")));
            progressPath = Path.Combine(temporaryDirectory, "emails.json");
            previousScene = SceneManager.GetActiveScene();
            fixtureScene = SceneManager.CreateScene("Isolated email UI " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(fixtureScene);
            camera = new GameObject("Email capture camera").AddComponent<Camera>();
            camera.enabled = false; camera.cullingMask = -1; camera.fieldOfView = 49;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.01f, .015f, .02f);
            CreateController();
        }

        void CreateController()
        {
            inbox = new GameObject("Isolated email controller").AddComponent<EmailInboxController>();
            inbox.normalFont = normalFont; inbox.childFont = childFont;
            inbox.Configure(catalog, progressPath);
            Assert.That(inbox.game, Is.Null);
            Assert.That(inbox.PersistenceError, Is.Empty);
        }

        static IEnumerator Layout()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
        }

        IEnumerator Escape()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
        }

        static void PointerClick(Button button)
        {
            Assert.That(button && button.IsActive() && button.IsInteractable(), Is.True);
            Assert.That(EventSystem.current, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current)
            { position = point, button = PointerEventData.InputButton.Left, clickCount = 1, eligibleForClick = true };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, button.name + " must be reachable through the real UI raycaster.");
            Assert.That(hits[0].module, Is.TypeOf<GraphicRaycaster>());
            var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(target, Is.EqualTo(button.gameObject), "A different UI element blocks " + button.name);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        IEnumerator ClickMessage(string id)
        {
            var ids = catalog.messages.Where(message => inbox.State.IsDelivered(message.id)).Select(message => message.id).ToArray();
            int index = Array.IndexOf(ids, id);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            inbox.View.ListScroll.verticalNormalizedPosition = ids.Length == 1 ? 1 : 1 - (float)index / (ids.Length - 1);
            yield return Layout();
            PointerClick(inbox.View.MessageButtons[id]);
            yield return Layout();
            Assert.That(inbox.CurrentMessageId, Is.EqualTo(id));
        }

        Button AttachmentButton(string fileName) => inbox.View.GetComponentsInChildren<Button>(true)
            .Single(button => button.GetComponentInChildren<TMP_Text>(true).text == fileName);
        TMP_Text ListLabel(string id) => inbox.View.MessageButtons[id].GetComponentInChildren<TMP_Text>();

        [UnityTest, Timeout(60000)]
        public IEnumerator PointerReadsOutOfOrderOnceAndDeliveryAndRestartKeepTheSameInbox()
        {
            var flags = new List<string>(); var deliveries = new List<string>();
            inbox.ReadFlagRaised += flags.Add; inbox.MessageDelivered += deliveries.Add;
            Assert.That(inbox.Open(EmailDevice.Desktop), Is.True);
            yield return Layout();
            Assert.That(inbox.UnreadCount, Is.EqualTo(7));
            Assert.That(inbox.View.MessageButtons.Count, Is.EqualTo(7));
            Assert.That(inbox.View.MessageButtons.ContainsKey("email.08"), Is.False);
            Assert.That(inbox.SelectMessage("email.08"), Is.False);
            Assert.That(inbox.State.IsRead("email.04"), Is.False, "A story's forged receipt is not a player read flag.");
            Assert.That(inbox.View.MessageButtons.Values.All(button => button.GetComponentInChildren<TMP_Text>().fontStyle == FontStyles.Bold), Is.True);
            yield return ClickMessage("email.07");
            Assert.That(ListLabel("email.07").fontStyle, Is.EqualTo(FontStyles.Normal));
            Assert.That(inbox.UnreadCount, Is.EqualTo(6));
            yield return ClickMessage("email.01");
            yield return ClickMessage("email.07");
            CollectionAssert.AreEqual(new[] { "email.07.read", "email.01.read" }, flags);
            Assert.That(inbox.Deliver("email.08"), Is.True);
            Assert.That(inbox.Deliver("email.08"), Is.False);
            Assert.That(inbox.Deliver("unknown"), Is.False);
            CollectionAssert.AreEqual(new[] { "email.08" }, deliveries);
            Assert.That(inbox.View.MessageButtons.Count, Is.EqualTo(8));
            Assert.That(ListLabel("email.08").fontStyle, Is.EqualTo(FontStyles.Bold));
            Assert.That(inbox.UnreadCount, Is.EqualTo(6));
            Assert.That(inbox.View.Counter.text, Does.Contain("6 não lidas"));
            Assert.That(File.Exists(progressPath), Is.True);
            Assert.That(inbox.HasPendingSave, Is.False, inbox.PersistenceError);
            var saved = inbox.CaptureState();
            inbox.Close(); UnityEngine.Object.Destroy(inbox.gameObject);
            yield return null;
            CreateController();
            CollectionAssert.AreEqual(saved.deliveredIds, inbox.State.deliveredIds);
            CollectionAssert.AreEqual(saved.readIds, inbox.State.readIds);
            Assert.That(inbox.State.IsRead("email.04"), Is.False);
            var restartedFlags = new List<string>(); inbox.ReadFlagRaised += restartedFlags.Add;
            Assert.That(inbox.Open(EmailDevice.Desktop), Is.True);
            yield return Layout();
            Assert.That(inbox.UnreadCount, Is.EqualTo(6));
            Assert.That(ListLabel("email.07").fontStyle, Is.EqualTo(FontStyles.Normal));
            yield return ClickMessage("email.07");
            Assert.That(restartedFlags, Is.Empty);
            yield return ClickMessage("email.08");
            CollectionAssert.AreEqual(new[] { "email.08.read" }, restartedFlags);
            Assert.That(inbox.UnreadCount, Is.EqualTo(5));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator EscapeClosesOneLayerAndBothDevicesPreserveCallerTimeAndCursor()
        {
            Time.timeScale = .37f;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            yield return null;
            var callerLock = Cursor.lockState; bool callerVisible = Cursor.visible, callerAudio = AudioListener.pause;
            Assert.That(inbox.Open(EmailDevice.Desktop), Is.True);
            yield return Layout();
            yield return ClickMessage("email.07");
            var snapshot = inbox.CaptureState();
            snapshot.readIds.Add("email.01");
            Assert.That(inbox.State.IsRead("email.01"), Is.False);
            snapshot.readIds.Remove("email.01");
            Assert.That(inbox.Deliver("email.08"), Is.True);
            Assert.That(inbox.SelectMessage("email.08"), Is.True);
            Assert.That(inbox.RestoreState(snapshot), Is.True);
            snapshot.readIds.Clear();
            Assert.That(inbox.State.IsRead("email.07"), Is.True, "Restore must copy the snapshot lists.");
            Assert.That(inbox.State.IsDelivered("email.08"), Is.False);
            Assert.That(inbox.CurrentMessageId, Is.Empty);
            inbox.Close();
            yield return null;
            foreach (var device in new[] { EmailDevice.Desktop, EmailDevice.Phone })
            {
                Assert.That(inbox.Open(device), Is.True);
                yield return Layout();
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
                Assert.That(Cursor.visible, Is.True);
                if (device == EmailDevice.Phone) Assert.That(inbox.View.Frame.rect.width, Is.LessThanOrEqualTo(460));
                else Assert.That(inbox.View.Frame.rect.width, Is.GreaterThan(460));
                yield return ClickMessage("email.07");
                PointerClick(AttachmentButton("relatorio_2006.pdf"));
                Assert.That(inbox.View.AttachmentOpen, Is.True);
                yield return Escape();
                Assert.That(inbox.IsOpen, Is.True); Assert.That(inbox.View.AttachmentOpen, Is.False);
                Assert.That(inbox.CurrentMessageId, Is.EqualTo("email.07"));
                yield return Escape();
                Assert.That(inbox.IsOpen, Is.True); Assert.That(inbox.CurrentMessageId, Is.Empty);
                Assert.That(inbox.View.ListScroll.gameObject.activeInHierarchy, Is.True);
                Assert.That(inbox.View.MessagePane.gameObject.activeSelf, Is.EqualTo(device == EmailDevice.Desktop));
                yield return Escape();
                Assert.That(inbox.IsOpen, Is.False);
                Assert.That(Cursor.lockState, Is.EqualTo(callerLock)); Assert.That(Cursor.visible, Is.EqualTo(callerVisible));
                Assert.That(Time.timeScale, Is.EqualTo(.37f)); Assert.That(AudioListener.pause, Is.EqualTo(callerAudio));
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator UnavailablePdfsKeepTheReaderAndDiagnosticImageKeepsItsAspectRatio()
        {
            image = new Texture2D(400, 200, TextureFormat.RGBA32, false);
            image.SetPixels32(Enumerable.Range(0, image.width * image.height)
                .Select(index => index % image.width < 200 ? new Color32(40, 140, 160, 255) : new Color32(210, 165, 70, 255)).ToArray());
            image.Apply();
            catalog.Find("email.07").attachments[2].preview = image;
            Assert.That(inbox.Open(EmailDevice.Desktop), Is.True);
            yield return Layout(); yield return ClickMessage("email.07");
            foreach (var fileName in new[] { "relatorio_2006.pdf", "relatorio_2via.pdf" })
            {
                PointerClick(AttachmentButton(fileName)); yield return Layout();
                Assert.That(inbox.View.AttachmentOpen, Is.True);
                Assert.That(inbox.View.AttachmentImage.gameObject.activeSelf, Is.False);
                Assert.That(inbox.View.AttachmentImage.texture, Is.Null);
                Assert.That(inbox.View.AttachmentPanel.GetComponentsInChildren<TMP_Text>()
                    .Single(text => text.name == "Error").text, Is.EqualTo("Não foi possível abrir este anexo."));
                yield return Escape();
                Assert.That(inbox.CurrentMessageId, Is.EqualTo("email.07"));
            }
            PointerClick(AttachmentButton("foto_escritorio.jpg")); yield return Layout();
            var preview = inbox.View.AttachmentImage;
            Assert.That(preview.gameObject.activeSelf, Is.True); Assert.That(preview.texture, Is.SameAs(image));
            Assert.That(preview.GetComponent<AspectRatioFitter>().aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.FitInParent));
            Assert.That(preview.GetComponent<AspectRatioFitter>().aspectRatio, Is.EqualTo(2).Within(.001f));
            Assert.That(preview.rectTransform.rect.width / preview.rectTransform.rect.height, Is.EqualTo(2).Within(.01f));
            var parent = (RectTransform)preview.transform.parent;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, preview.rectTransform);
            Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(parent.rect.xMin - 1));
            Assert.That(bounds.max.x, Is.LessThanOrEqualTo(parent.rect.xMax + 1));
            Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(parent.rect.yMin - 1));
            Assert.That(bounds.max.y, Is.LessThanOrEqualTo(parent.rect.yMax + 1));
            yield return Escape();
            Assert.That(inbox.View.AttachmentImage.texture, Is.Null);
            Assert.That(inbox.CurrentMessageId, Is.EqualTo("email.07"));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator LongChildWritingUsesTheApprovedFontAndScrollsWithoutChangingText()
        {
            Assert.That(inbox.Open(EmailDevice.Phone), Is.True);
            yield return Layout(); yield return ClickMessage("email.06");
            var message = catalog.Find("email.06");
            Assert.That(inbox.View.Paragraphs.Count, Is.EqualTo(message.paragraphs.Length));
            for (int index = 0; index < message.paragraphs.Length; index++)
            {
                var rendered = inbox.View.Paragraphs[index]; var paragraph = message.paragraphs[index];
                Assert.That(rendered.text, Is.EqualTo(paragraph.text));
                Assert.That(rendered.font, Is.SameAs(paragraph.child ? childFont : normalFont));
                Assert.That(rendered.richText, Is.False);
                Assert.That(rendered.fontStyle, Is.EqualTo(FontStyles.Normal), "Handwriting is a font, not fake italic styling.");
                rendered.ForceMeshUpdate();
                Assert.That(rendered.preferredHeight, Is.GreaterThan(0));
            }
            var scroll = inbox.View.BodyScroll;
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height + 10));
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(.001f));
            float start = scroll.content.anchoredPosition.y;
            Assert.That(ExecuteEvents.ExecuteHierarchy(scroll.viewport.gameObject,
                new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -200) }, ExecuteEvents.scrollHandler), Is.Not.Null);
            yield return Layout();
            Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(start + 10));
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0).Within(.001f));
            CollectionAssert.AreEqual(message.paragraphs.Select(paragraph => paragraph.text), inbox.View.Paragraphs.Select(paragraph => paragraph.text));
            Assert.That(inbox.View.Paragraphs.Last().font, Is.SameAs(childFont));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DesktopAndPhoneFitThreeResolutionsWithNativeCaptures()
        {
            var proofs = new List<CaptureProof>();
            foreach (var device in new[] { EmailDevice.Desktop, EmailDevice.Phone })
            {
                Assert.That(inbox.Open(device), Is.True); yield return Layout();
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1280, 960), new Vector2Int(2560, 1080) })
                    proofs.Add(Capture(device, "inbox", size));
                yield return ClickMessage("email.06");
                if (device == EmailDevice.Phone) { inbox.View.BodyScroll.verticalNormalizedPosition = 0; yield return Layout(); }
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1280, 960), new Vector2Int(2560, 1080) })
                    proofs.Add(Capture(device, "child-reader", size));
                inbox.Back(); yield return Layout(); yield return ClickMessage("email.07");
                PointerClick(AttachmentButton("relatorio_2006.pdf")); yield return Layout();
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1280, 960), new Vector2Int(2560, 1080) })
                    proofs.Add(Capture(device, "unavailable-pdf", size));
                inbox.Close(); yield return null;
            }
            Directory.CreateDirectory(VisualDirectory);
            File.WriteAllText(Path.Combine(VisualDirectory, "email-ui-layout.json"), JsonUtility.ToJson(new CaptureProofs { captures = proofs.ToArray() }, true));
            Assert.That(proofs.Count, Is.EqualTo(18));
        }

        CaptureProof Capture(EmailDevice device, string state, Vector2Int size)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("HorrorGame.Editor.PresentationCapture")).FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, "Use the existing native presentation capture helper.");
            var proof = new CaptureProof { device = device.ToString(), state = state, width = size.x, height = size.y };
            proof.file = device + "-" + state + "-" + size.x + "x" + size.y + ".png";
            Action<Camera> inspect = renderedCamera =>
            {
                var view = inbox.View;
                var rects = new List<RectTransform> { view.Frame };
                if (view.AttachmentOpen)
                {
                    rects.Add(view.AttachmentPanel);
                    rects.Add((RectTransform)view.AttachmentPanel.GetComponentsInChildren<Button>().Single().transform);
                }
                else
                {
                    rects.Add((RectTransform)view.BackButton.transform);
                    if (view.ListScroll.gameObject.activeInHierarchy) rects.Add(view.ListScroll.viewport);
                    if (!string.IsNullOrEmpty(inbox.CurrentMessageId))
                    {
                        rects.Add(view.MessagePane); rects.Add(view.BodyScroll.viewport);
                        rects.Add((RectTransform)view.MessagePane.Find("SubjectArea"));
                        rects.Add((RectTransform)view.MessagePane.Find("MetadataArea"));
                        foreach (var text in view.Paragraphs)
                        {
                            var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                            foreach (var corner in corners)
                                Assert.That(renderedCamera.WorldToScreenPoint(corner).x, Is.InRange(-1f, size.x + 1f), text.name + " must wrap within screen width.");
                            text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False, "Scrollable body text must not truncate.");
                        }
                    }
                }
                foreach (var rect in rects)
                {
                    var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = renderedCamera.WorldToScreenPoint(corner);
                        Assert.That(point.x, Is.InRange(-1f, size.x + 1f), device + " " + state + " " + rect.name);
                        Assert.That(point.y, Is.InRange(-1f, size.y + 1f), device + " " + state + " " + rect.name);
                    }
                    Assert.That(rect.rect.width, Is.GreaterThan(0), rect.name); Assert.That(rect.rect.height, Is.GreaterThan(0), rect.name);
                }
                proof.checkedRects = rects.Select(rect => rect.name).ToArray();
            };
            var method = type.GetMethod("Capture", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { fixtureScene, camera, Path.Combine(VisualDirectory, proof.file), size.x, size.y, inspect, true });
            Assert.That(new FileInfo(Path.Combine(VisualDirectory, proof.file)).Length, Is.GreaterThan(1000));
            return proof;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ImmediateReopenAndExternalDisableReleaseCursorAndInspectionOwnership()
        {
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            yield return null;
            var callerLock = Cursor.lockState; bool callerVisible = Cursor.visible;
            Assert.That(inbox.Open(EmailDevice.Desktop), Is.True);
            inbox.Close();
            Assert.That(inbox.Open(EmailDevice.Phone), Is.True, "Reopening in the same frame must retain a usable EventSystem.");
            yield return Layout(); yield return ClickMessage("email.01");
            inbox.View.gameObject.SetActive(false);
            Assert.That(inbox.IsOpen, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(callerLock)); Assert.That(Cursor.visible, Is.EqualTo(callerVisible));
            Assert.That(inbox.Open(EmailDevice.Desktop), Is.True);
            yield return Layout(); yield return ClickMessage("email.02");
            inbox.Close();
            var inertRoot = new GameObject("Permanently inactive inspection contract fixture");
            inertRoot.SetActive(false);
            var inertSession = inertRoot.AddComponent<GameSession>();
            inertSession.player = inertRoot.AddComponent<FirstPersonMotor>();
            Assert.That(GameSession.Instance, Is.Null, "The inactive fixture must never run GameSession.Awake.");
            Assert.That(inertSession.save, Is.Null); Assert.That(inertSession.input, Is.Null); Assert.That(inertSession.hud, Is.Null);
            inbox.game = inertSession;
            yield return null;
            Assert.That(inbox.Open(EmailDevice.Phone), Is.True);
            Assert.That(inertSession.State, Is.EqualTo(GameState.Inspecting));
            inbox.View.gameObject.SetActive(false);
            Assert.That(inbox.IsOpen, Is.False);
            Assert.That(inertSession.State, Is.EqualTo(GameState.Playing));
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked)); Assert.That(Cursor.visible, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(inertRoot.activeSelf, Is.False); Assert.That(GameSession.Instance, Is.Null);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator FailedWriteKeepsReadFlagsAndRetryButtonSavesAfterUnlock()
        {
            Assert.That(inbox.Open(EmailDevice.Desktop), Is.True); yield return Layout();
            yield return ClickMessage("email.01");
            byte[] before = File.ReadAllBytes(progressPath);
            using (var blocker = new FileStream(progressPath + ".tmp", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                yield return ClickMessage("email.07");
                Assert.That(inbox.HasPendingSave, Is.True); Assert.That(inbox.CanRetrySave, Is.True);
                Assert.That(inbox.PersistenceError, Is.Not.Empty); Assert.That(inbox.State.IsRead("email.07"), Is.True);
                Assert.That(inbox.UnreadCount, Is.EqualTo(5));
                yield return Layout(); yield return Layout();
                CollectionAssert.AreEqual(before, File.ReadAllBytes(progressPath), "Failed writes must not replace the prior snapshot.");
                Assert.That(blocker.Length, Is.EqualTo(0), "No background frame loop may write around the file lock.");
            }
            var retryButton = inbox.View.GetComponentsInChildren<Button>().Single(button => button.name == "RetrySave");
            PointerClick(retryButton); yield return Layout();
            Assert.That(inbox.HasPendingSave, Is.False); Assert.That(inbox.PersistenceError, Is.Empty);
            var store = new EmailProgressStore(progressPath);
            Assert.That(store.TryLoad(catalog, out var saved, out var error), Is.True, error);
            CollectionAssert.AreEquivalent(new[] { "email.01", "email.07" }, saved.readIds);
        }

        [Serializable] sealed class CaptureProof { public string device, state, file; public int width, height; public string[] checkedRects; }
        [Serializable] sealed class CaptureProofs { public CaptureProof[] captures; }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (inbox) inbox.Close();
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (fixtureScene.IsValid() && fixtureScene.isLoaded) yield return SceneManager.UnloadSceneAsync(fixtureScene);
            if (catalog) UnityEngine.Object.DestroyImmediate(catalog);
            if (image) UnityEngine.Object.DestroyImmediate(image);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior = inputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background;
            Time.timeScale = timeScale; AudioListener.pause = audioPaused;
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
            if (string.IsNullOrEmpty(temporaryDirectory)) yield break;
            Assert.That(Path.GetDirectoryName(temporaryDirectory), Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(temporaryDirectory), Does.StartWith("BeforeDemolitionEmailUI-"));
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string path = Path.GetFullPath(progressPath + suffix);
                Assert.That(Path.GetDirectoryName(path), Is.EqualTo(temporaryDirectory).IgnoreCase);
                if (File.Exists(path)) File.Delete(path);
            }
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, false);
        }
    }
}
