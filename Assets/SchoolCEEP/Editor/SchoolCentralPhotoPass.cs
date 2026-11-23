// Ferramenta da escola para construção ou acabamento.

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>Interior finishes from the central corridor photographs. Registered masonry and doors remain intact.</summary>
    public static class SchoolCentralPhotoPass
    {
        const string Assets = "Assets/SchoolCEEP/Materials/";
        const float Scale = .07055555556f;
        static Transform parent;
        static Material wall, floor, white, metal, lamp, cork, paper, wood, windowBlue, doorCream;
        static Report report;
        [Serializable] public sealed class Report
        {
            public bool dimensionsEstimated = true, originalCollidersPreserved = true;
            public int floorSurfaces, wallFaces, fixtures, noticeboards, conduits, beams;
            public int upperRightWhiteFaces,upperRightDeepBeams,upperRightBlueWindowFrames,upperRightCreamDoors;
            public float upperRightBeamBottomMeters=2.45f,upperRightBeamTopGapMeters=.27f;
            public string[] notes;
        }
        public static Report Apply(Transform root, Transform details)
        {
            parent = details;
            report = new Report { notes = new[] {
                "The user identifies five photographs as the central corridors, stair and upper landing.",
                "Gray-brown tiled lower wall faces and square gray floor tiles replace the generic central interior finish. Exterior blue facades remain unchanged.",
                "The additional photos identify the upper right corridor: its interior faces are white and its deep transverse beams leave a reveal below the ceiling. User confirms classrooms on both sides, so registered room/door/window openings remain unchanged.",
                "Flat white ceiling finishes, exposed pipes and fluorescent fixtures preserve the existing corridor light sources and nighttime intensity.",
                "Decorative surfaces have no new colliders. Native door leaves, paired behavior, stair elevations and the approved four-lane ramp remain unchanged.",
                "No people or text from the photograph notices is reproduced." } };
            wall = Mat("PhotoCentralWallTiles", Color.white, .24f);
            floor = Mat("PhotoCentralFloorTiles", Color.white, .28f);
            white = Mat("PhotoCentralWhite", new Color(.77f,.77f,.73f));
            metal = Mat("PhotoCentralRailSteel", new Color(.075f,.082f,.082f), .34f, .55f);
            lamp = Mat("PhotoCentralFluorescent", new Color(.74f,.78f,.76f));
            lamp.EnableKeyword("_EMISSION"); lamp.SetColor("_EmissionColor", new Color(.76f,.86f,.83f)*1.1f);
            cork = Mat("PhotoCentralNoticeBacking", new Color(.30f,.24f,.17f));
            wood = Mat("PhotoCentralNoticeFrame", new Color(.32f,.22f,.12f));
            paper = Mat("PhotoCentralNoticePaper", new Color(.72f,.71f,.63f));
            windowBlue=Mat("PhotoSchoolWindowBlue",new Color(.035f,.12f,.22f),.32f,.25f);
            doorCream=Mat("PhotoDoorCream",new Color(.79f,.79f,.73f),.12f);
            TileTexture(wall, "PhotoCentralWallTileGrid", new Color(.30f,.285f,.265f), true);
            TileTexture(floor, "PhotoCentralFloorTileGrid", new Color(.40f,.395f,.37f), false);
            foreach (string name in new[] {"main_laboratories_north", "main_classrooms_north", "main_middle_circulation"})
            {
                var block = root.Find(name); if (!block) throw new InvalidOperationException("Missing central block "+name);
                foreach(var renderer in block.GetComponentsInChildren<MeshRenderer>().ToArray())
                {
                    if(renderer.name=="Piso térreo" || renderer.name=="Primeiro pavimento")
                    { renderer.sharedMaterial=floor; WorldUV(renderer.GetComponent<MeshFilter>(), .60f, .60f); report.floorSurfaces++; }
                    if(renderer.name=="Forro") renderer.sharedMaterial=white;
                    if(renderer.name=="Parede — pintura inferior" || renderer.name=="Peitoril") CorridorWall(renderer,name);
                    if(name=="main_classrooms_north"&&renderer.bounds.center.y>3.81f)
                    {
                        if(renderer.name=="Parede — reboco"||renderer.name=="Verga da janela"||renderer.name=="Verga da porta") UpperRightWhitePart(renderer);
                        if(renderer.name=="Caixilho"||renderer.name=="Divisão do caixilho")
                        {renderer.sharedMaterial=windowBlue;report.upperRightBlueWindowFrames++;}
                    }
                }
                if(name=="main_classrooms_north")foreach(var door in block.GetComponentsInChildren<HorrorGame.InteractiveDoor>().Where(d=>d.transform.position.y>3.80f))
                {
                    if(door.leafCollider){var renderer=door.leafCollider.GetComponent<MeshRenderer>();if(renderer)renderer.sharedMaterial=doorCream;}
                    report.upperRightCreamDoors++;
                }
                if(name!="main_middle_circulation")
                    for(int level=0;level<2;level++) CorridorServices(block,name,level);
            }
            // The upper storey has a roof, not an occupied third level. Finish its underside only.
            // Match the native roof notch above the open western approach.
            // A full decorative box here previously bridged the deleted slab.
            float westEdge=SchoolMapBuilder.CorridorX("main_laboratories_north",1,829.48f);
            foreach(var box in new[] {new[]{640.48f,303.32f,966.64f,727f},new[]{westEdge,727f,966.64f,809.24f},new[]{1065.64f,303.32f,1399.56f,809.24f},new[]{829.48f,809.24f,1210.12f,851.12f}})
                Cube("Central — forro branco superior", P((box[0]+box[2])*.5f,(box[1]+box[3])*.5f,7.08f), new Vector3((box[2]-box[0])*Scale,.06f,(box[3]-box[1])*Scale),white);
            return report;
        }
        static void CorridorWall(MeshRenderer renderer,string block)
        {
            var b=renderer.bounds; int level=b.center.y>3.81f?1:0;
            bool vertical=b.size.x<b.size.z;
            if(block=="main_middle_circulation")
            {
                if(vertical)return;
                float pdfY=1628f-b.center.z/Scale;
                if(pdfY<840f)return; // Only the interior face of the real south masonry.
                float h=renderer.name=="Peitoril"?b.size.y:1.42f;
                var face=Cube("Central — revestimento interno transversal", new Vector3(b.center.x,level*3.81f+h*.5f,b.max.z+.009f), new Vector3(b.size.x,h,.012f),wall);
                WorldUV(face.GetComponent<MeshFilter>(),.60f,.30f); report.wallFaces++; return;
            }
            if(!vertical)return;
            float left=block=="main_laboratories_north"? (level==0?831.4f:832.36f):(level==0?1177.72f:1179.34f);
            float right=block=="main_laboratories_north"?860.68f:(level==0?1207.24f:1206.64f);
            left=SchoolMapBuilder.CorridorX(block,level,left); right=SchoolMapBuilder.CorridorX(block,level,right);
            float x=1413f+b.center.x/Scale;
            bool west=Mathf.Abs(x-left)<2f,east=Mathf.Abs(x-right)<2f;
            if(!west&&!east)return;
            bool plainUpperRight=block=="main_classrooms_north"&&level==1;
            float height=renderer.name=="Peitoril"||plainUpperRight?b.size.y:1.42f;
            var finish=plainUpperRight?white:wall;
            var go=Cube("Central — revestimento interno lateral",new Vector3(west?b.max.x+.009f:b.min.x-.009f,level*3.81f+height*.5f,b.center.z),new Vector3(.012f,height,b.size.z),finish);
            if(!plainUpperRight)WorldUV(go.GetComponent<MeshFilter>(),.60f,.30f);
            report.wallFaces++;if(plainUpperRight)report.upperRightWhiteFaces++;
            // Wrap the exposed jamb end faces, keeping the exterior/room-facing broad side unchanged.
            foreach(float z in new[]{b.min.z-.009f,b.max.z+.009f})
            {
                var cap=Cube("Central — retorno do revestimento na ombreira",new Vector3(b.center.x,level*3.81f+height*.5f,z),new Vector3(b.size.x,height,.012f),finish);
                if(!plainUpperRight)WorldUV(cap.GetComponent<MeshFilter>(),.60f,.30f);
                report.wallFaces++;if(plainUpperRight)report.upperRightWhiteFaces++;
            }
        }
        static void UpperRightWhitePart(MeshRenderer renderer)
        {
            var b=renderer.bounds;if(b.size.x>=b.size.z)return;
            float westAxis=SchoolMapBuilder.CorridorX("main_classrooms_north",1,1179.34f),eastAxis=SchoolMapBuilder.CorridorX("main_classrooms_north",1,1206.64f);
            float pdfX=1413f+b.center.x/Scale;
            bool west=Mathf.Abs(pdfX-westAxis)<2f,east=Mathf.Abs(pdfX-eastAxis)<2f;if(!west&&!east)return;
            float bottom=b.min.y,top=Mathf.Min(b.max.y,7.05f);if(top<=bottom)return;
            Cube("Central — parede branca superior direita",new Vector3(west?b.max.x+.009f:b.min.x-.009f,(bottom+top)*.5f,b.center.z),new Vector3(.012f,top-bottom,b.size.z),white);
            foreach(float z in new[]{b.min.z-.009f,b.max.z+.009f})
                Cube("Central — ombreira branca superior direita",new Vector3(b.center.x,(bottom+top)*.5f,z),new Vector3(b.size.x,top-bottom,.012f),white);
            report.upperRightWhiteFaces+=3;
        }
        static void CorridorServices(Transform block,string name,int level)
        {
            float baseY=level*3.81f;
            bool plainUpperRight=name=="main_classrooms_north"&&level==1;
            float x=name=="main_laboratories_north"?846f:1193f;
            x=SchoolMapBuilder.CorridorX(name,level,x);
            // Replace each simple luminous box with a shallow white housing and two long tubes.
            foreach(var old in block.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Luminária fluorescente" && r.transform.position.y>=baseY && r.transform.position.y<baseY+3.81f).ToArray())
            {
                Vector3 c=old.transform.position;if(plainUpperRight)c.y=baseY+3.155f;UnityEngine.Object.DestroyImmediate(old.gameObject);
                Cube("Central — luminária retangular",c+Vector3.up*.025f,new Vector3(1.24f,.09f,.24f),white);
                for(int side=-1;side<=1;side+=2) Cube("Central — tubo fluorescente",c+new Vector3(0,-.025f,side*.065f),new Vector3(1.10f,.025f,.042f),lamp);
                report.fixtures++;
            }
            for(int side=-1;side<=1;side+=2)
            {
                float px=x+side*18.8f;
                for(int pipe=0;pipe<2;pipe++)
                {
                    Pipe("Central — conduíte aparente",P(px,345,baseY+3.02f+pipe*.13f),P(px,787,baseY+3.02f+pipe*.13f),pipe==0?.038f:.021f);
                    foreach(float mountY in new[]{365f,455f,545f,635f,725f,783f})
                        Cube("Central — suporte do conduíte",P(px+side*.7f,mountY,baseY+3.02f+pipe*.13f),new Vector3(.13f,.018f,.025f),metal);
                    report.conduits++;
                }
            }
            foreach(float y in plainUpperRight?new[]{350f,395f,440f,485f,530f,575f,620f,665f,710f,755f,800f}:new[]{425f,610f,792f})
            {
                Cube(plainUpperRight?"Central — viga profunda superior direita":"Central — viga transversal",P(x,y,baseY+(plainUpperRight?2.71f:level==0?3.39f:3.18f)),new Vector3(plainUpperRight?3.28f:2.88f,plainUpperRight?.52f:.16f,.24f),white);report.beams++;
                if(plainUpperRight)report.upperRightDeepBeams++;
            }
            // Shallow boards attach only to long, solid corridor wall faces; no window/door is covered.
            var candidates=parent.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Central — revestimento interno lateral" && r.bounds.size.y>1.2f && r.bounds.center.y>=baseY && r.bounds.center.y<baseY+3.81f && r.bounds.size.z>1.42f && Mathf.Abs(r.bounds.center.x-P(x,0).x)<1.8f).OrderBy(r=>r.bounds.center.z).ToArray();
            foreach(var face in candidates.Where((r,i)=>i%5==0).Take(4))
            {
                float sign=face.bounds.center.x<P(x,0).x?1f:-1f;
                Vector3 c=face.bounds.center;c.y=baseY+1.85f;c.x+=sign*.024f;
                Cube("Central — quadro de avisos",c,new Vector3(.035f,.86f,1.12f),wood);
                Cube("Central — fundo do quadro",c+Vector3.right*(sign*.025f),new Vector3(.016f,.78f,1.04f),cork);
                for(int row=0;row<2;row++)for(int col=0;col<3;col++)
                    Cube("Central — aviso sem texto",c+new Vector3(sign*.037f,(row==0?-.19f:.19f),-.33f+col*.33f),new Vector3(.005f,.30f,.25f),paper);
                report.noticeboards++;
            }
        }
        static void TileTexture(Material material,string name,Color baseColor,bool horizontal)
        {
            string path=Assets+name+".asset";var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(!tex)
            {
                const int size=128;tex=new Texture2D(size,size,TextureFormat.RGB24,true){name=name,wrapMode=TextureWrapMode.Repeat};
                var pixels=new Color[size*size];var random=new System.Random(144);
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    bool joint=x<1||y<1;float noise=(float)random.NextDouble()*.018f-.009f;
                    if(horizontal)noise+=Mathf.Sin(y*1.9f)*.012f;
                    pixels[y*size+x]=joint?new Color(.57f,.565f,.53f):baseColor+new Color(noise,noise,noise);
                }
                tex.SetPixels(pixels);tex.Apply();AssetDatabase.CreateAsset(tex,path);
            }
            material.SetTexture("_BaseMap",tex);material.SetTextureScale("_BaseMap",Vector2.one);EditorUtility.SetDirty(material);
        }
        internal static void WorldUV(MeshFilter filter,float across,float vertical)
        {
            var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name="Central photo world-unit tile UV";
            var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 p=filter.transform.TransformPoint(vertices[i]);Vector3 n=filter.transform.TransformDirection(normals[i]);
                uv[i]=Mathf.Abs(n.y)>.5f?new Vector2(p.x/across,p.z/vertical):Mathf.Abs(n.x)>.5f?new Vector2(p.z/across,p.y/vertical):new Vector2(p.x/across,p.y/vertical);
            }
            mesh.uv=uv;filter.sharedMesh=mesh;
        }
        static Material Mat(string name,Color color,float smooth=.08f,float metallic=0)
        {
            string path=Assets+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metallic);EditorUtility.SetDirty(m);return m;
        }
        static Vector3 P(float x,float y,float h=0)=>SchoolMapBuilder.Point(x,y,h);
        static GameObject Cube(string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;go.isStatic=true;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void Pipe(string name,Vector3 a,Vector3 b,float diameter)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);go.transform.position=(a+b)*.5f;go.transform.up=b-a;go.transform.localScale=new Vector3(diameter,Vector3.Distance(a,b)*.5f,diameter);go.GetComponent<Renderer>().sharedMaterial=metal;go.isStatic=true;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
