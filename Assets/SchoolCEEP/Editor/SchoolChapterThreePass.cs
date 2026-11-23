// Ferramenta da escola para construção ou acabamento.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using HorrorGame;
using HorrorGame.Editor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace SchoolCEEP.Editor
{
    /// <summary>Adds the authorized chapter to the saved school without rebuilding architecture.</summary>
    public static class SchoolChapterThreePass
    {
        public const string GroupName="Escola — capítulo 3 e sanidade";
        public const string Output="C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/ChapterThree";
        const string Data="Assets/SchoolCEEP/Data/ChapterThree";
        const string Materials="Assets/SchoolCEEP/Materials/ChapterThree";
        const string Profile="Assets/SchoolCEEP/Materials/ChapterThree/ChapterThreeVolume.asset";
        [Serializable] public sealed class Report
        {
            public string scene,architectureBefore,architectureAfter;
            public bool architectureUnchanged,schoolOnly,noEnemyAI,noGhostCollider,stopsBeforeChapter4;
            public int batteryPacks,memoryAnchors,poweredOffFixtures,missingScripts;
            public SchoolAbandonmentPass.Report abandonment;
            public string[] storyErrors;
        }

        public static string Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene outside Play before chapter installation.");
            Directory.CreateDirectory(Output);
            var scene=EditorSceneManager.OpenScene(SchoolIntegrationPass.ScenePath,OpenSceneMode.Single);
            File.Copy(SchoolIntegrationPass.ScenePath,Output+"/School-before-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity",true);
            var root=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("CEEP")).transform;
            var report=Apply(root);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            EnableRoadHandoff();
            scene=EditorSceneManager.OpenScene(SchoolIntegrationPass.ScenePath,OpenSceneMode.Single);
            File.WriteAllText(Output+"/installation.json",JsonUtility.ToJson(report,true));
            return JsonUtility.ToJson(report,true);
        }

        public static Report Apply(Transform root)
        {
            if(!root || EditorUtility.IsPersistent(root) || EditorApplication.isPlayingOrWillChangePlaymode
                || root.gameObject.scene.path!=SchoolIntegrationPass.ScenePath)
                throw new InvalidOperationException("Loaded SchoolCEEP scene instance required.");
            Folders(Data); Folders(Materials);
            var scene=root.gameObject.scene;
            var game=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GameSession>(true)).Single();
            var flow=root.GetComponentInChildren<SchoolOpeningFlow>(true);
            var mood=root.GetComponentInChildren<SchoolHorrorAtmosphere>(true);
            if(!flow || !mood)throw new InvalidOperationException("Keep the existing school opening and atmosphere.");
            string signature=ArchitectureSignature(root);
            var prior=root.Find(GroupName);if(prior)UnityEngine.Object.DestroyImmediate(prior.gameObject);
            var oldDust=game.player.transform.GetComponentsInChildren<SchoolFlashlightDust>(true).FirstOrDefault();
            if(oldDust)UnityEngine.Object.DestroyImmediate(oldDust.gameObject);
            var oldView=game.hud.GetComponentsInChildren<SchoolSanityView>(true).FirstOrDefault();
            if(oldView)UnityEngine.Object.DestroyImmediate(oldView.gameObject);
            Transform group=new GameObject(GroupName).transform;group.SetParent(root,false);
            ConfigureStory(game);
            var sanity=group.gameObject.AddComponent<SchoolSanityController>();
            sanity.enabledForSchool=true;sanity.persistentId="school.chapter3.sanity";
            sanity.game=game;sanity.director=game.narrative;sanity.flashlight=game.save.flashlight;
            sanity.atmosphere=game.atmosphere;sanity.cameraEffect=game.player.view.playerCamera.GetComponent<VintageCameraEffect>();
            sanity.illuminationMask=~LayerMask.GetMask("Player");
            sanity.presentation=CreatePresentation(game.hud.transform);
            sanity.heartbeat=Heartbeat();
            group.gameObject.AddComponent<SchoolObjectiveJournal>().game=game;
            mood.sanity=sanity; mood.abandonedPower=true;
            // Avoid accumulating persistent listeners when applying this same scene pass again.
            for(int i=mood.onUnsettlingCue.GetPersistentEventCount()-1;i>=0;i--)
                if(mood.onUnsettlingCue.GetPersistentTarget(i) is SchoolSanityController)
                    UnityEventTools.RemovePersistentListener(mood.onUnsettlingCue,i);
            UnityEventTools.AddPersistentListener(mood.onUnsettlingCue,sanity.Disturb);
            var lights=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true))
                .Where(l=>!l.transform.IsChildOf(game.player.transform)&&l.type!=LightType.Directional
                    &&l.name!="Fachada — luz do poste lateral").ToArray();
            foreach(var fixture in lights){fixture.enabled=false;Record(fixture);}
            flow.schoolLights=lights;
            mood.flickerLights=lights.Where(l=>l.name=="Luz de ambiente"||l.name=="Hall — iluminação legível").ToArray();
            mood.fixtureBaseIntensity=mood.flickerLights.Select(l=>l.intensity).ToArray();
            mood.initialSilence=40; mood.quietInterval=new Vector2(42,70);
            var moon=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).FirstOrDefault(l=>l.name=="Luar");
            if(moon){moon.intensity=.20f;moon.shadows=LightShadows.Soft;Record(moon);}
            // Emissive fixture materials are copied per school; shared office/road materials stay intact.
            var copied=new Dictionary<Material,Material>();
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.transform.IsChildOf(flow.transform))continue;
                var assigned=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<assigned.Length;i++)
                {
                    var mat=assigned[i];if(!mat||!mat.HasProperty("_EmissionColor")||mat.GetColor("_EmissionColor").maxColorComponent<.025f)continue;
                    if(!copied.TryGetValue(mat,out var replacement))
                    {
                        string path=Materials+"/Unpowered-"+mat.name.Replace('/','-')+".mat";
                        replacement=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(!replacement){replacement=UnityEngine.Object.Instantiate(mat);AssetDatabase.CreateAsset(replacement,path);}
                        replacement.DisableKeyword("_EMISSION");replacement.SetColor("_EmissionColor",Color.black);
                        EditorUtility.SetDirty(replacement);copied.Add(mat,replacement);
                    }
                    assigned[i]=replacement;changed=true;
                }
                if(changed){renderer.sharedMaterials=assigned;Record(renderer);}
            }
            ConfigureProfile(game.atmosphere);
            // No synthetic breath or continuous ambience; existing licensed prop/footstep sounds remain.
            game.deathSound=null;
            game.save.flashlight.startsOn=true;
            game.save.flashlight.lamp.enabled=true;
            sanity.anchors=CreateAnchors(group,flow);
            sanity.recoveryLights=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true))
                .Where(l=>l.name=="Fachada — luz do poste lateral").ToArray();
            var batteryPositions=new[]{new Vector3(-1.20f,.94f,32.30f),flow.documentsClue.transform.position+new Vector3(.45f,.045f,.20f),
                new Vector3(-2.3f,.09f,58.6f),flow.ghostAnchor.position+new Vector3(1.25f,-.99f,1.35f)};
            for(int i=0;i<batteryPositions.Length;i++)Battery(group,batteryPositions[i],i,game.narrative);
            PerceptionChair(group,game,flow);
            CreateDust(game.save.flashlight);
            var decay=SchoolAbandonmentPass.Apply(root);
            foreach(var title in game.hud.gameOverPanel.GetComponentsInChildren<TMP_Text>(true))
            {
                if(title.text.Contains("MORREU")||title.text.Contains("GAME OVER"))title.text="GAME OVER";
                if(title.text.Contains("Tentar novamente"))title.text="Tentar novamente";
                Record(title);
            }
            foreach(var item in new UnityEngine.Object[]{game,game.narrative,flow,mood,sanity,game.save.flashlight,game.save.flashlight.lamp})Record(item);
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);
            string after=ArchitectureSignature(root);
            if(signature!=after)throw new InvalidOperationException("Existing physical architecture changed.");
            var objects=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
            return new Report{scene=scene.path,architectureBefore=signature,architectureAfter=after,architectureUnchanged=signature==after,
                schoolOnly=true,noEnemyAI=objects.All(g=>!g.GetComponent<EnemyBrain>()),noGhostCollider=flow.ghostVisual.GetComponentsInChildren<Collider>(true).Length==0,
                stopsBeforeChapter4=!game.narrative.autoLoadNextChapter,batteryPacks=4,memoryAnchors=sanity.anchors.Length,poweredOffFixtures=lights.Length,
                missingScripts=objects.Sum(UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount),
                abandonment=decay,storyErrors=game.narrative.book.ValidateContent()};
        }

        static void ConfigureStory(GameSession game)
        {
            var shared=AssetDatabase.LoadAssetAtPath<StoryBook>("Assets/HorrorGameplay/Data/WhispersStory.asset");
            if(!shared)throw new InvalidOperationException("Existing StoryBook required.");
            shared.Chapter("school").heading="CAPÍTULO 3"; shared.Chapter("school").sceneName="SchoolCEEP";
            shared.Chapter("left-behind").heading="CAPÍTULO 4";
            shared.Chapter("memories").heading="CAPÍTULO 5";shared.Chapter("terrace").heading="CAPÍTULO 6";
            shared.Chapter("last-exit").heading="CAPÍTULO FINAL";
            var chapter=shared.Chapter("school");
            chapter.initialObjective="Entrar na escola. Lanterna: F. Anotações e objetivo: I.";
            chapter.milestones.Single(m=>m.id=="school.entry-locked").objectiveAfter="Encontrar os registros na Secretaria, pelo corredor central.";
            chapter.milestones.Single(m=>m.id=="school.photograph").objectiveAfter="Investigar o quadro da Sala 02, no corredor de salas.";
            chapter.milestones.Single(m=>m.id=="school.blackboard-vanished").objectiveAfter="Investigar a Sala 03, ao lado da Sala 02.";
            shared.Chapter("left-behind").milestones.Single(m=>m.id=="left-behind.chase-started").objectiveAfter="Seguir pelo corredor apesar da presença.";
            EditorUtility.SetDirty(shared);
            var book=AssetDatabase.LoadAssetAtPath<StoryBook>(Data+"/ChapterThreeStory.asset");
            if(!book){book=ScriptableObject.CreateInstance<StoryBook>();AssetDatabase.CreateAsset(book,Data+"/ChapterThreeStory.asset");}
            EditorUtility.CopySerialized(shared,book);EditorUtility.SetDirty(book);
            game.narrative.book=book;game.narrative.startingChapterId="school";game.narrative.autoLoadNextChapter=false;
            game.narrative.transitionView.numberedCompletion=true;
            Record(game.narrative.transitionView);
        }

        static void EnableRoadHandoff()
        {
            var road=EditorSceneManager.OpenScene(RoadJourneyBuilder.ScenePath,OpenSceneMode.Single);
            var director=road.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NarrativeDirector>(true)).Single();
            director.autoLoadNextChapter=true;director.book.Chapter("school").heading="CAPÍTULO 3";
            director.book.Chapter("school").sceneName="SchoolCEEP";director.book.Chapter("left-behind").heading="CAPÍTULO 4";
            EditorUtility.SetDirty(director.book);Record(director);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(road);
        }

        static SchoolMemoryAnchor[] CreateAnchors(Transform parent,SchoolOpeningFlow flow)
        {
            var result=new List<SchoolMemoryAnchor>();
            foreach(var pair in new[]{new KeyValuePair<string,Vector3>("Âncora — foto da vida de João",flow.documentsClue.transform.position+new Vector3(-.38f,.015f,.22f)),
                new KeyValuePair<string,Vector3>("Âncora — relógio familiar parado",new Vector3(-1.40f,.93f,43.0f))})
            {
                Vector3 position=pair.Key.Contains("relógio")?SupportedFloor(pair.Value)+Vector3.up*.94f:pair.Value;
                var root=new GameObject(pair.Key).transform;root.SetParent(parent,false);root.position=position;
                var anchor=root.gameObject.AddComponent<SchoolMemoryAnchor>();anchor.radius=1.7f;anchor.recoveryPerSecond=.65f;result.Add(anchor);
                var note=root.gameObject.AddComponent<SchoolAnchorInspection>();
                note.title=pair.Key.Contains("foto")?"Fotografia familiar":"Relógio parado";
                note.caption=pair.Key.Contains("foto")?"João: Isso é meu... Eu ainda lembro de quem sou.":"João: O mesmo relógio. Um instante para respirar.";
                if(pair.Key.Contains("relógio"))Cube("Caixa do relógio",parent,position-new Vector3(0,.49f,0),new Vector3(.40f,.90f,.30f),Material("OldCrate",new Color(.16f,.12f,.075f)));
                var volume=root.gameObject.AddComponent<BoxCollider>();volume.size=new Vector3(.19f,.12f,.16f);
                Cube("Objeto de memória",root,Vector3.zero,new Vector3(.18f,.07f,.15f),Material("AnchorLeather",new Color(.12f,.095f,.055f)));
                Cube("Detalhe desbotado",root,new Vector3(0,.038f,0),new Vector3(.14f,.003f,.11f),Material("AnchorPaper",new Color(.48f,.44f,.31f)));
            }
            return result.ToArray();
        }

        static void Battery(Transform parent,Vector3 position,int index,NarrativeDirector director)
        {
            // A small tabletop battery pack in the hall is supported by its own unobtrusive crate.
            if(index==0){position=SupportedFloor(position)+Vector3.up*.94f;Cube("Caixa baixa de manutenção",parent,position-new Vector3(0,.49f,0),new Vector3(.55f,.90f,.40f),Material("OldCrate",new Color(.16f,.12f,.075f)));}
            else if(index!=1)position=SupportedFloor(position)+Vector3.up*.03f;
            var root=new GameObject("Pilhas de reserva — "+(index+1)).transform;root.SetParent(parent,false);root.position=position;
            var item=root.gameObject.AddComponent<CollectibleItem>();item.persistentId="school.chapter3.battery."+(index+1);
            item.kind=CollectibleKind.Battery;item.title="Pilhas de reserva";item.batterySeconds=110;
            var collider=root.gameObject.AddComponent<BoxCollider>();collider.size=new Vector3(.18f,.065f,.12f);
            var dark=Material("BatteryBlack",new Color(.037f,.034f,.025f),.1f);var copper=Material("BatteryCopper",new Color(.46f,.25f,.09f),.5f);
            for(int i=0;i<2;i++)
            {
                Cube("Pilha — corpo",root,new Vector3((i-.5f)*.05f,0,0),new Vector3(.032f,.032f,.09f),dark);
                Cube("Pilha — polo metálico",root,new Vector3((i-.5f)*.05f,0,.048f),new Vector3(.032f,.032f,.016f),copper);
            }
            UnityEventTools.AddPersistentListener(item.onCollected,director.ScheduleSave);
        }

        static void PerceptionChair(Transform parent,GameSession game,SchoolOpeningFlow flow)
        {
            var events=parent.gameObject.AddComponent<SchoolPerceptionEvents>();events.persistentId="school.chapter3.changed-chair";
            events.director=game.narrative;events.playerCamera=game.player.view.playerCamera;
            var chair=new GameObject("Cadeira fora do lugar — memória incerta").transform;chair.SetParent(parent,false);
            chair.position=SupportedFloor(flow.documentsClue.transform.position+new Vector3(.85f,-.93f,1.4f))+Vector3.up*.45f;
            events.chair=chair;events.originalChairPosition=chair.position;events.changedChairPosition=chair.position+new Vector3(.6f,0,-.35f);
            events.movementSound=AssetDatabase.LoadAssetAtPath<SoundSet>("Assets/HorrorGameplay/Audio/Recorded/Sets/Room creaks.asset");
            var wood=Material("ChairWood",new Color(.12f,.13f,.10f));var metal=Material("ChairRust",new Color(.10f,.095f,.075f),.3f);
            Cube("Assento gasto",chair,new Vector3(0,.015f,0),new Vector3(.4f,.038f,.4f),wood);
            Cube("Encosto gasto",chair,new Vector3(0,.26f,.18f),new Vector3(.4f,.32f,.035f),wood);
            for(int i=0;i<4;i++)Cube("Pé oxidado",chair,new Vector3(i%2==0?-.16f:.16f,-.21f,i<2?-.16f:.16f),new Vector3(.025f,.45f,.025f),metal);
        }

        static SchoolSanityView CreatePresentation(Transform hud)
        {
            var root=new GameObject("Sanidade — bordas e blackout",typeof(RectTransform)).transform;root.SetParent(hud,false);root.SetAsFirstSibling();
            var rect=(RectTransform)root;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var view=root.gameObject.AddComponent<SchoolSanityView>();
            view.vignette=Overlay("Visão periférica",root,VignetteTexture());view.blackout=Overlay("Desmaio",root,null);
            return view;
        }
        static CanvasGroup Overlay(string name,Transform parent,Sprite sprite)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasGroup),typeof(Image));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=go.GetComponent<Image>();image.sprite=sprite;image.color=Color.black;image.raycastTarget=false;
            var group=go.GetComponent<CanvasGroup>();group.alpha=0;group.blocksRaycasts=false;group.interactable=false;return group;
        }
        static Sprite VignetteTexture()
        {
            const string path=Materials+"/SanityVignette.asset";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(!texture)
            {
                texture=new Texture2D(128,128,TextureFormat.RGBA32,false);texture.name="Soft peripheral shade";
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                {float r=new Vector2((x-63.5f)/64,(y-63.5f)/64).magnitude;texture.SetPixel(x,y,new Color(1,1,1,Mathf.SmoothStep(0,1,Mathf.Clamp01((r-.45f)/.72f))));}
                texture.Apply();texture.wrapMode=TextureWrapMode.Clamp;AssetDatabase.CreateAsset(texture,path);
            }
            const string spritePath=Materials+"/SanityVignetteSprite.asset";
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if(!sprite){sprite=Sprite.Create(texture,new Rect(0,0,128,128),new Vector2(.5f,.5f));AssetDatabase.CreateAsset(sprite,spritePath);}return sprite;
        }

        static void CreateDust(FlashlightController flashlight)
        {
            var go=new GameObject("Lanterna — poeira suspensa",typeof(ParticleSystem));go.transform.SetParent(flashlight.handRig,false);go.transform.localPosition=Vector3.forward*.5f;
            var dust=go.AddComponent<SchoolFlashlightDust>();dust.flashlight=flashlight;dust.particles=go.GetComponent<ParticleSystem>();
            var particles=dust.particles;var main=particles.main;main.playOnAwake=false;main.loop=true;main.maxParticles=96;
            main.startLifetime=4;main.startSpeed=.012f;main.startSize=new ParticleSystem.MinMaxCurve(.009f,.022f);
            main.startColor=new Color(.46f,.47f,.41f,.14f);main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=particles.emission;emission.rateOverTime=15;
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.ConeVolume;shape.angle=19;shape.radius=.075f;shape.length=5.5f;
            var render=particles.GetComponent<ParticleSystemRenderer>();render.shadowCastingMode=ShadowCastingMode.Off;render.receiveShadows=false;
            var material=AssetDatabase.LoadAssetAtPath<Material>(Materials+"/BeamDust.mat");
            if(!material)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);
                material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;AssetDatabase.CreateAsset(material,Materials+"/BeamDust.mat");
            }
            render.sharedMaterial=material;particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        static SoundSet Heartbeat()
        {
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SchoolCEEP/Audio/ChapterThree/Heartbeat.wav");
            if(!clip)throw new InvalidOperationException("Chapter heartbeat clip must be imported first.");
            string path=Data+"/Heartbeat.asset";
            var set=AssetDatabase.LoadAssetAtPath<SoundSet>(path);
            if(!set){set=ScriptableObject.CreateInstance<SoundSet>();AssetDatabase.CreateAsset(set,path);}
            set.clips=new[]{clip};set.pitch=new Vector2(.98f,1.02f);set.volume=Vector2.one;
            EditorUtility.SetDirty(set);return set;
        }

        static void ConfigureProfile(HorrorAtmosphere atmosphere)
        {
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Profile);
            if(!profile)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="ChapterThreeVolume";
                foreach(var component in atmosphere.volume.sharedProfile.components)
                    if(component)profile.components.Add(UnityEngine.Object.Instantiate(component));
                AssetDatabase.CreateAsset(profile,Profile);
                foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);
            }
            atmosphere.volume.sharedProfile=profile;
            if(profile.TryGet<ColorAdjustments>(out var colour)){colour.postExposure.Override(.18f);colour.saturation.Override(-20);colour.contrast.Override(7);}
            var settings=new SerializedObject(atmosphere);settings.FindProperty("baseExposure").floatValue=.18f;settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);Record(atmosphere.volume);Record(atmosphere);
        }

        public static string ArchitectureSignature(Transform root, string authorizedExcludedCollider = null)
        {
            var values=root.GetComponentsInChildren<Collider>(true).Where(c=>!InOwnGroup(c.transform)&&c.name!=authorizedExcludedCollider).Select(c=>
                PathOf(c.transform)+"|"+c.GetType().Name+"|"+c.transform.position.ToString("F5")+"|"+c.transform.rotation.ToString("F5")
                +"|"+c.transform.lossyScale.ToString("F5")+"|"+c.enabled+"|"+c.isTrigger+"|"+c.bounds.center.ToString("F5")+"|"+c.bounds.size.ToString("F5")
                +(c is MeshCollider mesh?"|"+AssetDatabase.GetAssetPath(mesh.sharedMesh):"" )).OrderBy(v=>v);
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n",values)))).Replace("-","");
        }
        static bool InOwnGroup(Transform t){while(t){if(t.name==GroupName||t.name=="Escola — abandono do capítulo 3"||t.name==SchoolChapterFourPass.GroupName||t.name==SchoolChapterFivePass.GroupName||t.name==SchoolFinalChaptersPass.GroupName)return true;t=t.parent;}return false;}
        static Vector3 SupportedFloor(Vector3 guess)
        {
            var hits=Physics.RaycastAll(guess+Vector3.up*.8f,Vector3.down,4,~LayerMask.GetMask("Player"),QueryTriggerInteraction.Ignore)
                .Where(h=>h.normal.y>.75f&&!InOwnGroup(h.collider.transform)).OrderBy(h=>h.distance).ToArray();
            if(hits.Length==0)throw new InvalidOperationException("No supported floor for chapter prop at "+guess);
            return new Vector3(guess.x,hits[0].point.y,guess.z);
        }
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        static Transform Cube(string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
        }
        static Material Material(string name,Color colour,float metallic=0)
        {
            string path=Materials+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",colour);material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(material);return material;
        }
        static void Record(UnityEngine.Object item){if(!item)return;EditorUtility.SetDirty(item);if(PrefabUtility.IsPartOfPrefabInstance(item))PrefabUtility.RecordPrefabInstancePropertyModifications(item);}
        static void Folders(string path){if(AssetDatabase.IsValidFolder(path))return;string parent=path.Substring(0,path.LastIndexOf('/'));Folders(parent);AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));}
    }
}
