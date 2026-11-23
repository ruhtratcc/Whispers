// Ferramenta de preparação da cena e da apresentação do menu.

using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
namespace SchoolMenu.Editor {
public static class MenuUIBuilder {
 const string Root=MenuBuilder.Root;
 static TMP_FontAsset font;
 static Color bone=new Color(.78f,.77f,.69f);
 static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size){
  var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=pos;r.sizeDelta=size;return r;
 }
 static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
 static TextMeshProUGUI Text(string name,Transform parent,string text,Vector2 pos,Vector2 size,float fontSize,Color? color=null){
  var r=Rect(name,parent,new Vector2(0,1),new Vector2(0,1),pos,size);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=fontSize;t.characterSpacing=7;t.color=color??bone;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Overflow;return t;
 }
 static UnityEngine.UI.Button Button(string name,Transform parent,string text,float height=46){
  var r=Rect(name,parent,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(420,height));
  var le=r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();le.preferredHeight=height;
  var t=Text("Label",r,text,new Vector2(0,-9),new Vector2(420,height),22);
  t.raycastTarget=true;var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=t;b.transition=UnityEngine.UI.Selectable.Transition.None;
  var fx=r.gameObject.AddComponent<SchoolMenu.MenuItemFX>();fx.label=t;return b;
 }
 static void Layout(RectTransform r,float spacing){
  var g=r.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();g.childAlignment=TextAnchor.UpperLeft;g.spacing=spacing;g.childControlWidth=true;g.childControlHeight=true;g.childForceExpandWidth=true;g.childForceExpandHeight=false;
 }
 static UnityEngine.UI.Image Image(string name,Transform parent,Vector2 pos,Vector2 size,Color color){
  var r=Rect(name,parent,new Vector2(0,1),new Vector2(0,1),pos,size);var i=r.gameObject.AddComponent<UnityEngine.UI.Image>();i.color=color;i.raycastTarget=false;return i;
 }
 static UnityEngine.UI.Slider Slider(string caption,Transform parent,float min,float max,out TMP_Text value){
  var r=Rect(caption,parent,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(600,46));r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=46;
  var label=Text("Label",r,caption,new Vector2(0,-10),new Vector2(255,36),18);
  // Transparent hit target belongs only to this row, never a visible button box.
  var hit=r.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=Color.clear;hit.raycastTarget=true;
  var slider=r.gameObject.AddComponent<UnityEngine.UI.Slider>();slider.minValue=min;slider.maxValue=max;slider.transition=UnityEngine.UI.Selectable.Transition.None;
  Image("Track",r,new Vector2(273,-22),new Vector2(198,1),new Color(.55f,.54f,.47f,.55f));
  var area=Rect("Handle area",r,new Vector2(0,1),new Vector2(0,1),new Vector2(273,-15),new Vector2(198,14));
  var handle=Image("Handle",area,Vector2.zero,new Vector2(6,8),bone);handle.rectTransform.pivot=new Vector2(.5f,.5f);handle.raycastTarget=true;
  slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.direction=UnityEngine.UI.Slider.Direction.LeftToRight;
  value=Text("Value",r,"",new Vector2(500,-10),new Vector2(90,35),17);r.gameObject.AddComponent<SchoolMenu.MenuItemFX>().label=label;return slider;
 }
 static SchoolMenu.MenuChoice Choice(string caption,Transform parent,out TMP_Text value){
  var r=Rect(caption,parent,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(600,46));r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=46;
  var hit=r.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=Color.clear;hit.raycastTarget=true;
  var label=Text("Label",r,caption,new Vector2(0,-10),new Vector2(270,36),18);
  value=Text("Value",r,"",new Vector2(273,-10),new Vector2(310,36),17);
  var b=r.gameObject.AddComponent<SchoolMenu.MenuChoice>();b.transition=UnityEngine.UI.Selectable.Transition.None;b.targetGraphic=hit;
  r.gameObject.AddComponent<SchoolMenu.MenuItemFX>().label=label;return b;
 }
 public static string Interface(){
  if(GameObject.Find("Menu interface"))throw new Exception("Interface already exists.");
  // Use the bundled TMP fallback, with restrained tracking; no downloads.
  font=TMPro.TMP_Settings.defaultFontAsset;
  var cg=new GameObject("Menu interface",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
  var canvas=cg.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
  var scaler=cg.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=1f;
  var menu=cg.AddComponent<SchoolMenu.MainMenuController>();
  var title=Text("Title",cg.transform,"AFTER THE BELL",new Vector2(100,-190),new Vector2(690,75),39);title.characterSpacing=13;menu.title=title;
  var tag=Text("Location",cg.transform,"WEST WING  /  23:47",new Vector2(103,-255),new Vector2(500,28),12,new Color(.47f,.48f,.41f));tag.characterSpacing=16;
  var main=Rect("Main options",cg.transform,new Vector2(0,0),new Vector2(0,0),new Vector2(103,178),new Vector2(430,214));Layout(main,7);
  menu.mainPanel=main.gameObject;
  menu.continueButton=Button("Continue",main,"CONTINUE");menu.newGameButton=Button("New game",main,"NEW GAME");menu.settingsButton=Button("Settings",main,"SETTINGS");menu.quitButton=Button("Quit",main,"QUIT");
  var settings=Rect("Settings options",cg.transform,new Vector2(0,0),new Vector2(0,0),new Vector2(103,130),new Vector2(610,449));Layout(settings,2);menu.settingsPanel=settings.gameObject;
  var sm=cg.AddComponent<SchoolMenu.SettingsManager>();menu.settingsManager=sm;
  sm.master=Slider("MASTER",settings,0,1,out sm.masterValue);sm.music=Slider("MUSIC",settings,0,1,out sm.musicValue);sm.sfx=Slider("SFX",settings,0,1,out sm.sfxValue);sm.sensitivity=Slider("MOUSE SENSITIVITY",settings,.1f,3f,out sm.sensitivityValue);
  sm.resolution=Choice("RESOLUTION",settings,out sm.resolutionValue);sm.fullscreen=Choice("FULLSCREEN",settings,out sm.fullscreenValue);sm.quality=Choice("QUALITY",settings,out sm.qualityValue);sm.invert=Choice("INVERT Y",settings,out sm.invertValue);menu.backButton=Button("Back",settings,"BACK",44);
  settings.gameObject.SetActive(false);
  menu.status=Text("Status",cg.transform,"",Vector2.zero,new Vector2(720,40),16,new Color(.64f,.62f,.53f));
  var sr=menu.status.rectTransform;sr.anchorMin=sr.anchorMax=Vector2.zero;sr.pivot=Vector2.zero;sr.anchoredPosition=new Vector2(103,80);
  var footer=Text("Navigation hint",cg.transform,"ARROWS / D-PAD   SELECT     ENTER / A   CONFIRM     ESC / B   BACK",Vector2.zero,new Vector2(1200,30),12,new Color(.40f,.42f,.36f));
  footer.rectTransform.anchorMin=footer.rectTransform.anchorMax=Vector2.zero;footer.rectTransform.pivot=Vector2.zero;footer.rectTransform.anchoredPosition=new Vector2(103,37);
  var scan=Rect("Faint scanlines",cg.transform,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);Stretch(scan);
  var tex=new Texture2D(2,4,TextureFormat.RGBA32,false);tex.filterMode=FilterMode.Point;tex.wrapMode=TextureWrapMode.Repeat;
  tex.SetPixels(new[]{new Color(0,0,0,.025f),new Color(0,0,0,.025f),Color.clear,Color.clear,Color.clear,Color.clear,Color.clear,Color.clear});tex.Apply();AssetDatabase.CreateAsset(tex,Root+"/Art/Scanlines.asset");
  var raw=scan.gameObject.AddComponent<UnityEngine.UI.RawImage>();raw.texture=tex;raw.uvRect=new Rect(0,0,1,225);raw.raycastTarget=false;
  var black=Image("Fade curtain",cg.transform,Vector2.zero,Vector2.zero,Color.black);Stretch(black.rectTransform);black.raycastTarget=true;
  var group=black.gameObject.AddComponent<CanvasGroup>();group.alpha=0;group.blocksRaycasts=false;
  var line=Text("Transition line",black.transform,"",new Vector2(0,-30),new Vector2(900,70),20);
  line.rectTransform.anchorMin=line.rectTransform.anchorMax=new Vector2(.5f,.5f);line.rectTransform.pivot=new Vector2(.5f,.5f);line.alignment=TextAlignmentOptions.Center;
  var fader=cg.AddComponent<SchoolMenu.SceneFader>();fader.curtain=group;fader.line=line;menu.fader=fader;
  var ev=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));ev.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
  // Explicit vertical navigation avoids accidentally selecting controls in a hidden panel.
  SetNavigation(main);SetNavigation(settings);
  EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
  return "TMP interface, settings controls, Input System navigation and fade curtain created.";
 }
 static void SetNavigation(Transform parent){
  var controls=parent.GetComponentsInChildren<UnityEngine.UI.Selectable>(true);
  for(int i=0;i<controls.Length;i++){var nav=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnUp=controls[(i-1+controls.Length)%controls.Length],selectOnDown=controls[(i+1)%controls.Length]};controls[i].navigation=nav;}
 }
 public static string Audio(){
  var root=new GameObject("Menu audio");var manager=root.AddComponent<SchoolMenu.MenuAudio>();
  manager.ambientSource=Source(root,"Room tone");manager.musicSource=Source(root,"Music (optional)");manager.effectsSource=Source(root,"Menu effects");manager.distantSource=Source(root,"Distant sounds");
  manager.ambientLoop=Wave("RoomTone",8,0);manager.hoverTick=Wave("HoverTick",.055f,1);manager.confirmThud=Wave("ConfirmThud",.30f,2);manager.distantSounds=new[]{Wave("DistantCreak",2.1f,3),Wave("DistantFootsteps",2.8f,4)};
  var menu=UnityEngine.Object.FindAnyObjectByType<SchoolMenu.MainMenuController>();menu.audioManager=manager;menu.settingsManager.audioManager=manager;
  EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();return "Generated restrained placeholder WAV clips and wired inspector audio fields.";
 }
 static AudioSource Source(GameObject parent,string name){var go=new GameObject(name);go.transform.SetParent(parent.transform);var a=go.AddComponent<AudioSource>();a.playOnAwake=false;a.spatialBlend=0;return a;}
 static AudioClip Wave(string name,float duration,int kind){
  int rate=22050,n=Mathf.RoundToInt(duration*rate);var random=new System.Random(137+kind);float low=0;
  string path=Root+"/Audio/"+name+".wav";using(var writer=new BinaryWriter(File.Create(path))){
   writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+n*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(n*2);
   for(int i=0;i<n;i++){float t=(float)i/rate;float noise=(float)(random.NextDouble()*2-1);low=Mathf.Lerp(low,noise,.022f);float sample=0;
    if(kind==0){float wrap=Mathf.Sin(Mathf.PI*t/duration);sample=(Mathf.Sin(2*Mathf.PI*60*t)*.14f+Mathf.Sin(2*Mathf.PI*120*t)*.04f+low*.35f)*wrap*wrap;}
    if(kind==1)sample=(noise*.3f+Mathf.Sin(2*Mathf.PI*730*t)*.16f)*Mathf.Exp(-t*95)*Mathf.Clamp01(t*1500);
    if(kind==2)sample=(Mathf.Sin(2*Mathf.PI*65*t)*.48f+low*.4f)*Mathf.Exp(-t*18)*Mathf.Clamp01(t*300);
    if(kind==3)sample=(Mathf.Sin(2*Mathf.PI*(165*t+7*Mathf.Sin(t*2)))*.13f+low*.4f)*Mathf.Pow(Mathf.Sin(Mathf.PI*t/duration),2);
    if(kind==4){foreach(float at in new[]{.1f,.84f,1.65f}){float dt=t-at;if(dt>=0)sample+=(Mathf.Sin(2*Mathf.PI*72*dt)*.32f+low*.6f)*Mathf.Exp(-dt*19)*Mathf.Clamp01(dt*150);}}
    writer.Write((short)(Mathf.Clamp(sample,-1,1)*32767));
   }
  }AssetDatabase.ImportAsset(path);return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
 }
}
}