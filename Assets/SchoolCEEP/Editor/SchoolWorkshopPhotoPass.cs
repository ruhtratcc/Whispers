// Ferramenta da escola para construção ou acabamento.

using System;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>Photo-confirmed gate, continuous front low wall and open right side.</summary>
    public static class SchoolWorkshopPhotoPass
    {
        public const string Block="proposed_northeast_laboratories";
        public const string GateId="school.gate.workshops";
        public const float GateLeft=2548f,GateRight=2609f,Front=526.88f;
        public static void Prepare(SchoolMapBuilder.Layout layout)
        {
            var ground=layout.levels.Single(l=>l.index==0);
            // The front is a continuous low wall, open only above its cap.
            // The user locates the large walk-in opening on the RIGHT side
            // when looking at that front. Preserve the Depot walls to its north.
            ground.walls=ground.walls.Where(w=>!(w.block==Block&&!w.vertical
                &&w.p>=525.7f&&w.p<=528.1f&&w.a>=2624.5f&&w.b<=2709.7f)
                &&!(w.block==Block&&w.vertical&&Mathf.Abs(w.p-2709.64f)<.05f
                    &&w.a>=422.3f&&w.b<=528f)
                // The external 1500 dimension and its extension lines are
                // annotation in the native plan, not a second exterior wall.
                &&!(w.block==Block&&w.vertical&&Mathf.Abs(w.p-2739.76f)<.05f)
                &&!(w.block==Block&&!w.vertical&&w.a>=2709.6f&&w.b<=2742.7f
                    &&(Mathf.Abs(w.p-315.32f)<.05f||Mathf.Abs(w.p-527.96f)<.05f))).ToArray();
            // The corrugated gate replaces only the selected facade bay. Its
            // exact width/association remain estimated until a measured photo.
            ground.windows=ground.windows.Where(w=>!(w.block==Block&&!w.vertical
                &&Mathf.Abs(w.p-Front)<.05f&&w.a<GateRight&&w.b>GateLeft)).ToArray();
            var cut=new System.Collections.Generic.List<SchoolMapBuilder.Span>();
            foreach(var wall in ground.walls){
                if(wall.block!=Block||wall.vertical||Mathf.Abs(wall.p-Front)>.1f||wall.a>=GateRight||wall.b<=GateLeft){cut.Add(wall);continue;}
                if(wall.a<GateLeft)cut.Add(Copy(wall,wall.a,GateLeft));
                if(wall.b>GateRight)cut.Add(Copy(wall,GateRight,wall.b));
            }
            ground.walls=cut.ToArray();
        }
        static SchoolMapBuilder.Span Copy(SchoolMapBuilder.Span s,float a,float b)=>
            new SchoolMapBuilder.Span{block=s.block,source=s.source,vertical=s.vertical,p=s.p,a=a,b=b,thickness=s.thickness};

        public static void Build(Transform parent)
        {
            var plaster=AssetDatabase.LoadAssetAtPath<Material>(SchoolMapBuilder.Folder+"/Materials/PhotoWallTerracotta.mat");
            var steel=AssetDatabase.LoadAssetAtPath<Material>(SchoolMapBuilder.Folder+"/Materials/PhotoRoofSteel.mat");
            var roof=AssetDatabase.LoadAssetAtPath<Material>(SchoolMapBuilder.Folder+"/Materials/PhotoRoofGrey.mat");
            if(!plaster||!steel||!roof)throw new InvalidOperationException("Run photo materials before workshop frontage.");
            // Match the salmon facade in the supplied workshop photographs.
            foreach(var r in parent.GetComponentsInChildren<MeshRenderer>())
                if(r.name.StartsWith("Parede")||r.name.StartsWith("Peitoril")||r.name.StartsWith("Verga da janela"))r.sharedMaterial=plaster;
            Vector3 a=SchoolMapBuilder.Point(2624.56f,Front),b=SchoolMapBuilder.Point(2709.64f,Front);
            float lowWidth=b.x-a.x;
            Cube(parent,"Automotiva — mureta frontal",(a+b)*.5f+Vector3.up*.525f,new Vector3(lowWidth,1.05f,.18f),plaster,true,true);
            Cube(parent,"Automotiva — capeamento",(a+b)*.5f+Vector3.up*1.075f,new Vector3(lowWidth,.05f,.21f),roof,true,true);
            Cube(parent,"Automotiva — pilar da mureta",a+Vector3.up*1.7f,new Vector3(.14f,3.4f,.2f),steel,true,true);
            var end=SchoolMapBuilder.Point(2709.64f,Front);
            Cube(parent,"Automotiva — pilar do acesso lateral",end+Vector3.up*1.7f,new Vector3(.14f,3.4f,.2f),steel,true,true);
            var full=SchoolMapBuilder.Point(2709.64f,Front);
            Cube(parent,"Automotiva — viga superior",(a+full)*.5f+Vector3.up*3.22f,new Vector3(full.x-a.x,.22f,.23f),steel,true,true);
            // Frame the open side above head height; no wall or rail crosses
            // its walking area. The existing north partition remains intact.
            var sideBack=SchoolMapBuilder.Point(2709.64f,422.36f);
            Cube(parent,"Automotiva — pilar lateral do fundo",sideBack+Vector3.up*1.7f,new Vector3(.14f,3.4f,.2f),steel,true,true);
            Cube(parent,"Automotiva — viga lateral direita",(end+sideBack)*.5f+Vector3.up*3.22f,new Vector3(.23f,.22f,Mathf.Abs(sideBack.z-end.z)),steel,true,true);

            float width=(GateRight-GateLeft)*SchoolCirculation.PdfScale;
            var gateRoot=new GameObject("Portão metálico — oficinas");gateRoot.transform.SetParent(parent,false);
            // The sliding leaf runs outside the facade, with clearance from
            // the masonry and adjacent window grille throughout its travel.
            gateRoot.transform.position=SchoolMapBuilder.Point((GateLeft+GateRight)*.5f,Front+3f);
            var panel=Cube(gateRoot.transform,"Portão — folha de correr",gateRoot.transform.position+Vector3.up*1.55f,new Vector3(width,3.1f,.08f),steel,true,false);
            var slide=gateRoot.AddComponent<InteractiveDrawer>();slide.drawer=panel.transform;slide.persistentId=GateId;
            slide.displayName="portão";slide.localTravel=Vector3.right*(width+.12f);slide.travelSpeed=1.4f;slide.travelSmoothTime=.16f;
            // Ribs are children of the moving leaf and never enter static batching.
            int ribs=Mathf.CeilToInt(width/.075f);
            for(int i=0;i<=ribs;i++){
                var rib=Cube(panel.transform,"Chapa — nervura",panel.transform.position+new Vector3(Mathf.Lerp(-width*.5f,width*.5f,i/(float)ribs),0,-.052f),new Vector3(.022f,3.04f,.025f),roof,false,false);
                rib.transform.SetParent(panel.transform,true);
            }
            Cube(gateRoot.transform,"Portão — puxador",panel.transform.position+new Vector3(width*.35f,0,-.085f),new Vector3(.06f,.35f,.10f),steel,false,false).transform.SetParent(panel.transform,true);
            foreach(float x in new[]{GateLeft,GateRight})Cube(parent,"Portão — batente",SchoolMapBuilder.Point(x,Front,1.62f),new Vector3(.15f,3.24f,.20f),steel,true,true);
            Cube(parent,"Portão — trilho superior",SchoolMapBuilder.Point((GateLeft+GateRight)*.5f,Front,3.25f),new Vector3(width+.22f,.12f,.17f),steel,true,true);
            // Preserve the narrow facade portions beside the measured opening.
            foreach(var edge in new[]{new[]{2539.5f,GateLeft},new[]{GateRight,2621.1f}})
                Cube(parent,"Oficinas — ombreira do portão",SchoolMapBuilder.Point((edge[0]+edge[1])*.5f,Front,1.6f),new Vector3((edge[1]-edge[0])*SchoolCirculation.PdfScale,3.2f,.16f),plaster,true,true);
        }
        static GameObject Cube(Transform parent,string name,Vector3 position,Vector3 size,Material material,bool collision,bool isStatic)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,true);go.transform.position=position;
            // Assign world dimensions before parenting under a scaled moving leaf.
            go.transform.SetParent(null,true);go.transform.localScale=size;go.transform.SetParent(parent,true);
            go.isStatic=isStatic;go.GetComponent<MeshRenderer>().sharedMaterial=material;
            if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
    }
}
