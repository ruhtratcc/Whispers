// Ferramenta da escola para construção ou acabamento.

using System;
using System.IO;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    public static class SchoolReviewPass
    {
        public static SchoolValidationPass.ValidationReport Validate()
        {
            var scene=SceneManager.GetActiveScene();
            var report=JsonUtility.FromJson<SchoolCirculation.BuildReport>(File.ReadAllText(SchoolMapBuilder.Output+"/School-circulation-report.json"));
            var grade=JsonUtility.FromJson<SchoolHallGrade.BuildReport>(File.ReadAllText(SchoolMapBuilder.Output+"/School-hall-grade-report.json"));
            var connector=JsonUtility.FromJson<SchoolCanteenConnector.Report>(File.ReadAllText(SchoolMapBuilder.Output+"/School-canteen-connector-report.json"));
            var west=JsonUtility.FromJson<SchoolCirculation.BuildReport>(File.ReadAllText(SchoolMapBuilder.Output+"/School-west-block-access-report.json"));
            var campus=JsonUtility.FromJson<SchoolCirculation.BuildReport>(File.ReadAllText(SchoolMapBuilder.Output+"/School-campus-connections-report.json"));
            var routes=report.routes.Concat(grade.routes).Concat(connector.routes).Concat(west.routes).Concat(campus.routes).Select(r=>new SchoolValidationPass.TraversalPath{name=r.name,footPositions=r.feet,isStairRoute=r.requireAscending}).Concat(new[]{
                new SchoolValidationPass.TraversalPath{name="Entrada e hall principal",footPositions=new[]{SchoolMapBuilder.Point(1421,1588,.07f),SchoolMapBuilder.Point(1421,1442,.07f),SchoolMapBuilder.Point(1421,1341,.70f),SchoolMapBuilder.Point(1421,990,.70f)}},
                new SchoolValidationPass.TraversalPath{name="Passarela externa leste",footPositions=new[]{SchoolMapBuilder.Point(1460,760,.07f),SchoolMapBuilder.Point(2640,760,.07f)}}
            }).ToArray();
            var coordinates=new[]{new[]{1413f,1560f,0f},new[]{1413f,1300f,.60f},new[]{1460f,1090f,.60f},new[]{1570f,760f,0f},new[]{1990f,760f,0f},new[]{2450f,760f,0f},new[]{846f,500f,0f},new[]{1193f,500f,0f},new[]{846f,500f,3.81f},new[]{1193f,500f,3.81f},new[]{2405f,972f,0f},new[]{2405f,972f,3.81f},new[]{2766f,1382f,.075f},new[]{1255f,1060f,.04f},new[]{1645f,1080f,.04f},new[]{1427.6f,956f,.04f}};
            var probes=coordinates.Select((p,i)=>new SchoolValidationPass.WalkProbe{name="Circulation "+i,footPosition=SchoolMapBuilder.Point(p[0],p[1],p[2])}).ToArray();
            var result=SchoolValidationPass.Validate(scene,SchoolMapBuilder.Point(1413,1588,.05f),probes,routes);
            File.WriteAllText(SchoolMapBuilder.Output+"/School-validation-report.json",JsonUtility.ToJson(result,true));
            return result;
        }

        public static string Capture(string view, Vector3? positionOverride = null, Vector3? targetOverride = null)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Capture the saved school outside Play.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=SchoolIntegrationPass.ScenePath)throw new InvalidOperationException("Open SchoolCEEP before capturing.");
            var player=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<FirstPersonMotor>(true)).Single();
            var source=player.view.playerCamera;
            var preview=new GameObject("School capture viewpoint",typeof(Camera));SceneManager.MoveGameObjectToScene(preview,scene);
            var camera=preview.GetComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;
            bool daylight=view=="overview"||view.EndsWith("-layout");
            var data=preview.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=!daylight;data.volumeLayerMask=1;
            if(!daylight) {
                var vintage=preview.AddComponent<VintageCameraEffect>();vintage.ConfigureForChapter(2);
            }
            bool fog=RenderSettings.fog;
            Light previousSun=RenderSettings.sun;
            GameObject previewSun=null;
            Color ambientSky=RenderSettings.ambientSkyColor,ambientEquator=RenderSettings.ambientEquatorColor,ambientGround=RenderSettings.ambientGroundColor;
            var pipeline=UniversalRenderPipeline.asset;var previousResident=pipeline.gpuResidentDrawerMode;
            var canvases=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Canvas>(true)).Where(c=>c.enabled).ToArray();
            string path=SchoolMapBuilder.Output+"/School-"+view+".png";
            try {
                pipeline.gpuResidentDrawerMode=GPUResidentDrawerMode.Disabled;
                foreach(var canvas in canvases)canvas.enabled=false;
                camera.farClipPlane=400;camera.clearFlags=CameraClearFlags.SolidColor;
                if(daylight) {
                    RenderSettings.fog=false;
                    RenderSettings.ambientSkyColor=new Color(.55f,.58f,.60f);RenderSettings.ambientEquatorColor=new Color(.35f,.37f,.35f);RenderSettings.ambientGroundColor=new Color(.22f,.24f,.20f);
                    previewSun=new GameObject("School overview light",typeof(Light)){hideFlags=HideFlags.HideAndDontSave};
                    SceneManager.MoveGameObjectToScene(previewSun,scene);
                    previewSun.transform.rotation=Quaternion.Euler(50,-35,0);
                    var light=previewSun.GetComponent<Light>();light.type=LightType.Directional;light.color=Color.white;light.intensity=2.0f;light.shadows=LightShadows.None;
                    RenderSettings.sun=light;
                    camera.backgroundColor=new Color(.16f,.20f,.25f);
                    if(view=="overview") {
                        camera.orthographic=true;camera.orthographicSize=67;
                        camera.transform.position=SchoolMapBuilder.Point(1720,2510,152);camera.transform.LookAt(SchoolMapBuilder.Point(1650,920,0));
                    } else {
                        camera.orthographic=false;camera.fieldOfView=72;
                        if(view=="hall-left-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1243,1035,1.75f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1310,992,1.2f));
                        } else if(view=="canteen-connector-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1260,933,1.64f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1260,830,1.72f));
                        } else if(view=="west-passage-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1304,1160,1.64f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1048,1160,1.45f));
                        } else if(view=="west-ramp-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1335,1128,2.75f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1288,1032,.8f));
                        } else if(view=="workshops-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(2614,655,2.05f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(2635,526.88f,1.7f));
                        } else if(view=="automotive-right-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(2810,635,2.1f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(2695,487,1.5f));
                         } else if(view=="opposite-block-access-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(540,775,2.20f);camera.transform.LookAt(SchoolMapBuilder.Point(408,814,2.0f));camera.fieldOfView=72;
                        } else if(view=="west-ramp-connection-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(780,837,2.0f);camera.transform.LookAt(SchoolMapBuilder.Point(537.564f,819.968f,3.14f));
                        } else if(view=="workshop-connection-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(2578.5f,740,1.64f);camera.transform.LookAt(SchoolMapBuilder.Point(2578.5f,526.88f,1.9f));
                        } else if(view=="west-ramp-joints-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(537.564f,837,3.14f);camera.transform.LookAt(SchoolMapBuilder.Point(434,784,1.90f));camera.fieldOfView=75;
                        } else if(view=="campus-branches-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(2570,799,1.64f);camera.transform.LookAt(SchoolMapBuilder.Point(2570,1240,2.0f));
                        } else if(view=="court-photo-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(2766,965,2.1f);camera.transform.LookAt(SchoolMapBuilder.Point(2766,1330,4.6f));camera.fieldOfView=78;
                        } else if(view=="court-interior-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(2630,1245,1.72f);camera.transform.LookAt(SchoolMapBuilder.Point(2790,1510,4f));
                        } else if(view=="receded-blocks-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(2190,760,1.72f);camera.transform.LookAt(SchoolMapBuilder.Point(2480,814,2.3f));
                        } else if(view=="opposite-block-high-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(483.64f,903,3.16f);camera.transform.LookAt(SchoolMapBuilder.Point(483.64f,1290,3.22f));camera.fieldOfView=68;
                        } else if(view=="opposite-block-low-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(532,305,1.64f);camera.transform.LookAt(SchoolMapBuilder.Point(526,720,1.80f));camera.fieldOfView=72;
                        } else if(view=="refectory-interior-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1340,824,1.64f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1418,932,1.7f));
                        } else if(view=="canteen-hall-wall-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1421,1030,3.50f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1410,960.52f,4.80f));
                        } else if(view=="refectory-connector-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1260,826,1.64f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1360,836.74f,1.64f));
                        } else if(view=="colored-central-passage-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1018,825,1.64f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1018,895,1.6f));
                        } else if(view=="colored-library-aisle-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1916,822,1.64f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1916,982,1.6f));
                        } else if(view=="central-corridor-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1193,711,1.64f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1193,380,1.72f));
                        } else if(view=="central-stair-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1002.64f,818,1.64f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1002.64f,737,2.90f));
                        } else if(view=="central-upper-landing-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1034.82f,835,5.45f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1018,780,4.9f));
                        } else if(view=="central-upper-corridor-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(846,710,5.45f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(846,385,5.48f));
                        } else if(view=="upper-right-corridor-layout"||view=="upper-right-ceiling-layout") {
                            float x=SchoolMapBuilder.CorridorX("main_classrooms_north",1,1193f);
                            bool ceiling=view=="upper-right-ceiling-layout";
                            camera.transform.position=SchoolMapBuilder.Point(x,ceiling?730f:711f,ceiling?5.65f:5.45f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(x,ceiling?640f:380f,ceiling?6.65f:5.60f));
                        } else if(view=="upper-classroom-door-layout") {
                            var classroomDoor=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<InteractiveDoor>()).Single(d=>d.persistentId=="school.door.1.1");
                            var center=classroomDoor.leafCollider.bounds.center;
                            camera.transform.position=center+classroomDoor.leafCollider.transform.forward*1.9f+Vector3.up*.46f;
                            camera.transform.LookAt(center);
                        } else if(view=="upper-left-emergency-layout"||view=="upper-right-emergency-layout") {
                            bool right=view=="upper-right-emergency-layout";
                            float x=SchoolMapBuilder.CorridorX(right?"main_classrooms_north":"main_laboratories_north",1,right?1192.475f:846.635f);
                            camera.transform.position=SchoolMapBuilder.Point(x,405f,5.45f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(x,337.22f,5.30f));
                        } else if(view=="ramp-return-gap-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1004f,904f,3.50f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(989f,899f,2.52f));
                        } else if(view=="ramp-return-gap-ground-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(979f,891f,1.64f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(989f,901f,2.15f));
                        } else if(view=="ramp-walls-roof-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1041.76f,903f,5.45f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1018f,1028f,4.3f));
                        } else if(view=="classroom-front-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1325f,710f,5.52f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1300f,600f,5.35f));
                        } else if(view=="classroom-ceiling-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1368f,616f,5.62f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1260f,691f,6.18f));
                        } else if(view=="ramp-opposite-roof-layout") {
                            camera.transform.position=SchoolMapBuilder.Point(1016f,1150f,1.95f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1016f,970f,5.35f));
                        } else if(view=="central-ground-ramp-door-layout"||view=="central-upper-ramp-door-layout") {
                            bool upper=view=="central-upper-ramp-door-layout";
                            float x=upper?1041.76f:980.46f,h=upper?5.45f:1.64f;
                            camera.transform.position=SchoolMapBuilder.Point(x,823,h);
                            camera.transform.LookAt(SchoolMapBuilder.Point(x,898,h));
                        } else {
                            camera.transform.position=SchoolMapBuilder.Point(1690,1115,2.0f);
                            camera.transform.LookAt(SchoolMapBuilder.Point(1493.8f,1060,1.65f));
                        }
                    }
                } else {
                    Vector3 position,target;
                    if(view=="entrance") {position=SchoolMapBuilder.Point(1421,1460,1.64f);target=SchoolMapBuilder.Point(1421,1343,3.25f);}
                    else if(view=="main-hall") {position=SchoolMapBuilder.Point(1421,1315,2.24f);target=SchoolMapBuilder.Point(1421,980,3.2f);}
                    else if(view=="hall-left") {position=SchoolMapBuilder.Point(1328,1085,2.24f);target=SchoolMapBuilder.Point(1190,1030,3.35f);}
                    else if(view=="canteen-connector") {position=SchoolMapBuilder.Point(1260,933,1.64f);target=SchoolMapBuilder.Point(1260,830,1.72f);}
                    else if(view=="hall-right") {position=SchoolMapBuilder.Point(1718,1135,1.64f);target=SchoolMapBuilder.Point(1465,1080,1.75f);}
                    else if(view=="refectory") {position=SchoolMapBuilder.Point(1427.6f,1030,2.24f);target=SchoolMapBuilder.Point(1427.6f,951,1.95f);}
                    else if(view=="exterior-path") {position=SchoolMapBuilder.Point(2285,760,1.64f);target=SchoolMapBuilder.Point(1950,760,1.9f);}
                    else if(view=="corridor") {position=SchoolMapBuilder.Point(1193,711,1.64f);target=SchoolMapBuilder.Point(1193,380,1.74f);}
                    else if(view=="upper-floor") {position=SchoolMapBuilder.Point(846,710,5.45f);target=SchoolMapBuilder.Point(846,385,5.48f);}
                    else {position=SchoolMapBuilder.Point(2447,1120,1.64f);target=SchoolMapBuilder.Point(2740,1380,1.7f);}
                    camera.orthographic=false;camera.fieldOfView=68;camera.transform.position=position;camera.transform.LookAt(target);
                }
                if(positionOverride.HasValue&&targetOverride.HasValue) {
                    camera.orthographic=false;camera.fieldOfView=72;
                    camera.transform.position=positionOverride.Value;camera.transform.LookAt(targetOverride.Value);
                }
                HorrorGame.Editor.PresentationCapture.Capture(scene,camera,path,1600,900,null,true);
                return path;
            } finally {
                foreach(var canvas in canvases)if(canvas)canvas.enabled=true;
                pipeline.gpuResidentDrawerMode=previousResident;
                RenderSettings.fog=fog;RenderSettings.ambientSkyColor=ambientSky;RenderSettings.ambientEquatorColor=ambientEquator;RenderSettings.ambientGroundColor=ambientGround;
                RenderSettings.sun=previousSun;
                if(previewSun)UnityEngine.Object.DestroyImmediate(previewSun);
                UnityEngine.Object.DestroyImmediate(preview);
            }
        }
    }
}
