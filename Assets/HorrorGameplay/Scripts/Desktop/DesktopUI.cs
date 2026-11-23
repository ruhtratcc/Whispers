// Monta a área de trabalho e coordena aplicativos e janelas.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HorrorGame.Desktop
{
    public static class DesktopUI
    {
        public static DesktopContent Theme;
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
            r.sizeDelta = new Vector2(Mathf.Round(w), Mathf.Round(h));
            r.gameObject.layer = parent.gameObject.layer;
            return r;
        }

        public static RectTransform Panel(Transform parent, string name, float x, float y, float w, float h, Sprite sprite = null)
        {
            var r = Rect(parent, name, x, y, w, h);
            var image = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite ? sprite : Theme ? Theme.windowSprite : null;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = Color.white;
            return r;
        }

        public static TMP_Text Label(Transform parent, string text, float x, float y, float w, float h, float size = 16, bool child = false)
        {
            var r = Rect(parent, "Texto", x, y, w, h);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Theme ? (child ? Theme.childFont : Theme.normalFont) : TMP_Settings.defaultFontAsset;
            t.text = text;
            t.enableAutoSizing = false;
            t.extraPadding = false;
            t.fontSize = Mathf.Round(size);
            t.color = new Color(.065f, .12f, .19f);
            t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate;
            t.richText = false;
            return t;
        }

        public static UnityEngine.UI.Button Button(Transform parent, string text, float x, float y, float w, float h, Action action)
        {
            var r = Panel(parent, text, x, y, w, h, Theme ? Theme.buttonSprite : null);
            var b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = r.GetComponent<UnityEngine.UI.Image>();
            var c = b.colors;
            c.highlightedColor = new Color(.89f, .95f, 1);
            c.selectedColor = c.highlightedColor;
            c.pressedColor = new Color(.68f, .79f, .94f);
            b.colors = c;
            var l = Label(r, text, 5, 3, w - 10, h - 6, 14);
            l.alignment = TextAlignmentOptions.Midline;
            b.onClick.AddListener(() => action?.Invoke());
            return b;
        }

        public static UnityEngine.UI.Image Image(Transform parent, Sprite sprite, float x, float y, float w, float h)
        {
            if (sprite && sprite.rect.width == 22 && sprite.rect.height == 22)
            {
                float size = 22 * Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(w, h) / 22));
                x = Mathf.Round(x + (w - size) * .5f);
                y = Mathf.Round(y + (h - size) * .5f);
                w = h = size;
            }

            var r = Rect(parent, "Ícone", x, y, w, h);
            var i = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            i.sprite = sprite;
            i.preserveAspect = true;
            i.raycastTarget = false;
            return i;
        }

        public static RectTransform Scroll(Transform parent, float x, float y, float w, float h, float contentHeight)
        {
            var r = Panel(parent, "Rolagem", x, y, w, h);
            var scroll = r.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            var viewport = Rect(r, "Viewport", 1, 1, w - 18, h - 2);
            var maskImage = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            maskImage.color = new Color(1, 1, 1, .02f);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Rect(viewport, "Content", 0, 0, w - 22, Mathf.Max(h - 2, contentHeight));
            scroll.viewport = viewport;
            scroll.content = content;
            var bar = Panel(r, "Barra vertical", w - 16, 1, 15, h - 2, Theme ? Theme.buttonSprite : null);
            var scrollbar = bar.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
            scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            var area = Rect(bar, "Área do controle", 0, 15, 15, h - 32);
            var thumb = Panel(area, "Controle", 0, 0, 0, 0, Theme ? Theme.buttonSprite : null);
            thumb.anchorMin = Vector2.zero;
            thumb.anchorMax = Vector2.one;
            thumb.offsetMin = thumb.offsetMax = Vector2.zero;
            scrollbar.handleRect = thumb;
            scrollbar.targetGraphic = thumb.GetComponent<UnityEngine.UI.Image>();
            scroll.verticalScrollbar = scrollbar;
            var up = Button(bar, "^", 0, 0, 15, 15, () => scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + .15f));
            var down = Button(bar, "v", 0, h - 17, 15, 15, () => scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - .15f));
            foreach (var button in new[]
            {
                up,
                down
            }

            )
            {
                var label = button.GetComponentInChildren<TMP_Text>();
                label.fontSize = 10;
                label.rectTransform.anchoredPosition = Vector2.zero;
                label.rectTransform.sizeDelta = new Vector2(15, 15);
            }

            return content;
        }

        public static TMP_InputField Entry(Transform parent, string text, float x, float y, float w, float h, Action<string> submit)
        {
            var r = Panel(parent, "Endereço", x, y, w, h, Theme ? Theme.buttonSprite : null);
            var field = r.gameObject.AddComponent<TMP_InputField>();
            var l = Label(r, text, 6, 3, w - 12, h - 6, 16);
            l.alignment = TextAlignmentOptions.MidlineLeft;
            field.textViewport = r;
            field.textComponent = l;
            field.text = text;
            field.readOnly = true;
            r.gameObject.AddComponent<DesktopEntry>().Configure(field, submit);
            return field;
        }

        public static UnityEngine.UI.Slider Slider(Transform parent, float x, float y, float w, float h, Action<float> changed)
        {
            var r = Panel(parent, "Progresso", x, y, w, h, Theme ? Theme.buttonSprite : null);
            var s = r.gameObject.AddComponent<UnityEngine.UI.Slider>();
            var area = Rect(r, "Área preenchida", 1, 1, w - 2, h - 2);
            var fill = Panel(area, "Fill", 0, 0, 0, 0, Theme ? Theme.titleSprite : null);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.anchoredPosition = fill.sizeDelta = Vector2.zero;
            s.fillRect = fill;
            var handleArea = Rect(r, "Área do marcador", 6, 0, w - 12, h);
            var handle = Panel(handleArea, "Handle", 0, 0, 12, h, Theme ? Theme.buttonSprite : null);
            handle.pivot = new Vector2(.5f, .5f);
            handle.anchoredPosition = Vector2.zero;
            s.handleRect = handle;
            s.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            s.minValue = 0;
            s.maxValue = 1;
            s.wholeNumbers = false;
            s.onValueChanged.AddListener(v => changed?.Invoke(v));
            return s;
        }

        public static void Clear(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var c = root.GetChild(i);
                c.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(c.gameObject);
            }
        }
    }

    public sealed class DesktopEntry : MonoBehaviour, IPointerClickHandler
    {
        public TMP_InputField Field { get; private set; }

        public Action<string> Submitted;
        public void Configure(TMP_InputField field, Action<string> submit)
        {
            Field = field;
            Submitted = submit;
        }

        public void OnPointerClick(PointerEventData e)
        {
            var host = GetComponentInParent<DesktopWindow>()?.Host;
            if (host)
                host.FocusEntry(this);
        }
    }
}
