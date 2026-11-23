// Controla preferências sobre a apresentação do objetivo.

using TMPro;
using UnityEngine;

namespace HorrorGame
{
    public sealed class ObjectiveOptions : MonoBehaviour
    {
        Transform owner;
        TMP_FontAsset font;
        RectTransform frame;
        TMP_Text show, size;
        static RectTransform Rect(Transform p, string name, Vector2 dim, Vector2 pos)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(p, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = dim;
            r.anchoredPosition = pos;
            return r;
        }

        TMP_Text Label(Transform p, string text, float y, float points = 23)
        {
            var r = Rect(p, "Texto", new Vector2(630, 60), new Vector2(0, y));
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = points;
            t.text = text;
            t.color = new Color(.86f, .92f, .94f);
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        TMP_Text Button(Transform p, string text, float y, UnityEngine.Events.UnityAction click)
        {
            var r = Rect(p, text, new Vector2(660, 58), new Vector2(0, y));
            var image = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(.08f, .1f, .11f);
            var b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = image;
            b.onClick.AddListener(click);
            return Label(r, text, 0);
        }

        public static void Install(GameObject options, TMP_FontAsset font)
        {
            if (!options)
                return;
            var canvas = options.GetComponentInParent<Canvas>();
            if (!canvas)
                return;
            foreach (var panel in canvas.GetComponentsInChildren<ObjectiveOptions>(true))
                if (panel.owner == options.transform)
                    return;
            var root = Rect(canvas.transform, "Opções de objetivos", Vector2.zero, Vector2.zero);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.005f, .008f, .008f, .97f);
            var view = root.gameObject.AddComponent<ObjectiveOptions>();
            view.owner = options.transform;
            view.font = font;
            view.frame = Rect(root, "Painel", new Vector2(720, 450), Vector2.zero);
            view.Label(view.frame, "OBJETIVOS", 180, 30);
            view.show = view.Button(view.frame, "", 95, () =>
            {
                ObjectivePreferences.ShowObjectives = !ObjectivePreferences.ShowObjectives;
                view.Refresh();
            });
            view.size = view.Button(view.frame, "", 15, () =>
            {
                float scale = ObjectivePreferences.TextScale;
                ObjectivePreferences.TextScale = scale < 1.09f ? 1.2f : scale < 1.29f ? 1.4f : 1;
                view.Refresh();
            });
            view.Label(view.frame, "Luzes intermitentes e tremores podem ser suavizados\nem Acessibilidade > Reduzir efeitos.", -70, 20);
            view.Button(view.frame, "Voltar", -170, () => root.gameObject.SetActive(false));
            var entry = view.Button(options.transform, "Objetivos", 0, () =>
            {
                view.Refresh();
                root.SetAsLastSibling();
                root.gameObject.SetActive(true);
            });
            var er = (RectTransform)entry.transform.parent;
            er.anchorMin = er.anchorMax = er.pivot = new Vector2(0, 1);
            er.sizeDelta = new Vector2(250, 48);
            er.anchoredPosition = new Vector2(24, -80);
            root.gameObject.SetActive(false);
        }

        void Refresh()
        {
            show.text = "Mostrar objetivos: " + (ObjectivePreferences.ShowObjectives ? "sim" : "não");
            size.text = "Tamanho do texto: " + Mathf.RoundToInt(ObjectivePreferences.TextScale * 100) + "%";
        }

        public static bool TryClose()
        {
            foreach (var p in Object.FindObjectsByType<ObjectiveOptions>())
                if (p.gameObject.activeInHierarchy)
                {
                    p.gameObject.SetActive(false);
                    return true;
                }

            return false;
        }

        void LateUpdate()
        {
            if (!owner || !owner.gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
                return;
            }

            var c = (RectTransform)GetComponentInParent<Canvas>().transform;
            frame.localScale = Vector3.one * Mathf.Min(1, (c.rect.width - 40) / 720, (c.rect.height - 40) / 450);
        }
    }
}
