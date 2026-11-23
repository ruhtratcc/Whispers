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
    // Local user-confirmed corrections; original PDF registration remains immutable on disk.
    public static class SchoolCampusConnectionsPass
    {
        public const int SourceRevision = 5;
        public const float RearwardPdf = 32f;
        const float Scale=.07055555556f;
        const string MeshRoot="Assets/SchoolCEEP/Meshes/CampusConnections";
        static Transform parent;
        static Material concrete,steel,roof,blue,net,tactile;
        static string meshFolder;
        static readonly List<SchoolCirculation.Route> routes=new List<SchoolCirculation.Route>();
        [Serializable] public sealed class Report
        {
            public int sourceRevision=SourceRevision,canopyBranches,roofRibs,wallPanels;
            public bool dimensionsEstimated=true,openCourtGate=true;
            public float blockRecessionMeters=RearwardPdf*Scale,courtEave=5.6f,courtRidge=9.9f;
            public string[] movedBlocks=new[]{"proposed_library","proposed_east_teaching"};
            public SchoolCirculation.Route[] routes;
            public string[] notes;
        }
        static Report report;
        public static bool Receded(string block)=>block=="proposed_library"||block=="proposed_east_teaching";
        public static float Y(string block,float y)=>y+(Receded(block)?RearwardPdf:0f);
        public static float[] Box(string block,float[] b)=>new[]{b[0],Y(block,b[1]),b[2],Y(block,b[3])};
        public static void Prepare(SchoolMapBuilder.Layout layout)
        {
            foreach(var r in layout.regions)if(Receded(r.name))r.box=Box(r.name,r.box);
            foreach(var l in layout.levels)foreach(var spans in new[]{l.walls,l.doors,l.windows})foreach(var s in spans)
                if(Receded(s.block)){if(s.vertical){s.a+=RearwardPdf;s.b+=RearwardPdf;}else s.p+=RearwardPdf;}
        }
        public static void AdaptRooms(SchoolMapBuilder.Rooms data)
        {
            foreach(var r in data.rooms)
            {
                float x=(r.box[0]+r.box[2])*.5f,y=(r.box[1]+r.box[3])*.5f;
                if(y<774.5f||y>1062.2f)continue;
                if((x>=1892.9f&&x<=2121.5f)||(x>=2266.7f&&x<=2544.6f))r.box=Box("proposed_library",r.box);
            }
        }
        public static void ShiftEastStair(Transform circulation,SchoolCirculation.BuildReport data)
        {
            var stair=circulation.Find("School_Circulation/East_Teaching_U_Stair");
            if(!stair)throw new InvalidOperationException("Missing eastern stair before local block correction.");
            Vector3 offset=Vector3.back*(RearwardPdf*Scale);stair.position+=offset;
            foreach(var r in data.routes.Where(r=>r.name.StartsWith("EastU_")))for(int i=0;i<r.feet.Length;i++)r.feet[i]+=offset;
        }
        public static Report Build(Transform architecture,Transform group)
        {
            parent=group;routes.Clear();report=new Report();Setup();
            // Clear entrances follow the actual shifted door centres, not room-box guesses.
            foreach(string block in report.movedBlocks)
            {
                var doors=architecture.Find(block).GetComponentsInChildren<InteractiveDoor>().Where(d=>d.leafCollider&&d.leafCollider.bounds.min.y<.15f);
                var entrance=doors.OrderByDescending(d=>d.leafCollider.bounds.center.z).First();
                var c=entrance.pairedLeaf?(entrance.leafCollider.bounds.center+entrance.pairedLeaf.leafCollider.bounds.center)*.5f:entrance.leafCollider.bounds.center;
                float x=1413+c.x/Scale,y=1628-c.z/Scale;
                // The teaching block's northern mouth is an open passage, without a door leaf.
                if(block=="proposed_east_teaching"){x=2405.5f;y=Y(block,774.6f);}
                Path(block+" — acesso recuado",x,760,x,y,2.35f,false);
                Route(block+" — ligação com passarela",P(x,760,.094f),P(x,y-4,.094f),P(x,y+10,.05f));
            }
            // One straight exterior approach replaces the previous zigzag branches.
            // Its entire walking strip and roof remain west of the court perimeter.
            Path("Ligação direta do corredor à quadra",2570,760,2570,1250,3.0f,true);
            // The only lateral piece is the short uncovered threshold through the open gate.
            Cube("Quadra — soleira aberta",P(2587,1250,.03f),new Vector3(34*Scale,.15f,3f),Quaternion.identity,concrete,true);
            Route("Passarela principal até quadra",P(2570,760,.094f),P(2570,1250,.094f),P(2640,1250,.125f),P(2766.88f,1382.26f,.125f));
            // Both receded blocks retain their direct entrances from the main walkway.
            Route("Bloco recuado até ligação direta da quadra",P(2405.5f,816.6f,.05f),P(2405.5f,760,.094f),P(2570,760,.094f),P(2570,1250,.094f),P(2640,1250,.125f));
            // The user retained this gate approach in the subsequent correction.
            Path("Ramal coberto do portão das oficinas",2578.5f,760,2578.5f,535,3.0f,true);
            Path("Galeria coberta das salas técnicas",2111.68f,551f,2578.5f,551f,3.50f,true);
            Route("Passarela principal até portão grande das oficinas",P(2578.5f,760,.094f),P(2578.5f,540,.094f),P(2578.5f,535,.05f));
            ClearJunctionSupports(architecture);
            Court(architecture.Find("Quadra"));
            report.routes=routes.ToArray();report.notes=new[]{
                "Library and eastern teaching block recede 2.258m from the main walkway. Both storeys, room furniture, native doors/windows and the eastern stair use the same local coordinate correction.",
                "A single straight 3.0m-wide branch runs from the main walkway at PDF (2570,760) to (2570,1250), followed only by the short uncovered threshold into the existing court gate. The previous zigzag and workshop lateral extensions are removed. The covered route to the large workshop gate and the individual building entrances remain, as explicitly corrected by the user.",
                "The existing 24.1m by 30.4m court deck, markings and goals remain. A barrel metal roof, blue curved fascia, exposed arched steel ribs, gray low masonry and dark mesh follow the court photograph. The western gate opening is left physically open.",
                "This is scene geometry only. No gameplay, audio, menu, narrative or progression changes."};
            File.WriteAllText(SchoolMapBuilder.Output+"/School-campus-connections-report.json",JsonUtility.ToJson(report,true));return report;
        }
        static void Route(string name,params Vector3[] points)
        {
            var feet=new List<Vector3>();
            for(int j=0;j<points.Length-1;j++){int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(points[j],points[j+1])/.12f));for(int i=0;i<=n;i++)feet.Add(Vector3.Lerp(points[j],points[j+1],i/(float)n));}
            routes.Add(new SchoolCirculation.Route{name=name,feet=feet.ToArray(),requireAscending=false});
        }
        static void ClearJunctionSupports(Transform architecture)
        {
            Physics.SyncTransforms();
            var decks=parent.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.EndsWith(" — piso contínuo")).ToArray();
            var supports=architecture.GetComponentsInChildren<Collider>().Where(c=>c.name=="Passarela leste — poste"||c.name.EndsWith(" — pilar verde")||c.name.EndsWith(" — sapata")).ToArray();
            foreach(var support in supports)
            {
                var position=support.bounds.center;position.y=1.5f;
                bool junction=decks.Any(deck=>{
                    string prefix=deck.name.Replace(" — piso contínuo","");if(support.name.StartsWith(prefix+" —"))return false;
                    var b=deck.bounds;var reserved=new Bounds(new Vector3(b.center.x,1.5f,b.center.z),new Vector3(b.size.x+.9f,3f,b.size.z+.9f));
                    return reserved.Contains(position);
                });
                if(junction)UnityEngine.Object.DestroyImmediate(support.gameObject);
            }
        }
        static void Path(string name,float x0,float y0,float x1,float y1,float width,bool freeColumns,float roofInset=1.55f)
        {
            Vector3 a=P(x0,y0),b=P(x1,y1),forward=(b-a).normalized,side=Vector3.Cross(Vector3.up,forward);
            float length=Vector3.Distance(a,b);if(length<.2f)return;
            Quaternion rotation=Quaternion.LookRotation(forward);
            Cube(name+" — piso contínuo",(a+b)*.5f+Vector3.up*(-.02f),new Vector3(width,.128f,length),rotation,concrete,true);
            Cube(name+" — faixa tátil",(a+b)*.5f+Vector3.up*.048f,new Vector3(.26f,.008f,length),rotation,tactile,false);
            // Mitre the beginning at the existing roof eave; no transverse rails or columns across a junction.
            float inset=Mathf.Min(roofInset,length*.45f);Vector3 roofA=a+forward*inset;
            float roofLength=length-inset,half=width*.5f+.13f,eave=2.86f,ridge=3.38f;
            var vertices=new[]{roofA-side*half+Vector3.up*eave,b-side*half+Vector3.up*eave,roofA+Vector3.up*ridge,b+Vector3.up*ridge,
                roofA+side*half+Vector3.up*eave,b+side*half+Vector3.up*eave};
            MeshObject(name+" — cobertura em duas águas",vertices,new[]{0,2,1,1,2,3,2,4,3,3,4,5},roof,false);
            int count=Mathf.Max(1,Mathf.CeilToInt(roofLength/3.6f));
            for(int i=0;i<=count;i++)
            {
                Vector3 c=Vector3.Lerp(roofA,b,i/(float)count);
                Beam(name+" — tesoura",c-side*half+Vector3.up*(eave-.10f),c+Vector3.up*(ridge-.10f),.06f,steel);
                Beam(name+" — tesoura",c+Vector3.up*(ridge-.10f),c+side*half+Vector3.up*(eave-.10f),.06f,steel);
                Beam(name+" — travessa",c-side*half+Vector3.up*(eave-.14f),c+side*half+Vector3.up*(eave-.14f),.06f,steel);
                if(!freeColumns)continue;
                foreach(int s in new[]{-1,1})
                {
                    Vector3 p=c+side*(s*(width*.5f+.06f));
                    // A post must not appear inside the adjoining branch or a doorway.
                    float px=1413+p.x/Scale,py=1628-p.z/Scale;
                    if(name=="Galeria coberta das salas técnicas"&&px>2554)continue;
                    if((Mathf.Abs(py-1250)<23&&px>2535&&px<2645)||Mathf.Abs(py-760)<23)continue;
                    Cube(name+" — pilar verde",p+Vector3.up*1.42f,new Vector3(.09f,2.84f,.09f),Quaternion.identity,steel,true);
                    Cube(name+" — sapata",p+Vector3.up*.02f,new Vector3(.28f,.10f,.28f),Quaternion.identity,concrete,true);
                }
            }
            report.canopyBranches++;
        }
        static void Court(Transform court)
        {
            if(!court)throw new InvalidOperationException("Missing native court.");
            foreach(var t in court.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Poste da grade"||t.name=="Grade horizontal").ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
            var previous=parent;parent=new GameObject("Quadra — cobertura e perímetro das fotos").transform;parent.SetParent(court,false);
            var center=P(2766.88f,1382.26f);const float half=12.05f,depth=30.4f,eave=5.6f,rise=4.3f;
            const int sections=64;var v=new List<Vector3>();var tri=new List<int>();
            for(int i=0;i<=sections;i++)
            {
                float angle=-Mathf.PI*.5f+Mathf.PI*i/sections;
                float x=half*Mathf.Sin(angle),height=eave+rise*Mathf.Cos(angle);
                v.Add(center+new Vector3(x,height,-depth*.5f-.35f));v.Add(center+new Vector3(x,height,depth*.5f+.35f));
                if(i<sections){int n=i*2;tri.AddRange(new[]{n,n+1,n+2,n+2,n+1,n+3});}
            }
            MeshObject("Quadra — telhado metálico arqueado",v.ToArray(),tri.ToArray(),roof,false);
            for(int k=0;k<7;k++)
            {
                float z=-depth*.5f+depth*k/6f;
                for(int i=0;i<sections;i++)Beam("Quadra — arco de aço aparente",v[i*2]+Vector3.forward*(z+depth*.5f+.35f)-Vector3.up*.12f,
                    v[(i+1)*2]+Vector3.forward*(z+depth*.5f+.35f)-Vector3.up*.12f,.085f,steel);
                foreach(int side in new[]{-1,1})Cube("Quadra — suporte lateral",center+new Vector3(side*half,eave*.5f,z),new Vector3(.16f,eave,.16f),Quaternion.identity,steel,true);
                report.roofRibs++;
            }
            foreach(int side in new[]{-1,1})
            {
                float z=side*(depth*.5f+.32f);var face=new List<Vector3>();var inds=new List<int>();
                for(int i=0;i<=sections;i++)
                {
                    var p=v[i*2];face.Add(new Vector3(p.x,4.65f,center.z+z));face.Add(new Vector3(p.x,p.y-.05f,center.z+z));
                    if(i<sections){int n=i*2;inds.AddRange(new[]{n,n+2,n+1,n+1,n+2,n+3});}
                }
                MeshObject("Quadra — testeira azul curva",face.ToArray(),inds.ToArray(),blue,false);
                Wall(center+new Vector3(0,0,side*depth*.5f),Vector3.right,half*2);
            }
            Wall(center+new Vector3(half,0,0),Vector3.forward,depth);
            // West opening centred at PDF y1250; leave six metres clear through both masonry and mesh.
            float gateZ=P(2596.1f,1250).z,west=center.x-half,start=center.z-depth*.5f,end=center.z+depth*.5f;
            Wall(new Vector3(west,0,(start+gateZ-3f)*.5f),Vector3.forward,gateZ-3f-start);
            Wall(new Vector3(west,0,(gateZ+3f+end)*.5f),Vector3.forward,end-gateZ-3f);
            parent=previous;
        }
        static void Wall(Vector3 center,Vector3 axis,float length)
        {
            if(length<.1f)return;Quaternion rot=Quaternion.LookRotation(Vector3.Cross(axis,Vector3.up));
            Cube("Quadra — muro baixo cinza",center+Vector3.up*.835f,new Vector3(length,1.52f,.18f),rot,concrete,true);report.wallPanels++;
            Vector3 a=center-axis*length*.5f,b=center+axis*length*.5f;
            MeshObject("Quadra — tela escura sobre muro",new[]{a+Vector3.up*1.60f,b+Vector3.up*1.60f,a+Vector3.up*5.6f,b+Vector3.up*5.6f},new[]{0,2,1,1,2,3},net,false);
            Beam("Quadra — borda superior da tela",a+Vector3.up*5.6f,b+Vector3.up*5.6f,.055f,steel);
        }
        static void Setup()
        {
            if(!AssetDatabase.IsValidFolder(MeshRoot))AssetDatabase.CreateFolder("Assets/SchoolCEEP/Meshes","CampusConnections");
            string id=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6);AssetDatabase.CreateFolder(MeshRoot,id);meshFolder=MeshRoot+"/"+id;
            concrete=AssetDatabase.LoadAssetAtPath<Material>("Assets/SchoolCEEP/Materials/PhotoConcreteFloor.mat");
            tactile=AssetDatabase.LoadAssetAtPath<Material>("Assets/SchoolCEEP/Materials/PhotoTactilePink.mat");
            if(!tactile)tactile=Mat("CampusTactilePink",new Color(.48f,.27f,.25f));
            steel=Mat("CampusCanopyGreenSteel",new Color(.13f,.20f,.16f),.45f);blue=Mat("CampusCourtBlueFascia",new Color(.065f,.29f,.64f));
            roof=Mat("CampusCourtCorrugatedGrey",new Color(.46f,.47f,.43f),.4f);roof.SetFloat("_Cull",0);
            var roofingTexture=new Texture2D(128,128,TextureFormat.RGB24,true){name="Corrugated metal",wrapMode=TextureWrapMode.Repeat,anisoLevel=8};
            var roofingNormal=new Texture2D(128,128,TextureFormat.RGBA32,true,true){name="Corrugated metal normals",wrapMode=TextureWrapMode.Repeat,anisoLevel=8};
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float phase=x*Mathf.PI/8f,value=.91f+.055f*Mathf.Sin(phase)+.012f*Mathf.PerlinNoise(x*.2f,y*.17f);
                roofingTexture.SetPixel(x,y,new Color(value,value,value));var n=new Vector3(.20f*Mathf.Cos(phase),0,1).normalized;
                roofingNormal.SetPixel(x,y,new Color(n.x*.5f+.5f,.5f,n.z*.5f+.5f,1));
            }
            roofingTexture.Apply();roofingNormal.Apply();AssetDatabase.CreateAsset(roofingTexture,meshFolder+"/RoofDiffuse.asset");AssetDatabase.CreateAsset(roofingNormal,meshFolder+"/RoofNormal.asset");
            roof.SetTexture("_BaseMap",roofingTexture);roof.SetTexture("_BumpMap",roofingNormal);roof.SetFloat("_BumpScale",1);roof.EnableKeyword("_NORMALMAP");EditorUtility.SetDirty(roof);
            net=Mat("CampusCourtDarkMesh",new Color(.08f,.10f,.095f));net.SetFloat("_Cull",0);net.SetFloat("_AlphaClip",1);net.EnableKeyword("_ALPHATEST_ON");net.SetFloat("_Cutoff",.5f);
            var tex=new Texture2D(64,64,TextureFormat.RGBA32,true){name="Court diamond mesh",wrapMode=TextureWrapMode.Repeat};
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)tex.SetPixel(x,y,new Color(1,1,1,((x+y)%32<2||(x-y+128)%32<2)?1:0));tex.Apply();
            AssetDatabase.CreateAsset(tex,meshFolder+"/CourtMeshTexture.asset");net.SetTexture("_BaseMap",tex);EditorUtility.SetDirty(net);
        }
        static Material Mat(string name,Color c,float metallic=0)
        {
            string path="Assets/SchoolCEEP/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",c);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.18f);EditorUtility.SetDirty(m);return m;
        }
        static GameObject MeshObject(string name,Vector3[] v,int[] t,Material material,bool collision)
        {
            var mesh=new Mesh{name=name,vertices=v,triangles=t};
            // Net UVs use planar face distance, so holes stay consistent on every side.
            bool vertical=v.Length==4&&Mathf.Abs(v[2].y-v[0].y)>1f;Vector3 axis=vertical?(v[1]-v[0]).normalized:Vector3.right;
            mesh.uv=v.Select(p=>vertical?new Vector2(Vector3.Dot(p,axis)*2.0f,p.y*2.0f):new Vector2(p.x*.6f,p.z*.6f)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=meshFolder+"/Mesh-"+Guid.NewGuid().ToString("N")+".asset";AssetDatabase.CreateAsset(mesh,path);
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.isStatic=true;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
            if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;
        }
        static GameObject Cube(string name,Vector3 p,Vector3 size,Quaternion r,Material m,bool physical)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=p;go.transform.rotation=r;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=m;go.isStatic=true;
            if(!physical)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void Beam(string n,Vector3 a,Vector3 b,float w,Material m)=>Cube(n,(a+b)*.5f,new Vector3(w,w,Vector3.Distance(a,b)),Quaternion.LookRotation(b-a),m,false);
        static Vector3 P(float x,float y,float h=0)=>SchoolMapBuilder.Point(x,y,h);
    }
}
