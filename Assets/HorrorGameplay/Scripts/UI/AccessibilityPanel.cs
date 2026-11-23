// Mostra os ajustes de acessibilidade.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorGame
{
    public sealed class AccessibilityPanel : MonoBehaviour
    {
        public Slider gamma, subtitleSize, subtitleBackground;
        public Toggle reduceEffects;
        public Button back, entry;
        Button whistleHints;
        TMP_Text gammaValue, sizeValue, backgroundValue, sample;
        Image samplePlate;
        readonly Image[] swatches = new Image[3];
        TMP_Text symbol;
        TMP_FontAsset font;
        RectTransform frame;
        Transform owner;
        bool built;
        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        static RectTransform Fill(string name, Transform parent)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        TMP_Text Text(string name, Transform parent, string value, Vector2 size, Vector2 position, float points = 24)
        {
            var text = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = points;
            text.color = new Color(.91f, .90f, .83f);
            text.text = value;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        Button Button(string name, Transform parent, string label, Vector2 size, Vector2 position, Action action)
        {
            var rect = Rect(name, parent, size, position);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.10f, .12f, .11f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text("Label", rect, label, size - new Vector2(16, 4), Vector2.zero, 22);
            button.onClick.AddListener(() => action());
            return button;
        }

        public static AccessibilityPanel Install(GameObject options, TMP_FontAsset font)
        {
            if (!options)
                return null;
            var canvas = options.GetComponentInParent<Canvas>();
            if (!canvas)
                return null;
            EquipmentOptionsPanel.Install(options, font);
            ObjectiveOptions.Install(options, font);
            foreach (var panel in canvas.GetComponentsInChildren<AccessibilityPanel>(true))
                if (panel.owner == options.transform)
                    return panel;
            var root = Fill("Accessibility", canvas.transform);
            var panelNew = root.gameObject.AddComponent<AccessibilityPanel>();
            panelNew.owner = options.transform;
            panelNew.font = font ? font : TMP_Settings.defaultFontAsset;
            panelNew.Build();
            panelNew.entry = panelNew.Button("AccessibilityButton", options.transform, "Brilho e acessibilidade", new Vector2(310, 50), new Vector2(-24, -24), () => panelNew.Show());
            var entryRect = (RectTransform)panelNew.entry.transform;
            entryRect.anchorMin = entryRect.anchorMax = entryRect.pivot = Vector2.one;
            root.gameObject.SetActive(false);
            return panelNew;
        }

        Slider SliderRow(string name, string label, float y, float min, float max, Action<float> change, out TMP_Text value)
        {
            Text(name + " label", frame, label, new Vector2(430, 58), new Vector2(-310, y), 23).alignment = TextAlignmentOptions.Left;
            var rect = Rect(name, frame, new Vector2(410, 34), new Vector2(160, y));
            var track = Fill("Track", rect);
            track.offsetMin = new Vector2(0, 12);
            track.offsetMax = new Vector2(0, -12);
            track.gameObject.AddComponent<Image>().color = new Color(.19f, .22f, .20f);
            var fill = Fill("Fill", track);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(.68f, .70f, .60f);
            var handle = Rect("Handle", rect, new Vector2(22, 40), Vector2.zero);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(.88f, .87f, .77f);
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(v => change(v));
            value = Text(name + " value", frame, "", new Vector2(120, 54), new Vector2(460, y));
            return slider;
        }

        void Build()
        {
            gameObject.AddComponent<Image>().color = new Color(.005f, .008f, .008f, .82f);
            frame = Rect("Panel", transform, new Vector2(1180, 960), Vector2.zero);
            frame.gameObject.AddComponent<Image>().color = new Color(.025f, .032f, .028f, .98f);
            Text("Heading", frame, "BRILHO E ACESSIBILIDADE", new Vector2(1050, 60), new Vector2(0, 372), 32);
            Text("Calibration instruction", frame, "Ajuste o brilho até enxergar o símbolo no centro sem clarear demais o preto. Confira também o cenário ao fundo.", new Vector2(1050, 64), new Vector2(0, 300), 21);
            for (int i = 0; i < swatches.Length; i++)
            {
                var rect = Rect("Shadow reference " + i, frame, new Vector2(250, 90), new Vector2((i - 1) * 270, 205));
                swatches[i] = rect.gameObject.AddComponent<Image>();
            }

            symbol = Text("Calibration symbol", swatches[1].transform, "113", new Vector2(220, 75), Vector2.zero, 35);
            gamma = SliderRow("Gamma", "Brilho / gamma", 92, .8f, 1.2f, PresentationSettings.SetGamma, out gammaValue);
            subtitleSize = SliderRow("SubtitleSize", "Tamanho das legendas", 12, 1, 1.5f, PresentationSettings.SetSubtitleSize, out sizeValue);
            subtitleBackground = SliderRow("SubtitleBackground", "Fundo das legendas", -68, 0, .85f, PresentationSettings.SetSubtitleBackground, out backgroundValue);
            Text("Reduce label", frame, "Reduzir tremores e efeitos visuais (VHS / CRT)", new Vector2(790, 56), new Vector2(-130, -148), 23).alignment = TextAlignmentOptions.Left;
            var toggleRect = Rect("ReduceEffects", frame, new Vector2(48, 48), new Vector2(445, -148));
            var toggleBack = toggleRect.gameObject.AddComponent<Image>();
            toggleBack.color = new Color(.19f, .22f, .20f);
            var check = Rect("Check", toggleRect, new Vector2(28, 28), Vector2.zero).gameObject.AddComponent<Image>();
            check.color = new Color(.88f, .87f, .77f);
            reduceEffects = toggleRect.gameObject.AddComponent<Toggle>();
            reduceEffects.targetGraphic = toggleBack;
            reduceEffects.graphic = check;
            reduceEffects.onValueChanged.AddListener(PresentationSettings.SetReduceEffects);
            whistleHints = Button("WhistleHints", frame, "", new Vector2(760, 42), new Vector2(0, -210), () =>
            {
                WhistleSystem.SetHintsEnabled(!WhistleSystem.HintsEnabled);
                Refresh();
            });
            samplePlate = Rect("Subtitle preview background", frame, new Vector2(1030, 84), new Vector2(0, -292)).gameObject.AddComponent<Image>();
            samplePlate.raycastTarget = false;
            sample = Text("Subtitle preview", samplePlate.transform, "João: Eu acabei de ver alguém ali.", new Vector2(1010, 80), Vector2.zero, 26);
            Text("Danger remains", frame, "O aviso de escuridão e o desmaio continuam ativos.", new Vector2(1040, 38), new Vector2(0, -356), 19);
            back = Button("Back", frame, "Voltar — alterações salvas", new Vector2(400, 52), new Vector2(0, -415), Close);
            built = true;
            Refresh();
        }

        public void Show()
        {
            PresentationSettings.Reload();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            Refresh();
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(gamma.gameObject);
        }

        public void Close()
        {
            PlayerPrefs.SetInt("HorrorMenu.Calibrated", 1);
            PlayerPrefs.Save();
            gameObject.SetActive(false);
            if (EventSystem.current && entry && entry.gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(entry.gameObject);
        }

        public static bool TryClose()
        {
            var panel = FindAnyObjectByType<AccessibilityPanel>();
            if (!panel || !panel.gameObject.activeInHierarchy)
                return false;
            panel.Close();
            return true;
        }

        void OnEnable()
        {
            PresentationSettings.Changed += Refresh;
            if (built)
                Refresh();
        }

        void OnDisable() => PresentationSettings.Changed -= Refresh;
        void Refresh()
        {
            if (!built)
                return;
            if (whistleHints)
                whistleHints.GetComponentInChildren<TMP_Text>().text = "Dicas de controles do apito: " + (WhistleSystem.HintsEnabled ? "ativadas" : "desativadas");
            gamma.SetValueWithoutNotify(PresentationSettings.Gamma);
            subtitleSize.SetValueWithoutNotify(PresentationSettings.SubtitleSize);
            subtitleBackground.SetValueWithoutNotify(PresentationSettings.SubtitleBackground);
            reduceEffects.SetIsOnWithoutNotify(PresentationSettings.ReduceEffects);
            gammaValue.text = PresentationSettings.Gamma.ToString("0.00");
            sizeValue.text = Mathf.RoundToInt(PresentationSettings.SubtitleSize * 100) + "%";
            backgroundValue.text = Mathf.RoundToInt(PresentationSettings.SubtitleBackground * 100) + "%";
            for (int i = 0; i < swatches.Length; i++)
            {
                float level = Mathf.Pow(new[] { .014f, .028f, .055f }[i], 1 / PresentationSettings.Gamma);
                swatches[i].color = new Color(level, level, level);
            }

            float mark = Mathf.Pow(.065f, 1 / PresentationSettings.Gamma);
            symbol.color = new Color(mark, mark, mark);
            sample.fontSize = 26 * PresentationSettings.SubtitleSize;
            samplePlate.color = new Color(0, 0, 0, PresentationSettings.SubtitleBackground);
        }

        void LateUpdate()
        {
            if (!owner || !owner.gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
                return;
            }

            Layout();
        }

        void OnRectTransformDimensionsChange() => Layout();
        void Layout()
        {
            if (!frame)
                return;
            var canvasRect = (RectTransform)GetComponentInParent<Canvas>().transform;
            frame.sizeDelta = new Vector2(1180, 960);
            float scale = Mathf.Min(1, (canvasRect.rect.width - 48) / 1180, (canvasRect.rect.height - 40) / 960);
            frame.localScale = Vector3.one * Mathf.Max(.1f, scale);
        }
    }
}
