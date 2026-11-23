// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    public static class SchoolNearCanteenGradePass
    {
        public const string Block="east_north_classrooms",GroupName="Bloco junto à cantina — acesso rebaixado";
        public const float Floor=-.18f,High=.044f;
        const float Scale=.07055555556f,StartY=725,EndY=739.24f,CenterX=1672.50f,Width=(1701.20f-1643.8f)*Scale;
        public static readonly float[][] GroundHoles={
            new[]{1517f,298.28f,1701.20f,725f},new[]{1643.8f,725f,1701.20f,739.24f},
            new[]{373.12f,261.32f,557.44f,749f},new[]{345f,743f,562f,829.2f}};
        [Serializable] public sealed class Report
        {
            public bool dimensionsEstimated=true,passed,lowerFloorSupported,upperLandingConnected;
            public float floor=Floor,rise=High-Floor,run=(EndY-StartY)*Scale,width=Width;
            public int supportSamples,blockedSamples,renderedDeckSamples,railRuns,edgeDeckSamples,edgeClearanceSamples;
            public string[] errors,notes;public SchoolCirculation.Route[] routes;
        }
        static Transform parent;static string meshFolder;static Material concrete,steel;
        static Vector3 P(float x,float y,float h=0)=>SchoolMapBuilder.Point(x,y,h);
        static float Elevation(float y)=>Mathf.Lerp(Floor,High,Mathf.InverseLerp(StartY,EndY,y));
        public static Report Apply(Transform architecture)
        {
            var old=architecture.Find(GroupName);if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
            parent=new GameObject(GroupName).transform;parent.SetParent(architecture,false);
            concrete=AssetDatabase.LoadAssetAtPath<Material>(SchoolMapBuilder.Folder+"/Materials/PhotoConcreteFloor.mat");
            steel=AssetDatabase.LoadAssetAtPath<Material>(SchoolMapBuilder.Folder+"/Materials/PhotoFrameBlue.mat");
            meshFolder=SchoolMapBuilder.Folder+"/Meshes/NearCanteenGrade-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");Directory.CreateDirectory(meshFolder);
            var a=P(CenterX,StartY,Floor);var b=P(CenterX,EndY,High);
            Vector3 n=Vector3.right*(Width*.5f);
            var v=new[]{a-n,a+n,b-n,b+n,a-n-Vector3.up*.18f,a+n-Vector3.up*.18f,b-n-Vector3.up*.18f,b+n-Vector3.up*.18f};
            MeshSolid("Bloco rebaixado — soleira inclinada de concreto",v);
            var points=new[]{P(1675,710,Floor+.05f),P(1675,StartY,Floor+.05f),P(1675,EndY,High+.05f),P(1675,760,High+.05f)};
            var feet=new List<Vector3>();for(int j=0;j<points.Length-1;j++){int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(points[j],points[j+1])/.10f));for(int i=0;i<=count;i++)feet.Add(Vector3.Lerp(points[j],points[j+1],i/(float)count));}
            var report=new Report{railRuns=0,routes=new[]{new SchoolCirculation.Route{name="Bloco junto à cantina — soleira curta até corredor da quadra",feet=feet.ToArray(),requireAscending=true}},notes=new[]{
                "The orange classroom wing immediately opposite the canteen is lowered by an estimated 0.18m. Walls, floors, doors, glazing, classroom furniture and fixtures use the same source floor elevation.",
                "One short concrete threshold, 1.0058m long and 4.05m wide, joins the gallery mouth directly to the covered walkway. It softens a single step of 0.224m and has no handrails, columns on its deck, intermediate landings or long sideways ramp.",
                "The site slabs are cut below this lower floor, rather than burying the rooms under the previous continuous courtyard slab. Dimensions are estimated and no narrative event is added."}};
            File.WriteAllText(SchoolMapBuilder.Output+"/School-near-canteen-grade-report.json",JsonUtility.ToJson(report,true));return report;
        }
        static void Slab(string name,float x0,float y0,float x1,float y1,float top)=>Cube(name,P((x0+x1)*.5f,(y0+y1)*.5f,top-.09f),new Vector3((x1-x0)*Scale,.18f,(y1-y0)*Scale),concrete);
        static void Cube(string name,Vector3 p,Vector3 size,Material mat)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=p;go.transform.localScale=size;go.GetComponent<MeshRenderer>().sharedMaterial=mat;go.isStatic=true;}
        static void Beam(string name,Vector3 a,Vector3 b,float diameter)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);go.transform.position=(a+b)*.5f;go.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);go.transform.localScale=new Vector3(diameter,Vector3.Distance(a,b)*.5f,diameter);go.GetComponent<MeshRenderer>().sharedMaterial=steel;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.isStatic=true;}
        static void MeshSolid(string name,Vector3[] v)
        {
            var mesh=new Mesh{name=name,vertices=v,triangles=new[]{0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,4,1,5,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,5,3,7}};
            var indices=mesh.triangles;for(int i=0;i<indices.Length;i+=3){int swap=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=swap;}mesh.triangles=indices;
            mesh.uv=v.Select(p=>new Vector2(p.x*.35f,p.z*.35f)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,meshFolder+"/Ramp.asset");
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.isStatic=true;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=concrete;go.AddComponent<MeshCollider>().sharedMesh=mesh;go.AddComponent<SurfaceType>().surface=GroundSurface.Concrete;
        }
        public static Report Verify(Transform architecture)
        {
            var report=JsonUtility.FromJson<Report>(File.ReadAllText(SchoolMapBuilder.Output+"/School-near-canteen-grade-report.json"));var errors=new List<string>();Physics.SyncTransforms();
            var roots=architecture.gameObject.scene.GetRootGameObjects();var player=roots.SelectMany(r=>r.GetComponentsInChildren<FirstPersonMotor>(true)).Single();var capsule=player.GetComponent<CharacterController>();
            foreach(var route in report.routes)foreach(var expected in route.feet)
            {
                var hits=Physics.RaycastAll(expected+Vector3.up*.35f,Vector3.down,.70f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.normal.y>.5f&&!h.collider.transform.IsChildOf(player.transform)).OrderBy(h=>h.distance).ToArray();
                if(hits.Length==0||Mathf.Abs(hits[0].point.y-(expected.y-.05f))>.12f){errors.Add("Lower-block route lacks matching support at "+expected);continue;}
                report.supportSamples++;var foot=expected;foot.y=hits[0].point.y+.045f;float r=capsule.radius-.015f;
                if(Physics.OverlapCapsule(foot+Vector3.up*r,foot+Vector3.up*(capsule.height-r),r,~0,QueryTriggerInteraction.Ignore).Any(c=>!c.transform.IsChildOf(player.transform)))report.blockedSamples++;
            }
            var ramp=architecture.Find(GroupName).GetComponentsInChildren<MeshCollider>().Single(c=>c.name=="Bloco rebaixado — soleira inclinada de concreto");
            var renderer=ramp.GetComponent<MeshRenderer>();var mesh=ramp.GetComponent<MeshFilter>().sharedMesh;
            foreach(float y in new[]{726f,728f,731f,734f,738f})foreach(float offset in new[]{-1.80f,0f,1.80f})
            {
                var p=P(CenterX,y,Elevation(y)+.75f)+Vector3.right*offset;var ray=new Ray(p,Vector3.down);
                var hits=Physics.RaycastAll(ray,1.2f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.normal.y>.5f&&!h.collider.transform.IsChildOf(player.transform)).OrderBy(h=>h.distance).ToArray();
                if(hits.Length>0&&hits[0].collider==ramp&&Mathf.Abs(hits[0].point.y-Elevation(y))<.005f)report.edgeDeckSamples++;
                else errors.Add("A terrain or walkway slab buries the lowered ramp edge at "+p);
                if(mesh&&renderer.enabled&&ramp.Raycast(ray,out var hit,1.2f)&&Mathf.Abs(hit.point.y-Elevation(y))<.005f&&RenderedTop(mesh,ramp.transform,ray,Elevation(y)))report.renderedDeckSamples++;
            }
            var floor=architecture.Find(Block).GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="Piso térreo");report.lowerFloorSupported=Mathf.Abs(floor.bounds.max.y-Floor)<.005f;
            foreach(float y in new[]{725f,726f,728f,731f,734f,738f,739.24f})foreach(float offset in new[]{-1.50f,1.50f})
            {
                var foot=P(CenterX,y,Elevation(y)+.045f)+Vector3.right*offset;float r=capsule.radius-.015f;
                if(Physics.OverlapCapsule(foot+Vector3.up*r,foot+Vector3.up*(capsule.height-r),r,~0,QueryTriggerInteraction.Ignore).Any(c=>!c.transform.IsChildOf(player.transform)))errors.Add("Short threshold usable width is obstructed at "+foot);
                else report.edgeClearanceSamples++;
            }
            report.upperLandingConnected=report.routes[0].feet.Last().y>0&&report.supportSamples==report.routes[0].feet.Length;
            if(!report.lowerFloorSupported||!report.upperLandingConnected||report.blockedSamples>0||report.renderedDeckSamples!=15||report.edgeDeckSamples!=15||report.edgeClearanceSamples!=14)errors.Add("Lower classroom wing/ramp is incomplete or blocked.");
            report.errors=errors.Distinct().ToArray();report.passed=report.errors.Length==0;
            File.WriteAllText(SchoolMapBuilder.Output+"/School-near-canteen-grade-proof.json",JsonUtility.ToJson(report,true));return report;
        }
        static bool RenderedTop(Mesh mesh,Transform transform,Ray ray,float height)
        {
            var v=mesh.vertices;var t=mesh.triangles;
            for(int i=0;i<t.Length;i+=3){Vector3 a=transform.TransformPoint(v[t[i]]),b=transform.TransformPoint(v[t[i+1]]),c=transform.TransformPoint(v[t[i+2]]);
                if(Vector3.Cross(b-a,c-a).normalized.y<.5f)continue;
                float den=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(den)<.000001f)continue;
                float u=((b.z-c.z)*(ray.origin.x-c.x)+(c.x-b.x)*(ray.origin.z-c.z))/den;
                float w=((c.z-a.z)*(ray.origin.x-c.x)+(a.x-c.x)*(ray.origin.z-c.z))/den;
                if(u>=-.0001f&&w>=-.0001f&&u+w<=1.0001f&&Mathf.Abs(u*a.y+w*b.y+(1-u-w)*c.y-height)<.005f)return true;
            }return false;
        }
    }
}
