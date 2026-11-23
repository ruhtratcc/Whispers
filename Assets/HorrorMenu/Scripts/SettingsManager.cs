// Aplica e salva volume, sensibilidade e opções de tela.

using System;
using System.Linq;
using UnityEngine;
using TMPro;

namespace SchoolMenu
{
    public sealed class SettingsManager : MonoBehaviour
    {
        public const string Prefix = "HorrorMenu.";
        [SerializeField]
        public UnityEngine.UI.Slider master, music, sfx, sensitivity;
        [SerializeField]
        public MenuChoice resolution, fullscreen, quality, invert;
        [Header("Language preview (English translation is not available yet)")]
        [SerializeField]
        public MenuChoice language;
        [SerializeField]
        public TMP_Text languageValue, languageNotice;
        [SerializeField]
        public TMP_Text masterValue, musicValue, sfxValue, sensitivityValue, resolutionValue, fullscreenValue, qualityValue, invertValue;
        [SerializeField]
        public MenuAudio audioManager;
        [SerializeField]
        float defaultMaster = .8f, defaultSfx = .8f, defaultSensitivity = 1f;
        Vector2Int[] sizes;
        int sizeIndex;
        bool loading;
        public static float MouseSensitivity => PlayerPrefs.GetFloat(Prefix + "Sensitivity", 1f);
        public static bool InvertY => PlayerPrefs.GetInt(Prefix + "InvertY", 0) == 1;
        // This preference changes only the language selector, never the game's text.
        public static int LanguagePreview => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "LanguagePreview", 0), 0, 1);

        void Start()
        {
            sizes = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Append(new Vector2Int(Screen.width, Screen.height)).Distinct().OrderBy(r => r.x).ThenBy(r => r.y).ToArray();
            int width = PlayerPrefs.GetInt(Prefix + "Width", Screen.width), height = PlayerPrefs.GetInt(Prefix + "Height", Screen.height);
            sizeIndex = Array.FindIndex(sizes, s => s.x == width && s.y == height);
            if (sizeIndex < 0)
                sizeIndex = sizes.Length - 1;
            loading = true;
            master.SetValueWithoutNotify(Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Master", defaultMaster)));
            music.SetValueWithoutNotify(Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Music", HorrorGame.PresentationSettings.DefaultMusic)));
            sfx.SetValueWithoutNotify(Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "SFX", defaultSfx)));
            sensitivity.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "Sensitivity", defaultSensitivity), sensitivity.minValue, sensitivity.maxValue));
            loading = false;
            master.onValueChanged.AddListener(v => Save("Master", v));
            music.onValueChanged.AddListener(v => Save("Music", v));
            sfx.onValueChanged.AddListener(v => Save("SFX", v));
            sensitivity.onValueChanged.AddListener(v => Save("Sensitivity", v));
            resolution.Step += StepResolution;
            fullscreen.Step += StepFullscreen;
            quality.Step += StepQuality;
            invert.Step += StepInvert;
            if (language)
                language.Step += StepLanguage;
            if (PlayerPrefs.HasKey(Prefix + "Quality"))
                QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Quality"), 0, QualitySettings.names.Length - 1), true);
            if (PlayerPrefs.HasKey(Prefix + "Width"))
                ApplyDisplay();
            else if (PlayerPrefs.HasKey(Prefix + "Fullscreen"))
                Screen.fullScreen = PlayerPrefs.GetInt(Prefix + "Fullscreen") == 1;
            ApplySound();
            Labels();
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            if (menu)
                HorrorGame.AccessibilityPanel.Install(menu.settingsPanel, masterValue.font);
        }

        void Save(string key, float value)
        {
            if (loading)
                return;
            PlayerPrefs.SetFloat(Prefix + key, value);
            PlayerPrefs.Save();
            ApplySound();
            Labels();
        }

        void ApplySound()
        {
            AudioListener.volume = master.value;
            if (audioManager)
                audioManager.Levels(sfx.value, music.value);
        }

        public void Refresh()
        {
            HorrorGame.PresentationSettings.Reload();
            if (sizes != null)
                Labels();
        }

        void Labels()
        {
            masterValue.text = Mathf.RoundToInt(master.value * 100).ToString();
            musicValue.text = Mathf.RoundToInt(music.value * 100).ToString();
            sfxValue.text = Mathf.RoundToInt(sfx.value * 100).ToString();
            sensitivityValue.text = sensitivity.value.ToString("0.00");
            resolutionValue.text = sizes[sizeIndex].x + " x " + sizes[sizeIndex].y;
            fullscreenValue.text = PlayerPrefs.GetInt(Prefix + "Fullscreen", Screen.fullScreen ? 1 : 0) == 1 ? "ON" : "OFF";
            qualityValue.text = QualitySettings.names[QualitySettings.GetQualityLevel()].ToUpperInvariant();
            invertValue.text = InvertY ? "ON" : "OFF";
            RefreshLanguagePreview();
        }

        public void RefreshLanguagePreview()
        {
            if (languageValue)
                languageValue.text = LanguagePreview == 0 ? "Português" : "English";
            if (languageNotice)
                languageNotice.text = LanguagePreview == 0 ? "" : "Tradução disponível em breve.";
        }

        void StepLanguage(int direction)
        {
            if (direction == 0)
                return;
            int next = (LanguagePreview + (direction > 0 ? 1 : -1) + 2) % 2;
            PlayerPrefs.SetInt(Prefix + "LanguagePreview", next);
            PlayerPrefs.Save();
            RefreshLanguagePreview();
            if (audioManager)
                audioManager.Hover();
        }

        void StepResolution(int d)
        {
            sizeIndex = (sizeIndex + d + sizes.Length) % sizes.Length;
            PlayerPrefs.SetInt(Prefix + "Width", sizes[sizeIndex].x);
            PlayerPrefs.SetInt(Prefix + "Height", sizes[sizeIndex].y);
            ApplyDisplay();
            Commit();
        }

        void StepFullscreen(int d)
        {
            PlayerPrefs.SetInt(Prefix + "Fullscreen", 1 - PlayerPrefs.GetInt(Prefix + "Fullscreen", Screen.fullScreen ? 1 : 0));
            ApplyDisplay();
            Commit();
        }

        void ApplyDisplay()
        {
            Screen.SetResolution(sizes[sizeIndex].x, sizes[sizeIndex].y, PlayerPrefs.GetInt(Prefix + "Fullscreen", Screen.fullScreen ? 1 : 0) == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        void StepQuality(int d)
        {
            int count = QualitySettings.names.Length;
            if (count == 0)
                return;
            int next = (QualitySettings.GetQualityLevel() + d + count) % count;
            QualitySettings.SetQualityLevel(next, true);
            PlayerPrefs.SetInt(Prefix + "Quality", next);
            Commit();
        }

        void StepInvert(int d)
        {
            PlayerPrefs.SetInt(Prefix + "InvertY", InvertY ? 0 : 1);
            Commit();
        }

        void Commit()
        {
            PlayerPrefs.Save();
            Labels();
            if (audioManager)
                audioManager.Hover();
        }
    }
}
