// Ferramenta de montagem da estrada, do carro ou do rádio.

using System;
using System.IO;
using System.Linq;
using HorrorGame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HorrorGame.Editor
{
    /// <summary>A separate, short road chapter. It never rebuilds the office or school.</summary>
    public static class RoadJourneyBuilder
    {
        public const string ScenePath = "Assets/RoadChapter/Scenes/RoadJourney.unity";
        public const string Output = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RoadChapter";
        const string Data = "Assets/RoadChapter/Data";
        static Transform environment;
        static Material asphalt, earth, concrete, paint, foliage, bark, blue, white, metal, rubber, body, fabric, dash, amber, glass;

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build road outside Play.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene first.");
            Directory.CreateDirectory(Output);
            Folders("Assets/RoadChapter/Scenes"); Folders(Data);
            if (File.Exists(ScenePath)) File.Copy(ScenePath, Output + "/RoadJourney-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".unity");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Materials();
            environment = new GameObject("Caminho — cenário exterior").transform;
            Road(); SchoolExterior();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HorrorGameplay/Prefabs/GameplayRoot.prefab");
            if (!prefab) throw new InvalidOperationException("Existing GameplayRoot is required.");
            var gameplay = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            PrefabUtility.UnpackPrefabInstance(gameplay, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            gameplay.name = "GameplayRoot — viagem";
            var game = gameplay.GetComponentInChildren<GameSession>(true);
            ConfigureGameplay(game);
            var flow = new GameObject("Viagem — sequência do capítulo 2").AddComponent<RoadJourneyFlow>();
            flow.director = game.narrative; flow.player = game.player; flow.playerCamera = game.player.view.playerCamera;
            flow.input = game.input; flow.flashlight = game.save.flashlight;
            flow.driveDuration=24; flow.seatedYawLimit=85; flow.seatedPitchLimit=40;
            flow.observationMask=~LayerMask.GetMask("Player");
            flow.ringSound=AssetDatabase.LoadAssetAtPath<SoundSet>("Assets/HorrorGameplay/Audio/Narrative/TelephoneRing.asset");
            flow.receiverSound=AssetDatabase.LoadAssetAtPath<SoundSet>("Assets/HorrorGameplay/Audio/Narrative/ReceiverPickup.asset");
            Car(flow, game);
            flow.driveStart = Anchor("Estrada — início", new Vector3(0,0,0));
            flow.driveEnd = Anchor("Estrada — estacionamento", new Vector3(0,0,384));
            flow.exitPose = Anchor("Carro — saída no passeio", new Vector3(-2.1f,0,384));
            flow.windowAnchor = Anchor("Janela — observação", new Vector3(7.5f,5.1f,418.68f));
            var window = Cube("Janela — claridade impossível", environment, new Vector3(7.5f,5.1f,418.70f), new Vector3(3.85f,1.1f,.025f), amber);
            flow.windowVisual = window; window.SetActive(false);
            var windowLight = new GameObject("Janela — evento de luz").AddComponent<Light>();
            windowLight.transform.SetParent(environment); windowLight.transform.position = new Vector3(7.5f,5.1f,418.4f);
            windowLight.type = LightType.Point; windowLight.color = new Color(1,.68f,.27f); windowLight.intensity = 5; windowLight.range = 8;
            windowLight.shadows = LightShadows.None; windowLight.enabled = false; flow.windowLight = windowLight;
            var gate = new GameObject("Portão — chegada à escola");gate.transform.SetParent(environment,false);gate.transform.position=new Vector3(0,0,410);
            var gateCollider=gate.AddComponent<BoxCollider>();gateCollider.center=Vector3.up*1.05f;gateCollider.size=new Vector3(6.45f,2.1f,.075f);
            flow.gate = gate.AddComponent<RoadJourneyInteraction>(); flow.gate.flow = flow; flow.gate.kind = RoadJourneyInteractionKind.SchoolGate;
            Cube("Portão — chapa inferior",gate.transform,new Vector3(0,.28f,0),new Vector3(6.4f,.5f,.07f),blue);
            for (int i=0;i<26;i++) Cube("Portão — barra", gate.transform, new Vector3(-3.05f+i*.244f,1.30f,0), new Vector3(.035f,1.55f,.03f), metal);
            Cube("Portão — travessa superior",gate.transform,new Vector3(0,2.08f,0),new Vector3(6.45f,.04f,.07f),blue);
            game.player.transform.SetPositionAndRotation(flow.seatedPose.position-Vector3.up*game.player.EyeHeight, Quaternion.identity);
            game.player.view.Snap();
            Settings();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
            var listed = EditorBuildSettings.scenes.ToList();
            if (!listed.Any(s=>s.path==ScenePath)) listed.Add(new EditorBuildSettingsScene(ScenePath,true));
            foreach(var entry in listed.Where(s=>s.path==ScenePath))entry.enabled=true;
            EditorBuildSettings.scenes = listed.ToArray();
            AssetDatabase.SaveAssets();
            File.WriteAllText(Output+"/installation.json", JsonUtility.ToJson(new InstallReport { scene=ScenePath, driveSeconds=24, pathLength=384,
                enemies=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EnemyBrain>(true)).Count(),
                cameras=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Count(),
                interactiveItems=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RoadJourneyInteraction>(true)).Count(),
                storyErrors=game.narrative.book.ValidateContent(), autoLoadNext=game.narrative.autoLoadNextChapter },true));
            return "RoadJourney saved; road binding added; completed school and office untouched.";
        }

        public static string Capture(string phase)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Capture outside Play.");
            var scene=SceneManager.GetActiveScene();if(scene.path!=ScenePath)throw new InvalidOperationException("Road scene required.");
            var flow=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RoadJourneyFlow>(true)).Single();
            var carPosition=flow.carRoot.position;var windowVisible=flow.windowVisual.activeSelf;var lightEnabled=flow.windowLight.enabled;
            var torch=flow.flashlight;bool torchEnabled=torch.lamp.enabled;
            Vector3 torchPosition=torch.handRig.position;Quaternion torchRotation=torch.handRig.rotation;
            var canvases=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).Where(c=>c.enabled).ToArray();
            var preview=new GameObject("Road capture temporary",typeof(Camera));var camera=preview.GetComponent<Camera>();
            camera.CopyFrom(flow.playerCamera);VintageCameraSetup.CopyPreviewConfiguration(flow.playerCamera,camera);camera.enabled=false;
            var pipeline=UniversalRenderPipeline.asset;var drawer=pipeline.gpuResidentDrawerMode;
            GameObject modelPreviewLight=null;
            string path=Output+"/"+phase+".png";
            try
            {
                pipeline.gpuResidentDrawerMode=GPUResidentDrawerMode.Disabled;
                foreach(var canvas in canvases)canvas.enabled=false;
                torch.lamp.enabled=false;
                if(phase=="arrival")
                {
                    flow.carRoot.position=flow.driveEnd.position;flow.windowVisual.SetActive(true);flow.windowLight.enabled=true;
                    camera.transform.position=flow.seatedPose.position;
                    camera.transform.rotation=Quaternion.Euler(0,5,0);
                }
                else if(phase=="gate")
                {
                    flow.carRoot.position=flow.driveEnd.position;camera.transform.position=new Vector3(-1.8f,1.64f,404.5f);
                    camera.transform.LookAt(new Vector3(0,2.3f,419));
                }
                else if(phase=="cabin")
                {
                    flow.carRoot.position=flow.driveEnd.position;
                    camera.transform.position=flow.seatedPose.position;
                    camera.transform.LookAt(flow.flashlightPickup.transform.position);
                }
                else if(phase=="car" || phase=="car-rear")
                {
                    flow.carRoot.position=flow.driveEnd.position;
                    Vector3 offset=phase=="car" ? new Vector3(-3.8f,2.2f,5.2f) : new Vector3(3.8f,2.2f,-5.2f);
                    camera.transform.position=flow.carRoot.position+offset;
                    camera.transform.LookAt(flow.carRoot.position+Vector3.up*.85f);
                    // Auxiliary inspection lighting is temporary and does not change the game scene.
                    modelPreviewLight=new GameObject("Car preview — temporary inspection light",typeof(Light));
                    var key=modelPreviewLight.GetComponent<Light>();key.type=LightType.Directional;
                    key.transform.position=flow.carRoot.position+new Vector3(offset.x,3.5f,offset.z*.6f);
                    key.transform.rotation=Quaternion.Euler(38,phase=="car"?-32:145,0);
                    key.color=new Color(.88f,.85f,.76f);key.intensity=1.1f;key.shadows=LightShadows.None;
                }
                else{camera.transform.SetPositionAndRotation(flow.seatedPose.position,Quaternion.Euler(0,0,0));}
                if(phase=="gate")
                {
                    torch.handRig.SetPositionAndRotation(camera.transform.TransformPoint(new Vector3(.16f,-.16f,.25f)),camera.transform.rotation);
                    torch.lamp.enabled=true;
                }
                camera.fieldOfView=phase.StartsWith("car")?42:65;Physics.SyncTransforms();
                PresentationCapture.Capture(scene,camera,path,1600,900,null,true);
                return path;
            }
            finally
            {
                flow.carRoot.position=carPosition;flow.windowVisual.SetActive(windowVisible);flow.windowLight.enabled=lightEnabled;
                torch.handRig.SetPositionAndRotation(torchPosition,torchRotation);torch.lamp.enabled=torchEnabled;
                foreach(var canvas in canvases)if(canvas)canvas.enabled=true;
                pipeline.gpuResidentDrawerMode=drawer;UnityEngine.Object.DestroyImmediate(preview);Physics.SyncTransforms();
                if(modelPreviewLight)UnityEngine.Object.DestroyImmediate(modelPreviewLight);
            }
        }

        static void ConfigureGameplay(GameSession game)
        {
            if (!game || !game.narrative || !game.narrative.transitionView) throw new InvalidOperationException("Reviewed narrative prefab required.");
            var shared = AssetDatabase.LoadAssetAtPath<StoryBook>("Assets/HorrorGameplay/Data/WhispersStory.asset");
            if (!shared) throw new InvalidOperationException("Existing story is required.");
            var road = shared.Chapter("road"); road.sceneName="RoadJourney"; road.heading="CAPÍTULO 2"; road.title="A Chegada";
            road.initialObjective="Chegar à escola e entrar na biblioteca.";
            road.completionPhrase="A porta estava esperando por ele.";
            road.milestones.Single(m=>m.id=="road.window-lit").objectiveAfter="Pegar a lanterna no banco.";
            road.milestones.Single(m=>m.id=="road.flashlight-taken").objectiveAfter="Sair do carro pela porta do motorista.";
            if(!road.milestones.Any(m=>m.id=="road.car-exited"))
            {
                var milestones=road.milestones.ToList();
                milestones.Insert(milestones.FindIndex(m=>m.id=="road.school-gate"),
                    new StoryMilestoneDefinition{id="road.car-exited",objectiveAfter="Entrar pelo portão e seguir até a biblioteca."});
                road.milestones=milestones.ToArray();
            }
            var context = new StorySequenceDefinition { id="road.context", completionSignal="road.context-shown", lockPlayer=false,
                lines=new[] { new StoryLine{speaker="Rádio",text="A escola está abandonada há décadas.",seconds=4},
                    new StoryLine{speaker="Rádio",text="O último registro dos alunos desaparecidos veio do chamado Bloco Hospício.",seconds=5},
                    new StoryLine{speaker="João",text="Hospício... Só um apelido de bloco escolar. Preciso começar pela biblioteca.",seconds=5} } };
            shared.sequences=shared.sequences.Where(s=>s.id!="road.context").Append(context).ToArray();
            EditorUtility.SetDirty(shared);
            // A chapter-local book allows the new card numbering without rewriting ready scenes.
            string localPath=Data+"/RoadStory.asset";
            var local=AssetDatabase.LoadAssetAtPath<StoryBook>(localPath);
            if(!local) { local=ScriptableObject.CreateInstance<StoryBook>(); AssetDatabase.CreateAsset(local,localPath); }
            EditorUtility.CopySerialized(shared,local); local.name="RoadStory";
            local.Chapter("school").heading="CAPÍTULO 3";
            EditorUtility.SetDirty(local);
            game.narrative.book=local; game.narrative.startingChapterId="road"; game.narrative.enabled=true;
            game.narrative.autoLoadNextChapter=AssetDatabase.LoadAssetAtPath<StoryBook>("Assets/SchoolCEEP/Data/ChapterThree/ChapterThreeStory.asset")!=null;
            game.narrative.transitionView.numberedCompletion=true;
            game.save.narrative=game.narrative;
            game.interaction.mask=~LayerMask.GetMask("Player");
            var vintage=game.player.view.playerCamera.GetComponent<VintageCameraEffect>();
            if(!vintage) vintage=game.player.view.playerCamera.gameObject.AddComponent<VintageCameraEffect>();
            vintage.ConfigureForChapter(1); vintage.schoolTreatment=false;
            game.narrative.vintageCamera=vintage;
            game.player.view.playerCamera.farClipPlane=180;
            var volume=game.atmosphere.volume;
            string profilePath=Data+"/RoadVolume.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if(!profile)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,profilePath);
                foreach(var source in volume.sharedProfile.components)
                {var component=UnityEngine.Object.Instantiate(source); component.hideFlags=HideFlags.HideInInspector; AssetDatabase.AddObjectToAsset(component,profile);profile.components.Add(component);}
            }
            volume.sharedProfile=profile;
            if(profile.TryGet(out ColorAdjustments color)){color.postExposure.Override(.35f);color.saturation.Override(-22);color.contrast.Override(6);}
            if(profile.TryGet(out WhiteBalance balance)){balance.temperature.Override(-12);balance.tint.Override(-3);}
            if(profile.TryGet(out Bloom bloom)){bloom.intensity.Override(.18f);bloom.threshold.Override(1.1f);}
            if(profile.TryGet(out Vignette vignette))vignette.intensity.Override(.30f);
            if(profile.TryGet(out FilmGrain grain))grain.intensity.Override(0);
            EditorUtility.SetDirty(profile);
            var serialized=new SerializedObject(game.atmosphere);
            serialized.FindProperty("baseExposure").floatValue=.35f; serialized.FindProperty("adaptationGain").floatValue=.1f;
            serialized.FindProperty("defaultDarkness").floatValue=.15f; serialized.ApplyModifiedPropertiesWithoutUndo();
            var audio=game.GetComponentInChildren<AudioManager>(true);
            foreach(var source in new[]{audio.calmMusic,audio.tenseMusic,audio.ambience})
                if(source){source.clip=null;source.playOnAwake=false;}
            foreach(var automatic in game.GetComponentsInChildren<RandomScareAudio>(true)) automatic.enabled=false;
            foreach(var automatic in game.GetComponentsInChildren<SpatialAmbience>(true)) automatic.enabled=false;
            game.save.flashlight.startsOn=false;
            game.save.flashlight.lamp.enabled=false;
        }

        static void Road()
        {
            Cube("Estrada — asfalto",environment,new Vector3(0,-.12f,198),new Vector3(6,.24f,430),asphalt,true);
            Cube("Estrada — terreno",environment,new Vector3(0,-.3f,198),new Vector3(120,.25f,460),earth,true);
            Cube("Chegada — passeio",environment,new Vector3(0,-.08f,398),new Vector3(24,.16f,38),concrete,true);
            for(int i=0;i<71;i++)Cube("Estrada — faixa gasta",environment,new Vector3(1.3f,.006f,i*6-12),new Vector3(.10f,.008f,2.8f),paint);
            for(int side=-1;side<=1;side+=2)
            {
                Cube("Estrada — acostamento",environment,new Vector3(side*3.2f,-.015f,193),new Vector3(.4f,.05f,420),concrete,true);
                for(int i=0;i<34;i++)
                {
                    float z=i*12-16;float x=side*(7+(i*7%5));
                    var trunk=Primitive("Árvore — tronco",PrimitiveType.Cylinder,environment,new Vector3(x,2.4f,z),new Vector3(.35f,2.4f,.35f),bark);
                    var canopy=Primitive("Árvore — copa",PrimitiveType.Sphere,environment,new Vector3(x,6,z),new Vector3(4.5f+(i%3),6,4.5f),foliage);
                    trunk.isStatic=canopy.isStatic=true;
                }
            }
            for(int i=0;i<4;i++)
            {
                float z=90+i*88;
                Primitive("Poste — fuste",PrimitiveType.Cylinder,environment,new Vector3(-4.6f,3,z),new Vector3(.16f,3,.16f),concrete);
                Cube("Poste — braço",environment,new Vector3(-3.6f,5.8f,z),new Vector3(2,.10f,.13f),metal);
                Cube("Poste — luminária",environment,new Vector3(-2.7f,5.68f,z),new Vector3(.42f,.08f,.25f),amber);
                var lamp=new GameObject("Poste — luz distante").AddComponent<Light>();lamp.transform.SetParent(environment);
                lamp.transform.position=new Vector3(-2.7f,5.6f,z);lamp.color=new Color(1,.65f,.33f);lamp.type=LightType.Point;lamp.intensity=2.1f;lamp.range=19;lamp.shadows=LightShadows.None;
            }
            for(int side=-1;side<=1;side+=2)
            {
                Cube("Chegada — mureta do passeio",environment,new Vector3(side*12,.65f,398),new Vector3(.22f,1.3f,38),white,true);
                Cube("Chegada — limite do terreno",environment,new Vector3(side*9.8f,1.5f,410),new Vector3(12.8f,3,.2f),white,true);
            }
            var rearBoundary=new GameObject("Chegada — barreira invisível posterior");rearBoundary.transform.SetParent(environment,false);
            rearBoundary.transform.position=new Vector3(0,2,379);
            rearBoundary.AddComponent<BoxCollider>().size=new Vector3(24,4,.2f);
        }

        static void SchoolExterior()
        {
            Cube("Escola — fachada superior",environment,new Vector3(0,4.25f,419),new Vector3(30,3.5f,.32f),white,true);
            Cube("Escola — faixa azul",environment,new Vector3(0,1.45f,419),new Vector3(30,2.1f,.32f),blue,true);
            Cube("Escola — cobertura",environment,new Vector3(0,6.12f,421),new Vector3(31,.25f,7),concrete);
            for(int row=0;row<2;row++)for(int i=0;i<6;i++)
            {
                float x=-12.5f+i*5, y=row==0?2.2f:5.1f;
                Cube("Escola — janela escura",environment,new Vector3(x,y,418.79f),new Vector3(3.9f,1.15f,.08f),glass);
                for(int bar=0;bar<7;bar++)Cube("Escola — grade",environment,new Vector3(x-1.8f+bar*.6f,y,418.72f),new Vector3(.035f,1.15f,.035f),blue);
                for(int bar=0;bar<3;bar++)Cube("Escola — travessa",environment,new Vector3(x,y-.5f+bar*.5f,418.70f),new Vector3(3.9f,.035f,.035f),blue);
            }
            Cube("Escola — entrada ao fundo",environment,new Vector3(0,1.3f,418.68f),new Vector3(4.5f,2.6f,.08f),rubber);
            for(int side=-1;side<=1;side+=2)
            {
                Cube("Entrada — pilar",environment,new Vector3(side*3.35f,1.6f,410),new Vector3(.25f,3.2f,.35f),white,true);
                Cube("Entrada — apoio da cobertura",environment,new Vector3(side*3.35f,2.1f,415),new Vector3(.16f,4.2f,.16f),metal);
            }
            var left=Cube("Entrada — cobertura esquerda",environment,new Vector3(-1.7f,3.56f,414.3f),new Vector3(3.6f,.10f,10),concrete);left.transform.rotation=Quaternion.Euler(0,0,14);
            var right=Cube("Entrada — cobertura direita",environment,new Vector3(1.7f,3.56f,414.3f),new Vector3(3.6f,.10f,10),concrete);right.transform.rotation=Quaternion.Euler(0,0,-14);
            for(int i=0;i<12;i++)
            {
                float x=-13+(i*17%27),y=3+(i%4)*.55f;
                Cube("Escola — reboco manchado",environment,new Vector3(x,y,418.825f),new Vector3(.15f+(i%3)*.13f,.9f+(i%2),.009f),concrete);
            }
        }

        static void Car(RoadJourneyFlow flow, GameSession game)
        {
            RoadVehicleAssets.Build(flow,game,body,metal,rubber,amber);
        }

        static void Settings()
        {
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.17f,.21f,.26f);RenderSettings.ambientIntensity=1;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.012f,.018f,.025f);RenderSettings.fogDensity=.009f;
            var moon=new GameObject("Exterior — lua").AddComponent<Light>();moon.type=LightType.Directional;moon.color=new Color(.35f,.44f,.55f);moon.intensity=.42f;
            moon.transform.rotation=Quaternion.Euler(38,-32,0);moon.shadows=LightShadows.Soft;
        }
        static void Materials()
        {
            asphalt=Mat("Asphalt",new Color(.055f,.061f,.064f));earth=Mat("Earth",new Color(.045f,.054f,.041f));concrete=Mat("Concrete",new Color(.23f,.25f,.23f));
            paint=Mat("RoadMark",new Color(.51f,.47f,.34f));foliage=Mat("Foliage",new Color(.022f,.038f,.03f));bark=Mat("Bark",new Color(.065f,.056f,.044f));
            blue=Mat("SchoolBlue",new Color(.025f,.065f,.14f));white=Mat("SchoolPlaster",new Color(.55f,.55f,.51f));metal=Mat("Metal",new Color(.22f,.23f,.24f),.7f,.28f);
            rubber=Mat("Rubber",new Color(.012f,.015f,.017f));body=Mat("CarBody",new Color(.055f,.075f,.071f),.25f,.35f);fabric=Mat("SeatFabric",new Color(.035f,.044f,.047f));
            dash=Mat("Dashboard",new Color(.020f,.028f,.029f));glass=Mat("UnlitWindow",new Color(.013f,.020f,.023f),.1f,.35f);
            amber=Mat("Amber",new Color(.23f,.15f,.045f));amber.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;amber.EnableKeyword("_EMISSION");amber.SetColor("_EmissionColor",new Color(.5f,.25f,.08f));EditorUtility.SetDirty(amber);
        }
        static Material Mat(string name,Color color,float metallic=0,float smoothness=.04f)
        {
            string path=Data+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smoothness);EditorUtility.SetDirty(material);return material;
        }
        static GameObject Cube(string name,Transform parent,Vector3 p,Vector3 size,Material mat,bool collider=false)=>Primitive(name,PrimitiveType.Cube,parent,p,size,mat,collider);
        static GameObject Primitive(string name,PrimitiveType type,Transform parent,Vector3 p,Vector3 size,Material mat,bool collider=false)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collider)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static Transform Anchor(string name,Vector3 position,Transform parent=null)
        {var go=new GameObject(name).transform;go.SetParent(parent,false);go.localPosition=position;return go;}
        static GameObject Ring(string name,Transform parent,Vector3 position,float radius,float tube,Material mat)
        {
            const int steps=32, sides=8;var vertices=new Vector3[steps*sides];var triangles=new int[steps*sides*6];int at=0;
            for(int i=0;i<steps;i++)for(int j=0;j<sides;j++)
            {
                float a=i*Mathf.PI*2/steps,b=j*Mathf.PI*2/sides;vertices[i*sides+j]=new Vector3(Mathf.Cos(a)*(radius+Mathf.Cos(b)*tube),Mathf.Sin(a)*(radius+Mathf.Cos(b)*tube),Mathf.Sin(b)*tube);
                int p=i*sides+j,q=((i+1)%steps)*sides+j,r=((i+1)%steps)*sides+(j+1)%sides,s=i*sides+(j+1)%sides;
                triangles[at++]=p;triangles[at++]=q;triangles[at++]=r;triangles[at++]=p;triangles[at++]=r;triangles[at++]=s;
            }
            string path=Data+"/SteeringRing.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.name="SteeringRing";mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;return go;
        }
        static void Folders(string path)
        {string current="Assets";foreach(var part in path.Split('/').Skip(1)){if(!AssetDatabase.IsValidFolder(current+"/"+part))AssetDatabase.CreateFolder(current,part);current+="/"+part;}}
        [Serializable]sealed class InstallReport{public string scene;public float driveSeconds,pathLength;public int enemies,cameras,interactiveItems;public string[] storyErrors;public bool autoLoadNext;}
    }
}
