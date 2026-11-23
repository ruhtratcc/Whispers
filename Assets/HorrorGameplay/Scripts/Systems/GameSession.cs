// Coordena os modos de jogo como exploração, pausa, inspeção e cutscene.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorGame
{
    public enum GameState
    {
        Playing,
        Paused,
        GameOver,
        Cutscene,
        StoryScreen,
        Inspecting,
        BlackingOut,
        Tutorial
    }

    [DefaultExecutionOrder(-150)]
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        [Header("Scene integration")]
        [SerializeField]
        public SceneCatalog scenes;
        [Header("References")]
        [SerializeField]
        public InputReader input;
        [SerializeField]
        public FirstPersonMotor player;
        [SerializeField]
        public PlayerInteraction interaction;
        [SerializeField]
        public GameHUD hud;
        [SerializeField]
        public SaveSystem save;
        public EquipmentController equipment;
        public LockpickSystem lockpick;
        public TutorialCardSystem tutorials;
        bool tutorialFreezesGame=true;
        [SerializeField]
        public HorrorAtmosphere atmosphere;
        [SerializeField]
        public NarrativeDirector narrative;
        [SerializeField]
        public SoundSet deathSound;
        [Header("Opening objective")]
        [SerializeField, TextArea]
        public string initialObjective = "Encontre a chave e explore os sistemas de teste.";
        [Header("Gameplay safeguards")]
        [SerializeField]
        float killFloor = -25;
        public GameState State { get; private set; } = GameState.Playing;

        int controlAvailableFrame;
        bool discardedTransitionScene;
        public bool CanControl => !SceneTransitionService.ControlsBlocked && !IsRestoringCheckpoint && State == GameState.Playing && Time.frameCount > controlAvailableFrame;
        public bool IsBlackingOut { get; private set; }
        public bool IsRestoringCheckpoint { get; private set; }

        public event Action<GameState> StateChanged;
        Coroutine cutscene;
        bool darknessDeathPending;
        GameState stateBeforePause = GameState.Playing;
        Vector3 fallRecoveryPosition;
        float fallRecoveryHeading, fallRecoveryPitch;
        string ProgressKey(string suffix) => BeforeDemolitionCampaign.Key(narrative ? narrative.book : null, suffix);
        void Awake()
        {
            using(LoadingMetrics.Measure("GameSession.Awake")){
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            discardedTransitionScene = SceneTransitionService.IsDiscarding(gameObject.scene.name);
            if (discardedTransitionScene) { IsRestoringCheckpoint = true; return; }
            GameClock.Ensure(this);
            bool carry = PlayerPrefs.GetInt(ProgressKey("StoryCarryRequested"), 0) == 1;
            IsRestoringCheckpoint = carry || PlayerPrefs.GetInt(ProgressKey("ResumeRequested"), 0) == 1;
            if (!carry) SaveSystem.ClearChapterCarry();
            Time.timeScale = 1;
            AudioListener.pause = false;
        
            }
        }

        IEnumerator Start()
        {
            if (discardedTransitionScene) yield break;
            if (player)
            {
                fallRecoveryPosition = player.transform.position;
                fallRecoveryHeading = player.transform.eulerAngles.y;
                fallRecoveryPitch = player.Pitch;
            }
            yield return null;
            bool resume = PlayerPrefs.GetInt(ProgressKey("ResumeRequested"), 0) == 1;
            bool carry = PlayerPrefs.GetInt(ProgressKey("StoryCarryRequested"), 0) == 1;
            PlayerPrefs.SetInt(ProgressKey("ResumeRequested"), 0);
            PlayerPrefs.SetInt(ProgressKey("StoryCarryRequested"), 0);
            bool restored = false;
            if (resume || carry)
            {
                restored = save && (resume ? save.LoadCurrent() : save.LoadChapterCarry());
                if (!restored)
                {
                    ShowStoryScreen();
                    hud?.Notify("Não foi possível retomar do último ponto salvo. Seu salvamento foi preservado.");
                    ReturnToMenu();
                    yield break;
                }
            }
            IsRestoringCheckpoint = false;
            SetState(GameState.Playing);
            if (narrative)
            {
                narrative.InitializeAfterSession(restored);
                if (!narrative.IsInitialized) yield break;
            }
            else if (!string.IsNullOrWhiteSpace(initialObjective))
                hud?.ShowObjective(initialObjective);
            if (!resume && save) save.SaveAsync();
            LoadingMetrics.SessionReady();
        }

        void Update()
        {
            if (SceneTransitionService.ControlsBlocked || tutorials && tutorials.IsVisible) return;
            if (input && input.Inventory && !(input.Pause && State != GameState.Inspecting))
            {
                if (State == GameState.Paused && hud && hud.InventoryOpen) { Resume(); return; }
                if (CanControl && hud) hud.ShowInventory();
            }
            if (input && input.Pause && State != GameState.Inspecting)
            {
                if (State == GameState.Paused)
                {
                    if (ObjectiveOptions.TryClose() || EquipmentOptionsPanel.TryClose() || AccessibilityPanel.TryClose()) return;
                    if (hud && hud.optionsPanel && hud.optionsPanel.activeSelf) { hud.CloseOptions(); return; }
                    Resume();
                }
                else if (State == GameState.Playing || State == GameState.Cutscene || State == GameState.BlackingOut)
                    Pause();
                else if (State == GameState.StoryScreen)
                    ReturnToMenu();
            }

            if (State == GameState.Playing && player && player.transform.position.y < killFloor)
            {
                if (gameObject.scene.name == "SchoolCEEP") RecoverFromFall();
                else Kill();
            }
        }

        public void RecoverFromFall()
        {
            if (State != GameState.Playing || !player || gameObject.scene.name != "SchoolCEEP") return;
            // Use the same save/event lock as sanity recovery, without inventing a lethal school event.
            BeginBlackout("");
            try
            {
                bool restored = save && save.TryReadCurrent(out var checkpoint)
                    && checkpoint.position.y > killFloor && save.RestoreSnapshot(checkpoint);
                if (!restored) player.Warp(fallRecoveryPosition, fallRecoveryHeading, fallRecoveryPitch);
                EndBlackout();
                narrative?.InitializeAfterSession(true);
                hud?.Notify(restored ? "Você voltou ao último checkpoint." : "Não foi possível voltar ao último ponto salvo. O progresso atual foi mantido.");
            }
            finally { EndBlackout(); }
        }

        public void Pause()
        {
            if (State != GameState.Playing && State != GameState.Cutscene && State != GameState.Inspecting && State != GameState.BlackingOut)
                return;
            stateBeforePause = State;
            interaction?.grabber?.Drop();
            SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State != GameState.Paused)
                return;
            hud?.CloseModal();
            SetState(stateBeforePause);
            AudioManager.Instance?.ApplySettings();
        }

        public bool BeginInspection()
        {
            if (!CanControl || !player || player.IsHidden || (narrative && narrative.SequencePlaying))
                return false;
            interaction?.grabber?.Drop();
            player.HoldingBreath = false;
            SetState(GameState.Inspecting);
            return true;
        }

        public void EndInspection()
        {
            if (State == GameState.Inspecting)
                SetState(GameState.Playing);
            else if (State == GameState.Paused && stateBeforePause == GameState.Inspecting)
                stateBeforePause = GameState.Playing;
        }

        public bool BeginTutorial(bool freeze=true){if(!CanControl)return false;tutorialFreezesGame=freeze;interaction?.grabber?.Drop();SetState(GameState.Tutorial);return true;}
        public void EndTutorial(){if(State==GameState.Tutorial)SetState(GameState.Playing);}
        void SetState(GameState value)
        {
            State = value;
            if (value == GameState.Playing)
                controlAvailableFrame = Time.frameCount;
            bool paused = value == GameState.Paused || value == GameState.GameOver;
            Time.timeScale = paused || value==GameState.Tutorial&&tutorialFreezesGame ? 0 : 1;
            AudioListener.pause = paused;
            input?.EnableGameplay(value == GameState.Playing);
            Cursor.lockState = value == GameState.Playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = value != GameState.Playing;
            if (value == GameState.Cutscene || value == GameState.BlackingOut)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            StateChanged?.Invoke(value);
            hud?.RefreshState(value);
        }

        public void DisableControl()
        {
            if (State == GameState.Playing)
            {
                interaction?.grabber?.Drop();
                if (player) player.HoldingBreath = false;
                SetState(GameState.Cutscene);
            }
        }

        public void EnableControl()
        {
            if (IsBlackingOut) return;
            if (State == GameState.Cutscene)
            {
                if (player && player.view) player.view.ClearOverride();
                SetState(GameState.Playing);
            }
            else if (State == GameState.Paused && stateBeforePause == GameState.Cutscene)
                stateBeforePause = GameState.Playing;
        }

        public void PlayCutscene(Transform focus, float duration, float shakeStrength = .045f)
        {
            if (State != GameState.Playing)
                return;
            if (cutscene != null)
                StopCoroutine(cutscene);
            cutscene = StartCoroutine(Cutscene(focus, duration, shakeStrength));
        }

        IEnumerator Cutscene(Transform focus, float duration, float strength)
        {
            DisableControl();
            if (focus)
                player.view.OverridePose(focus);
            player.view.Shake(strength, duration);
            yield return new WaitForSeconds(Mathf.Max(.05f, duration));
            cutscene = null;
            if (State == GameState.Cutscene)
                EnableControl();
        }

        public void Kill()
        {
            if (State == GameState.GameOver)
                return;
            if (cutscene != null)
            {
                StopCoroutine(cutscene);
                cutscene = null;
            }

            interaction?.grabber?.Drop();
            if (interaction && interaction.CurrentHidingSpot)
                interaction.CurrentHidingSpot.Exit(true);
            if (player && player.view) player.view.ClearOverride();
            if (narrative) narrative.ClearSubtitle();
            AudioManager.Instance?.Play(deathSound, player ? player.transform.position : transform.position, .9f, AudioChannel.SFX, false, true);
            SetState(GameState.GameOver);
            if (narrative && hud) hud.ShowSubtitle("Você nunca deveria ter voltado.", 4);
        }

        /// <summary>A non-lethal recovery state; pending saves and school events cannot advance.</summary>
        public void BeginBlackout(string caption)
        {
            if (IsBlackingOut || State == GameState.GameOver || State == GameState.StoryScreen) return;
            IsBlackingOut = true;
            if (cutscene != null) { StopCoroutine(cutscene); cutscene = null; }
            interaction?.grabber?.Drop();
            if (interaction && interaction.CurrentHidingSpot) interaction.CurrentHidingSpot.Exit(true);
            if (player && player.view) player.view.ClearOverride();
            SetState(GameState.BlackingOut);
            narrative?.SuspendForBlackout();
            if (hud)
            {
                hud.ShowSubtitle(caption, float.MaxValue);
                if (hud.subtitleGroup) hud.subtitleGroup.alpha = 1;
            }
        }

        public void EndBlackout()
        {
            if (!IsBlackingOut) return;
            IsBlackingOut = false;
            SetState(GameState.Playing);
        }

        public void CompleteDarknessDeath()
        {
            if (!IsBlackingOut) return;
            // Keep the failed position out of the checkpoint and release the retry button's lock.
            darknessDeathPending = true;
            IsBlackingOut = false;
            Kill();
        }

        /// <summary>Reload a prepared snapshot through the normal checkpoint startup path.</summary>
        public bool ReloadCurrentCheckpoint()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (!Application.CanStreamedLevelBeLoaded(scene)) return false;
            save?.StageCurrentResume();
            PlayerPrefs.SetInt(ProgressKey("ResumeRequested"), 1);
            PlayerPrefs.SetInt(ProgressKey("StoryCarryRequested"), 0);
            PlayerPrefs.Save();
            LoadScene(scene);
            return true;
        }

        public void RestartCheckpoint()
        {
            if (IsBlackingOut || (State != GameState.GameOver && State != GameState.Paused))
                return;
            if (!save || !save.TryReadCurrent(out var checkpoint)
                || !Application.CanStreamedLevelBeLoaded(checkpoint.scene))
            {
                hud?.Notify("Não foi possível ler o checkpoint.");
                return;
            }
            if (darknessDeathPending)
            {
                var sanity = FindAnyObjectByType<SchoolSanityController>();
                if (sanity) checkpoint = sanity.PrepareDarknessRetrySnapshot(checkpoint);
            }
            checkpoint = BeforeDemolitionMirrorFlow.PrepareChoiceRetrySnapshot(checkpoint);
            if (checkpoint == null || !save.SaveSnapshot(checkpoint) || !ReloadCurrentCheckpoint())
                hud?.Notify("Não foi possível ler o checkpoint.");
        }

        public void ShowStoryScreen()
        {
            interaction?.grabber?.Drop();
            if (interaction && interaction.CurrentHidingSpot) interaction.CurrentHidingSpot.Exit(true);
            if (player && player.view) player.view.ClearOverride();
            SetState(GameState.StoryScreen);
        }

        /// <summary>Opt-in chapter continuation in the current scene, with the world intact.</summary>
        public void ResumeStoryChapter()
        {
            if (State == GameState.StoryScreen) SetState(GameState.Playing);
        }

        public bool LoadStoryChapter(string scene)
        {
            if (SceneTransitionService.ControlsBlocked || string.IsNullOrWhiteSpace(scene) || !Application.CanStreamedLevelBeLoaded(scene)) return false;
            if (!save || !save.StageChapterCarry(scene))
            {
                hud?.Notify("Não foi possível preparar o próximo capítulo. Seu checkpoint foi preservado.");
                return false;
            }
            PlayerPrefs.SetInt(ProgressKey("ResumeRequested"), 0);
            PlayerPrefs.SetInt(ProgressKey("StoryCarryRequested"), 1);
            PlayerPrefs.Save();
            LoadScene(scene);
            return true;
        }

        public void ReturnToMenu()
        {
            if (scenes && Application.CanStreamedLevelBeLoaded(scenes.mainMenuScene))
            {
                SaveSystem.ClearChapterCarry();
                LoadScene(scenes.mainMenuScene);
            }
            else
                hud?.Notify("Configure Main Menu Scene no Scene Catalog e na lista de cenas.");
        }

        void LoadScene(string scene)
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            LoadingMetrics.Request(scene);
            SceneTransitionService.Load(scene);
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1;
                AudioListener.pause = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
