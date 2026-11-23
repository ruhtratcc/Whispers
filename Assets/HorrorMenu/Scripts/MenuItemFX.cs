// Reage ao mouse e à seleção para animar as opções.

using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace SchoolMenu
{
    public sealed class MenuItemFX : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        [SerializeField]
        public TMP_Text label;
        [SerializeField]
        public UnityEngine.UI.Image focusLine, focusMarker;
        [SerializeField]
        Color normal = new Color(.77f, .76f, .68f), selected = new Color(.55f, .23f, .20f);
        [SerializeField]
        float shift = 4f, response = 12f, dim = .66f;
        Vector2 home;
        bool initialized;
        float flickerUntil, focus;
        UnityEngine.UI.Selectable selectable;
        Color lineColor, markerColor;
        Vector3 lineScale;
        void Start()
        {
            Init();
        }

        void Init()
        {
            if (initialized)
                return;
            selectable = GetComponent<UnityEngine.UI.Selectable>();
            if (label)
                home = label.rectTransform.anchoredPosition;
            if (focusLine)
            {
                lineColor = focusLine.color;
                lineScale = focusLine.rectTransform.localScale;
                focusLine.raycastTarget = false;
            }

            if (focusMarker)
            {
                markerColor = focusMarker.color;
                focusMarker.raycastTarget = false;
            }

            initialized = true;
            ApplyFocus();
        }

        bool CanFocus => !selectable || (selectable.IsActive() && selectable.IsInteractable());

        public void OnPointerEnter(PointerEventData data)
        {
            Init();
            if (EventSystem.current && selectable && CanFocus)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnSelect(BaseEventData data)
        {
            Init();
            if (!CanFocus)
                return;
            flickerUntil = Time.unscaledTime + .09f;
            var menu = GetComponentInParent<MainMenuController>();
            if (menu && menu.audioManager)
                menu.audioManager.Hover();
        }

        void Update()
        {
            Init();
            var current = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            bool active = current == gameObject && CanFocus;
            focus = Mathf.Lerp(focus, active ? 1 : 0, Time.unscaledDeltaTime * response);
            ApplyFocus();
            if (!label)
                return;
            var color = active ? selected : normal;
            if (!CanFocus || (!active && current && current.transform.parent == transform.parent))
                color *= new Color(dim, dim, dim, 1);
            if (active && Time.unscaledTime < flickerUntil)
                color.a = .72f;
            label.color = color;
            label.rectTransform.anchoredPosition = Vector2.Lerp(label.rectTransform.anchoredPosition, home + Vector2.right * (active ? shift : 0), Time.unscaledDeltaTime * response);
        }

        void ApplyFocus()
        {
            if (focusLine)
            {
                var color = lineColor;
                color.a *= focus;
                focusLine.color = color;
                focusLine.rectTransform.localScale = new Vector3(lineScale.x * focus, lineScale.y, lineScale.z);
            }

            if (focusMarker)
            {
                var color = markerColor;
                color.a *= focus;
                focusMarker.color = color;
            }
        }

        void OnDisable()
        {
            if (!initialized)
                return;
            focus = 0;
            ApplyFocus();
            if (label)
            {
                label.color = normal;
                label.rectTransform.anchoredPosition = home;
            }
        }
    }
}
