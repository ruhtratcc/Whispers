// Controla correio, documentos, imagens e lixeira.

using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.Desktop
{
    public sealed class DesktopMailApp : DesktopApp
    {
        const string DeletedId = "email.02", RestoredFlag = "trash.email.02";
        string selectedId;
        int deliveredCount = -1, readCount = -1;
        bool restored;

        bool Visible(EmailMessage message) => message != null && Context.Inbox.State.IsDelivered(message.id)
            && (Context.Inbox.ChronologicalOfficeReading || message.id != DeletedId || Context.State.Has(RestoredFlag));

        public override void Build() => Refresh();
        public override void OnResize() => DesktopAppResize.PreserveScroll(Body, Refresh);
        public override void OnOpen(string argument)
        {
            if (!string.IsNullOrEmpty(argument)) Select(argument);
            else Refresh();
        }
        public override void OnClose()
        {
            if (!string.IsNullOrEmpty(selectedId) && Context.Inbox.CurrentMessageId == selectedId) Context.Inbox.Back();
        }
        public override void Tick(float deltaTime)
        {
            Context.Inbox.AdvanceReading(Time.unscaledDeltaTime, Window.IsFocused && !Window.Minimized
                && !string.IsNullOrEmpty(selectedId) && Context.Inbox.CurrentMessageId == selectedId);
            var state = Context.Inbox.State;
            if (state != null && (deliveredCount != state.deliveredIds.Count || readCount != state.readIds.Count
                || restored != Context.State.Has(RestoredFlag))) Refresh();
        }
        void Select(string id)
        {
            var message = Context.Inbox.catalog.Find(id);
            if (!Visible(message) || !Context.Inbox.SelectMessage(id)) return;
            selectedId = id;
            if (UnityEngine.EventSystems.EventSystem.current)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            Context.PlaySound("click");
            Refresh();
            if (id == "email.04") Context.ShowError("receipt");
        }
        void Refresh()
        {
            DesktopUI.Clear(Body);
            if (!Context.Inbox || !Context.Inbox.catalog || Context.Inbox.State == null) return;
            var state = Context.Inbox.State;
            deliveredCount = state.deliveredIds.Count;
            readCount = state.readIds.Count;
            restored = Context.State.Has(RestoredFlag);
            var messages = Context.Inbox.catalog.messages.Where(Visible)
                .OrderBy(message => DateTime.ParseExact(message.date, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture))
                .ThenBy(message => message.time, StringComparer.Ordinal).ToArray();
            int unread = messages.Count(message => !state.IsRead(message.id));
            Window.SetTitle("Email");
            float width = Body.rect.width > 0 ? Body.rect.width : 920;
            float height = Body.rect.height > 0 ? Body.rect.height : 608;
            DesktopUI.Label(Body, "Caixa de entrada", 12, 9, width - 24, 25, 17).fontStyle = FontStyles.Bold;
            DesktopInvestigationChrome.Status(Body, width, height, messages.Length + " mensagens     " + unread + " não lidas");
            float listWidth = Mathf.Round(Mathf.Clamp(width * .34f, 200, 320));
            float top = 42, regionHeight = Mathf.Max(120, height - 70), readerX = listWidth + 14;
            float readerWidth = width - readerX - 6;
            var list = DesktopInvestigationChrome.Surface(Body, "Lista de emails", 6, top, listWidth, regionHeight, Color.white);
            float rowPitch = Mathf.Min(96, Mathf.Floor((regionHeight - 6) / Mathf.Max(1, messages.Length)));
            var rows = DesktopUI.Scroll(list, 2, 2, listWidth - 4, regionHeight - 4, regionHeight - 6);
            float rowWidth = listWidth - 28;
            for (int i = 0; i < messages.Length; i++)
            {
                var message = messages[i];
                bool current = message.id == selectedId, allowed = Context.Inbox.CanSelectMessage(message.id);
                var button = DesktopUI.Button(rows, "", 0, i * rowPitch, rowWidth, rowPitch - 1, () => Select(message.id));
                button.interactable = allowed;
                var background = (Image)button.targetGraphic;
                background.sprite = null;
                background.color = current ? new Color32(0x31, 0x6A, 0xC5, 255)
                    : !allowed ? new Color(.94f, .94f, .94f) : i % 2 == 0 ? Color.white : new Color(.97f, .98f, 1);
                var sender = DesktopUI.Label(button.transform, message.sender, 8, 3, rowWidth - 16, 19, 14);
                var subject = DesktopUI.Label(button.transform, Subject(message), 8, 22, rowWidth - 16, Mathf.Min(34, rowPitch >= 60 ? rowPitch - 43 : rowPitch - 24), 14);
                var date = DesktopUI.Label(button.transform, allowed && rowPitch >= 60 ? GameClock.MailDate(message) : "", 8, rowPitch - 17, rowWidth - 16, 16, 11);
                var lockLabel = DesktopUI.Label(button.transform, allowed || rowPitch < 60 ? "" : "Leia o anterior primeiro", 8, rowPitch - 17, rowWidth - 16, 15, 11);
                sender.fontStyle = subject.fontStyle = state.IsRead(message.id) ? FontStyles.Normal : FontStyles.Bold;
                sender.textWrappingMode = date.textWrappingMode = TextWrappingModes.NoWrap;
                sender.overflowMode = subject.overflowMode = date.overflowMode = TextOverflowModes.Ellipsis;
                if (current) sender.color = subject.color = date.color = lockLabel.color = Color.white;
                else if (!allowed) sender.color = subject.color = date.color = lockLabel.color = new Color(.48f, .49f, .51f);
            }
            var reader = DesktopInvestigationChrome.Surface(Body, "Painel de leitura", readerX, top, readerWidth, regionHeight, Color.white);
            var selected = Context.Inbox.catalog.Find(selectedId);
            if (!Visible(selected))
            {
                selectedId = null;
                DesktopUI.Label(reader, "Selecione uma mensagem para ler.", 18, 24, readerWidth - 36, 54, 16);
                return;
            }
            DesktopUI.Label(reader, Subject(selected), 12, 10, readerWidth - 24, 48, 17).fontStyle = FontStyles.Bold;
            DesktopUI.Label(reader, "De: " + selected.sender, 12, 62, readerWidth - 24, 23, 14);
            DesktopUI.Label(reader, GameClock.MailDate(selected) + (string.IsNullOrEmpty(GameClock.MailTime(selected)) ? "" : "   " + GameClock.MailTime(selected)),
                12, 88, readerWidth - 24, 20, 13);
            int attachmentCount = selected.attachments?.Length ?? 0;
            float attachmentsHeight = attachmentCount > 0 ? 33 : 0;
            var paragraphs = DesktopUI.Scroll(reader, 5, 118, readerWidth - 10,
                Mathf.Max(36, regionHeight - 126 - attachmentsHeight), 1);
            float textWidth = readerWidth - 46, y = 8;
            foreach (var paragraph in selected.paragraphs ?? Array.Empty<EmailParagraph>())
            {
                if (paragraph == null) continue;
                var label = DesktopUI.Label(paragraphs, paragraph.text, 8, y, textWidth, 32, paragraph.child ? 23 : 16, paragraph.child);
                label.textWrappingMode = TextWrappingModes.Normal;
                float textHeight = Mathf.Max(25, label.GetPreferredValues(paragraph.text, textWidth, Mathf.Infinity).y + 5);
                label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);
                y += textHeight + 10;
            }
            paragraphs.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y + 8);
            float attachmentX = 8, attachmentWidth = (readerWidth - 20) / Mathf.Max(1, attachmentCount);
            foreach (var attachment in selected.attachments ?? Array.Empty<EmailAttachment>())
            {
                if (attachment == null) continue;
                var button = DesktopUI.Button(reader, "Anexo: " + attachment.fileName, attachmentX, regionHeight - 31, attachmentWidth - 5, 25, () =>
                {
                    if (attachment.unavailable || !attachment.preview) Context.ShowError("attachment");
                    else Context.OpenApp("images", attachment.fileName);
                });
                button.GetComponentInChildren<TMP_Text>().fontSize = 13;
                attachmentX += attachmentWidth;
            }
        }
        static string Subject(EmailMessage message) => string.IsNullOrEmpty(message.subject) ? "(sem assunto)" : message.subject;
    }

    public sealed class DesktopDocumentsApp : DesktopApp
    {
        string openedReport;
        int lastUnlockCount = -1, lastReadCount = -1;

        bool Visible(DesktopFile file) => file != null && (Context.State.Has(file.id)
            || string.IsNullOrEmpty(file.requiredFlag) || Context.HasFlag(file.requiredFlag));

        public override void Build() => ShowFiles();
        public override void OnResize() => DesktopAppResize.PreserveScroll(Body, ResizeLayout);

        void ResizeLayout()
        {
            var file = Context.Data.files?.FirstOrDefault(item => item != null && item.id == openedReport);
            if (openedReport != null && Visible(file)) ShowReport(file);
            else ShowFiles();
        }
        public override void OnOpen(string argument)
        {
            openedReport = null;
            if (string.IsNullOrEmpty(argument)) ShowFiles();
            else
            {
                var file = Context.Data.files?.FirstOrDefault(item => item != null && (item.id == argument || item.title == argument));
                if (Visible(file)) Open(file);
                else ShowFiles();
            }
        }

        public override void Tick(float deltaTime)
        {
            if (openedReport == null && (lastUnlockCount != Context.State.unlockedFiles.Count
                || lastReadCount != (Context.Inbox.State?.readIds.Count ?? 0))) ShowFiles();
        }

        void ShowFiles()
        {
            DesktopUI.Clear(Body);
            openedReport = null;
            Window.SetTitle("Meus Documentos");
            lastUnlockCount = Context.State.unlockedFiles.Count;
            lastReadCount = Context.Inbox.State?.readIds.Count ?? 0;
            float width = Body.rect.width > 0 ? Body.rect.width : 780;
            float height = Body.rect.height > 0 ? Body.rect.height : 480;
            DesktopInvestigationChrome.Menu(Body, width, "Arquivo     Editar     Exibir     Favoritos     Ferramentas     Ajuda");
            DesktopInvestigationChrome.Toolbar(Body, width);
            DesktopUI.Button(Body, "Atualizar", 10, 31, 100, 31, ShowFiles);
            DesktopUI.Button(Body, "Correio", 119, 31, 94, 31, () => Context.OpenApp("mail"));
            DesktopUI.Button(Body, "Imagens", 222, 31, 100, 31, () => Context.OpenApp("images"));
            DesktopInvestigationChrome.Path(Body, width, "Endereço:   Meus Documentos");
            var files = (Context.Data.files ?? Array.Empty<DesktopFile>()).Where(Visible).ToArray();
            DesktopInvestigationChrome.Status(Body, width, height, files.Length + " objetos     Arquivos locais");
            var sidebar = DesktopInvestigationChrome.Surface(Body, "Tarefas", 6, 101, 148, height - 128, new Color(.88f, .93f, 1));
            DesktopInvestigationChrome.Heading(sidebar, "Tarefas de arquivo", 148);
            DesktopUI.Button(sidebar, "Correio", 10, 45, 128, 29, () => Context.OpenApp("mail"));
            DesktopUI.Button(sidebar, "Imagens", 10, 80, 128, 29, () => Context.OpenApp("images"));
            DesktopUI.Label(sidebar, "Detalhes", 12, 147, 124, 25, 16).fontStyle = FontStyles.Bold;
            DesktopUI.Label(sidebar, "Meus Documentos\n\n" + files.Length + " arquivos disponíveis.", 12, 181, 124, 100, 14);
            float gridWidth = width - 170;
            int columns = Mathf.Max(1, Mathf.FloorToInt((gridWidth - 18) / 138));
            var content = DesktopUI.Scroll(Body, 162, 101, gridWidth, height - 128,
                Mathf.Max(height - 128, Mathf.CeilToInt((float)files.Length / columns) * 126));
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var button = DesktopUI.Button(content, file.title, 6 + i % columns * 138, 8 + i / columns * 126, 130, 116, () => Open(file));
                ((Image)button.targetGraphic).color = new Color(1, 1, 1, .12f);
                var label = button.GetComponentInChildren<TMP_Text>();
                label.rectTransform.anchoredPosition = new Vector2(4, -72);
                label.rectTransform.sizeDelta = new Vector2(122, 40);
                label.fontSize = 12; label.alignment = TextAlignmentOptions.Top;label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;
                DesktopInvestigationChrome.Icon(Context, button.transform, file.kind == DesktopFileKind.Image ? "images" : "documents", 40, 12, 48);
            }
            if (files.Length == 0) DesktopUI.Label(content, "Esta pasta está vazia.", 12, 14, gridWidth - 32, 32, 16);
        }

        void Open(DesktopFile file)
        {
            if (!Visible(file)) return;
            Context.PlaySound("hd");
            if (file.kind == DesktopFileKind.LockedCase)
            {
                Context.Host.CloseApp("documents");
                Context.ShowError("case");
                return;
            }
            Context.Unlock(file.id);
            if (file.kind == DesktopFileKind.Image) { Context.OpenApp("images", file.id); return; }
            if (string.IsNullOrEmpty(file.text)) { Context.ShowError("attachment"); return; }
            ShowReport(file);
        }

        void ShowReport(DesktopFile file)
        {
            openedReport = file.id;
            DesktopUI.Clear(Body);
            Window.SetTitle(file.title);
            float width = Body.rect.width > 0 ? Body.rect.width : 780;
            float height = Body.rect.height > 0 ? Body.rect.height : 480;
            DesktopInvestigationChrome.Menu(Body, width, "Arquivo     Editar     Exibir     Documento     Ajuda");
            DesktopInvestigationChrome.Toolbar(Body, width);
            DesktopUI.Button(Body, "Voltar à pasta", 10, 31, 150, 31, ShowFiles);
            DesktopUI.Label(Body, file.title, 180, 36, width - 194, 25, 16).fontStyle = FontStyles.Bold;
            DesktopInvestigationChrome.Status(Body, width, height, "Documento local     Somente leitura");
            var content = DesktopUI.Scroll(Body, 8, 78, width - 16, height - 105, 1);
            var text = DesktopUI.Label(content, file.text, 24, 18, width - 88, 40, 16);
            text.textWrappingMode = TextWrappingModes.Normal;
            float textHeight = Mathf.Max(height - 126, text.GetPreferredValues(file.text, width - 88, Mathf.Infinity).y + 20);
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight + 30);
        }
    }

    public sealed class DesktopImagesApp : DesktopApp
    {
        Texture2D displayedTexture;
        string displayedTitle;
        bool displayingImage;

        public override void OnResize()
        {
            if (displayingImage) Show(displayedTexture, displayedTitle);
            else Build();
        }

        public override void Build()
        {
            displayingImage = false;
            DesktopUI.Clear(Body);
            Window.SetTitle("Visualizador de imagens");
            float width = Body.rect.width > 0 ? Body.rect.width : 780;
            float height = Body.rect.height > 0 ? Body.rect.height : 480;
            DrawToolbar(width, "Imagens locais");
            var files = ImageFiles();
            int columns = Mathf.Max(1, Mathf.FloorToInt((width - 30) / 154));
            var content = DesktopUI.Scroll(Body, 8, 101, width - 16, height - 128,
                Mathf.Max(height - 128, Mathf.CeilToInt((float)files.Length / columns) * 138));
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var button = DesktopUI.Button(content, file.title, 8 + i % columns * 154, 8 + i / columns * 138,
                    144, 130, () => Context.OpenApp("images", file.id));
                var label = button.GetComponentInChildren<TMP_Text>();
                label.rectTransform.anchoredPosition = new Vector2(5, -101);
                label.rectTransform.sizeDelta = new Vector2(134, 26);
                label.fontSize = 13;
                Photo(DesktopUI.Rect(button.transform, "Prévia", 8, 7, 128, 88), file.texture, "Miniatura");
            }
            if (files.Length == 0)
                DesktopUI.Label(content, "Abra uma foto em Meus Documentos ou nos anexos do correio.", 18, 28, width - 64, 70, 16);
            DesktopInvestigationChrome.Status(Body, width, height, files.Length + " imagens disponíveis");
        }

        public override void OnOpen(string argument)
        {
            if (string.IsNullOrEmpty(argument)) { Build(); return; }
            var file = Context.Data.files?.FirstOrDefault(item => item != null && item.kind == DesktopFileKind.Image
                && (string.Equals(item.id, argument, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.title, argument, StringComparison.OrdinalIgnoreCase)));
            if (file != null && (Context.State.Has(file.id) || string.IsNullOrEmpty(file.requiredFlag) || Context.HasFlag(file.requiredFlag)))
            {
                Show(file.texture, file.title);
                if (file.texture) Context.Unlock(file.id);
                return;
            }
            foreach (var email in Context.Inbox.catalog.messages)
            {
                if (!Context.Inbox.State.IsDelivered(email.id)) continue;
                var attachment = email.attachments?.FirstOrDefault(item => item != null
                    && string.Equals(item.fileName, argument, StringComparison.OrdinalIgnoreCase));
                if (attachment == null) continue;
                if (attachment.unavailable) { Context.ShowError("attachment"); return; }
                Show(attachment.preview, attachment.fileName);
                return;
            }
            Build();
            Context.ShowError("attachment");
        }

        public void Show(Texture2D texture, string title)
        {
            displayedTexture = texture;
            displayedTitle = title;
            displayingImage = true;
            DesktopUI.Clear(Body);
            Window.SetTitle("Visualizador — " + title);
            float width = Body.rect.width > 0 ? Body.rect.width : 780;
            float height = Body.rect.height > 0 ? Body.rect.height : 480;
            DrawToolbar(width, title);
            var others = ImageFiles().Where(file => file.texture != texture)
                .Take(Mathf.Max(1, Mathf.FloorToInt((width - 24) / 100))).ToArray();
            var area = DesktopInvestigationChrome.Surface(Body, "Fotografia", 8, 101, width - 16,
                height - (others.Length > 0 ? 209 : 128), new Color(.92f, .93f, .91f));
            if (!texture)
            {
                DesktopUI.Label(area, "[Imagem indisponível] " + title, 18, 24, width - 56, 70, 16);
                return;
            }
            Photo(area, texture, "Imagem");
            for (int i = 0; i < others.Length; i++)
            {
                var file = others[i];
                var button = DesktopUI.Button(Body, file.title, 12 + i * 100, height - 101, 94, 73,
                    () => Context.OpenApp("images", file.id));
                var label = button.GetComponentInChildren<TMP_Text>();
                label.rectTransform.anchoredPosition = new Vector2(3, -52);
                label.rectTransform.sizeDelta = new Vector2(88, 19);
                label.fontSize = 12; label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                Photo(DesktopUI.Rect(button.transform, "Prévia", 5, 4, 84, 45), file.texture, "Miniatura");
            }
            DesktopInvestigationChrome.Status(Body, width, height, texture.width + " × " + texture.height + " pixels     " + title);
        }

        DesktopFile[] ImageFiles() => (Context.Data.files ?? Array.Empty<DesktopFile>()).Where(file => file != null
            && file.kind == DesktopFileKind.Image && file.texture && (Context.State.Has(file.id)
                || string.IsNullOrEmpty(file.requiredFlag) || Context.HasFlag(file.requiredFlag)))
            .GroupBy(file => file.texture).Select(group => group.First()).ToArray();

        void DrawToolbar(float width, string title)
        {
            DesktopInvestigationChrome.Menu(Body, width, "Arquivo     Editar     Exibir     Imagem     Ajuda");
            DesktopInvestigationChrome.Toolbar(Body, width);
            DesktopUI.Button(Body, "Meus Documentos", 10, 31, 150, 31, () => Context.OpenApp("documents"));
            DesktopUI.Button(Body, "Miniaturas", 169, 31, 112, 31, Build);
            DesktopUI.Button(Body, "Correio", 290, 31, 94, 31, () => Context.OpenApp("mail"));
            DesktopInvestigationChrome.Path(Body, width, title);
        }

        static void Photo(RectTransform area, Texture2D texture, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(area, false);
            rect.gameObject.layer = area.gameObject.layer;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8, 8);
            rect.offsetMax = new Vector2(-8, -8);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            var aspect = rect.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = (float)texture.width / Mathf.Max(1, texture.height);
        }
    }

    public sealed class DesktopTrashApp : DesktopApp
    {
        const string MessageId = "email.02", RestoredFlag = "trash.email.02";
        bool restored;
        public override void Build() => Refresh();
        public override void OnResize() => Refresh();
        public override void OnOpen(string argument) => Refresh();
        public override void Tick(float deltaTime)
        {
            if (restored != Context.State.Has(RestoredFlag)) Refresh();
        }

        void Refresh()
        {
            DesktopUI.Clear(Body);
            Window.SetTitle("Lixeira");
            restored = Context.State.Has(RestoredFlag);
            float width = Body.rect.width > 0 ? Body.rect.width : 780;
            float height = Body.rect.height > 0 ? Body.rect.height : 480;
            var message = Context.Inbox.catalog.Find(MessageId);
            DesktopInvestigationChrome.Menu(Body, width, "Arquivo     Editar     Exibir     Ferramentas     Ajuda");
            DesktopInvestigationChrome.Toolbar(Body, width);
            DesktopUI.Button(Body, "Correio", 10, 31, 94, 31, () => Context.OpenApp("mail"));
            DesktopUI.Button(Body, "Atualizar", 113, 31, 100, 31, Refresh);
            DesktopInvestigationChrome.Path(Body, width, "Endereço:   Lixeira");
            DesktopInvestigationChrome.Status(Body, width, height, restored ? "0 objetos" : "1 mensagem removida");
            var tasks = DesktopInvestigationChrome.Surface(Body, "Tarefas da Lixeira", 6, 101, 182, height - 128, new Color(.88f, .93f, 1));
            DesktopInvestigationChrome.Heading(tasks, "Tarefas da Lixeira", 182);
            DesktopUI.Label(tasks, "Os itens restaurados voltam para a pasta de origem.", 13, 47, 156, 90, 14);
            if (restored || message == null || !Context.Inbox.State.IsDelivered(MessageId))
            {
                DesktopUI.Label(Body, "A Lixeira está vazia.", 210, 127, width - 236, 40, 16);
                return;
            }
            DesktopUI.Button(tasks, "Restaurar mensagem", 10, 155, 162, 34, Restore)
                .GetComponentInChildren<TMP_Text>().fontSize = 14;
            float detailsWidth = width - 204;
            var details = DesktopInvestigationChrome.Surface(Body, "Itens removidos", 196, 101, detailsWidth, height - 128, Color.white);
            DesktopInvestigationChrome.Surface(details, "Cabeçalhos", 3, 3, detailsWidth - 6, 27, new Color(.89f, .90f, .86f));
            DesktopUI.Label(details, "Nome", 12, 8, detailsWidth - 188, 22, 13);
            DesktopUI.Label(details, "Data", detailsWidth - 118, 8, 110, 22, 13);
            DesktopInvestigationChrome.Icon(Context, details, "mail", 12, 43, 22);
            DesktopUI.Label(details, message.subject, 44, 44, detailsWidth - 176, 28, 15);
            DesktopUI.Label(details, GameClock.MailDate(message), detailsWidth - 118, 44, 110, 28, 14);
            DesktopUI.Label(details, "Origem: Caixa de entrada\nRemetente: " + message.sender, 15, 105, detailsWidth - 30, 65, 16);
        }

        void Restore()
        {
            // Delivery remains canonical; only this desktop folder presentation changes.
            if (!Context.Inbox.SelectMessage(MessageId))
            {
                Context.Notify("Não foi possível restaurar a mensagem.");
                return;
            }
            if (UnityEngine.EventSystems.EventSystem.current)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            Context.Unlock(RestoredFlag);
            Context.PlaySound("notification");
            Refresh();
            Context.OpenApp("mail", MessageId);
        }
    }

    static class DesktopInvestigationChrome
    {
        public static RectTransform Surface(Transform parent, string name, float x, float y, float width, float height, Color tint)
        {
            var rect = DesktopUI.Panel(parent, name, x, y, width, height);
            rect.GetComponent<Image>().color = tint;
            return rect;
        }

        public static void Menu(RectTransform body, float width, string text)
        {
            Surface(body, "Barra de menus", 0, 0, width, 25, new Color(.96f, .95f, .90f));
            DesktopUI.Label(body, text, 10, 4, width - 20, 21, 14);
        }

        public static void Toolbar(RectTransform body, float width) =>
            Surface(body, "Barra de ferramentas", 0, 25, width, 43, new Color(.95f, .95f, .91f));

        public static void Path(RectTransform body, float width, string path)
        {
            Surface(body, "Endereço da pasta", 6, 72, width - 12, 25, Color.white);
            DesktopUI.Label(body, path, 14, 76, width - 28, 21, 14);
        }

        public static void Heading(RectTransform parent, string text, float width)
        {
            Surface(parent, "Cabeçalho da pasta", 3, 3, width - 6, 28, new Color(.23f, .40f, .72f));
            DesktopUI.Label(parent, text, 10, 8, width - 20, 23, 14).color = Color.white;
        }

        public static void Status(RectTransform body, float width, float height, string text)
        {
            Surface(body, "Barra de status", 0, height - 23, width, 23, new Color(.94f, .94f, .90f));
            DesktopUI.Label(body, text, 10, height - 20, width - 20, 20, 13);
        }

        public static void Icon(DesktopContext context, Transform parent, string appId, float x, float y, float size)
        {
            var icon = context.Data.FindApp(appId)?.icon;
            if (icon) DesktopUI.Image(parent, icon, x, y, size, size);
        }
    }

    static class DesktopAppResize
    {
        public static void PreserveScroll(RectTransform body, Action rebuild)
        {
            var positions = body.GetComponentsInChildren<ScrollRect>()
                .Select(scroll => scroll.verticalNormalizedPosition).ToArray();
            rebuild();
            Canvas.ForceUpdateCanvases();
            var current = body.GetComponentsInChildren<ScrollRect>();
            for (int i = 0; i < Mathf.Min(positions.Length, current.Length); i++)
                current[i].verticalNormalizedPosition = positions[i];
        }
    }
}
