// Ferramenta de preparação ou verificação usada no Editor.

using HorrorGame;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace HorrorGame.Editor
{
    public static class GameplayUI
    {
        static TMP_FontAsset font;
        static readonly Color Ink = new Color(.8f, .84f, .83f), PanelColor = new Color(.025f, .033f, .042f, .96f);
        public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position, Vector2? anchor = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(.5f, .5f);
            rect.pivot = rect.anchorMin;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        static RectTransform Fill(string name, Transform parent)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static UnityEngine.UI.Image Image(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string value, Vector2 size, Vector2 position, float pointSize = 24, Vector2? anchor = null, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var rect = Rect(name, parent, size, position, anchor);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = pointSize;
            text.color = Ink;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        static UnityEngine.UI.Button Button(string name, Transform parent, string value, Vector2 position, Vector2? size = null)
        {
            var rect = Rect(name, parent, size ?? new Vector2(380, 54), position);
            var image = Image(rect, new Color(.08f, .1f, .115f), true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.7f, 1.9f, 1.85f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.7f, .4f, .35f);
            button.colors = colors;
            Text(name + " label", rect, value, new Vector2(rect.sizeDelta.x - 20, rect.sizeDelta.y), Vector2.zero, 24);
            return button;
        }

        static GameObject Modal(string name, Transform parent, string title, Vector2 size)
        {
            var outer = Fill(name, parent);
            Image(outer, new Color(.005f, .007f, .01f, .84f), true);
            var panel = Rect("Content", outer, size, Vector2.zero);
            Image(panel, PanelColor);
            Text("Heading", panel, title, new Vector2(size.x - 70, 70), new Vector2(0, size.y * .5f - 72), 32);
            return outer.gameObject;
        }

        static UnityEngine.UI.Slider Slider(string name, Transform parent, Vector2 position, float minimum = 0, float maximum = 1)
        {
            Text(name + " label", parent, name, new Vector2(300, 42), position + new Vector2(-230, 0), 22, alignment: TextAlignmentOptions.Left);
            var rect = Rect(name, parent, new Vector2(420, 24), position + new Vector2(130, 0));
            var background = Image(rect, new Color(.07f, .085f, .1f), true);
            var fillArea = Fill("Fill area", rect);
            fillArea.offsetMin = new Vector2(8, 7);
            fillArea.offsetMax = new Vector2(-8, -7);
            var fill = Fill("Fill", fillArea);
            Image(fill, new Color(.55f, .62f, .59f));
            var handle = Rect("Handle", rect, new Vector2(14, 30), Vector2.zero);
            var handleImage = Image(handle, new Color(.8f, .84f, .81f), true);
            var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.value = minimum;
            return slider;
        }

        static UnityEngine.UI.Toggle Toggle(string name, Transform parent, Vector2 position)
        {
            var rect = Rect(name, parent, new Vector2(610, 42), position);
            var box = Rect("Box", rect, new Vector2(28, 28), new Vector2(-282, 0));
            var background = Image(box, new Color(.13f, .16f, .18f), true);
            var mark = Rect("Check", box, new Vector2(18, 18), Vector2.zero);
            var check = Image(mark, new Color(.68f, .74f, .68f));
            var toggle = rect.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check;
            toggle.isOn = true;
            Text("Label", rect, name, new Vector2(535, 42), new Vector2(30, 0), 22, alignment: TextAlignmentOptions.Left);
            return toggle;
        }

        static TMP_Text ScrollingBody(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var root = Rect(name, parent, size, position);
            var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var viewport = Fill("Viewport", root);
            Image(viewport, new Color(.1f, .1f, .1f, .05f), true);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Fill("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(0, 0);
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var body = Text("Text", content, "", new Vector2(size.x - 24, 100), Vector2.zero, 25, alignment: TextAlignmentOptions.TopLeft);
            scroll.viewport = viewport;
            scroll.content = content;
            return body;
        }

        public static GameHUD Create(GameSession session, Transform parent)
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/HorrorMenu/TMP/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (!font)
                throw new System.InvalidOperationException("Imported TMP font is required.");
            var canvasObject = new GameObject("Gameplay HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var hud = canvasObject.AddComponent<GameHUD>();
            hud.game = session;
            hud.player = session.player;
            hud.interaction = session.interaction;
            hud.input = session.input;
            hud.flashlight = session.player.GetComponent<FlashlightController>();
            hud.crosshair = Text("Crosshair", canvasObject.transform, "·", new Vector2(28, 28), Vector2.zero, 28);
            hud.prompt = Text("Interaction prompt", canvasObject.transform, "", new Vector2(1000, 60), new Vector2(0, 118), 22, new Vector2(.5f, 0));
            var stamina = Rect("Stamina", canvasObject.transform, new Vector2(180, 4), new Vector2(0, 78), new Vector2(.5f, 0));
            Image(stamina, new Color(.01f, .02f, .025f, .7f));
            hud.staminaGroup = stamina.gameObject.AddComponent<CanvasGroup>();
            hud.staminaGroup.alpha = 0;
            hud.staminaGroup.blocksRaycasts = false;
            hud.staminaFill = Image(Fill("Fill", stamina), new Color(.6f, .65f, .63f));
            hud.staminaFill.type = UnityEngine.UI.Image.Type.Filled;
            hud.staminaFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            var battery = Rect("Battery", canvasObject.transform, new Vector2(38, 16), new Vector2(-54, 40), new Vector2(1, 0));
            Image(battery, new Color(.2f, .25f, .25f));
            var batteryInside = Fill("Charge", battery);
            batteryInside.offsetMin = new Vector2(3, 3);
            batteryInside.offsetMax = new Vector2(-3, -3);
            hud.batteryFill = Image(batteryInside, new Color(.6f, .68f, .62f));
            hud.batteryFill.type = UnityEngine.UI.Image.Type.Filled;
            hud.batteryFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            Image(Rect("Terminal", battery, new Vector2(4, 8), new Vector2(2, 4), new Vector2(1, 0)), new Color(.3f, .38f, .35f));
            hud.batteryLabel = Text("Battery percent", canvasObject.transform, "100%", new Vector2(88, 28), new Vector2(-105, 33), 17, new Vector2(1, 0), TextAlignmentOptions.Right);
            hud.objective = Text("Objective", canvasObject.transform, "", new Vector2(720, 95), new Vector2(40, 38), 23, Vector2.zero, TextAlignmentOptions.BottomLeft);
            hud.objectiveGroup = hud.objective.gameObject.AddComponent<CanvasGroup>();
            hud.objectiveGroup.blocksRaycasts = false;
            hud.objectiveGroup.alpha = 0;
            hud.subtitle = Text("Subtitles", canvasObject.transform, "", new Vector2(920, 100), new Vector2(0, 175), 26, new Vector2(.5f, 0));
            hud.subtitleGroup = hud.subtitle.gameObject.AddComponent<CanvasGroup>();
            hud.subtitleGroup.blocksRaycasts = false;
            hud.subtitleGroup.alpha = 0;
            hud.notification = Text("Notification", canvasObject.transform, "", new Vector2(1050, 55), new Vector2(0, -44), 22, new Vector2(.5f, 1));
            hud.flashImage = Image(Fill("Scare flash", canvasObject.transform), Color.clear);
            hud.flashImage.gameObject.SetActive(false);
            var curtain = Fill("Death blackout", canvasObject.transform);
            Image(curtain, Color.black);
            hud.deathCurtain = curtain.gameObject.AddComponent<CanvasGroup>();
            hud.deathCurtain.alpha = 0;
            hud.deathCurtain.blocksRaycasts = false;
            hud.pausePanel = Modal("Pause", canvasObject.transform, "PAUSADO", new Vector2(560, 430));
            var pause = hud.pausePanel.transform.Find("Content");
            hud.resumeButton = Button("Resume", pause, "Continuar", new Vector2(0, 52));
            hud.optionsButton = Button("Options", pause, "Opções", new Vector2(0, -18));
            hud.mainMenuButton = Button("Main menu", pause, "Voltar ao menu", new Vector2(0, -88));
            hud.optionsPanel = Modal("Options", canvasObject.transform, "OPÇÕES", new Vector2(940, 930));
            var optionsRoot = hud.optionsPanel.transform.Find("Content");
            var options = optionsRoot.gameObject.AddComponent<GameplayOptions>();
            hud.options = options;
            options.cameraMotion = session.player.view;
            options.master = Slider("Volume geral", optionsRoot, new Vector2(0, 315));
            options.music = Slider("Música", optionsRoot, new Vector2(0, 255));
            options.sfx = Slider("Efeitos", optionsRoot, new Vector2(0, 195));
            options.ambience = Slider("Ambiente", optionsRoot, new Vector2(0, 135));
            options.voice = Slider("Voz / respiração", optionsRoot, new Vector2(0, 75));
            options.sensitivity = Slider("Sensibilidade", optionsRoot, new Vector2(0, 15), .2f, 3);
            options.invert = Toggle("Inverter eixo Y", optionsRoot, new Vector2(0, -55));
            options.bob = Toggle("Movimento da câmera ao andar", optionsRoot, new Vector2(0, -115));
            options.sway = Toggle("Respiração da câmera", optionsRoot, new Vector2(0, -175));
            hud.optionsBackButton = Button("Options back", optionsRoot, "Voltar", new Vector2(0, -340));
            hud.gameOverPanel = Modal("Game over", canvasObject.transform, "VOCÊ NÃO ESCAPOU", new Vector2(750, 440));
            var death = hud.gameOverPanel.transform.Find("Content");
            Text("Death text", death, "O silêncio voltou.", new Vector2(600, 60), new Vector2(0, 48), 24);
            hud.restartButton = Button("Restart checkpoint", death, "Recomeçar do checkpoint", new Vector2(0, -30), new Vector2(460, 54));
            hud.deathMenuButton = Button("Death main menu", death, "Voltar ao menu", new Vector2(0, -105), new Vector2(460, 54));
            hud.notePanel = Modal("Read note", canvasObject.transform, "", new Vector2(1100, 850));
            var note = hud.notePanel.transform.Find("Content");
            hud.noteTitle = Text("Note title", note, "", new Vector2(950, 80), new Vector2(0, 315), 32);
            hud.noteBody = ScrollingBody(note, "Note scroll", new Vector2(950, 515), new Vector2(0, -15));
            hud.noteCloseButton = Button("Close note", note, "Guardar nota", new Vector2(0, -330));
            hud.inventoryPanel = Modal("Inventory", canvasObject.transform, "INVENTÁRIO", new Vector2(1100, 850));
            var inventory = hud.inventoryPanel.transform.Find("Content");
            hud.inventoryText = ScrollingBody(inventory, "Inventory scroll", new Vector2(950, 535), new Vector2(0, -5));
            hud.inventoryCloseButton = Button("Close inventory", inventory, "Voltar", new Vector2(0, -330));
            foreach (var hidden in new[]
            {
                hud.pausePanel,
                hud.optionsPanel,
                hud.gameOverPanel,
                hud.notePanel,
                hud.inventoryPanel
            }

            )
                hidden.SetActive(false);
            var eventSystem = new GameObject("Gameplay EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(parent, false);
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            return hud;
        }
    }
}
