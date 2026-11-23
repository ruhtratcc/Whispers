// Recebe comandos de teste de capítulos, como o atalho da estrada.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HorrorGame
{
    public sealed class ChapterCommandTerminal : MonoBehaviour
    {
        public GameSession game;
        GameObject panel;
        TMP_InputField input;
        TMP_Text status;
        bool ownsInspection;
        public bool IsOpen => panel && panel.activeSelf;

        void Awake()
        {
            if (!game)
                game = GetComponent<GameSession>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.f8Key.wasPressedThisFrame)
            {
                if (IsOpen)
                    Close();
                else
                    Open();
            }
            else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame && game.State != GameState.Paused)
                Close();
        }

        public bool Open()
        {
            if (!game || !game.BeginInspection())
                return false;
            ownsInspection = true;
            if (!panel)
                Build();
            panel.SetActive(true);
            input.text = "";
            input.ActivateInputField();
            return true;
        }

        void Build()
        {
            panel = new GameObject("Terminal de capítulos", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            panel.transform.SetParent(transform, false);
            var canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scale = panel.GetComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1280, 720);
            var bg = HorrorGame.Desktop.DesktopUI.Rect(panel.transform, "Terminal", 0, 0, 1280, 180);
            bg.gameObject.AddComponent<Image>().color = new Color(.015f, .02f, .025f, .97f);
            Label(bg, "F8 — terminal   |   skip: pular capítulo do carro", 18, 12, 1240, 30);
            status = Label(bg, "Digite um comando e pressione Enter.", 18, 54, 1240, 30);
            var field = HorrorGame.Desktop.DesktopUI.Rect(bg, "Comando", 18, 100, 1240, 44);
            field.gameObject.AddComponent<Image>().color = new Color(.06f, .08f, .08f);
            var text = Label(field, "", 10, 6, 1220, 32);
            input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = field;
            input.textComponent = text;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.onSubmit.AddListener(Submit);
        }

        TMP_Text Label(Transform parent, string value, float x, float y, float w, float h)
        {
            var t = HorrorGame.Desktop.DesktopUI.Label(parent, value, x, y, w, h, 19);
            t.color = new Color(.78f, .9f, .79f);
            if (game.hud && game.hud.objective)
                t.font = game.hud.objective.font;
            return t;
        }

        void Submit(string command)
        {
            if (game.State == GameState.Paused)
                return;
            if (!string.Equals(command.Trim(), "skip", System.StringComparison.OrdinalIgnoreCase))
            {
                status.text = "Comando desconhecido. Use: skip";
                input.ActivateInputField();
                return;
            }

            var road = game.GetComponent<BeforeDemolitionRoadFlow>();
            if (!road || !road.IsActive)
            {
                status.text = "skip só está disponível durante o capítulo do carro.";
                input.ActivateInputField();
                return;
            }

            Close();
            StartCoroutine(Skip(road));
        }

        IEnumerator Skip(BeforeDemolitionRoadFlow road)
        {
            yield return null;
            yield return null;
            if (!road.SkipChapter())
                game.hud?.Notify("Aguarde a cena terminar antes de pular.");
        }

        public void Close()
        {
            if (panel)
                panel.SetActive(false);
            if (ownsInspection && game)
                game.EndInspection();
            ownsInspection = false;
        }

        void OnDisable() => Close();
    }
}
