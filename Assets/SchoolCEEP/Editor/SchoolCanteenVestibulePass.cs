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
    // An open vestibule occupies the existing covered link. No second roof is added.
    public static class SchoolCanteenVestibulePass
    {
        public const string GroupName="Cantina — salinha de passagem aberta";
        const string Block="central_east_services";
        const float Scale=.07055555556f,Center=1430f,South=808.64f,North=780f;
        const float West=1408.75f,East=1451.25f,Opening=1.80f;
        [Serializable] public sealed class Report
        {
            public bool dimensionsEstimated=true,passed,doorLeafAbsent,windowAbsent,reusesExistingCanopy=true;
            public int removedWindowSpans,openDoorSlot,sideWallPanels,wallPhysicsSamples,wallRenderSamples,clearPassageSamples;
            public float clearWidthMeters=(East-West)*Scale-.16f,openingWidthMeters=Opening;
            public string[] errors,notes;public SchoolCirculation.Route[] routes;
        }
        static int openDoorSlot,removedWindows;
        static Vector3 P(float x,float y,float h=0)=>SchoolMapBuilder.Point(x,y,h);
        public static void Prepare(SchoolMapBuilder.Layout layout)
        {
            var level=layout.levels.Single(l=>l.index==0);
            var door=level.doors.Single(d=>d.block==Block&&!d.vertical&&Mathf.Abs(d.p-South)<.1f);
            openDoorSlot=Array.IndexOf(level.doors,door);
            door.a=Center-Opening/(2*Scale);door.b=Center+Opening/(2*Scale);door.width=door.b-door.a;
            door.source=SchoolUpperEmergencyPass.SourceOpenPassage;door.provisional=true;
            var windows=level.windows.Where(w=>!(w.block==Block&&!w.vertical&&Mathf.Abs(w.p-South)<4f&&w.b>West&&w.a<East+3)).ToArray();
            removedWindows=level.windows.Length-windows.Length;level.windows=windows;
        }
        public static Report Apply(Transform architecture)
        {
            var previous=architecture.Find(GroupName);if(previous)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var group=new GameObject(GroupName).transform;group.SetParent(architecture,false);
            var plaster=AssetDatabase.LoadAssetAtPath<Material>(SchoolMapBuilder.Folder+"/Materials/PhotoPlasterWhite.mat");
            var blue=AssetDatabase.LoadAssetAtPath<Material>(SchoolMapBuilder.Folder+"/Materials/PhotoBlueLower.mat");
            if(!blue)blue=AssetDatabase.LoadAssetAtPath<Material>(SchoolMapBuilder.Folder+"/Materials/Pintura inferior.mat");
            foreach(float x in new[]{West,East})
            {
                // These walls meet the actual pitched canopy of Ligação leste.
                float roofHeight=3.30f-Mathf.Abs(x-1427.28f)/27.72f*.45f;
                Cube(group,"Parede — passagem da cantina — base",P(x,(North+South)*.5f,.57f),new Vector3(.16f,1.07f,(South-North)*Scale),blue?blue:plaster);
                Cube(group,"Parede — passagem da cantina — reboco",P(x,(North+South)*.5f,(1.105f+roofHeight)*.5f),new Vector3(.16f,roofHeight-1.105f,(South-North)*Scale),plaster);
            }
            // The full-height north facade is still the canteen boundary. Only
            // the open aperture gets a lintel; its high cap follows the canteen roof.
            Cube(architecture.Find(Block),"Verga da passagem aberta da cantina",P(Center,South,2.935f),new Vector3(Opening+.13f,.83f,.20f),plaster);
            var report=new Report{removedWindowSpans=removedWindows,openDoorSlot=openDoorSlot,sideWallPanels=4,routes=Routes(),notes=new[]{
                "The north canteen leaf and adjacent window are removed. A 1.80m open aperture leads through a 2.84m-wide small vestibule to the existing main covered corridor.",
                "The existing Ligação leste pitched canopy and floor cover this vestibule. There is no duplicated ceiling or new high roof above it.",
                "Dimensions are estimated. This change adds scene circulation, not a scripted objective or narrative event."}};
            File.WriteAllText(SchoolMapBuilder.Output+"/School-canteen-vestibule-report.json",JsonUtility.ToJson(report,true));return report;
        }
        static SchoolCirculation.Route[] Routes()
        {
            var feet=new List<Vector3>();
            var points=new[]{P(Center,828,.05f),P(Center,774,.085f),P(1462,760,.085f)};
            for(int j=0;j<points.Length-1;j++)
            {int n=Mathf.CeilToInt(Vector3.Distance(points[j],points[j+1])/.12f);for(int i=0;i<=n;i++)feet.Add(Vector3.Lerp(points[j],points[j+1],i/(float)n));}
            return new[]{new SchoolCirculation.Route{name="Cantina — passagem aberta até corredor da quadra",feet=feet.ToArray(),requireAscending=false}};
        }
        static void Cube(Transform parent,string name,Vector3 p,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=p;go.transform.localScale=size;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;go.isStatic=true;
        }
        public static Report Verify(Transform architecture)
        {
            var report=JsonUtility.FromJson<Report>(File.ReadAllText(SchoolMapBuilder.Output+"/School-canteen-vestibule-report.json"));
            var errors=new List<string>();Physics.SyncTransforms();
            report.doorLeafAbsent=!architecture.GetComponentsInChildren<InteractiveDoor>(true).Any(d=>d.persistentId=="school.door.0."+report.openDoorSlot);
            var block=architecture.Find(Block);
            report.windowAbsent=!block.GetComponentsInChildren<Collider>().Any(c=>c.name.StartsWith("Janela")&&Mathf.Abs(1628-c.bounds.center.z/Scale-South)<4&&c.bounds.max.x>P(West,0).x&&c.bounds.min.x<P(East+3,0).x);
            if(!report.doorLeafAbsent)errors.Add("Canteen passage still has a door leaf.");if(!report.windowAbsent)errors.Add("Canteen passage still has adjacent glazing.");
            foreach(float height in new[]{.60f,1.65f,2.35f})foreach(float x in new[]{Center-8,Center,Center+8})
            {
                Vector3 origin=P(x,South-4,height);
                if(Physics.RaycastAll(origin,Vector3.back,.60f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.enabled&&!h.collider.isTrigger))errors.Add("Blocked open aperture at "+origin);
                else report.clearPassageSamples++;
            }
            var group=architecture.Find(GroupName);var walls=group.GetComponentsInChildren<Collider>().Where(c=>c.name.StartsWith("Parede")).ToArray();
            var filters=group.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh&&f.GetComponent<MeshRenderer>()&&f.GetComponent<MeshRenderer>().enabled).ToArray();
            foreach(float x in new[]{West,East})foreach(float y in new[]{782f,793f,805f})foreach(float height in new[]{.60f,1.65f,2.65f})foreach(int side in new[]{-1,1})
            {
                var origin=P(x,y,height)+Vector3.right*(side*.30f);var direction=Vector3.left*side;
                if(Physics.RaycastAll(origin,direction,.60f,~0,QueryTriggerInteraction.Ignore).Any(h=>walls.Contains(h.collider)))report.wallPhysicsSamples++;else errors.Add("Missing vestibule collision at "+origin);
                if(RenderedHit(filters,origin,direction,.60f))report.wallRenderSamples++;else errors.Add("Missing vestibule rendered wall at "+origin);
            }
            report.errors=errors.ToArray();report.passed=errors.Count==0;
            File.WriteAllText(SchoolMapBuilder.Output+"/School-canteen-vestibule-proof.json",JsonUtility.ToJson(report,true));return report;
        }
        static bool RenderedHit(MeshFilter[] filters,Vector3 origin,Vector3 direction,float limit)
        {
            foreach(var f in filters)
            {
                var mesh=f.sharedMesh;var v=mesh.vertices;var tri=mesh.triangles;
                for(int i=0;i<tri.Length;i+=3)
                {
                    var a=f.transform.TransformPoint(v[tri[i]]);var b=f.transform.TransformPoint(v[tri[i+1]]);var c=f.transform.TransformPoint(v[tri[i+2]]);
                    var e1=b-a;var e2=c-a;var p=Vector3.Cross(direction,e2);float det=Vector3.Dot(e1,p);if(Mathf.Abs(det)<.000001f)continue;
                    var t=origin-a;float u=Vector3.Dot(t,p)/det;if(u<0||u>1)continue;var q=Vector3.Cross(t,e1);float w=Vector3.Dot(direction,q)/det;if(w<0||u+w>1)continue;
                    float distance=Vector3.Dot(e2,q)/det;if(distance>=0&&distance<=limit)return true;
                }
            }
            return false;
        }
    }
}
