// Exibe páginas locais da investigação no navegador fictício.

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.Desktop
{
    public sealed class DesktopBrowserApp : DesktopApp
    {
        readonly List<string> history = new List<string>();
        int index = -1;
        TMP_InputField address;
        RectTransform pageArea;
        Button back, forward;
        string visibleMailId;
        public string CurrentAddress => index < 0 ? "" : history[index];
        public int HistoryCount => history.Count;

        public override void Build()
        {
            float w = Body.rect.width, h = Body.rect.height;
            back = DesktopUI.Button(Body, "<", 8, 8, 40, 30, () => Step(-1));
            forward = DesktopUI.Button(Body, ">", 54, 8, 40, 30, () => Step(1));
            address = DesktopUI.Entry(Body, DesktopSystemText.SearchHome, 100, 8, w - 170, 30, v => Navigate(v));
            DesktopUI.Button(Body, DesktopSystemText.Go, w - 62, 8, 54, 30, () => Navigate(address.text));
            string[] labels =
            {
                DesktopSystemText.SearchName,
                DesktopSystemText.WebMailName,
                DesktopSystemText.News,
                DesktopSystemText.Demolition
            };
            string[] routes =
            {
                DesktopSystemText.SearchHome,
                DesktopSystemText.MailHome,
                "seepe.local/arquivo/quatro-alunos",
                "seepe.local/demolicao"
            };
            for (int i = 0; i < 4; i++)
            {
                string route = routes[i];
                DesktopUI.Button(Body, labels[i], 8 + i * 175, 45, 168, 30, () => Navigate(route));
            }

            pageArea = DesktopUI.Panel(Body, "Página", 8, 83, w - 16, h - 91);
        }

        public override void OnOpen(string arg)
        {
            if (!string.IsNullOrEmpty(arg))
                Navigate(arg);
            else if (index < 0)
                Navigate(DesktopSystemText.SearchHome);
            else
                Display();
        }

        public override void OnResize()
        {
            DesktopUI.Clear(Body);
            Build();
            Display();
        }

        public bool Navigate(string value)
        {
            string route = NormalizeAddress(value);
            if (!Known(route))
            {
                Context.ShowError("disk");
                return false;
            }

            if (CurrentAddress != route)
            {
                if (index + 1 < history.Count)
                    history.RemoveRange(index + 1, history.Count - index - 1);
                history.Add(route);
                index = history.Count - 1;
            }

            Display();
            return true;
        }

        public bool Step(int direction)
        {
            int next = index + Math.Sign(direction);
            if (next < 0 || next >= history.Count)
                return false;
            index = next;
            Display();
            return true;
        }

        public static string NormalizeAddress(string s)
        {
            s = (s ?? "").Trim().ToLowerInvariant();
            if (s.StartsWith("https://"))
                s = s.Substring(8);
            else if (s.StartsWith("http://"))
                s = s.Substring(7);
            return s.Replace("ceep.local", "seepe.local").TrimEnd('/');
        }

        bool Known(string route) => route == DesktopSystemText.SearchHome || route == DesktopSystemText.MailHome || route.StartsWith(DesktopSystemText.SearchHome + "/busca?") || Context.Data.pages.Any(p => p && NormalizeAddress(p.address) == route);
        void Display()
        {
            visibleMailId = null;
            if (index < 0)
                return;
            address.SetTextWithoutNotify(CurrentAddress);
            back.interactable = index > 0;
            forward.interactable = index + 1 < history.Count;
            Window.SetTitle(DesktopSystemText.BrowserName);
            DesktopUI.Clear(pageArea);
            float w = pageArea.rect.width;
            if (CurrentAddress == DesktopSystemText.SearchHome || CurrentAddress == "seepe.local/inicio")
            {
                DesktopUI.Label(pageArea, DesktopSystemText.SearchName, 30, 30, w - 60, 60, 32);
                var search = DesktopUI.Entry(pageArea, "", 30, 110, w - 200, 34, q => Navigate(DesktopSystemText.SearchHome + "/busca?q=" + Uri.EscapeDataString(q)));
                DesktopUI.Button(pageArea, DesktopSystemText.Search, w - 160, 110, 130, 34, () => Navigate(DesktopSystemText.SearchHome + "/busca?q=" + Uri.EscapeDataString(search.text)));
                DesktopUI.Label(pageArea, DesktopSystemText.SearchNotice, 30, 165, w - 60, 40, 14);
                return;
            }

            if (CurrentAddress.StartsWith(DesktopSystemText.SearchHome + "/busca?"))
            {
                string q = Uri.UnescapeDataString(CurrentAddress.Substring(CurrentAddress.IndexOf("q=") + 2));
                DesktopUI.Label(pageArea, DesktopSystemText.SearchResults, 20, 15, w - 40, 40, 18);
                int y = 65;
                foreach (var p in Context.Data.pages.Where(p => p && (string.IsNullOrWhiteSpace(q) || p.title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || string.Join(" ", p.paragraphs.Select(t => t.text)).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    var page = p;
                    DesktopUI.Button(pageArea, page.title, 20, y, w - 40, 40, () => Navigate(page.address));
                    y += 48;
                }

                if (y == 65)
                    DesktopUI.Label(pageArea, DesktopSystemText.NoResults, 20, 65, w - 40, 40);
                return;
            }

            if (CurrentAddress == DesktopSystemText.MailHome)
            {
                DesktopUI.Label(pageArea, DesktopSystemText.WebMailName + " — " + DesktopSystemText.Inbox, 15, 10, w - 30, 35, 22);
                int y = 55;
                foreach (var m in Context.Inbox.catalog.messages.Where(m => Context.Inbox.State.IsDelivered(m.id) && (Context.Inbox.enforceChronologicalReading || m.id != "email.02" || Context.State.Has("trash.email.02"))))
                {
                    var mail = m;
                    DesktopUI.Button(pageArea, mail.subject, 15, y, w - 30, 32, () => Read(mail));
                    y += 36;
                }

                return;
            }

            var entry = Context.Data.pages.FirstOrDefault(p => p && NormalizeAddress(p.address) == CurrentAddress);
            if (entry)
            {
                float top = 15;
                if (entry.image)
                {
                    float height = Mathf.Min(150, pageArea.rect.height * .3f), width = height * entry.image.width / entry.image.height;
                    var photo = DesktopUI.Rect(pageArea, "Foto do arquivo", 15, 15, width, height).gameObject.AddComponent<RawImage>();
                    photo.texture = entry.image;
                    photo.raycastTarget = false;
                    top += height + 10;
                }

                DesktopParagraphs.Draw(pageArea, entry.paragraphs, 15, top, w - 30, pageArea.rect.height - top - 15);
            }
        }

        public override void Tick(float dt)
        {
            if (Window.IsFocused && !Window.Minimized && !string.IsNullOrEmpty(visibleMailId) && Context.Inbox.CurrentMessageId == visibleMailId)
                Context.Inbox.AdvanceReading(dt, true);
        }

        void Read(EmailMessage mail)
        {
            if (!Context.Inbox.SelectMessage(mail.id))
                return;
            visibleMailId = mail.id;
            DesktopUI.Clear(pageArea);
            float w = pageArea.rect.width;
            DesktopUI.Button(pageArea, DesktopSystemText.Inbox, 12, 8, 180, 30, Display);
            DesktopUI.Label(pageArea, mail.subject + "\n" + mail.sender + " — " + GameClock.MailDate(mail) + " " + GameClock.MailTime(mail), 12, 45, w - 24, 70, 17);
            DesktopParagraphs.Draw(pageArea, mail.paragraphs, 12, 120, w - 24, pageArea.rect.height - 170);
            int y = Mathf.RoundToInt(pageArea.rect.height - 40);
            foreach (var a in mail.attachments ?? Array.Empty<EmailAttachment>())
            {
                var attachment = a;
                DesktopUI.Button(pageArea, a.fileName, 12, y, w - 24, 30, () =>
                {
                    if (attachment.unavailable || !attachment.preview)
                        Context.ShowError("attachment");
                    else
                        Context.OpenApp("images", attachment.fileName);
                });
                y -= 34;
            }

            if (mail.id == "email.04")
                Context.ShowError("receipt");
        }
    }
}
