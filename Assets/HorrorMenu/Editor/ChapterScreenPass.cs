// Ferramenta de preparação da cena e da apresentação do menu.

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace SchoolMenu.Editor
{
    public static class ChapterScreenPass
    {
        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static TMP_Text Text(string name, Transform parent, TMP_Text sample, string content,
            Vector2 anchor, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var rect = Rect(name, parent, anchor, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = sample.font;
            text.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/HorrorMenu/Art/MenuWornText.mat");
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.horizontalMapping = TextureMappingOptions.Line;
            text.verticalMapping = TextureMappingOptions.Line;
            text.mappingUvLineOffset = .17f;
            return text;
        }

        static CanvasGroup Group(string name, Transform parent)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect.gameObject.AddComponent<CanvasGroup>();
        }

        static void Haunted(TMP_Text label)
        {
            var effect = label.gameObject.AddComponent<HauntedTitle>();
            effect.enabled = false;
            effect.title = label;
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("unevenBaseline").floatValue = .75f;
            serialized.FindProperty("maximumTilt").floatValue = .5f;
            serialized.FindProperty("maximumStretch").floatValue = .03f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            effect.enabled = true;
        }

        public static string Apply()
        {
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            var scene = menu.gameObject.scene;
            if (scene.path != "Assets/HorrorMenu/Scenes/MainMenu.unity" || EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Open MainMenu outside Play Mode.");
            if (menu.chapterSelection) return "Chapter screen already exists.";
            var menuGroup = Group("Menu presentation", menu.transform);
            foreach (var name in new[] {"Title","Location","Main options","Settings options","Status","Navigation hint","Settings heading"})
            {
                var child = menu.transform.Find(name);
                if (child) child.SetParent(menuGroup.transform, false);
            }
            menuGroup.transform.SetSiblingIndex(3);
            var group = Group("Chapter presentation", menu.transform);
            group.transform.SetSiblingIndex(4);
            var root = group.transform;
            var title = UnityEngine.Object.Instantiate(menu.title, root);
            title.name = "Story title";
            title.text = "<size=68>WHISPERS</size>\n<size=98>OF PAST</size>";
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0, .5f);
            title.rectTransform.pivot = new Vector2(0, 1);
            title.rectTransform.anchoredPosition = new Vector2(100, 320);
            title.rectTransform.sizeDelta = new Vector2(680, 250);
            title.gameObject.SetActive(true);

            var bone = new Color(.85f, .83f, .77f);
            Text("Story introduction", root, menu.title,
                "Uma escola abandonada. Um chamado que ninguém deveria ouvir.\n\nExplore os corredores, siga os sussurros e descubra o que ficou preso no passado.",
                new Vector2(0, .5f), new Vector2(104, 40), new Vector2(680, 200), 27, bone);
            Text("Expected play time", root, menu.title, "DURAÇÃO PREVISTA  ·  30–45 MINUTOS",
                new Vector2(0, .5f), new Vector2(104, -185), new Vector2(700, 55), 23, new Color(.62f,.61f,.55f));
            var card = Rect("Chapter one", root, new Vector2(.67f,.5f), new Vector2(0,95), new Vector2(520,238));
            var cardImage = card.gameObject.AddComponent<UnityEngine.UI.Image>();
            cardImage.color = new Color(.016f,.020f,.017f,.66f);
            var chapterButton = card.gameObject.AddComponent<UnityEngine.UI.Button>();
            chapterButton.targetGraphic = cardImage;
            var colors = chapterButton.colors;
            colors.highlightedColor = new Color(1.3f,1.2f,1.15f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = .18f;
            chapterButton.colors = colors;
            var rule = Rect("Chapter red margin", card, new Vector2(0,1), Vector2.zero, new Vector2(2,238));
            rule.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.56f,.075f,.045f,.75f);
            rule.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Text("Chapter number", card, menu.title, "CAPÍTULO I", new Vector2(0,1), new Vector2(28,-23), new Vector2(430,30), 21, new Color(.62f,.13f,.085f));
            var chapterName = Text("Chapter name", card, menu.title, "O TELEFONE", new Vector2(0,1), new Vector2(28,-67), new Vector2(460,64), 36, bone);
            chapterName.fontStyle = FontStyles.Bold;
            Haunted(chapterName);
            Text("Chapter premise", card, menu.title, "Uma ligação muda o rumo da investigação.", new Vector2(0,1), new Vector2(28,-137), new Vector2(450,55), 22, bone);
            Text("Chapter availability", card, menu.title, "EM BREVE", new Vector2(0,1), new Vector2(28,-201), new Vector2(420,25), 18, new Color(.54f,.54f,.48f));
            var chapterFx = card.gameObject.AddComponent<MenuItemFX>();
            chapterFx.label = chapterName;
            var back = Rect("Return to menu", root, new Vector2(0,.5f), new Vector2(104,-295), new Vector2(270,56));
            var backImage = back.gameObject.AddComponent<UnityEngine.UI.Image>();
            backImage.color = Color.clear;
            var returnButton = back.gameObject.AddComponent<UnityEngine.UI.Button>();
            returnButton.targetGraphic = backImage;
            var backLabel = Text("Return label", back, menu.title, "<  VOLTAR", new Vector2(0,1), new Vector2(0,-5), new Vector2(260,45), 27, bone);
            var backFx = back.gameObject.AddComponent<MenuItemFX>();
            backFx.label = backLabel;
            var status = Text("Chapter status", root, menu.title, "", new Vector2(0,.5f), new Vector2(104,-370), new Vector2(1100,65), 22, bone);
            var navigation = new UnityEngine.UI.Navigation {mode=UnityEngine.UI.Navigation.Mode.Explicit};
            navigation.selectOnLeft = navigation.selectOnUp = navigation.selectOnDown = returnButton;
            chapterButton.navigation = navigation;
            navigation.selectOnRight = navigation.selectOnUp = navigation.selectOnDown = chapterButton;
            navigation.selectOnLeft = null;
            returnButton.navigation = navigation;
            var controller = menu.gameObject.AddComponent<ChapterSelection>();
            controller.atmosphere = UnityEngine.Object.FindAnyObjectByType<MenuAtmosphere>();
            controller.menuGroup = menuGroup;
            controller.chapterGroup = group;
            controller.chapterButton = chapterButton;
            controller.returnButton = returnButton;
            controller.chapterStatus = status;
            menu.chapterSelection = controller;
            group.alpha = 0;
            group.interactable = group.blocksRaycasts = false;
            group.gameObject.SetActive(false);
            menu.transform.Find("Fade curtain").SetAsLastSibling();
            EditorUtility.SetDirty(menu);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "New game chapter presentation saved.";
        }
    }
}

