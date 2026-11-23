// Ferramenta de captura de imagem para revisão.

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorGame.Editor
{
    public static class WhispersPhotoCapture
    {
        const string Folder="Assets/ThirdParty/Images/";
        const string School="Assets/SchoolCEEP/Scenes/SchoolCEEP.unity";
        const string Office="Assets/ChapterOneOffice/Scenes/Chapter1Office.unity";
        [MenuItem("Tools/Capturar fotos/Preparar marcadores")]
        public static void PrepareMarkers()=>WithScenes(false);
        [MenuItem("Tools/Capturar fotos/Capturar e restaurar")]
        public static void CaptureAll()=>WithScenes(true);
        static void WithScenes(bool capture)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new InvalidOperationException("Pare o Play e aguarde a compilação.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Salve a cena antes da captura.");
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach(string path in new[]{Office,School})
                {
                    var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                    var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
                    bool office=path==Office;
                    var root=all.FirstOrDefault(t=>t.name=="Marcadores de fotos");
                    if(!root){Backup(path);root=new GameObject("Marcadores de fotos").transform;}
                    var game=all.Select(t=>t.GetComponent<GameSession>()).FirstOrDefault(g=>g);
                    if(!game||!game.player||!game.player.view)throw new InvalidOperationException("Câmera do jogador ausente.");
                    var camera=game.player.view.playerCamera;
                    if(office)
                    {
                        var cupboard=all.FirstOrDefault(t=>t.GetComponent<InteractiveDoor>()&&t.name.IndexOf("arm",StringComparison.OrdinalIgnoreCase)>=0);
                        Vector3 p=cupboard?cupboard.position+cupboard.forward*.1f+Vector3.up*1.3f:new Vector3(-3.4f,1.4f,1);
                        Marker(root,"foto_escritorio",p,game.player.transform.position+Vector3.up*1.1f);
                    }
                    else
                    {
                        var flow=game.GetComponent<BeforeDemolitionSchoolFlow>();
                        var door=flow.secretaryDoor.transform.position;
                        Marker(root,"foto_porta_secretaria",door+new Vector3(3.4f,1.5f,2.5f),door+Vector3.up);
                        var corridor=game.GetComponent<BeforeDemolitionTwoVoicesFlow>();
                        Vector3 point=corridor&&corridor.returnPose?corridor.returnPose.position:new Vector3(-15.45f,3.84f,63.3f);
                        Marker(root,"foto_corredor",point+Vector3.up*1.55f,point+Vector3.up*1.3f+Vector3.forward*8);
                    }
                    // Only marker transforms are saved; all capture props and cameras are disposable.
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                    if(capture)foreach(Transform marker in root)
                    {
                        if(office&&File.Exists("Assets/HorrorGameplay/Data/BeforeDemolition/foto_escritorio.png"))
                        {File.Copy("Assets/HorrorGameplay/Data/BeforeDemolition/foto_escritorio.png",Folder+"Incoming/foto_escritorio.png",true);continue;}
                        Capture(scene,camera,marker);
                    }
                }
            }
            finally
            {
                if(previous.Length==0)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                else EditorSceneManager.RestoreSceneManagerSetup(previous);
                AssetDatabase.Refresh();
            }
        }
        static void Marker(Transform root,string name,Vector3 position,Vector3 look)
        {
            var t=root.Find(name);if(!t){t=new GameObject(name).transform;t.SetParent(root,false);}t.position=position;t.rotation=Quaternion.LookRotation(look-position);
        }
        static void Backup(string path)
        {string p="Assets/_Substituidos/Imagens20261104/"+path+".bak";Directory.CreateDirectory(Path.GetDirectoryName(p));if(!File.Exists(p))File.Copy(path,p);}
        static void Capture(Scene scene,Camera original,Transform marker)
        {
            var go=new GameObject("Câmera de foto temporária",typeof(Camera));var camera=go.GetComponent<Camera>();camera.CopyFrom(original);camera.enabled=false;camera.transform.SetPositionAndRotation(marker.position,marker.rotation);camera.fieldOfView=49;
            GameObject silhouette=null;Material black=null;
            var canvases=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).ToArray();var enabled=canvases.Select(c=>c.enabled).ToArray();foreach(var canvas in canvases)canvas.enabled=false;
            try
            {
                if(marker.name=="foto_corredor"||marker.name=="foto_porta_secretaria")
                {
                    silhouette=new GameObject("Pessoa — somente captura");
                    black=new Material(Shader.Find("Universal Render Pipeline/Unlit"));black.SetColor("_BaseColor",new Color(.004f,.004f,.005f));
                    Vector3 p=marker.position+marker.forward*(marker.name=="foto_corredor"?6:2.7f);p.y=marker.position.y-1.45f;
                    if(Physics.Raycast(p+Vector3.up*1.5f,Vector3.down,out var floor,5,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))p.y=floor.point.y+.01f;
                    silhouette.transform.position=p;silhouette.transform.rotation=marker.rotation;
                    bool seated=marker.name=="foto_corredor";
                    Part(silhouette.transform,PrimitiveType.Capsule,new Vector3(0,seated?.91f:1.1f,0),new Vector3(.49f,seated?.36f:.56f,.3f),black);
                    Part(silhouette.transform,PrimitiveType.Sphere,new Vector3(0,seated?1.29f:1.65f,0),Vector3.one*.26f,black);
                    for(int side=-1;side<=1;side+=2){Part(silhouette.transform,PrimitiveType.Capsule,new Vector3(side*.25f,seated?.84f:1.12f,.03f),new Vector3(.15f,.31f,.15f),black);if(!seated)Part(silhouette.transform,PrimitiveType.Capsule,new Vector3(side*.105f,.4f,0),new Vector3(.19f,.4f,.2f),black);}
                    if(seated){Part(silhouette.transform,PrimitiveType.Cube,new Vector3(0,.48f,.2f),new Vector3(.4f,.16f,.55f),black);Part(silhouette.transform,PrimitiveType.Cube,new Vector3(0,.22f,.42f),new Vector3(.35f,.45f,.16f),black);Part(silhouette.transform,PrimitiveType.Cube,new Vector3(0,.45f,0),new Vector3(.6f,.06f,.6f),black);Part(silhouette.transform,PrimitiveType.Cube,new Vector3(0,.78f,-.3f),new Vector3(.6f,.65f,.05f),black);for(int side=-1;side<=1;side+=2)for(int depth=-1;depth<=1;depth+=2)Part(silhouette.transform,PrimitiveType.Cube,new Vector3(side*.25f,.23f,depth*.25f),new Vector3(.04f,.46f,.04f),black);Part(silhouette.transform,PrimitiveType.Cube,new Vector3(0,.75f,.85f),new Vector3(.65f,.045f,.5f),black);}
                }
                Directory.CreateDirectory(Folder+"Incoming");string target=Folder+"Incoming/"+marker.name+".png";
                PresentationCapture.Capture(scene,camera,target,1280,960);
                AgePhoto(target);
            }
            finally{for(int i=0;i<canvases.Length;i++)if(canvases[i])canvases[i].enabled=enabled[i];UnityEngine.Object.DestroyImmediate(go);if(silhouette)UnityEngine.Object.DestroyImmediate(silhouette);if(black)UnityEngine.Object.DestroyImmediate(black);}
        }
        static void Part(Transform parent,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {var go=GameObject.CreatePrimitive(type);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());}
        static void AgePhoto(string path)
        {
            var image=new Texture2D(2,2,TextureFormat.RGB24,false);image.LoadImage(File.ReadAllBytes(path));var pixels=image.GetPixels32();var random=new System.Random(307);
            for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)
            {
                int i=y*image.width+x;Color c=pixels[i];float dx=(x/(float)image.width-.5f)*2,dy=(y/(float)image.height-.5f)*2;float vignette=1-Mathf.Clamp01((dx*dx+dy*dy)*.24f);float noise=((float)random.NextDouble()-.5f)*.035f;float gray=c.grayscale;
                c=Color.Lerp(c,new Color(gray*1.04f,gray,gray*.91f),.35f)*vignette;c.r=Mathf.Clamp01(c.r+noise);c.g=Mathf.Clamp01(c.g+noise);c.b=Mathf.Clamp01(c.b+noise);pixels[i]=c;
            }
            image.SetPixels32(pixels);image.Apply();var encoded=image.EncodeToJPG(72);image.LoadImage(encoded);File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
