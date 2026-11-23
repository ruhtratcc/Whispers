// Ferramenta da escola para construção ou acabamento.

using System;
using System.IO;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>Estimated taller refectory and its full-height partition against the main hall.</summary>
    public static class SchoolCanteenHeightPass
    {
        public const float Ceiling = 4.20f, RoofTop = 4.40f;
        public const float HallEave = 5.10f, HallRidge = 6.40f;
        const float Scale=.07055555556f;
        [Serializable] public sealed class Report
        {
            public bool passed, dimensionsEstimated=true, doorIdentitiesPreserved;
            public int extendedWallPieces, wallCollidersBefore, wallCollidersAfter;
            public float clearCeilingMeters=Ceiling, roofTopMeters=RoofTop, hallEaveMeters=HallEave, hallRidgeMeters=HallRidge;
            public string[] notes;
        }
        public static Report Apply(Transform architecture)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Raise canteen outside Play.");
            var block=architecture.Find("central_east_services");
            var doors=architecture.GetComponentsInChildren<InteractiveDoor>(true);
            var states=doors.Select(d=>new{d,id=d.persistentId,position=d.transform.position,hinge=d.hinge,leaf=d.leafCollider}).ToArray();
            var original=block.GetComponentsInChildren<Collider>(true);
            var report=new Report{wallCollidersBefore=original.Length};
            foreach(var renderer in block.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(renderer.name!="Parede — reboco"&&!renderer.name.StartsWith("Verga da "))continue;
                var b=renderer.bounds;
                float left=1413+b.min.x/Scale,right=1413+b.max.x/Scale,pdfY=1628-b.center.z/Scale;
                if(left>1465.1f||right>1466f||pdfY<801f||pdfY>965f||Mathf.Abs(b.max.y-3.35f)>.02f)continue;
                bool south=b.size.x>b.size.z&&pdfY>957f;
                float target=south?HallEave:Ceiling;
                float extra=target-b.max.y;
                var size=renderer.transform.localScale;size.y+=extra;renderer.transform.localScale=size;
                renderer.transform.position+=Vector3.up*(extra*.5f);report.extendedWallPieces++;
            }
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/SchoolCEEP/Materials/PhotoPlasterWhite.mat");
            // The wide hall doorway belongs to main_hall, and the adjacent sash
            // crosses the canteen footprint boundary. Their original lintels
            // stop at 3.35m; bridge the upper band without altering either opening.
            var upperBand=GameObject.CreatePrimitive(PrimitiveType.Cube);
            upperBand.name="Cantina — fecho alto sobre entrada do hall";
            upperBand.transform.SetParent(block,false);upperBand.isStatic=true;
            upperBand.transform.position=SchoolMapBuilder.Point((1409.90f+1464.16f)*.5f,960.52f,(3.35f+HallEave)*.5f);
            upperBand.transform.localScale=new Vector3((1464.16f-1409.90f)*Scale,HallEave-3.35f,.24f);
            upperBand.GetComponent<MeshRenderer>().sharedMaterial=material;
            // The high wall continues over the glazed bands and doorway lintels.
            // Its top follows the hall's actual two roof slopes, rather than a floating rectangle.
            Vector3 P(float x,float y,float h)=>SchoolMapBuilder.Point(x,y,h);
            float west=1345f,mid=1421f,east=1464.16f,p=960.52f;
            float eastTop=Mathf.Lerp(HallRidge,HallEave,(east-mid)/(1497f-mid));
            var face=new[]{P(west,p,HallEave),P(mid,p,HallEave),P(east,p,HallEave),P(west,p,HallEave),P(mid,p,HallRidge),P(east,p,eastTop)};
            var vertices=face.Select(v=>v+Vector3.forward*.12f).Concat(face.Select(v=>v-Vector3.forward*.12f)).ToArray();
            var mesh=new Mesh{name="Cantina — empena na altura do hall",vertices=vertices,
                triangles=new[]{0,4,1,1,4,5,1,5,2,6,7,10,7,11,10,7,8,11,0,1,6,1,7,6,1,2,7,2,8,7,3,9,4,4,9,10,4,10,5,5,10,11,2,5,8,5,11,8}};
            mesh.uv=vertices.Select(v=>new Vector2(v.x*.25f,v.y*.25f)).ToArray();mesh.RecalculateBounds();mesh.RecalculateNormals();
            string folder="Assets/SchoolCEEP/Meshes",path=folder+"/CanteenHallWall-"+Guid.NewGuid().ToString("N")+".asset";
            AssetDatabase.CreateAsset(mesh,path);
            var cap=new GameObject("Cantina — parede alta até o teto do hall");cap.transform.SetParent(block,false);cap.isStatic=true;
            cap.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            cap.AddComponent<MeshRenderer>().sharedMaterial=material;cap.AddComponent<MeshCollider>().sharedMesh=cap.GetComponent<MeshFilter>().sharedMesh;
            Physics.SyncTransforms();
            report.wallCollidersAfter=block.GetComponentsInChildren<Collider>(true).Length;
            report.doorIdentitiesPreserved=states.All(s=>s.d&&s.d.persistentId==s.id&&s.d.hinge==s.hinge&&s.d.leafCollider==s.leaf&&Vector3.Distance(s.d.transform.position,s.position)<.001f);
            report.passed=report.extendedWallPieces>0&&original.All(c=>c)&&report.doorIdentitiesPreserved;
            report.notes=new[]{"Clear height increases from3.32m to an estimated4.20m. Only the canteen roof footprint west ofPDFX1464.16 is raised; adjoining service rooms keep their own roof.",
                "The partition next to the hall rises to5.10m at the eave and6.40m at the ridge, matching the existing hall canopy after its0.60m grade adjustment.",
                "Native opening positions, door heights, IDs and hinges stay intact. Tables and serving counters stay at their existing working heights. Dimensions are visual estimates authorized by the user."};
            File.WriteAllText(SchoolMapBuilder.Output+"/School-canteen-height-report.json",JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException("Canteen height did not preserve openings.");
            return report;
        }
    }
}
