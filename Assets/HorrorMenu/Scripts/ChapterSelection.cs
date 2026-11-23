// Exibe e confirma o início do capítulo.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SchoolMenu
{
    public sealed class ChapterSelection : MonoBehaviour
    {
        [SerializeField] public MenuAtmosphere atmosphere;
        [SerializeField] public CanvasGroup menuGroup, chapterGroup;
        [SerializeField] public UnityEngine.UI.Button chapterButton, returnButton;
        [SerializeField] public TMP_Text chapterStatus;
        [SerializeField, Min(.1f)] float transitionDuration = 2.4f;
        [SerializeField, Min(0)] float forwardDistance = 5.5f;
        public bool IsOpen { get; private set; }

        void Awake()
        {
            menuGroup.alpha = 1;
            menuGroup.interactable = menuGroup.blocksRaycasts = true;
            chapterGroup.alpha = 0;
            chapterGroup.interactable = chapterGroup.blocksRaycasts = false;
            chapterGroup.gameObject.SetActive(false);
            atmosphere.PresentationOffset = Vector3.zero;
            var menu = GetComponent<MainMenuController>();
            chapterButton.onClick.AddListener(menu.StartChapter);
            returnButton.onClick.AddListener(menu.ReturnToMain);
        }

        public IEnumerator Show()
        {
            IsOpen = true;
            chapterStatus.text = "";
            chapterGroup.gameObject.SetActive(true);
            yield return Travel(true);
            chapterGroup.interactable = chapterGroup.blocksRaycasts = true;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(chapterButton.gameObject);
        }

        public IEnumerator Hide()
        {
            yield return Travel(false);
            IsOpen = false;
            chapterGroup.gameObject.SetActive(false);
            menuGroup.interactable = menuGroup.blocksRaycasts = true;
        }

        IEnumerator Travel(bool forward)
        {
            menuGroup.interactable = menuGroup.blocksRaycasts = false;
            chapterGroup.interactable = chapterGroup.blocksRaycasts = false;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            Vector3 from = atmosphere.PresentationOffset;
            Vector3 to = forward ? Vector3.forward * forwardDistance : Vector3.zero;
            float elapsed = 0;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / transitionDuration);
                float ease = progress * progress * (3f - 2f * progress);
                atmosphere.PresentationOffset = Vector3.LerpUnclamped(from, to, ease);
                if (forward)
                {
                    menuGroup.alpha = 1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / .35f));
                    chapterGroup.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((progress - .48f) / .52f));
                }
                else
                {
                    chapterGroup.alpha = 1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / .35f));
                    menuGroup.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((progress - .48f) / .52f));
                }
                yield return null;
            }
            atmosphere.PresentationOffset = to;
            menuGroup.alpha = forward ? 0 : 1;
            chapterGroup.alpha = forward ? 1 : 0;
        }

        public void ShowUnavailable()
        {
            chapterStatus.text = "Este capítulo ainda está em desenvolvimento.";
        }
    }
}
