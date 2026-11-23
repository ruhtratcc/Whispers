// Ferramenta de preparação da cena e da apresentação do menu.

using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace SchoolMenu.Editor {
public static class MenuBuilder {
 public const string Root="Assets/HorrorMenu";
 static Transform env;
 public static void Folders(){foreach(var p in new[]{"","/Scripts","/Editor","/Art","/Audio","/Scenes","/Resources"})Directory.CreateDirectory(Root+p);AssetDatabase.Refresh();}
 static Material Mat(string name,Color c,float noise=.12f){
  string path=Root+"/Art/"+name+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing)return existing;
  var t=new Texture2D(64,64,TextureFormat.RGB24,false);t.name=name;t.filterMode=FilterMode.Point;t.wrapMode=TextureWrapMode.Repeat;
  var rand=new System.Random(name.Length*731);var pixels=new Color[4096];
  for(int y=0;y<64;y++)for(int x=0;x<64;x++){float n=1f+(float)(rand.NextDouble()-.5)*noise; if(name=="Floor"&&(x<1||y<1))n*=.55f; if(name=="Plaster"&&y>48)n*=.8f;pixels[y*64+x]=c*n;}
  t.SetPixels(pixels);t.Apply();AssetDatabase.CreateAsset(t,Root+"/Art/"+name+"Texture.asset");
  var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=name;m.SetTexture("_BaseMap",t);m.SetColor("_BaseColor",Color.white);m.SetFloat("_Smoothness",.08f);AssetDatabase.CreateAsset(m,path);return m;
 }
 public static GameObject Box(string name,Vector3 pos,Vector3 size,Material mat,Transform parent=null){
  var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent?parent:env,false);go.transform.localPosition=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
 }
 static Light Lamp(string name,Vector3 pos,Color color,float power,float range,bool shadow){
  var g=new GameObject(name);g.transform.SetParent(env);g.transform.position=pos;var l=g.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=power;l.range=range;l.shadows=shadow?LightShadows.Soft:LightShadows.None;return l;
 }
 public static string Environment(){
  Folders();
  if(AssetDatabase.IsValidFolder("Assets/TextMesh Pro")&&!AssetDatabase.IsValidFolder(Root+"/TMP")){var err=AssetDatabase.MoveAsset("Assets/TextMesh Pro",Root+"/TMP");if(!string.IsNullOrEmpty(err))throw new Exception(err);}
  if(File.Exists(Root+"/Scenes/MainMenu.unity"))throw new Exception("MainMenu already exists; use targeted edits.");
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  env=new GameObject("School corridor").transform;
  var plaster=Mat("Plaster",new Color(.42f,.40f,.32f),.32f);
  var paint=Mat("FadedPaint",new Color(.14f,.19f,.16f),.28f);
  var floor=Mat("Floor",new Color(.22f,.23f,.20f),.32f);
  var metal=Mat("LockerMetal",new Color(.23f,.28f,.25f),.24f);
  var dark=Mat("DarkMetal",new Color(.055f,.061f,.053f));
  var wood=Mat("OldWood",new Color(.23f,.18f,.12f),.42f);
  var paper=Mat("OldPaper",new Color(.47f,.44f,.33f),.22f);
  Box("Floor",new Vector3(0,-.12f,12),new Vector3(6,.2f,36),floor).GetComponent<Renderer>().sharedMaterial.mainTextureScale=new Vector2(6,36);
  Box("Ceiling",new Vector3(0,3.55f,12),new Vector3(6,.2f,36),plaster);
  for(int side=-1;side<=1;side+=2){
   Box("Upper wall",new Vector3(side*2.85f,2.5f,12),new Vector3(.18f,2.1f,36),plaster);
   Box("Lower wall",new Vector3(side*2.83f,.72f,12),new Vector3(.2f,1.44f,36),paint);
   Box("Dado rail",new Vector3(side*2.71f,1.47f,12),new Vector3(.1f,.045f,36),dark);
   Box("Skirting",new Vector3(side*2.70f,.09f,12),new Vector3(.12f,.16f,36),dark);
   for(int z=0;z<28;z+=6)Box("Column",new Vector3(side*2.68f,1.76f,z),new Vector3(.18f,3.5f,.2f),paint);
  }
  Box("End wall",new Vector3(0,1.75f,29),new Vector3(5.6f,3.5f,.2f),paint);
  Box("Far door",new Vector3(0,1.15f,28.8f),new Vector3(1.7f,2.3f,.14f),wood);
  Box("Far door window",new Vector3(0,1.68f,28.69f),new Vector3(.8f,.64f,.02f),dark);
  for(int i=0;i<15;i++){
   float z=1.6f+i*.68f;
   Box("Locker "+(i+1),new Vector3(2.40f,1.13f,z),new Vector3(.58f,2.12f,.65f),metal);
   Box("Door seam",new Vector3(2.102f,1.12f,z-.305f),new Vector3(.015f,2.04f,.016f),dark);
   Box("Latch",new Vector3(2.085f,1.12f,z+.18f),new Vector3(.055f,.16f,.035f),dark);
   for(int v=0;v<3;v++)Box("Vent",new Vector3(2.099f,1.80f-v*.065f,z),new Vector3(.014f,.018f,.34f),dark);
   Box("Label",new Vector3(2.085f,1.51f,z),new Vector3(.016f,.072f,.14f),paper);
  }
  for(int z=4;z<=22;z+=6){
   Box("Classroom frame",new Vector3(-2.69f,1.22f,z),new Vector3(.17f,2.5f,1.3f),dark);
   Box("Classroom door",new Vector3(-2.57f,1.17f,z),new Vector3(.10f,2.34f,1.13f),wood);
   Box("Door glass",new Vector3(-2.509f,1.65f,z),new Vector3(.012f,.65f,.63f),dark);
   Box("Door handle",new Vector3(-2.45f,1.0f,z-.4f),new Vector3(.11f,.035f,.17f),metal);
  }
  Box("Notice board",new Vector3(-2.66f,1.98f,.25f),new Vector3(.13f,1.0f,1.5f),wood);
  for(int i=0;i<5;i++)Box("Faded notice",new Vector3(-2.583f,1.8f+(i%2)*.28f,-.22f+i*.24f),new Vector3(.01f,.30f,.20f),paper);
  for(int i=0;i<6;i++){var p=Box("Dropped paper",new Vector3(.3f+Mathf.Sin(i*2)*1.3f,.003f,4+i*2.1f),new Vector3(.24f,.005f,.34f),paper);p.transform.localRotation=Quaternion.Euler(0,i*47,0);}
  var desk=new GameObject("Overturned desk").transform;desk.SetParent(env);desk.position=new Vector3(-1.8f,.53f,11);desk.rotation=Quaternion.Euler(12,22,77);
  Box("Desktop",new Vector3(0,.65f,0),new Vector3(1.0f,.09f,.6f),wood,desk);
  foreach(float x in new[]{-.4f,.4f})foreach(float z in new[]{-.23f,.23f})Box("Desk leg",new Vector3(x,.31f,z),new Vector3(.035f,.65f,.035f),dark,desk);
  var emission=new Material(Shader.Find("Universal Render Pipeline/Unlit"));emission.SetColor("_BaseColor",new Color(.78f,.80f,.61f));AssetDatabase.CreateAsset(emission,Root+"/Art/Fluorescent.mat");
  Renderer tube=null;
  for(int i=0;i<4;i++){float z=3+i*7;Box("Light housing",new Vector3(.2f,3.39f,z),new Vector3(1.3f,.12f,.24f),dark);var b=Box("Fluorescent tube",new Vector3(.2f,3.31f,z),new Vector3(1.15f,.035f,.1f),i==0?emission:metal);if(i==0)tube=b.GetComponent<Renderer>();}
  var key=Lamp("Failing fluorescent",new Vector3(.4f,2.95f,3.1f),new Color(.78f,.84f,.65f),6f,9,true);
  Lamp("Distant spill",new Vector3(-1.2f,2.8f,17),new Color(.72f,.74f,.58f),2.0f,8,true);
  Lamp("Near bounce",new Vector3(-1,2,-2),new Color(.61f,.65f,.53f),.20f,7,false);
  var cg=new GameObject("Menu Camera",typeof(Camera),typeof(AudioListener));cg.tag="MainCamera";cg.transform.position=new Vector3(-.55f,1.62f,-3.7f);cg.transform.rotation=Quaternion.Euler(1,-8.5f,.35f);
  var cam=cg.GetComponent<Camera>();cam.fieldOfView=57;cam.nearClipPlane=.1f;cam.farClipPlane=45;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.012f,.014f,.01f);
  cam.GetUniversalAdditionalCameraData().renderPostProcessing=true;
  var motion=cg.AddComponent<SchoolMenu.MenuAtmosphere>();motion.menuCamera=cam;motion.fluorescent=key;motion.tube=tube;
  RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.075f,.084f,.068f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.036f;RenderSettings.fogColor=new Color(.018f,.022f,.017f);RenderSettings.skybox=null;
  var vol=new GameObject("Menu film").AddComponent<Volume>();vol.isGlobal=true;var profile=ScriptableObject.CreateInstance<VolumeProfile>();
  var vig=profile.Add<Vignette>(true);vig.intensity.value=.40f;vig.smoothness.value=.55f;
  var grain=profile.Add<FilmGrain>(true);grain.type.value=FilmGrainLookup.Thin1;grain.intensity.value=.19f;grain.response.value=.65f;
  var ca=profile.Add<ChromaticAberration>(true);ca.intensity.value=.035f;
  var color=profile.Add<ColorAdjustments>(true);color.saturation.value=-24;color.contrast.value=11;
  AssetDatabase.CreateAsset(profile,Root+"/Art/MenuFilm.asset");vol.sharedProfile=profile;
  EditorSceneManager.SaveScene(scene,Root+"/Scenes/MainMenu.unity");AssetDatabase.SaveAssets();
  return "Environment created and saved.";
 }
}

}
