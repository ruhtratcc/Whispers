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
    /// <summary>Physical, visible limits for the current playable school. Does not implement story events.</summary>
    public static class SchoolPlayableBoundsPass
    {
        public const string GroupName = "Limites físicos do percurso escolar";
        public const string AutomotiveRoomName = "Oficina Automotiva";
        public const float BoundaryHeight = 1.25f;
        const string GateName = "Portão metálico — oficinas";
        const string SealName = "Automotiva — fecho permanente da passagem lateral";
        const float Scale = SchoolCirculation.PdfScale;
        [Serializable] public sealed class Segment
        {
            public string name;
            public float ax, ay, bx, by, height, thickness;
        }
        [Serializable] public sealed class Report
        {
            public int revision = 1;
            public bool passed, automotiveInaccessible, automotiveGateStatic, automotiveContentsAbsent;
            public bool technicalRoomDoorsPreserved, physicalLimitsPresent, renderedLimitsPresent, walkingCrossingsBlocked;
            public int removedInteriorGroups, removedInteriorObjects, removedInteriorRenderers, removedInteriorColliders;
            public int masonrySections, physicalSamples, renderedSamples, blockedCapsuleCrossings, remainingInteriorProps;
            public string[] preservedTechnicalDoorIds, deliberatelyInaccessibleRooms, removedObjects, failures, notes;
            public Segment[] boundaries;
            public float[] automotiveBay = {2538.48f,316.4f,2623.48f,526.88f};
        }
        static string ReportPath => SchoolMapBuilder.Output + "/School-playable-bounds-report.json";
        static Vector3 P(float x,float y,float h=0) => SchoolMapBuilder.Point(x,y,h);
        public static bool IsDeliberatelyInaccessible(string roomName) => roomName == AutomotiveRoomName;
        public static bool IsAutomotiveGateRoute(string name) => name == "Passarela principal até portão grande das oficinas";

        public static Report Apply(Transform architecture)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Apply school limits outside Play.");
            var old=architecture.Find(GroupName);if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var group=new GameObject(GroupName).transform;group.SetParent(architecture,false);
            var block=architecture.Find(SchoolWorkshopPhotoPass.Block);
            if(!block)throw new InvalidOperationException("Workshop facade is required.");
            var report=new Report{deliberatelyInaccessibleRooms=new[]{AutomotiveRoomName}};
            report.preservedTechnicalDoorIds=block.GetComponentsInChildren<InteractiveDoor>(true).Select(d=>d.persistentId).OrderBy(x=>x).ToArray();
            var contents=architecture.GetComponentsInChildren<Transform>(true).Where(t=>t.name==AutomotiveRoomName+" — função escolar").ToArray();
            var removed=new List<string>();
            foreach(var room in contents)
            {
                var parts=room.GetComponentsInChildren<Transform>(true);
                report.removedInteriorGroups++;
                report.removedInteriorObjects+=parts.Length;
                report.removedInteriorRenderers+=room.GetComponentsInChildren<Renderer>(true).Length;
                report.removedInteriorColliders+=room.GetComponentsInChildren<Collider>(true).Length;
                removed.AddRange(parts.Select(t=>t.name));
                UnityEngine.Object.DestroyImmediate(room.gameObject);
            }
            report.removedObjects=removed.ToArray();
            var gate=block.Find(GateName);
            if(!gate)throw new InvalidOperationException("Automotive exterior gate is required.");
            foreach(var interaction in gate.GetComponentsInChildren<InteractiveDrawer>(true))
            {
                interaction.RestoreState("{\"open\":false}");
                UnityEngine.Object.DestroyImmediate(interaction);
            }
            foreach(var t in gate.GetComponentsInChildren<Transform>(true))t.gameObject.isStatic=true;
            var plaster=AssetDatabase.LoadAssetAtPath<Material>(SchoolMapBuilder.Folder+"/Materials/PhotoWallTerracotta.mat");
            var cap=AssetDatabase.LoadAssetAtPath<Material>(SchoolMapBuilder.Folder+"/Materials/PhotoColumnGrey.mat");
            if(!plaster||!cap)throw new InvalidOperationException("School photo materials must exist before playable limits.");
            // The depot and five adjacent technical rooms keep their independent accesses.
            Vector3 sideA=P(2623.48f,453.56f),sideB=P(2623.48f,526.88f);
            Cube(group,SealName,(sideA+sideB)*.5f+Vector3.up*1.675f,new Vector3(.20f,3.35f,Vector3.Distance(sideA,sideB)),plaster);
            var segments=new List<Segment>();
            Garden(segments,"Jardim reservado atrás da biblioteca",1735,1030,2215,1190);
            Garden(segments,"Jardim reservado a leste das oficinas",2745,280,3000,705);
            segments.Add(S("Limite sul atrás da quadra",2215,1570,3037,1570));
            segments.Add(S("Limite do portão de entrada",1374,1628,1452,1628));
            report.boundaries=segments.ToArray();report.masonrySections=segments.Count;
            foreach(var segment in segments)
            {
                Vector3 a=P(segment.ax,segment.ay),b=P(segment.bx,segment.by),direction=(b-a).normalized;
                var body=Cube(group,segment.name+" — mureta",(a+b)*.5f+Vector3.up*(segment.height*.5f),
                    new Vector3(segment.thickness,segment.height,Vector3.Distance(a,b)),cap);
                body.transform.rotation=Quaternion.LookRotation(direction);
                var top=Cube(group,segment.name+" — capeamento",(a+b)*.5f+Vector3.up*(segment.height+.03f),
                    new Vector3(segment.thickness+.05f,.06f,Vector3.Distance(a,b)),cap);
                top.transform.rotation=body.transform.rotation;
            }
            report.notes=new[]{
                "Only the Automotive bay is inaccessible. Its furniture is removed, its sliding gate is a physical static exterior element without an interaction or save-state opening, and its east internal passage is masonry. The depot and all five neighbouring technical rooms retain their doors and contents.",
                "The covered approach to the Automotive gate is retained and ends outside the closed panel. The original campus perimeter remains; new 1.25m visible physical walls bound two scenery gardens, the rear of the court and the entrance exit.",
                "Low walls are placed away from classroom entrances, the new near-canteen ramp, the central circulation, the straight court approach and the library route. The existing player cannot jump; no invisible wall, narrative lock or gameplay event is introduced."
            };
            File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));
            return report;
        }
        static Segment S(string name,float ax,float ay,float bx,float by) => new Segment{name=name,ax=ax,ay=ay,bx=bx,by=by,height=BoundaryHeight,thickness=.22f};
        static void Garden(List<Segment> s,string name,float x0,float y0,float x1,float y1)
        {
            s.Add(S(name+" — norte",x0,y0,x1,y0));s.Add(S(name+" — leste",x1,y0,x1,y1));
            s.Add(S(name+" — sul",x1,y1,x0,y1));s.Add(S(name+" — oeste",x0,y1,x0,y0));
        }
        static GameObject Cube(Transform parent,string name,Vector3 center,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,true);
            go.transform.position=center;go.transform.localScale=size;go.isStatic=true;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }

        public static Report Verify(Transform architecture,Report expected=null)
        {
            if(expected==null)expected=JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));
            var report=JsonUtility.FromJson<Report>(JsonUtility.ToJson(expected));
            var failures=new List<string>();var group=architecture.Find(GroupName);
            if(!group)throw new InvalidOperationException("Physical school limits are absent.");
            var block=architecture.Find(SchoolWorkshopPhotoPass.Block);var gate=block.Find(GateName);
            var gateCollider=gate?gate.GetComponentsInChildren<BoxCollider>(true).SingleOrDefault(c=>c.name=="Portão — folha de correr"):null;
            report.automotiveGateStatic=gate&&gateCollider&&gate.gameObject.isStatic
                &&gate.GetComponentsInChildren<MonoBehaviour>(true).All(c=>!(c is HorrorGame.IInteractable));
            if(!report.automotiveGateStatic)failures.Add("Automotive gate must remain physical and have no interactive opening component.");
            report.automotiveContentsAbsent=!architecture.GetComponentsInChildren<Transform>(true).Any(t=>t.name==AutomotiveRoomName+" — função escolar");
            var actualTechnical=block.GetComponentsInChildren<InteractiveDoor>(true).Select(d=>d.persistentId).OrderBy(x=>x).ToArray();
            report.technicalRoomDoorsPreserved=expected.preservedTechnicalDoorIds.SequenceEqual(actualTechnical);
            if(!report.technicalRoomDoorsPreserved)failures.Add("A neighbouring technical-room/depot door was removed or changed.");
            Vector3 nw=P(2538.48f,316.4f),se=P(2623.48f,526.88f);
            report.remainingInteriorProps=architecture.GetComponentsInChildren<Collider>(true).Count(c=>c.enabled&&!c.isTrigger&&c.bounds.min.y<3.0f&&c.bounds.max.y>.25f
                &&c.bounds.center.x>nw.x+.45f&&c.bounds.center.x<se.x-.45f&&c.bounds.center.z>se.z+.45f&&c.bounds.center.z<nw.z-.45f);
            report.automotiveContentsAbsent&=report.remainingInteriorProps==0;
            if(!report.automotiveContentsAbsent)failures.Add("Automotive interior retains "+report.remainingInteriorProps+" physical props.");
            report.physicalSamples=report.renderedSamples=report.blockedCapsuleCrossings=0;
            var visual=architecture.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.sharedMesh&&m.GetComponent<Renderer>()&&m.GetComponent<Renderer>().enabled
                &&(m.transform.IsChildOf(group)||m.transform.IsChildOf(block))).ToArray();
            foreach(var segment in report.boundaries)
            {
                Vector3 a=P(segment.ax,segment.ay),b=P(segment.bx,segment.by),d=(b-a).normalized,n=Vector3.Cross(Vector3.up,d);
                var body=group.GetComponentsInChildren<BoxCollider>(true).SingleOrDefault(c=>c.name==segment.name+" — mureta");
                if(!body){failures.Add(segment.name+" has no physical wall.");continue;}
                foreach(float f in new[]{.10f,.30f,.50f,.70f,.90f})foreach(float h in new[]{.30f,.75f,1.15f})
                    Sample(segment.name,Vector3.Lerp(a,b,f)+Vector3.up*h,n,body,visual,report,failures);
                foreach(float f in new[]{.20f,.50f,.80f})
                {
                    Vector3 start=Vector3.Lerp(a,b,f)-n*.85f+Vector3.up*.06f;
                    var hits=Physics.CapsuleCastAll(start+Vector3.up*.32f,start+Vector3.up*1.48f,.32f,n,1.70f,~0,QueryTriggerInteraction.Ignore);
                    if(hits.Any(hit=>hit.collider==body))report.blockedCapsuleCrossings++;
                    else failures.Add(segment.name+" permits a standing capsule to cross.");
                }
            }
            var seal=group.GetComponentsInChildren<BoxCollider>(true).SingleOrDefault(c=>c.name==SealName);
            if(seal)foreach(float f in new[]{.1f,.3f,.5f,.7f,.9f})foreach(float h in new[]{.3f,1.5f,3.1f})
                Sample("Automotive lateral seal",Vector3.Lerp(P(2623.48f,453.56f),P(2623.48f,526.88f),f)+Vector3.up*h,Vector3.right,seal,visual,report,failures);
            else failures.Add("Automotive lateral seal is absent.");
            if(gateCollider)foreach(float f in new[]{.1f,.3f,.5f,.7f,.9f})foreach(float h in new[]{.3f,1.5f,2.9f})
            {
                Vector3 point=P(Mathf.Lerp(SchoolWorkshopPhotoPass.GateLeft,SchoolWorkshopPhotoPass.GateRight,f),SchoolWorkshopPhotoPass.Front+3f,h);
                Sample("Static automotive gate",point,Vector3.forward,gateCollider,visual,report,failures);
            }
            report.physicalLimitsPresent=report.physicalSamples==report.boundaries.Length*15+30;
            report.renderedLimitsPresent=report.renderedSamples==report.physicalSamples;
            report.walkingCrossingsBlocked=report.blockedCapsuleCrossings==report.boundaries.Length*3;
            report.automotiveInaccessible=report.automotiveGateStatic&&seal&&report.automotiveContentsAbsent;
            report.failures=failures.ToArray();report.passed=failures.Count==0&&report.physicalLimitsPresent&&report.renderedLimitsPresent&&report.walkingCrossingsBlocked;
            File.WriteAllText(SchoolMapBuilder.Output+"/School-playable-bounds-proof.json",JsonUtility.ToJson(report,true));return report;
        }
        static void Sample(string name,Vector3 point,Vector3 normal,Collider expected,MeshFilter[] rendered,Report report,List<string> failures)
        {
            var ray=new Ray(point-normal*.50f,normal);
            if(Physics.RaycastAll(ray,1f,~0,QueryTriggerInteraction.Ignore).Any(hit=>hit.collider==expected))report.physicalSamples++;
            else failures.Add(name+" lacks native collision at "+point);
            if(RenderedHit(rendered,ray,1f))report.renderedSamples++;
            else failures.Add(name+" lacks a visible mesh face at "+point);
        }
        static bool RenderedHit(MeshFilter[] filters,Ray ray,float maximum)
        {
            foreach(var filter in filters)
            {
                var renderer=filter.GetComponent<Renderer>();
                if(!renderer.bounds.IntersectRay(ray,out float enter)||enter>maximum)continue;
                var mesh=filter.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    Vector3 a=filter.transform.TransformPoint(vertices[triangles[i]]),b=filter.transform.TransformPoint(vertices[triangles[i+1]]),c=filter.transform.TransformPoint(vertices[triangles[i+2]]);
                    Vector3 e1=b-a,e2=c-a,p=Vector3.Cross(ray.direction,e2);float determinant=Vector3.Dot(e1,p);
                    if(Mathf.Abs(determinant)<.000001f)continue;float inv=1f/determinant;
                    Vector3 t=ray.origin-a;float u=Vector3.Dot(t,p)*inv;if(u<-.00001f||u>1.00001f)continue;
                    Vector3 q=Vector3.Cross(t,e1);float v=Vector3.Dot(ray.direction,q)*inv;if(v<-.00001f||u+v>1.00001f)continue;
                    float distance=Vector3.Dot(e2,q)*inv;if(distance>=0&&distance<=maximum)return true;
                }
            }
            return false;
        }
    }
}
