// Mostra os novos registros em fila, com miniatura e som quando disponíveis.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame
{
    public sealed class DiscoveryNotifications : MonoBehaviour
    {
        sealed class Notice
        {
            public string id, heading, title, message;
            public Texture2D image;
            public float duration;
            public ArchiveCategory category;
        }

        [SerializeField, Min(1)] float duration = 3.6f;
        readonly Queue<Notice> pending = new Queue<Notice>();
        readonly HashSet<string> emitted = new HashSet<string>(StringComparer.Ordinal);
        GameHUD hud;
        Inventory inventory;
        PhoneMessages phone;
        Func<InventoryItem, InspectionItemData> definition;
        Func<InventoryItem, InspectionItemData, Texture2D> picture;
        GameObject panel;
        RectTransform card;
        CanvasGroup group;
        TMP_Text heading, title, message;
        RawImage image;
        AspectRatioFitter aspect;
        Notice current;
        float elapsed;

        public void Initialize(GameHUD owner, Func<InventoryItem, InspectionItemData> find,
            Func<InventoryItem, InspectionItemData, Texture2D> artwork)
        {
            hud = owner;
            inventory = owner.interaction.inventory;
            phone = PhoneMessages.For(hud.game);
            definition = find;
            picture = artwork;
            inventory.ItemAdded += Added;
            inventory.Restored += ResetQueue;
        }

        void Added(InventoryItem item)
        {
            if (!ArchiveRecords.Visible(item) || ArchiveRecords.IsEmail(item) || hud.game.IsRestoringCheckpoint
                || hud.game.State == GameState.GameOver || !emitted.Add(item.id)) return;
            var asset = definition(item);
            var category = ArchiveRecords.Get(item, asset).category;
            string header, hint;
            switch (category)
            {
                case ArchiveCategory.Documents:
                    header = "NOVO DOCUMENTO ENCONTRADO";
                    hint = "Pressione Tab para consultar o arquivo.";
                    break;
                case ArchiveCategory.Clues:
                    header = "NOVA PISTA DESCOBERTA";
                    hint = "Nova informação adicionada ao arquivo.";
                    break;
                case ArchiveCategory.Media:
                    header = "NOVO REGISTRO ENCONTRADO";
                    hint = "Pressione Tab para consultar o arquivo.";
                    break;
                default:
                    header = "ITEM IMPORTANTE ADQUIRIDO";
                    hint = "Adicionado ao inventário.";
                    break;
            }
            Enqueue(item.id, category, header, item.title, hint, picture(item, asset), duration);
        }

        void Enqueue(string id, ArchiveCategory category, string header, string name, string hint, Texture2D thumbnail, float seconds)
        {
            pending.Enqueue(new Notice
            {
                id = id, heading = header, title = name, message = hint,
                image = thumbnail, duration = seconds, category = category
            });
        }

        bool Loading => SceneTransitionService.ControlsBlocked || hud.game.State == GameState.StoryScreen
            || hud.game.State == GameState.GameOver || hud.game.IsBlackingOut;

        void LateUpdate()
        {
            if (pending.Count == 0 && current == null) return;
            if (Loading)
            {
                if (hud.game.State == GameState.GameOver || hud.game.IsBlackingOut) ResetQueue();
                else if (panel) panel.SetActive(false);
                return;
            }
            bool safe = hud.game.CanControl && !(hud.game.narrative && hud.game.narrative.SequencePlaying)
                && !(hud.game.tutorials && hud.game.tutorials.IsVisible)
                && !(hud.game.save && hud.game.save.HasPendingSave)
                && !(InspectionManager.Instance && InspectionManager.Instance.IsCompleting)
                && !(phone && phone.BannerVisible);
            if (!safe)
            {
                if (panel) panel.SetActive(false);
                return;
            }
            if (current == null)
            {
                do { current = pending.Dequeue(); }
                while (!inventory.Contains(current.id) && pending.Count > 0);
                if (!inventory.Contains(current.id)) { current = null; return; }
                Begin();
            }
            if (!inventory.Contains(current.id)) { Finish(); return; }
            panel.SetActive(true);
            elapsed += Time.unscaledDeltaTime;
            bool reduced = PresentationSettings.ReduceEffects;
            group.alpha = reduced ? 1 : Mathf.Min(Mathf.Clamp01(elapsed / .24f), Mathf.Clamp01((current.duration - elapsed) / .38f));
            card.anchoredPosition = new Vector2(-32 + (reduced ? 0 : (1 - group.alpha) * 22), 140);
            if (elapsed >= current.duration) Finish();
        }

        void Begin()
        {
            if (!panel) Build();
            elapsed = 0;
            heading.text = current.heading;
            title.text = current.title;
            message.text = current.message;
            heading.color = current.category == ArchiveCategory.Documents ? new Color32(216, 184, 137, 255)
                : current.category == ArchiveCategory.Clues ? new Color32(222, 142, 137, 255) : new Color32(213, 208, 198, 255);
            image.texture = current.image;
            image.gameObject.SetActive(current.image);
            if (current.image) aspect.aspectRatio = (float)current.image.width / current.image.height;
            float left = current.image ? .27f : .055f;
            foreach (var label in new[] { heading, title, message })
            {
                var rect = label.rectTransform;
                rect.anchorMin = new Vector2(left, rect.anchorMin.y);
            }
            var manager = AudioManager.Instance;
            if (manager && current.category != ArchiveCategory.Media)
            {
                bool document = current.category == ArchiveCategory.Documents;
                var id = document ? "paper_rustle" : "ui_click";
                var sound = Resources.Load<SoundSet>("NarrativeArchive/Sound-" + id);
                if (sound) manager.Play(sound, Vector3.zero, document ? .11f : .09f, AudioChannel.UI, false);
            }
        }

        void Finish() { current = null; if (panel) panel.SetActive(false); }
        void ResetQueue() { pending.Clear(); Finish(); }

        void Build()
        {
            var root = hud.inventoryPanel.transform.parent;
            card = NarrativeArchive.Area("Notificação de descoberta", root, new Vector2(1, 0), new Vector2(1, 0));
            card.pivot = new Vector2(1, 0);
            card.sizeDelta = new Vector2(375, 104);
            card.SetAsLastSibling();
            panel = card.gameObject;
            group = panel.AddComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false;
            Paint(card, new Color32(28, 24, 26, 246));
            Paint(NarrativeArchive.Area("Filete", card, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(3, 0)), new Color32(145, 59, 59, 255));
            var thumb = NarrativeArchive.Area("Moldura da miniatura", card, new Vector2(.036f, .12f), new Vector2(.228f, .88f));
            image = NarrativeArchive.Area("Miniatura", thumb, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            aspect = image.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            heading = Label("Categoria", .70f, .91f, 16);
            title = Label("Nome", .36f, .70f, 22);
            message = Label("Instrução", .09f, .35f, 15);
        }

        TMP_Text Label(string name, float bottom, float top, int size)
        {
            var text = NarrativeArchive.Area(name, card, new Vector2(.055f, bottom), new Vector2(.962f, top))
                .gameObject.AddComponent<TextMeshProUGUI>();
            if (hud.noteBody) text.font = hud.noteBody.font;
            text.alignment = TextAlignmentOptions.Left;
            text.richText = text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12;
            text.fontSize = text.fontSizeMax = size;
            text.color = new Color32(231, 220, 195, 255);
            return text;
        }

        static void Paint(RectTransform rect, Color color)
        {
            var background = rect.gameObject.AddComponent<Image>();
            background.color = color;
            background.raycastTarget = false;
        }

        void OnDestroy()
        {
            if (inventory) { inventory.ItemAdded -= Added; inventory.Restored -= ResetQueue; }
            if (panel) Destroy(panel);
        }
    }
}

