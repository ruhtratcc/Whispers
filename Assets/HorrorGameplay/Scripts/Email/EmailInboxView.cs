// Exibe a caixa de entrada e o conteúdo das mensagens.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace HorrorGame
{
    public sealed class EmailInboxView : MonoBehaviour
    {
        public bool AttachmentOpen => attachmentPanel && attachmentPanel.gameObject.activeSelf;
        public UnityEngine.UI.ScrollRect BodyScroll => bodyScroll;
        public UnityEngine.UI.ScrollRect ListScroll => listScroll;
        public TMP_Text Counter => counter;
        public RectTransform Frame => frame;
        public RectTransform MessagePane => messagePane;
        public RectTransform AttachmentPanel => attachmentPanel;
        public UnityEngine.UI.Button BackButton => back;
        public UnityEngine.UI.RawImage AttachmentImage => attachmentImage;
        public IReadOnlyDictionary<string, UnityEngine.UI.Button> MessageButtons => buttons;
        public IReadOnlyList<TMP_Text> Paragraphs => paragraphs;

        EmailInboxController owner;
        TMP_FontAsset font, handwriting;
        RectTransform frame, listPane, messagePane, bodyContent, listContent, attachments, attachmentPanel;
        UnityEngine.UI.ScrollRect bodyScroll, listScroll;
        UnityEngine.UI.RawImage attachmentImage;
        UnityEngine.UI.Button back, retry;
        TMP_Text counter, subject, metadata, status, attachmentName, attachmentError, empty;
        readonly Dictionary<string, UnityEngine.UI.Button> buttons = new Dictionary<string, UnityEngine.UI.Button>();
        readonly List<TMP_Text> paragraphs = new List<TMP_Text>();
        EmailDevice device;
        GameObject ownedEventSystem;
        GameObject previousSelection;
        bool built;
        readonly Color ink = new Color(.11f, .15f, .14f);
        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = lo;
            r.offsetMax = hi;
            return r;
        }

        static RectTransform Fill(string name, Transform parent) => Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        static UnityEngine.UI.Image Plate(RectTransform r, Color color)
        {
            var i = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            i.color = color;
            return i;
        }

        TMP_Text Text(string name, Transform parent, string text, float size, Color color)
        {
            var t = Fill(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.text = text;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.raycastTarget = false;
            t.richText = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        UnityEngine.UI.Button Button(RectTransform rect, string label, Action click)
        {
            var image = Plate(rect, new Color(.76f, .79f, .75f));
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(.85f, .9f, .84f);
            colors.pressedColor = new Color(.6f, .72f, .66f);
            button.colors = colors;
            var text = Text("Label", rect, label, 17, ink);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.margin = new Vector4(12, 3, 12, 3);
            button.onClick.AddListener(() => click());
            return button;
        }

        UnityEngine.UI.ScrollRect Scroll(string name, Transform parent, out RectTransform content)
        {
            var root = Fill(name, parent);
            var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport = Fill("Viewport", root);
            Plate(viewport, Color.white);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            content = Rect("Content", viewport, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 12;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            viewport.offsetMax = new Vector2(-14, 0);
            var barRect = Rect("VerticalScrollbar", root, new Vector2(1, 0), Vector2.one, new Vector2(-10, 0), Vector2.zero);
            Plate(barRect, new Color(.80f, .82f, .77f));
            var handle = Fill("Handle", barRect);
            var handleImage = Plate(handle, new Color(.43f, .52f, .47f));
            var bar = barRect.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = handleImage;
            bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }

        public void Initialize(EmailInboxController controller, TMP_FontAsset normal, TMP_FontAsset child)
        {
            owner = controller;
            font = normal ? normal : TMP_Settings.defaultFontAsset;
            handwriting = child ? child : font;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 160;
            var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            Plate((RectTransform)transform, new Color(.006f, .011f, .012f, .94f));
            frame = Rect("MailWindow", transform, Vector2.zero, Vector2.one, new Vector2(36, 36), new Vector2(-36, -36));
            Plate(frame, new Color(.82f, .84f, .80f));
            var title = Rect("TitleBar", frame, new Vector2(0, 1), Vector2.one, new Vector2(0, -40), Vector2.zero);
            Plate(title, new Color(.12f, .24f, .23f));
            var heading = Text("Heading", title, "CORREIO  /  João de Claus", 20, new Color(.91f, .94f, .87f));
            heading.margin = new Vector4(16, 7, 12, 4);
            var toolbar = Rect("MailboxBar", frame, new Vector2(0, 1), Vector2.one, new Vector2(12, -80), new Vector2(-12, -44));
            counter = Text("UnreadCount", toolbar, "", 18, ink);
            counter.alignment = TextAlignmentOptions.MidlineLeft;
            var footer = Rect("Footer", frame, Vector2.zero, new Vector2(1, 0), new Vector2(12, 8), new Vector2(-12, 44));
            back = Button(Rect("Back", footer, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(125, 0)), "Voltar  [Esc]", owner.Back);
            retry = Button(Rect("RetrySave", footer, new Vector2(1, 0), Vector2.one, new Vector2(-160, 0), Vector2.zero), "Tentar salvar", () => owner.RetryPendingSave());
            status = Text("Status", Rect("StatusArea", footer, Vector2.zero, Vector2.one, new Vector2(139, 0), new Vector2(-170, 0)), "", 15, ink);
            status.alignment = TextAlignmentOptions.MidlineLeft;
            listPane = Rect("InboxList", frame, Vector2.zero, Vector2.one, new Vector2(12, 54), new Vector2(-12, -84));
            Plate(listPane, new Color(.88f, .89f, .85f));
            listScroll = Scroll("Messages", listPane, out listContent);
            messagePane = Rect("Message", frame, Vector2.zero, Vector2.one, new Vector2(12, 54), new Vector2(-12, -84));
            Plate(messagePane, new Color(.94f, .94f, .89f));
            subject = Text("Subject", Rect("SubjectArea", messagePane, new Vector2(0, 1), Vector2.one, new Vector2(18, -61), new Vector2(-18, -10)), "", 22, ink);
            subject.fontStyle = FontStyles.Bold;
            metadata = Text("SenderDate", Rect("MetadataArea", messagePane, new Vector2(0, 1), Vector2.one, new Vector2(18, -109), new Vector2(-18, -64)), "", 16, ink);
            var body = Rect("Body", messagePane, Vector2.zero, Vector2.one, new Vector2(6, 12), new Vector2(-6, -112));
            bodyScroll = Scroll("Reading", body, out bodyContent);
            attachments = Rect("Attachments", messagePane, Vector2.zero, new Vector2(1, 0), new Vector2(18, 12), new Vector2(-18, 130));
            empty = Text("EmptySelection", messagePane, "Selecione uma mensagem na caixa de entrada.", 21, ink);
            empty.margin = new Vector4(26, 150, 26, 20);
            attachmentPanel = Fill("AttachmentViewer", frame);
            Plate(attachmentPanel, new Color(.04f, .06f, .06f, .99f));
            attachmentName = Text("FileName", Rect("AttachmentHeading", attachmentPanel, new Vector2(0, 1), Vector2.one, new Vector2(20, -64), new Vector2(-20, -16)), "", 21, Color.white);
            var imageArea = Rect("ImageArea", attachmentPanel, Vector2.zero, Vector2.one, new Vector2(24, 76), new Vector2(-24, -76));
            var imageRect = Fill("Image", imageArea);
            attachmentImage = imageRect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            attachmentImage.raycastTarget = false;
            imageRect.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>().aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            attachmentError = Text("Error", imageArea, "", 21, Color.white);
            attachmentError.alignment = TextAlignmentOptions.Center;
            Button(Rect("CloseAttachment", attachmentPanel, Vector2.zero, new Vector2(0, 0), new Vector2(20, 16), new Vector2(260, 60)), "Voltar à mensagem  [Esc]", CloseAttachment);
            attachmentPanel.gameObject.SetActive(false);
            built = true;
        }

        public void Present(EmailDevice mode)
        {
            device = mode;
            if (!EventSystem.current)
            {
                if (!ownedEventSystem)
                {
                    ownedEventSystem = new GameObject("EmailEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                }
                else
                    ownedEventSystem.SetActive(true);
            }

            previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            ApplyLayout();
            RefreshList();
            ShowInbox();
        }

        static void Clear(RectTransform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var item = content.GetChild(i);
                item.SetParent(null, false);
                Destroy(item.gameObject);
            }
        }

        public void RefreshList()
        {
            Clear(listContent);
            buttons.Clear();
            foreach (var message in owner.catalog.messages)
            {
                if (!owner.State.IsDelivered(message.id))
                    continue;
                string id = message.id;
                var rect = new GameObject(id, typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement)).GetComponent<RectTransform>();
                rect.SetParent(listContent, false);
                rect.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = device == EmailDevice.Phone ? 100 : 112;
                var button = Button(rect, message.sender + "\n" + Subject(message) + "\n" + GameClock.MailDate(message), () => owner.SelectMessage(id));
                var label = button.GetComponentInChildren<TMP_Text>();
                label.fontSize = 17;
                label.fontStyle = owner.State.IsRead(id) ? FontStyles.Normal : FontStyles.Bold;
                label.overflowMode = TextOverflowModes.Ellipsis;
                if (owner.CurrentMessageId == id)
                    ((UnityEngine.UI.Image)button.targetGraphic).color = new Color(.64f, .76f, .70f);
                buttons.Add(id, button);
            }

            counter.text = "Caixa de entrada   /   " + owner.UnreadCount + " não lidas";
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            RefreshStatus();
        }

        static string Subject(EmailMessage message) => string.IsNullOrEmpty(message.subject) ? "(sem assunto)" : message.subject;
        public void ShowMessage(EmailMessage message)
        {
            empty.gameObject.SetActive(false);
            messagePane.gameObject.SetActive(true);
            if (device == EmailDevice.Phone)
                listPane.gameObject.SetActive(false);
            subject.text = Subject(message);
            metadata.text = "De: " + message.sender + "\n" + GameClock.MailDate(message) + (string.IsNullOrEmpty(GameClock.MailTime(message)) ? "" : "   " + GameClock.MailTime(message));
            Clear(bodyContent);
            paragraphs.Clear();
            foreach (var paragraph in message.paragraphs)
            {
                var t = Text(paragraph.child ? "ChildWriting" : "Paragraph", bodyContent, paragraph.text, paragraph.child ? 26 : 21, paragraph.child ? new Color(.38f, .16f, .16f) : ink);
                t.font = paragraph.child ? handwriting : font;
                t.margin = Vector4.zero;
                paragraphs.Add(t);
            }

            Clear(attachments);
            bool hasAttachments = message.attachments != null && message.attachments.Length > 0;
            attachments.gameObject.SetActive(hasAttachments);
            ((RectTransform)bodyScroll.transform.parent).offsetMin = new Vector2(6, hasAttachments ? 140 : 12);
            if (hasAttachments)
            {
                for (int i = 0; i < message.attachments.Length; i++)
                {
                    var attachment = message.attachments[i];
                    Button(Rect("Attachment " + i, attachments, new Vector2(0, 1), Vector2.one, new Vector2(0, -34 * (i + 1)), new Vector2(0, -34 * i - 4)), attachment.fileName, () => OpenAttachment(attachment));
                }
            }

            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(bodyContent);
            bodyScroll.verticalNormalizedPosition = 1;
            bodyScroll.StopMovement();
        }

        public void ShowInbox()
        {
            CloseAttachment();
            listPane.gameObject.SetActive(true);
            messagePane.gameObject.SetActive(device == EmailDevice.Desktop);
            empty.gameObject.SetActive(true);
            subject.text = metadata.text = "";
            Clear(bodyContent);
            paragraphs.Clear();
            attachments.gameObject.SetActive(false);
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(back.gameObject);
        }

        public void OpenAttachment(EmailAttachment attachment)
        {
            attachmentPanel.gameObject.SetActive(true);
            attachmentPanel.SetAsLastSibling();
            attachmentName.text = attachment.fileName;
            bool available = !attachment.unavailable && attachment.preview;
            attachmentImage.gameObject.SetActive(available);
            attachmentError.gameObject.SetActive(!available);
            attachmentError.text = attachment.unavailable ? "Não foi possível abrir este anexo." : "Não foi possível carregar a imagem.";
            attachmentImage.texture = available ? attachment.preview : null;
            if (available)
                attachmentImage.GetComponent<UnityEngine.UI.AspectRatioFitter>().aspectRatio = (float)attachment.preview.width / attachment.preview.height;
        }

        public void CloseAttachment()
        {
            attachmentPanel.gameObject.SetActive(false);
            attachmentImage.texture = null;
        }

        public void FocusBack()
        {
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(back.gameObject);
        }

        public void RefreshStatus()
        {
            status.text = owner.PersistenceError;
            retry.gameObject.SetActive(owner.HasPendingSave);
            retry.interactable = owner.CanRetrySave;
        }

        void ApplyLayout()
        {
            if (!built)
                return;
            if (device == EmailDevice.Phone)
            {
                frame.anchorMin = frame.anchorMax = new Vector2(.5f, .5f);
                var canvas = (RectTransform)transform;
                frame.sizeDelta = new Vector2(Mathf.Min(460, canvas.rect.width - 40), Mathf.Max(360, canvas.rect.height - 48));
                frame.anchoredPosition = Vector2.zero;
                listPane.anchorMax = Vector2.one;
                listPane.offsetMin = new Vector2(12, 88);
                listPane.offsetMax = new Vector2(-12, -84);
                messagePane.anchorMin = Vector2.zero;
                messagePane.offsetMin = new Vector2(12, 88);
                var area = (RectTransform)status.transform.parent;
                area.SetParent(frame, false);
                area.anchorMin = Vector2.zero;
                area.anchorMax = new Vector2(1, 0);
                area.offsetMin = new Vector2(18, 45);
                area.offsetMax = new Vector2(-18, 80);
            }
            else
            {
                frame.anchorMin = Vector2.zero;
                frame.anchorMax = Vector2.one;
                frame.offsetMin = new Vector2(36, 36);
                frame.offsetMax = new Vector2(-36, -36);
                listPane.anchorMax = new Vector2(.31f, 1);
                listPane.offsetMin = new Vector2(12, 54);
                listPane.offsetMax = new Vector2(-6, -84);
                messagePane.anchorMin = new Vector2(.31f, 0);
                messagePane.offsetMin = new Vector2(6, 54);
                var area = (RectTransform)status.transform.parent;
                area.SetParent(back.transform.parent, false);
                area.anchorMin = Vector2.zero;
                area.anchorMax = Vector2.one;
                area.offsetMin = new Vector2(139, 0);
                area.offsetMax = new Vector2(-170, 0);
            }
        }

        void OnRectTransformDimensionsChange() => ApplyLayout();
        void OnDisable()
        {
            if (owner)
                owner.Close();
            if (ownedEventSystem)
                ownedEventSystem.SetActive(false);
            else if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(previousSelection && previousSelection.activeInHierarchy ? previousSelection : null);
        }

        void OnDestroy()
        {
            if (ownedEventSystem)
                Destroy(ownedEventSystem);
        }
    }
}
