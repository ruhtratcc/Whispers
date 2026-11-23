// Mostra objetivos, interação, legendas, pausa e demais indicadores.

using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HorrorGame
{
    public sealed class GameHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        public GameSession game;
        [SerializeField]
        public PlayerInteraction interaction;
        [SerializeField]
        public FirstPersonMotor player;
        [SerializeField]
        public FlashlightController flashlight;
        [SerializeField]
        public InputReader input;
        [SerializeField]
        public TMP_Text prompt, crosshair, batteryLabel, objective, subtitle, notification, noteTitle, noteBody, inventoryText;
        [Tooltip("Optional presentation labels. Existing HUD prefabs continue working without them.")]
        [SerializeField]
        public TMP_Text interactionKey, batteryStateText;
        [SerializeField]
        public UnityEngine.UI.Image staminaFill, batteryFill, flashImage;
        [SerializeField]
        public CanvasGroup staminaGroup, objectiveGroup, subtitleGroup, deathCurtain;
        [SerializeField]
        public CanvasGroup interactionGroup, batteryGroup;
        [SerializeField]
        public GameObject pausePanel, optionsPanel, gameOverPanel, notePanel, inventoryPanel;
        [SerializeField]
        public UnityEngine.UI.Button resumeButton, optionsButton, optionsBackButton, mainMenuButton, restartButton, deathMenuButton, noteCloseButton, inventoryCloseButton;
        [SerializeField]
        public GameplayOptions options;
        [Header("Presentation")]
        [SerializeField]
        float objectiveDuration = 6, subtitleFade = .18f;
        float objectiveUntil, subtitleUntil, notificationUntil;
        bool noteOpen, inventoryOpen;
        NarrativeArchive archive;
        public bool InventoryOpen => inventoryOpen;
        Coroutine flashRoutine;
        string lastRawPrompt, displayedPrompt = "";
        bool promptHadKey;
        int displayedBatteryPercent = int.MinValue;
        bool warnedLowBattery, warnedEmptyBattery;
        public bool SubtitleVisible => subtitle && !string.IsNullOrWhiteSpace(subtitle.text) && (Time.time < subtitleUntil || subtitleGroup && subtitleGroup.alpha > .05f);
        public UnityEngine.UI.Button SaveRetryButton { get; private set; }
        public void ShowObjective(string text)
        {
            objective.text = text;
            objectiveUntil = Time.time + objectiveDuration;
        }

        public void ShowSubtitle(string text, float seconds = 3)
        {
            subtitle.text = text;
            subtitleUntil = Time.time + Mathf.Max(.1f, seconds);
        }

        public void ClearSubtitle(string expected)
        {
            if (!subtitle || subtitle.text != expected) return;
            subtitle.text = "";
            subtitleUntil = 0;
            if (subtitleGroup) subtitleGroup.alpha = 0;
        }

        public void Say(string text) => ShowSubtitle(text, 4);
        public void Notify(string text)
        {
            notification.text = text;
            notificationUntil = Time.time + 3.5f;
        }

        void Start()
        {
            if (subtitle && subtitle.transform.parent && subtitle.transform.parent.name == "Subtitle plate"
                && subtitle.transform.parent.TryGetComponent<UnityEngine.UI.Image>(out var authoredPlate))
                authoredPlate.enabled = false;
            CaptionPresentation.Attach(subtitle);
            if (inventoryPanel && interaction && interaction.inventory)
            {
                archive = gameObject.AddComponent<NarrativeArchive>();
                archive.Initialize(this);
            }
            resumeButton.onClick.AddListener(game.Resume);
            optionsButton.onClick.AddListener(OpenOptions);
            optionsBackButton.onClick.AddListener(CloseOptions);
            mainMenuButton.onClick.AddListener(game.ReturnToMenu);
            restartButton.onClick.AddListener(game.RestartCheckpoint);
            deathMenuButton.onClick.AddListener(game.ReturnToMenu);
            noteCloseButton.onClick.AddListener(game.Resume);
            inventoryCloseButton.onClick.AddListener(game.Resume);
            SaveRetryButton = Instantiate(resumeButton, transform);
            SaveRetryButton.name = "Retry failed checkpoint";
            SaveRetryButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            SaveRetryButton.onClick.AddListener(() => Notify(game.save && game.save.RetryPendingSave()
                ? "Progresso salvo." : "Não foi possível salvar. Ainda há progresso para salvar."));
            var retryRect = (RectTransform)SaveRetryButton.transform;
            retryRect.anchorMin = retryRect.anchorMax = retryRect.pivot = new Vector2(.5f, 1);
            retryRect.sizeDelta = new Vector2(380, 48);
            retryRect.anchoredPosition = new Vector2(0, -112);
            SaveRetryButton.GetComponentInChildren<TMP_Text>().text = "Tentar salvar novamente";
            SaveRetryButton.gameObject.SetActive(false);
            player.view.bobEnabled = PlayerPrefs.GetInt("Gameplay.Bob", 1) == 1;
            player.view.swayEnabled = PlayerPrefs.GetInt("Gameplay.Sway", 1) == 1;
            RefreshState(game.State);
        }

        void Update()
        {
            bool playing = game.State == GameState.Playing;
            bool pendingSave = game.save && game.save.HasPendingSave;
            bool canRetry = game.State == GameState.Paused || game.State == GameState.StoryScreen;
            if (SaveRetryButton) SaveRetryButton.gameObject.SetActive(pendingSave && canRetry);
            if (pendingSave && game.State != GameState.GameOver && !game.IsBlackingOut)
                notification.text = canRetry ? "Não foi possível salvar. O progresso ainda não foi salvo."
                    : "Falha ao salvar. Pressione [Esc] para tentar salvar novamente.";
            string action = (playing ? interaction.CurrentPrompt : "") ?? "";
            if (action != lastRawPrompt || (bool)interactionKey != promptHadKey)
            {
                lastRawPrompt = action;
                promptHadKey = interactionKey;
                if (interactionKey)
                {
                    string key = "E";
                    if (action.StartsWith("["))
                    {
                        int bracket = action.IndexOf(']');
                        if (bracket > 0)
                        {
                            key = action.Substring(1, bracket - 1);
                            action = action.Substring(bracket + 1).TrimStart();
                        }
                    }
                    interactionKey.text = string.IsNullOrEmpty(action) ? "" : key;
                    interactionKey.fontSize = key.Length > 3 ? 14 : 19;
                    action = action.Replace("[clique / botão direito]", "clique");
                }
                displayedPrompt = action;
            }
            action = displayedPrompt;
            prompt.text = action;
            if (interactionGroup)
                interactionGroup.alpha = Mathf.MoveTowards(interactionGroup.alpha, playing && !string.IsNullOrEmpty(action) ? 1 : 0, Time.unscaledDeltaTime * 8);
            crosshair.gameObject.SetActive(playing && !player.IsHidden);
            crosshair.text = interaction.HasTarget ? "+" : "·";
            Color aimColor = interaction.HasTarget ? new Color(.83f, .70f, .51f, .94f) : new Color(.75f, .76f, .72f, .43f);
            float aimFollow = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 12);
            crosshair.color = Color.Lerp(crosshair.color, aimColor, aimFollow);
            crosshair.fontSize = Mathf.Lerp(crosshair.fontSize, interaction.HasTarget ? 22 : 18, aimFollow);
            float stamina = player.IsHidden && interaction.CurrentHidingSpot ? interaction.CurrentHidingSpot.Breath01 : player.Stamina01;
            staminaFill.fillAmount = stamina;
            staminaGroup.alpha = Mathf.MoveTowards(staminaGroup.alpha, playing && (stamina < .98f || player.IsHidden) ? 1 : 0, Time.unscaledDeltaTime * 4);
            batteryFill.fillAmount = flashlight.Battery01;
            batteryFill.color = flashlight.Battery01 < .2f ? new Color(.8f, .26f, .18f) : new Color(.62f, .69f, .66f);
            int batteryPercent = Mathf.CeilToInt(flashlight.Battery01 * 100);
            if (batteryPercent != displayedBatteryPercent)
            {
                displayedBatteryPercent = batteryPercent;
                batteryLabel.text = batteryPercent + "%";
            }
            if (batteryStateText)
                batteryStateText.text = flashlight.Battery01 <= 0 ? "SEM CARGA — PROCURE PILHAS [E]" : flashlight.Battery01 < .2f ? "BATERIA FRACA — PILHAS [E]" : flashlight.IsOn ? "LANTERNA LIGADA" : "LANTERNA DESLIGADA";
            if (batteryGroup)
                batteryGroup.alpha = Mathf.MoveTowards(batteryGroup.alpha, playing && !game.equipment && flashlight.IsAvailable ? flashlight.IsOn || flashlight.Battery01 < .2f ? .95f : .45f : 0, Time.unscaledDeltaTime * 4);
            if (flashlight.Battery01 >= .2f) warnedLowBattery = warnedEmptyBattery = false;
            if (playing && flashlight.IsAvailable && flashlight.Battery01 <= 0 && !warnedEmptyBattery)
            {
                warnedLowBattery = warnedEmptyBattery = true;
                Notify("Sem carga. Procure pilhas [E] ou um lugar iluminado antes que a escuridão tome conta.");
            }
            else if (playing && flashlight.IsAvailable && flashlight.Battery01 < .2f && !warnedLowBattery)
            {
                warnedLowBattery = true;
                Notify("Bateria fraca. Aproxime-se das pilhas e use [E] para trocar e recarregar.");
            }
            if (game.State == GameState.Paused || game.State == GameState.GameOver)
                return;
            objectiveGroup.alpha = Mathf.MoveTowards(objectiveGroup.alpha, Time.time < objectiveUntil ? 1 : 0, Time.deltaTime * 2);
            subtitleGroup.alpha = Mathf.MoveTowards(subtitleGroup.alpha, Time.time < subtitleUntil ? 1 : 0, Time.deltaTime / Mathf.Max(.01f, subtitleFade));
            if (Time.time > notificationUntil && !pendingSave)
                notification.text = "";
        }

        public void RefreshState(GameState state)
        {
            bool paused = state == GameState.Paused;
            pausePanel.SetActive(paused && !noteOpen && !inventoryOpen);
            optionsPanel.SetActive(false);
            notePanel.SetActive(paused && noteOpen);
            if (!archive) inventoryPanel.SetActive(paused && inventoryOpen);
            else if (paused && inventoryOpen)
            {
                inventoryPanel.SetActive(true);
                archive.Show();
            }
            else if (state != GameState.Playing || !inventoryPanel.activeSelf) inventoryPanel.SetActive(false);
            gameOverPanel.SetActive(state == GameState.GameOver);
            if (state == GameState.GameOver)
            {
                deathCurtain.alpha = 1;
                notification.text = "";
                Select(restartButton);
            }
            else
            {
                deathCurtain.alpha = 0;
                if (paused)
                    Select(noteOpen ? noteCloseButton : inventoryOpen && archive ? archive.CloseButton : inventoryOpen ? inventoryCloseButton : resumeButton);
            }
        }

        void Select(UnityEngine.UI.Button button)
        {
            if (EventSystem.current && button)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        public void OpenOptions()
        {
            pausePanel.SetActive(false);
            optionsPanel.SetActive(true);
            options.Refresh();
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(options.master.gameObject);
        }

        public void CloseOptions()
        {
            optionsPanel.SetActive(false);
            pausePanel.SetActive(true);
            Select(optionsButton);
        }

        public void CloseModal()
        {
            noteOpen = inventoryOpen = false;
            notePanel.SetActive(false);
            if (archive) archive.Hide();
            else inventoryPanel.SetActive(false);
        }

        public void ShowNote(string title, string body)
        {
            if (InspectionManager.Instance && InspectionManager.Instance.IsCompleting)
                return;
            if (game.State != GameState.Playing)
                return;
            noteOpen = true;
            noteTitle.text = title;
            noteBody.text = body;
            game.Pause();
        }

        public void ShowInventory()
        {
            if (game.State != GameState.Playing || game.narrative && game.narrative.SequencePlaying
                || game.tutorials && game.tutorials.IsVisible || SceneTransitionService.ControlsBlocked) return;
            inventoryOpen = true;
            string contents = string.Join("\n\n", interaction.inventory.items.Select(i => i.title + (i.note ? "\n" + i.text : "")));
            if (inventoryText) inventoryText.text = "OBJETIVO\n" + objective.text + "\n\n" + contents;
            game.Pause();
        }

        public void Flash(Color color, float duration)
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine(color, duration));
        }

        IEnumerator FlashRoutine(Color color, float duration)
        {
            flashImage.gameObject.SetActive(true);
            float time = 0;
            duration = Mathf.Max(.05f, duration);
            while (time < duration)
            {
                time += Time.deltaTime;
                var c = color;
                c.a = color.a * (1 - time / duration);
                flashImage.color = c;
                yield return null;
            }

            flashImage.gameObject.SetActive(false);
            flashRoutine = null;
        }
    }
}
