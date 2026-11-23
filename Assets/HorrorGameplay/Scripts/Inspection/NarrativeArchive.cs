// Mostra e reabre os registros que João já coletou.

using System;
using System.Linq;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame
{
    public sealed class NarrativeArchive : MonoBehaviour
    {
        static readonly string[] Categories = { "Documentos", "Objetos importantes", "Pistas", "Fotografias e mídias" };
        static readonly Color Ink = new Color32(23, 21, 22, 255);
        static readonly Color Surface = new Color32(35, 32, 33, 250);
        static readonly Color Paper = new Color32(228, 216, 190, 255);
        static readonly Color Muted = new Color32(184, 171, 151, 255);
        static readonly Color Accent = new Color32(148, 65, 64, 255);
        static readonly Color Selected = new Color32(82, 43, 44, 255);
        static readonly Color Clear = Color.clear;
        static readonly Vector2 TopLeft = new Vector2(0, 1);

        GameHUD hud;
        Inventory inventory;
        SchoolInspectionCatalog catalog;
        InspectionManager inspection;
        TMP_FontAsset font, headingFont;
        Texture2D paper;
        CanvasGroup group;
        RectTransform rows, bodyContent, readerImageBox;
        ScrollRect listScroll, bodyScroll, imageScroll;
        TMP_Text chapter, objective, count, empty, title, metadata, body, pageLabel;
        RawImage readerImage;
        GameObject reader, detail;
        Button[] categories = new Button[4];
        Button read, inspect, listen, equip, previous, next;
        readonly System.Collections.Generic.List<Texture2D> pages = new System.Collections.Generic.List<Texture2D>();
        int selectedCategory, pageIndex;
        float zoom = 1;
        string selectedId;
        bool dirty, shown;
        int lastDisplayWidth, lastDisplayHeight;
        public Button CloseButton { get; private set; }

        public void Initialize(GameHUD owner)
        {
            hud = owner;
            inventory = hud.interaction.inventory;
            catalog = Resources.Load<SchoolInspectionCatalog>("NarrativeArchive/ArchiveItems");
            paper = Resources.Load<Texture2D>("NarrativeArchive/Paper");
            font = hud.noteBody ? hud.noteBody.font : TMP_Settings.defaultFontAsset;
            headingFont = hud.objective ? hud.objective.font : font;
            inventory.Changed += Changed;
            inventory.Restored += ResetReader;
            gameObject.AddComponent<DiscoveryNotifications>().Initialize(hud, Definition, Picture);
        }

        void Changed() => dirty = true;
        void ResetReader()
        {
            activeId = selectedId = null;
        }
        InspectionItemData Definition(InventoryItem item) => catalog ? catalog.Find(item.inspectionId) ?? catalog.Find(item.id) : null;
        public void Show()
        {
            inspection = hud.game.GetComponent<InspectionManager>();
            if (!group) Build();
            shown = true;
            group.alpha = PresentationSettings.ReduceEffects ? 1 : 0;
            group.blocksRaycasts = group.interactable = true;
            var current = hud.game.narrative?.CurrentChapter;
            chapter.text = current == null ? "" : (string.IsNullOrWhiteSpace(current.heading) ? current.title : current.heading + "  ·  " + current.title);
            var service = hud.game.GetComponent<ObjectiveService>();
            var goal = hud.objective ? hud.objective.text : "";
            var subitems = service && service.CurrentEntry != null ? service.CurrentEntry.subitems : null;
            objective.text = subitems != null && subitems.Length > 0
                ? goal + "\n" + string.Join("    ·    ", subitems.Select((item, index) =>
                    (service.IsSubitemComplete(index) ? "[x] " : "[ ] ") + item?.label))
                : goal;
            RebuildList();
        }

        public void Hide()
        {
            shown = false;
            if (!group) { hud.inventoryPanel.SetActive(false); return; }
            group.blocksRaycasts = group.interactable = false;
            if (PresentationSettings.ReduceEffects) { group.alpha = 0; hud.inventoryPanel.SetActive(false); }
        }

        void LateUpdate()
        {
            if (!group || !hud.inventoryPanel.activeSelf) return;
            group.alpha = Mathf.MoveTowards(group.alpha, shown ? 1 : 0, Time.unscaledDeltaTime * 6);
            if (!shown && group.alpha <= 0) hud.inventoryPanel.SetActive(false);
            if (shown && dirty) RebuildList();
            if (shown && reader.activeSelf && (Screen.width != lastDisplayWidth || Screen.height != lastDisplayHeight))
                ResizeImage(zoom);
        }

        void Build()
        {
            var root = (RectTransform)hud.inventoryPanel.transform;
            foreach (Transform child in root) child.gameObject.SetActive(false);
            var overlay = root.GetComponent<Image>();
            if (overlay) overlay.color = new Color(.025f, .022f, .024f, .91f);
            group = root.GetComponent<CanvasGroup>();
            if (!group) group = root.gameObject.AddComponent<CanvasGroup>();

            var frame = Area("Arquivo pessoal", root, new Vector2(.045f, .06f), new Vector2(.955f, .94f));
            Paint(frame, Ink);
            if (paper)
            {
                var grain = Area("Papel envelhecido", frame, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
                grain.texture = paper;
                grain.color = new Color(.61f, .50f, .43f, .16f);
                grain.raycastTarget = false;
            }
            var header = Area("Cabeçalho", frame, new Vector2(0, .823f), Vector2.one);
            Paint(header, Surface);
            var rule = Area("Linha", header, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 2));
            Paint(rule, Accent);
            var heading = Label("Título", header, "ARQUIVO PESSOAL", new Vector2(.024f, .40f), new Vector2(.67f, .90f), 46);
            heading.font = headingFont;
            heading.fontStyle = FontStyles.Bold;
            chapter = Label("Capítulo atual", header, "", new Vector2(.026f, .13f), new Vector2(.71f, .40f), 20, Muted);
            CloseButton = Control("Fechar [Esc]", header, new Vector2(.832f, .25f), new Vector2(.976f, .72f), hud.game.Resume);

            var side = Area("Categorias", frame, new Vector2(0, .12f), new Vector2(.245f, .823f), new Vector2(0, 1), new Vector2(-1, -1));
            Paint(side, Surface);
            Label("Seção", side, "REGISTROS", new Vector2(.09f, .86f), new Vector2(.94f, .97f), 17, Muted);
            for (int i = 0; i < categories.Length; i++)
            {
                int index = i;
                float y = .83f - i * .145f;
                categories[i] = Control(Categories[i], side, new Vector2(.055f, y - .116f), new Vector2(.945f, y), () => SelectCategory(index));
            }
            Label("Atalho", side, "Tab  /  I", new Vector2(.09f, .03f), new Vector2(.94f, .15f), 17, Muted);
            var center = Area("Lista de registros", frame, new Vector2(.245f, .12f), new Vector2(.544f, .823f), new Vector2(1, 1), new Vector2(-1, -1));
            Paint(center, new Color32(29, 26, 27, 252));
            count = Label("Contagem", center, "", new Vector2(.065f, .87f), new Vector2(.95f, .98f), 18, Muted);
            listScroll = Scroller("Lista rolável", center, new Vector2(.045f, .03f), new Vector2(.955f, .86f), out rows);
            Stack(rows, 7, 2);
            empty = Label("Lista vazia", center, "Nenhum registro encontrado.", new Vector2(.065f, .61f), new Vector2(.95f, .86f), 20, Muted);

            var right = Area("Visualização", frame, new Vector2(.544f, .12f), new Vector2(1, .823f), new Vector2(1, 1), new Vector2(0, -1));
            Paint(right, Surface);
            detail = Area("Registro selecionado", right, Vector2.zero, Vector2.one).gameObject;
            title = Label("Nome do registro", detail.transform, "", new Vector2(.044f, .83f), new Vector2(.956f, .98f), 29);
            title.fontStyle = FontStyles.Bold;
            metadata = Label("Origem e estado", detail.transform, "", new Vector2(.046f, .70f), new Vector2(.954f, .83f), 17, Muted);

            bodyScroll = Scroller("Conteúdo rolável", detail.transform, new Vector2(.04f, .16f), new Vector2(.96f, .68f), out bodyContent);
            Stack(bodyContent, 12, 4);
            reader = Area("Visualizador de imagens", detail.transform, new Vector2(.04f, .16f), new Vector2(.96f, .68f)).gameObject;
            Paint((RectTransform)reader.transform, Ink);
            imageScroll = Scroller("Imagem rolável", reader.transform, Vector2.zero, Vector2.one, out readerImageBox);
            var viewport = imageScroll.viewport;
            viewport.anchorMin = new Vector2(.005f, .15f);
            imageScroll.horizontal = true;
            readerImage = Artwork(readerImageBox, null);
            var toolbar = Area("Controles da imagem", reader.transform, Vector2.zero, new Vector2(1, .15f));
            Paint(toolbar, Surface);
            previous = Control("‹", toolbar, new Vector2(.01f, .08f), new Vector2(.12f, .92f), () => ChangePage(-1));
            next = Control("›", toolbar, new Vector2(.13f, .08f), new Vector2(.24f, .92f), () => ChangePage(1));
            pageLabel = Label("Página", toolbar, "", new Vector2(.28f, 0), new Vector2(.60f, 1), 16, Muted);
            Control("−", toolbar, new Vector2(.64f, .08f), new Vector2(.80f, .92f), () => ResizeImage(zoom / 1.3f));
            Control("+", toolbar, new Vector2(.82f, .08f), new Vector2(.98f, .92f), () => ResizeImage(zoom * 1.3f));

            read = Control("Ler", detail.transform, new Vector2(.04f, .027f), new Vector2(.44f, .134f), Read);
            inspect = Control("Examinar em 3D", detail.transform, new Vector2(.47f, .027f), new Vector2(.96f, .134f), () => Reopen(false));
            listen = Control("Ouvir gravação", detail.transform, new Vector2(.47f, .027f), new Vector2(.96f, .134f), () => Reopen(true));
            equip = Control("Equipar", detail.transform, new Vector2(.04f, .027f), new Vector2(.44f, .134f), Equip);

            Label("Objetivo", frame, "OBJETIVO", new Vector2(.024f, .006f), new Vector2(.18f, .118f), 17, Muted);
            objective = Label("Objetivo atual", frame, "", new Vector2(.181f, .006f), new Vector2(.976f, .118f), 20);
        }

        void SelectCategory(int category)
        {
            if (selectedCategory == category) return;
            selectedCategory = category;
            selectedId = null;
            RebuildList();
        }

        void RebuildList()
        {
            dirty = false;
            ClearChildren(rows);
            foreach (var button in categories) button.image.color = Surface;
            categories[selectedCategory].image.color = Selected;
            int total = 0;
            InventoryItem selected = null, first = null;
            foreach (var item in inventory.items)
            {
                if (!ArchiveRecords.Visible(item)) continue;
                var data = Definition(item);
                if ((int)ArchiveRecords.Get(item, data).category != selectedCategory) continue;
                first ??= item;
                if (item.id == selectedId) selected = item;
                total++;
                var entry = Control(item.title, rows, Vector2.zero, Vector2.one, () =>
                {
                    selectedId = item.id;
                    RebuildList();
                });
                entry.name = "Registro " + item.id;
                entry.image.color = item.id == selectedId ? Selected : Surface;
                entry.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
                var label = entry.GetComponentInChildren<TMP_Text>();
                var picture = Picture(item, data);
                Gap(label.rectTransform, new Vector2(picture ? 66 : 12, 0), new Vector2(-13, 0));
                if (ArchiveRecords.IsNew(item))
                {
                    label.fontStyle = FontStyles.Bold;
                    Paint(Area("Novo", entry.transform, new Vector2(0, .19f), new Vector2(0, .81f), Vector2.zero, new Vector2(3, 0)), Accent);
                }
                if (picture) Artwork(Area("Miniatura", entry.transform, new Vector2(0, .09f), new Vector2(0, .91f), new Vector2(9, 0), new Vector2(57, 0)), picture);
            }
            count.text = total == 1 ? "1 registro" : total + " registros";
            empty.gameObject.SetActive(total == 0);
            detail.SetActive(total > 0);
            if (selected == null && first != null)
            {
                selectedId = first.id;
                foreach (Transform row in rows)
                    if (row.gameObject.activeSelf && row.name == "Registro " + selectedId) row.GetComponent<Image>().color = Selected;
                selected = first;
            }
            if (selected != null)
            {
                bool keepReader = reader.activeSelf && activeId == selected.id;
                ShowRecord(selected, keepReader);
            }
            else { activeId = null; reader.SetActive(false); }
        }

        string activeId;
        void ShowRecord(InventoryItem item, bool keepReader)
        {
            bool changedSelection = activeId != item.id;
            activeId = selectedId = item.id;
            var info = Definition(item);
            var details = ArchiveRecords.Get(item, info);
            var picture = Picture(item, info);
            title.text = item.title;
            var origin = hud.game.narrative?.book?.Chapter(details.chapter ?? item.chapterId);
            string status = details.category == ArchiveCategory.Clues ? "Descoberta"
                : details.category == ArchiveCategory.Documents ? ArchiveRecords.IsNew(item) ? "Novo" : "Lido" : "";
            metadata.text = string.Join("  ·  ", new[] { details.location, origin?.title, status }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

            ClearChildren(bodyContent);
            if (picture)
            {
                var box = Area("Prévia", bodyContent, Vector2.zero, Vector2.one);
                box.gameObject.AddComponent<LayoutElement>().preferredHeight = details.category == ArchiveCategory.Objects ? 134 : 145;
                Artwork(box, picture);
            }
            var excerpt = info && info.description != info.backText && info.description != info.frontText
                ? info.description : "";
            if (string.IsNullOrWhiteSpace(excerpt) || !string.IsNullOrWhiteSpace(item.text) && excerpt == item.text)
                excerpt = ArchiveRecords.IsEmail(item) ? "Correspondência da investigação." : "";
            if (!string.IsNullOrWhiteSpace(excerpt)) Paragraph("Descrição", excerpt, bodyContent, Muted);
            if (details.related != null)
                foreach (string id in details.related)
                {
                    var related = inventory.Find(id);
                    if (!ArchiveRecords.Visible(related)) continue;
                    var link = Control("Ver documento: " + related.title, bodyContent, Vector2.zero, Vector2.one, () =>
                    {
                        selectedCategory = (int)ArchiveRecords.Get(related, Definition(related)).category;
                        selectedId = related.id;
                        RebuildList();
                    });
                    link.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
                }
            body = Paragraph("Conteúdo", FullText(item, info), bodyContent, Paper);
            if (ArchiveRecords.IsNew(item) && (details.category == ArchiveCategory.Documents || details.category == ArchiveCategory.Media))
                body.gameObject.SetActive(false);

            reader.SetActive(keepReader);
            bodyScroll.gameObject.SetActive(!keepReader);
            read.gameObject.SetActive(picture || !string.IsNullOrWhiteSpace(body.text));
            read.GetComponentInChildren<TMP_Text>().text = keepReader ? "Voltar ao texto"
                : picture ? "Abrir imagem" : "Ler";
            if (inspection && info && info.model) inspection.RegisterArchivedItem(info);
            bool canInspect = inspection && info && info.model;
            bool canListen = canInspect && info.canPlay && info.recording && info.recording.clips != null && Array.Exists(info.recording.clips, clip => clip);
            inspect.gameObject.SetActive(canInspect && !canListen);
            listen.gameObject.SetActive(canListen);
            bool hasEquipment = EquipmentSlot(item) > 0;
            equip.gameObject.SetActive(hasEquipment);
            var equipRect = (RectTransform)equip.transform;
            bool reading = read.gameObject.activeSelf;
            equipRect.anchorMin = new Vector2(reading ? .47f : .04f, .027f);
            equipRect.anchorMax = new Vector2(reading ? .70f : .44f, .134f);
            var inspectRect = (RectTransform)inspect.transform;
            inspectRect.anchorMin = new Vector2(hasEquipment && reading ? .72f : .47f, .027f);
            if (changedSelection) bodyScroll.verticalNormalizedPosition = 1;
        }

        static string FullText(InventoryItem item, InspectionItemData info)
        {
            if (!info) return item.text ?? "";
            var lines = new System.Collections.Generic.List<string>();
            foreach (var line in new[] { info.frontText, item.text, info.backText, info.detailText, info.recordingText })
                if (!string.IsNullOrWhiteSpace(line) && line != item.title && !lines.Contains(line)) lines.Add(line);
            return string.Join("\n\n", lines);
        }

        void Read()
        {
            var item = inventory.Find(selectedId);
            if (item == null) return;
            var data = Definition(item);
            MarkRead(item);
            body.gameObject.SetActive(!string.IsNullOrWhiteSpace(body.text));
            if (reader.activeSelf) { reader.SetActive(false); bodyScroll.gameObject.SetActive(true); return; }
            pages.Clear();
            var picture = Picture(item, data);
            if (picture) pages.Add(picture);
            if (data)
            {
                if (data.backTexture && !pages.Contains(data.backTexture)) pages.Add(data.backTexture);
                if (data.pages != null)
                    foreach (var page in data.pages)
                        if (page != null && page.image && !pages.Contains(page.image)) pages.Add(page.image);
            }
            if (pages.Count == 0) return;
            pageIndex = 0;
            zoom = 1;
            reader.SetActive(true);
            bodyScroll.gameObject.SetActive(false);
            ChangePage(0);
        }

        void MarkRead(InventoryItem item)
        {
            if (!inventory.MarkExamined(item.id)) return;
            if (hud.game.narrative) hud.game.narrative.ScheduleSave();
            else hud.game.save?.Save();
        }

        void ChangePage(int direction)
        {
            pageIndex = Mathf.Clamp(pageIndex + direction, 0, pages.Count - 1);
            readerImage.texture = pages[pageIndex];
            previous.interactable = pageIndex > 0;
            next.interactable = pageIndex + 1 < pages.Count;
            pageLabel.text = pages.Count > 1 ? (pageIndex + 1) + " / " + pages.Count : "Imagem";
            ResizeImage(zoom);
        }

        void ResizeImage(float scale)
        {
            zoom = Mathf.Clamp(scale, 1, 3);
            Canvas.ForceUpdateCanvases();
            lastDisplayWidth = Screen.width;
            lastDisplayHeight = Screen.height;
            var texture = pages[pageIndex];
            Vector2 view = imageScroll.viewport.rect.size;
            var fit = Mathf.Min(view.x / texture.width, view.y / texture.height);
            readerImageBox.anchorMin = readerImageBox.anchorMax = readerImageBox.pivot = Vector2.one * .5f;
            readerImageBox.sizeDelta = new Vector2(texture.width, texture.height) * fit * zoom;
            readerImageBox.anchoredPosition = Vector2.zero;
        }

        void Reopen(bool play)
        {
            if (inspection && inspection.Reopen(selectedId, play)) return;
            hud.Notify("Este registro não pode ser aberto agora.");
        }

        int EquipmentSlot(InventoryItem item)
        {
            var equipment = hud.game.equipment;
            if (equipment && item != null)
                for (int i = 0; i < equipment.items.Length; i++)
                    if (equipment.items[i] && equipment.items[i].inventoryItemId == item.id && equipment.Available(i)) return equipment.items[i].slot;
            return 0;
        }

        void Equip()
        {
            int slot = EquipmentSlot(inventory.Find(selectedId));
            if (slot <= 0) return;
            hud.game.Resume();
            StartCoroutine(EquipAfterResume(slot));
        }

        IEnumerator EquipAfterResume(int slot)
        {
            yield return null;
            if (hud.game.CanControl && hud.game.equipment) hud.game.equipment.RequestSlot(slot);
        }

        Texture2D Picture(InventoryItem item, InspectionItemData info)
        {
            var original = ArchiveRecords.OriginalImage(info);
            if (original) return original;
            string id = info ? info.itemId : item.id;
            if (!thumbnails.TryGetValue(id, out var image))
            {
                image = Resources.Load<Texture2D>("NarrativeArchive/" + id);
                thumbnails[id] = image;
            }
            return image;
        }

        readonly System.Collections.Generic.Dictionary<string, Texture2D> thumbnails = new System.Collections.Generic.Dictionary<string, Texture2D>();

        TMP_Text Paragraph(string name, string value, RectTransform content, Color color)
        {
            var label = Label(name, content, value, Vector2.zero, Vector2.one, 20, color);
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.fontSizeMin = label.fontSizeMax = 20;
            label.enableAutoSizing = false;
            if (string.IsNullOrWhiteSpace(value)) label.gameObject.SetActive(false);
            return label;
        }

        Button Control(string label, Transform parent, Vector2 min, Vector2 max, Action action)
        {
            var rect = Area(label, parent, min, max);
            var image = Paint(rect, Surface, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.16f, 1.13f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.8f, .7f, .7f);
            colors.disabledColor = new Color(.45f, .45f, .45f);
            button.colors = colors;
            button.onClick.AddListener(() => action());
            var text = Label("Rótulo", rect, label, Vector2.zero, Vector2.one, 20);
            Gap(text.rectTransform, new Vector2(12, 0), new Vector2(-12, 0));
            return button;
        }

        TMP_Text Label(string name, Transform parent, string value, Vector2 min, Vector2 max, int size, Color color = default)
        {
            var text = Area(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            if (font) text.font = font;
            text.text = value ?? "";
            text.color = color == default ? Paper : color;
            text.alignment = TextAlignmentOptions.Left;
            text.enableAutoSizing = true;
            text.fontSize = text.fontSizeMax = size;
            text.fontSizeMin = Mathf.Max(12, size * .65f);
            text.richText = false;
            text.raycastTarget = false;
            return text;
        }

        internal static RectTransform Area(string name, Transform parent, Vector2 min, Vector2 max, Vector2 low = default, Vector2 high = default)
        {
            var rect = InspectionUI.Fill(name, parent);
            rect.anchorMin = min;
            rect.anchorMax = max;
            Gap(rect, low, high);
            return rect;
        }

        static void Gap(RectTransform rect, Vector2 low, Vector2 high) { rect.offsetMin = low; rect.offsetMax = high; }
        static Image Paint(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        static RawImage Artwork(RectTransform parent, Texture2D picture)
        {
            var art = Area("Imagem", parent, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            art.texture = picture;
            art.raycastTarget = false;
            if (picture)
            {
                var fit = art.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = (float)picture.width / picture.height;
            }
            return art;
        }

        static ScrollRect Scroller(string name, Transform parent, Vector2 min, Vector2 max, out RectTransform content)
        {
            var rect = Area(name, parent, min, max);
            var scroll = rect.gameObject.AddComponent<ScrollRect>();
            var viewport = Area("Janela", rect, Vector2.zero, Vector2.one);
            Paint(viewport, Clear, true);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Area("Conteúdo", viewport, TopLeft, Vector2.one);
            content.pivot = TopLeft;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 33;
            return scroll;
        }

        static void Stack(RectTransform content, int spacing, int padding)
        {
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        static void ClearChildren(RectTransform content)
        {
            foreach (Transform child in content)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        void OnDestroy()
        {
            if (inventory) { inventory.Changed -= Changed; inventory.Restored -= ResetReader; }
        }
    }
}

