// Ferramenta da escola para construção ou acabamento.

using System;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>User-confirmed closures of the core/ramp interface, without closing the registered ramp lanes.</summary>
    public static class SchoolCentralEnclosurePass
    {
        public const float BoundaryY=887.96f;
        const float PreviousBoundaryY=851.12f;
        public const string Source="User-confirmed central/ramp enclosure";
        public const float UpperDoorEndPdfX=1052.08f,UpperDoorHeightMeters=2.28f;
        public const float RampDoorJambClearanceMeters=.065f,GroundAdjacentJambClearanceMeters=.09f;
        static readonly float[] Starts={971.92f,1031.44f}, Ends={989f,UpperDoorEndPdfX};
        static readonly float[] WallEnds={1076.20f,1079.32f};
        [Serializable] public sealed class Report
        {
            public bool dimensionsEstimated=true,existingDoorIdsPreserved=true;
            public int rampDoors,fullHeightStairWalls,upperParapets,trimmedUpperRampGuardGroups;
            public string[] doorIds,notes;
            public float dividerTopMeters=7.10f,upperParapetMeters=1.10f;
            public float boundaryPdfY=BoundaryY,wallMovedTowardRampMeters=(BoundaryY-PreviousBoundaryY)*SchoolCirculation.PdfScale;
            public float upperDoorWidthMeters=(UpperDoorEndPdfX-1031.44f)*SchoolCirculation.PdfScale,upperDoorHeightMeters=UpperDoorHeightMeters;
            public float rampDoorJambClearanceMeters=RampDoorJambClearanceMeters,groundAdjacentJambClearanceMeters=GroundAdjacentJambClearanceMeters;
        }
        public static void Prepare(SchoolMapBuilder.Layout layout)
        {
            foreach(var level in layout.levels)
            {
                int f=level.index;
                // The 10cm leaf needs clearance at its hinge and free edge throughout
                // the approved +/-95-degree arc. Keep the hinge and leaf dimensions.
                float jambGap=RampDoorJambClearanceMeters/SchoolCirculation.PdfScale;
                var spans=new[]{Wall(BoundaryY,961.36f,Starts[f]-jambGap),Wall(BoundaryY,Ends[f]+jambGap,WallEnds[f])};
                if(f==0)
                {
                    // This parallel CAD segment sits 20cm south of the ground hinge.
                    // Only its hinge-side end enters the leaf beyond60 degrees.
                    var adjacent=level.walls.Where(w=>w.block=="main_coordination_south"&&!w.vertical
                        &&Mathf.Abs(w.p-890.78f)<.01f&&Mathf.Abs(w.a-961.36f)<.01f&&Mathf.Abs(w.b-Starts[0])<.01f).ToArray();
                    if(adjacent.Length!=1)throw new InvalidOperationException("Expected the single ground ramp/Coordination jamb segment.");
                    adjacent[0].b=Starts[0]-GroundAdjacentJambClearanceMeters/SchoolCirculation.PdfScale;
                }
                if(f==0)spans=spans.Concat(new[]{Wall(809.24f,1022.75f,1049.92f)}).ToArray();
                level.walls=level.walls.Concat(spans).ToArray();
                // Append only: every pre-existing persistent numeric door ID retains its index.
                level.doors=level.doors.Concat(new[]{new SchoolMapBuilder.Span{block="main_middle_circulation",source=Source,
                    vertical=false,p=BoundaryY,a=Starts[f],b=Ends[f],width=Ends[f]-Starts[f]}}).ToArray();
            }
        }
        static SchoolMapBuilder.Span Wall(float p,float a,float b)=>new SchoolMapBuilder.Span
            {block="main_middle_circulation",source=Source,vertical=false,p=p,a=a,b=b,thickness=2.13f};
        public static Report Apply(Transform root,Transform parent)
        {
            var white=AssetDatabase.LoadAssetAtPath<Material>("Assets/SchoolCEEP/Materials/PhotoCentralWhite.mat");
            var tile=AssetDatabase.LoadAssetAtPath<Material>("Assets/SchoolCEEP/Materials/PhotoCentralWallTiles.mat");
            var cream=AssetDatabase.LoadAssetAtPath<Material>("Assets/SchoolCEEP/Materials/PhotoDoorCream.mat");
            if(!white||!tile||!cream)throw new InvalidOperationException("Central materials must exist first.");
            var block=root.Find("main_middle_circulation");
            var report=new Report{notes=new[]{
                "User confirms the two ramp doorway walls farther south, toward the ramp. Estimated placement at PDF y887.96 joins the existing side masonry after the Coordination/Auditorium double-door apertures, without solid side returns through those doors.",
                "A ground-only wall closes the unused opening below the east stair exit; the west stair entrance and upper east exit stay open.",
                "The central stair divider reaches the upper ceiling. Solid stair skins receive continuous physical collision outside the full tread widths; original tread collision remains intact, while the two guards projecting into the transverse corridor are omitted.",
                "The small upper wall protects the lower-flight void at the upper landing. It does not cross the upper stair exit or the north return.",
                "New single doors reuse the existing interactive prefab and persistent/save behavior, appended after all existing door entries. Fourteen established double-door pairs are preserved.",
                "Native jambs leave6.5cm beside both unchanged ramp leaves. The one parallel ground Coordination segment is shortened by9cm at the same hinge, leaving its remaining wall and neighboring double-door apertures intact. The full +/-95-degree sweeps must be verified against all active colliders.",
                "The upper connector guards start just south of the relocated wall, over the supported gallery. Exposed ramp/landing edges remain guarded. Ground leaf opens toward the ramp; upper leaf opens into the gallery, clear of these guards."}};
            TrimUpperConnectorGuards(root,report);
            for(int f=0;f<2;f++)
            {
                float baseY=f*3.81f,top=f==0?3.64f:7.10f,nativeTop=baseY+3.35f;
                // Upper native walls already extend above the finished ceiling. Only fill a positive gap.
                if(top>nativeTop)
                {
                    foreach(var range in new[]{new[]{961.36f,Starts[f]},new[]{Ends[f],WallEnds[f]}})
                        Cube(parent,"Central — encontro da parede com o teto",SchoolMapBuilder.Point((range[0]+range[1])*.5f,BoundaryY,(nativeTop+top)*.5f),new Vector3((range[1]-range[0])*SchoolCirculation.PdfScale,top-nativeTop,.1502833f),white,true);
                    Cube(parent,"Central — fecho acima da porta da rampa",SchoolMapBuilder.Point((Starts[f]+Ends[f])*.5f,BoundaryY,(nativeTop+top)*.5f),new Vector3((Ends[f]-Starts[f])*SchoolCirculation.PdfScale,top-nativeTop,.1502833f),white,true);
                }
                var doors=block.GetComponentsInChildren<InteractiveDoor>().Where(d=>Mathf.Abs(d.transform.position.y-baseY)<.01f
                    &&Vector3.Distance(d.transform.position,SchoolMapBuilder.Point(Starts[f],BoundaryY,baseY))<.015f).ToArray();
                if(doors.Length!=1)throw new InvalidOperationException("Expected one new ramp door at floor "+f);
                foreach(var r in doors[0].GetComponentsInChildren<MeshRenderer>())if(!r.name.Contains("Handle"))r.sharedMaterial=cream;
                report.rampDoors++;
            }
            // Cover the additional length above the existing gallery; no new occupied floor or side walls.
            var roof=AssetDatabase.LoadAssetAtPath<Material>("Assets/SchoolCEEP/Materials/Cobertura.mat");
            if(!roof)throw new InvalidOperationException("School roof material must exist first.");
            float coveredEnd=BoundaryY+1.065f;
            var stripCenter=SchoolMapBuilder.Point((961.36f+WallEnds[1])*.5f,(PreviousBoundaryY+coveredEnd)*.5f,7.22f);
            var stripSize=new Vector3((WallEnds[1]-961.36f)*SchoolCirculation.PdfScale,.20f,(coveredEnd-PreviousBoundaryY)*SchoolCirculation.PdfScale);
            Cube(parent,"Central — cobertura até a parede da rampa",stripCenter,stripSize,roof,true);
            stripCenter.y=7.08f;stripSize.y=.06f;
            Cube(parent,"Central — forro superior até a parede da rampa",stripCenter,stripSize,white,false);
            stripCenter.y=3.51f;stripSize.y=.08f;stripSize.x=(1076.16f-961.36f)*SchoolCirculation.PdfScale;
            stripCenter.x=SchoolMapBuilder.Point((961.36f+1076.16f)*.5f,0).x;
            Cube(parent,"Central — forro térreo até a parede da rampa",stripCenter,stripSize,white,false);
            var floor=AssetDatabase.LoadAssetAtPath<Material>("Assets/SchoolCEEP/Materials/PhotoCentralFloorTiles.mat");
            foreach(var gallery in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Galeria superior — patamar norte da rampa"))
            {gallery.sharedMaterial=floor;SchoolCentralPhotoPass.WorldUV(gallery.GetComponent<MeshFilter>(),.60f,.60f);}
            Cube(parent,"Central — fecho sob a saída superior da escada",SchoolMapBuilder.Point((1022.75f+1049.92f)*.5f,809.24f,(3.35f+3.64f)*.5f),new Vector3((1049.92f-1022.75f)*SchoolCirculation.PdfScale,.29f,.1502833f),white,true);
            foreach(var renderer in block.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Parede — pintura inferior"&&r.transform.position.y<1.1f
                &&Mathf.Abs(r.bounds.center.z-SchoolMapBuilder.Point(0,809.24f).z)<.01f &&r.bounds.center.x>SchoolMapBuilder.Point(1022.9f,0).x))
            {renderer.sharedMaterial=tile;SchoolCentralPhotoPass.WorldUV(renderer.GetComponent<MeshFilter>(),.60f,.30f);}
            var stairDetails=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Escada central — acabamento das fotos");
            foreach(var t in stairDetails.GetComponentsInChildren<Transform>().Where(t=>t.name=="Escada — alvenaria lateral branca"||t.name=="Escada — divisória central sólida"||t.name=="Escada — parede norte branca"))
            {
                if(!t.GetComponent<BoxCollider>())t.gameObject.AddComponent<BoxCollider>();
                report.fullHeightStairWalls++;
            }
            var parapet=Cube(parent,"Central — mureta do vão superior da escada",SchoolMapBuilder.Point((990.55f+1014.73f)*.5f,809.24f,3.81f+.55f),new Vector3((1014.73f-990.55f)*SchoolCirculation.PdfScale,1.10f,.15f),tile,true);
            SchoolCentralPhotoPass.WorldUV(parapet.GetComponent<MeshFilter>(),.60f,.30f);
            Cube(parent,"Central — capeamento da mureta superior",SchoolMapBuilder.Point((990.55f+1014.73f)*.5f,809.24f,4.92f),new Vector3((1014.73f-990.55f)*SchoolCirculation.PdfScale,.02f,.17f),white,false);
            report.upperParapets=1;
            report.doorIds=block.GetComponentsInChildren<InteractiveDoor>().Where(d=>Mathf.Abs(d.transform.position.z-SchoolMapBuilder.Point(0,BoundaryY).z)<.01f).Select(d=>d.persistentId).ToArray();
            return report;
        }
        static void TrimUpperConnectorGuards(Transform root,Report report)
        {
            // North of the new wall the full gallery supports both sides; avoid rails crossing its doorway.
            float southZ=SchoolMapBuilder.Point(0,920.6f).z;
            foreach(string name in new[]{"Upper_Hall_Connector_Left","Upper_Hall_Connector_Right"})
            {
                float stopPdfY=BoundaryY+1.8f;
                float stopZ=SchoolMapBuilder.Point(0,stopPdfY).z;
                var guard=root.Find("Escadas e rampas/School_Circulation/Central_Four_Lane_Ramp/"+name);
                if(!guard)throw new InvalidOperationException("Missing upper ramp connector guard: "+name);
                foreach(string beamName in new[]{"Handrail","Midrail","Toe_Guard"})
                {
                    var beam=guard.Find(beamName);if(!beam)throw new InvalidOperationException("Missing ramp guard beam.");
                    var p=beam.position;p.z=(southZ+stopZ)*.5f;beam.position=p;
                    var s=beam.localScale;s.z=Mathf.Abs(stopZ-southZ);beam.localScale=s;
                }
                var posts=guard.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Post_",StringComparison.Ordinal)).ToArray();
                var reference=posts.First();var postPosition=reference.position;postPosition.z=stopZ;
                var postSize=reference.localScale;var postMaterial=reference.GetComponent<MeshRenderer>().sharedMaterial;
                foreach(var post in posts)if(post.position.z>stopZ)UnityEngine.Object.DestroyImmediate(post.gameObject);
                Cube(guard,"Post_Wall_End",postPosition,postSize,postMaterial,true);
                report.trimmedUpperRampGuardGroups++;
            }
        }
        static GameObject Cube(Transform parent,string name,Vector3 p,Vector3 size,Material mat,bool collision)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=p;go.transform.localScale=size;go.GetComponent<MeshRenderer>().sharedMaterial=mat;go.isStatic=true;
            if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
    }
}
