// Guarda os ajustes de jogabilidade.

using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame
{
    public sealed class GameplayOptions : MonoBehaviour
    {
        [SerializeField]
        public Slider master, music, sfx, ambience, voice, sensitivity;
        [SerializeField]
        public Toggle invert, bob, sway, recording;
        [SerializeField]
        public FirstPersonCamera cameraMotion;
        [SerializeField]
        public RecordingOverlay overlay;
        [Header("Language preview")]
        [Tooltip("English is a preview choice only; the UI remains Portuguese until a translation exists.")]
        [SerializeField] public TMPro.TMP_Dropdown languagePreview;
        [SerializeField] public TMPro.TMP_Text languageNotice;
        bool refreshing;
        void Start()
        {
            if (languagePreview)
                languagePreview.onValueChanged.AddListener(PreviewLanguage);
            master.onValueChanged.AddListener(v => Save("Master", v));
            music.onValueChanged.AddListener(v => Save("Music", v));
            sfx.onValueChanged.AddListener(v => Save("SFX", v));
            ambience.onValueChanged.AddListener(v => Save("Ambience", v));
            voice.onValueChanged.AddListener(v => Save("Voice", v));
            sensitivity.onValueChanged.AddListener(v => Save("Sensitivity", v));
            invert.onValueChanged.AddListener(v =>
            {
                if (refreshing)
                    return;
                PlayerPrefs.SetInt("HorrorMenu.InvertY", v ? 1 : 0);
                PlayerPrefs.Save();
            });
            bob.onValueChanged.AddListener(v =>
            {
                if (refreshing)
                    return;
                cameraMotion.bobEnabled = v;
                PlayerPrefs.SetInt("Gameplay.Bob", v ? 1 : 0);
                PlayerPrefs.Save();
            });
            sway.onValueChanged.AddListener(v =>
            {
                if (refreshing)
                    return;
                cameraMotion.swayEnabled = v;
                PlayerPrefs.SetInt("Gameplay.Sway", v ? 1 : 0);
                PlayerPrefs.Save();
            });
            // Old serialized prefabs may still contain the retired recording control.
            if (recording) recording.gameObject.SetActive(false);
            if (GameSession.Instance && GameSession.Instance.hud)
                AccessibilityPanel.Install(GameSession.Instance.hud.optionsPanel, GameSession.Instance.hud.subtitle.font);
            Refresh();
        }

        void PreviewLanguage(int value)
        {
            if (refreshing || !languagePreview) return;
            value = Mathf.Clamp(value, 0, 1);
            PlayerPrefs.SetInt("HorrorMenu.LanguagePreview", value);
            PlayerPrefs.Save();
            if (languageNotice) languageNotice.text = value == 1 ? "Inglês: só uma prévia. A tradução ainda não está disponível." : "A tradução para inglês estará disponível em breve.";
        }

        void Save(string name, float value)
        {
            if (refreshing)
                return;
            PlayerPrefs.SetFloat("HorrorMenu." + name, value);
            PlayerPrefs.Save();
            AudioManager.Instance?.ApplySettings();
        }

        public void Refresh()
        {
            PresentationSettings.Reload();
            refreshing = true;
            if (languagePreview)
            {
                int language = Mathf.Clamp(PlayerPrefs.GetInt("HorrorMenu.LanguagePreview", 0), 0, 1);
                languagePreview.SetValueWithoutNotify(language);
                languagePreview.RefreshShownValue();
                if (languageNotice) languageNotice.text = language == 1 ? "Inglês: só uma prévia. A tradução ainda não está disponível." : "A tradução para inglês estará disponível em breve.";
            }
            master.SetValueWithoutNotify(PlayerPrefs.GetFloat("HorrorMenu.Master", .8f));
            music.SetValueWithoutNotify(PlayerPrefs.GetFloat("HorrorMenu.Music", PresentationSettings.DefaultMusic));
            float effect = PlayerPrefs.GetFloat("HorrorMenu.SFX", .8f);
            sfx.SetValueWithoutNotify(effect);
            ambience.SetValueWithoutNotify(PlayerPrefs.GetFloat("HorrorMenu.Ambience", effect));
            voice.SetValueWithoutNotify(PlayerPrefs.GetFloat("HorrorMenu.Voice", effect));
            sensitivity.SetValueWithoutNotify(PlayerPrefs.GetFloat("HorrorMenu.Sensitivity", 1));
            invert.SetIsOnWithoutNotify(PlayerPrefs.GetInt("HorrorMenu.InvertY", 0) == 1);
            bob.SetIsOnWithoutNotify(cameraMotion.bobEnabled);
            sway.SetIsOnWithoutNotify(cameraMotion.swayEnabled);
            refreshing = false;
        }
    }
}
