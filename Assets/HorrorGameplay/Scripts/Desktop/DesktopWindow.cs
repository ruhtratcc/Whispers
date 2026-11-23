// Gerencia posição, foco e tamanho das janelas.

using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace HorrorGame.Desktop
{
    public sealed class DesktopWindow : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler
    {
        public RetroComputer Host;
        public string AppId;
        public DesktopApp App;
        public RectTransform body;
        public UnityEngine.UI.Button TaskButton;
        public bool Minimized { get; private set; }
        public bool Maximized { get; private set; }
        public bool IsFocused { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        TMP_Text caption;
        UnityEngine.UI.Image titleImage;
        Vector2 restorePosition, restoreSize, dragOriginWindow, dragOriginPointer;
        bool dragTitle;
        public void Initialize(RetroComputer host, DesktopAppDefinition def)
        {
            Host = host;
            AppId = def.id;
            caption = DesktopUI.Label(transform, def.title, 38, 7, def.defaultSize.x - 160, 25, 18);
            caption.color = Color.white;
            if (def.icon)
                DesktopUI.Image(transform, def.icon, 10, 6, 22, 22);
            var title = DesktopUI.Panel(transform, "Barra de título", 3, 3, def.defaultSize.x - 6, 32, host.content.titleSprite);
            title.SetAsFirstSibling();
            titleImage = title.GetComponent<UnityEngine.UI.Image>();
            DesktopUI.Button(transform, "_", def.defaultSize.x - 96, 5, 27, 27, Minimize);
            DesktopUI.Button(transform, "□", def.defaultSize.x - 65, 5, 27, 27, ToggleMaximize);
            DesktopUI.Button(transform, "×", def.defaultSize.x - 34, 5, 27, 27, () => Host.CloseApp(AppId));
            var close = transform.Find("×");
            if (close && host.content.closeSprite)
                close.GetComponent<UnityEngine.UI.Image>().sprite = host.content.closeSprite;
            body = DesktopUI.Rect(transform, "Conteúdo", 5, 39, def.defaultSize.x - 10, def.defaultSize.y - 44);
            body.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            SetFocused(true);
        }

        public void SetTitle(string text)
        {
            if (caption)
                caption.text = text;
            if (TaskButton)
            {
                var t = TaskButton.GetComponentInChildren<TMP_Text>();
                if (t)
                    t.text = text;
            }
        }

        public void SetFocused(bool focused)
        {
            IsFocused = focused && !Minimized;
            if (titleImage)
            {
                titleImage.sprite = IsFocused ? Host.content.titleSprite : Host.content.inactiveTitleSprite ? Host.content.inactiveTitleSprite : Host.content.buttonSprite;
                titleImage.color = Color.white;
            }

            if (caption)
                caption.color = IsFocused ? Color.white : new Color(.18f, .21f, .27f);
        }

        public void Minimize()
        {
            Minimized = true;
            var g = GetComponent<CanvasGroup>();
            if (!g)
                g = gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0;
            g.interactable = g.blocksRaycasts = false;
            Host.FocusWindow(null);
        }

        public void Restore()
        {
            Minimized = false;
            gameObject.SetActive(true);
            var g = GetComponent<CanvasGroup>();
            if (g)
            {
                g.alpha = 1;
                g.interactable = g.blocksRaycasts = true;
            }

            Focus();
        }

        public void Focus() => Host.FocusWindow(this);
        public void ToggleMaximize()
        {
            if (!Maximized)
            {
                restorePosition = Rect.anchoredPosition;
                restoreSize = Rect.sizeDelta;
                Rect.anchoredPosition = new Vector2(4, -4);
                Rect.sizeDelta = new Vector2(1016, 716);
            }
            else
            {
                Rect.anchoredPosition = restorePosition;
                Rect.sizeDelta = restoreSize;
            }

            Maximized = !Maximized;
            ResizeChrome();
            Canvas.ForceUpdateCanvases();
            App?.OnResize();
        }

        void ResizeChrome()
        {
            var title = transform.Find("Barra de título") as RectTransform;
            if (title)
                title.sizeDelta = new Vector2(Rect.sizeDelta.x - 6, 32);
            var controls = GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var b in controls)
            {
                var text = b.GetComponentInChildren<TMP_Text>();
                if (!text)
                    continue;
                float x = text.text == "_" ? 96 : text.text == "□" ? 65 : text.text == "×" ? 34 : -1;
                if (x > 0)
                {
                    var r = (RectTransform)b.transform;
                    r.anchoredPosition = new Vector2(Rect.sizeDelta.x - x, -5);
                }
            }

            caption.rectTransform.sizeDelta = new Vector2(Rect.sizeDelta.x - 160, 25);
            body.sizeDelta = Rect.sizeDelta - new Vector2(10, 44);
        }

        public void OnPointerDown(PointerEventData e) => Focus();
        public void OnBeginDrag(PointerEventData e)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, e.pressPosition, e.pressEventCamera, out local);
            dragTitle = !Maximized && local.y >= -36;
            dragOriginWindow = Rect.anchoredPosition;
            dragOriginPointer = e.pressPosition;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!dragTitle)
                return;
            var p = dragOriginWindow + (e.position - dragOriginPointer);
            p.x = Mathf.Round(Mathf.Clamp(p.x, 0, 1024 - Rect.rect.width));
            p.y = Mathf.Round(Mathf.Clamp(p.y, -718 + 36, 0));
            Rect.anchoredPosition = p;
        }
    }
}
