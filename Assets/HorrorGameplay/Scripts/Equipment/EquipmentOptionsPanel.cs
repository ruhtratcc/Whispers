// Exibe as opções de HUD, lanterna e rolagem da roda do mouse.

using TMPro;
using UnityEngine;

namespace HorrorGame
{
    public sealed class EquipmentOptionsPanel : MonoBehaviour
    {
        Transform owner;
        TMP_Text modeLabel, lightLabel, wheelLabel, assistLabel, autoLabel;
        RectTransform frame;
        TMP_FontAsset font;
        public static void Install(GameObject options, TMP_FontAsset font)
        {
            if (!options || !options.GetComponentInParent<Canvas>())
                return;
            var canvas = options.GetComponentInParent<Canvas>();
            foreach (var existing in canvas.GetComponentsInChildren<EquipmentOptionsPanel>(true))
                if (existing.owner == options.transform)
                    return;
            var root = Rect("Opções de equipamento", canvas.transform, Vector2.zero, Vector2.zero);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.005f, .008f, .008f, .96f);
            var panel = root.gameObject.AddComponent<EquipmentOptionsPanel>();
            panel.owner = options.transform;
            panel.font = font;
            panel.frame = Rect("Painel", root, new Vector2(720, 600), Vector2.zero);
            panel.Label(panel.frame, "EQUIPAMENTOS", new Vector2(0, 250), 30);
            panel.modeLabel = panel.Button(panel.frame, "", 175, () =>
            {
                EquipmentPreferences.Set((EquipmentHudMode)(((int)EquipmentPreferences.Mode + 1) % 3), EquipmentPreferences.KeepLight, EquipmentPreferences.ReverseWheel);
                panel.Refresh();
            });
            panel.lightLabel = panel.Button(panel.frame, "", 95, () =>
            {
                EquipmentPreferences.Set(EquipmentPreferences.Mode, !EquipmentPreferences.KeepLight, EquipmentPreferences.ReverseWheel);
                panel.Refresh();
            });
            panel.wheelLabel = panel.Button(panel.frame, "", 15, () =>
            {
                EquipmentPreferences.Set(EquipmentPreferences.Mode, EquipmentPreferences.KeepLight, !EquipmentPreferences.ReverseWheel);
                panel.Refresh();
            });
            panel.assistLabel = panel.Button(panel.frame, "", -65, () =>
            {
                LockpickPreferences.Set(!LockpickPreferences.Assisted, LockpickPreferences.Automatic);
                panel.Refresh();
            });
            panel.autoLabel = panel.Button(panel.frame, "", -145, () =>
            {
                LockpickPreferences.Set(LockpickPreferences.Assisted, !LockpickPreferences.Automatic);
                panel.Refresh();
            });
            panel.Button(panel.frame, "Voltar", -245, () => root.gameObject.SetActive(false));
            var entry = panel.Button(options.transform, "Equipamentos", 0, () =>
            {
                panel.Refresh();
                root.SetAsLastSibling();
                root.gameObject.SetActive(true);
            });
            var entryRect = (RectTransform)entry.transform.parent;
            entryRect.anchorMin = entryRect.anchorMax = entryRect.pivot = new Vector2(0, 1);
            entryRect.sizeDelta = new Vector2(250, 48);
            entryRect.anchoredPosition = new Vector2(24, -24);
            root.gameObject.SetActive(false);
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

        TMP_Text Label(Transform parent, string value, Vector2 position, int points = 22)
        {
            var r = Rect("Texto", parent, new Vector2(650, 54), position);
            var text = r.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = points;
            text.enableAutoSizing = false;
            text.color = new Color(.87f, .91f, .93f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        TMP_Text Button(Transform parent, string value, float y, UnityEngine.Events.UnityAction action)
        {
            var r = Rect(value, parent, new Vector2(680, 62), new Vector2(0, y));
            var image = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(.08f, .1f, .11f);
            var button = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var text = Label(r, value, Vector2.zero);
            var tr = (RectTransform)text.transform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            return text;
        }

        void Refresh()
        {
            assistLabel.text = "Arrombar com ajuda: " + (LockpickPreferences.Assisted ? "sim" : "não");
            autoLabel.text = "Arrombar automaticamente ao segurar: " + (LockpickPreferences.Automatic ? "sim" : "não");
            modeLabel.text = "HUD: " + (EquipmentPreferences.Mode == EquipmentHudMode.Always ? "Sempre mostrar" : EquipmentPreferences.Mode == EquipmentHudMode.Hidden ? "Não mostrar" : "Esconder sozinha");
            lightLabel.text = "Manter a lanterna acesa ao trocar: " + (EquipmentPreferences.KeepLight ? "sim" : "não");
            wheelLabel.text = "Inverter roda do mouse: " + (EquipmentPreferences.ReverseWheel ? "sim" : "não");
        }

        public static bool TryClose()
        {
            foreach (var panel in Object.FindObjectsByType<EquipmentOptionsPanel>(FindObjectsSortMode.None))
                if (panel.gameObject.activeInHierarchy)
                {
                    panel.gameObject.SetActive(false);
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

            var canvas = (RectTransform)GetComponentInParent<Canvas>().transform;
            frame.localScale = Vector3.one * Mathf.Min(1, (canvas.rect.width - 40) / 720, (canvas.rect.height - 40) / 600);
        }
    }
}
