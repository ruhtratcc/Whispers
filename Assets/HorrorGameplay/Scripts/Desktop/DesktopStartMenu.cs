// Abre os aplicativos pelo menu Iniciar.

using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.Desktop
{
    public sealed class DesktopStartMenu
    {
        public RectTransform Root { get; }

        readonly RetroComputer host;
        readonly RectTransform recent, programs;
        readonly List<string> history = new List<string>();
        public DesktopStartMenu(RetroComputer owner, Transform parent)
        {
            host = owner;
            Root = DesktopUI.Panel(parent, "Menu Iniciar", 4, 208, 460, 518, host.content.frameSprite);
            var header = DesktopUI.Panel(Root, "Usuário", 3, 3, 454, 58, host.content.titleSprite);
            DesktopUI.Image(header, host.content.FindApp("computer").icon, 12, 11, 44, 44);
            DesktopUI.Label(header, DesktopSystemText.UserName, 72, 17, 350, 33, 20).color = Color.white;
            DesktopUI.Panel(Root, "Programas fixos", 4, 65, 232, 392, host.content.windowSprite);
            var right = DesktopUI.Panel(Root, "Pastas", 238, 65, 218, 392, host.content.windowSprite);
            right.GetComponent<UnityEngine.UI.Image>().color = new Color(.82f, .9f, 1);
            string[] pinned =
            {
                "browser",
                "mail",
                "media",
                "notepad"
            };
            for (int i = 0; i < pinned.Length; i++)
                Entry(Root, pinned[i], 8, 70 + i * 35, 224);
            DesktopUI.Label(Root, DesktopSystemText.Recent, 16, 214, 210, 25, 13);
            recent = DesktopUI.Rect(Root, "Recentes", 8, 242, 224, 145);
            string[] labels =
            {
                DesktopSystemText.Documents,
                DesktopSystemText.Pictures,
                DesktopSystemText.Music,
                DesktopSystemText.Computer,
                DesktopSystemText.ControlPanel,
                DesktopSystemText.Search,
                DesktopSystemText.Run
            };
            string[] ids =
            {
                "documents",
                "pictures",
                "music",
                "computer",
                "control",
                "search",
                "run"
            };
            for (int i = 0; i < labels.Length; i++)
            {
                string id = ids[i];
                MenuButton(Root, labels[i], 244, 75 + i * 44, 206, 37, () =>
                {
                    if (id == "documents" || id == "computer")
                        host.OpenApp(id);
                    else
                        host.OpenSystemTool(id);
                    Root.gameObject.SetActive(false);
                });
            }

            programs = DesktopUI.Panel(Root, "Todos os Programas", 458, 65, 254, host.content.apps.Length * 34 + 10, host.content.windowSprite);
            for (int i = 0; i < host.content.apps.Length; i++)
                Entry(programs, host.content.apps[i].id, 5, 5 + i * 34, 244);
            programs.gameObject.SetActive(false);
            MenuButton(Root, DesktopSystemText.AllPrograms + "  >", 8, 410, 224, 35, () => programs.gameObject.SetActive(!programs.gameObject.activeSelf));
            DesktopUI.Panel(Root, "Rodapé", 3, 460, 454, 55, host.content.titleSprite);
            MenuButton(Root, DesktopSystemText.SignOut, 12, 471, 192, 32, host.Close);
            MenuButton(Root, DesktopSystemText.Shutdown, 216, 471, 232, 32, host.RequestShutdownDialog);
            Root.gameObject.SetActive(false);
        }

        void MenuButton(Transform parent, string text, float x, float y, float width, float height, UnityEngine.Events.UnityAction action)
        {
            var b = DesktopUI.Button(parent, text, x, y, width, height, () => action());
            var c = b.colors;
            c.highlightedColor = new Color32(0x31, 0x6A, 0xC5, 255);
            b.colors = c;
        }

        void Entry(Transform parent, string id, float x, float y, float width)
        {
            var app = host.content.FindApp(id);
            if (app == null)
                return;
            var b = DesktopUI.Button(parent, app.title, x, y, width, 32, () =>
            {
                Root.gameObject.SetActive(false);
                host.OpenApp(id);
            });
            DesktopUI.Image(b.transform, app.icon, 4, 5, 22, 22);
            var label = b.GetComponentInChildren<TMPro.TMP_Text>();
            label.rectTransform.anchoredPosition = new Vector2(34, -3);
            label.rectTransform.sizeDelta = new Vector2(width - 39, 26);
            label.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            var c = b.colors;
            c.highlightedColor = new Color32(0x31, 0x6A, 0xC5, 255);
            b.colors = c;
        }

        public void Record(string id)
        {
            history.Remove(id);
            history.Insert(0, id);
            if (history.Count > 4)
                history.RemoveAt(4);
            DesktopUI.Clear(recent);
            for (int i = 0; i < history.Count; i++)
                Entry(recent, history[i], 0, i * 34, 224);
        }
    }
}
