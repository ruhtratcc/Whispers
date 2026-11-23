// Entrega, registra leituras e comunica mudanças ao jogo.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace HorrorGame
{
    public enum EmailDevice { Desktop, Phone }

    public sealed class EmailInboxController : MonoBehaviour
    {
        public EmailCatalog catalog;
        public TMP_FontAsset normalFont, childFont;
        public GameSession game;
        public bool openOnStart;
        [Min(.1f)] public float minimumReadingSeconds = 2.5f;
        public bool enforceChronologicalReading = true;
        public UnityEvent<string> onReadFlag = new UnityEvent<string>();
        public event Action<string> ReadFlagRaised;
        public event Action<string> MessageDelivered;
        public event Action<string> ReadingCompleted;
        public event Action<string> ReadingAbandoned;
        public EmailInboxState State { get; private set; }
        public bool IsOpen => view && view.gameObject.activeSelf;
        public bool HasPendingSave { get; private set; }
        public bool CanRetrySave => HasPendingSave && store != null && !storageReadFailed;
        public string PersistenceError { get; private set; } = "";
        public string CurrentMessageId { get; private set; }
        public bool ExternalKeyboardInput { get; set; }
        public float ReadingSecondsElapsed { get; private set; }
        public bool ChronologicalOfficeReading => enforceChronologicalReading && catalog && catalog.Find("email.01") != null;
        public int UnreadCount => catalog && State != null ? Array.FindAll(catalog.messages,
            m => State.IsDelivered(m.id) && !State.IsRead(m.id)).Length : 0;
        public EmailInboxView View => view;
        EmailInboxView view;
        EmailProgressStore store;
        bool storageReadFailed, ownsInspection, sessionOpen, cursorVisible;
        CursorLockMode cursorLock;
        int openedFrame;

        // An explicit new-story path is required; this never opens the existing campaign save.
        public void Configure(EmailCatalog data, string progressPath = null)
        {
            if (IsOpen) throw new InvalidOperationException("Close the inbox before changing its storage.");
            if (!data || data.ValidateContent().Length != 0) throw new ArgumentException("Invalid email catalog.");
            catalog = data; State = EmailInboxState.Create(data); store = null;
            HasPendingSave = storageReadFailed = false; PersistenceError = "";
            if (!string.IsNullOrEmpty(progressPath))
            {
                store = new EmailProgressStore(progressPath);
                if (store.TryLoad(catalog, out var restored, out var error)) State = restored;
                else { storageReadFailed = true; PersistenceError = error; }
            }
        }
        void Start() { if (openOnStart) Open(EmailDevice.Desktop); }
        public bool Open(EmailDevice device)
        {
            if (sessionOpen || !catalog) return false;
            if (State == null) Configure(catalog);
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            if (game && !game.BeginInspection()) return false;
            ownsInspection = game;
            if (!view)
            {
                var root = new GameObject("EmailInbox", typeof(RectTransform));
                root.transform.SetParent(transform, false);
                view = root.AddComponent<EmailInboxView>();
                view.Initialize(this, normalFont, childFont);
            }
            CurrentMessageId = ""; openedFrame = Time.frameCount; sessionOpen = true;
            view.gameObject.SetActive(true); view.Present(device);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            return true;
        }
        public bool SelectMessage(string id)
        {
            if (!IsOpen || !CanSelectMessage(id)) return false;
            var message = catalog.Find(id); if (message == null) return false;
            if (CurrentMessageId != id) { AbandonReading(); ReadingSecondsElapsed = 0; }
            CurrentMessageId = id;
            view.ShowMessage(message); view.RefreshList();
            view.FocusBack();
            return true;
        }
        public bool CanSelectMessage(string id)
        {
            if (State == null || !catalog || !State.IsDelivered(id) || catalog.Find(id) == null) return false;
            if (State.IsRead(id) || !ChronologicalOfficeReading) return true;
            int number = OriginalMailNumber(id);
            for (int i = 1; i < number; i++)
                if (catalog.Find("email.0" + i) != null && !State.IsRead("email.0" + i)) return false;
            return true;
        }
        static int OriginalMailNumber(string id) => id != null && id.Length == 8 && id.StartsWith("email.0", StringComparison.Ordinal)
            && id[7] >= '1' && id[7] <= '7' ? id[7] - '0' : 0;
        // External desktop readers advance only while their actual message window is visible.
        public bool AdvanceReading(float seconds, bool visible = true)
        {
            if (!visible || !IsOpen || string.IsNullOrEmpty(CurrentMessageId) || State == null
                || State.IsRead(CurrentMessageId) || !float.IsFinite(seconds) || seconds <= 0
                || game && game.State != GameState.Inspecting) return false;
            ReadingSecondsElapsed += seconds;
            if (ReadingSecondsElapsed < Mathf.Max(.1f, minimumReadingSeconds)) return false;
            string id = CurrentMessageId;
            if (!State.MarkRead(catalog, id)) return false;
            SaveChangedState();
            string flag = id + ".read";
            ReadFlagRaised?.Invoke(flag); onReadFlag.Invoke(flag);
            ReadingCompleted?.Invoke(id);
            if (IsOpen) view.RefreshList();
            return true;
        }
        void AbandonReading()
        {
            if (ReadingSecondsElapsed > 0 && !string.IsNullOrEmpty(CurrentMessageId) && State != null && !State.IsRead(CurrentMessageId))
                ReadingAbandoned?.Invoke(CurrentMessageId);
        }
        public bool Deliver(string id)
        {
            if (State == null) { if (!catalog) return false; Configure(catalog); }
            if (!State.Deliver(catalog, id)) return false;
            SaveChangedState(); MessageDelivered?.Invoke(id);
            if (IsOpen) view.RefreshList();
            return true;
        }
        void SaveChangedState()
        {
            if (store == null) return;
            HasPendingSave = true;
            RetryPendingSave();
        }
        public bool RetryPendingSave()
        {
            if (!HasPendingSave || store == null || storageReadFailed) return false;
            if (!store.TrySave(State, catalog, out var error)) PersistenceError = error;
            else { HasPendingSave = false; PersistenceError = ""; }
            if (IsOpen) view.RefreshStatus();
            return !HasPendingSave;
        }
        public EmailInboxState CaptureState() => State?.Copy();
        public bool RestoreState(EmailInboxState state)
        {
            if (state == null || !state.Validate(catalog)) return false;
            State = state.Copy();
            ReadingSecondsElapsed = 0;
            if (IsOpen) { CurrentMessageId = ""; view.ShowInbox(); view.RefreshList(); }
            return true;
        }
        public void Back()
        {
            if (!IsOpen) return;
            if (view.AttachmentOpen) { view.CloseAttachment(); return; }
            if (!string.IsNullOrEmpty(CurrentMessageId)) { AbandonReading(); ReadingSecondsElapsed = 0; CurrentMessageId = ""; view.ShowInbox(); return; }
            Close();
        }
        public void Close()
        {
            if (!sessionOpen && !ownsInspection) return;
            AbandonReading(); ReadingSecondsElapsed = 0;
            bool releaseInspection = ownsInspection; ownsInspection = false; sessionOpen = false;
            CurrentMessageId = ""; if (view) view.gameObject.SetActive(false);
            if (releaseInspection && game) game.EndInspection();
            else { Cursor.lockState = cursorLock; Cursor.visible = cursorVisible; }
        }
        void Update()
        {
            if (ExternalKeyboardInput || !IsOpen || Time.frameCount <= openedFrame || (game && game.State == GameState.Paused)) return;
            if (game && ownsInspection && game.State != GameState.Inspecting) { Close(); return; }
            AdvanceReading(Time.unscaledDeltaTime, !view.AttachmentOpen);
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
        }
        void OnDisable() => Close();
    }
}
