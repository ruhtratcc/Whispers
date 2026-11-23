// Ferramenta de montagem da estrada, do carro ou do rádio.

using System;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorGame.Editor
{
    /// <summary>Physical markings share each existing button's unscaled interaction target.</summary>
    public static class RoadRadioButtonsPass
    {
        const string Folder = "Assets/RoadChapter/Data/RadioButtons";
        const float ButtonScale = .48f;
        public const string VisualName = "Botão físico do rádio";

        public static int Apply(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Pare o Play antes de editar o rádio.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/RoadChapter/Data", "RadioButtons");
            var face = Material("ABS", new Color(.07f,.078f,.075f), .12f, .20f);
            var rim = Material("Metal", new Color(.28f,.30f,.28f), .65f, .30f);
            var ink = Material("Marcas", new Color(.79f,.77f,.65f), .05f, .15f);
            int count=0;
            foreach(var target in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BeforeDemolitionRoadInteraction>(true)))
            {
                if (!target.gameObject.activeInHierarchy || !target.enabled || !IsButton(target.action)) continue;
                var old=target.transform.Find(VisualName); if(old) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var oldLabel=target.transform.Find("Rótulo"); if(oldLabel) oldLabel.gameObject.SetActive(false);
                var oldRenderer=target.GetComponent<MeshRenderer>(); if(oldRenderer) oldRenderer.enabled=false;
                var car=target.flow.driver.carRoot;
                int index=target.action==BeforeDemolitionRoadAction.RadioPower?0:target.action==BeforeDemolitionRoadAction.RadioStation?1:target.action==BeforeDemolitionRoadAction.RadioVolumeDown?2:3;
                // Surface measured on the installed taxicab dashboard, not the earlier sedan's anchor.
                var normal=new Vector3(0,.52634f,-.85027f);
                target.transform.position=car.TransformPoint(new Vector3(-.062f+index*.0347f,.888f,.462f)+normal*.0025f);
                target.transform.rotation=car.rotation*Quaternion.Euler(31.76f,0,0);
                target.transform.localScale=Vector3.one;
                var collider=target.GetComponent<BoxCollider>(); if(!collider) collider=target.gameObject.AddComponent<BoxCollider>();
                collider.center=new Vector3(0,0,-.004f)*ButtonScale; collider.size=new Vector3(.064f,.042f,.045f)*ButtonScale; collider.isTrigger=false;
                var root=new GameObject(VisualName).transform;root.SetParent(target.transform,false);root.localScale=Vector3.one*ButtonScale;
                Box(root,"Aro metálico",new Vector3(0,0,-.002f),new Vector3(.062f,.039f,.022f),rim);
                Box(root,"Tecla em relevo",new Vector3(0,0,-.015f),new Vector3(.053f,.030f,.011f),face);
                if(target.action==BeforeDemolitionRoadAction.RadioPower)
                {
                    Disk(root,"Anel de energia",Vector3.zero, .016f,-.0216f,ink);
                    Disk(root,"Centro do anel",Vector3.zero, .012f,-.0224f,face);
                    Box(root,"Abertura do anel",new Vector3(0,.007f,-.0233f),new Vector3(.007f,.006f,.0008f),face);
                    Box(root,"Traço de energia",new Vector3(0,.006f,-.0241f),new Vector3(.0018f,.009f,.0008f),ink);
                }
                else if(target.action==BeforeDemolitionRoadAction.RadioStation)
                    foreach(float x in new[]{-.005f,.005f})
                    { Segment(root,new Vector2(x-.003f,-.005f),new Vector2(x+.003f,0),ink);Segment(root,new Vector2(x+.003f,0),new Vector2(x-.003f,.005f),ink); }
                else
                {
                    Box(root,"Marca menos",new Vector3(0,0,-.0216f),new Vector3(.015f,.002f,.0008f),ink);
                    if(target.action==BeforeDemolitionRoadAction.RadioVolumeUp)
                        Box(root,"Marca mais",new Vector3(0,0,-.0221f),new Vector3(.002f,.014f,.0008f),ink);
                }
                EditorUtility.SetDirty(target);EditorUtility.SetDirty(collider);count++;
            }
            if(count!=4)throw new InvalidOperationException("Esperados quatro controles de rádio ativos; encontrados: "+count);
            return count;
        }
        static bool IsButton(BeforeDemolitionRoadAction action)=>action==BeforeDemolitionRoadAction.RadioPower||action==BeforeDemolitionRoadAction.RadioStation||action==BeforeDemolitionRoadAction.RadioVolumeDown||action==BeforeDemolitionRoadAction.RadioVolumeUp;
        static Material Material(string name,Color color,float metal,float smooth)
        {
            string path=Folder+"/"+name+".mat";var result=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!result){result=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(result,path);}
            result.SetColor("_BaseColor",color);result.SetFloat("_Metallic",metal);result.SetFloat("_Smoothness",smooth);
            result.DisableKeyword("_EMISSION");result.SetColor("_EmissionColor",Color.black);EditorUtility.SetDirty(result);return result;
        }
        static Transform Primitive(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go.transform;
        }
        static void Box(Transform p,string n,Vector3 pos,Vector3 size,Material m)=>Primitive(p,n,PrimitiveType.Cube,pos,size,m);
        static void Disk(Transform p,string n,Vector3 pos,float diameter,float z,Material m)
        {pos.z=z;Primitive(p,n,PrimitiveType.Cylinder,pos,new Vector3(diameter,.0003f,diameter),m).localRotation=Quaternion.Euler(90,0,0);}
        static void Segment(Transform p,Vector2 a,Vector2 b,Material m)
        {
            Vector2 d=b-a,mid=(a+b)*.5f;
            Primitive(p,"Marca de próxima faixa",PrimitiveType.Cube,new Vector3(mid.x,mid.y,-.0216f),new Vector3(d.magnitude,.0018f,.0008f),m).localRotation=Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
        }
    }
}
