// Desenha a interface de um cartão.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HorrorGame
{
    public sealed class TutorialCardView : MonoBehaviour
    {
        public RectTransform panel, mediaRect;
        public TMP_Text title, body;
        public TutorialCardMedia media;
        public CanvasGroup fade;
        public bool HelpVisible { get; private set; }

        public UnityEngine.UI.Button understood, replay, helpBack;
        public readonly List<UnityEngine.UI.Button> helpButtons = new List<UnityEngine.UI.Button>();
        TutorialCardSystem system;
        TMP_FontAsset font;
        RectTransform controls;
        TMP_Text modeLabel;
        float age;
        bool textOnly;
        readonly List<RectTransform> edges = new List<RectTransform>();
        readonly List<GameObject> rows = new List<GameObject>();
        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = size;
            r.anchoredPosition = pos;
            return r;
        }

        static RectTransform Fill(string name, Transform parent)
        {
            var r = Rect(name, parent, Vector2.zero, Vector2.zero);
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }

        TMP_Text Text(string name, Transform parent, string value, Vector2 size, Vector2 pos, int points)
        {
            var t = Rect(name, parent, size, pos).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = value;
            t.fontSize = points;
            t.enableAutoSizing = false;
            t.color = new Color(.87f, .9f, .87f);
            t.alignment = TextAlignmentOptions.Left;
            t.raycastTarget = false;
            return t;
        }

        static void Plate(RectTransform r, Color color)
        {
            r.gameObject.AddComponent<UnityEngine.UI.Image>().color = color;
        }

        UnityEngine.UI.Button Button(string name, Transform parent, string label, Vector2 pos, Vector2 size, Action action)
        {
            var r = Rect(name, parent, size, pos);
            var image = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(.10f, .125f, .12f);
            var b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = image;
            var t = Text("Texto", r, label, size - new Vector2(18, 0), Vector2.zero, 21);
            t.alignment = TextAlignmentOptions.Center;
            b.onClick.AddListener(() =>
            {
                system.Play(system.Current ? system.Current.buttonSound : null);
                action();
            });
            return b;
        }

        public void Build(TutorialCardSystem owner, TMP_FontAsset face)
        {
            system = owner;
            InputSystem.onActionChange += BindingsChanged;
            font = face ? face : TMP_Settings.defaultFontAsset;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;
            canvas.pixelPerfect = true;
            var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            fade = gameObject.AddComponent<CanvasGroup>();
            var back = Fill("Fundo", transform);
            Plate(back, new Color(.005f, .008f, .009f, .88f));
            back.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
            {
                if (HelpVisible)
                    system.CloseHelp();
                else
                    system.Close();
            });
            panel = Rect("Cartão", transform, new Vector2(1440, 820), Vector2.zero);
            Plate(panel, new Color(.025f, .032f, .030f, .99f));
            for (int i = 0; i < 4; i++)
            {
                var edge = Rect("Vinheta " + i, panel, i < 2 ? new Vector2(6, 820) : new Vector2(1440, 6), i == 0 ? new Vector2(-717, 0) : i == 1 ? new Vector2(717, 0) : i == 2 ? new Vector2(0, 407) : new Vector2(0, -407));
                edges.Add(edge);
                Plate(edge, new Color(0, 0, 0, .25f));
                edge.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            }

            title = Text("Título", panel, "", new Vector2(640, 70), new Vector2(310, 292), 34);
            title.characterSpacing = 4;
            body = Text("Descrição", panel, "", new Vector2(640, 165), new Vector2(310, 174), 25);
            body.alignment = TextAlignmentOptions.TopLeft;
            mediaRect = Rect("Mídia", panel, new Vector2(590, 440), new Vector2(-360, 55));
            Plate(mediaRect, new Color(.012f, .019f, .018f));
            var raw = Fill("Imagem", mediaRect).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            raw.raycastTarget = false;
            var fit = raw.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 16f / 9;
            media = raw.gameObject.AddComponent<TutorialCardMedia>();
            media.image = raw;
            controls = Rect("Controles", panel, new Vector2(640, 250), new Vector2(310, -78));
            understood = Button("Entendi", panel, "Entendi", new Vector2(-440, -350), new Vector2(300, 58), () => system.Close());
            replay = Button("Ver de novo", panel, "Ver de novo", new Vector2(0, -350), new Vector2(300, 58), () => media.Replay());
            helpBack = Button("Voltar à pausa", panel, "Voltar", new Vector2(0, -350), new Vector2(300, 58), () => system.CloseHelp());
            gameObject.SetActive(false);
        }

        public void InstallMenuEntries()
        {
            if (!system.game.hud)
                return;
            var hud = system.game.hud;
            if (hud.pausePanel)
            {
                var b = Button("Ajuda de itens", hud.pausePanel.transform, "Ajuda", Vector2.zero, new Vector2(180, 48), () => system.ShowHelp());
                var rect = (RectTransform)b.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(24, 24);
            }

            if (hud.optionsPanel)
            {
                var b = Button("Dicas de item", hud.optionsPanel.transform, "", Vector2.zero, new Vector2(330, 48), () =>
                {
                    system.SetMode((TutorialMode)(((int)TutorialPreferences.Mode + 1) % 3));
                    RefreshMode();
                });
                var rect = (RectTransform)b.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(24, 88);
                modeLabel = b.GetComponentInChildren<TMP_Text>();
                RefreshMode();
                system.Changed += RefreshMode;
            }
        }

        void RefreshMode()
        {
            if (modeLabel)
                modeLabel.text = "Dicas de item: " + (TutorialPreferences.Mode == TutorialMode.Full ? "Completas" : TutorialPreferences.Mode == TutorialMode.TextOnly ? "Só texto" : "Desligadas");
        }

        public void Show(TutorialCardData card, bool onlyText)
        {
            gameObject.SetActive(true);
            HelpVisible = false;
            textOnly = onlyText;
            age = 0;
            fade.alpha = 0;
            title.text = card.title.ToUpperInvariant();
            body.text = string.Join("\n", card.lines ?? Array.Empty<string>());
            ClearRows();
            foreach (var b in helpButtons)
                if (b)
                    b.gameObject.SetActive(false);
            understood.gameObject.SetActive(true);
            helpBack.gameObject.SetActive(false);
            mediaRect.gameObject.SetActive(!textOnly);
            media.image.GetComponent<UnityEngine.UI.AspectRatioFitter>().aspectRatio = card.mediaKind == TutorialMediaKind.Model3D ? 4f / 3 : card.image ? (float)card.image.width / card.image.height : 16f / 9;
            media.Show(card, textOnly);
            replay.gameObject.SetActive(!textOnly && (card.mediaKind == TutorialMediaKind.Model3D && card.model || card.mediaKind == TutorialMediaKind.Video && card.video));
            foreach (var control in card.controls ?? Array.Empty<TutorialControl>())
                AddControl(control);
            Layout();
            Select(understood);
        }

        void AddControl(TutorialControl data)
        {
            var row = Rect("Controle " + data.actionId, controls, new Vector2(640, 46), new Vector2(0, 98 - rows.Count * 53));
            rows.Add(row.gameObject);
            var binding = TutorialBindings.Resolve(system.game.input ? system.game.input.Actions : null, data.actionId, PhoneMessages.For(system.game));
            var key = Rect("Glifo", row, new Vector2(138, 38), new Vector2(-249, 0));
            Plate(key, new Color(.13f, .16f, .15f));
            if (binding.glyph == TutorialGlyph.Key)
                Text("Tecla atual", key, binding.text, new Vector2(130, 35), Vector2.zero, 20).alignment = TextAlignmentOptions.Center;
            else
                DrawMouse(key, binding.glyph, data.hold);
            var label = Text("Ação", row, data.label, new Vector2(468, 42), new Vector2(78, 0), 23);
            label.alignment = TextAlignmentOptions.MidlineLeft;
        }

        void DrawMouse(RectTransform parent, TutorialGlyph glyph, bool hold)
        {
            var color = new Color(.8f, .87f, .83f);
            float x = -35;
            foreach (var part in new[]
            {
                new Vector4(x - 12, 0, 2, 26),
                new Vector4(x + 12, 0, 2, 26),
                new Vector4(x, 14, 20, 2),
                new Vector4(x, -14, 20, 2),
                new Vector4(x, 5, 2, 16),
                new Vector4(x, 0, 24, 2)
            }

            )
            {
                var r = Rect("Mouse", parent, new Vector2(part.z, part.w), new Vector2(part.x, part.y));
                Plate(r, color);
                r.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            }

            var marker = Rect("Botão", parent, new Vector2(glyph == TutorialGlyph.Wheel ? 4 : 9, glyph == TutorialGlyph.Wheel ? 13 : 9), new Vector2(x + (glyph == TutorialGlyph.LeftMouse ? -6 : glyph == TutorialGlyph.RightMouse ? 6 : 0), 7));
            Plate(marker, new Color(.47f, .68f, .61f));
            Text("Gesto", parent, hold ? "SEGURAR" : glyph == TutorialGlyph.Wheel ? "RODA" : glyph == TutorialGlyph.Move ? "MOVER" : "CLIQUE", new Vector2(72, 34), new Vector2(29, 0), 17).alignment = TextAlignmentOptions.Center;
        }

        void ClearRows()
        {
            foreach (var row in rows)
            {
                row.SetActive(false);
                Destroy(row);
            }

            rows.Clear();
        }

        public void ShowHelp()
        {
            gameObject.SetActive(true);
            HelpVisible = true;
            age = 0;
            fade.alpha = 0;
            ClearRows();
            media.Release();
            mediaRect.gameObject.SetActive(false);
            title.text = "AJUDA";
            body.text = "";
            understood.gameObject.SetActive(false);
            replay.gameObject.SetActive(false);
            helpBack.gameObject.SetActive(true);
            foreach (var b in helpButtons)
                if (b)
                    Destroy(b.gameObject);
            helpButtons.Clear();
            int i = 0;
            foreach (var card in system.HelpCards)
            {
                var value = card;
                helpButtons.Add(Button("Rever " + card.id, panel, card.title.ToUpperInvariant(), new Vector2(0, 230 - i++ * 86), new Vector2(900, 62), () => system.Open(value.id, true)));
            }

            if (i == 0)
                body.text = "Ainda não há dicas para rever.";
            Layout();
            Select(helpButtons.Count > 0 ? helpButtons[0] : helpBack);
        }

        public void Hide()
        {
            media.Release();
            HelpVisible = false;
            gameObject.SetActive(false);
        }

        static void Select(UnityEngine.UI.Button b)
        {
            if (EventSystem.current && b)
                EventSystem.current.SetSelectedGameObject(b.gameObject);
        }

        public void ReadKeyboard()
        {
            var key = Keyboard.current;
            if (key == null)
                return;
            if (key.escapeKey.wasPressedThisFrame)
            {
                if (HelpVisible)
                    system.CloseHelp();
                else
                    system.Close();
                return;
            }

            if (key.tabKey.wasPressedThisFrame)
            {
                var buttons = HelpVisible ? helpButtons.ToArray() : new[]
                {
                    understood,
                    replay
                };
                var visible = new List<UnityEngine.UI.Button>();
                foreach (var b in buttons)
                    if (b && b.gameObject.activeInHierarchy)
                        visible.Add(b);
                int current = visible.FindIndex(b => EventSystem.current && EventSystem.current.currentSelectedGameObject == b.gameObject);
                if (visible.Count > 0)
                    Select(visible[(current + 1) % visible.Count]);
            }

            if ((key.enterKey.wasPressedThisFrame || key.spaceKey.wasPressedThisFrame) && (!EventSystem.current || !EventSystem.current.currentSelectedGameObject))
                system.Close();
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float duration = PresentationSettings.ReduceEffects ? .08f : .22f;
            fade.alpha = Mathf.Clamp01(age / duration);
            Layout();
        }

        void Layout()
        {
            if (!panel)
                return;
            var canvas = (RectTransform)transform;
            bool narrow = canvas.rect.width / canvas.rect.height < 1.4f;
            Vector2 size = HelpVisible ? new Vector2(1100, 820) : textOnly ? new Vector2(1020, 820) : narrow ? new Vector2(1020, 960) : new Vector2(1440, 820);
            panel.sizeDelta = size;
            for (int i = 0; i < edges.Count; i++)
            {
                edges[i].sizeDelta = i < 2 ? new Vector2(6, size.y) : new Vector2(size.x, 6);
                edges[i].anchoredPosition = i == 0 ? new Vector2(-size.x * .5f + 3, 0) : i == 1 ? new Vector2(size.x * .5f - 3, 0) : i == 2 ? new Vector2(0, size.y * .5f - 3) : new Vector2(0, -size.y * .5f + 3);
            }

            float scale = Mathf.Max(.1f, Mathf.Min(1, (canvas.rect.width - 48) / size.x, (canvas.rect.height - 40) / size.y));
            panel.localScale = Vector3.one * scale * (PresentationSettings.ReduceEffects ? 1 : Mathf.Lerp(.985f, 1, Mathf.Clamp01(age / .22f)));
            if (HelpVisible)
            {
                title.rectTransform.anchoredPosition = new Vector2(0, 330);
                title.rectTransform.sizeDelta = new Vector2(900, 70);
                title.alignment = TextAlignmentOptions.Center;
                body.rectTransform.anchoredPosition = new Vector2(0, 0);
                helpBack.transform.localPosition = new Vector3(0, -350);
                return;
            }

            if (textOnly)
            {
                title.alignment = TextAlignmentOptions.Left;
                title.rectTransform.anchoredPosition = new Vector2(0, 292);
                title.rectTransform.sizeDelta = new Vector2(850, 70);
                body.rectTransform.anchoredPosition = new Vector2(0, 170);
                body.rectTransform.sizeDelta = new Vector2(850, 165);
                controls.anchoredPosition = new Vector2(0, -78);
                LayoutFooter(-350);
                return;
            }

            title.alignment = TextAlignmentOptions.Left;
            title.rectTransform.anchoredPosition = narrow ? new Vector2(0, 408) : new Vector2(310, 292);
            title.rectTransform.sizeDelta = narrow ? new Vector2(900, 60) : new Vector2(640, 70);
            body.rectTransform.anchoredPosition = narrow ? new Vector2(0, 52) : new Vector2(310, 174);
            body.rectTransform.sizeDelta = narrow ? new Vector2(900, 150) : new Vector2(640, 165);
            mediaRect.sizeDelta = narrow ? new Vector2(700, 248) : new Vector2(590, 440);
            mediaRect.anchoredPosition = narrow ? new Vector2(0, 244) : new Vector2(-360, 55);
            controls.anchoredPosition = narrow ? new Vector2(0, -178) : new Vector2(310, -78);
            float y = narrow ? -418 : -350;
            LayoutFooter(y);
        }

        void LayoutFooter(float y)
        {
            bool animated = replay.gameObject.activeSelf;
            understood.GetComponent<RectTransform>().anchoredPosition = new Vector2(animated ? -180 : 0, y);
            replay.GetComponent<RectTransform>().anchoredPosition = new Vector2(180, y);
        }

        void BindingsChanged(object subject, InputActionChange change)
        {
            if (change != InputActionChange.BoundControlsChanged || !system || !system.Current || HelpVisible)
                return;
            ClearRows();
            foreach (var control in system.Current.controls)
                AddControl(control);
        }

        void OnDestroy()
        {
            InputSystem.onActionChange -= BindingsChanged;
            if (system)
                system.Changed -= RefreshMode;
        }
    }
}
