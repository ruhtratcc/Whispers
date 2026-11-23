// Verifica .

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HorrorGame.Desktop;

namespace HorrorGame.Tests
{
    // The existing fixture must also be declared partial; its save/input guards and helpers are reused.
    public sealed partial class DesktopOfficePlayTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator VirtualInputHandlesTextMenusResizeSliderAndReducedEffects()
        {
            const string reducedKey = "HorrorMenu.ReduceEffects";
            bool hadPreference = PlayerPrefs.HasKey(reducedKey);
            int preference = PlayerPrefs.GetInt(reducedKey, 0);
            const string calibratedKey="HorrorMenu.Calibrated";bool hadCalibrated=PlayerPrefs.HasKey(calibratedKey);int calibrated=PlayerPrefs.GetInt(calibratedKey,0);
            try
            {
                yield return Enter();
                foreach (string id in pc.Windows.Keys.ToArray()) pc.CloseApp(id);
                yield return null;

                // Background right-click must create the real context menu and its real error action.
                pc.Pointer.ClickAt(new Vector2(970, 510), true);
                yield return null;
                var context = pc.DesktopRoot.Find("Menu de contexto");
                Assert.That(context && context.gameObject.activeInHierarchy, Is.True);
                yield return Click(DesktopButton("Propriedades"));
                Assert.That(pc.Windows.Count,Is.GreaterThan(0),"Desktop properties must provide a visible response.");
                foreach(string id in pc.Windows.Keys.ToArray())pc.CloseApp(id);
                yield return null;

                // A single icon click selects; the second click opens through DesktopIcon itself.
                var icon = pc.DesktopRoot.GetComponentsInChildren<DesktopIcon>()
                    .Single(item => item.AppId == "browser");
                var iconRect = (RectTransform)icon.transform;
                Vector2 iconPoint = DesktopPoint(iconRect, iconRect.rect.center);
                pc.Pointer.SetPosition(iconPoint);
                Assert.That(pc.Pointer.Hovered && pc.Pointer.Hovered.GetComponentInParent<DesktopIcon>() == icon, Is.True);
                pc.Pointer.ClickAt(iconPoint);
                Assert.That(pc.Windows.ContainsKey("browser"), Is.False, "Single-click must not launch an app.");
                pc.Pointer.ClickAt(iconPoint);
                yield return null;
                Assert.That(pc.Windows.ContainsKey("browser"), Is.True, "The icon's double-click must launch the browser.");
                var browser = pc.Windows["browser"];
                var browserApp = (DesktopBrowserApp)browser.App;
                var entry = browser.body.GetComponentInChildren<TMP_InputField>();
                Assert.That(entry, Is.Not.Null);
                yield return ClickDesktopRect((RectTransform)entry.transform, entry.gameObject);
                yield return Press(Key.LeftCtrl, Key.A);
                foreach(var source in AudioManager.Instance.GetComponentsInChildren<AudioSource>(true).Where(s=>s.name.StartsWith("Spatial voice"))){source.Stop();source.clip=null;}
                foreach (char character in "ceep.local/demolicao") InputSystem.QueueTextEvent(keyboard, character);
                yield return null; yield return null;
                Assert.That(entry.text, Is.EqualTo("ceep.local/demolicao"), "TextEvent must reach the focused virtual entry.");
                var audio=AudioManager.Instance;var keyClips=audio.cues.Find("pc_click").sound.clips;
                Assert.That(audio.GetComponentsInChildren<AudioSource>(true).Any(s=>keyClips.Contains(s.clip)&&s.outputAudioMixerGroup==audio.pcGroup),Is.True,"Typing plays quiet Foley on the PC bus.");
                yield return Press(Key.Enter);
                Assert.That(browserApp.CurrentAddress, Is.EqualTo("ceep.local/demolicao"));
                yield return ClickDesktopRect((RectTransform)entry.transform, entry.gameObject);
                yield return Press(Key.LeftCtrl, Key.A);
                foreach (char character in "ceep.local/ausente") InputSystem.QueueTextEvent(keyboard, character);
                yield return null; yield return null;
                yield return Press(Key.Enter);
                Assert.That(browserApp.CurrentAddress, Is.EqualTo("ceep.local/demolicao"));
                Assert.That(pc.Windows.ContainsKey("error.disk"), Is.True);
                pc.CloseApp("error.disk"); pc.CloseApp("browser");
                yield return null;

                // Rebuilding a reading pane while maximized must not leave it wider than a restored window.
                var mail = pc.OpenApp("mail");
                yield return Click(Button(mail, "□"));
                Assert.That(mail.Maximized, Is.True);
                yield return ReadMail("email.07");
                AssertMailViewportsFit(mail);
                yield return Click(Button(mail, "□"));
                Assert.That(mail.Maximized, Is.False);
                Canvas.ForceUpdateCanvases(); yield return null;
                AssertMailViewportsFit(mail);
                Assert.That(Text(mail), Does.Contain(pc.inbox.catalog.Find("email.07").paragraphs[0].text));
                pc.CloseApp("mail"); yield return null;

                var mediaWindow = pc.OpenApp("media");
                var media = (DesktopMediaApp)mediaWindow.App;
                media.Stop();
                var slider = mediaWindow.body.GetComponentInChildren<Slider>();
                Assert.That(slider && slider.interactable, Is.True);
                foreach (float fraction in new[] { .25f, .75f })
                {
                    var rect = (RectTransform)slider.transform;
                    Vector2 local = new Vector2(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, fraction), rect.rect.center.y);
                    yield return ClickDesktopRect(rect, slider.gameObject, local);
                    Assert.That(slider.normalizedValue, Is.EqualTo(fraction).Within(.025f));
                    Assert.That(media.PositionSeconds / media.DurationSeconds, Is.EqualTo(fraction).Within(.025f),
                        "Pointer seek must update media progress rather than only moving the handle.");
                    Assert.That(media.IsPlaying, Is.False, "Seeking a stopped track must not start playback.");
                }

                bool wasReduced = PresentationSettings.ReduceEffects;
                Assert.That(pc.DesktopRoot.GetComponentsInChildren<Button>(true).Any(button=>button.GetComponentInChildren<TMP_Text>()&&button.GetComponentInChildren<TMP_Text>().text=="Reduzir efeitos CRT"),Is.False,"Game options must not live on the computer desktop.");
                yield return Press(Key.F10);Assert.That(game.State,Is.EqualTo(GameState.Paused));
                yield return ClickGameUI(game.hud.optionsButton.gameObject);
                var accessibility=game.hud.optionsPanel.GetComponentInParent<Canvas>().GetComponentsInChildren<AccessibilityPanel>(true)
                    .Single(panel=>panel.entry&&panel.entry.transform.IsChildOf(game.hud.optionsPanel.transform));
                yield return ClickGameUI(accessibility.entry.gameObject);Assert.That(accessibility.gameObject.activeInHierarchy,Is.True);
                yield return ClickGameUI(accessibility.reduceEffects.gameObject);
                Assert.That(PresentationSettings.ReduceEffects, Is.EqualTo(!wasReduced));
                yield return ClickGameUI(accessibility.back.gameObject);
                yield return ClickGameUI(game.hud.optionsBackButton.gameObject);
                yield return ClickGameUI(game.hud.resumeButton.gameObject);
                Assert.That(game.State,Is.EqualTo(GameState.Inspecting));
                yield return null; yield return null;
                Assert.That(pc.crtMaterial, Is.Not.Null);
                Assert.That(pc.crtMaterial.GetFloat("_Intensity"), Is.EqualTo(pc.content.crtStrength
                    * (PresentationSettings.ReduceEffects ? .12f : 1f)).Within(.0001f));
                yield return Click(DesktopButton("Iniciar"));yield return Capture("desktop-start-menu", false);
                yield return Click(DesktopButton("Iniciar"));
                yield return Capture("desktop-advanced-controls", false);
            }
            finally
            {
                if (hadPreference) PlayerPrefs.SetInt(reducedKey, preference);
                else PlayerPrefs.DeleteKey(reducedKey);
                if(hadCalibrated)PlayerPrefs.SetInt(calibratedKey,calibrated);else PlayerPrefs.DeleteKey(calibratedKey);PlayerPrefs.Save(); PresentationSettings.Reload();
            }
        }

        IEnumerator ClickGameUI(GameObject expected)
        {
            Canvas.ForceUpdateCanvases();yield return null;
            var rect=(RectTransform)expected.transform;var canvas=expected.GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center)),button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
            var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(data,hits);
            Assert.That(hits.Count>0&&(hits[0].gameObject==expected||hits[0].gameObject.transform.IsChildOf(expected.transform)),Is.True,"Pause/options control must be visible and reachable: "+expected.name);
            UnityEngine.EventSystems.ExecuteEvents.Execute(expected,data,UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);UnityEngine.EventSystems.ExecuteEvents.Execute(expected,data,UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);UnityEngine.EventSystems.ExecuteEvents.Execute(expected,data,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            yield return null;yield return null;
        }

        Button DesktopButton(string text) => pc.DesktopRoot.GetComponentsInChildren<Button>(true)
            .Single(button => button.gameObject.activeInHierarchy && button.GetComponentInChildren<TMP_Text>()
                && button.GetComponentInChildren<TMP_Text>().text == text);

        Vector2 DesktopPoint(RectTransform rect, Vector2 local) =>
            RectTransformUtility.WorldToScreenPoint(pc.ScreenCamera, rect.TransformPoint(local));

        IEnumerator ClickDesktopRect(RectTransform rect, GameObject expected, Vector2? local = null)
        {
            Canvas.ForceUpdateCanvases(); yield return null;
            Vector2 point = DesktopPoint(rect, local ?? rect.rect.center);
            Assert.That(point.x, Is.InRange(0, 1023)); Assert.That(point.y, Is.InRange(0, 767));
            pc.Pointer.SetPosition(point);
            Assert.That(pc.Pointer.Hovered && (pc.Pointer.Hovered == expected
                || pc.Pointer.Hovered.transform.IsChildOf(expected.transform)), Is.True,
                "The real virtual raycast must reach " + expected.name);
            pc.Pointer.ClickAt(point); yield return null; yield return null;
        }

        static void AssertMailViewportsFit(DesktopWindow window)
        {
            var scrolls = window.body.GetComponentsInChildren<ScrollRect>();
            Assert.That(scrolls.Length, Is.GreaterThanOrEqualTo(2));
            var corners = new Vector3[4];
            foreach (var scroll in scrolls)
            {
                scroll.viewport.GetWorldCorners(corners);
                foreach (Vector3 world in corners)
                {
                    Vector3 local = window.body.InverseTransformPoint(world);
                    Assert.That(local.x, Is.InRange(window.body.rect.xMin - 2, window.body.rect.xMax + 2),
                        "Restored mail viewport spills horizontally outside the current body.");
                    Assert.That(local.y, Is.InRange(window.body.rect.yMin - 2, window.body.rect.yMax + 2),
                        "Mail viewport spills vertically outside the current body.");
                }
            }
        }
    }
}
#endif
