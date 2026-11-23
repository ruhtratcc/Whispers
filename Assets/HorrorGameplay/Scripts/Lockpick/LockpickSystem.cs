// Executa o minijogo e controla os pinos da fechadura.

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorGame
{
    public interface IEquippable
    {
        bool Equipped { get; }

        bool CanEquip { get; }
    }

    [DefaultExecutionOrder(300), DisallowMultipleComponent]
    public sealed class LockpickSystem : MonoBehaviour, IEquippable
    {
        public const string ItemId = "lockpick";
        public GameSession game;
        public LockpickConfig config;
        public LockpickView view;
        public LockpickState state = new LockpickState();
        public bool IsOpen => Attempt != null;
        public LockpickAttempt Attempt { get; private set; }
        public LockableDoor CurrentDoor { get; private set; }
        public int Count => state.count;
        public bool Acquired => state.acquired;
        public bool CanEquip => Acquired;
        public bool Equipped => game && game.equipment && game.equipment.ActiveItem && game.equipment.ActiveItem.inventoryItemId == ItemId;
        public bool CanAcquire => config && Count < config.maximumPins && game && game.CanControl;
        public bool KeepLightWhilePicking => IsOpen && game.State == GameState.Inspecting && EquipmentPreferences.KeepLight;
        public float HintRemaining { get; private set; }

        Inventory inventory;
        WhistleSystem whistle;
        AudioSource scraping;
        AudioClip scrapingClip;
        float nextThought, speakAfter, blockedAfter, whisperAge, helenaAfter, openedAt, tutorialRemaining, flickerAge;
        int variation, lastLabelCount = -1;
        string countLabel, lastSoundCaption;
        float soundCaptionUntil;
        bool hintGranted, trainingPending, nearSpoken, blockedSpoken;
        public string CountLabel
        {
            get
            {
                if (lastLabelCount != Count)
                {
                    lastLabelCount = Count;
                    countLabel = Count <= 0 ? "SEM GRAMPO" : "x" + Count;
                }

                return countLabel;
            }
        }

        public static LockpickSystem For(GameSession g) => g ? g.lockpick : null;
        void Awake()
        {
            if (!game)
                game = GetComponent<GameSession>();
            if (game)
            {
                game.lockpick = this;
                inventory = game.interaction ? game.interaction.inventory : null;
            }

            whistle = GetComponent<WhistleSystem>();
            if (view)
                view.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (whistle)
                whistle.Blown += OnWhistle;
            if (game)
                game.StateChanged += StateChanged;
        }

        void OnDisable()
        {
            if (whistle)
                whistle.Blown -= OnWhistle;
            if (game)
                game.StateChanged -= StateChanged;
            Cancel(false);
        }

        void StateChanged(GameState next)
        {
            if (!IsOpen)
                return;
            if (next == GameState.Paused)
            {
                if (view)
                    view.gameObject.SetActive(false);
            }
            else if (next == GameState.Inspecting)
            {
                if (view)
                    view.gameObject.SetActive(true);
            }
            else
                Cancel(false);
        }

        public bool Acquire(int amount, bool initial = false)
        {
            if (!CanAcquire || amount <= 0)
                return false;
            state.initialized = state.acquired = true;
            state.count = Mathf.Min(config.maximumPins, state.count + amount);
            if (inventory && !inventory.Contains(ItemId))
                inventory.Add(new InventoryItem { id = ItemId, title = "Grampo da Helena" });
            game.equipment?.hud?.ShowFor(3.5f);
            if (initial)
                Speak("pickup", transform.position);
            else
                game.hud?.Notify("Grampos de reserva · x" + state.count);
            Save();
            return true;
        }

        public bool Open(LockableDoor target)
        {
            if (IsOpen || !config || !target || !target.IsOptional || target.unlocked || !target.ChapterAllowed || !Equipped || Count <= 0 || !game || !game.CanControl || !game.BeginInspection())
                return false;
            CurrentDoor = target;
            Attempt = new LockpickAttempt(config, target.difficulty, target.persistentId, target.pinsCompleted, target.toleranceOverride);
            nearSpoken = blockedSpoken = false;
            nextThought = 20;
            openedAt = Time.unscaledTime;
            whisperAge = 0;
            hintGranted = target.hintPending;
            HintRemaining = hintGranted ? config.hintSeconds : 0;
            target.hintPending = false;
            if (!state.tutorialShown && WhistleSystem.HintsEnabled)
            {
                state.tutorialShown = true;
                tutorialRemaining = 3.5f;
            }

            if (view)
            {
                view.gameObject.SetActive(true);
                view.Bind(this);
            }

            speakAfter = 0;
            trainingPending = target.training && !state.trainingWhispered;
            Speak(state.careerSpoken ? target.breaks > 0 ? "recomecar" : "comecar" : "first", transform.position);
            state.careerSpoken = true;
            Save();
            return true;
        }

        void Update()
        {
            if (!IsOpen)
                return;
            var keyboard = Keyboard.current;
            if (game.State == GameState.Paused)
            {
                if (keyboard != null && keyboard.f10Key.wasPressedThisFrame)
                    game.Resume();
                return;
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cancel();
                return;
            }

            if (keyboard != null && keyboard.f10Key.wasPressedThisFrame)
            {
                game.Pause();
                return;
            }

            var mouse = Mouse.current;
            // Converte a posição horizontal do mouse em um ângulo entre 0 e 180 graus.
            float angle = mouse == null ? 90 : Mathf.Clamp01(mouse.position.ReadValue().x / Mathf.Max(1, Screen.width)) * 180;
            bool tension = mouse != null && mouse.leftButton.isPressed && Time.unscaledTime - openedAt > .15f;
            Advance(Time.deltaTime, angle, tension);
        }

        public void Advance(float seconds, float angle, bool tension)
        {
            if (!IsOpen || !game || game.State != GameState.Inspecting || !float.IsFinite(seconds) || seconds <= 0)
                return;
            float dt = Mathf.Min(seconds, .1f);
            HintRemaining = Mathf.Max(0, HintRemaining - dt);
            tutorialRemaining = Mathf.Max(0, tutorialRemaining - dt);
            whisperAge += dt;
            var result = Attempt.Step(dt, angle, tension, LockpickPreferences.Assisted, LockpickPreferences.Automatic, CurrentDoor.breaks);
            UpdateScraping(tension);
            if (Attempt.Elapsed >= nextThought)
            {
                nextThought = Attempt.Elapsed + 24;
                Speak(tension && Attempt.Stress > .5f ? "ruido" : tension ? "pressa" : "calma", transform.position);
            }

            if (tension && Attempt.Stress > .25f && !blockedSpoken)
            {
                blockedSpoken = true;
                Play(config.blocked, config.clickGain);
                Speak("travou", transform.position);
            }

            if (tension && Attempt.Progress > .75f && !nearSpoken)
            {
                nearSpoken = true;
                Speak("quase", transform.position);
            }

            if (trainingPending && Attempt.Elapsed >= 3)
            {
                trainingPending = false;
                state.trainingWhispered = true;
                Speak("child.slow", BehindDoor(), true);
                Save();
            }

            if (whisperAge >= config.whisperSeconds)
            {
                whisperAge = 0;
                Speak("child", BehindDoor(), true);
            }

            if ((Attempt.Elapsed > config.commentAfter || CurrentDoor.breaks >= 2) && Time.unscaledTime >= Mathf.Max(helenaAfter, speakAfter))
            {
                helenaAfter = Time.unscaledTime + config.helenaCooldown;
                flickerAge = .9f;
                Speak("helena", BehindDoor(), true);
            }

            // Ao encaixar um pino, registra o avanço para quando o jogador voltar.
            if (result == LockpickResult.PinSet)
            {
                if (hintGranted)
                    HintRemaining = config.hintSeconds;
                nearSpoken = blockedSpoken = false;
                CurrentDoor.pinsCompleted = Attempt.CompletedPins;
                Play(config.pinClick, config.clickGain);
                Caption("[clique]");
                Save();
            }
            // Ao quebrar, perde um grampo. Os pinos encaixados continuam registrados.
            else if (result == LockpickResult.Broken)
            {
                nearSpoken = blockedSpoken = false;
                speakAfter = 0;
                CurrentDoor.breaks++;
                state.count = Mathf.Max(0, state.count - 1);
                Play(config.snap, config.breakGain);
                Caption("[grampo quebrou]");
                Speak(Count > 0 ? "quebrou" : "empty", transform.position);
                flickerAge = .8f;
                Save();
                if (Count == 0)
                {
                    Cancel();
                    return;
                }
            }
            // Quando o último pino encaixa, abre a fechadura e salva o resultado.
            else if (result == LockpickResult.Complete)
            {
                if (LockpickPreferences.Automatic)
                {
                    if (Count <= 0)
                    {
                        Cancel();
                        return;
                    }

                    state.count--;
                }

                speakAfter = 0;
                var target = CurrentDoor;
                target.Complete();
                Play(config.unlock, config.clickGain);
                Speak("conseguiu", transform.position);
                Cancel();
                Save();
                return;
            }

            if (view)
                view.Present(tutorialRemaining > 0);
        }

        void LateUpdate()
        {
            if (flickerAge <= 0 || !game || game.State == GameState.Paused)
                return;
            flickerAge = Mathf.Max(0, flickerAge - Time.deltaTime);
            var lamp = game.save && game.save.flashlight ? game.save.flashlight.lamp : null;
            if (lamp && !PresentationSettings.ReduceEffects)
                lamp.intensity *= .94f + .06f * Mathf.Cos(flickerAge * Mathf.PI * 2);
        }

        void UpdateScraping(bool tension)
        {
            if (!tension)
            {
                StopScraping();
                return;
            }

            if ((!scraping || scraping.clip != scrapingClip || !scraping.isPlaying) && config.scrape)
            {
                scraping = AudioManager.Instance?.Play(config.scrape, CurrentDoor.transform.position, config.scrapeGain, AudioChannel.SFX, true);
                scrapingClip = scraping ? scraping.clip : null;
                if (scraping)
                {
                    scraping.loop = true;
                    Caption("[grampo mexendo na fechadura]");
                }
            }

            if (scraping && scraping.clip == scrapingClip)
                scraping.pitch = .84f + .24f * Attempt.Proximity;
        }

        void StopScraping()
        {
            if (scraping && scraping.clip == scrapingClip)
            {
                scraping.loop = false;
                scraping.Stop();
            }

            scraping = null;
            scrapingClip = null;
        }

        void Caption(string text)
        {
            lastSoundCaption = text;
            soundCaptionUntil = Time.unscaledTime + .75f;
            game.hud?.ShowSubtitle(text, .75f);
        }

        void Play(SoundSet sound, float gain)
        {
            if (sound)
                AudioManager.Instance?.Play(sound, CurrentDoor ? CurrentDoor.transform.position : transform.position, gain, AudioChannel.SFX, true);
        }

        Vector3 BehindDoor() => CurrentDoor ? CurrentDoor.transform.position + CurrentDoor.transform.forward * .7f : transform.position;
        public void HintSound(Vector3 position)
        {
            if (config && config.pinClick)
                AudioManager.Instance?.Play(config.pinClick, position, .09f, AudioChannel.SFX, true);
        }

        void OnWhistle(WhistleContext context)
        {
            if (context.kind != WhistleKind.Tap)
                return;
            for (int i = 0; i < LockableDoor.Active.Count; i++)
                LockableDoor.Active[i].OfferHint(context);
        }

        public void Speak(string trigger, Vector3 position, bool child = false)
        {
            if (!config || !config.dialogue || !game || !game.hud)
                return;
            float now = Time.unscaledTime;
            if (child)
            {
                if (now < blockedAfter)
                    return;
                blockedAfter = now + 9;
            }
            else
            {
                if (now < speakAfter)
                    return;
                speakAfter = now + (trigger == "sem_grampo" || trigger == "empty" ? 12 : 5);
            }

            var line = config.dialogue.Find(trigger, variation++);
            if (line == null)
                return;
            game.hud.ShowSubtitle((Time.unscaledTime < soundCaptionUntil ? lastSoundCaption + "\n" : "") + line.character + ": " + line.text, line.voice ? line.voice.length : Mathf.Clamp(line.text.Length * .05f, 1.5f, 4));
            if (line.voice)
                AudioManager.Instance?.PlayClip(line.voice, position, child ? config.voiceGain : .5f, 1, AudioChannel.Voice, child);
        }

        public void Cancel(bool resume = true)
        {
            if (!IsOpen)
                return;
            StopScraping();
            Attempt = null;
            CurrentDoor = null;
            HintRemaining = 0;
            if (view)
                view.gameObject.SetActive(false);
            if (resume && game)
                game.EndInspection();
            Save();
        }

        void Save()
        {
            if (game && game.narrative && !game.IsRestoringCheckpoint)
                game.narrative.ScheduleSave();
        }

        public LockpickState Capture() => new LockpickState
        {
            initialized = true,
            acquired = Acquired,
            count = Count,
            tutorialShown = state.tutorialShown,
            trainingWhispered = state.trainingWhispered,
            careerSpoken = state.careerSpoken
        };
        public void Restore(LockpickState saved)
        {
            Cancel(false);
            state = saved != null && saved.initialized && saved.Valid ? JsonUtility.FromJson<LockpickState>(JsonUtility.ToJson(saved)) : new LockpickState
            {
                initialized = true,
                acquired = inventory && inventory.Contains(ItemId),
                count = inventory && inventory.Contains(ItemId) && config ? config.initialPins : 0
            };
            if (config)
                state.count = Mathf.Clamp(state.count, 0, config.maximumPins);
            if (state.acquired && inventory && !inventory.Contains(ItemId))
                inventory.Add(new InventoryItem { id = ItemId, title = "Grampo da Helena" });
            for (int i = 0; i < LockableDoor.Active.Count; i++)
                LockableDoor.Active[i].Reconcile();
        }
    }
}
