// Ferramenta da escola para construção ou acabamento.

using System;
using System.IO;
using System.Linq;
using HorrorGame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    /// <summary>Rebuildable first school chapter. Story state stays in existing milestones.</summary>
    public static class SchoolOpeningPass
    {
        public const string GroupName = "Escola — primeira investigação";
        const string Data = "Assets/SchoolCEEP/Data/Opening";
        static Material blue, steel, paper, chalk, skin, uniform, hair;
        static Transform group;
        static Vector3 P(float x,float y,float h=0)=>SchoolMapBuilder.Point(x,y,h);

        public static SchoolOpeningFlow Apply(Transform architecture)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Install opening outside Play.");
            var scene=architecture.gameObject.scene;
            if(scene.path!=SchoolIntegrationPass.ScenePath)throw new InvalidOperationException("School scene required.");
            var prior=architecture.Find(GroupName);if(prior)UnityEngine.Object.DestroyImmediate(prior.gameObject);
            var game=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GameSession>(true)).Single();
            if(!AssetDatabase.IsValidFolder(Data))AssetDatabase.CreateFolder("Assets/SchoolCEEP/Data","Opening");
            group=new GameObject(GroupName).transform;group.SetParent(architecture,false);
            blue=Material("GateBlue",new Color(.025f,.07f,.15f),.55f);
            steel=Material("GateSteel",new Color(.19f,.21f,.22f),.82f);
            paper=Material("AgedPaper",new Color(.68f,.65f,.51f));
            chalk=Material("Chalk",new Color(.73f,.77f,.67f));
            skin=Material("GhostSkin",new Color(.43f,.46f,.43f));
            uniform=Material("GhostUniform",new Color(.10f,.13f,.14f));
            hair=Material("GhostHair",new Color(.018f,.020f,.020f));
            var flow=group.gameObject.AddComponent<SchoolOpeningFlow>();
            flow.director=game.narrative;flow.player=game.player.transform;flow.playerCamera=game.player.view.playerCamera;
            flow.observationMask=~LayerMask.GetMask("Player");
            // Old parked geometry is removed before static mesh batching, avoiding duplicate leaves.
            foreach(var t in architecture.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Portão aberto —")).ToArray())
                if(t)UnityEngine.Object.DestroyImmediate(t.gameObject);
            Vector3 opening=P(1420.9f,1343,.644f);
            var left=Gate(opening+Vector3.left*3.215f,true);var right=Gate(opening+Vector3.right*3.215f,false);
            left.pairedLeaf=right;right.pairedLeaf=left;flow.entryDoor=left;
            var volume=new GameObject("Entrada — cruzamento depois do giro");volume.transform.SetParent(group,false);
            volume.transform.position=opening+new Vector3(0,1.0f,4.05f);
            flow.entryVolume=volume.AddComponent<BoxCollider>();flow.entryVolume.isTrigger=true;flow.entryVolume.size=new Vector3(5.65f,2.5f,1.0f);

            var secretary=architecture.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Secretaria — função escolar");
            var table=secretary.GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="Apoio — mesa de trabalho");
            var center=table.bounds.center;center.y=table.bounds.max.y+.009f;
            flow.documentsClue=Clue("Registro de desaparecimentos",SchoolOpeningClueKind.Documents,
                center+new Vector3(-.46f,0,-.15f),new Vector2(.30f,.39f),
                "Registro de ocorrências\n\nVários alunos desapareceram. A investigação não esclareceu o que aconteceu.\n\nUma fotografia da turma foi guardada junto destes documentos.",flow);
            PaperText(flow.documentsClue.transform,"REGISTRO DE\nOCORRÊNCIAS",.022f);
            flow.photographClue=Clue("Fotografia da turma",SchoolOpeningClueKind.Photograph,
                center+new Vector3(.06f,0,-.19f),new Vector2(.28f,.21f),
                "Uma turma diante da escola. O rosto de uma das crianças está parcialmente apagado.\n\nNo verso: Alguns nunca foram embora.",flow);
            PhotoIllustration(flow.photographClue.transform);

            var board=architecture.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Sala — quadro verde"&&t.position.y<3)
                .OrderBy(t=>Vector3.Distance(t.position,P(1303.6f,600,1.82f))).First();
            var anchor=new GameObject("Sala 02 — ponto de leitura do quadro").transform;anchor.SetParent(group,false);
            anchor.position=board.position+board.forward*.06f;flow.boardAnchor=anchor;
            var message=Text("JOÃO, VOCÊ\nVOLTOU.",anchor.position,Quaternion.identity,new Vector2(3.0f,.86f),2.5f,new Color(.76f,.79f,.70f));
            message.name="Quadro — inscrição que desaparece";flow.boardMessage=message.gameObject;message.gameObject.SetActive(false);
            var classroomDoor=architecture.GetComponentsInChildren<InteractiveDoor>(true).Single(d=>d.persistentId=="school.door.0.4");
            var doorway=classroomDoor.leafCollider.bounds.center;doorway.y=1.64f;
            // Observe the real aperture, just inside its jamb, from the classroom aisle.
            flow.corridorLookAnchor=Anchor("Sala 02 — corredor depois da batida",doorway+Vector3.right*.15f);
            flow.ghostAnchor=Anchor("Sala 03 — aparição infantil",P(1303.6f,555.7f,1.05f));
            flow.ghostVisual=Ghost(flow.ghostAnchor.position-Vector3.up*1.05f);flow.ghostVisual.SetActive(false);
            flow.drawingClue=Clue("Desenho infantil",SchoolOpeningClueKind.Drawing,
                flow.ghostAnchor.position+new Vector3(0,-1.032f,-.50f),new Vector2(.30f,.39f),
                "O desenho mostra a escola, uma criança e uma figura escura no último andar.\n\nNÃO SUBA.",flow);
            DrawingIllustration(flow.drawingClue.transform);flow.drawingClue.SetVisible(false);
            flow.corridorSlamSound=Sound("Door latch");
            flow.ghostDisappearSound=Sound("Room creaks");
            flow.schoolLights=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true))
                .Where(l=>!l.transform.IsChildOf(game.player.transform)&&l.type!=LightType.Directional).ToArray();
            // Directional night light and fog remain readable after the local fixtures fail.
            flow.blackoutDelay=2.2f;
            var mood=architecture.GetComponentInChildren<SchoolHorrorAtmosphere>(true);
            if(mood)
            {
                UnityEventTools.AddFloatPersistentListener(flow.onEntered,mood.ReactToEvent,.45f);
                UnityEventTools.AddFloatPersistentListener(flow.onCorridorSlam,mood.ReactToEvent,.7f);
                UnityEventTools.AddFloatPersistentListener(flow.onGhostDisappeared,mood.ReactToEvent,.55f);
                UnityEventTools.AddFloatPersistentListener(flow.onBlackout,mood.ReactToEvent,.8f);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(game);
            Physics.SyncTransforms();
            return flow;
        }

        static InteractiveDoor Gate(Vector3 position,bool left)
        {
            var go=new GameObject(left?"Entrada — folha esquerda":"Entrada — folha direita");go.transform.SetParent(group,false);go.transform.position=position;
            var hinge=new GameObject("Dobradiça real").transform;hinge.SetParent(go.transform,false);
            float sign=left?1:-1,width=3.205f,height=2.14f;
            var volume=new GameObject("Portão — volume da folha");volume.transform.SetParent(hinge,false);
            var leaf=volume.AddComponent<BoxCollider>();leaf.center=new Vector3(sign*width*.5f,height*.5f+.025f,0);leaf.size=new Vector3(width,height,.07f);
            foreach(float y in new[]{.05f,.79f,1.99f,2.15f})Cube(hinge,"Portão — travessa",new Vector3(sign*width*.5f,y,0),new Vector3(width,.045f,.07f),blue);
            for(int i=0;i<=19;i++)Cube(hinge,"Portão — barra vertical",new Vector3(sign*i*width/19,1.09f,0),new Vector3(.026f,2.1f,.026f),blue);
            Cube(hinge,"Portão — chapa inferior",new Vector3(sign*width*.5f,.41f,0),new Vector3(width,.68f,.028f),blue);
            Cube(hinge,"Portão — fechadura",new Vector3(sign*(width-.11f),1.02f,-.056f),new Vector3(.075f,.15f,.035f),steel);
            Cube(hinge,"Portão — puxador metálico",new Vector3(sign*(width-.22f),1.02f,-.096f),new Vector3(.035f,.24f,.035f),steel);
            foreach(float y in new[]{.24f,1.86f})Primitive(hinge,"Portão — dobradiça",new Vector3(0,y,0),new Vector3(.055f,.10f,.055f),steel,PrimitiveType.Cylinder);
            var door=go.AddComponent<InteractiveDoor>();door.persistentId=left?"school.entry.left":"school.entry.right";
            door.hinge=hinge;door.leafCollider=leaf;door.openAngle=left?-90:90;door.speed=220;
            door.requiredKey="";door.lockedMessage="A porta não abre.";door.displayName="portão da entrada";
            door.openSound=Sound("Door opening");door.closeSound=Sound("Door closing");door.latchSound=Sound("Door latch");door.lockedSound=Sound("Locked door");
            door.playerMask=LayerMask.GetMask("Player");return door;
        }
        static SchoolOpeningClue Clue(string title,SchoolOpeningClueKind kind,Vector3 p,Vector2 size,string body,SchoolOpeningFlow flow)
        {
            var go=new GameObject(title);go.transform.SetParent(group,false);go.transform.position=p;
            var clue=go.AddComponent<SchoolOpeningClue>();clue.flow=flow;clue.kind=kind;clue.title=title;clue.body=body;
            var visual=new GameObject("Objeto de investigação");visual.transform.SetParent(go.transform,false);clue.visual=visual;
            Cube(visual.transform,"Papel envelhecido com volume",Vector3.zero,new Vector3(size.x,.005f,size.y),paper);
            var collider=go.AddComponent<BoxCollider>();collider.size=new Vector3(size.x,.022f,size.y);return clue;
        }
        static void PaperText(Transform clue,string content,float y)
        {
            var text=Text(content,clue.position+Vector3.up*y,Quaternion.Euler(90,0,0),new Vector2(.26f,.31f),.25f,new Color(.13f,.12f,.09f));
            text.transform.SetParent(clue.GetComponent<SchoolOpeningClue>().visual.transform,true);
        }
        static void PhotoIllustration(Transform clue)
        {
            // Authored illustrative photograph prop; narrative caption supplies readable detail.
            var t=clue.GetComponent<SchoolOpeningClue>().visual.transform;
            var ink=Material("PhotoSepia",new Color(.19f,.18f,.14f));
            Cube(t,"Fotografia — fachada antiga",new Vector3(0,.007f,.028f),new Vector3(.25f,.002f,.12f),ink);
            for(int i=0;i<7;i++)
            {
                float x=-.105f+i*.035f;
                Cube(t,"Fotografia — turma",new Vector3(x,.009f,-.035f),new Vector3(.025f,.002f,.056f),i==4?paper:chalk);
                Primitive(t,"Fotografia — rosto",new Vector3(x,.010f,-.001f),new Vector3(.024f,.002f,.024f),i==4?paper:skin,PrimitiveType.Sphere);
            }
        }
        static void DrawingIllustration(Transform clue)
        {
            var t=clue.GetComponent<SchoolOpeningClue>().visual.transform;
            var pencil=Material("Pencil",new Color(.18f,.20f,.17f));
            foreach(float x in new[]{-.095f,.095f})Cube(t,"Desenho — parede",new Vector3(x,.008f,.035f),new Vector3(.002f,.002f,.12f),pencil);
            foreach(float z in new[]{-.023f,.094f})Cube(t,"Desenho — laje",new Vector3(0,.008f,z),new Vector3(.19f,.002f,.002f),pencil);
            for(int i=0;i<4;i++)Cube(t,"Desenho — janela",new Vector3(-.066f+i*.045f,.009f,.052f),new Vector3(.017f,.002f,.022f),pencil);
            Primitive(t,"Desenho — figura escura",new Vector3(.044f,.010f,.13f),new Vector3(.021f,.002f,.04f),hair,PrimitiveType.Sphere);
            var text=Text("NÃO SUBA",clue.position+new Vector3(0,.025f,-.115f),Quaternion.Euler(90,0,0),new Vector2(.26f,.065f),.25f,new Color(.14f,.13f,.1f));text.transform.SetParent(t,true);
        }
        static GameObject Ghost(Vector3 p)
        {
            var g=new GameObject("Aparição — criança na sala vazia");g.transform.SetParent(group,false);g.transform.position=p;
            Primitive(g.transform,"Uniforme antigo",new Vector3(0,.64f,0),new Vector3(.31f,.45f,.19f),uniform,PrimitiveType.Capsule);
            Primitive(g.transform,"Rosto sem expressão",new Vector3(0,1.12f,0),new Vector3(.22f,.26f,.21f),skin,PrimitiveType.Sphere);
            Primitive(g.transform,"Cabelo escuro",new Vector3(0,1.19f,.025f),new Vector3(.23f,.17f,.21f),hair,PrimitiveType.Sphere);
            foreach(float side in new[]{-1f,1f})
            {
                Primitive(g.transform,"Braço imóvel",new Vector3(side*.18f,.66f,0),new Vector3(.075f,.31f,.075f),uniform,PrimitiveType.Capsule);
                Primitive(g.transform,"Mão pálida",new Vector3(side*.18f,.34f,0),new Vector3(.06f,.09f,.055f),skin,PrimitiveType.Sphere);
                Primitive(g.transform,"Perna",new Vector3(side*.075f,.23f,.02f),new Vector3(.084f,.24f,.084f),uniform,PrimitiveType.Capsule);
                Primitive(g.transform,"Sapato",new Vector3(side*.075f,.042f,-.034f),new Vector3(.1f,.065f,.16f),hair,PrimitiveType.Cube);
            }
            return g;
        }
        static Transform Anchor(string name,Vector3 p){var go=new GameObject(name);go.transform.SetParent(group,false);go.transform.position=p;return go.transform;}
        static GameObject Cube(Transform t,string name,Vector3 p,Vector3 size,Material m)=>Primitive(t,name,p,size,m,PrimitiveType.Cube);
        static GameObject Primitive(Transform t,string name,Vector3 p,Vector3 size,Material m,PrimitiveType type)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(t,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
        static TextMeshPro Text(string content,Vector3 p,Quaternion q,Vector2 size,float fontSize,Color color)
        {
            var go=new GameObject(content);go.transform.SetParent(group,false);go.transform.SetPositionAndRotation(p,q);
            var text=go.AddComponent<TextMeshPro>();text.text=content;text.fontSize=fontSize;text.color=color;text.alignment=TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta=size;text.textWrappingMode=TextWrappingModes.NoWrap;text.font=TMP_Settings.defaultFontAsset;text.raycastTarget=false;
            return text;
        }
        static Material Material(string name,Color color,float metallic=0)
        {
            string path=Data+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",.22f);EditorUtility.SetDirty(mat);return mat;
        }
        static SoundSet Sound(string name)=>AssetDatabase.LoadAssetAtPath<SoundSet>("Assets/HorrorGameplay/Audio/Recorded/Sets/"+name+".asset");
    }
}
