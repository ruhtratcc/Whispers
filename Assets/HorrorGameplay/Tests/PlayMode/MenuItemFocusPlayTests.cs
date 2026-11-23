// Verifica FocusAccentsAnimateWhilePausedAndReleaseForDisabledOrDeselectedButtons.

using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("FinalMenu")]
    public sealed class MenuItemFocusPlayTests
    {
        [UnityTest]
        public IEnumerator FocusAccentsAnimateWhilePausedAndReleaseForDisabledOrDeselectedButtons()
        {
            // Menu scripts stay in Assembly-CSharp; do not migrate production assemblies for this fixture.
            var type = Type.GetType("SchoolMenu.MenuItemFX, Assembly-CSharp");
            Assert.That(type, Is.Not.Null);
            var previousEvents = EventSystem.current;
            float previousScale = Time.timeScale;
            var root = new GameObject("Menu focus fixture");
            root.SetActive(false);
            try
            {
                var events = root.AddComponent<EventSystem>();
                var item = new GameObject("Action", typeof(RectTransform), typeof(UnityEngine.UI.Button));
                item.transform.SetParent(root.transform, false);
                var button = item.GetComponent<UnityEngine.UI.Button>();
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(item.transform, false);
                var label = labelObject.GetComponent<TextMeshProUGUI>();
                label.text = "Novo jogo";
                var home = new Vector2(11, -4);
                label.rectTransform.anchoredPosition = home;
                var line = Accent(item.transform, "Focus line", .6f);
                line.rectTransform.localScale = new Vector3(2, 1, 1);
                var marker = Accent(item.transform, "Focus marker", .85f);
                var fx = item.AddComponent(type);
                Set(fx, "label", label); Set(fx, "focusLine", line); Set(fx, "focusMarker", marker);
                Set(fx, "response", 30f);
                var other = new GameObject("Other action", typeof(RectTransform), typeof(UnityEngine.UI.Button));
                other.transform.SetParent(root.transform, false);
                Time.timeScale = 0;
                root.SetActive(true);
                EventSystem.current = events;
                events.SetSelectedGameObject(null);
                yield return null;
                Assert.That(line.color.a, Is.EqualTo(0));
                Assert.That(marker.color.a, Is.EqualTo(0));
                Assert.That(line.raycastTarget || marker.raycastTarget, Is.False, "Decorations must not intercept clicks.");

                events.SetSelectedGameObject(item);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(line.color.a, Is.EqualTo(.6f).Within(.015f));
                Assert.That(marker.color.a, Is.EqualTo(.85f).Within(.015f));
                Assert.That(line.rectTransform.localScale.x, Is.EqualTo(2).Within(.02f));
                Assert.That(label.rectTransform.anchoredPosition.x, Is.EqualTo(home.x + 4).Within(.05f));

                events.SetSelectedGameObject(other);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(line.color.a, Is.LessThan(.015f));
                Assert.That(marker.color.a, Is.LessThan(.015f));
                Assert.That(label.rectTransform.anchoredPosition.x, Is.EqualTo(home.x).Within(.05f));

                events.SetSelectedGameObject(item);
                yield return new WaitForSecondsRealtime(.3f);
                button.interactable = false;
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(marker.color.a, Is.LessThan(.015f), "A stale selected object cannot light a disabled action.");
                Assert.That(line.rectTransform.localScale.x, Is.LessThan(.02f));
                Assert.That(label.rectTransform.anchoredPosition.x, Is.EqualTo(home.x).Within(.05f));
                events.SetSelectedGameObject(other);
                ((IPointerEnterHandler)fx).OnPointerEnter(new PointerEventData(events));
                Assert.That(events.currentSelectedGameObject, Is.SameAs(other));

                button.interactable = true;
                ((IPointerEnterHandler)fx).OnPointerEnter(new PointerEventData(events));
                Assert.That(events.currentSelectedGameObject, Is.SameAs(item));
                yield return new WaitForSecondsRealtime(.3f);
                ((Behaviour)fx).enabled = false;
                Assert.That(line.color.a, Is.EqualTo(0));
                Assert.That(marker.color.a, Is.EqualTo(0));
                Assert.That(label.rectTransform.anchoredPosition, Is.EqualTo(home));
            }
            finally
            {
                Time.timeScale = previousScale;
                UnityEngine.Object.DestroyImmediate(root);
                if (previousEvents && previousEvents.isActiveAndEnabled) EventSystem.current = previousEvents;
            }
        }

        static UnityEngine.UI.Image Accent(Transform parent, string name, float alpha)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            item.transform.SetParent(parent, false);
            var image = item.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(.8f, .3f, .2f, alpha);
            return image;
        }

        static void Set(Component target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
