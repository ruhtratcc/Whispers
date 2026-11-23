// Ferramenta da escola para construção ou acabamento.

using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HorrorGame;

namespace SchoolCEEP.Editor
{
    /// <summary>Photographic architecture layered onto the measured plan; no story events.</summary>
    public static class SchoolPhotoPass
    {
        const float Scale = .07055555556f;
        const string Folder = "Assets/SchoolCEEP/Materials";
        static Material white, grey, navy, cream, paleBlue, terracotta, ochre, steel, roofing, floor, paving, grass, tactile;
        static Transform parent;

        public static void Apply(Transform architecture, Transform photoGroup)
        {
            parent = photoGroup;
            white = Mat("PhotoPlasterWhite", new Color(.76f,.76f,.72f));
            grey = Mat("PhotoPlasterGrey", new Color(.42f,.45f,.46f));
            navy = Mat("PhotoFrameBlue", new Color(.025f,.095f,.27f), .22f, .25f);
            cream = Mat("PhotoDoorCream", new Color(.73f,.73f,.63f));
            paleBlue = Mat("PhotoFacadeBlue", new Color(.22f,.50f,.64f));
            terracotta = Mat("PhotoWallTerracotta", new Color(.60f,.29f,.20f));
            ochre = Mat("PhotoWallOchre", new Color(.66f,.48f,.20f));
            steel = Mat("PhotoRoofSteel", new Color(.15f,.20f,.20f), .18f, .5f);
            roofing = Mat("PhotoRoofGrey", new Color(.45f,.46f,.42f));
            floor = Mat("PhotoConcreteFloor", new Color(.49f,.49f,.46f), .14f);
            paving = Mat("PhotoPaving", new Color(.43f,.42f,.38f), .12f);
            grass = Mat("PhotoGrassGround", new Color(.16f,.25f,.115f));
            tactile = Mat("PhotoTactileBrown", new Color(.28f,.16f,.105f));
            RoughTexture(floor, "PhotoConcreteNoise", 6f);
            RoughTexture(white, "PhotoPlasterNoise", 2f);
            Repaint(architecture);
            ReplaceCanopies(architecture);
            Facade();
            FloorDetails();
            Gardens();
            MainHallTables();
            Text("DIREÇÃO / SECRETARIA",P(1349.4f,1290,2.65f),.15f,white,2.5f,Quaternion.Euler(0,-90,0));
            Text("PEDAGOGIA",P(1490.8f,1283,2.65f),.15f,white,1.7f,Quaternion.Euler(0,90,0));
            ConfigurePhotoLights(architecture);
        }

        static Material Mat(string name, Color color, float smooth=.08f, float metallic=0)
        {
            string path=Folder+"/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metallic);
            EditorUtility.SetDirty(m);return m;
        }

        static void RoughTexture(Material material,string name,float tiling)
        {
            string path=Folder+"/"+name+".asset";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(!texture)
            {
                texture=new Texture2D(128,128,TextureFormat.RGB24,true){name=name,wrapMode=TextureWrapMode.Repeat};
                var random=new System.Random(37);var colors=new Color[128*128];
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                {
                    float value=.88f+(float)random.NextDouble()*.11f+Mathf.PerlinNoise(x*.055f,y*.055f)*.015f;
                    colors[y*128+x]=new Color(value,value,value);
                }
                texture.SetPixels(colors);texture.Apply(true,false);AssetDatabase.CreateAsset(texture,path);
            }
            material.SetTexture("_BaseMap",texture);material.SetTextureScale("_BaseMap",Vector2.one*tiling);EditorUtility.SetDirty(material);
        }

        static Vector3 P(float x,float y,float h=0)=>SchoolMapBuilder.Point(x,y,h);
        static GameObject Cube(string name,Vector3 position,Vector3 size,Material material,bool collision=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;go.isStatic=true;
            if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static GameObject Slab(string name,float x0,float y0,float x1,float y1,float top,Material material,bool collision=true)
        {
            var go=Cube(name,P((x0+x1)/2,(y0+y1)/2,top-.055f),new Vector3((x1-x0)*Scale,.11f,(y1-y0)*Scale),material,collision);
            if(collision)go.AddComponent<SurfaceType>().surface=GroundSurface.Concrete;return go;
        }
        static GameObject Beam(string name,Vector3 a,Vector3 b,float thickness,Material material)
        {
            var go=Cube(name,(a+b)*.5f,new Vector3(thickness,thickness,Vector3.Distance(a,b)),material);
            go.transform.rotation=Quaternion.LookRotation(b-a);return go;
        }

        static void Repaint(Transform architecture)
        {
            foreach(Transform block in architecture)
            {
                bool hall=block.name=="administration_south"||block.name=="pedagogy_south";
                bool orange=block.name=="proposed_east_teaching"||block.name=="east_north_classrooms";
                foreach(var renderer in block.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string n=renderer.name;
                    if(n.StartsWith("Parede — reboco")||n=="Verga da janela"||n=="Verga da porta")renderer.sharedMaterial=orange?terracotta:white;
                    else if(n=="Parede — pintura inferior"||n=="Peitoril")
                        renderer.sharedMaterial=block.name=="main_auditorium_south"&&renderer.transform.position.y>3.81f
                            ?white:hall?grey:orange?ochre:navy;
                    else if(n=="Caixilho"||n=="Divisão do caixilho")renderer.sharedMaterial=navy;
                    else if(n=="Piso térreo"||n=="Primeiro pavimento")renderer.sharedMaterial=floor;
                }
                foreach(var door in block.GetComponentsInChildren<InteractiveDoor>(true))
                {
                    if(!hall)continue;
                    foreach(var renderer in door.GetComponentsInChildren<MeshRenderer>())
                        renderer.sharedMaterial=renderer.name.Contains("Handle")?navy:cream;
                    DoorBrace(door);
                }
                // Horizontal louvres and steel grilles retain the original opening/collider.
                foreach(var window in block.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Janela —")).ToArray())
                {
                    Vector3 center=window.transform.position,size=window.transform.localScale;
                    bool vertical=size.x<size.z;float span=vertical?size.z:size.x;
                    for(int i=0;i<5;i++)
                    {
                        float spacing=Mathf.Min(.29f,window.bounds.size.y*.20f);
                        Vector3 offset=Vector3.up*((i-2)*spacing);
                        Cube("Grade azul — barra horizontal",center+offset,vertical?new Vector3(.045f,.025f,span):new Vector3(span,.025f,.045f),navy);
                    }
                }
            }
        }

        static void DoorBrace(InteractiveDoor door)
        {
            var leaf=door.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r=>!r.name.Contains("Handle"));
            if(!leaf)return;
            var leafCollider=leaf.GetComponent<BoxCollider>();if(!leafCollider)return;
            // Bracing follows the existing moving leaf, and never adds another door collider.
            // Newly moved prefab colliders may still expose their old physics
            // bounds until SyncTransforms. Derive the leaf geometry directly
            // from its local box and current transform instead.
            Vector3 c=leafCollider.transform.TransformPoint(leafCollider.center);
            Vector3 size=Vector3.Scale(leafCollider.size,leafCollider.transform.lossyScale);
            Vector3 x=door.transform.right*Mathf.Abs(size.x)*.40f;
            Vector3 up=Vector3.up*Mathf.Abs(size.y)*.42f;
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 depth=door.transform.forward*(side*(Mathf.Abs(size.z)*.5f+.012f));
                foreach(var pair in new[]{new[]{c-x-up+depth,c+x+up+depth},new[]{c-x+up+depth,c+x-up+depth}})
                {
                    var beam=Beam("Armação azul em X",pair[0],pair[1],.038f,navy);
                    beam.transform.SetParent(leaf.transform,true);beam.isStatic=false;
                }
            }
        }

        static void ReplaceCanopies(Transform architecture)
        {
            var old=architecture.Find("Passarelas cobertas");
            if(old)
            {
                foreach(var renderer in old.GetComponentsInChildren<MeshRenderer>().ToArray())
                    if(renderer.name=="Telhado da passarela"||renderer.name=="Pilar da passarela"||renderer.name=="Luminária fluorescente")UnityEngine.Object.DestroyImmediate(renderer.gameObject);
                foreach(var floorRenderer in old.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Passagem coberta"))floorRenderer.sharedMaterial=paving;
            }
            var courtyard=architecture.Find("covered_courtyard");
            if(courtyard)
                foreach(var renderer in courtyard.GetComponentsInChildren<MeshRenderer>().ToArray())
                    if(renderer.name.StartsWith("Cobertura")||renderer.name=="Pilar")UnityEngine.Object.DestroyImmediate(renderer.gameObject);
            // The main hall alone is covered. Both adjacent low patios are
            // open to the sky in the supplied side photographs.
            Slab("Hall principal — piso amplo",1348,963.08f,1493.8f,1343,.044f,floor);
            Canopy("Hall principal",1345,960,1497,1347,4.5f,5.8f,5.0f,false);
            // Roof supports stay at the hall boundary, never in the exterior
            // ramp, stair edge or pedestrian passage. The grade pass raises
            // these hall-owned posts with the occupied platform.
            foreach(float x in new[]{1345f,1478f})foreach(float y in new[]{970f,1065f,1167f})
                Cube("Hall principal — pilar branco",P(x,y,2.25f),new Vector3(.24f,4.5f,.24f),white,true);
            // The supplied front photograph shows an open forecourt before the portal.
            Canopy("Passarela leste",1455,739.24f,2645,781.76f,2.85f,3.35f,3.6f,true);
            Canopy("Ligação central",961,809.24f,1076,851.12f,3.48f,3.49f,5,true);
            Canopy("Ligação leste",1399.56f,774,1455,809,2.85f,3.3f,3.6f,true);
            SchoolMapBuilder.GroundSlab(parent,"Passarela leste — piso ampliado",new[]{1455f,739.24f,2645f,781.76f},.044f,.11f,paving);
        }

        static void Canopy(string name,float x0,float y0,float x1,float y1,float eave,float ridge,float module,bool columns)
        {
            var center=P((x0+x1)/2,(y0+y1)/2);float width=(x1-x0)*Scale,length=(y1-y0)*Scale;
            bool alongX=width>length;float cross=alongX?length:width,run=alongX?width:length;
            float half=cross*.5f,rise=ridge-eave,slope=Mathf.Atan2(rise,half)*Mathf.Rad2Deg;
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 pos=center+Vector3.up*((eave+ridge)*.5f)+(alongX?Vector3.forward:Vector3.right)*(side*cross*.25f);
                var panel=Cube(name+" — telha ondulada",pos,alongX?new Vector3(run,.065f,Mathf.Sqrt(half*half+rise*rise)):new Vector3(Mathf.Sqrt(half*half+rise*rise),.065f,run),roofing);
                panel.transform.rotation=alongX?Quaternion.Euler(side*slope,0,0):Quaternion.Euler(0,0,-side*slope);
            }
            int frames=Mathf.Max(1,Mathf.CeilToInt(run/module));
            for(int i=0;i<=frames;i++)
            {
                float t=Mathf.Lerp(-run*.5f,run*.5f,(float)i/frames);
                Vector3 c=center+(alongX?Vector3.right:Vector3.forward)*t;
                Vector3 a=c+(alongX?Vector3.forward:Vector3.right)*(-half)+Vector3.up*eave;
                Vector3 b=c+(alongX?Vector3.forward:Vector3.right)*half+Vector3.up*eave;
                Vector3 r=c+Vector3.up*ridge;
                Beam(name+" — tesoura",a,r,.065f,steel);Beam(name+" — tesoura",r,b,.065f,steel);Beam(name+" — travessa",a,b,.065f,steel);
                Beam(name+" — pendural",c+Vector3.up*eave,r,.04f,steel);
                if(columns&&!(name=="Ligação leste"&&i>0&&i<frames))
                    foreach(var p in new[]{a,b})
                    {
                        // Steel posts sit just outside the walking floor edge.
                        Vector3 outward=(p-c-Vector3.up*eave).normalized*.11f;
                        UnityEngine.Physics.SyncTransforms();
                        Vector3 foot=p+outward-Vector3.up*eave;
                        // The new low ramp occupies the former north-edge column line.
                        // Support its roof from the neighbouring high walkway with a steel corbel.
                        if(name=="Passarela leste"&&foot.x>P(1638,0).x&&foot.x<P(1707,0).x
                            &&foot.z>P(0,739.24f).z&&foot.z<P(0,725f).z)
                        {
                            Vector3 originalTop=foot+Vector3.up*eave;
                            foot.z=P(0,746).z;
                            Beam("Passarela leste — consolo junto à rampa baixa",originalTop,foot+Vector3.up*eave,.08f,steel);
                        }
                        // Attach canopy to adjacent masonry instead of planting posts in door/stair approaches.
                        if(UnityEngine.Physics.OverlapCapsule(foot+Vector3.up*.68f,foot+Vector3.up*(eave-.15f),.62f,~0,QueryTriggerInteraction.Ignore)
                            .Any(col=>col.bounds.max.y>.20f))continue;
                        Cube(name+" — poste",foot+Vector3.up*(eave*.5f),new Vector3(.095f,eave,.095f),steel,true);
                        Cube(name+" — sapata",foot+Vector3.up*.08f,new Vector3(.25f,.16f,.25f),grey,true);
                    }
            }
            // Fine raised corrugations: no colliders, visible silhouette without heavy geometry.
            int ribs=Mathf.CeilToInt(run/.32f);
            for(int i=0;i<=ribs;i++)
            {
                float t=Mathf.Lerp(-run*.5f,run*.5f,(float)i/ribs);
                Vector3 c=center+(alongX?Vector3.right:Vector3.forward)*t;
                Vector3 r=c+Vector3.up*(ridge+.043f);
                Beam(name+" — ondulação",c+(alongX?Vector3.forward:Vector3.right)*(-half)+Vector3.up*(eave+.043f),r,.014f,roofing);
                Beam(name+" — ondulação",r,c+(alongX?Vector3.forward:Vector3.right)*half+Vector3.up*(eave+.043f),.014f,roofing);
            }
        }

        static void Facade()
        {
            var c=P(1420.9f,1343);const float width=10.29f,opening=6.45f;
            for(int side=-1;side<=1;side+=2)
            {
                float pier=(width-opening)*.5f;
                Vector3 p=c+Vector3.right*(side*(opening*.5f+pier*.5f));
                Cube("Fachada — ombreira branca",p+Vector3.up*2.8f,new Vector3(pier,5.6f,.22f),white,true);
                Cube("Fachada — base azul",p+Vector3.up*.72f-Vector3.forward*.13f,new Vector3(pier,1.44f,.035f),navy);
            }
            // Pitched opening: empty below the two sloping lintels, allowing real traversal.
            Vector3 apex=c+Vector3.up*4.35f;
            Beam("Pórtico branco inclinado",c+Vector3.left*(opening*.5f)+Vector3.up*3.45f,apex,.19f,white);
            Beam("Pórtico branco inclinado",apex,c+Vector3.right*(opening*.5f)+Vector3.up*3.45f,.19f,white);
            Cube("Fachada — painel azul alto",c+Vector3.up*5.03f,new Vector3(opening,1.18f,.20f),paleBlue);
            // Fill the triangular portions above the opening using a procedural panel, not a solid wall.
            TrianglePanel(c,opening);
            Text("CEEP",c+new Vector3(-3.84f,3.95f,-.147f),.68f,white,3,Quaternion.identity);
            Text("CENTRO ESTADUAL DE\nEDUCAÇÃO PROFISSIONAL",c+new Vector3(.05f,5.06f,-.127f),.225f,white,6,Quaternion.identity);
            // The entrance is open for exploration: blue gate leaves park against the piers.
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 a=c+Vector3.right*(side*(opening*.5f-.10f)),b=a+Vector3.forward*1.55f;
                Beam("Portão aberto — travessa",a+Vector3.up*.20f,b+Vector3.up*.20f,.035f,navy);
                Beam("Portão aberto — travessa",a+Vector3.up*1.9f,b+Vector3.up*1.9f,.035f,navy);
                for(int i=0;i<=12;i++)Beam("Portão aberto — grade",Vector3.Lerp(a,b,i/12f)+Vector3.up*.20f,Vector3.Lerp(a,b,i/12f)+Vector3.up*1.9f,.024f,navy);
            }
        }
        static void TrianglePanel(Vector3 c,float opening)
        {
            var mesh=new Mesh{name="Photographic facade upper portal"};
            float w=opening*.5f;
            var front=new[]{new Vector3(-w,3.45f,-.115f),new Vector3(0,4.35f,-.115f),new Vector3(-w,4.44f,-.115f),new Vector3(0,4.44f,-.115f),new Vector3(w,3.45f,-.115f),new Vector3(w,4.44f,-.115f)};
            mesh.vertices=front.Concat(front).ToArray();
            mesh.triangles=new[]{0,1,2,2,1,3,1,4,3,3,4,5,8,7,6,9,7,8,9,10,7,11,10,9};mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("Fachada — painel sobre arco");go.transform.SetParent(parent,false);go.transform.position=c;go.isStatic=true;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=paleBlue;
        }
        static void Text(string text,Vector3 position,float size,Material unused,float width,Quaternion rotation)
        {
            var go=new GameObject("Placa — "+text);go.transform.SetParent(parent,false);go.transform.position=position;go.transform.rotation=rotation;
            var tmp=go.AddComponent<TextMeshPro>();tmp.text=text;tmp.font=TMP_Settings.defaultFontAsset;
            tmp.fontSize=size*10;tmp.color=new Color(.80f,.81f,.76f);tmp.alignment=TextAlignmentOptions.Center;tmp.rectTransform.sizeDelta=new Vector2(width,1.1f);
            tmp.textWrappingMode=TextWrappingModes.NoWrap;
        }

        static void FloorDetails()
        {
            Vector3 a=P(1421,1343,.049f),b=P(1421,970,.049f);
            Beam("Hall — faixa tátil azul",a,b,.26f,navy);
            Beam("Hall — faixa tátil até administração",P(1421,1297,.049f),P(1345,1250,.049f),.26f,navy);
            // Flat strips prevent decorative thresholds from snagging CharacterController.
            foreach(var renderer in parent.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.Contains("faixa tátil")))
            { var s=renderer.transform.localScale;s.x=.26f;s.y=.008f;renderer.transform.localScale=s; }
            // A mipmapped surface pattern replaces the thin detached joint cubes,
            // whose long edges alias into dotted lines in the corridor view.
            foreach(var walkway in parent.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Passarela leste — piso ampliado"))
                ApplyWalkwayPavingGrid(walkway);
            Beam("Passarela — faixa tátil marrom",P(1455,758.5f,.049f),P(2645,758.5f,.049f),.28f,tactile);
            var strip=parent.GetComponentsInChildren<MeshRenderer>().Last(r=>r.name=="Passarela — faixa tátil marrom").transform;
            var size=strip.localScale;size.x=.28f;size.y=.008f;strip.localScale=size;
        }
        static void ApplyWalkwayPavingGrid(MeshFilter walkway)
        {
            const int resolution=512;
            const float tileMeters=.8f,groutMeters=.014f;
            string texturePath=Folder+"/PhotoWalkwayPavingGridTexture.asset";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(!texture)
            {
                texture=new Texture2D(resolution,resolution,TextureFormat.RGB24,true){name="PhotoWalkwayPavingGridTexture"};
                AssetDatabase.CreateAsset(texture,texturePath);
            }
            texture.wrapMode=TextureWrapMode.Repeat;
            texture.filterMode=FilterMode.Trilinear;
            texture.anisoLevel=16;
            var random=new System.Random(83);
            var colors=new Color[resolution*resolution];
            float pixelMeters=tileMeters/resolution;
            for(int y=0;y<resolution;y++)for(int x=0;x<resolution;x++)
            {
                float u=(x+.5f)/resolution,v=(y+.5f)/resolution;
                float edgeMeters=Mathf.Min(Mathf.Min(u,1-u),Mathf.Min(v,1-v))*tileMeters;
                float slab=Mathf.SmoothStep(0,1,Mathf.InverseLerp(groutMeters*.5f-pixelMeters*.5f,groutMeters*.5f+pixelMeters*.5f,edgeMeters));
                float noise=(float)random.NextDouble()*.014f+Mathf.PerlinNoise(x*.035f,y*.035f)*.006f;
                float value=Mathf.Lerp(.79f,.994f,slab)+noise;
                // The existing PhotoPaving base color supplies the warm concrete tone.
                colors[y*resolution+x]=new Color(value,value,value,1);
            }
            texture.SetPixels(colors);
            texture.Apply(true,false);
            EditorUtility.SetDirty(texture);
            var material=Mat("PhotoWalkwayPavingGrid",paving.GetColor("_BaseColor"),paving.GetFloat("_Smoothness"),paving.GetFloat("_Metallic"));
            material.SetTexture("_BaseMap",texture);
            material.SetTextureScale("_BaseMap",Vector2.one);
            material.SetTextureOffset("_BaseMap",Vector2.zero);
            EditorUtility.SetDirty(material);

            // Clone only this slab's rendered mesh. World XZ UVs retain 0.8m
            // spacing across its 83.96m length without changing any vertex,
            // transform, BoxCollider, top height or shadow setting.
            string meshPath=Folder+"/PhotoWalkwayPavingGridUV.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(!mesh)
            {
                mesh=UnityEngine.Object.Instantiate(walkway.sharedMesh);
                mesh.name="PhotoWalkwayPavingGridUV";
                AssetDatabase.CreateAsset(mesh,meshPath);
            }
            else EditorUtility.CopySerialized(walkway.sharedMesh,mesh);
            mesh.name="PhotoWalkwayPavingGridUV";
            mesh.uv=mesh.vertices.Select(vertex=>{
                var world=walkway.transform.TransformPoint(vertex);
                return new Vector2(world.x/tileMeters,world.z/tileMeters);
            }).ToArray();
            EditorUtility.SetDirty(mesh);
            walkway.sharedMesh=mesh;
            walkway.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        static void Gardens()
        {
            // Lawn lies between wings, while the actual court and plan-defined patio remain paved.
            // The library and teaching wing now sit farther back. Keep lawn
            // south of their complete relocated floors, without grass skins
            // entering the occupied rooms above their native floor surfaces.
            foreach(var b in new[]{new[]{1718f,1030f,2215f,1200f},new[]{2150f,1110f,2600f,1195f},new[]{1735f,540f,2044f,732f}})
                Slab("Jardim aberto — grama",b[0],b[1],b[2],b[3],.025f,grass);
        }
        static void MainHallTables()
        {
            for(int i=0;i<2;i++)
            {
                Vector3 c=P(1378,1200+i*90,.044f);
                Cube("Mesa de convivência — tampo",c+Vector3.up*.74f,new Vector3(.78f,.055f,2.05f),cream,true);
                for(int side=-1;side<=1;side+=2)
                {
                    Cube("Mesa de convivência — banco",c+new Vector3(side*.64f,.44f,0),new Vector3(.27f,.055f,2.05f),cream,true);
                    foreach(float z in new[]{-.75f,.75f})
                    {Cube("Mesa — pé",c+new Vector3(side*.28f,.35f,z),new Vector3(.035f,.70f,.035f),steel);Cube("Banco — pé",c+new Vector3(side*.64f,.21f,z),new Vector3(.035f,.42f,.035f),steel);}
                }
            }
        }
        static void ConfigurePhotoLights(Transform architecture)
        {
            foreach(var light in architecture.GetComponentsInChildren<Light>())
            {
                if(light.type==LightType.Directional)continue;
                if(light.transform.parent&&light.transform.parent.name=="Passarelas cobertas")
                {
                    // Old fixture heights belong to replaced roofs; narrow canopies use higher-mounted downlights.
                    Vector3 p=light.transform.position;
                    float py=1628-p.z/Scale;
                    if(py>1343){UnityEngine.Object.DestroyImmediate(light.gameObject);continue;}
                    if(py>=963&&py<=1343){p.y=4.16f;light.intensity=3.2f;light.range=12;}
                    else{p.y=2.58f;light.intensity=1.4f;light.range=10;}
                    light.transform.position=p;
                    Cube("Passarela — luminária sob cobertura",p+Vector3.up*.12f,new Vector3(.65f,.055f,.16f),cream);
                }
            }
            foreach(float y in new[]{990f,1055f,1120f,1200f,1275f})
            {
                Vector3 p=P(1421,y,4.12f);
                var fixture=Cube("Hall — luminária suspensa",p+Vector3.up*.13f,new Vector3(.34f,.07f,.65f),cream);
                var go=new GameObject("Hall — iluminação legível");go.transform.SetParent(parent,false);go.transform.position=p;
                var light=go.AddComponent<Light>();light.type=LightType.Point;light.intensity=4;light.range=12;light.color=new Color(.80f,.84f,.83f);light.shadows=LightShadows.None;
            }
            var facadeLamp=new GameObject("Fachada — luz do poste lateral");facadeLamp.transform.SetParent(parent,false);
            facadeLamp.transform.position=P(1515,1370,5.4f);facadeLamp.transform.LookAt(P(1421,1343,3.2f));
            var signLight=facadeLamp.AddComponent<Light>();signLight.type=LightType.Spot;signLight.spotAngle=100;signLight.innerSpotAngle=60;
            signLight.intensity=35;signLight.range=17;signLight.color=new Color(.77f,.83f,.86f);signLight.shadows=LightShadows.Soft;
            signLight.shadowBias=.1f;signLight.shadowNormalBias=.35f;signLight.shadowNearPlane=.15f;
        }
    }
}
