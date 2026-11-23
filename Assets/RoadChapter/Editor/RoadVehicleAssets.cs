// Ferramenta de montagem da estrada, do carro ou do rádio.

using System;
using System.IO;
using System.Linq;
using HorrorGame;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame.Editor
{
    // The custom sedan and downloaded torch belong only to the road scene.
    public static class RoadVehicleAssets
    {
        const string TorchPath="Assets/RoadChapter/Models/QuaterniusTorch/Flashlight.obj";
        const string Data="Assets/RoadChapter/Data/";

        public static void Build(RoadJourneyFlow flow,GameSession game,Material body,Material metal,Material rubber,Material amber)
        {
            var car=new GameObject("Carro — João — sedã antigo").transform;flow.carRoot=car;
            var shell=car.gameObject.AddComponent<BoxCollider>();shell.center=new Vector3(0,.82f,.02f);shell.size=new Vector3(2.16f,1.64f,4.98f);
            DetectiveSedanBuilder.Build(car);
            flow.seatedPose=Anchor("Carro — olhos do motorista",new Vector3(-.43f,1.27f,-.28f),car);
            var handle=new GameObject("Carro — interação da porta");handle.transform.SetParent(car,false);
            handle.transform.localPosition=new Vector3(-.77f,1.04f,.02f);
            var doorTarget=handle.AddComponent<BoxCollider>();
            doorTarget.center=new Vector3(-.03f,-.16f,-.01f);
            doorTarget.size=new Vector3(.12f,.70f,1.30f);
            flow.driverDoor=handle.AddComponent<RoadJourneyInteraction>();flow.driverDoor.flow=flow;flow.driverDoor.kind=RoadJourneyInteractionKind.ExitCar;
            var phone=Cube("Carro — telefone",car,new Vector3(.12f,1.03f,.22f),new Vector3(.095f,.035f,.19f),rubber,true);
            Cube("Telefone — visor",car,new Vector3(.12f,1.051f,.23f),new Vector3(.074f,.004f,.08f),amber);
            flow.telephone=phone.AddComponent<RoadJourneyInteraction>();flow.telephone.flow=flow;flow.telephone.kind=RoadJourneyInteractionKind.AnswerPhone;
            var torch=new GameObject("Carro — lanterna no banco");torch.transform.SetParent(car,false);torch.transform.localPosition=new Vector3(.38f,1.04f,.03f);
            torch.AddComponent<BoxCollider>().size=new Vector3(.085f,.085f,.28f);
            Model(TorchPath,torch.transform,"Lanterna — Quaternius");
            flow.flashlightPickup=torch.AddComponent<RoadJourneyInteraction>();flow.flashlightPickup.flow=flow;flow.flashlightPickup.kind=RoadJourneyInteractionKind.TakeFlashlight;
            foreach(var renderer in flow.flashlight.handRig.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            Model(TorchPath,flow.flashlight.handRig,"Lanterna na mão — Quaternius");
            var cabin=new GameObject("Carro — luz suave da cabine").AddComponent<Light>();cabin.transform.SetParent(car,false);
            cabin.transform.localPosition=new Vector3(0,1.53f,-.12f);cabin.type=LightType.Point;cabin.color=new Color(.78f,.72f,.61f);
            cabin.intensity=3.2f;cabin.range=2.6f;cabin.shadows=LightShadows.None;
            for(int side=-1;side<=1;side+=2)
            {
                var lamp=new GameObject("Carro — feixe do farol").AddComponent<Light>();lamp.transform.SetParent(car,false);
                lamp.transform.localPosition=new Vector3(side*.70f,.782f,2.44f);lamp.transform.localRotation=Quaternion.Euler(7,0,0);
                lamp.type=LightType.Spot;lamp.color=new Color(.75f,.79f,.71f);lamp.intensity=34;lamp.range=52;
                lamp.spotAngle=48;lamp.innerSpotAngle=24;lamp.shadows=LightShadows.Soft;
            }
            var hum=car.gameObject.AddComponent<AudioSource>();hum.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RoadChapter/Audio/CarInterior.mp3");
            hum.loop=true;hum.playOnAwake=false;hum.spatialBlend=0;hum.volume=.14f;hum.outputAudioMixerGroup=game.GetComponentInChildren<AudioManager>().sfxGroup;
            flow.driveHum=hum;
        }

        static GameObject Model(string path,Transform parent,string name)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!source)throw new InvalidOperationException("Downloaded model missing: "+path);
            var model=UnityEngine.Object.Instantiate(source,parent,false);model.name=name;
            var definitions=JArray.Parse(File.ReadAllText(Path.ChangeExtension(path,".Materials.json")));
            var materials=new System.Collections.Generic.Dictionary<string,Material>();
            foreach(var definition in definitions)
            {
                string materialName=(string)definition["objMaterialName"];
                var pbr=definition["pbrMetallicRoughness"];var components=pbr?["baseColorFactor"];
                Color color=components!=null?new Color((float)components[0],(float)components[1],(float)components[2],1):new Color(.05f,.06f,.06f);
                float metallic=(float?)(pbr?["metallicFactor"])??0,roughness=(float?)(pbr?["roughnessFactor"])??.7f;
                string matPath=Data+"Torch_"+materialName+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,matPath);}
                material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",1-roughness);
                var emission=definition["emissiveFactor"];
                if(emission!=null)
                {
                    material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color((float)emission[0],(float)emission[1],(float)emission[2])*.3f);
                }
                material.SetFloat("_Cull",(float)CullMode.Off);materials[materialName]=material;EditorUtility.SetDirty(material);
            }
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m&&materials.TryGetValue(m.name.Split(' ')[0],out var replacement)?replacement:m).ToArray();
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            return model;
        }

        static Transform Anchor(string name,Vector3 position,Transform parent)
        {var go=new GameObject(name).transform;go.SetParent(parent,false);go.localPosition=position;return go;}
        static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 size,Material material,bool collider=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
            if(!collider)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
    }
}
