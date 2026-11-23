// Ferramenta da escola para construção ou acabamento.

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace SchoolCEEP.Editor
{
    public static class SchoolArchitectureBatch
    {
        public static string QueueBuild()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)
                throw new InvalidOperationException("Finish Play/compilation before repairing the school.");
            var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(active.path!=SchoolIntegrationPass.ScenePath||active.isDirty)
                throw new InvalidOperationException("The saved school scene must be active and clean.");
            File.WriteAllText(SchoolMapBuilder.Output+"/School-architecture-batch-status.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=false,operation="queued live build",started=DateTime.Now}));
            EditorApplication.delayCall+=Build;
            return "Native school repair queued";
        }
        public static void Review()
        {
            var started=DateTime.Now;
            try
            {
                EditorSceneManager.OpenScene(SchoolIntegrationPass.ScenePath,OpenSceneMode.Single);
                UnityEngine.Debug.Log("Architecture: checking room envelopes");
                SchoolArchitectureProof.Verify();
                UnityEngine.Debug.Log("Architecture: capturing views");
                SchoolArchitectureCaptures.Capture();
                UnityEngine.Debug.Log("Architecture: auditing circulation");
                SchoolWholeMapAudit.Audit();
                AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                WriteStatus(started,"review");
            }
            catch(Exception e)
            {
                File.WriteAllText(SchoolMapBuilder.Output+"/School-architecture-batch-status.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=false,operation="review",started,time=DateTime.Now,error=e.ToString()}));
                UnityEngine.Debug.LogException(e);if(UnityEngine.Application.isBatchMode)EditorApplication.Exit(1);
            }
        }
        public static void Build()
        {
            var started=DateTime.Now;
            try
            {
                EditorSceneManager.OpenScene(SchoolIntegrationPass.ScenePath,OpenSceneMode.Single);
                SchoolMapBuilder.Build();
                EditorSceneManager.OpenScene(SchoolIntegrationPass.ScenePath,OpenSceneMode.Single);
                File.WriteAllText(SchoolMapBuilder.Output+"/School-campus-build-status.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=true,started,time=DateTime.Now}));
                SchoolArchitectureProof.Verify();
                SchoolArchitectureCaptures.Capture();
                SchoolWholeMapAudit.Audit();
                AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                WriteStatus(started,"build");
            }
            catch(Exception e)
            {
                File.WriteAllText(SchoolMapBuilder.Output+"/School-architecture-batch-status.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=false,started,time=DateTime.Now,error=e.ToString()}));
                UnityEngine.Debug.LogException(e);if(UnityEngine.Application.isBatchMode)EditorApplication.Exit(1);
            }
        }
        static void WriteStatus(DateTime started,string operation)
        {
            var proof=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(SchoolMapBuilder.Output+"/School-architecture-proof.json"));
            var audit=UnityEngine.JsonUtility.FromJson<SchoolWholeMapAudit.Report>(File.ReadAllText(SchoolMapBuilder.Output+"/School-whole-map-audit.json"));
            var masonry=SchoolMasonryOptimizationPass.Inspect(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            int gaps=proof["unclosedEdgeSamples"].Count(),overlaps=proof["closedDoorWallOverlaps"].Count();
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots=scene.GetRootGameObjects();
            int cameras=roots.Sum(r=>r.GetComponentsInChildren<UnityEngine.Camera>(true).Length);
            bool temporary=roots.SelectMany(r=>r.GetComponentsInChildren<UnityEngine.Transform>(true)).Any(t=>t.name=="School capture viewpoint"||t.name=="School overview light");
            var gate=roots.SelectMany(r=>r.GetComponentsInChildren<HorrorGame.InteractiveDrawer>(true)).SingleOrDefault(g=>g.persistentId==SchoolWorkshopPhotoPass.GateId);
            var architecture=roots.Single(r=>r.name.StartsWith("CEEP")).transform;
            var doorDetails=SchoolDoorDetailPass.Verify(architecture);
            var stairEnvelope=SchoolStairEnvelopePass.Verify(architecture);
            var vestibule=SchoolCanteenVestibulePass.Verify(architecture);
            var nearGrade=SchoolNearCanteenGradePass.Verify(architecture);
            var playableBounds=SchoolPlayableBoundsPass.Verify(architecture);
            var lab=architecture.Find("main_laboratories_north");
            var finish=architecture.Find("Detalhes do central");
            var native=architecture.GetComponentsInChildren<UnityEngine.MeshFilter>(true)
                .Where(f=>f.sharedMesh&&f.GetComponent<UnityEngine.MeshRenderer>()&&f.GetComponent<UnityEngine.MeshRenderer>().enabled
                    &&(f.transform.IsChildOf(lab)||f.transform.IsChildOf(finish))).ToArray();
            int roofOverhangs=0;
            foreach(float x in new[]{660f,700f,780f})foreach(float y in new[]{745f,770f,795f})
            {
                var point=SchoolMapBuilder.Point(x,y);
                if(RenderedRoofAt(native,point))roofOverhangs++;
            }
            var west=roots.SelectMany(r=>r.GetComponentsInChildren<UnityEngine.Transform>(true)).Single(t=>t.name=="west_south");
            var westRoof=west.GetComponentsInChildren<UnityEngine.BoxCollider>(true).Single(r=>r.name=="Cobertura (sem acesso previsto)");
            var westFloor=west.GetComponentsInChildren<UnityEngine.BoxCollider>(true).Single(r=>r.name=="Piso térreo");
            float westHeight=westRoof.bounds.min.y-westFloor.bounds.max.y;
            int lawnInteriorOverlaps=LawnInteriorOverlaps(architecture);
            File.WriteAllText(SchoolMapBuilder.Output+"/School-final-details-proof.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=roofOverhangs==0&&westHeight>4.85f&&lawnInteriorOverlaps==0,roofOverhangs,lawnInteriorOverlaps,westCorridorHeight=westHeight,time=DateTime.Now}));
            bool finalState=!scene.isDirty&&!UnityEditor.EditorApplication.isPlaying&&cameras==1&&!temporary&&(!gate||!gate.IsOpen);
            File.WriteAllText(SchoolMapBuilder.Output+"/School-final-state.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{saved=scene.path,dirty=scene.isDirty,play=UnityEditor.EditorApplication.isPlaying,compiling=UnityEditor.EditorApplication.isCompiling,cameras,previewExists=temporary,temporarySunExists=temporary,workshopGateClosed=!gate||!gate.IsOpen,fogEnabled=UnityEngine.RenderSettings.fog,time=DateTime.Now}));
            bool passed=audit.passed&&masonry.passed&&gaps==0&&overlaps==0&&audit.wallJointSuspects==0&&finalState&&roofOverhangs==0&&westHeight>4.85f&&lawnInteriorOverlaps==0&&doorDetails.passed&&stairEnvelope.passed&&vestibule.passed&&nearGrade.passed&&playableBounds.passed;
            File.WriteAllText(SchoolMapBuilder.Output+"/School-architecture-batch-status.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed,operation,started,time=DateTime.Now,gaps,overlaps,auditPassed=audit.passed,masonryPassed=masonry.passed,joints=audit.wallJointSuspects,doorDetailsPassed=doorDetails.passed,stairEnvelopePassed=stairEnvelope.passed,vestibulePassed=vestibule.passed,nearGradePassed=nearGrade.passed,playableBoundsPassed=playableBounds.passed,finalState}));
        }
        static int LawnInteriorOverlaps(UnityEngine.Transform architecture)
        {
            var design=UnityEngine.JsonUtility.FromJson<SchoolArchitectureRepairPass.Report>(File.ReadAllText(SchoolMapBuilder.Output+"/School-architecture-redesign.json"));
            var lawns=architecture.GetComponentsInChildren<UnityEngine.BoxCollider>(true).Where(c=>c.name=="Jardim aberto — grama").ToArray();
            int overlaps=0;
            foreach(var room in design.roomLayout.Where(r=>r.level==0)) {
                var box=SchoolMapBuilder.CorridorBox(room.block,room.level,SchoolCampusConnectionsPass.Box(room.block,room.box));
                var nw=SchoolMapBuilder.Point(box[0],box[1]);var se=SchoolMapBuilder.Point(box[2],box[3]);
                float floor=SchoolMapBuilder.OccupiedFloorHeight(room.block,room.level);
                foreach(var lawn in lawns) {
                    var b=lawn.bounds;
                    if(b.max.y<=floor+.001f||b.min.y>floor+.10f)continue;
                    float width=UnityEngine.Mathf.Min(se.x,b.max.x)-UnityEngine.Mathf.Max(nw.x,b.min.x);
                    float depth=UnityEngine.Mathf.Min(nw.z,b.max.z)-UnityEngine.Mathf.Max(se.z,b.min.z);
                    if(width>.01f&&depth>.01f)overlaps++;
                }
            }
            return overlaps;
        }
        static bool RenderedRoofAt(UnityEngine.MeshFilter[] filters,UnityEngine.Vector3 point)
        {
            foreach(var filter in filters)
            {
                var bounds=filter.GetComponent<UnityEngine.MeshRenderer>().bounds;
                if(point.x<bounds.min.x||point.x>bounds.max.x||point.z<bounds.min.z||point.z>bounds.max.z||bounds.max.y<6.9f||bounds.min.y>7.4f)continue;
                var vertices=filter.sharedMesh.vertices;var triangles=filter.sharedMesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var a=filter.transform.TransformPoint(vertices[triangles[i]]);var b=filter.transform.TransformPoint(vertices[triangles[i+1]]);var c=filter.transform.TransformPoint(vertices[triangles[i+2]]);
                    float determinant=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
                    if(UnityEngine.Mathf.Abs(determinant)<.00001f)continue;
                    float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.z-c.z))/determinant;
                    float v=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.z-c.z))/determinant;
                    if(u<-.0001f||v<-.0001f||u+v>1.0001f)continue;
                    float height=u*a.y+v*b.y+(1-u-v)*c.y;
                    if(height>=6.9f&&height<=7.4f)return true;
                }
            }
            return false;
        }
    }
}
