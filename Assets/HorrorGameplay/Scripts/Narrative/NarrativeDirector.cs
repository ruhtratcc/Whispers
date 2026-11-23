// Aceita eventos, atualiza o objetivo e coordena diálogos e capítulos.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace HorrorGame
{
    [DisallowMultipleComponent]
    public sealed class NarrativeDirector : MonoBehaviour
    {
        [Header("Story and scene references")]
        public StoryBook book;
        public GameSession game;
        public Inventory inventory;
        public ChapterTransitionView transitionView;
        public VintageCameraEffect vintageCamera;
        public string startingChapterId = "prologue";
        [Tooltip("Disable on a chapter preview to finish with its completion card instead of loading the next scene.")]
        public bool autoLoadNextChapter = true;
        [Tooltip("Opt in to entering the next chapter in this loaded scene without resetting its world or player.")]
        public bool continueSameSceneChapters;
        [Header("Map event hooks — saved milestone IDs")]
        public UnityEvent<string> onSignal = new UnityEvent<string>();
        public UnityEvent<string> onChapterCompleted = new UnityEvent<string>();
        public UnityEvent onInitialized = new UnityEvent();
        public event Action<string> SignalAccepted;
        public event Action<string> SequenceStarted;
        public event Action<string> SequenceCompleted;
        public event Action Initialized;
        /// <summary>After scene flows restore their visuals; chapter baselines can now capture the restored world.</summary>
        public event Action<string> ChapterInitialized;
        public NarrativeProgress Progress { get; private set; } = new NarrativeProgress();
        public bool IsInitialized { get; private set; }
        public bool MigratedStory { get; private set; }
        public bool SequencePlaying => sequenceRoutine != null;
        public string ActiveSequenceId { get; private set; } = "";
        public StoryChapterDefinition CurrentChapter => book ? book.Chapter(Progress.chapterId) : null;
        public string NextSignal => Progress.Next(book)?.id ?? "";

        readonly Queue<string> sequenceQueue = new Queue<string>();
        readonly HashSet<string> queued = new HashSet<string>();
        Coroutine sequenceRoutine, saveRoutine, inventoryRoutine, transitionRoutine;
        bool ownsControl;
        Inventory subscribedInventory;
        AudioSource dialogueVoice;
        VoiceAudioPresentation voicePresentation;

        void OnEnable() { SubscribeInventory(); }
        void SubscribeInventory()
        {
            if (subscribedInventory == inventory) return;
            if (subscribedInventory) subscribedInventory.Changed -= InventoryChanged;
            subscribedInventory = inventory;
            if (subscribedInventory) subscribedInventory.Changed += InventoryChanged;
        }
        public void InitializeAfterSession(bool restored)
        {
            if (!game) game = GameSession.Instance;
            if (!inventory && game && game.interaction) inventory = game.interaction.inventory;
            SubscribeInventory();
            StopPresentation();
            if (!book || book.ChapterIndex(startingChapterId) < 0)
            {
                Debug.LogError("Narrative scene requires a valid StoryBook and starting chapter.", this);
                return;
            }
            if (restored && ((Progress.storyId ?? "") != (book.storyId ?? "")
                || BeforeDemolitionCampaign.Is(book) && (book.revision != BeforeDemolitionCampaign.Revision
                    || Progress.storyRevision != book.revision || book.ChapterIndex(Progress.chapterId) < 0)))
            {
                IsInitialized = false;
                Debug.LogWarning("Checkpoint story identity does not match this campaign.", this);
                if (game)
                {
                    game.ShowStoryScreen();
                    game.hud?.Notify("Não foi possível retomar o checkpoint. Seu save foi preservado.");
                    game.ReturnToMenu();
                }
                return;
            }
            MigratedStory = restored && book.revision == 2 && SimplifiedStoryContent.Migrate(Progress, inventory);
            if (!restored || book.ChapterIndex(Progress.chapterId) < 0)
                Progress = new NarrativeProgress { storyId = book.storyId, chapterId = startingChapterId, storyRevision = book.revision };
            IsInitialized = true;
            ConfigureCameraChapter(Progress.chapterId);
            RefreshObjective();
            Initialized?.Invoke();
            onInitialized.Invoke();
            ChapterInitialized?.Invoke(Progress.chapterId);
            InventoryChanged();
            if (Progress.IsChapterComplete(book)) TryAdvanceChapter();
            else
            {
                // Story recordings resume; contextual reactions belong to the action, not a loaded backlog.
                var chapter = CurrentChapter;
                foreach (var milestone in chapter.milestones)
                    if (Progress.HasSignal(milestone.id) && !string.IsNullOrEmpty(milestone.automaticSequenceId)
                        && !Progress.HasSequence(milestone.automaticSequenceId))
                    {
                        if (restored && milestone.automaticSequenceId.StartsWith("voice.", StringComparison.Ordinal))
                            Progress.CompleteSequence(milestone.automaticSequenceId);
                        else PlaySequence(milestone.automaticSequenceId);
                    }
            }
        }
        public NarrativeProgress CaptureProgress() => Progress.Copy();
        public void RestoreProgress(NarrativeProgress saved)
        {
            StopPresentation();
            IsInitialized = false;
            Progress = saved != null ? saved.Copy() : new NarrativeProgress { storyId = book ? book.storyId : null, chapterId = startingChapterId };
        }
        public bool HasSignal(string id) => Progress.HasSignal(id);
        public bool HasSequence(string id) => Progress.HasSequence(id);
        public bool Signal(string id)
        {
            if (!IsInitialized || !book || !game || game.IsBlackingOut || game.State == GameState.Paused || game.State == GameState.Tutorial || game.State == GameState.GameOver
                || game.State == GameState.StoryScreen || !Progress.TryComplete(book, id, item => inventory && inventory.Contains(item))) return false;
            RefreshObjective();
            SignalAccepted?.Invoke(id);
            onSignal.Invoke(id);
            ScheduleSave();
            InventoryChanged();
            var milestone = Array.Find(CurrentChapter.milestones, m => m.id == id);
            if (!string.IsNullOrEmpty(milestone.automaticSequenceId)) PlaySequence(milestone.automaticSequenceId);
            if (Progress.IsChapterComplete(book) && !SequencePlaying) TryAdvanceChapter();
            return true;
        }
        public bool PlaySequence(string id)
        {
            if (!IsInitialized || !book || !game || game.IsBlackingOut || game.State == GameState.GameOver || game.State == GameState.StoryScreen || game.State == GameState.Tutorial
                || Progress.HasSequence(id) || queued.Contains(id)) return false;
            var sequence = book.Sequence(id);
            if (sequence == null) return false;
            if (!string.IsNullOrEmpty(sequence.completionSignal) && NextSignal != sequence.completionSignal) return false;
            queued.Add(id); sequenceQueue.Enqueue(id);
            if (sequenceRoutine == null) sequenceRoutine = StartCoroutine(RunSequences());
            return true;
        }
        IEnumerator RunSequences()
        {
            // Yield guarantees the coroutine handle exists before event callbacks fire.
            yield return null;
            while (sequenceQueue.Count > 0)
            {
                while (game && game.State == GameState.Paused) yield return null;
                if (!game || game.State == GameState.GameOver || game.State == GameState.StoryScreen) { AbortSequences(); yield break; }
                string id = sequenceQueue.Dequeue();
                var sequence = book.Sequence(id);
                var demon = game ? game.GetComponent<DemonVoice>() : null;
                bool spoken = Array.Exists(sequence.lines ?? Array.Empty<StoryLine>(), line => line != null && line.voice) || demon && demon.HandlesSequence(id);
                while (spoken && game && game.State == GameState.Inspecting) yield return null;
                if (!game || game.State == GameState.GameOver || game.State == GameState.StoryScreen) { AbortSequences(); yield break; }
                ActiveSequenceId = id;
                if (sequence.lockPlayer && game.State == GameState.Playing)
                { game.DisableControl(); ownsControl = true; }
                SequenceStarted?.Invoke(id);
                int lineIndex=0;
                foreach (var authored in sequence.lines ?? Array.Empty<StoryLine>())
                {
                    var line=demon?demon.Prepare(authored,id,lineIndex++):authored;
                    if (game.State == GameState.GameOver) { AbortSequences(); yield break; }
                    string text = line == null ? "" : string.IsNullOrEmpty(line.speaker) ? line.text : line.speaker + ": " + line.text;
                    if (line != null && line.voice)
                    {
                        if (line.voice.loadState == AudioDataLoadState.Unloaded) line.voice.LoadAudioData();
                        while (line.voice.loadState == AudioDataLoadState.Loading) yield return null;
                        while (demon && !demon.AudioReady(line)) yield return null;
                        // Loading or inspection must not let the caption run ahead of its recording.
                        while (game && (game.State == GameState.Paused || game.State == GameState.Inspecting)) yield return null;
                    }
                    float remaining = line != null ? line.Duration : .1f;
                    if (game.hud) game.hud.ShowSubtitle(text, remaining);
                    PlayVoice(line, id);
                    bool inspectionPaused = false;
                    while (remaining > 0)
                    {
                        if (!game || game.State == GameState.GameOver) { AbortSequences(); yield break; }
                        if (spoken && game.State == GameState.Inspecting)
                        {
                            if (!inspectionPaused && dialogueVoice) dialogueVoice.Pause();
                            if (!inspectionPaused && demon) demon.PauseSpeech();
                            inspectionPaused = true;
                            yield return null;
                            continue;
                        }
                        if (inspectionPaused)
                        {
                            if (dialogueVoice) dialogueVoice.UnPause();
                            if (demon) demon.ResumeSpeech();
                            if (game.hud) game.hud.ShowSubtitle(text, remaining);
                            inspectionPaused = false;
                        }
                        // Recorded speech follows the audio clock, including slow or clamped game frames.
                        remaining -= spoken ? (game.State == GameState.Paused ? 0 : Time.unscaledDeltaTime) : Time.deltaTime;
                        if (line != null && line.voice && game.hud && game.State != GameState.Paused)
                            game.hud.ShowSubtitle(text, .25f);
                        yield return null;
                    }
                    StopVoice();
                }
                ClearSubtitle();
                while (game && game.State == GameState.Paused) yield return null;
                if (!game || game.State == GameState.GameOver) { AbortSequences(); yield break; }
                queued.Remove(id);
                if (!string.IsNullOrEmpty(sequence.completionSignal) && !Signal(sequence.completionSignal))
                {
                    Debug.LogWarning("Story sequence could not complete its required milestone: " + id, this);
                    continue;
                }
                Progress.CompleteSequence(id);
                SequenceCompleted?.Invoke(id);
                ScheduleSave();
            }
            while (game && game.State == GameState.Paused) yield return null;
            sequenceRoutine = null; ActiveSequenceId = "";
            if (ownsControl && game && game.State == GameState.Cutscene) game.EnableControl();
            ownsControl = false;
            if (Progress.IsChapterComplete(book)) TryAdvanceChapter();
        }
        void AbortSequences()
        {
            StopVoice();
            sequenceRoutine = null; ActiveSequenceId = "";
            sequenceQueue.Clear(); queued.Clear(); ownsControl = false;
            if (!game || game.State != GameState.GameOver) ClearSubtitle();
        }
        public bool TryAdvanceChapter()
        {
            if (!IsInitialized || !Progress.IsChapterComplete(book) || transitionRoutine != null || SequencePlaying || !game || game.IsBlackingOut
                || (game.State != GameState.Playing && game.State != GameState.Cutscene)) return false;
            bool newlyCompleted = Progress.CompleteChapter(book);
            if (newlyCompleted) onChapterCompleted.Invoke(Progress.chapterId);
            game.ShowStoryScreen();
            SaveChapterTransition();
            transitionRoutine = StartCoroutine(Transition());
            return true;
        }
        IEnumerator Transition()
        {
            yield return null;
            ClearSubtitle();
            var current = CurrentChapter;
            var next = book.Chapter(Progress.pendingChapterId);
            if (transitionView) yield return transitionView.Present(current, next, Progress.endingSeen);
            if (continueSameSceneChapters && next != null && next.sceneName == SceneManager.GetActiveScene().name
                && Progress.EnterPendingChapter(book))
            {
                // Enter in memory: LoadChapterCarry intentionally resets the previous map's
                // objects/spawn, which would discard this school's doors, clues and position.
                ConfigureCameraChapter(Progress.chapterId);
                RefreshObjective();
                Initialized?.Invoke();
                onInitialized.Invoke();
                ChapterInitialized?.Invoke(Progress.chapterId);
                if (transitionView) transitionView.Hide();
                game.ResumeStoryChapter();
                InventoryChanged();
                SaveChapterTransition();
            }
            // The school's preview policy must not replace the actual ending with a chapter card.
            else if (Progress.endingSeen && transitionView)
                transitionView.ShowUnavailable(null, true);
            else if (!autoLoadNextChapter && transitionView)
                transitionView.ShowChapterEnd(current, next);
            else if (next != null && !string.IsNullOrWhiteSpace(next.sceneName) && Application.CanStreamedLevelBeLoaded(next.sceneName))
            {
                ConfigureCameraChapter(next.id);
                game.LoadStoryChapter(next.sceneName);
            }
            else if (transitionView) transitionView.ShowUnavailable(next, Progress.endingSeen);
            else
            {
                // Scene integration error still leaves ESC and ReturnToMenu usable.
                if (game.hud) game.hud.ShowSubtitle(Progress.endingSeen ? "FIM" : (next?.title ?? "") + "\nEsta parte da história ainda não está disponível.", float.MaxValue);
            }
            transitionRoutine = null;
        }
        void ConfigureCameraChapter(string chapterId)
        {
            if (!vintageCamera && game && game.player && game.player.view && game.player.view.playerCamera)
                vintageCamera = game.player.view.playerCamera.GetComponent<VintageCameraEffect>();
            if (!vintageCamera) vintageCamera = FindAnyObjectByType<VintageCameraEffect>();
            if (vintageCamera && book) vintageCamera.ConfigureForChapter(book.ChapterIndex(chapterId), true);
        }
        void InventoryChanged()
        {
            if (!IsInitialized || inventoryRoutine != null || !isActiveAndEnabled) return;
            inventoryRoutine = StartCoroutine(CheckInventoryNextFrame());
        }
        IEnumerator CheckInventoryNextFrame()
        {
            // CollectibleItem invokes Changed before its own saved collected flag is set.
            yield return null;
            while (game && game.State == GameState.Paused) yield return null;
            inventoryRoutine = null;
            if (!game || game.State == GameState.GameOver || game.State == GameState.StoryScreen) yield break;
            var next = Progress.Next(book);
            if (next != null && next.requiredItemIds != null && next.requiredItemIds.Length > 0
                && !Array.Exists(book.sequences, sequence => sequence != null && sequence.completionSignal == next.id)) Signal(next.id);
        }
        void RefreshObjective()
        {
            var chapter = CurrentChapter;
            if (chapter == null || !game || !game.hud) return;
            string objective = chapter.initialObjective;
            foreach (var milestone in chapter.milestones)
                if (Progress.HasSignal(milestone.id)) objective = milestone.objectiveAfter;
            game.hud.ShowObjective(objective);
        }
        public void ScheduleSave()
        {
            if (game && (game.IsBlackingOut || game.State == GameState.StoryScreen)) return;
            if (saveRoutine == null && isActiveAndEnabled) saveRoutine = StartCoroutine(SaveNextFrame());
        }
        void SaveChapterTransition()
        {
            if(saveRoutine!=null){StopCoroutine(saveRoutine);saveRoutine=null;}
            if(game&&game.save)game.save.SaveAsync();
        }
        public bool SaveCheckpointNow()
        {
            if (saveRoutine != null) { StopCoroutine(saveRoutine); saveRoutine = null; }
            return game && game.save && game.save.Save();
        }
        IEnumerator SaveNextFrame()
        { yield return null; saveRoutine = null; if (IsInitialized && game && !game.IsBlackingOut && game.save && game.State != GameState.GameOver && game.State != GameState.StoryScreen) game.save.Save(); }
        public void SuspendForBlackout() => StopPresentation();
        public void ClearSubtitle()
        {
            if (!game || !game.hud) return;
            game.hud.ShowSubtitle("", .1f);
            if (game.hud.subtitleGroup) game.hud.subtitleGroup.alpha = 0;
        }
        void StopPresentation()
        {
            StopVoice();
            if (sequenceRoutine != null) StopCoroutine(sequenceRoutine);
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            if (inventoryRoutine != null) StopCoroutine(inventoryRoutine);
            if (saveRoutine != null) StopCoroutine(saveRoutine);
            sequenceRoutine = transitionRoutine = inventoryRoutine = saveRoutine = null;
            ActiveSequenceId = "";
            sequenceQueue.Clear(); queued.Clear();
            if (ownsControl && game && (game.State == GameState.Cutscene || game.State == GameState.Paused)) game.EnableControl();
            ownsControl = false; ClearSubtitle();
        }
        void PlayVoice(StoryLine line, string sequenceId)
        {
            StopVoice();
            var demon=game?game.GetComponent<DemonVoice>():null;
            if(demon&&demon.TryPlay(line,sequenceId))return;
            var clip = line?.voice;
            if (!clip) return;
            if (!dialogueVoice)
            {
                // Dialogue owns its source so ambient sounds cannot reuse or cut it off.
                var emitter = new GameObject("Dialogue voice");
                emitter.transform.SetParent(transform, false);
                dialogueVoice = emitter.AddComponent<AudioSource>();
                dialogueVoice.playOnAwake = false;
                dialogueVoice.ignoreListenerPause = false;
                voicePresentation = emitter.AddComponent<VoiceAudioPresentation>();
            }
            bool protagonist = line.speaker == "João";
            bool telephone = !protagonist && (sequenceId.StartsWith("prologue.call-") || sequenceId == "road.arrival-call");
            bool intercom = !protagonist && sequenceId == BeforeDemolitionFourStudentsFlow.IntercomSequenceId;
            var style = intercom ? VoicePlaybackStyle.Intercom : line.speaker == "Rádio" ? VoicePlaybackStyle.Radio
                : telephone ? VoicePlaybackStyle.Telephone : protagonist ? VoicePlaybackStyle.Natural : VoicePlaybackStyle.Presence;
            Transform listener = game && game.player && game.player.view && game.player.view.playerCamera
                ? game.player.view.playerCamera.transform : null;
            voicePresentation.Configure(dialogueVoice, style, SceneManager.GetActiveScene().name == "SchoolCEEP",
                intercom || style == VoicePlaybackStyle.Presence ? null : listener,
                style == VoicePlaybackStyle.Radio ? new Vector3(.35f, -.25f, .8f) : new Vector3(.12f, -.1f, .08f));
            var road = style == VoicePlaybackStyle.Radio ? FindAnyObjectByType<RoadJourneyFlow>() : null;
            if (road && road.carRoot && listener)
                voicePresentation.Configure(dialogueVoice, style, false, road.carRoot,
                    road.carRoot.InverseTransformPoint(listener.TransformPoint(new Vector3(.35f, -.25f, .8f))));
            // An apparition's voice stays at its origin rather than following João through the room.
            if ((intercom || style == VoicePlaybackStyle.Presence) && listener)
            {
                var mirror = game ? game.GetComponentInChildren<BeforeDemolitionMirrorFlow>(true) : null;
                var corridor = game ? game.GetComponent<BeforeDemolitionTwoVoicesFlow>() : null;
                var four = game ? game.GetComponent<BeforeDemolitionFourStudentsFlow>() : null;
                if ((sequenceId.StartsWith("bd.mirror.") || sequenceId.StartsWith("bd.ending.")) && mirror && mirror.mirrorPlane)
                    dialogueVoice.transform.position = mirror.mirrorPlane.position;
                else if (sequenceId=="bd.loop.voice" && corridor && corridor.voiceAnchor)
                    dialogueVoice.transform.position = corridor.voiceAnchor.position;
                else if (intercom && four)
                {
                    dialogueVoice.transform.position = four.intercomSpeaker
                        ? four.intercomSpeaker.position : four.intercomArrival.center + Vector3.up * 1.5f;
                    dialogueVoice.maxDistance = 33;
                    dialogueVoice.minDistance = 7;
                    dialogueVoice.volume = .95f;
                }
                else dialogueVoice.transform.position = listener.position + listener.forward * 6 + Vector3.up * .2f;
            }
            dialogueVoice.outputAudioMixerGroup = AudioManager.Instance ? AudioManager.Instance.voiceGroup : null;
            dialogueVoice.clip = clip;
            dialogueVoice.Play();
        }
        void StopVoice()
        {
            if(game)game.GetComponent<DemonVoice>()?.StopSpeech();
            if (!dialogueVoice) return;
            dialogueVoice.Stop();
            dialogueVoice.clip = null;
        }
        void OnDisable()
        {
            if (subscribedInventory) subscribedInventory.Changed -= InventoryChanged;
            subscribedInventory = null;
            StopPresentation(); IsInitialized = false;
        }
    }
}
