// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace HorrorGame.Editor
{
    /// <summary>Styles the existing HUD and preserves gameplay/save/input references.</summary>
    public static class HudPresentationPass
    {
        const string PrefabPath = "Assets/HorrorGameplay/Prefabs/GameplayRoot.prefab";
        const string ReportPath = "Assets/ChapterOneOffice/Documentation/HUD-presentation-report.json";
        static readonly string[] ScenePaths = {
            "Assets/HorrorGameplay/Scenes/TestScene.unity",
            "Assets/ChapterOneOffice/Scenes/Chapter1Office.unity"
        };
        static readonly Color Ink = new Color(.86f, .86f, .81f, 1);
        static readonly Color Quiet = new Color(.56f, .59f, .56f, 1);
        static readonly Color Accent = new Color(.60f, .24f, .20f, 1);
        static readonly Color Card = new Color(.026f, .031f, .029f, .98f);
        static TMP_FontAsset font;

        [Serializable] sealed class Report
        {
            public string generatedUtc;
            public bool passed;
            public string[] targets, errors;
        }

        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop PlayMode before polishing the HUD.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Polish(RequiredHud(root));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            foreach (string path in ScenePaths)
                WithScene(path, scene => {
                    foreach (var hud in Huds(scene)) Polish(hud);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                });
            AssetDatabase.SaveAssets();
            return "HUD polished in GameplayRoot, TestScene and Chapter1Office; gameplay references preserved.";
        }

        public static string Validate()
        {
            var errors = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { Check(RequiredHud(root), PrefabPath, errors); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            foreach (string path in ScenePaths)
                WithScene(path, scene => {
                    var found = Huds(scene).ToArray();
                    if (found.Length != 1) errors.Add(path + ": expected one gameplay HUD.");
                    foreach (var hud in found) Check(hud, path, errors);
                    int eventSystems = scene.GetRootGameObjects().Sum(g => g.GetComponentsInChildren<EventSystem>(true).Length);
                    if (eventSystems != 1) errors.Add(path + ": expected exactly one EventSystem.");
                });
            var report = new Report { generatedUtc = DateTime.UtcNow.ToString("O"), passed = errors.Count == 0,
                targets = new[] { PrefabPath, ScenePaths[0], ScenePaths[1] }, errors = errors.ToArray() };
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            if (errors.Count > 0) throw new InvalidOperationException("HUD validation failed: " + string.Join("; ", errors));
            return "HUD validation PASS: 3 targets, existing references and decorative language picker wired.";
        }

        static IEnumerable<GameHUD> Huds(Scene scene) => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameHUD>(true));
        static GameHUD RequiredHud(GameObject root)
        {
            var hud = root.GetComponentInChildren<GameHUD>(true);
            if (!hud) throw new InvalidOperationException("Existing gameplay HUD was not found.");
            return hud;
        }

        static void WithScene(string path, Action<Scene> action)
        {
            var original = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { action(scene); }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void Polish(GameHUD hud)
        {
            font = hud.prompt.font;
            if (!font) throw new InvalidOperationException("The existing TMP font is required.");
            var canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = hud.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;

            foreach (var text in hud.GetComponentsInChildren<TMP_Text>(true))
            {
                text.raycastTarget = false;
                text.enableAutoSizing = false;
                text.color = Ink;
            }
            Set(hud.crosshair.rectTransform, new Vector2(26, 26), Vector2.zero, new Vector2(.5f, .5f));
            TextStyle(hud.crosshair, 18, TextAlignmentOptions.Center, new Color(.75f, .76f, .72f, .43f));

            var interaction = Rect("Interaction plate", hud.transform, new Vector2(640, 58), new Vector2(0, 106), new Vector2(.5f, 0));
            var plate = Image(interaction, new Color(.011f, .015f, .013f, .75f));
            plate.raycastTarget = false;
            hud.interactionGroup = Group(interaction, 0);
            hud.prompt.transform.SetParent(interaction, false);
            Set(hud.prompt.rectTransform, new Vector2(542, 44), new Vector2(27, 0), new Vector2(.5f, .5f));
            TextStyle(hud.prompt, 21, TextAlignmentOptions.Left, Ink);
            var key = Rect("Key badge", interaction, new Vector2(48, 34), new Vector2(-278, 0), new Vector2(.5f, .5f));
            Image(key, new Color(.13f, .145f, .13f, .86f));
            hud.interactionKey = Text("Key", key, "E", new Vector2(46, 32), Vector2.zero, 19, TextAlignmentOptions.Center, Ink);

            Set(hud.staminaGroup.GetComponent<RectTransform>(), new Vector2(148, 3), new Vector2(0, 78), new Vector2(.5f, 0));
            var barSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            if (!barSprite) barSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (!barSprite) throw new InvalidOperationException("Built-in uGUI bar sprite was not found.");
            hud.staminaFill.sprite = barSprite;
            hud.batteryFill.sprite = barSprite;
            hud.staminaFill.color = new Color(.68f, .69f, .62f, .8f);
            hud.staminaGroup.alpha = 0;

            var battery = Rect("Flashlight status", hud.transform, new Vector2(205, 44), new Vector2(-42, 38), new Vector2(1, 0));
            hud.batteryGroup = Group(battery, .45f);
            var batteryIcon = hud.batteryFill.transform.parent.GetComponent<RectTransform>();
            batteryIcon.SetParent(battery, false);
            Set(batteryIcon, new Vector2(36, 15), new Vector2(60, 0), new Vector2(.5f, .5f));
            hud.batteryLabel.transform.SetParent(battery, false);
            Set(hud.batteryLabel.rectTransform, new Vector2(60, 26), new Vector2(8, 0), new Vector2(.5f, .5f));
            TextStyle(hud.batteryLabel, 16, TextAlignmentOptions.Right, Ink);
            hud.batteryStateText = Text("Flashlight state", battery, "LANTERNA DESLIGADA", new Vector2(200, 20), new Vector2(0, 27), 12, TextAlignmentOptions.Right, Quiet);
            Text("Flashlight key", battery, "F", new Vector2(24, 24), new Vector2(-61, 0), 13, TextAlignmentOptions.Center, Quiet);

            Set(hud.objective.rectTransform, new Vector2(590, 78), new Vector2(50, -122), Vector2.up);
            TextStyle(hud.objective, 22, TextAlignmentOptions.TopLeft, Ink);
            hud.objective.margin = new Vector4(0, 28, 0, 0);
            var objectiveTitle = Text("Objective heading", hud.objective.transform, "INVESTIGAÇÃO", new Vector2(570, 24), new Vector2(0, 0), 12, TextAlignmentOptions.Left, new Color(.66f, .60f, .50f));
            Set(objectiveTitle.rectTransform, new Vector2(570, 24), Vector2.zero, Vector2.up);
            objectiveTitle.characterSpacing = 4;
            Image(Rect("Objective accent", hud.objective.transform, new Vector2(2, 58), new Vector2(-14, -8), Vector2.up), Accent);
            hud.objectiveGroup.alpha = 0;

            var subtitlePlate = Rect("Subtitle plate", hud.transform, new Vector2(980, 82), new Vector2(0, 196), new Vector2(.5f, 0));
            Image(subtitlePlate, new Color(.009f, .012f, .01f, .78f));
            hud.subtitle.transform.SetParent(subtitlePlate, false);
            Set(hud.subtitle.rectTransform, new Vector2(922, 66), Vector2.zero, new Vector2(.5f, .5f));
            TextStyle(hud.subtitle, 25, TextAlignmentOptions.Center, Ink);
            hud.subtitle.lineSpacing = 8;
            var oldGroup = hud.subtitle.GetComponent<CanvasGroup>();
            if (oldGroup) oldGroup.alpha = 1;
            hud.subtitleGroup = Group(subtitlePlate, 0);
            interaction.SetSiblingIndex(hud.flashImage.transform.GetSiblingIndex());
            battery.SetSiblingIndex(hud.flashImage.transform.GetSiblingIndex());
            subtitlePlate.SetSiblingIndex(hud.flashImage.transform.GetSiblingIndex());
            Set(hud.notification.rectTransform, new Vector2(920, 50), new Vector2(0, -76), new Vector2(.5f, 1));
            TextStyle(hud.notification, 20, TextAlignmentOptions.Center, Ink);

            Modal(hud.pausePanel, new Vector2(590, 460), "PAUSADO");
            Modal(hud.optionsPanel, new Vector2(960, 914), "OPÇÕES");
            Modal(hud.gameOverPanel, new Vector2(780, 474), "VOCÊ NÃO ESCAPOU");
            Modal(hud.notePanel, new Vector2(1080, 856), "DOCUMENTO");
            Modal(hud.inventoryPanel, new Vector2(1080, 856), "INVENTÁRIO");
            Button(hud.resumeButton, "Continuar", new Vector2(0, 38), 410);
            Button(hud.optionsButton, "Opções", new Vector2(0, -29), 410);
            Button(hud.mainMenuButton, "Voltar ao menu", new Vector2(0, -96), 410);
            Button(hud.restartButton, "Recomeçar do checkpoint", new Vector2(0, -40), 480);
            Button(hud.deathMenuButton, "Voltar ao menu", new Vector2(0, -108), 480);
            var deathTextTransform = hud.gameOverPanel.transform.Find("Content/Death text");
            var deathText = deathTextTransform ? deathTextTransform.GetComponent<TMP_Text>() : null;
            if (deathText) TextStyle(deathText, 22, TextAlignmentOptions.Center, Quiet);
            Button(hud.noteCloseButton, "Guardar nota", new Vector2(0, -343), 380);
            Button(hud.inventoryCloseButton, "Voltar", new Vector2(0, -343), 380);
            Set(hud.noteTitle.rectTransform, new Vector2(960, 82), new Vector2(0, 280), new Vector2(.5f, .5f));
            TextStyle(hud.noteTitle, 29, TextAlignmentOptions.Center, Ink);
            ScrollBody(hud.noteBody, 22);
            ScrollBody(hud.inventoryText, 22);
            Options(hud);

            foreach (var group in hud.GetComponentsInChildren<CanvasGroup>(true))
            {
                if (group == hud.deathCurtain || group == hud.staminaGroup || group == hud.objectiveGroup || group == hud.subtitleGroup || group == hud.interactionGroup || group == hud.batteryGroup)
                    group.blocksRaycasts = false;
            }
            foreach (var component in hud.GetComponentsInChildren<Component>(true))
            {
                if (!component) continue;
                EditorUtility.SetDirty(component);
                if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }

        static void Options(GameHUD hud)
        {
            var options = hud.options;
            var content = hud.optionsPanel.transform.Find("Content");
            var sliders = new[] { options.master, options.music, options.sfx, options.ambience, options.voice, options.sensitivity };
            for (int i = 0; i < sliders.Length; i++)
            {
                float y = 278 - i * 49;
                var slider = sliders[i];
                Set(slider.GetComponent<RectTransform>(), new Vector2(420, 24), new Vector2(160, y), new Vector2(.5f, .5f));
                if (slider.handleRect)
                {
                    // Inactive options may not have run Slider.OnEnable; establish its vertical stretch now.
                    var handle = slider.handleRect;
                    var anchorMin = handle.anchorMin;
                    var anchorMax = handle.anchorMax;
                    anchorMin.y = 0;
                    anchorMax.y = 1;
                    handle.anchorMin = anchorMin;
                    handle.anchorMax = anchorMax;
                    var handleSize = handle.sizeDelta;
                    handleSize.y = 0;
                    handle.sizeDelta = handleSize;
                    handle.ForceUpdateRectTransforms();
                }
                var labelTransform = content.Find(slider.name + " label");
                var label = labelTransform ? labelTransform.GetComponent<TMP_Text>() : null;
                if (label)
                {
                    Set(label.rectTransform, new Vector2(320, 34), new Vector2(-207, y), new Vector2(.5f, .5f));
                    TextStyle(label, 20, TextAlignmentOptions.Left, Ink);
                    if (slider == options.voice) label.text = "Voz";
                }
                foreach (var image in slider.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    image.color = image.gameObject.name == "Fill" ? new Color(.55f, .54f, .44f) : image.gameObject.name == "Handle" ? new Color(.84f, .83f, .72f) : new Color(.09f, .105f, .096f);
            }
            var toggles = new[] { options.invert, options.bob, options.sway };
            for (int i = 0; i < toggles.Length; i++)
            {
                Set(toggles[i].GetComponent<RectTransform>(), new Vector2(710, 38), new Vector2(0, -28 - i * 46), new Vector2(.5f, .5f));
                var label = toggles[i].GetComponentInChildren<TMP_Text>(true);
                Image(toggles[i].GetComponent<RectTransform>(), Color.clear, true);
                var box = toggles[i].transform.Find("Box") as RectTransform;
                if (box) Set(box, new Vector2(26, 26), new Vector2(-337, 0), new Vector2(.5f, .5f));
                if (label)
                {
                    Set(label.rectTransform, new Vector2(620, 38), new Vector2(21, 0), new Vector2(.5f, .5f));
                    TextStyle(label, 19, TextAlignmentOptions.Left, Ink);
                    if (toggles[i] == options.sway) label.text = "Oscilação sutil da câmera";
                }
            }
            Text("Language label", content, "Idioma", new Vector2(290, 34), new Vector2(-216, -237), 20, TextAlignmentOptions.Left, Ink);
            options.languagePreview = LanguageDropdown(content);
            options.languageNotice = Text("Language notice", content, "English em breve.", new Vector2(740, 42), new Vector2(0, -284), 14, TextAlignmentOptions.Center, Quiet);
            Button(hud.optionsBackButton, "Voltar", new Vector2(0, -356), 380);
        }

        static TMPro.TMP_Dropdown LanguageDropdown(Transform content)
        {
            var rect = Rect("Language preview", content, new Vector2(420, 40), new Vector2(160, -237), new Vector2(.5f, .5f));
            var dropdown = rect.GetComponent<TMPro.TMP_Dropdown>();
            if (!dropdown) dropdown = rect.gameObject.AddComponent<TMPro.TMP_Dropdown>();
            dropdown.targetGraphic = Image(rect, new Color(.08f, .094f, .083f), true);
            var caption = Text("Label", rect, "Português", new Vector2(366, 38), new Vector2(-10, 0), 19, TextAlignmentOptions.Left, Ink);
            Text("Arrow", rect, "v", new Vector2(26, 32), new Vector2(182, 0), 16, TextAlignmentOptions.Center, Quiet);
            var template = Rect("Template", rect, new Vector2(0, 94), new Vector2(0, -5), new Vector2(.5f, 0));
            template.anchorMin = Vector2.zero;
            template.anchorMax = Vector2.right;
            template.pivot = new Vector2(.5f, 1);
            Image(template, new Color(.033f, .043f, .035f, 1), true);
            var scroll = template.GetComponent<UnityEngine.UI.ScrollRect>();
            if (!scroll) scroll = template.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var viewport = Fill("Viewport", template);
            Image(viewport, Color.white, true);
            var mask = viewport.GetComponent<UnityEngine.UI.Mask>();
            if (!mask) mask = viewport.gameObject.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic = false;
            var items = Rect("Content", viewport, new Vector2(0, 88), Vector2.zero, Vector2.up);
            items.anchorMax = Vector2.one;
            items.pivot = Vector2.up;
            var item = Rect("Item", items, new Vector2(0, 42), Vector2.zero, Vector2.up);
            item.anchorMax = Vector2.one;
            item.pivot = Vector2.up;
            var toggle = item.GetComponent<UnityEngine.UI.Toggle>();
            if (!toggle) toggle = item.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = Image(item, new Color(.09f, .112f, .091f), true);
            var mark = Rect("Selected", item, new Vector2(3, 28), new Vector2(7, -7), Vector2.up);
            toggle.graphic = Image(mark, new Color(.72f, .65f, .49f));
            var itemText = Text("Item label", item, "Português", new Vector2(360, 40), new Vector2(20, -1), 19, TextAlignmentOptions.Left, Ink);
            Set(itemText.rectTransform, new Vector2(360, 40), new Vector2(20, -1), Vector2.up);
            dropdown.captionText = caption;
            dropdown.itemText = itemText;
            dropdown.template = template;
            dropdown.options = new List<TMPro.TMP_Dropdown.OptionData> {
                new TMPro.TMP_Dropdown.OptionData("Português"), new TMPro.TMP_Dropdown.OptionData("English")
            };
            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
            scroll.viewport = viewport;
            scroll.content = items;
            template.gameObject.SetActive(false);
            return dropdown;
        }

        static void ScrollBody(TMP_Text body, float size)
        {
            TextStyle(body, size, TextAlignmentOptions.TopLeft, Ink);
            body.lineSpacing = 12;
            body.paragraphSpacing = 14;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.margin = new Vector4(4, 6, 4, 12);
            var scroll = body.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (scroll) scroll.scrollSensitivity = 28;
        }

        static void Modal(GameObject modal, Vector2 size, string title)
        {
            var outer = modal.GetComponent<UnityEngine.UI.Image>();
            if (outer) { outer.color = new Color(.003f, .005f, .004f, .86f); outer.raycastTarget = true; }
            var content = modal.transform.Find("Content") as RectTransform;
            if (!content) throw new InvalidOperationException("Existing modal Content missing: " + modal.name);
            Set(content, size, Vector2.zero, new Vector2(.5f, .5f));
            Image(content, Card);
            var headingTransform = content.Find("Heading");
            var heading = headingTransform ? headingTransform.GetComponent<TMP_Text>() : null;
            if (heading)
            {
                heading.text = title;
                Set(heading.rectTransform, new Vector2(size.x - 100, 64), new Vector2(0, size.y * .5f - 96), new Vector2(.5f, .5f));
                TextStyle(heading, title == "DOCUMENTO" ? 13 : 30, TextAlignmentOptions.Center, title == "DOCUMENTO" ? Quiet : Ink);
                heading.characterSpacing = title == "DOCUMENTO" ? 5 : 7;
            }
            Image(Rect("Top rule", content, new Vector2(size.x - 90, 1), new Vector2(0, size.y * .5f - 42), new Vector2(.5f, .5f)), new Color(.43f, .39f, .32f, .38f));
            Image(Rect("Bottom rule", content, new Vector2(size.x - 90, 1), new Vector2(0, -size.y * .5f + 42), new Vector2(.5f, .5f)), new Color(.43f, .39f, .32f, .38f));
            var footer = Text("Game signature", content, "WHISPERS OF PAST", new Vector2(size.x - 110, 20), new Vector2(0, -size.y * .5f + 21), 10, TextAlignmentOptions.Center, Quiet);
            footer.characterSpacing = 4;
        }

        static void Button(UnityEngine.UI.Button button, string title, Vector2 position, float width)
        {
            Set(button.GetComponent<RectTransform>(), new Vector2(width, 50), position, new Vector2(.5f, .5f));
            var image = button.GetComponent<UnityEngine.UI.Image>();
            if (image) { image.color = new Color(.061f, .072f, .064f, .98f); image.raycastTarget = true; }
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label)
            {
                label.text = title;
                Set(label.rectTransform, new Vector2(width - 32, 48), Vector2.zero, new Vector2(.5f, .5f));
                TextStyle(label, 22, TextAlignmentOptions.Center, Color.white);
                button.targetGraphic = label;
            }
            button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Ink;
            colors.highlightedColor = new Color(.98f, .88f, .69f, 1);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.72f, .35f, .29f, 1);
            colors.disabledColor = new Color(.36f, .39f, .35f, .7f);
            colors.colorMultiplier = 1;
            colors.fadeDuration = .12f;
            button.colors = colors;
        }

        static void TextStyle(TMP_Text text, float size, TextAlignmentOptions alignment, Color color)
        {
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.enableAutoSizing = false;
        }
        static TMP_Text Text(string name, Transform parent, string value, Vector2 size, Vector2 position, float points, TextAlignmentOptions alignment, Color color)
        {
            var rect = Rect(name, parent, size, position, new Vector2(.5f, .5f));
            var text = rect.GetComponent<TMP_Text>();
            if (!text) text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            TextStyle(text, points, alignment, color);
            return text;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position, Vector2 anchor)
        {
            var rect = parent.Find(name) as RectTransform;
            if (!rect)
            {
                var go = new GameObject(name, typeof(RectTransform));
                rect = go.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
            }
            Set(rect, size, position, anchor);
            return rect;
        }
        static RectTransform Fill(string name, Transform parent)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f));
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        static void Set(RectTransform rect, Vector2 size, Vector2 position, Vector2 anchor)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
        }
        static UnityEngine.UI.Image Image(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            if (!image) image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }
        static CanvasGroup Group(RectTransform rect, float alpha)
        {
            var group = rect.GetComponent<CanvasGroup>();
            if (!group) group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = alpha;
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        static void Check(GameHUD hud, string target, List<string> errors)
        {
            if (!hud.game || !hud.player || !hud.interaction || !hud.input || !hud.flashlight || !hud.options)
                errors.Add(target + ": a gameplay reference is missing.");
            if (!hud.interactionKey || !hud.interactionGroup || !hud.batteryGroup || !hud.batteryStateText)
                errors.Add(target + ": presentation references are missing.");
            if (!hud.GetComponent<UnityEngine.UI.GraphicRaycaster>()) errors.Add(target + ": Canvas raycaster missing.");
            if (!hud.staminaFill.sprite || !hud.batteryFill.sprite) errors.Add(target + ": filled bars require sprites.");
            var options = hud.options;
            if (!options.languagePreview || options.languagePreview.options.Count != 2 || !options.languageNotice)
                errors.Add(target + ": decorative language picker missing.");
            if (options.languagePreview && (!options.languagePreview.template || !options.languagePreview.itemText || !options.languagePreview.captionText))
                errors.Add(target + ": incomplete dropdown template.");
            var optionSliders = new[] { options.master, options.music, options.sfx, options.ambience, options.voice, options.sensitivity };
            var optionAncestors = new List<GameObject>();
            if (hud.optionsPanel)
                for (var node = hud.optionsPanel.transform; node; node = node.parent) optionAncestors.Add(node.gameObject);
            var activeStates = optionAncestors.ToDictionary(go => go, go => go.activeSelf);
            var canvasStates = optionAncestors.Select(go => go.GetComponent<Canvas>()).Where(canvas => canvas)
                .ToDictionary(canvas => canvas, canvas => canvas.enabled);
            try
            {
                // Measure the visible UI. An inactive freshly loaded prefab instance can cache zero-height Rects.
                for (int i = optionAncestors.Count - 1; i >= 0; i--) optionAncestors[i].SetActive(true);
                foreach (var canvas in canvasStates.Keys) canvas.enabled = true;
                Canvas.ForceUpdateCanvases();
                if (hud.optionsPanel)
                {
                    foreach (var rect in hud.optionsPanel.GetComponentsInChildren<RectTransform>(true)) rect.ForceUpdateRectTransforms();
                    var content = hud.optionsPanel.transform.Find("Content") as RectTransform;
                    if (content) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                }
                Canvas.ForceUpdateCanvases();
                foreach (var slider in optionSliders)
                {
                    if (!slider || !slider.handleRect)
                    {
                        errors.Add(target + ": options slider handle missing.");
                        continue;
                    }
                    float rowY = slider.GetComponent<RectTransform>().anchoredPosition.y;
                    float rowSpace = float.PositiveInfinity;
                    foreach (var other in optionSliders)
                        if (other && other != slider)
                            rowSpace = Mathf.Min(rowSpace, Mathf.Abs(rowY - other.GetComponent<RectTransform>().anchoredPosition.y));
                    // Validate Unity's resolved rectangle, not the serialized sizeDelta correction.
                    slider.handleRect.ForceUpdateRectTransforms();
                    float handleHeight = slider.handleRect.rect.height;
                    if (handleHeight <= 0 || handleHeight >= rowSpace)
                        errors.Add(target + ": options handle overlaps adjacent rows: " + slider.name
                            + " (height " + handleHeight + ", row spacing " + rowSpace + ").");
                }
            }
            finally
            {
                foreach (var state in canvasStates) if (state.Key) state.Key.enabled = state.Value;
                foreach (var go in optionAncestors) if (go) go.SetActive(activeStates[go]);
            }
            foreach (var panel in new[] { hud.pausePanel, hud.optionsPanel, hud.gameOverPanel, hud.notePanel, hud.inventoryPanel })
            {
                var content = panel.transform.Find("Content") as RectTransform;
                if (!content || content.sizeDelta.x <= 0 || content.sizeDelta.y <= 0 || content.sizeDelta.y > 960)
                    errors.Add(target + ": invalid modal geometry " + panel.name);
            }
            foreach (var button in new[] { hud.resumeButton, hud.optionsButton, hud.optionsBackButton, hud.mainMenuButton, hud.restartButton, hud.deathMenuButton, hud.noteCloseButton, hud.inventoryCloseButton })
                if (!button || !button.targetGraphic || button.GetComponent<RectTransform>().sizeDelta.x <= 0)
                    errors.Add(target + ": button reference/geometry missing.");
            foreach (var scroll in hud.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true))
                if (!scroll.viewport || !scroll.content || !scroll.viewport.GetComponent<UnityEngine.UI.Mask>())
                    errors.Add(target + ": incomplete ScrollRect " + scroll.name);
        }
    }
}
