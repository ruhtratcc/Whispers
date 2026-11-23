// Verifica .

#if UNITY_EDITOR
using System;using System.IO;using System.Collections;using System.Collections.Generic;using System.Linq;
using NUnit.Framework;using UnityEngine;using UnityEngine.SceneManagement;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.TestTools;using Unity.Profiling;using UnityEngine.InputSystem;
namespace HorrorGame.Tests
{
 // Explicit capture uses disposable checkpoints before any scene can run Awake.
 [Category("ModelReplacementCapture"),Explicit("Requested before/after model-pass measurements only")]
 public sealed class ModelReplacementCaptureTests
 {
  const string W="C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/work/ModelPass20261030";
  string previous,temporary,saveScene;bool guarded,hadScene,background,audioPaused,cursorVisible;float timeScale;CursorLockMode cursorLock;InputSettings.BackgroundBehavior inputBackground;InputSettings.EditorInputBehaviorInPlayMode editorInput;
  readonly Dictionary<string,(bool had,int value)> prefs=new Dictionary<string,(bool,int)>();
  [Serializable]sealed class OfficeState{public EmailInboxState emails;}
  [Serializable]sealed class Sample{public string scene,stage,scope="Editor-observed render frame, fixed initial player camera; not standalone/per-camera isolation";public int width,height,startFrame,endFrame;public float fov;public long[] drawCalls,triangles,setPass;public string screenshot;}
  [UnitySetUp]public IEnumerator SetUp()
  {
   Assert.That(UnityEngine.Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None),Is.Empty,"Start from blank scene");
   previous=SaveSystem.TestSaveDirectory;temporary=Path.Combine(Path.GetTempPath(),"Whispers-ModelPass-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temporary);SaveSystem.TestSaveDirectory=temporary;background=Application.runInBackground;timeScale=Time.timeScale;guarded=true;
   foreach(string suffix in new[]{"ResumeRequested","StoryCarryRequested"}){string k="BeforeDemolition."+suffix;prefs[k]=(PlayerPrefs.HasKey(k),PlayerPrefs.GetInt(k));PlayerPrefs.SetInt(k,0);}
   hadScene=PlayerPrefs.HasKey("BeforeDemolition.SaveScene");saveScene=PlayerPrefs.GetString("BeforeDemolition.SaveScene");audioPaused=AudioListener.pause;cursorLock=Cursor.lockState;cursorVisible=Cursor.visible;AudioListener.pause=false;Application.runInBackground=true;Time.timeScale=1;inputBackground=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;SaveSystem.ClearChapterCarry();SceneManager.sceneLoaded+=Loaded;yield return null;
  }
  void Loaded(Scene s,LoadSceneMode mode)
  {foreach(var g in s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameSession>(true)))if(g.narrative&&g.narrative.transitionView){g.narrative.transitionView.fadeSeconds=.03f;g.narrative.transitionView.cardSeconds=.05f;}}
  SaveData Carry(bool school)
  {
   var cat=AssetDatabase.LoadAssetAtPath<EmailCatalog>("Assets/HorrorGameplay/Data/BeforeDemolition/BeforeDemolitionOfficeEmails.asset");var emails=EmailInboxState.Create(cat);if(school)emails.Deliver(cat,"email.08");foreach(var id in new[]{"email.01","email.06","email.07"})emails.MarkRead(cat,id);
   var d=new SaveData{scene=school?"RoadJourney":"Chapter1Office",battery=240,flashlightOn=school,stamina=100,narrative=new NarrativeProgress{storyId=BeforeDemolitionCampaign.StoryId,storyRevision=3,chapterId=school?BeforeDemolitionCampaign.RoadChapterId:BeforeDemolitionCampaign.OfficeChapterId,pendingChapterId=school?BeforeDemolitionCampaign.SchoolChapterId:BeforeDemolitionCampaign.RoadChapterId,milestones=new List<string>{BeforeDemolitionCampaign.ReadySignal,BeforeDemolitionCampaign.ExitSignal},completedChapters=new List<string>{BeforeDemolitionCampaign.OfficeChapterId}},inventory=new List<InventoryItem>{new InventoryItem{id=BeforeDemolitionCampaign.WhistleItemId,inspectionId=BeforeDemolitionCampaign.WhistleItemId,note=true}}};
   foreach(var id in emails.readIds)d.inventory.Add(new InventoryItem{id="bd.note."+id,note=true});
   if(school){d.narrative.completedChapters.Add(BeforeDemolitionCampaign.RoadChapterId);d.narrative.milestones.AddRange(BeforeDemolitionCampaign.RoadSignals);d.inventory.Add(new InventoryItem{id=BeforeDemolitionRoadFlow.EmailDeliveryItemId});d.objects.Add(new SavedObject{id=BeforeDemolitionRoadFlow.StateId,json=JsonUtility.ToJson(new BeforeDemolitionRoadState{phase=RoadRadioPhase.Parked,emails=emails})});}
   else d.objects.Add(new SavedObject{id=BeforeDemolitionCampaign.FlowStateId,json=JsonUtility.ToJson(new OfficeState{emails=emails})});return d;
  }
  IEnumerator Capture(string name,string asset,bool carry,bool school)
  {
   if(carry){File.WriteAllText(Path.Combine(temporary,BeforeDemolitionCampaign.CheckpointFile),JsonUtility.ToJson(Carry(school)));PlayerPrefs.SetInt("BeforeDemolition.StoryCarryRequested",1);}
   yield return EditorSceneManager.LoadSceneAsyncInPlayMode(asset,new LoadSceneParameters(LoadSceneMode.Single));
   float t=Time.realtimeSinceStartup;while((!GameSession.Instance||!GameSession.Instance.narrative.IsInitialized||!GameSession.Instance.CanControl)&&Time.realtimeSinceStartup-t<90)yield return null;
   var game=GameSession.Instance;Assert.That(game&&game.narrative.IsInitialized&&game.CanControl,Is.True,"Scene initialized");Assert.That(Path.GetDirectoryName(game.save.SavePath),Is.EqualTo(temporary).IgnoreCase);
   if(name=="RoadJourney"){var road=game.GetComponent<BeforeDemolitionRoadFlow>();road.driver.enabled=false;road.enabled=false;}
   game.player.view.bobEnabled=game.player.view.swayEnabled=false;
   string stage=File.ReadAllText(W+"/measurement-stage.txt").Trim();string folder=W+"/Frames/"+stage;Directory.CreateDirectory(folder);
   using(var dc=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",32))using(var tr=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",32))using(var sp=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count",32))
   {
    for(int i=0;i<10;i++)yield return null;int start=Time.frameCount;var draws=new List<long>();var tris=new List<long>();var sets=new List<long>();
    // The generic profiler draw-call marker stays zero with this GPU-driven setup; use the Editor render statistics source.
    for(int i=0;i<20;i++){yield return null;draws.Add(UnityEditor.UnityStats.drawCalls);if(tr.Valid)tris.Add(tr.LastValue);sets.Add(UnityEditor.UnityStats.setPassCalls);}
    Assert.That(Time.frameCount-start,Is.GreaterThanOrEqualTo(20));Assert.That(game.player.view.playerCamera.fieldOfView,Is.EqualTo(49).Within(.2f));
    string shot=folder+"/"+name+".png";ScreenCapture.CaptureScreenshot(shot);yield return null;yield return null;
    var sample=new Sample{scene=name,stage=stage,width=Screen.width,height=Screen.height,startFrame=start,endFrame=Time.frameCount,fov=game.player.view.playerCamera.fieldOfView,drawCalls=draws.ToArray(),triangles=tris.ToArray(),setPass=sets.ToArray(),screenshot=shot};File.WriteAllText(folder+"/"+name+".json",JsonUtility.ToJson(sample,true));
   }
   if(stage=="after")
   {
    bool viewEnabled=game.player.view.enabled;game.player.view.enabled=false;
    if(name=="Chapter1Office"){yield return Detail(game,folder,"office-chair",new Vector3(-1.4f,.04f,-1.35f),new Vector3(-.73f,.55f,-.3f));yield return Detail(game,folder,"office-lamp",new Vector3(1.6f,.04f,.6f),new Vector3(.86f,1.2f,1.4f));yield return Detail(game,folder,"office-clock",new Vector3(.24f,.04f,1.4f),new Vector3(.24f,2.66f,3.49f));}
    if(name=="SchoolCEEP")yield return Detail(game,folder,"school-bench",new Vector3(-4.0f,.04f,57.1f),new Vector3(-5.7378f,.5f,57.0794f));
    game.player.view.enabled=viewEnabled;
   }
  }
  static IEnumerator Detail(GameSession game,string folder,string label,Vector3 feet,Vector3 target)
  {var eyeOffset=game.player.view.playerCamera.transform.position-game.player.transform.position;var rotation=Quaternion.LookRotation(target-(feet+eyeOffset)).eulerAngles;float pitch=rotation.x>180?rotation.x-360:rotation.x;game.player.Warp(feet,rotation.y,pitch);game.player.view.playerCamera.transform.LookAt(target);yield return null;yield return null;ScreenCapture.CaptureScreenshot(folder+"/"+label+".png");yield return null;yield return null;}
  [UnityTest,Timeout(180000)]public IEnumerator OfficeRenderCounters()=>Capture("Chapter1Office","Assets/ChapterOneOffice/Scenes/Chapter1Office.unity",false,false);
  [UnityTest,Timeout(180000)]public IEnumerator RoadRenderCounters()=>Capture("RoadJourney","Assets/RoadChapter/Scenes/RoadJourney.unity",true,false);
  [UnityTest,Timeout(180000)]public IEnumerator SchoolRenderCounters()=>Capture("SchoolCEEP","Assets/SchoolCEEP/Scenes/SchoolCEEP.unity",true,true);
  [UnityTearDown]public IEnumerator TearDown()
  {
   if(!guarded)yield break;SceneManager.sceneLoaded-=Loaded;
   var loaded=new List<Scene>();for(int i=0;i<SceneManager.sceneCount;i++)loaded.Add(SceneManager.GetSceneAt(i));var blank=SceneManager.CreateScene("Model capture cleanup "+Guid.NewGuid().ToString("N"));SceneManager.SetActiveScene(blank);Time.timeScale=1;AudioListener.pause=false;
   foreach(var s in loaded)if(s.IsValid()&&s.isLoaded)yield return SceneManager.UnloadSceneAsync(s);
   SaveSystem.ClearChapterCarry();SaveSystem.TestSaveDirectory=previous;foreach(var p in prefs)if(p.Value.had)PlayerPrefs.SetInt(p.Key,p.Value.value);else PlayerPrefs.DeleteKey(p.Key);if(hadScene)PlayerPrefs.SetString("BeforeDemolition.SaveScene",saveScene);else PlayerPrefs.DeleteKey("BeforeDemolition.SaveScene");Application.runInBackground=background;Time.timeScale=timeScale;AudioListener.pause=audioPaused;Cursor.lockState=cursorLock;Cursor.visible=cursorVisible;InputSystem.settings.backgroundBehavior=inputBackground;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
   Assert.That(Path.GetFileName(temporary),Does.StartWith("Whispers-ModelPass-"));foreach(var f in Directory.GetFiles(temporary))File.Delete(f);Directory.Delete(temporary);guarded=false;
  }
 }
}
#endif
