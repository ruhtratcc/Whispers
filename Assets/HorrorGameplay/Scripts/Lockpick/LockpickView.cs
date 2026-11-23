// Desenha a interface da fechadura e seus controles.

using TMPro;
using UnityEngine;

namespace HorrorGame
{
    public sealed class LockpickView : MonoBehaviour
    {
        public LockpickSystem system;
        public TMP_FontAsset font;
        public RectTransform panel, needle, cylinder, hint;
        public TMP_Text title, status, tutorial;
        public UnityEngine.UI.Image proximity;
        public UnityEngine.UI.Image[] pins;
        public UnityEngine.UI.Button cancelButton, pauseButton;
        bool wired;
        public void Bind(LockpickSystem value)
        {
            system = value;
            if (!wired)
            {
                cancelButton.onClick.RemoveAllListeners();
                pauseButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(() => system.Cancel());
                pauseButton.onClick.AddListener(() => system.game.Pause());
                wired = true;
            }

            cancelButton.GetComponentInChildren<TMP_Text>().text = "Cancelar";
            pauseButton.GetComponentInChildren<TMP_Text>().text = "Pausar";
            tutorial.text = "Mova o mouse e segure o clique.";
            cylinder.GetComponent<UnityEngine.UI.Image>().sprite = system.config.cylinderSprite;
            title.text = value.CurrentDoor.displayName.ToUpperInvariant();
            Present(false);
        }

        public void Build()
        {
            var back = gameObject.AddComponent<UnityEngine.UI.Image>();
            back.color = new Color(.006f, .01f, .015f, .86f);
            panel = Rect("Fechadura", transform, new Vector2(700, 530), Vector2.zero);
            panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.035f, .045f, .05f, .97f);
            title = Text("Título", panel, "FECHADURA", new Vector2(0, 210), 25);
            status = Text("Estado", panel, "", new Vector2(0, -130), 20);
            tutorial = Text("Dica", panel, "Mova o mouse e segure o clique.", new Vector2(0, -174), 18);
            cylinder = Rect("Cilindro", panel, new Vector2(190, 190), new Vector2(0, 18));
            var face = cylinder.gameObject.AddComponent<UnityEngine.UI.Image>();
            face.sprite = system.config.cylinderSprite;
            face.color = new Color(.44f, .5f, .52f);
            face.raycastTarget = false;
            needle = Rect("Grampo", panel, new Vector2(4, 174), new Vector2(0, 18));
            needle.pivot = new Vector2(.5f, 0);
            needle.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.83f, .88f, .9f);
            hint = Rect("Ponto indicado", panel, new Vector2(7, 7), Vector2.zero);
            hint.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.52f, .7f, .76f, .3f);
            pins = new UnityEngine.UI.Image[3];
            for (int i = 0; i < 3; i++)
                pins[i] = Rect("Pino " + (i + 1), panel, new Vector2(24, 4), new Vector2((i - 1) * 38, 162)).gameObject.AddComponent<UnityEngine.UI.Image>();
            var track = Rect("Ajuda", panel, new Vector2(210, 2), new Vector2(0, -101));
            track.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.12f, .15f, .17f);
            proximity = Rect("Proximidade", track, new Vector2(210, 2), Vector2.zero).gameObject.AddComponent<UnityEngine.UI.Image>();
            proximity.color = new Color(.52f, .67f, .73f);
            proximity.type = UnityEngine.UI.Image.Type.Filled;
            proximity.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            cancelButton = Button("Cancelar", panel, new Vector2(-140, -225));
            pauseButton = Button("Pausar", panel, new Vector2(140, -225));
        }

        public void Present(bool showTutorial)
        {
            if (!system || !system.IsOpen)
                return;
            var a = system.Attempt;
            var d = system.CurrentDoor;
            needle.localEulerAngles = new Vector3(0, 0, a.Angle - 90);
            cylinder.localEulerAngles = new Vector3(0, 0, -a.Progress * 75);
            bool assisted = LockpickPreferences.Assisted;
            bool show = assisted || system.HintRemaining > 0;
            proximity.transform.parent.gameObject.SetActive(assisted);
            proximity.fillAmount = a.Proximity;
            hint.gameObject.SetActive(show);
            if (show)
            {
                float angle = a.TargetAngle * Mathf.Deg2Rad;
                hint.anchoredPosition = new Vector2(Mathf.Round(Mathf.Cos(angle) * 116), 18 + Mathf.Round(Mathf.Sin(angle) * 116));
                hint.GetComponent<UnityEngine.UI.Image>().color = new Color(.52f, .7f, .76f, .18f + .15f * Mathf.Sin(Time.unscaledTime * 4));
            }

            for (int i = 0; i < pins.Length; i++)
            {
                pins[i].gameObject.SetActive(i < a.PinCount);
                pins[i].color = i < a.CompletedPins ? new Color(.71f, .8f, .82f) : new Color(.22f, .27f, .29f);
            }

            string text = system.CountLabel;
            if (status.text != text)
                status.text = text;
            tutorial.gameObject.SetActive(showTutorial);
            float jitter = !PresentationSettings.ReduceEffects ? a.Stress * system.config.vibrationPixels : 0;
            panel.anchoredPosition = new Vector2(Mathf.Round(Mathf.Sin(Time.unscaledTime * 31) * jitter), Mathf.Round(Mathf.Cos(Time.unscaledTime * 27) * jitter));
            var canvas = (RectTransform)GetComponentInParent<Canvas>().transform;
            panel.localScale = Vector3.one * Mathf.Min(1, (canvas.rect.width - 40) / 700, (canvas.rect.height - 40) / 530);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = size;
            r.anchoredPosition = position;
            return r;
        }

        TMP_Text Text(string name, Transform parent, string text, Vector2 position, int size)
        {
            var r = Rect(name, parent, new Vector2(620, 40), position);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.enableAutoSizing = false;
            t.color = new Color(.84f, .9f, .92f);
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        UnityEngine.UI.Button Button(string label, Transform parent, Vector2 position)
        {
            var r = Rect(label, parent, new Vector2(230, 44), position);
            var image = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(.08f, .105f, .12f);
            var b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = image;
            Text("Texto", r, label, Vector2.zero, 19);
            return b;
        }
    }
}
