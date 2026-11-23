// Ferramenta de preparação da cena e da apresentação do menu.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SchoolMenu.Editor
{
    /// <summary>Refines the existing menu; never rebuilds its scene, camera, door or save integration.</summary>
    public static class MenuPresentationPass
    {
        public const string ScenePath = "Assets/HorrorMenu/Scenes/MainMenu.unity";
        public const string Documentation = "Assets/HorrorMenu/Documentation";
        public const string ReportPath = Documentation + "/Menu-presentation-report.json";
        const float LampIntensity = 14f;
        const string TubeMaterialPath = "Assets/HorrorMenu/Art/MenuFluorescentTube.mat";
        static readonly Color Bone = new Color(.90f, .86f, .77f);
        static readonly Color Muted = new Color(.65f, .66f, .62f);
        static readonly Color Rust = new Color(.87f, .17f, .11f);
        static readonly string[] FutureTitles =
        {
            "O Caminho até a Escola", "A Escola Abandonada", "O Que Foi Deixado para Trás",
            "As Memórias", "O Terraço", "A Última Saída"
        };

        [Serializable]
        public sealed class Report
        {
            public bool passed;
            public string scene = ScenePath;
            public string[] errors = Array.Empty<string>();
            public string[] notes = Array.Empty<string>();
            public string[] chapters = Array.Empty<string>();
            public string[] captures = Array.Empty<string>();
            public int punctualShadowMaps;
            public string[] shadowLights = Array.Empty<string>();
            public float lampIntensity, lampBloomIntensity;
            public Vector3 lampPosition, lampFloorPoint;
            public bool lampEmissionEnabled, lampFloorPathClear;
            public string[] lampOccluders = Array.Empty<string>();
        }

        sealed class SceneLease : IDisposable
        {
            readonly Scene previous;
            readonly SceneSetup[] setup;
            readonly bool restore;
            public readonly Scene scene;
            public SceneLease()
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Pare o Play Mode antes de revisar o menu.");
                previous = SceneManager.GetActiveScene();
                scene = SceneManager.GetSceneByPath(ScenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                        if (SceneManager.GetSceneAt(i).isDirty)
                            throw new InvalidOperationException("Salve as cenas abertas antes de revisar o menu.");
                    setup = EditorSceneManager.GetSceneManagerSetup();
                    restore = true;
                    scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
                SceneManager.SetActiveScene(scene);
            }
            public void Dispose()
            {
                if (restore) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        static T InScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).FirstOrDefault();
        }
        static Transform Named(Scene scene, string name)
        {
            return scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == name);
        }
        static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
        static RectTransform Rect(string name, Transform parent)
        {
            var existing = parent.Find(name);
            if (existing) return (RectTransform)existing;
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }
        static TMP_Text Text(string name, Transform parent, TMP_Text sample)
        {
            var rect = Rect(name, parent);
            var text = rect.GetComponent<TMP_Text>();
            if (!text) text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = sample.font;
            text.fontSharedMaterial = sample.font.material;
            text.raycastTarget = false;
            text.richText = false;
            text.color = Bone;
            text.characterSpacing = 0;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
        static TMP_Text Caption(Transform parent, string name, string caption, TMP_Text sample,
            Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var text = Text(name, parent, sample);
            text.text = caption;
            text.fontSize = fontSize;
            text.color = color;
            Place(text.rectTransform, new Vector2(0, 1), new Vector2(0, 1), position, size);
            return text;
        }
        static void Float(UnityEngine.Object item, string field, float value)
        {
            if (!item) return;
            var serialized = new SerializedObject(item);
            var property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException(item.name + ": campo ausente " + field);
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static void TuneFocus(MenuItemFX fx)
        {
            if (!fx) return;
            var serialized = new SerializedObject(fx);
            serialized.FindProperty("normal").colorValue = Bone;
            serialized.FindProperty("selected").colorValue = new Color(.97f, .47f, .32f);
            serialized.FindProperty("dim").floatValue = .91f;
            serialized.FindProperty("shift").floatValue = 3f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static void TuneTitle(TMP_Text text, float first, float second)
        {
            text.text = "<size=" + first + ">WHISPERS</size>\n<size=" + second + ">OF PAST</size>";
            text.richText = true;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = -1.2f;
            text.lineSpacing = -5;
            text.color = Rust;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var haunted = text.GetComponent<HauntedTitle>();
            if (haunted)
            {
                Float(haunted, "unevenBaseline", 1.15f);
                Float(haunted, "maximumTilt", .75f);
                Float(haunted, "maximumStretch", .045f);
            }
            text.ForceMeshUpdate();
        }

        public static Report Apply()
        {
            using (var lease = new SceneLease())
            {
                var menu = InScene<MainMenuController>(lease.scene);
                Require(menu && menu.title && menu.mainPanel && menu.settingsPanel && menu.chapterSelection && menu.settingsManager,
                    "O menu existente está incompleto; nenhuma hierarquia foi recriada.");
                ApplyTypography(menu);
                ApplySettings(menu);
                ApplyChapters(menu);
                ApplyAtmosphere(lease.scene, menu);
                EditorUtility.SetDirty(menu.settingsManager);
                EditorSceneManager.MarkSceneDirty(lease.scene);
                EditorSceneManager.SaveScene(lease.scene);
                AssetDatabase.SaveAssets();
                var report = Validate(lease.scene, menu);
                SaveReport(report);
                Debug.Log("Whispers of Past: menu refinado. Prólogo disponível; seis capítulos futuros bloqueados.");
                return report;
            }
        }

        [Serializable] public sealed class PointerHit
        {
            public string button, resolution, firstHit;
            public bool received;
            public int graphicDepth, registeredRaycasters;
            public bool graphicActive, graphicCulled;
            public Vector2 screenPoint;
        }
        [Serializable] sealed class PointerFixReport
        {
            public string generatedUtc;
            public bool passed;
            public string[] restoredTargets;
            public PointerHit[] hits;
        }

        public static void RepairPointerTargets()
        {
            using (var lease = new SceneLease())
            {
                var menu = InScene<MainMenuController>(lease.scene);
                Require(menu, "Menu existente ausente.");
                var buttons = new[] { menu.continueButton, menu.newGameButton, menu.settingsButton, menu.quitButton, menu.backButton };
                var restored = new List<string>();
                foreach (var button in buttons)
                {
                    Require(button && button.targetGraphic, "Alvo gráfico de botão ausente.");
                    if (!button.targetGraphic.raycastTarget) restored.Add(button.name);
                    Undo.RecordObject(button.targetGraphic, "Restore menu pointer target");
                    button.targetGraphic.raycastTarget = true;
                    EditorUtility.SetDirty(button.targetGraphic);
                }
                EditorSceneManager.MarkSceneDirty(lease.scene);
                EditorSceneManager.SaveScene(lease.scene);
                AssetDatabase.SaveAssets();
                var hits = new List<PointerHit>();
                var paths = new List<string>();
                CaptureScreens(lease.scene, menu, paths, hits);
                bool passed = hits.Count == 10 && hits.All(hit => hit.received);
                var report = new PointerFixReport { generatedUtc = DateTime.UtcNow.ToString("O"), passed = passed,
                    restoredTargets = restored.ToArray(), hits = hits.ToArray() };
                File.WriteAllText(Documentation + "/Menu-pointer-fix-report.json", JsonUtility.ToJson(report, true));
                Require(passed, "Um botão ainda não recebe o raycast do mouse; consulte Menu-pointer-fix-report.json.");
                var presentation = Validate(lease.scene, menu);
                presentation.captures = paths.ToArray();
                SaveReport(presentation);
                Require(presentation.passed, "Validação do menu falhou após corrigir os cliques.");
            }
        }

        static void ApplyTypography(MainMenuController menu)
        {
            var canvas = menu.GetComponent<Canvas>();
            // Overlay letters bypass URP grading/grain, while the signal overlays remain below them.
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            var inputGraphics = menu.GetComponentsInChildren<UnityEngine.UI.Selectable>(true)
                .Where(selectable => selectable.targetGraphic).Select(selectable => selectable.targetGraphic).ToHashSet();
            foreach (var text in menu.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == menu.title || text.name == "Story title") continue;
                text.fontSharedMaterial = text.font.material;
                text.characterSpacing = 0;
                // Existing text-only buttons use their label as their sole pointer target.
                text.raycastTarget = inputGraphics.Contains(text);
            }
            TuneTitle(menu.title, 85, 120);
            Place(menu.title.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(88, 35), new Vector2(710, 300));
            Place((RectTransform)menu.mainPanel.transform, new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(-88, -25), new Vector2(345, 285));
            menu.mainPanel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing = 13;
            var buttons = new[] { menu.continueButton, menu.newGameButton, menu.settingsButton, menu.quitButton };
            var captions = new[] { "Continuar", "Novo jogo", "Opções", "Sair" };
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                var label = button.GetComponentInChildren<TMP_Text>(true);
                label.text = captions[i];
                label.fontSize = 35;
                label.fontStyle = FontStyles.Bold;
                label.color = Bone;
                button.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 60;
                var labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero; labelRect.offsetMax = Vector2.zero;
                TuneFocus(button.GetComponent<MenuItemFX>());
            }
            Float(menu, "titleFlickerDuration", .065f);
            var footer = menu.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Navigation hint");
            footer.text = "SETAS  NAVEGAR    ENTER  CONFIRMAR    ESC  VOLTAR";
            footer.fontSize = 16;
            footer.color = Muted;
            Place(footer.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(88, 38), new Vector2(1100, 34));
            menu.status.fontSize = 21;
            menu.status.color = Bone;
            Place(menu.status.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(88, 87), new Vector2(1050, 42));
            if (menu.fader && menu.fader.line)
            {
                menu.fader.line.fontSize = 24;
                menu.fader.line.color = Bone;
            }
        }

        static void ApplySettings(MainMenuController menu)
        {
            var settings = menu.settingsManager;
            var panel = (RectTransform)menu.settingsPanel.transform;
            Place(panel, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-88, -23), new Vector2(620, 567));
            var layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            string[] names = { "MASTER", "MUSIC", "SFX", "MOUSE SENSITIVITY", "RESOLUTION", "FULLSCREEN", "QUALITY", "INVERT Y" };
            string[] labels = { "Volume geral", "Música", "Efeitos", "Sensibilidade", "Resolução", "Tela cheia", "Qualidade", "Inverter Y" };
            for (int i = 0; i < names.Length; i++)
            {
                var row = panel.Find(names[i]);
                Require(row, "Configuração existente não encontrada: " + names[i]);
                var label = row.Find("Label").GetComponent<TMP_Text>();
                label.text = labels[i]; label.fontSize = 22; label.color = Bone;
                var value = row.Find("Value").GetComponent<TMP_Text>();
                value.fontSize = 21; value.color = Bone;
                var element = row.GetComponent<UnityEngine.UI.LayoutElement>();
                if (!element) element = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                element.preferredHeight = 48;
                TuneFocus(row.GetComponent<MenuItemFX>());
            }
            menu.settingsHeading.text = "Opções";
            menu.settingsHeading.fontSize = 46;
            menu.settingsHeading.fontStyle = FontStyles.Bold;
            menu.settingsHeading.color = Bone;
            Place(menu.settingsHeading.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(-88, 310), new Vector2(620, 70));
            var languageRect = Rect("LANGUAGE", panel);
            languageRect.SetSiblingIndex(8);
            var image = languageRect.GetComponent<UnityEngine.UI.Image>();
            if (!image) image = languageRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.clear; image.raycastTarget = true;
            var choice = languageRect.GetComponent<MenuChoice>();
            if (!choice) choice = languageRect.gameObject.AddComponent<MenuChoice>();
            choice.targetGraphic = image;
            var elementLanguage = languageRect.GetComponent<UnityEngine.UI.LayoutElement>();
            if (!elementLanguage) elementLanguage = languageRect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            elementLanguage.preferredHeight = 77;
            var labelLanguage = Caption(languageRect, "Label", "Idioma", menu.title, new Vector2(0, -8), new Vector2(250, 36), 22, Bone);
            var valueLanguage = Caption(languageRect, "Value", "Português", menu.title, new Vector2(273, -8), new Vector2(310, 36), 21, Bone);
            var notice = Caption(languageRect, "Availability", "", menu.title, new Vector2(273, -39), new Vector2(310, 36), 15, Muted);
            var fx = languageRect.GetComponent<MenuItemFX>();
            if (!fx) fx = languageRect.gameObject.AddComponent<MenuItemFX>();
            fx.label = labelLanguage; TuneFocus(fx);
            settings.language = choice; settings.languageValue = valueLanguage; settings.languageNotice = notice;
            settings.RefreshLanguagePreview();
            menu.backButton.transform.SetAsLastSibling();
            menu.backButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 48;
            var back = menu.backButton.GetComponentInChildren<TMP_Text>(true);
            back.text = "Voltar"; back.fontSize = 25; back.color = Bone;
            TuneFocus(menu.backButton.GetComponent<MenuItemFX>());
            var ordered = new UnityEngine.UI.Selectable[] { settings.master, settings.music, settings.sfx, settings.sensitivity,
                settings.resolution, settings.fullscreen, settings.quality, settings.invert, settings.language, menu.backButton };
            for (int i = 0; i < ordered.Length; i++)
            {
                var navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnUp = ordered[(i + ordered.Length - 1) % ordered.Length], selectOnDown = ordered[(i + 1) % ordered.Length] };
                ordered[i].navigation = navigation;
            }
        }

        static void ApplyChapters(MainMenuController menu)
        {
            var chapter = menu.chapterSelection;
            var parent = chapter.chapterGroup.transform;
            var title = parent.Find("Story title").GetComponent<TMP_Text>();
            TuneTitle(title, 67, 98);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(88, -143), new Vector2(620, 235));
            var intro = parent.Find("Story introduction").GetComponent<TMP_Text>();
            intro.text = "João de Claus é um investigador particular. Entre casos sem resposta e noites no escritório, uma ligação misteriosa dá início a uma nova investigação.";
            intro.fontSize = 26; intro.color = Bone; intro.lineSpacing = 8;
            intro.textWrappingMode = TextWrappingModes.Normal;
            Place(intro.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(91, -397), new Vector2(595, 205));
            var duration = parent.Find("Expected play time").GetComponent<TMP_Text>();
            duration.text = "DURAÇÃO  ·  A definir"; duration.fontSize = 18; duration.color = Muted;
            Place(duration.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(91, -610), new Vector2(590, 40));
            var card = (RectTransform)chapter.chapterButton.transform;
            Place(card, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-80, -88), new Vector2(540, 143));
            chapter.chapterButton.interactable = true;
            var cardImage = card.GetComponent<UnityEngine.UI.Image>();
            cardImage.color = new Color(.018f, .025f, .024f, .86f); cardImage.raycastTarget = true;
            Caption(card, "Chapter number", "PRÓLOGO", menu.title, new Vector2(25, -12), new Vector2(460, 27), 17, new Color(.93f, .47f, .32f));
            var name = Caption(card, "Chapter name", "O TELEFONE", menu.title, new Vector2(25, -41), new Vector2(480, 47), 33, Bone);
            name.fontStyle = FontStyles.Bold;
            Caption(card, "Chapter premise", "O escritório de João", menu.title, new Vector2(26, -85), new Vector2(450, 30), 20, Muted);
            Caption(card, "Chapter availability", "INICIAR PRÓLOGO", menu.title, new Vector2(26, -118), new Vector2(450, 24), 14, new Color(.86f, .73f, .57f));
            var margin = (RectTransform)card.Find("Chapter red margin");
            Place(margin, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(3, 143));
            margin.GetComponent<UnityEngine.UI.Image>().color = Rust;
            margin.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var fx = card.GetComponent<MenuItemFX>();
            if (fx) { fx.label = name; TuneFocus(fx); }
            var haunted = name.GetComponent<HauntedTitle>();
            if (haunted) { Float(haunted, "unevenBaseline", .55f); Float(haunted, "maximumTilt", .3f); Float(haunted, "maximumStretch", .015f); }
            for (int i = 0; i < FutureTitles.Length; i++)
            {
                var row = Rect("Future chapter " + (i + 1), parent);
                Place(row, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-80, -246 - i * 79), new Vector2(540, 70));
                var image = row.GetComponent<UnityEngine.UI.Image>();
                if (!image) image = row.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.color = new Color(.014f, .022f, .022f, .66f); image.raycastTarget = false;
                var button = row.GetComponent<UnityEngine.UI.Button>();
                if (!button) button = row.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = image; button.interactable = false;
                button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
                button.onClick.RemoveAllListeners();
                Caption(row, "Number", (i + 1).ToString("00"), menu.title, new Vector2(20, -15), new Vector2(35, 34), 22, new Color(.55f, .38f, .31f));
                var chapterName = Caption(row, "Name", FutureTitles[i], menu.title, new Vector2(65, -9), new Vector2(453, 38), 23, new Color(.76f, .75f, .69f));
                chapterName.fontStyle = FontStyles.Normal;
                Caption(row, "Availability", "EM BREVE", menu.title, new Vector2(66, -44), new Vector2(430, 24), 13, Muted);
            }
            var returnRect = (RectTransform)chapter.returnButton.transform;
            Place(returnRect, new Vector2(0, 0), new Vector2(0, 0), new Vector2(91, 60), new Vector2(310, 55));
            var back = chapter.returnButton.GetComponentInChildren<TMP_Text>(true);
            back.text = "‹  Voltar ao menu"; back.fontSize = 26; back.color = Bone;
            TuneFocus(chapter.returnButton.GetComponent<MenuItemFX>());
            chapter.chapterStatus.fontSize = 20; chapter.chapterStatus.color = Bone;
            Place(chapter.chapterStatus.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(91, 12), new Vector2(1040, 40));
            chapter.chapterButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnDown = chapter.returnButton, selectOnUp = chapter.returnButton, selectOnLeft = chapter.returnButton };
            chapter.returnButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnDown = chapter.chapterButton, selectOnUp = chapter.chapterButton, selectOnRight = chapter.chapterButton };
            chapter.chapterGroup.gameObject.SetActive(false);
        }

        static void ApplyAtmosphere(Scene scene, MainMenuController menu)
        {
            var analog = InScene<AnalogSignal>(scene);
            Require(analog && analog.distantEyes && analog.menuCamera, "Filtro/olhos/câmera existentes precisam ser preservados.");
            Float(analog, "noiseOpacity", .0045f);
            Float(analog, "scanlineOpacity", .028f);
            Float(analog, "trackingOpacity", .22f);
            Float(analog, "disturbanceDuration", 1.8f);
            Float(analog, "chromaticBaseline", .028f);
            if (analog.noiseLayer) { analog.noiseLayer.transform.SetSiblingIndex(0); analog.noiseLayer.color = new Color(1, 1, 1, .0045f); }
            if (analog.scanlines) { analog.scanlines.transform.SetSiblingIndex(1); analog.scanlines.color = new Color(1, 1, 1, .028f); }
            if (analog.bandTransform) analog.bandTransform.SetSiblingIndex(2);
            if (analog.trackingBand) analog.trackingBand.alpha = 0;
            // All letters render above the rolling signal. Their own worn glyph/focus effects remain.
            menu.chapterSelection.menuGroup.transform.SetAsLastSibling();
            menu.chapterSelection.chapterGroup.transform.SetAsLastSibling();
            if (menu.fader && menu.fader.curtain) menu.fader.curtain.transform.SetAsLastSibling();
            var profile = analog.filmVolume ? analog.filmVolume.sharedProfile : null;
            Require(profile, "Perfil URP do menu ausente.");
            if (profile.TryGet<FilmGrain>(out var grain)) { grain.intensity.Override(.052f); grain.response.Override(.72f); EditorUtility.SetDirty(grain); }
            if (profile.TryGet<ChromaticAberration>(out var chromatic)) { chromatic.intensity.Override(.028f); EditorUtility.SetDirty(chromatic); }
            if (profile.TryGet<Vignette>(out var vignette)) { vignette.intensity.Override(.35f); vignette.smoothness.Override(.68f); EditorUtility.SetDirty(vignette); }
            if (profile.TryGet<ColorAdjustments>(out var color)) { color.postExposure.Override(.04f); color.contrast.Override(18); color.saturation.Override(-30); EditorUtility.SetDirty(color); }
            if (profile.TryGet<DepthOfField>(out var depth)) { depth.gaussianMaxRadius.Override(.38f); EditorUtility.SetDirty(depth); }
            EditorUtility.SetDirty(profile);
            RenderSettings.fogDensity = .038f;
            var atmosphere = InScene<MenuAtmosphere>(scene);
            Float(atmosphere, "baseIntensity", LampIntensity);
            var near = Named(scene, "Near bounce");
            if (near) { var light = near.GetComponent<Light>(); light.intensity = .14f; light.shadows = LightShadows.None; EditorUtility.SetDirty(light); }
            var fluorescent = Named(scene, "Failing fluorescent");
            if (fluorescent) { var light = fluorescent.GetComponent<Light>(); light.intensity = LampIntensity; EditorUtility.SetDirty(light); }
            ApplyShadowBudget(scene);
            ConfigureLampLighting(scene, atmosphere, profile);
            // Keep the repaired left doorway, its dim spill, its wind motion and all distant eye materials.
            EditorUtility.SetDirty(analog);
        }

        static void ConfigureLampLighting(Scene scene, MenuAtmosphere atmosphere, VolumeProfile profile)
        {
            Require(atmosphere && atmosphere.tube && atmosphere.fluorescent && atmosphere.menuCamera, "Referências de flicker da luminária incompletas.");
            var tube = atmosphere.tube;
            var light = atmosphere.fluorescent;
            Require(light.name == "Failing fluorescent" && light.type == LightType.Spot, "Luminária principal precisa preservar o Spot existente.");
            // The spot originates below the diffuser/housing, aligned to the same visible fixture.
            light.transform.position = tube.bounds.center - Vector3.up * (tube.bounds.extents.y + .10f);
            light.transform.rotation = Quaternion.LookRotation(new Vector3(0, -1, .10f), Vector3.forward);
            light.spotAngle = 116;
            light.innerSpotAngle = 78;
            light.color = new Color(.82f, .86f, .73f);
            light.intensity = LampIntensity;
            Float(atmosphere, "baseIntensity", LampIntensity);
            var material = AssetDatabase.LoadAssetAtPath<Material>(TubeMaterialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.name = "Menu fluorescent tube";
                AssetDatabase.CreateAsset(material, TubeMaterialPath);
            }
            material.SetColor("_BaseColor", new Color(.64f, .69f, .53f));
            material.SetColor("_EmissionColor", new Color(.78f, .83f, .64f) * 2.8f);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", .14f);
            // Set valid GI flags before enabling the keyword so URP validation keeps emission.
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            UnityEditor.MaterialEditor.FixupEmissiveFlag(material);
            material.EnableKeyword("_EMISSION");
            tube.sharedMaterial = material;
            tube.enabled = true; // MenuAtmosphere continues to extinguish it during its existing flicker.
            if (!profile.TryGet<Bloom>(out var bloom))
            {
                bloom = profile.Add<Bloom>(true);
                AssetDatabase.AddObjectToAsset(bloom, profile);
            }
            bloom.active = true;
            bloom.intensity.Override(.14f);
            bloom.threshold.Override(1.2f);
            bloom.scatter.Override(.55f);
            atmosphere.menuCamera.allowHDR = true;
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(tube);
            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(atmosphere.menuCamera);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(profile);
        }

        static void ApplyShadowBudget(Scene scene)
        {
            var lights = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true)).ToArray();
            foreach (var light in lights)
            {
                // Each shadowed point reserves six atlas faces; fills need only their illumination.
                if (light.type == LightType.Point) { light.shadows = LightShadows.None; EditorUtility.SetDirty(light); }
            }
            var key = lights.FirstOrDefault(light => light.name == "Failing fluorescent");
            Require(key, "Luminária principal do corredor ausente.");
            key.type = LightType.Spot;
            key.transform.rotation = Quaternion.LookRotation(new Vector3(0, -1, .12f), Vector3.forward);
            key.spotAngle = 128;
            key.innerSpotAngle = 90;
            key.shadows = LightShadows.Soft;
            SetShadowTier(key);
            var doorway = lights.FirstOrDefault(light => light.name == "Left classroom interior light");
            Require(doorway && doorway.type == LightType.Spot, "Spot da fresta esquerda ausente.");
            doorway.shadows = LightShadows.Soft;
            SetShadowTier(doorway);
        }
        static void SetShadowTier(Light light)
        {
            var data = light.GetComponent<UniversalAdditionalLightData>();
            if (!data) data = light.gameObject.AddComponent<UniversalAdditionalLightData>();
            // URP's public setter rejects Edit Mode; modify its serialized tier through native Editor APIs.
            var serialized = new SerializedObject(data);
            serialized.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(light);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static Report Validate(Scene scene, MainMenuController menu)
        {
            var errors = new List<string>();
            Action<bool, string> check = (condition, message) => { if (!condition) errors.Add(message); };
            check(menu && menu.chapterSelection && menu.settingsManager, "Referências de runtime incompletas.");
            if (!menu || !menu.chapterSelection || !menu.settingsManager)
                return new Report { passed = false, errors = errors.ToArray() };
            var chapter = menu.chapterSelection;
            var settings = menu.settingsManager;
            var canvas = menu.GetComponent<Canvas>();
            check(canvas && canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(), "Canvas sem GraphicRaycaster.");
            int eventSystems = scene.GetRootGameObjects().Sum(g => g.GetComponentsInChildren<EventSystem>(true).Length);
            check(eventSystems == 1, "Menu deve conter exatamente um EventSystem.");
            check(InScene<InputSystemUIInputModule>(scene), "InputSystemUIInputModule ausente.");
            foreach (var button in new[] { menu.continueButton, menu.newGameButton, menu.settingsButton, menu.quitButton, menu.backButton })
                check(button && button.targetGraphic && button.targetGraphic.raycastTarget, "Botão sem área de clique: " + (button ? button.name : "ausente"));
            check(settings.language && settings.languageValue && settings.languageNotice, "Idioma decorativo sem referências.");
            check(settings.language && settings.language.navigation.selectOnUp == settings.invert && settings.language.navigation.selectOnDown == menu.backButton,
                "Idioma fora da navegação de configurações.");
            check(chapter.chapterButton && chapter.chapterButton.interactable, "Prólogo indisponível.");
            check(chapter.chapterButton.transform.Find("Chapter name").GetComponent<TMP_Text>().text == "O TELEFONE", "Nome do prólogo incorreto.");
            check(chapter.chapterButton.transform.Find("Chapter number").GetComponent<TMP_Text>().text == "PRÓLOGO", "Prólogo apresentado como capítulo numerado.");
            for (int i = 0; i < FutureTitles.Length; i++)
            {
                var row = chapter.chapterGroup.transform.Find("Future chapter " + (i + 1));
                check(row, "Capítulo futuro ausente: " + FutureTitles[i]);
                if (!row) continue;
                var button = row.GetComponent<UnityEngine.UI.Button>();
                check(button && !button.interactable, "Capítulo futuro habilitado: " + FutureTitles[i]);
                check(row.Find("Name").GetComponent<TMP_Text>().text == FutureTitles[i], "Ordem/nome incorreto: " + FutureTitles[i]);
                check(button && button.onClick.GetPersistentEventCount() == 0, "Capítulo futuro tem ação serializada.");
            }
            var serialized = new SerializedObject(menu);
            var catalog = serialized.FindProperty("gameplayScenes").objectReferenceValue as HorrorGame.SceneCatalog;
            string gameplay = catalog ? catalog.gameplayScene : serialized.FindProperty("firstGameScene").stringValue;
            check(gameplay == "Chapter1Office", "Prólogo não aponta para Chapter1Office.");
            var analog = InScene<AnalogSignal>(scene);
            check(analog && analog.distantEyes && analog.bandTransform && analog.menuCamera, "VHS/olhos/câmera perderam referências.");
            if (analog && analog.bandTransform)
                check(chapter.menuGroup.transform.GetSiblingIndex() > analog.bandTransform.GetSiblingIndex()
                    && chapter.chapterGroup.transform.GetSiblingIndex() > analog.bandTransform.GetSiblingIndex(), "Filtro está cobrindo letras.");
            check(Named(scene, "Left classroom hinge") && Named(scene, "Room visible through left doorway"), "Porta esquerda reparada ausente.");
            check(Named(scene, "Drifting corridor mist"), "Névoa do corredor ausente.");
            var shadowLights = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true))
                .Where(light => light.enabled && light.gameObject.activeInHierarchy && light.shadows != LightShadows.None).ToArray();
            int shadowMaps = shadowLights.Sum(light => light.type == LightType.Point ? 6 : light.type == LightType.Spot ? 1 : 0);
            check(!shadowLights.Any(light => light.type == LightType.Point), "Preenchimento Point está reservando seis faces de sombra.");
            check(shadowMaps <= 2, "Menu excede o orçamento de dois mapas de sombra locais.");
            check(shadowLights.Any(light => light.name == "Failing fluorescent" && light.type == LightType.Spot), "Luminária principal sem sombra Spot.");
            check(shadowLights.Any(light => light.name == "Left classroom interior light" && light.type == LightType.Spot), "Spot da porta esquerda perdeu a sombra.");
            CheckLayout(menu, errors);
            var report = new Report
            {
                passed = errors.Count == 0, errors = errors.ToArray(),
                punctualShadowMaps = shadowMaps,
                shadowLights = shadowLights.Select(light => light.name + " (" + light.type + ")").ToArray(),
                chapters = new[] { "PRÓLOGO — O Telefone" }.Concat(FutureTitles.Select((s, i) => (i + 1) + " — " + s)).ToArray(),
                notes = new[] { "English muda somente o seletor e mostra disponibilidade futura; textos continuam em português.",
                    "Somente o prólogo carrega Chapter1Office. Ligações e progressão narrativa continuam futuras.",
                    "Transição da câmera, porta esquerda, sincronização do risco/olhos e preferências anteriores preservadas." }
            };
            ValidateLampLighting(scene, errors, report);
            report.passed = errors.Count == 0;
            report.errors = errors.ToArray();
            return report;
        }

        static void ValidateLampLighting(Scene scene, List<string> errors, Report report)
        {
            Action<bool, string> check = (condition, message) => { if (!condition) errors.Add(message); };
            var atmosphere = InScene<MenuAtmosphere>(scene);
            check(atmosphere && atmosphere.fluorescent && atmosphere.tube && atmosphere.menuCamera, "Referências de flicker/tubo/câmera da luminária incompletas.");
            if (!atmosphere || !atmosphere.fluorescent || !atmosphere.tube || !atmosphere.menuCamera) return;
            var light = atmosphere.fluorescent;
            var tube = atmosphere.tube;
            report.lampIntensity = light.intensity;
            report.lampPosition = light.transform.position;
            float baseline = new SerializedObject(atmosphere).FindProperty("baseIntensity").floatValue;
            check(Mathf.Approximately(baseline, LampIntensity) && Mathf.Approximately(light.intensity, baseline), "MenuAtmosphere sobrescreverá a intensidade da luminária com outro valor.");
            var delta = light.transform.position - tube.bounds.center;
            check(Mathf.Abs(delta.x) < .02f && Mathf.Abs(delta.z) < .02f && light.transform.position.y < tube.bounds.min.y - .05f,
                "Spot desalinhado ou bloqueado pelo tubo/carcaça da luminária.");
            check(light.type == LightType.Spot && light.transform.forward.y < -.9f, "Spot da luminária não aponta para o piso.");
            var material = tube.sharedMaterial;
            report.lampEmissionEnabled = material && material.HasProperty("_EmissionColor") && material.IsKeywordEnabled("_EMISSION")
                && material.GetColor("_EmissionColor").maxColorComponent > 1;
            check(report.lampEmissionEnabled, "Tubo sem emissão HDR habilitada.");
            var analog = InScene<AnalogSignal>(scene);
            var profile = analog && analog.filmVolume ? analog.filmVolume.sharedProfile : null;
            Bloom bloom = null;
            bool hasBloom = profile && profile.TryGet(out bloom) && bloom.active;
            if (hasBloom) report.lampBloomIntensity = bloom.intensity.value;
            check(hasBloom && report.lampBloomIntensity > 0 && report.lampBloomIntensity <= .25f, "Bloom da luminária ausente ou exagerado.");
            check(atmosphere.menuCamera.allowHDR, "Câmera do menu desativou HDR da luminária.");
            var floorTransform = Named(scene, "Floor");
            var floor = floorTransform ? floorTransform.GetComponent<Renderer>() : null;
            check(floor, "Piso existente do corredor não encontrado.");
            if (!floor || light.transform.forward.y >= -.01f) return;
            var ray = new Ray(light.transform.position, light.transform.forward);
            float distance = (floor.bounds.max.y - ray.origin.y) / ray.direction.y;
            if (distance <= 0) { check(false, "Spot não alcança o piso abaixo da luminária."); return; }
            var hit = ray.GetPoint(distance);
            report.lampFloorPoint = hit;
            check(hit.x >= floor.bounds.min.x && hit.x <= floor.bounds.max.x && hit.z >= floor.bounds.min.z && hit.z <= floor.bounds.max.z,
                "Centro da iluminação cai fora do piso do corredor.");
            check(light.range > distance + 1, "Alcance da luminária insuficiente para iluminar o piso.");
            // Menu blockout intentionally has no colliders. Bounds test covers its primitive geometry.
            var blockers = new List<string>();
            foreach (var renderer in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true)))
            {
                if (renderer == floor || renderer == tube || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (renderer.bounds.IntersectRay(ray, out float entry) && entry >= 0 && entry < distance - .02f)
                    blockers.Add(renderer.gameObject.name);
            }
            report.lampOccluders = blockers.Distinct().ToArray();
            report.lampFloorPathClear = blockers.Count == 0;
            check(report.lampFloorPathClear, "Geometria bloqueia o centro da luz da luminária: " + string.Join(", ", blockers));
        }

        static void CheckLayout(MainMenuController menu, List<string> errors)
        {
            var chapter = menu.chapterSelection;
            var rects = new List<RectTransform>
            {
                menu.title.rectTransform, (RectTransform)menu.mainPanel.transform,
                (RectTransform)menu.settingsPanel.transform, menu.settingsHeading.rectTransform,
                (RectTransform)chapter.chapterGroup.transform.Find("Story title"),
                (RectTransform)chapter.chapterGroup.transform.Find("Story introduction"),
                (RectTransform)chapter.chapterGroup.transform.Find("Expected play time"),
                (RectTransform)chapter.chapterButton.transform, (RectTransform)chapter.returnButton.transform
            };
            for (int i = 1; i <= 6; i++)
            {
                var row = chapter.chapterGroup.transform.Find("Future chapter " + i);
                if (row) rects.Add((RectTransform)row);
            }
            // CanvasScaler's logarithmic .5 match: both 4:3 and ultrawide must retain visible controls.
            foreach (var pixels in new[] { new Vector2(1600, 900), new Vector2(1280, 720), new Vector2(1280, 960), new Vector2(2100, 900) })
            {
                float scale = Mathf.Sqrt((pixels.x / 1600f) * (pixels.y / 900f));
                var size = pixels / scale;
                foreach (var rect in rects)
                {
                    if (!rect) continue;
                    Vector2 lower = Vector2.Scale(rect.anchorMin, size) + rect.anchoredPosition - Vector2.Scale(rect.pivot, rect.sizeDelta);
                    Vector2 upper = lower + rect.sizeDelta;
                    if (rect.sizeDelta.x <= 0 || rect.sizeDelta.y <= 0 || lower.x < -1 || lower.y < -1 || upper.x > size.x + 1 || upper.y > size.y + 1)
                        errors.Add(rect.name + " fora da tela em " + pixels.x + "x" + pixels.y + ".");
                }
            }
            for (int i = 1; i <= 6; i++)
            {
                var row = chapter.chapterGroup.transform.Find("Future chapter " + i);
                if (!row) continue;
                var label = row.Find("Name").GetComponent<TMP_Text>();
                if (label.GetPreferredValues(label.text).x > label.rectTransform.rect.width + 2)
                    errors.Add("Título do capítulo excede o cartão: " + label.text);
            }
        }

        public static Report ValidateAndCapture()
        {
            using (var lease = new SceneLease())
            {
                var menu = InScene<MainMenuController>(lease.scene);
                var report = Validate(lease.scene, menu);
                if (report.passed)
                {
                    var paths = new List<string>();
                    try
                    {
                        CaptureScreens(lease.scene, menu, paths);
                        report.captures = paths.ToArray();
                    }
                    catch (Exception exception)
                    {
                        report.passed = false;
                        report.errors = report.errors.Concat(new[] { "Captura: " + exception.Message }).ToArray();
                    }
                }
                SaveReport(report);
                Debug.Log("Whispers of Past: validação do menu " + (report.passed ? "aprovada" : "falhou") + ". " + ReportPath);
                return report;
            }
        }

        static void CaptureScreens(Scene scene, MainMenuController menu, List<string> paths, List<PointerHit> pointerHits = null)
        {
            var chapter = menu.chapterSelection;
            var camera = chapter.atmosphere.menuCamera;
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            var active = menu.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.gameObject, t => t.gameObject.activeSelf);
            var groups = menu.GetComponentsInChildren<CanvasGroup>(true).ToDictionary(g => g, g => new Vector3(g.alpha, g.interactable ? 1 : 0, g.blocksRaycasts ? 1 : 0));
            var titleAlpha = menu.title.alpha;
            var textValues = menu.GetComponentsInChildren<TMP_Text>(true).ToDictionary(t => t, t => t.text);
            var sliderValues = menu.GetComponentsInChildren<UnityEngine.UI.Slider>(true).ToDictionary(s => s, s => s.value);
            var eyes = InScene<AnalogSignal>(scene).distantEyes;
            bool eyesActive = eyes.activeSelf;
            try
            {
                eyes.SetActive(false);
                if (menu.fader && menu.fader.curtain) { menu.fader.curtain.alpha = 0; menu.fader.curtain.blocksRaycasts = false; }
                chapter.menuGroup.gameObject.SetActive(true); chapter.menuGroup.alpha = 1;
                if (pointerHits != null)
                {
                    chapter.menuGroup.interactable = chapter.menuGroup.blocksRaycasts = true;
                    menu.continueButton.gameObject.SetActive(true); // Verify its hit area even when no save makes it visible.
                }
                chapter.chapterGroup.gameObject.SetActive(false);
                menu.mainPanel.SetActive(true); menu.settingsPanel.SetActive(false);
                menu.title.gameObject.SetActive(true); menu.title.alpha = 1; menu.settingsHeading.gameObject.SetActive(false);
                var mainButtons = new[] { menu.continueButton, menu.newGameButton, menu.settingsButton, menu.quitButton };
                Capture(scene, camera, "Menu-refined-main.png", 1600, 900, paths, mainButtons, pointerHits);
                Capture(scene, camera, "Menu-refined-main-4x3.png", 1280, 960, paths, mainButtons, pointerHits);
                menu.mainPanel.SetActive(false); menu.settingsPanel.SetActive(true);
                menu.title.gameObject.SetActive(false); menu.settingsHeading.gameObject.SetActive(true);
                PrepareSettingsPreview(menu.settingsManager);
                menu.settingsManager.RefreshLanguagePreview();
                Capture(scene, camera, "Menu-refined-settings.png", 1600, 900, paths, new[] { menu.backButton }, pointerHits);
                Capture(scene, camera, "Menu-refined-settings-4x3.png", 1280, 960, paths, new[] { menu.backButton }, pointerHits);
                chapter.menuGroup.alpha = 0; chapter.chapterGroup.gameObject.SetActive(true); chapter.chapterGroup.alpha = 1;
                // Same world-space offset used by ChapterSelection.Travel; no runtime animation is replaced.
                float forward = new SerializedObject(chapter).FindProperty("forwardDistance").floatValue;
                camera.transform.position = position + Vector3.forward * forward;
                Capture(scene, camera, "Menu-refined-chapters.png", 1600, 900, paths);
                Capture(scene, camera, "Menu-refined-chapters-4x3.png", 1280, 960, paths);
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                foreach (var pair in active) if (pair.Key) pair.Key.SetActive(pair.Value);
                foreach (var pair in groups) if (pair.Key) { pair.Key.alpha = pair.Value.x; pair.Key.interactable = pair.Value.y > 0; pair.Key.blocksRaycasts = pair.Value.z > 0; }
                menu.title.alpha = titleAlpha;
                foreach (var pair in textValues) if (pair.Key) pair.Key.text = pair.Value;
                foreach (var pair in sliderValues) if (pair.Key) pair.Key.SetValueWithoutNotify(pair.Value);
                eyes.SetActive(eyesActive);
                Canvas.ForceUpdateCanvases();
            }
        }
        static void PrepareSettingsPreview(SettingsManager settings)
        {
            // Read preferences without invoking Start(), audio/display changes or saving preferences.
            settings.master.SetValueWithoutNotify(PlayerPrefs.GetFloat(SettingsManager.Prefix + "Master", .8f));
            settings.music.SetValueWithoutNotify(PlayerPrefs.GetFloat(SettingsManager.Prefix + "Music", 0));
            settings.sfx.SetValueWithoutNotify(PlayerPrefs.GetFloat(SettingsManager.Prefix + "SFX", .8f));
            settings.sensitivity.SetValueWithoutNotify(SettingsManager.MouseSensitivity);
            settings.masterValue.text = Mathf.RoundToInt(settings.master.value * 100).ToString();
            settings.musicValue.text = Mathf.RoundToInt(settings.music.value * 100).ToString();
            settings.sfxValue.text = Mathf.RoundToInt(settings.sfx.value * 100).ToString();
            settings.sensitivityValue.text = settings.sensitivity.value.ToString("0.00");
            settings.resolutionValue.text = PlayerPrefs.GetInt(SettingsManager.Prefix + "Width", Screen.width) + " x " + PlayerPrefs.GetInt(SettingsManager.Prefix + "Height", Screen.height);
            settings.fullscreenValue.text = PlayerPrefs.GetInt(SettingsManager.Prefix + "Fullscreen", Screen.fullScreen ? 1 : 0) == 1 ? "ON" : "OFF";
            settings.qualityValue.text = QualitySettings.names[QualitySettings.GetQualityLevel()].ToUpperInvariant();
            settings.invertValue.text = SettingsManager.InvertY ? "ON" : "OFF";
        }
        static void Capture(Scene scene, Camera camera, string file, int width, int height, List<string> paths,
            UnityEngine.UI.Button[] buttons = null, List<PointerHit> pointerHits = null)
        {
            string path = Documentation + "/" + file;
            HorrorGame.Editor.PresentationCapture.Capture(scene, camera, path, width, height, preview => {
                if (pointerHits == null || buttons == null) return;
                var events = InScene<EventSystem>(scene);
                Require(events, "EventSystem ausente durante prova do mouse.");
                foreach (var button in buttons)
                {
                    var graphic = button.targetGraphic;
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(preview, graphic.rectTransform.TransformPoint(graphic.rectTransform.rect.center));
                    var results = new List<RaycastResult>();
                    var raycaster = InScene<MainMenuController>(scene).GetComponent<UnityEngine.UI.GraphicRaycaster>();
                    Require(raycaster && raycaster.isActiveAndEnabled, "GraphicRaycaster desativado.");
                    // BaseRaycaster registers with EventSystem on Play-mode OnEnable, which does not run in this Editor proof.
                    raycaster.Raycast(new PointerEventData(events) { position = point }, results);
                    var first = results.Count > 0 ? results[0].gameObject : null;
                    var handler = first ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(first) : null;
                    pointerHits.Add(new PointerHit { button = button.name, resolution = width + "x" + height,
                        firstHit = first ? first.name : "none", received = handler == button.gameObject && button.IsInteractable(),
                        graphicDepth = graphic.depth, graphicActive = graphic.isActiveAndEnabled, graphicCulled = graphic.canvasRenderer.cull,
                        registeredRaycasters = UnityEngine.EventSystems.RaycasterManager.GetRaycasters().Count, screenPoint = point });
                }
            });
            paths.Add(path);
        }
        static void SaveReport(Report report)
        {
            Directory.CreateDirectory(Documentation);
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        }
    }
}
