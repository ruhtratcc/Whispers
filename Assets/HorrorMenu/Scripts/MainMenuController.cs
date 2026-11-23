// Coordena Novo Jogo, Continuar, configurações e a saída.

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;
namespace SchoolMenu {
public sealed class MainMenuController : MonoBehaviour {
 [Header("Scene and save integration")]
 [SerializeField] string firstGameScene="";
 [SerializeField] HorrorGame.SceneCatalog gameplayScenes;
 string DefaultGameScene=>gameplayScenes?gameplayScenes.gameplayScene:firstGameScene;
 [SerializeField] string saveFileName="save.json";
 HorrorGame.StoryBook Book=>gameplayScenes?gameplayScenes.storyBook:null;
 string SavePath=>HorrorGame.SaveSystem.PathForFile(HorrorGame.BeforeDemolitionCampaign.FileName(Book,saveFileName));
 string ProgressKey(string suffix)=>HorrorGame.BeforeDemolitionCampaign.Key(Book,suffix);
 string StoryId=>Book?Book.storyId??"":"";
 [Header("Interface")]
 [SerializeField] public GameObject mainPanel,settingsPanel;
 [SerializeField] public UnityEngine.UI.Button continueButton,newGameButton,settingsButton,quitButton,backButton;
 [SerializeField] public TMP_Text title,status,settingsHeading;
 [SerializeField] public MenuAudio audioManager;
 [SerializeField] public SettingsManager settingsManager;
 [SerializeField] public SceneFader fader;
 [SerializeField] public ChapterSelection chapterSelection;
 [SerializeField] Vector2 titleFlickerInterval=new Vector2(20,40);
 [SerializeField] float titleFlickerDuration=.1f,statusDuration=5f;
 bool busy;float nextFlicker,flickerEnd,statusUntil; Vector2 titleHome;
 void Awake(){
  if(HorrorGame.SceneTransitionService.IsDiscarding(gameObject.scene.name)){enabled=false;return;}
  HorrorGame.SaveSystem.ClearChapterCarry();HorrorGame.SaveSystem.ClearResume();
  Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
  var input=UnityEngine.Object.FindAnyObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();if(input)input.AssignDefaultActions();
  if(fader&&fader.curtain){fader.curtain.alpha=1;fader.curtain.blocksRaycasts=true;}
  continueButton.gameObject.SetActive(HorrorGame.SaveSystem.TryReadCheckpoint(SavePath,out _,null,StoryId));
  var active=mainPanel.GetComponentsInChildren<UnityEngine.UI.Selectable>();for(int i=0;i<active.Length;i++){var navigation=active[i].navigation;navigation.selectOnUp=active[(i-1+active.Length)%active.Length];navigation.selectOnDown=active[(i+1)%active.Length];active[i].navigation=navigation;}
  newGameButton.onClick.AddListener(()=>BeginGame(false));continueButton.onClick.AddListener(()=>BeginGame(true));
  settingsButton.onClick.AddListener(()=>OpenSettings(true));backButton.onClick.AddListener(()=>OpenSettings(false));quitButton.onClick.AddListener(()=>{if(!busy)StartCoroutine(Exit());});
  settingsPanel.SetActive(false);mainPanel.SetActive(true);if(settingsHeading)settingsHeading.gameObject.SetActive(false);titleHome=title.rectTransform.anchoredPosition;
 }
 IEnumerator Start(){busy=true;nextFlicker=Time.unscaledTime+Random.Range(titleFlickerInterval.x,titleFlickerInterval.y);if(fader)yield return fader.Fade(0,HorrorGame.SceneTransitionService.ControlsBlocked?.35f:fader.introDuration);busy=false;HorrorGame.LoadingMetrics.SessionReady();SelectFirst();if(PlayerPrefs.GetInt("HorrorMenu.Calibrated",0)==0){OpenSettings(true);var panel=HorrorGame.AccessibilityPanel.Install(settingsPanel,settingsManager?settingsManager.masterValue.font:null);if(panel)panel.Show();}}
 void SelectFirst(){if(EventSystem.current)EventSystem.current.SetSelectedGameObject(continueButton.gameObject.activeSelf?continueButton.gameObject:newGameButton.gameObject);}
 public void OpenSettings(bool open){if(busy||(chapterSelection&&chapterSelection.IsOpen))return;if(audioManager)audioManager.Confirm();settingsPanel.SetActive(open);mainPanel.SetActive(!open);title.gameObject.SetActive(!open);if(settingsHeading)settingsHeading.gameObject.SetActive(open);if(open){if(settingsManager)settingsManager.Refresh();EventSystem.current.SetSelectedGameObject(settingsManager.master.gameObject);}else{EventSystem.current.SetSelectedGameObject(settingsButton.gameObject);}status.text="";}
 void Update(){
  if(HorrorGame.SceneTransitionService.ControlsBlocked)return;
  if(!busy&&((Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)||(Gamepad.current!=null&&Gamepad.current.buttonEast.wasPressedThisFrame))){if(HorrorGame.AccessibilityPanel.TryClose())return;if(chapterSelection&&chapterSelection.IsOpen)ReturnToMain();else if(settingsPanel.activeSelf)OpenSettings(false);else OpenSettings(true);}
  if(!busy&&EventSystem.current&&EventSystem.current.currentSelectedGameObject==null){bool nav=(Keyboard.current!=null&&(Keyboard.current.upArrowKey.wasPressedThisFrame||Keyboard.current.downArrowKey.wasPressedThisFrame))||(Gamepad.current!=null&&Gamepad.current.dpad.ReadValue().sqrMagnitude>.1f);if(nav){if(chapterSelection&&chapterSelection.IsOpen)EventSystem.current.SetSelectedGameObject(chapterSelection.chapterButton.gameObject);else if(settingsPanel.activeSelf)EventSystem.current.SetSelectedGameObject(settingsManager.master.gameObject);else SelectFirst();}}
  if(Time.unscaledTime>nextFlicker){flickerEnd=Time.unscaledTime+titleFlickerDuration;nextFlicker=Time.unscaledTime+Random.Range(titleFlickerInterval.x,titleFlickerInterval.y);}
  bool flicker=!HorrorGame.PresentationSettings.ReduceEffects&&Time.unscaledTime<flickerEnd;title.alpha=flicker?.48f:1f;title.rectTransform.anchoredPosition=titleHome+(flicker?Vector2.right:Vector2.zero);
  if(status&&Time.unscaledTime>statusUntil)status.text="";
 }
 public void BeginGame(bool resume){
  if(busy||HorrorGame.SceneTransitionService.ControlsBlocked)return;if(!resume&&chapterSelection){StartCoroutine(PresentChapter());return;}string scene=DefaultGameScene;
  if(resume){if(!HorrorGame.SaveSystem.TryReadCheckpoint(SavePath,out var checkpoint,null,StoryId)){status.text="Não foi possível ler o último ponto salvo. Seu salvamento foi preservado.";statusUntil=Time.unscaledTime+statusDuration;continueButton.gameObject.SetActive(false);SelectFirst();return;}scene=checkpoint.scene;HorrorGame.SaveSystem.StageResume(checkpoint,SavePath);}
  if(string.IsNullOrWhiteSpace(scene)||!Application.CanStreamedLevelBeLoaded(scene)){status.text="As portas ainda estão trancadas.";statusUntil=Time.unscaledTime+statusDuration;if(audioManager)audioManager.Confirm();return;}
  StartCoroutine(Load(scene,resume));
 }
 IEnumerator PresentChapter(){busy=true;if(audioManager)audioManager.Confirm();status.text="";yield return chapterSelection.Show();busy=false;}
 public void ReturnToMain(){if(busy||HorrorGame.SceneTransitionService.ControlsBlocked||!chapterSelection||!chapterSelection.IsOpen)return;StartCoroutine(ReturnFromChapter());}
 IEnumerator ReturnFromChapter(){busy=true;if(audioManager)audioManager.Confirm();yield return chapterSelection.Hide();busy=false;if(EventSystem.current)EventSystem.current.SetSelectedGameObject(newGameButton.gameObject);}
 public void StartChapter(){
  if(busy||HorrorGame.SceneTransitionService.ControlsBlocked||!chapterSelection||!chapterSelection.IsOpen)return;
  if(string.IsNullOrWhiteSpace(DefaultGameScene)||!Application.CanStreamedLevelBeLoaded(DefaultGameScene)){chapterSelection.ShowUnavailable();if(audioManager)audioManager.Confirm();return;}
  StartCoroutine(Load(DefaultGameScene,false));
 }
 IEnumerator Load(string scene,bool resume){
  HorrorGame.LoadingMetrics.Request(scene);
  HorrorGame.SceneTransitionService.Preload(scene);
  busy=true;if(audioManager)audioManager.Confirm();yield return FadeAudio();
  if(fader)
  {
   if(!resume && HorrorGame.BeforeDemolitionCampaign.Is(Book)) yield return fader.PlayNewGameIntro();
   else yield return fader.Fade(1,.35f,"Alguém deixou a luz acesa.");
  }
  // The gameplay save loader can read this flag; this menu never edits save data.
  PlayerPrefs.SetInt(ProgressKey("ResumeRequested"),resume?1:0);PlayerPrefs.SetInt(ProgressKey("StoryCarryRequested"),0);PlayerPrefs.Save();
  HorrorGame.SceneTransitionService.Load(scene,true);
 }
 IEnumerator FadeAudio(){float elapsed=0;while(elapsed<.18f){elapsed+=Time.unscaledDeltaTime;if(audioManager)audioManager.FadeLevel(Mathf.Lerp(1,0,elapsed/.18f));title.alpha=HorrorGame.PresentationSettings.ReduceEffects?1:Mathf.Sin(elapsed*90)>0?.4f:1;yield return null;}}
 IEnumerator Exit(){busy=true;if(audioManager)audioManager.Confirm();yield return FadeAudio();if(fader)yield return fader.Fade(1,fader.exitDuration);
  double flushUntil=Time.realtimeSinceStartupAsDouble+2;while(HorrorGame.SaveSystem.HasQueuedWrites&&Time.realtimeSinceStartupAsDouble<flushUntil)yield return null;
 #if UNITY_EDITOR
 UnityEditor.EditorApplication.isPlaying=false;
 #else
 Application.Quit();
 #endif
 }
}

}



