// Mostra os cartões e fades das trocas de capítulo.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame
{
    public sealed class ChapterTransitionView : MonoBehaviour
    {
        public GameSession game;
        public CanvasGroup group;
        public TMP_Text heading, body;
        public Button returnButton;
        [Min(.1f)] public float fadeSeconds = .8f;
        [Min(.1f)] public float cardSeconds = 2.5f;
        [Min(.1f)] public float endingTitleSeconds = 4f;
        public bool numberedCompletion;
        void OnEnable() { if (returnButton) returnButton.onClick.AddListener(ReturnToMenu); }
        void OnDisable() { if (returnButton) returnButton.onClick.RemoveListener(ReturnToMenu); }
        void ReturnToMenu() { if (game) game.ReturnToMenu(); }
        public IEnumerator Present(StoryChapterDefinition completed, StoryChapterDefinition next, bool ending)
        {
            if (returnButton) returnButton.gameObject.SetActive(false);
            if (group) { group.blocksRaycasts = true; group.interactable = false; group.alpha = 0; }
            if (ending)
            {
                if (heading) heading.text = "WHISPERS OF THE PAST";
                if (body) body.text = "";
                float endFade = 0;
                while (endFade < fadeSeconds)
                { endFade += Time.deltaTime; if (group) group.alpha = Mathf.Clamp01(endFade / fadeSeconds); yield return null; }
                yield return new WaitForSeconds(endingTitleSeconds);
                if (body) body.text = "FIM";
                yield break;
            }
            if (heading) heading.text = numberedCompletion && completed != null ? completed.heading + " CONCLUÍDO" : "CAPÍTULO CONCLUÍDO";
            if (body) body.text = completed == null ? "" : completed.heading + " — " + completed.title
                + (string.IsNullOrEmpty(completed.completionPhrase) ? "" : "\n\n" + completed.completionPhrase);
            float elapsed = 0;
            while (elapsed < fadeSeconds)
            { elapsed += Time.deltaTime; if (group) group.alpha = Mathf.Clamp01(elapsed / fadeSeconds); yield return null; }
            yield return new WaitForSeconds(cardSeconds);
            if (heading) heading.text = next?.heading ?? "";
            if (body) body.text = next?.title ?? "";
            yield return new WaitForSeconds(cardSeconds);
        }
        public void ShowUnavailable(StoryChapterDefinition next, bool ending)
        {
            if (group) { group.alpha = 1; group.blocksRaycasts = true; group.interactable = true; }
            if (!ending)
            {
                if (heading) heading.text = next?.heading ?? "";
                if (body) body.text = (next?.title ?? "") + "\n\nEsta parte da história ainda não está disponível.";
            }
            else if (body && body.text != "FIM")
            {
                if (heading) heading.text = "WHISPERS OF THE PAST";
                body.text = "FIM";
            }
            if (returnButton) returnButton.gameObject.SetActive(true);
        }
        public void Hide()
        {
            if (group) { group.alpha = 0; group.blocksRaycasts = false; group.interactable = false; }
            if (returnButton) returnButton.gameObject.SetActive(false);
        }
        public void ShowChapterEnd(StoryChapterDefinition completed, StoryChapterDefinition next)
        {
            if (group) { group.alpha = 1; group.blocksRaycasts = true; group.interactable = true; }
            if (heading) heading.text = (completed?.heading ?? "CAPÍTULO") + " CONCLUÍDO";
            if (body) body.text = (completed?.completionPhrase ?? "")
                + (next != null ? "\n\n" + next.heading + " — " + next.title : "");
            if (returnButton) returnButton.gameObject.SetActive(true);
        }
    }
}
