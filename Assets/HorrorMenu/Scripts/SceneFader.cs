// Mostra o fade e os cartões de introdução de Novo Jogo.

using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
namespace SchoolMenu {
public sealed class SceneFader : MonoBehaviour {
 [SerializeField] public CanvasGroup curtain;
 [SerializeField] public TMP_Text line;
 [SerializeField] public float introDuration=2.6f, exitDuration=1.15f;
 static readonly string[] NewGameIntro =
 {
  "Curitiba, 2006.\nQuatro alunos da 7ª B desaparecem do SEEPE.",
  "Vinte anos depois, a escola está prestes a ser demolida.\nO caso continua sem resposta.",
  "João de Claus investigou os desaparecimentos.\nNaquela noite, os emails de Helena voltam a chamar sua atenção."
 };
 public IEnumerator PlayNewGameIntro()
 {
  if (!curtain || !line) { yield return Fade(1, .35f); yield break; }

  var rect = line.rectTransform;
  var oldSize = rect.sizeDelta;
  var oldPosition = rect.anchoredPosition;
  float oldSizeOfFont = line.fontSize;
  line.fontSize = 32;
  rect.sizeDelta = new Vector2(940, 190);
  rect.anchoredPosition = Vector2.zero;

  var hintObject = new GameObject("Pular introdução", typeof(RectTransform), typeof(TextMeshProUGUI));
  var hint = hintObject.GetComponent<TextMeshProUGUI>();
  var hintRect = hint.rectTransform;
  hintRect.SetParent(curtain.transform, false);
  hintRect.anchorMin = hintRect.anchorMax = new Vector2(.5f, .08f);
  hintRect.sizeDelta = new Vector2(700, 42);
  hint.font = line.font;
  hint.fontSize = 20;
  hint.color = new Color(.62f, .59f, .55f);
  hint.alignment = TextAlignmentOptions.Center;
  hint.text = "Aperte qualquer botão para pular";
  hint.raycastTarget = false;

  // The event that selected the chapter ran before this subscription.
  bool skip = false;
  using (InputSystem.onAnyButtonPress.Call(_ => skip = true))
  {
   yield return Fade(1, .35f);
   foreach (string page in NewGameIntro)
   {
    if (skip) break;
    line.text = page;
    for (float elapsed = 0; elapsed < 5.5f && !skip; elapsed += Time.unscaledDeltaTime)
    {
     line.alpha = HorrorGame.PresentationSettings.ReduceEffects ? 1 :
      Mathf.Min(Mathf.Clamp01(elapsed / .35f), Mathf.Clamp01((5.5f - elapsed) / .45f));
     yield return null;
    }
   }
  }

  line.text = "";
  line.alpha = 1;
  line.fontSize = oldSizeOfFont;
  rect.sizeDelta = oldSize;
  rect.anchoredPosition = oldPosition;
  Destroy(hintObject);
 }
 public IEnumerator Fade(float target,float duration,string message=""){
  if(!curtain)yield break;
  if(line)line.text=message;
  curtain.blocksRaycasts=true;float from=curtain.alpha,elapsed=0;
  while(elapsed<duration){elapsed+=Time.unscaledDeltaTime;curtain.alpha=Mathf.Lerp(from,target,Mathf.SmoothStep(0,1,elapsed/Mathf.Max(.01f,duration)));yield return null;}
  curtain.alpha=target;curtain.blocksRaycasts=target>.01f;
 }
}
}