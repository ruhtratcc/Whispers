// Mostra a sequência de ligar o computador.

using TMPro;
using UnityEngine;

namespace HorrorGame.Desktop
{
    public enum DesktopBootStage
    {
        Black,
        Bios,
        Splash,
        Welcome,
        Hidden
    }

    public sealed class DesktopBootView
    {
        readonly UnityEngine.UI.Image background;
        readonly RectTransform bios, splash, welcome;
        TMP_Text diagnostics, password;
        RectTransform activity;
        public RectTransform Root { get; }
        public DesktopBootStage Stage { get; private set; }

        public DesktopBootView(Transform parent, DesktopContent data)
        {
            Root = DesktopUI.Rect(parent, "Inicialização do computador", 0, 0, 1024, 768);
            background = Root.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.raycastTarget = false;
            var group = Root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            bios = DesktopUI.Rect(Root, "BIOS", 0, 0, 1024, 768);
            var heading = Text(bios, DesktopSystemText.Manufacturer, 42, 47, 940, 40, 24, Color.white);
            if (data.retroFont)
                heading.font = data.retroFont;
            diagnostics = Text(bios, "", 42, 124, 940, 505, 22, new Color(.86f, .89f, .88f));
            if (data.retroFont)
                diagnostics.font = data.retroFont;
            splash = DesktopUI.Rect(Root, "Sistema Limiar", 0, 0, 1024, 768);
            DoorMark(splash, 472, 223, Color.white, new Color(.24f, .61f, 1));
            var logo = Text(splash, DesktopSystemText.SystemName, 212, 335, 600, 84, 46, Color.white);
            logo.alignment = TextAlignmentOptions.Center;
            logo.fontStyle = FontStyles.Bold;
            var system = Text(splash, DesktopSystemText.Manufacturer, 212, 418, 600, 28, 14, new Color(.69f, .82f, 1));
            system.alignment = TextAlignmentOptions.Center;
            Strip(splash, "Trilho de inicialização", 424, 489, 176, 5, new Color(.08f, .2f, .43f));
            Strip(splash, "Atividade da unidade", 482, 489, 60, 5, new Color(.35f, .73f, 1));
            welcome = DesktopUI.Rect(Root, "Entrada do usuário", 0, 0, 1024, 768);
            DoorMark(welcome, 472, 186, Color.white, new Color(.46f, .79f, 1));
            var greeting = Text(welcome, "Bem-vindo", 112, 311, 800, 63, 34, Color.white);
            greeting.alignment = TextAlignmentOptions.Center;
            var user = Text(welcome, DesktopSystemText.UserName, 112, 390, 800, 44, 24, Color.white);
            user.alignment = TextAlignmentOptions.Center;
            user.fontStyle = FontStyles.Bold;
            var loading = Text(welcome, DesktopSystemText.Loading, 112, 455, 800, 30, 14, new Color(.82f, .9f, 1));
            loading.alignment = TextAlignmentOptions.Center;
            password = Text(welcome, DesktopSystemText.Password, 112, 510, 800, 30, 16, Color.white);
            password.alignment = TextAlignmentOptions.Center;
            activity = splash.Find("Atividade da unidade") as RectTransform;
            UpdateIdentity(1, 0);
            SetStage(DesktopBootStage.Black);
        }

        static TMP_Text Text(Transform parent, string value, float x, float y, float width, float height, float size, Color color)
        {
            var text = DesktopUI.Label(parent, value, x, y, width, height, size);
            text.color = color;
            return text;
        }

        static void Strip(Transform parent, string name, int x, int y, int width, int height, Color color)
        {
            var rect = DesktopUI.Rect(parent, name, x, y, width, height);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.color = color;
        }

        static void DoorMark(Transform parent, int x, int y, Color frame, Color threshold)
        {
            // A doorway is the original system mark; no borrowed operating-system logo.
            Strip(parent, "Portal esquerdo", x, y, 8, 78, frame);
            Strip(parent, "Portal superior", x, y, 80, 8, frame);
            Strip(parent, "Portal direito", x + 72, y, 8, 78, frame);
            Strip(parent, "Limiar", x - 8, y + 84, 96, 7, threshold);
        }

        public void SetStage(DesktopBootStage stage)
        {
            Stage = stage;
            Root.gameObject.SetActive(stage != DesktopBootStage.Hidden);
            if (stage == DesktopBootStage.Hidden)
                return;
            bios.gameObject.SetActive(stage == DesktopBootStage.Bios);
            splash.gameObject.SetActive(stage == DesktopBootStage.Splash);
            welcome.gameObject.SetActive(stage == DesktopBootStage.Welcome);
            background.color = stage == DesktopBootStage.Welcome ? new Color(.08f, .35f, .79f) : stage == DesktopBootStage.Splash ? new Color(.025f, .075f, .18f) : Color.black;
            Root.SetAsLastSibling();
        }

        public void UpdateIdentity(int visits, float progress)
        {
            var clock = GameClock.Instance;
            diagnostics.text = DesktopSystemText.Manufacturer + " / " + DesktopSystemText.BootVersion + "\n\n" + DesktopSystemText.Memory + "\n\n" + DesktopSystemText.Disk + "\n\n" + (visits >= 2 ? DesktopSystemText.Extra : DesktopSystemText.NormalInput) + "\n\n" + (clock ? clock.DateText + "  " + clock.TimeText : "") + "\n\n" + DesktopSystemText.BootStart;
            if (activity)
            {
                activity.anchoredPosition = new Vector2(424, -489);
                activity.sizeDelta = new Vector2(Mathf.Round(176 * Mathf.Clamp01(progress)), 5);
            }
        }

        public void Hide() => SetStage(DesktopBootStage.Hidden);
        public void Dispose()
        {
            if (Root)
                Object.Destroy(Root.gameObject);
        }
    }
}
