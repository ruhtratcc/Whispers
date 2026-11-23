// Aplica o estilo de apresentação das legendas.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame
{
    public sealed class CaptionPresentation : MonoBehaviour
    {
        TMP_Text text;
        Image background;
        float authoredSize;
        string lastText;
        int lastVersion = -1;
        public static void Attach(TMP_Text label)
        {
            if (!label || label.GetComponent<CaptionPresentation>()) return;
            label.gameObject.AddComponent<CaptionPresentation>();
        }
        void Awake()
        {
            text = GetComponent<TMP_Text>(); authoredSize = text.fontSize;
            var plate = new GameObject("Subtitle background", typeof(RectTransform), typeof(Image));
            plate.transform.SetParent(text.transform.parent, false);
            plate.transform.SetSiblingIndex(text.transform.GetSiblingIndex());
            background = plate.GetComponent<Image>(); background.raycastTarget = false;
        }
        void LateUpdate()
        {
            if (!text || !background || (lastText == text.text && lastVersion == PresentationSettings.Version)) return;
            lastText = text.text; lastVersion = PresentationSettings.Version;
            text.fontSize = authoredSize * PresentationSettings.SubtitleSize;
            float width = text.rectTransform.rect.width;
            float height = Mathf.Clamp(text.GetPreferredValues(text.text, Mathf.Max(100, width), 0).y + 16, 36, 300);
            var target = background.rectTransform;
            target.anchorMin = text.rectTransform.anchorMin; target.anchorMax = text.rectTransform.anchorMax;
            target.pivot = text.rectTransform.pivot; target.anchoredPosition = text.rectTransform.anchoredPosition;
            target.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x + 24,
                text.rectTransform.sizeDelta.y + height - text.rectTransform.rect.height);
            background.color = new Color(0, 0, 0, string.IsNullOrEmpty(text.text) ? 0 : PresentationSettings.SubtitleBackground);
        }
        void OnEnable() { if (background) background.enabled = true; lastVersion = -1; }
        void OnDisable() { if (background) background.enabled = false; }
        void OnDestroy() { if (background) Destroy(background.gameObject); }
    }
}
