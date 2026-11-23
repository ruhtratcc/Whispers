// Ferramenta da escola de validação.

using System;
using System.IO;
using System.Linq;
using HorrorGame;
using HorrorGame.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    public static class SchoolOpeningVerification
    {
        [Serializable] public sealed class Report
        {
            public string generatedUtc,scene;
            public bool passed,pairedEntry,entryOutsideSwing,cluesMounted,storyBound,noGhostColliders;
            public int phases,storyClues,ghostColliders;
            public SchoolHorrorAtmospherePass.Report atmosphere;
            public string[] errors;
        }
        static SchoolOpeningFlow Flow()=>UnityEngine.Object.FindObjectsByType<SchoolOpeningFlow>(FindObjectsInactive.Include).Single();
        public static Report Validate()
        {
            var flow=Flow();var scene=flow.gameObject.scene;var errors=new System.Collections.Generic.List<string>();
            var root=flow.transform.parent;
            bool paired=flow.entryDoor&&flow.entryDoor.pairedLeaf&&flow.entryDoor.pairedLeaf.pairedLeaf==flow.entryDoor
                &&flow.entryDoor.persistentId=="school.entry.left"&&flow.entryDoor.pairedLeaf.persistentId=="school.entry.right";
            bool outside=flow.entryVolume&&flow.entryVolume.isTrigger&&flow.entryVolume.transform.position.z-flow.entryDoor.transform.position.z
                >flow.entryDoor.leafCollider.size.x+.5f;
            var clues=new[]{flow.documentsClue,flow.photographClue,flow.drawingClue};
            bool mounted=clues.All(c=>c&&c.flow==flow&&c.visual&&c.GetComponent<BoxCollider>());
            bool story=flow.director&&flow.director.startingChapterId=="school"&&flow.director.book
                &&flow.director.book.chapters.Single(c=>c.id=="school").milestones.Length==9;
            int ghostColliders=flow.ghostVisual?flow.ghostVisual.GetComponentsInChildren<Collider>(true).Length:1;
            if(!paired)errors.Add("Entrada pareada incompleta.");if(!outside)errors.Add("Volume dentro do giro do portão.");
            if(!mounted||!story)errors.Add("Pistas ou capítulo sem ligação.");if(ghostColliders!=0)errors.Add("Aparição obstrui a sala.");
            if(!flow.boardMessage||!flow.boardAnchor||!flow.corridorLookAnchor||!flow.ghostAnchor)errors.Add("Pontos de observação faltando.");
            var atmosphere=SchoolHorrorAtmospherePass.Validate(root);if(!atmosphere.passed)errors.AddRange(atmosphere.errors);
            var report=new Report{generatedUtc=DateTime.UtcNow.ToString("O"),scene=scene.path,passed=errors.Count==0,
                pairedEntry=paired,entryOutsideSwing=outside,cluesMounted=mounted,storyBound=story,phases=9,storyClues=clues.Length,
                ghostColliders=ghostColliders,noGhostColliders=ghostColliders==0,atmosphere=atmosphere,errors=errors.ToArray()};
            File.WriteAllText(SchoolMapBuilder.Output+"/School-opening-installation.json",JsonUtility.ToJson(report,true));return report;
        }
        public static string Capture(string phase,bool effectEnabled=true)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use saved scene capture outside Play.");
            var flow=Flow();var scene=flow.gameObject.scene;var game=flow.director.game;
            var source=game.player.view.playerCamera;var preview=new GameObject("School opening review camera",typeof(Camera));
            SceneManager.MoveGameObjectToScene(preview,scene);var camera=preview.GetComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;
            var data=preview.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=true;data.volumeLayerMask=1;
            var effect=preview.AddComponent<VintageCameraEffect>();effect.ConfigureSchoolTreatment();effect.ConfigureForChapter(2);effect.enabled=effectEnabled;
            var lightObject=new GameObject("Preview of equipped flashlight",typeof(Light));lightObject.transform.SetParent(preview.transform,false);
            var flashlight=source.GetComponentsInChildren<Light>(true).FirstOrDefault(l=>l.type==LightType.Spot);
            var lamp=lightObject.GetComponent<Light>();lamp.type=LightType.Spot;lamp.intensity=flashlight?flashlight.intensity:90;
            lamp.range=flashlight?flashlight.range:25;lamp.spotAngle=flashlight?flashlight.spotAngle:55;
            lamp.color=flashlight?flashlight.color:new Color(.9f,.95f,1);lamp.shadows=LightShadows.Soft;
            bool board=flow.boardMessage.activeSelf,ghost=flow.ghostVisual.activeSelf;
            var canvases=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).Where(c=>c.enabled).ToArray();
            var pipeline=UniversalRenderPipeline.asset;var drawer=pipeline.gpuResidentDrawerMode;
            string path=SchoolMapBuilder.Output+"/School-opening-"+phase+(effectEnabled?"":"-clean")+".png";
            try
            {
                pipeline.gpuResidentDrawerMode=GPUResidentDrawerMode.Disabled;
                foreach(var canvas in canvases)canvas.enabled=false;
                camera.fieldOfView=68;
                Vector3 target;
                if(phase=="documents")
                {camera.transform.position=flow.documentsClue.transform.position+new Vector3(.0f,.58f,-.60f);target=flow.documentsClue.transform.position+Vector3.right*.12f;camera.fieldOfView=48;}
                else if(phase=="blackboard")
                {flow.boardMessage.SetActive(true);camera.transform.position=flow.boardAnchor.position+new Vector3(0,-.18f,-4.25f);target=flow.boardAnchor.position;}
                else if(phase=="ghost")
                {flow.ghostVisual.SetActive(true);camera.transform.position=flow.ghostAnchor.position+new Vector3(-3.8f,.59f,-.2f);target=flow.ghostAnchor.position;}
                else
                {flow.entryDoor.SetOpen(true,true);camera.transform.position=SchoolMapBuilder.Point(1421,1375,2.284f);target=SchoolMapBuilder.Point(1421,1035,2.8f);lamp.enabled=false;}
                camera.transform.LookAt(target);Physics.SyncTransforms();
                PresentationCapture.Capture(scene,camera,path,1600,900,null,true);return path;
            }
            finally
            {
                flow.boardMessage.SetActive(board);flow.ghostVisual.SetActive(ghost);flow.entryDoor.SetOpen(false,true);
                foreach(var canvas in canvases)if(canvas)canvas.enabled=true;
                pipeline.gpuResidentDrawerMode=drawer;UnityEngine.Object.DestroyImmediate(preview);Physics.SyncTransforms();
            }
        }
    }
}
