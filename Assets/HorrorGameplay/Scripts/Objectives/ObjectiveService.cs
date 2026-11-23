// Atualiza a tarefa atual conforme o capítulo e o progresso.

using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame
{
    /// <summary>Presentation derived from the existing inventory/milestones; no second progress store.</summary>
    [DefaultExecutionOrder(480), DisallowMultipleComponent]
    public sealed class ObjectiveService : MonoBehaviour
    {
        public GameSession game;
        public ChapterObjectives[] chapters = Array.Empty<ChapterObjectives>();
        public SoundSet completedSound;
        public Vector2 margin = new Vector2(42, 48);
        [Min(180)]
        public float width = 500;
        public string CurrentTitle { get; private set; } = "";
        public string CurrentObjectiveId { get; private set; } = "";
        public float LastProgressTime { get; private set; }
        public bool HasTarget => current != null && current.hasTarget;
        public Vector3 TargetPosition => HasTarget ? current.targetPosition : game && game.player ? game.player.transform.position : Vector3.zero;
        public int CompletedSubitemCount { get; private set; }
        public int SubitemCount => checkedItems.Length;
        public ObjectiveEntry CurrentEntry => current;

        public event Action ProgressChanged;
        public bool Visible => isActiveAndEnabled && ObjectivePreferences.ShowObjectives && game && game.CanControl && !game.IsBlackingOut && (!director || !director.SequencePlaying) && !string.IsNullOrWhiteSpace(CurrentTitle);
        public CanvasGroup View => group;

        NarrativeDirector director;
        Inventory inventory;
        PhoneProgression phoneProgression;
        ObjectiveEntry current;
        CanvasGroup group;
        RectTransform panel, list;
        TMP_Text title;
        TMP_Text[] labels = Array.Empty<TMP_Text>();
        Image[] checks = Array.Empty<Image>();
        bool[] checkedItems = Array.Empty<bool>();
        float[] checkProgress = Array.Empty<float>();
        bool dirty = true, initialized, syncedJournal, currentComplete;
        string fallbackTitle = "", lastWrittenTitle, lastChapter, lastNext, previousTitle, journalWritten;
        float highlightedUntil, transitionUntil, lastScale = -1;
        Color normal = new Color(.86f, .92f, .94f, .86f), muted = new Color(.64f, .70f, .72f, .82f);
        void Awake()
        {
            if (!game)
                game = GetComponent<GameSession>();
            if (!game)
                game = GetComponentInParent<GameSession>();
            phoneProgression = PhoneProgression.For(game);
            LastProgressTime = Time.unscaledTime;
        }

        void OnEnable()
        {
            Bind();
            if (phoneProgression)
                phoneProgression.ProgressChanged += Changed;
            dirty = true;
        }

        void OnDisable()
        {
            Unbind();
            if (phoneProgression)
                phoneProgression.ProgressChanged -= Changed;
            if (group)
                group.alpha = 0;
            if (game && game.hud && game.hud.objectiveGroup)
                game.hud.objectiveGroup.alpha = 0;
        }

        void OnDestroy()
        {
            if (panel)
                Destroy(panel.gameObject);
        }

        void Bind()
        {
            var nextDirector = game ? game.narrative : null;
            var nextInventory = game && game.interaction ? game.interaction.inventory : null;
            if (nextDirector == director && nextInventory == inventory)
                return;
            Unbind();
            director = nextDirector;
            inventory = nextInventory;
            if (director)
            {
                director.SignalAccepted += SignalChanged;
                director.SequenceCompleted += SignalChanged;
                director.ChapterInitialized += SignalChanged;
                director.Initialized += Changed;
            }

            if (inventory)
                inventory.Changed += Changed;
            dirty = true;
        }

        void Unbind()
        {
            if (director)
            {
                director.SignalAccepted -= SignalChanged;
                director.SequenceCompleted -= SignalChanged;
                director.ChapterInitialized -= SignalChanged;
                director.Initialized -= Changed;
            }

            if (inventory)
                inventory.Changed -= Changed;
            director = null;
            inventory = null;
        }

        void SignalChanged(string value) => Changed();
        void Changed()
        {
            dirty = true;
        }

        void LateUpdate()
        {
            if (!game || !game.hud)
                return;
            Bind();
            EnsureView();
            string chapter = director && director.Progress != null ? director.Progress.chapterId : "";
            string next = director ? director.NextSignal : "";
            string raw = game.hud.objective ? game.hud.objective.text : "";
            if (raw != lastWrittenTitle)
            {
                fallbackTitle = raw ?? "";
                dirty = true;
            }

            if (chapter != lastChapter || next != lastNext)
            {
                lastChapter = chapter;
                lastNext = next;
                dirty = true;
            }

            if (dirty)
                RefreshNow();
            if (game.hud.objectiveGroup)
                game.hud.objectiveGroup.alpha = 0;
            if (game.hud.objective && game.hud.objective.text != CurrentTitle)
                game.hud.objective.text = CurrentTitle;
            lastWrittenTitle = CurrentTitle;
            if (group)
            {
                group.alpha = Visible ? 1 : 0;
                group.blocksRaycasts = group.interactable = false;
            }

            AdvanceView();
            SyncJournal();
        }

        ObjectiveEntry SelectEntry()
        {
            if (!director || !director.IsInitialized)
                return null;
            foreach (var chapter in chapters)
            {
                if (!chapter || chapter.chapterId != director.Progress.chapterId || chapter.entries == null)
                    continue;
                ObjectiveEntry completed = null;
                foreach (var entry in chapter.entries)
                {
                    if (entry == null || !entry.Matches(director))
                        continue;
                    if (!entry.IsComplete(inventory, director))
                        return entry;
                    completed = entry;
                }

                if (completed != null && !string.IsNullOrEmpty(completed.nextObjectiveId))
                    foreach (var entry in chapter.entries)
                        if (entry != null && entry.id == completed.nextObjectiveId && entry.Matches(director))
                            return entry;
                return completed;
            }

            return null;
        }

        public bool IsSubitemComplete(int index) => index >= 0 && index < checkedItems.Length && checkedItems[index];
        public void RefreshNow(bool silent = false)
        {
            Bind();
            EnsureView();
            if (game && game.hud && game.hud.objective && game.hud.objective.text != lastWrittenTitle)
                fallbackTitle = game.hud.objective.text ?? "";
            var entry = SelectEntry();
            bool newEntry = !ReferenceEquals(entry, current);
            string value = entry != null ? entry.title ?? "" : fallbackTitle;
            if (phoneProgression && !string.IsNullOrEmpty(phoneProgression.PendingObjective))
                value = phoneProgression.PendingObjective;
            bool changed = newEntry || value != CurrentTitle;
            if (newEntry)
            {
                if (initialized && current != null && current.IsComplete(inventory, director) && !PresentationSettings.ReduceEffects)
                {
                    previousTitle = CurrentTitle;
                    transitionUntil = Time.unscaledTime + .65f;
                }

                current = entry;
                BuildRows();
            }

            CurrentTitle = value;
            CurrentObjectiveId = current == null ? "fallback" : current.id ?? "";
            currentComplete = current != null && current.IsComplete(inventory, director);
            int count = 0;
            bool newlyChecked = false;
            for (int i = 0; i < checkedItems.Length; i++)
            {
                bool complete = current.subitems[i] != null && current.subitems[i].IsComplete(inventory, director);
                if (complete)
                    count++;
                if (complete != checkedItems[i])
                {
                    changed = true;
                    if (complete && initialized && !newEntry)
                        newlyChecked = true;
                    checkedItems[i] = complete;
                }

                if (!initialized || newEntry || silent)
                    checkProgress[i] = complete ? 1 : 0;
            }

            CompletedSubitemCount = count;
            if (changed)
            {
                LastProgressTime = Time.unscaledTime;
                highlightedUntil = Time.unscaledTime + 2;
                ProgressChanged?.Invoke();
                syncedJournal = false;
            }

            if (newlyChecked && !silent && Visible)
            {
                if (completedSound)
                    AudioManager.Instance?.Play(completedSound, game.player ? game.player.transform.position : transform.position, .12f, AudioChannel.UI, false);
                else
                    AudioManager.Instance?.PlayCue("ui_click", game.player ? game.player.transform.position : transform.position, .10f);
            }

            initialized = true;
            dirty = false;
        }

        void EnsureView()
        {
            if (panel || !game || !game.hud)
                return;
            panel = Rect("Objetivos persistentes", game.hud.transform, new Vector2(width, 300), new Vector2(margin.x, -margin.y));
            group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = group.blocksRaycasts = false;
            title = Label("Objetivo", panel, new Vector2(width, 64), Vector2.zero, 24);
            title.fontStyle = FontStyles.Normal;
            title.characterSpacing = 1.5f;
            list = Rect("Etapas", panel, new Vector2(width, 230), new Vector2(0, -67));
            BuildRows();
        }

        TMP_FontAsset Font(bool heading)
        {
            var equipment = game && game.equipment ? game.equipment.hud : null;
            return equipment ? (heading ? equipment.titleFont : equipment.subtitleFont) : game && game.hud && game.hud.objective ? game.hud.objective.font : null;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        TMP_Text Label(string name, Transform parent, Vector2 size, Vector2 position, float fontSize)
        {
            var label = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Font(name == "Objetivo");
            label.fontSize = fontSize;
            label.enableAutoSizing = false;
            label.extraPadding = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.color = normal;
            label.raycastTarget = false;
            return label;
        }

        void BuildRows()
        {
            int count = current != null && current.subitems != null ? current.subitems.Length : 0;
            checkedItems = new bool[count];
            checkProgress = new float[count];
            labels = new TMP_Text[count];
            checks = new Image[count];
            if (!list)
                return;
            for (int i = list.childCount - 1; i >= 0; i--)
            {
                var child = list.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            for (int i = 0; i < count; i++)
            {
                var row = Rect("Etapa " + i, list, new Vector2(width, 35), new Vector2(0, -i * 36));
                var border = Rect("Caixa", row, new Vector2(14, 14), new Vector2(1, -6));
                border.gameObject.AddComponent<Image>().color = new Color(normal.r, normal.g, normal.b, .28f);
                var inner = Rect("Concluído", border, new Vector2(10, 10), new Vector2(2, -2));
                checks[i] = inner.gameObject.AddComponent<Image>();
                checks[i].color = normal;
                border.GetComponent<Image>().raycastTarget = false;
                checks[i].raycastTarget = false;
                labels[i] = Label("Texto", row, new Vector2(width - 32, 35), new Vector2(28, 0), 20);
                labels[i].text = current.subitems[i]?.label ?? "";
            }
        }

        void AdvanceView()
        {
            if (!panel)
                return;
            float scale = ObjectivePreferences.TextScale;
            if (!Mathf.Approximately(scale, lastScale))
            {
                lastScale = scale;
                title.fontSize = 24 * scale;
                title.rectTransform.sizeDelta = new Vector2(width, Mathf.Ceil(62 * scale));
                list.anchoredPosition = new Vector2(0, -Mathf.Ceil(67 * scale));
                for (int i = 0; i < labels.Length; i++)
                {
                    labels[i].fontSize = 20 * scale;
                    ((RectTransform)labels[i].transform.parent).anchoredPosition = new Vector2(0, -Mathf.Ceil(i * 36 * scale));
                }
            }

            bool transitioning = Time.unscaledTime < transitionUntil && !PresentationSettings.ReduceEffects;
            string text = transitioning ? "<s>" + previousTitle + "</s>" : currentComplete ? "<s>" + CurrentTitle + "</s>" : CurrentTitle;
            if (title.text != text)
                title.text = text;
            title.color = Time.unscaledTime < highlightedUntil ? normal : new Color(normal.r, normal.g, normal.b, .76f);
            if (transitioning)
            {
                var color = title.color;
                color.a *= Mathf.Clamp01((transitionUntil - Time.unscaledTime) / .65f);
                title.color = color;
            }

            list.gameObject.SetActive(!transitioning);
            for (int i = 0; i < checks.Length; i++)
            {
                checkProgress[i] = PresentationSettings.ReduceEffects ? (checkedItems[i] ? 1 : 0) : Mathf.MoveTowards(checkProgress[i], checkedItems[i] ? 1 : 0, Time.unscaledDeltaTime * 5);
                checks[i].rectTransform.localScale = Vector3.one * checkProgress[i];
                labels[i].color = checkedItems[i] ? muted : normal;
            }
        }

        void SyncJournal()
        {
            if (!game.hud.inventoryPanel || !game.hud.inventoryPanel.activeSelf || !game.hud.inventoryText)
            {
                syncedJournal = false;
                return;
            }

            string old = game.hud.inventoryText.text ?? "";
            if (syncedJournal && old == journalWritten)
                return;
            string remaining = old;
            if (old.StartsWith("OBJETIVO\n", StringComparison.Ordinal))
            {
                int end = old.IndexOf("\n\n", StringComparison.Ordinal);
                remaining = end >= 0 ? old.Substring(end + 2) : "";
            }

            var text = new StringBuilder("OBJETIVO\n").Append(CurrentTitle);
            for (int i = 0; i < checkedItems.Length; i++)
                text.Append("\n").Append(checkedItems[i] ? "[x] " : "[ ] ").Append(current.subitems[i]?.label ?? "");
            text.Append("\n\n").Append(remaining);
            journalWritten = text.ToString();
            game.hud.inventoryText.text = journalWritten;
            syncedJournal = true;
        }
    }
}
