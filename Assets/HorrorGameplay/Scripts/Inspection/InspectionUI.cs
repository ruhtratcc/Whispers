// Exibe controles, páginas, leitura e gravações durante a inspeção.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HorrorGame
{
    public sealed class InspectionUI : MonoBehaviour
    {
        InspectionManager manager;
        Canvas canvas;
        CanvasGroup group, chromeGroup, hintsGroup;
        UnityEngine.UI.RawImage preview;
        RectTransform previewArea;
        TextMeshProUGUI title, description, transcription, detail, hintsText;
        UnityEngine.UI.Button flipButton, playButton, secondTapeButton, closeButton;
        TMP_Text playLabel, secondTapeLabel;
        UnityEngine.UI.AspectRatioFitter previewAspect;
        InspectionItemData displayedItem;
        bool displayedBack, displayedDetail;
        GameObject selectedBeforeZoom;
        CanvasGroup readerGroup;
        UnityEngine.UI.ScrollRect readerScroll;
        TextMeshProUGUI readerBody, readerTitle;
        UnityEngine.UI.Button readButton;
        bool readerBack;
        UnityEngine.UI.RawImage pageImage;
        UnityEngine.UI.AspectRatioFitter pageAspect;
        UnityEngine.UI.Button previousPage, nextPage;
        public int PageIndex { get; private set; }

        bool HasPages => displayedItem && displayedItem.pages != null && displayedItem.pages.Length > 0;

        float readerSanity = 1;
        public bool ReaderOpen => readerGroup && readerGroup.gameObject.activeSelf;

        float openedAt;
        public void Initialize(InspectionManager owner, TMP_FontAsset font)
        {
            if (!owner)
                throw new ArgumentNullException(nameof(owner));
            if (group)
                return;
            manager = owner;
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>();
            var backdrop = Fill("Backdrop", transform);
            var dim = backdrop.gameObject.AddComponent<UnityEngine.UI.Image>();
            dim.color = new Color(.005f, .008f, .009f, .82f);
            dim.raycastTarget = false;
            previewArea = Rect("ItemPreview", transform, new Vector2(1100, 680), new Vector2(0, 90));
            var image = Fill("Texture", previewArea);
            preview = image.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            preview.raycastTarget = false;
            previewAspect = image.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            previewAspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            var chrome = Fill("InspectionChrome", transform);
            chromeGroup = chrome.gameObject.AddComponent<CanvasGroup>();
            title = Text("ItemTitle", chrome, "", new Vector2(1180, 56), new Vector2(0, -38), font, 30);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = title.rectTransform.pivot = new Vector2(.5f, 1);
            var footer = Rect("InspectionFooter", chrome, new Vector2(1370, 280), new Vector2(0, 28));
            footer.anchorMin = footer.anchorMax = footer.pivot = new Vector2(.5f, 0);
            description = Text("Description", footer, "", new Vector2(1120, 60), new Vector2(0, 110), font, 20);
            transcription = Text("BackTranscription", footer, "", new Vector2(1120, 70), new Vector2(0, 39), font, 22);
            transcription.rectTransform.sizeDelta = new Vector2(1120, 200);
            transcription.rectTransform.anchoredPosition = new Vector2(0, 85);
            CaptionPresentation.Attach(transcription);
            detail = Text("HiddenDetail", footer, "", new Vector2(1180, 34), new Vector2(0, -21), font, 18);
            detail.color = new Color(.89f, .78f, .60f);
            var hints = Rect("Controls", footer, new Vector2(1370, 28), new Vector2(0, -61));
            hintsGroup = hints.gameObject.AddComponent<CanvasGroup>();
            hintsGroup.blocksRaycasts = false;
            hintsText = Text("Hints", hints, "", hints.sizeDelta, Vector2.zero, font, 16);
            flipButton = Button("Flip", footer, "Virar [F]", new Vector2(-282, -113), new Vector2(260, 54), font, () =>
            {
                if (CanUse())
                    manager.Flip();
            });
            playButton = Button("PlayRecording", footer, "Ouvir [R]", new Vector2(0, -113), new Vector2(260, 54), font, () =>
            {
                if (CanUse())
                    manager.PlayRecording();
            });
            secondTapeButton = Button("PlaySecondTape", footer, "Ouvir fita B", new Vector2(0, -113), new Vector2(260, 54), font, () =>
            {
                if (CanUse())
                    manager.PlayRecording(1);
            });
            playLabel = playButton.GetComponentInChildren<TMP_Text>();
            secondTapeLabel = secondTapeButton.GetComponentInChildren<TMP_Text>();
            closeButton = Button("Close", footer, "Guardar [E / Esc]", new Vector2(282, -113), new Vector2(260, 54), font, () =>
            {
                if (CanUse())
                    manager.Close();
            });
            readButton = Button("ReadText", transform, "Ler texto [T]", new Vector2(-32, 28), new Vector2(260, 54), font, ToggleReader);
            var readRect = (RectTransform)readButton.transform;
            readRect.anchorMin = readRect.anchorMax = readRect.pivot = new Vector2(1, 0);
            var reader = Fill("IndependentReader", transform);
            reader.anchorMin = new Vector2(.12f, .13f);
            reader.anchorMax = new Vector2(.88f, .87f);
            reader.offsetMin = reader.offsetMax = Vector2.zero;
            readerGroup = reader.gameObject.AddComponent<CanvasGroup>();
            reader.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.025f, .03f, .03f, .98f);
            readerTitle = Text("ReaderTitle", reader, "", new Vector2(1000, 48), new Vector2(0, -24), font, 28);
            readerTitle.rectTransform.anchorMin = readerTitle.rectTransform.anchorMax = readerTitle.rectTransform.pivot = new Vector2(.5f, 1);
            var viewport = Fill("ReaderViewport", reader);
            viewport.offsetMin = new Vector2(32, 100);
            viewport.offsetMax = new Vector2(-32, -80);
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.clear;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            readerBody = Text("ReaderText", viewport, "", Vector2.zero, Vector2.zero, font, 26);
            var body = readerBody.rectTransform;
            body.anchorMin = new Vector2(0, 1);
            body.anchorMax = body.pivot = Vector2.one;
            body.sizeDelta = Vector2.zero;
            readerBody.alignment = TextAlignmentOptions.TopLeft;
            readerBody.raycastTarget = true;
            body.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            readerScroll = viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            readerScroll.viewport = viewport;
            readerScroll.content = body;
            readerScroll.horizontal = false;
            readerScroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var pageRect = Fill("DrawingPage", viewport);
            pageImage = pageRect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            pageImage.raycastTarget = false;
            pageAspect = pageRect.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            pageAspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            var front = Button("ReadFront", reader, "Frente", new Vector2(-280, 28), new Vector2(240, 54), font, () => ShowReaderSide(false));
            var reverse = Button("ReadBack", reader, "Verso", new Vector2(0, 28), new Vector2(240, 54), font, () => ShowReaderSide(true));
            var done = Button("CloseReader", reader, "Voltar [Esc]", new Vector2(280, 28), new Vector2(240, 54), font, () => CloseReader());
            previousPage = front;
            nextPage = reverse;
            foreach (var button in new[]
            {
                front,
                reverse,
                done
            }

            )
            {
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
            }

            Hide();
        }

        public void ToggleReader()
        {
            if (!CanUse() || !displayedItem)
                return;
            if (ReaderOpen)
                CloseReader();
            else
            {
                manager.MarkReaderExamined();
                readerGroup.gameObject.SetActive(true);
                if (HasPages)
                {
                    PageIndex = 0;
                    ShowPage();
                }
                else
                    ShowReaderSide(displayedBack);
                if (EventSystem.current)
                    EventSystem.current.SetSelectedGameObject(readerGroup.transform.Find("ReadFront").gameObject);
            }
        }

        void ShowReaderSide(bool back)
        {
            if (HasPages)
            {
                PageIndex = Mathf.Clamp(PageIndex + (back ? 1 : -1), 0, displayedItem.pages.Length - 1);
                ShowPage();
                return;
            }

            pageImage.gameObject.SetActive(false);
            readerBody.gameObject.SetActive(true);
            readerScroll.enabled = true;
            previousPage.GetComponentInChildren<TMP_Text>().text = "Frente";
            nextPage.GetComponentInChildren<TMP_Text>().text = "Verso";
            previousPage.interactable = nextPage.interactable = true;
            readerBack = back;
            string text = back ? displayedItem.backText : displayedItem.frontText;
            if (!back && string.IsNullOrWhiteSpace(text))
                text = displayedItem.description;
            if (!back && !string.IsNullOrWhiteSpace(displayedItem.detailText) && !(text ?? "").Contains(displayedItem.detailText))
                text += "\n\n" + displayedItem.detailText;
            if (!back && !string.IsNullOrWhiteSpace(displayedItem.recordingText) && !(text ?? "").Contains(displayedItem.recordingText))
                text += "\n\n" + displayedItem.recordingText;
            if (back && manager.InspectionCount >= 2 && readerSanity < .4f && !string.IsNullOrWhiteSpace(displayedItem.alternateBackText))
                text = displayedItem.alternateBackText;
            readerTitle.text = displayedItem.title + (readerBack ? " — verso" : " — frente");
            readerBody.text = string.IsNullOrWhiteSpace(text) ? "Sem texto neste lado." : text;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(readerBody.rectTransform);
            readerScroll.verticalNormalizedPosition = 1;
        }

        void ShowPage()
        {
            var page = displayedItem.pages[PageIndex];
            readerScroll.enabled = false;
            readerBody.gameObject.SetActive(false);
            pageImage.gameObject.SetActive(true);
            pageImage.texture = page.image;
            pageAspect.aspectRatio = page.image ? (float)page.image.width / page.image.height : 1.3333f;
            readerTitle.text = $"{displayedItem.title} — {page.label} ({PageIndex + 1}/{displayedItem.pages.Length})";
            previousPage.GetComponentInChildren<TMP_Text>().text = "Anterior";
            nextPage.GetComponentInChildren<TMP_Text>().text = "Próximo";
            previousPage.interactable = PageIndex > 0;
            nextPage.interactable = PageIndex + 1 < displayedItem.pages.Length;
            manager.ReportPageViewed(PageIndex);
        }

        public bool CloseReader()
        {
            if (!ReaderOpen)
                return false;
            readerGroup.gameObject.SetActive(false);
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(readButton.gameObject);
            return true;
        }

        bool CanUse() => manager && manager.IsOpen && !manager.IsCompleting;
        public bool CanDragAt(Vector2 screenPoint) => !ReaderOpen && preview && group && group.interactable && RectTransformUtility.RectangleContainsScreenPoint(preview.rectTransform, screenPoint, null) && !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)readButton.transform, screenPoint, null) && !(ButtonAt(flipButton, screenPoint) || ButtonAt(playButton, screenPoint) || ButtonAt(secondTapeButton, screenPoint) || ButtonAt(closeButton, screenPoint));
        bool ButtonAt(UnityEngine.UI.Button button, Vector2 point) => chromeGroup && chromeGroup.blocksRaycasts && button && button.gameObject.activeInHierarchy && button.interactable && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, point, null);
        public void Present(InspectionItemData item, RenderTexture texture, float alpha, bool back, bool zoomDetail, float sanity01)
        {
            if (!group || !item)
                return;
            readerSanity = sanity01;
            var pixels = canvas.pixelRect;
            if (manager.CameraController && pixels.width > 0 && pixels.height > 0)
            {
                manager.CameraController.SetViewportSize(Mathf.RoundToInt(pixels.width), Mathf.RoundToInt(pixels.height));
                texture = manager.Texture;
            }

            if (displayedItem != item)
            {
                displayedItem = item;
                title.text = item.title ?? "";
                description.text = manager.InspectingRecorder ? "Selecione uma fita que já encontrou. Se ainda não tem uma, guarde o gravador e procure na sala aberta. Depois, volte pelas Anotações." : item.description ?? "";
                detail.text = item.detailText ?? "";
                flipButton.gameObject.SetActive(!manager.InspectingRecorder);
                playButton.gameObject.SetActive(item.canPlay || manager.InspectingRecorder);
                ((RectTransform)playButton.transform).anchoredPosition = new Vector2(manager.InspectingRecorder ? -282 : 0, -113);
                secondTapeButton.gameObject.SetActive(manager.InspectingRecorder);
                hintsText.text = item.canPlay || manager.InspectingRecorder ? "Arraste para girar  •  Scroll: aproximar  •  F: virar  •  R: ouvir  •  E / Esc: guardar" : "Arraste para girar  •  Scroll: aproximar  •  F: virar  •  E / Esc: guardar";
                displayedBack = !back;
                displayedDetail = !zoomDetail;
                openedAt = Time.unscaledTime;
                hintsGroup.alpha = 1;
                selectedBeforeZoom = null;
                readerGroup.gameObject.SetActive(false);
                readButton.GetComponentInChildren<TMP_Text>().text = HasPages ? "Ver desenhos [R]" : item.canPlay || manager.InspectingRecorder ? "Ler texto [T]" : "Ler texto [R]";
            }

            if (preview.texture != texture)
            {
                preview.texture = texture;
                if (texture)
                    previewAspect.aspectRatio = (float)texture.width / Mathf.Max(1, texture.height);
            }

            string backText = manager.InspectionCount >= 2 && sanity01 < .4f && !string.IsNullOrWhiteSpace(item.alternateBackText) ? item.alternateBackText : item.backText;
            bool recording = manager.RecordingPlaying;
            string displayedText = recording ? manager.RecordingCaption : backText;
            bool showText = (recording || back) && !string.IsNullOrWhiteSpace(displayedText);
            description.gameObject.SetActive(!recording && !showText);
            readerBody.fontSize = 26 * PresentationSettings.SubtitleSize;
            if (transcription.text != (displayedText ?? ""))
                transcription.text = displayedText ?? "";
            if (displayedBack != back || transcription.gameObject.activeSelf != showText)
            {
                displayedBack = back;
                transcription.gameObject.SetActive(showText);
            }

            if (displayedDetail != zoomDetail)
            {
                displayedDetail = zoomDetail;
                detail.gameObject.SetActive(zoomDetail && !string.IsNullOrWhiteSpace(item.detailText));
            }

            float expansion = Mathf.Clamp01(manager.PresentationZoom01);
            Vector2 viewport = ((RectTransform)transform).rect.size;
            if (viewport.x > 0 && viewport.y > 0)
            {
                previewArea.sizeDelta = Vector2.Lerp(new Vector2(Mathf.Min(1100, viewport.x - 64), Mathf.Min(680, viewport.y - 400)), viewport, expansion);
                previewArea.anchoredPosition = Vector2.Lerp(new Vector2(0, 90), Vector2.zero, expansion);
            }

            chromeGroup.alpha = 1 - expansion;
            bool interactive = CanUse() && alpha > .95f && chromeGroup.alpha > .05f && !ReaderOpen;
            SetChromeInteractive(interactive);
            readButton.interactable = CanUse() && alpha > .95f;
            flipButton.interactable = closeButton.interactable = interactive;
            playButton.interactable = interactive && manager.HasRecording(0);
            secondTapeButton.interactable = interactive && manager.HasRecording(1);
            bool playingFirst = recording && manager.PlayingRecordingId == "hospicio_child_tape";
            bool playingSecond = recording && manager.PlayingRecordingId == "hospicio_director_tape";
            playLabel.text = manager.InspectingRecorder ? !manager.HasRecording(0) ? "Fita A — não encontrada" : playingFirst ? "Parar fita A [R]" : "Ouvir fita A [R]" : recording ? "Parar [R]" : "Ouvir [R]";
            secondTapeLabel.text = !manager.HasRecording(1) ? "Fita B — não encontrada" : playingSecond ? "Parar fita B" : "Ouvir fita B";
            group.alpha = Mathf.Clamp01(alpha);
            group.blocksRaycasts = group.interactable = alpha > .01f;
            hintsGroup.alpha = Mathf.MoveTowards(hintsGroup.alpha, Time.unscaledTime - openedAt < 6 ? 1 : 0, Time.unscaledDeltaTime * 2);
            float stress = Mathf.Clamp01((.4f - sanity01) / .4f);
            preview.rectTransform.anchoredPosition = stress * new Vector2(Mathf.Sin(Time.unscaledTime * 17) * 1.5f, Mathf.Sin(Time.unscaledTime * 23));
        }

        void SetChromeInteractive(bool active)
        {
            if (chromeGroup.interactable == active)
                return;
            chromeGroup.blocksRaycasts = chromeGroup.interactable = active;
            var events = EventSystem.current;
            if (!events)
                return;
            if (!active && events.currentSelectedGameObject && events.currentSelectedGameObject.transform.IsChildOf(chromeGroup.transform))
            {
                selectedBeforeZoom = events.currentSelectedGameObject;
                events.SetSelectedGameObject(null);
            }
            else if (active && !events.currentSelectedGameObject)
            {
                events.SetSelectedGameObject(selectedBeforeZoom && selectedBeforeZoom.activeInHierarchy ? selectedBeforeZoom : closeButton.gameObject);
                selectedBeforeZoom = null;
            }
        }

        public void Hide()
        {
            if (!group)
                return;
            group.alpha = 0;
            group.blocksRaycasts = group.interactable = false;
            chromeGroup.alpha = 1;
            SetChromeInteractive(false);
            selectedBeforeZoom = null;
            preview.texture = null;
            preview.rectTransform.anchoredPosition = Vector2.zero;
            displayedItem = null;
            readerGroup.gameObject.SetActive(false);
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
        }

        internal static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        internal static RectTransform Fill(string name, Transform parent)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        internal static TextMeshProUGUI Text(string name, Transform parent, string value, Vector2 size, Vector2 position, TMP_FontAsset font, float pointSize)
        {
            var text = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            if (font)
                text.font = font;
            text.text = value;
            text.fontSize = pointSize;
            text.color = new Color(.92f, .91f, .84f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        internal static UnityEngine.UI.Button Button(string name, Transform parent, string value, Vector2 position, Vector2 size, TMP_FontAsset font, Action action)
        {
            var rect = Rect(name, parent, size, position);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(.08f, .095f, .085f, .98f);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = colors.selectedColor = new Color(1.4f, 1.3f, 1.1f);
            colors.pressedColor = new Color(.6f, .7f, .65f);
            button.colors = colors;
            button.onClick.AddListener(() => action());
            Text("Label", rect, value, new Vector2(size.x - 24, size.y - 4), Vector2.zero, font, 19);
            return button;
        }
    }
}
