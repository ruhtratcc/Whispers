// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>Details from the second seven-photo set, without changing plan architecture.</summary>
    public static class SchoolFrontPhotoProps
    {
        public const float RaisedHallLevel = .60f;
        const float Scale = .0705555556f;
        const string Folder = "Assets/SchoolCEEP/Materials";

        [Serializable]
        public sealed class Report
        {
            public int curvedBenches, pingPongTables, pots, grilles, airConditioners, serviceCounters;
            public int ceilingFixtures, speakers, weakFillLights, meshes, colliders;
            public float raisedHallLevel = RaisedHallLevel;
            public float refectoryClearOpeningMeters;
            public string[] photoSources, regions, notes;
        }

        public static Report LastReport { get; private set; }
        static Material blue, darkBlue, tileBlue, mortar, white, steel, black, leaf, soil, lamp;
        static Mesh cylinder;

        public static Report Build(Transform parent)
        {
            if(!parent)throw new ArgumentNullException(nameof(parent));
            if(EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build school photo props outside Play mode.");
            CreateMaterials();
            LastReport=new Report
            {
                refectoryClearOpeningMeters=(1446.3f-1408.9f)*Scale-.045f,
                photoSources=new[]
                {
                    "C:/Users/ruhtra/Downloads/IMG-20261002-WA0001.jpg",
                    "C:/Users/ruhtra/Downloads/IMG-20261002-WA0008.jpg",
                    "C:/Users/ruhtra/Downloads/IMG-20261002-WA0007.jpg",
                    "C:/Users/ruhtra/Downloads/IMG-20261002-WA0005.jpg",
                    "C:/Users/ruhtra/Downloads/IMG_20261002_085313.jpg",
                    "C:/Users/ruhtra/Downloads/IMG_20261002_085552.jpg",
                    "C:/Users/ruhtra/Downloads/IMG_20261002_085537.jpg"
                },
                regions=new[]
                {
                    "Lower west court: curved bench center1227,1038 rotated toward the court; planter1227,1060; clear low pedestrian routePDFx1260, beside Auditorium east envelope1210.12 and west ramp1275..1318.",
                    "Lower east Games Tables: no curved bench per user correction; ping-pong at 1660,1110 and relocated pot at 1687,1000 in the low court corner, leaving the stair and ramp approaches clear.",
                    "Raised west platform: pots1338,1080 and1338,1118 at0.60m; the first planter is clear of the new ramp exit at y1023. Helper does not elevate existing architecture.",
                    "Refectory south facade: structuralPDFy959; cosmetic faces at962 ahead of native face961.34; panels1318.48..1408.9 and1446.3..1605.16.",
                    "Kitchen service prop: center1490,945; width2.60m; floor0; footprint1471.6..1508.4/y940.3..949.7 avoids native internal walls.",
                    "Auditorium east facade: AC atPDFx1213.2/y920 and1000,height4.50m; cabinetry offset from structural face1210.12.",
                    "Main canopy: fixture/speaker mounting around4.8..4.96m after root applies the0.60m hall elevation."
                },
                notes=new[]
                {
                    "Photo-to-plan associations follow the agreed registration; curved bench locations and furniture dimensions remain visual estimates.",
                    "The right court curved bench at 1630,1050 was removed by user request; its pot now sits in the low court corner at 1687,1000. The left curved bench and existing inner-court benches are preserved.",
                    "The native refectory mouth1409.9..1445.3 remains open; new blue grilles and tile overlays have no colliders.",
                    "No floors, steps, ramps, room footprints, existing doors, roof surfaces, people, AudioSources, voices or story events are changed.",
                    "Call after SchoolHallGrade and before static mesh persistence; all new prop meshes are static.",
                    "Two shadowless local wall-mounted lights make the open lateral courts readable; no lamps or suspension wires float without a roof."
                }
            };
            var props=Group(parent,"Segundo conjunto de fotos — pátios e refeitório");
            var lower=Group(props,"Pátios baixos — mobiliário fotográfico");
            CurvedBench(lower,P(1227f,1038f,.025f),90f);
            Pot(lower,P(1227f,1060f,.025f),.45f,.25f);
            Pot(lower,P(1687f,1000f,0f),.46f,.26f);
            PingPong(lower,P(1660f,1110f,0f),0f);
            Pot(lower,P(1338f,1080f,RaisedHallLevel),.43f,.24f);
            Pot(lower,P(1338f,1118f,RaisedHallLevel),.46f,.25f);
            var facade=Group(props,"Refeitório — grade e azulejos");
            Tiles(facade,1318.48f,1408.9f,962f,0f,1.15f);
            Tiles(facade,1446.3f,1605.16f,962f,0f,1.15f);
            Grille(facade,"Grade azul esquerda — fora do vão",1318.48f,1408.9f,962f,1.15f,3.40f,false);
            Grille(facade,"Grade azul direita — fora do vão",1446.3f,1605.16f,962f,1.15f,3.40f,false);
            Counter(facade,P(1490f,945f,0f),2.60f);
            var ac=Group(props,"Auditório — detalhes altos da fachada leste");
            AirConditioner(ac,P(1213.2f,920f,4.50f),90f);
            AirConditioner(ac,P(1213.2f,1000f,4.50f),90f);
            var ceiling=Group(props,"Cobertura principal — luminárias e caixas visuais");
            foreach(float x in new[]{1385f,1460f})foreach(float y in new[]{1040f,1120f})
                Fixture(ceiling,P(x,y,4.80f));
            Speaker(ceiling,P(1382f,1070f,4.96f),60f);
            Speaker(ceiling,P(1462f,1150f,4.96f),-60f);
            var fill=new GameObject("Pátio lateral — luz fraca de leitura");fill.transform.SetParent(props,false);
            fill.transform.localPosition=P(1220f,1060f,3.80f);
            var light=fill.AddComponent<Light>();light.type=LightType.Point;light.range=5.5f;light.intensity=1.15f;
            light.color=new Color(.79f,.83f,.77f);light.shadows=LightShadows.None;
            Fixture(props,P(1220f,1060f,3.87f));
            Line(props,"Pátio oeste — braço de arandela",P(1211f,1060f,3.96f),P(1220f,1060f,3.96f),.035f,darkBlue);
            LastReport.weakFillLights=1;
            var gamesLight=new GameObject("Mesas de jogos — arandela na fachada");gamesLight.transform.SetParent(props,false);
            gamesLight.transform.localPosition=P(1645f,1167f,3.65f);
            var games=gamesLight.AddComponent<Light>();games.type=LightType.Point;games.range=11;games.intensity=5;
            games.color=new Color(.79f,.83f,.77f);games.shadows=LightShadows.None;
            Fixture(props,P(1645f,1167f,3.72f));
            Line(props,"Mesas de jogos — braço de arandela",P(1645f,1174.5f,3.81f),P(1645f,1167f,3.81f),.035f,darkBlue);
            LastReport.weakFillLights++;
            return LastReport;
        }

        static Vector3 P(float x, float y, float h = 0f) =>
            new Vector3((x - 1413f) * Scale, h, (1628f - y) * Scale);

        static void CreateMaterials()
        {
            EnsureFolder(Folder);
            blue = Mat("FrontPhotoConcreteBlue", new Color(.060f,.18f,.37f));
            darkBlue = Mat("FrontPhotoSteelBlue", new Color(.045f,.13f,.28f), .24f,.25f);
            tileBlue = Mat("FrontPhotoCeramicBlue", new Color(.055f,.075f,.25f), .30f);
            mortar = Mat("FrontPhotoTileJoint", new Color(.63f,.64f,.62f));
            white = Mat("FrontPhotoOffWhite", new Color(.69f,.70f,.66f));
            steel = Mat("FrontPhotoKitchenSteel", new Color(.43f,.46f,.46f), .32f,.70f);
            black = Mat("FrontPhotoSpeakerDark", new Color(.025f,.03f,.034f), .12f);
            leaf = Mat("FrontPhotoLeafGreen", new Color(.12f,.25f,.10f));
            soil = Mat("FrontPhotoPotEarth", new Color(.075f,.060f,.040f));
            lamp = Mat("FrontPhotoLampFace", new Color(.72f,.76f,.71f));
            lamp.EnableKeyword("_EMISSION");
            lamp.SetColor("_EmissionColor", new Color(.80f,.86f,.78f) * .9f);
            EditorUtility.SetDirty(lamp);
        }

        static void CurvedBench(Transform parent, Vector3 origin, float yaw)
        {
            var bench = Group(parent, "Banco curvo de concreto azul");
            bench.localPosition = origin;
            bench.localRotation = Quaternion.Euler(0f,yaw,0f);
            const float inner = 1.00f, outer = 1.43f, bottom = .46f, thickness = .14f;
            const int segments = 18;
            const float a0 = -8f, a1 = 188f;
            MeshObject(bench,"Assento semicircular",ArcSeat(inner,outer,thickness,a0,a1,segments),
                Vector3.up * bottom,Quaternion.identity,Vector3.one,blue);
            for(int i = 0; i < 3; i++)
            {
                float a = (15f + i * 75f) * Mathf.Deg2Rad;
                Vector3 foot = new Vector3(Mathf.Cos(a)*1.215f,.235f,Mathf.Sin(a)*1.215f);
                var leg = Cube(bench,"Apoio maciço",foot,new Vector3(.27f,.47f,.43f),blue,true);
                leg.transform.localRotation = Quaternion.Euler(0f,-a*Mathf.Rad2Deg,0f);
            }
            // Simple boxes follow the curved seat, so the seat cannot be walked through.
            for(int i = 0; i < segments; i++)
            {
                float a = Mathf.Lerp(a0,a1,(i+.5f)/segments)*Mathf.Deg2Rad;
                var blocker = new GameObject("Colisor do assento curvo") { isStatic = true };
                blocker.transform.SetParent(bench,false);
                blocker.transform.localPosition = new Vector3(Mathf.Cos(a)*1.215f,bottom+thickness/2f,Mathf.Sin(a)*1.215f);
                blocker.transform.localRotation = Quaternion.Euler(0f,-a*Mathf.Rad2Deg,0f);
                blocker.AddComponent<BoxCollider>().size = new Vector3(outer-inner,thickness,.247f);
                LastReport.colliders++;
            }
            LastReport.curvedBenches++;
        }

        static void PingPong(Transform parent,Vector3 origin,float yaw)
        {
            var table = Group(parent,"Mesa de ping-pong de concreto azul");
            table.localPosition = origin; table.localRotation = Quaternion.Euler(0f,yaw,0f);
            Cube(table,"Tampo azul",new Vector3(0f,.70f,0f),new Vector3(2.74f,.14f,1.525f),blue,true);
            foreach(float x in new[]{-.85f,.85f})
                Cube(table,"Pé largo de concreto",new Vector3(x,.315f,0f),new Vector3(.34f,.63f,1.03f),blue,true);
            foreach(float z in new[]{-.750f,.750f})
                Cube(table,"Linha branca lateral",new Vector3(0f,.774f,z),new Vector3(2.72f,.006f,.016f),white);
            foreach(float x in new[]{-1.36f,1.36f})
                Cube(table,"Linha branca de fundo",new Vector3(x,.774f,0f),new Vector3(.016f,.006f,1.50f),white);
            Cube(table,"Linha central de duplas",new Vector3(0f,.774f,0f),new Vector3(2.72f,.006f,.012f),white);
            foreach(float z in new[]{-.84f,.84f})
                Cylinder(table,"Poste da rede",new Vector3(0f,.885f,z),.021f,.23f,darkBlue);
            for(int i=0;i<4;i++)
                Cube(table,"Fio horizontal da rede",new Vector3(0f,.80f+i*.041f,0f),new Vector3(.007f,.006f,1.67f),darkBlue);
            for(int i=0;i<23;i++)
                Cube(table,"Fio vertical da rede",new Vector3(0f,.862f,-.80f+i*.073f),new Vector3(.006f,.15f,.006f),darkBlue);
            LastReport.pingPongTables++;
        }

        static void Pot(Transform parent,Vector3 origin,float height=.42f,float radius=.23f)
        {
            var pot=Group(parent,"Vaso branco com folhagem");pot.localPosition=origin;
            MeshObject(pot,"Vaso cônico",PotMesh(height,radius),Vector3.zero,Quaternion.identity,Vector3.one,white);
            Cylinder(pot,"Terra visível",new Vector3(0f,height-.035f,0f),radius*.86f,.02f,soil);
            var blocker=pot.gameObject.AddComponent<BoxCollider>();
            blocker.center=Vector3.up*(height/2f);blocker.size=new Vector3(radius*1.8f,height,radius*1.8f);
            LastReport.colliders++;
            for(int i=0;i<9;i++)
            {
                float length=.48f+(i%4)*.11f;
                MeshObject(pot,"Folha",LeafMesh(length,.105f),Vector3.up*(height-.025f),
                    Quaternion.Euler(25f+(i%3)*12f,i*137.5f,0f),Vector3.one,leaf);
            }
            LastReport.pots++;
        }

        static void AirConditioner(Transform parent,Vector3 origin,float yaw)
        {
            var unit=Group(parent,"Condensadora de ar condicionado — fachada");
            unit.localPosition=origin;unit.localRotation=Quaternion.Euler(0f,yaw,0f);
            Cube(unit,"Gabinete claro",Vector3.zero,new Vector3(.77f,.50f,.30f),white);
            Cylinder(unit,"Ventilador escuro",new Vector3(-.15f,0f,.156f),.184f,.015f,black,Quaternion.Euler(90f,0f,0f));
            for(int i=0;i<6;i++)
            {
                float a=i*60f*Mathf.Deg2Rad;
                Line(unit,"Grade radial do ventilador",new Vector3(-.15f,0f,.168f),
                    new Vector3(-.15f+Mathf.Cos(a)*.183f,Mathf.Sin(a)*.183f,.168f),.007f,steel);
            }
            for(int i=0;i<5;i++)
                Cube(unit,"Grelha lateral",new Vector3(.245f,-.12f+i*.06f,.160f),new Vector3(.16f,.012f,.018f),steel);
            foreach(float x in new[]{-.25f,.25f})
            {
                Cube(unit,"Suporte de parede",new Vector3(x,-.31f,-.04f),new Vector3(.04f,.055f,.38f),steel);
                Cube(unit,"Suporte vertical",new Vector3(x,-.24f,-.20f),new Vector3(.04f,.23f,.04f),steel);
            }
            Line(unit,"Tubo de dreno",new Vector3(.32f,-.20f,-.16f),new Vector3(.32f,-1.22f,-.16f),.012f,white);
            LastReport.airConditioners++;
        }

        static void Grille(Transform parent,string name,float x0,float x1,float pdfY,float bottom,float top,bool collision)
        {
            var grille=Group(parent,name);
            float width=(x1-x0)*Scale;
            grille.localPosition=P((x0+x1)/2f,pdfY,(bottom+top)/2f);
            float height=top-bottom;
            foreach(float x in new[]{-width/2f,width/2f})
                Cube(grille,"Montante azul",new Vector3(x,0f,0f),new Vector3(.045f,height,.055f),darkBlue);
            foreach(float y in new[]{-height/2f,height/2f})
                Cube(grille,"Travessa azul",new Vector3(0f,y,0f),new Vector3(width,.045f,.055f),darkBlue);
            int rows=Mathf.Max(2,Mathf.CeilToInt(height/.22f));
            for(int row=1;row<rows;row++)
                Cube(grille,"Barra horizontal",new Vector3(0f,-height/2f+row*height/rows,0f),new Vector3(width,.017f,.025f),darkBlue);
            for(int row=0;row<rows;row++)
            {
                float start=-width/2f+(row%2==0?.37f:.72f);
                for(float x=start;x<width/2f-.025f;x+=.72f)
                    Cube(grille,"Barra desencontrada",new Vector3(x,-height/2f+(row+.5f)*height/rows,0f),new Vector3(.014f,height/rows,.025f),darkBlue);
            }
            if(collision)
            {
                grille.gameObject.AddComponent<BoxCollider>().size=new Vector3(width,height,.05f);
                LastReport.colliders++;
            }
            LastReport.grilles++;
        }

        static void Tiles(Transform parent,float x0,float x1,float pdfY,float bottom,float height)
        {
            float width=(x1-x0)*Scale;
            var panel=Group(parent,"Revestimento de azulejo azul — refeitório");
            panel.localPosition=P((x0+x1)/2f,pdfY,bottom);
            Cube(panel,"Painel cerâmico azul",new Vector3(0f,height/2f,0f),new Vector3(width,height,.025f),tileBlue);
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(float x=-width/2f;x<=width/2f;x+=.155f)
                Quad(vertices,triangles,x-.0017f,0f,x+.0017f,height,-.0135f);
            for(float y=0f;y<=height;y+=.155f)
                Quad(vertices,triangles,-width/2f,y-.0017f,width/2f,y+.0017f,-.0135f);
            MeshObject(panel,"Juntas claras entre azulejos",MakeMesh("Front photo tile joints",vertices.ToArray(),triangles.ToArray()),
                Vector3.zero,Quaternion.identity,Vector3.one,mortar);
        }

        static void Counter(Transform parent,Vector3 origin,float width)
        {
            var counter=Group(parent,"Serviço da cozinha atrás da grade");counter.localPosition=origin;
            counter.localRotation=Quaternion.Euler(0f,180f,0f);
            Cube(counter,"Base do balcão azul",new Vector3(0f,.43f,0f),new Vector3(width,.86f,.66f),blue,true);
            Cube(counter,"Tampo de aço",new Vector3(0f,.90f,0f),new Vector3(width+.10f,.08f,.75f),steel,true);
            float halfSash=(width-.18f)/4f;
            foreach(float x in new[]{-width/4f,width/4f})
            {
                var service=Group(counter,"Janela de atendimento da cozinha");
                service.localPosition=new Vector3(x,.98f,.17f);
                foreach(float side in new[]{-halfSash,halfSash})
                    Cube(service,"Ombreira clara",new Vector3(side,.55f,0f),new Vector3(.045f,1.10f,.055f),white);
                foreach(float y in new[]{0f,1.10f})
                    Cube(service,"Travessa da janela",new Vector3(0f,y,0f),new Vector3(halfSash*2f+.05f,.045f,.055f),white);
                // The awning stays open; slim metal framing conveys the service sash without a wall.
                foreach(float side in new[]{-halfSash,halfSash})
                    Line(service,"Braço da folha levantada",new Vector3(side,1.10f,0f),new Vector3(side,.72f,.63f),.017f,steel);
                Line(service,"Borda da folha levantada",new Vector3(-halfSash,.72f,.63f),new Vector3(halfSash,.72f,.63f),.017f,steel);
                Cylinder(counter,"Panela de cozinha",new Vector3(x,1.07f,-.14f),.14f,.26f,steel);
                Cylinder(counter,"Tampa da panela",new Vector3(x,1.21f,-.14f),.147f,.018f,steel);
                Cylinder(counter,"Puxador da tampa",new Vector3(x,1.24f,-.14f),.022f,.034f,black);
            }
            LastReport.serviceCounters++;
        }

        static void Fixture(Transform parent,Vector3 position)
        {
            var fixture=Group(parent,"Luminária de teto — foto");fixture.localPosition=position;
            Cube(fixture,"Carcaça da luminária",Vector3.zero,new Vector3(.26f,.055f,.36f),steel);
            Cube(fixture,"Difusor emissivo",Vector3.down*.033f,new Vector3(.22f,.012f,.32f),lamp);
            Line(fixture,"Suspensão",Vector3.up*.02f,Vector3.up*.35f,.0045f,black);
            LastReport.ceilingFixtures++;
        }

        static void Speaker(Transform parent,Vector3 position,float yaw)
        {
            var speaker=Group(parent,"Caixa de som suspensa — somente visual");
            speaker.localPosition=position;speaker.localRotation=Quaternion.Euler(25f,yaw,0f);
            Cube(speaker,"Gabinete preto",Vector3.zero,new Vector3(.35f,.27f,.25f),black);
            Cylinder(speaker,"Cone da caixa",new Vector3(0f,0f,.133f),.085f,.01f,steel,Quaternion.Euler(90f,0f,0f));
            Line(speaker,"Cabo de suspensão",new Vector3(0f,.135f,0f),new Vector3(0f,.53f,-.18f),.005f,black);
            LastReport.speakers++;
        }

        static Material Mat(string name,Color color,float smooth=.10f,float metallic=0f)
        {
            string path=Folder+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material)
            {
                var shader=Shader.Find("Universal Render Pipeline/Lit");
                if(!shader)throw new InvalidOperationException("URP Lit shader is required.");
                material=new Material(shader){name=name};AssetDatabase.CreateAsset(material,path);
            }
            material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",smooth);material.SetFloat("_Metallic",metallic);
            EditorUtility.SetDirty(material);return material;
        }

        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            int split=path.LastIndexOf('/');string parent=path.Substring(0,split);
            EnsureFolder(parent);AssetDatabase.CreateFolder(parent,path.Substring(split+1));
        }

        static Transform Group(Transform parent,string name)
        {
            var item=new GameObject(name){isStatic=true};item.transform.SetParent(parent,false);return item.transform;
        }

        static GameObject Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material material,bool collision=false)
        {
            var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.name=name;item.isStatic=true;
            item.transform.SetParent(parent,false);item.transform.localPosition=position;item.transform.localScale=scale;
            item.GetComponent<MeshRenderer>().sharedMaterial=material;
            if(!collision)UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());else LastReport.colliders++;
            LastReport.meshes++;return item;
        }

        static void MeshObject(Transform parent,string name,Mesh mesh,Vector3 position,Quaternion rotation,Vector3 scale,Material material)
        {
            var item=new GameObject(name){isStatic=true};item.transform.SetParent(parent,false);
            item.transform.localPosition=position;item.transform.localRotation=rotation;item.transform.localScale=scale;
            item.AddComponent<MeshFilter>().sharedMesh=mesh;item.AddComponent<MeshRenderer>().sharedMaterial=material;
            LastReport.meshes++;
        }

        static void Cylinder(Transform parent,string name,Vector3 position,float radius,float height,Material material,Quaternion? rotation=null)
        {
            if(!cylinder)cylinder=CylinderMesh();
            MeshObject(parent,name,cylinder,position,rotation??Quaternion.identity,new Vector3(radius*2f,height,radius*2f),material);
        }

        static void Line(Transform parent,string name,Vector3 start,Vector3 end,float radius,Material material)
        {
            if(!cylinder)cylinder=CylinderMesh();
            MeshObject(parent,name,cylinder,(start+end)/2f,Quaternion.FromToRotation(Vector3.up,end-start),
                new Vector3(radius*2f,Vector3.Distance(start,end),radius*2f),material);
        }

        static void Quad(List<Vector3> vertices,List<int> triangles,float x0,float y0,float x1,float y1,float z)
        {
            int start=vertices.Count;
            vertices.AddRange(new[]{new Vector3(x0,y0,z),new Vector3(x1,y0,z),new Vector3(x0,y1,z),new Vector3(x1,y1,z)});
            triangles.AddRange(new[]{start,start+2,start+1,start+2,start+3,start+1});
        }

        static Mesh ArcSeat(float inner,float outer,float height,float startAngle,float endAngle,int segments)
        {
            var vertices=new Vector3[(segments+1)*4];var indices=new List<int>();
            for(int i=0;i<=segments;i++)
            {
                float a=Mathf.Lerp(startAngle,endAngle,(float)i/segments)*Mathf.Deg2Rad;
                Vector3 radial=new Vector3(Mathf.Cos(a),0f,Mathf.Sin(a));
                vertices[i*4]=radial*outer;vertices[i*4+1]=radial*outer+Vector3.up*height;
                vertices[i*4+2]=radial*inner;vertices[i*4+3]=radial*inner+Vector3.up*height;
                if(i==segments)continue;
                int b=i*4,n=b+4;
                indices.AddRange(new[]{b,b+1,n,n,b+1,n+1,b+2,n+2,b+3,n+2,n+3,b+3,
                    b+1,b+3,n+1,n+1,b+3,n+3,b,n,b+2,n,n+2,b+2});
            }
            indices.AddRange(new[]{0,2,1,1,2,3});int e=segments*4;
            indices.AddRange(new[]{e,e+1,e+2,e+1,e+3,e+2});
            return MakeMesh("Front photo curved bench",vertices,indices.ToArray());
        }

        static Mesh PotMesh(float height,float radius)
        {
            const int sides=16;var vertices=new Vector3[sides*4];var indices=new List<int>();
            float[] radii={radius*.68f,radius,radius*.88f,radius*.56f};float[] heights={0f,height,height,.06f};
            for(int ring=0;ring<4;ring++)for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2f/sides;vertices[ring*sides+i]=new Vector3(Mathf.Cos(a)*radii[ring],heights[ring],Mathf.Sin(a)*radii[ring]);
                int n=(i+1)%sides,next=(ring+1)%4,a0=ring*sides+i,b=next*sides+i,c=ring*sides+n,d=next*sides+n;
                indices.AddRange(new[]{a0,b,c,c,b,d});
            }
            return MakeMesh("Front photo hollow pot",vertices,indices.ToArray());
        }

        static Mesh LeafMesh(float length,float width)
        {
            Vector3[] front={Vector3.zero,new Vector3(-width*.7f,length*.25f,.02f),new Vector3(width*.7f,length*.25f,.02f),
                new Vector3(-width,length*.60f,.08f),new Vector3(width,length*.60f,.08f),new Vector3(0f,length,.20f)};
            var vertices=new Vector3[front.Length*2];Array.Copy(front,vertices,front.Length);Array.Copy(front,0,vertices,front.Length,front.Length);
            int[] faces={0,1,2,1,3,2,2,3,4,3,5,4};var indices=new int[faces.Length*2];Array.Copy(faces,indices,faces.Length);
            for(int i=0;i<faces.Length;i+=3)
            {indices[faces.Length+i]=faces[i+2]+front.Length;indices[faces.Length+i+1]=faces[i+1]+front.Length;indices[faces.Length+i+2]=faces[i]+front.Length;}
            return MakeMesh("Front photo plant leaf",vertices,indices);
        }

        static Mesh CylinderMesh()
        {
            const int sides=12;var vertices=new Vector3[sides*2+2];var triangles=new int[sides*12];
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2f/sides;vertices[i]=new Vector3(Mathf.Cos(a)*.5f,-.5f,Mathf.Sin(a)*.5f);vertices[sides+i]=vertices[i]+Vector3.up;
                int next=(i+1)%sides,t=i*12;
                int[] face={i,sides+i,next,next,sides+i,sides+next,sides*2,i,next,sides*2+1,sides+next,sides+i};
                Array.Copy(face,0,triangles,t,12);
            }
            vertices[sides*2]=Vector3.down*.5f;vertices[sides*2+1]=Vector3.up*.5f;
            return MakeMesh("Front photo cylinder",vertices,triangles);
        }

        static Mesh MakeMesh(string name,Vector3[] vertices,int[] triangles)
        {
            var mesh=new Mesh{name=name,vertices=vertices,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
