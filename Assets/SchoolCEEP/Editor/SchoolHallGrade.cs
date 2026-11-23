// Ferramenta da escola para construção ou acabamento.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HorrorGame;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>Local photographic grade changes; call after lighting, before static batching.</summary>
    public static class SchoolHallGrade
    {
        public const float Rise=.60f, HighTop=.644f, LowFinish=.044f;
        const float Scale=.07055555556f, FootClearance=.05f, RouteSpacing=.10f;
        const string MeshRoot="Assets/SchoolCEEP/Meshes/HallGrade";

        [Serializable] public sealed class Transition
        {
            public string name;
            public bool ramp;
            public int risers;
            public float width, run, low, high, riser, slopeDegrees;
        }
        [Serializable] public sealed class BuildReport
        {
            public float nominalRise, highLevel, lowFinish, raisedRoomFloor, preservedFirstFloor, maximumRiser;
            public int movedTransforms, rampMeshAssets;
            public string meshFolder;
            public Transition[] transitions;
            public SchoolCirculation.Route[] routes;
            public string[] notes;
        }
        public static BuildReport LastReport {get;private set;}
        static Transform group;
        static Material floor,blue;
        static string meshFolder;
        static int moved;
        static readonly List<Transition> transitions=new List<Transition>();
        static readonly List<SchoolCirculation.Route> routes=new List<SchoolCirculation.Route>();

        public static BuildReport Apply(Transform architecture,Transform photoGroup,Transform detailsGroup,Transform ownGroup)
        {
            if(!architecture||!photoGroup||!detailsGroup||!ownGroup)throw new ArgumentNullException("Provide all four school groups.");
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Build hall grade outside Play.");
            if(ownGroup.childCount!=0)throw new InvalidOperationException("Hall grade must run once on a freshly built group.");
            group=ownGroup;moved=0;transitions.Clear();routes.Clear();
            floor=AssetDatabase.LoadAssetAtPath<Material>("Assets/SchoolCEEP/Materials/PhotoConcreteFloor.mat");
            blue=AssetDatabase.LoadAssetAtPath<Material>("Assets/SchoolCEEP/Materials/PhotoFrameBlue.mat");
            if(!floor||!blue)throw new InvalidOperationException("Run SchoolPhotoPass before hall grade.");
            EnsureFolder(MeshRoot);
            string revision=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
            AssetDatabase.CreateFolder(MeshRoot,revision);meshFolder=MeshRoot+"/"+revision;
            RaiseExisting(architecture,photoGroup,detailsGroup);
            Platforms();

            Stairs("Entrada_Hall",P(1421,1367,.035f),P(1421,1343,HighTop),3.528f,
                P(1421,1372,.035f),P(1421,1338,HighTop),.035f,true,false);
            WestReturnRamp();
            EastCornerStairs();
            Stairs("Leste_Escada_Ampla",P(1512.3f,1075,0),P(1493.8f,1075,HighTop),170f*Scale,
                P(1517,1075,LowFinish),P(1489,1075,HighTop),LowFinish,false,false);
            var broadStair=routes[routes.Count-1];
            foreach(float y in new[]{1008f,1060f,1125f}) {
                Vector3 shift=P(0,y)-P(0,1075);
                routes.Add(new SchoolCirculation.Route{name="Leste_Escada_Trecho_"+y.ToString("F0"),
                    feet=broadStair.feet.Select(p=>p+shift).ToArray(),requireAscending=true});
            }
            Stairs("Leste_Escada_Parede_Lateral",P(1600,1141.5f,0),P(1600,1160,HighTop),211.68f*Scale,
                P(1600,1137,LowFinish),P(1600,1160.1f,HighTop),LowFinish,false,false);
            var sideStair=routes[routes.Count-1];var sideFeet=sideStair.feet.ToList();
            Line(sideFeet,Foot(P(1600,1160.1f,HighTop)),Foot(P(1480,1160.1f,HighTop)));sideStair.feet=sideFeet.ToArray();
            Ramp("Leste_Rampa",P(1703,978,0),P(1555,978,HighTop),22f*Scale,
                P(1708,978,LowFinish),P(1550,978,HighTop),LowFinish);
            var east=routes[routes.Count-1];var eastFeet=east.feet.ToList();
            Line(eastFeet,Foot(P(1550,978,HighTop)),Foot(P(1489,978,HighTop)));east.feet=eastFeet.ToArray();
            Stairs("Refeitorio_Escada",P(1427.6f,961.34f,0),P(1427.6f,983,HighTop),35.4f*Scale,
                P(1427.6f,956,0),P(1427.6f,988,HighTop),0);
            EdgeGuards();
            GroundPassage();
            Physics.SyncTransforms();
            LastReport=new BuildReport {
                nominalRise=Rise,highLevel=HighTop,lowFinish=LowFinish,raisedRoomFloor=Rise,preservedFirstFloor=3.81f,
                maximumRiser=HighTop/4f,movedTransforms=moved,rampMeshAssets=3,meshFolder=meshFolder,
                transitions=transitions.ToArray(),routes=routes.ToArray(),
                notes=new[]{
                    "Only administration, pedagogy, the main hall and west platform are raised; first-floor architecture remains at3.81m.",
                    "Hall finish is0.644m; eastern courtyard finish is0.044m: nominal local difference0.60m. Raised room floors remain0.60m.",
                    "The refectory opening is cut through the high slab from PDFy963.08 to983, betweenx1409.9 and1445.3.",
                    "All stairs have four solid risers of0.161m. Sloped decks are closed wedges with persisted MeshCollider assets.",
                    "Route feet follow actual tread/deck tops plus0.05m; east ramp's first samples follow the0.044m lower finish above the wedge.",
                    "Refectory route ends atPDFy956 on the low side and does not cross the service wall at947.",
                    "Both lateral courts are open to the sky. The east ramp extends across the cantina corner at PDFy978, from x1703 to1555, with a 10.44m run.",
                    "The east stair wraps in an L along both hall and Pedagogy edges, using eight non-overlapping solid tread strips. Stairs have no railings; the extended ramp retains its rails.",
                    "A separate low pedestrian passage remains clear behind the west ramp, alongPDFx1260 toPDFy930. Furniture stays outside its walking strip.",
                    "West ramp now follows two parallel lanes with a full-width south return landing. Its low entrance shares a flat junction with the adjacent short stairs; the high exit opens sideways onto the hall platform.",
                    "West stairs run directly eastward into the hall atPDFy995, with four straight rectangular treads 2.822m wide and a round blue handrail mounted on the treads beside the ramp. The return ramp ends farther south so its high deck does not cover the stairs.",
                    "A continuous low pedestrian path follows the garden around the south end of the west ramp, then runs beside the north Administration wall. It connects the lower west courtyard rather than stopping at the elevated platform.",
                    "Blue coarse guards sit0.04m outside the walking edges; decorative rails have no additional collision.",
                    "No other block, gameplay script, checkpoint, main stair or upper ramp is changed."
                }
            };
            Directory.CreateDirectory(SchoolMapBuilder.Output);
            File.WriteAllText(SchoolMapBuilder.Output+"/School-hall-grade-report.json",JsonUtility.ToJson(LastReport,true));
            return LastReport;
        }

        static Vector3 P(float x,float y,float h=0)=>SchoolMapBuilder.Point(x,y,h);
        static float PdfX(Vector3 p)=>1413+p.x/Scale;
        static float PdfY(Vector3 p)=>1628-p.z/Scale;
        static bool Inside(Vector3 p,float x0,float y0,float x1,float y1)=>PdfX(p)>=x0&&PdfX(p)<=x1&&PdfY(p)>=y0&&PdfY(p)<=y1;
        // Room grilles live under photoGroup; their facade axes may extend a
        // few PDF points past the nominal room bounding rectangle.
        static bool RaisedRoom(Vector3 p)=>Inside(p,1059.28f,1172.84f,1351,1346)||Inside(p,1490.8f,1172.84f,1708.48f,1346);
        static bool RaisedHall(Vector3 p)=>Inside(p,1348,963.08f,1493.8f,1343)||Inside(p,1318.48f,963.08f,1348,1174.76f);
        static void Shift(Transform item){item.position+=Vector3.up*Rise;moved++;}

        static void RaiseExisting(Transform architecture,Transform photoGroup,Transform detailsGroup)
        {
            foreach(string name in new[]{"administration_south","pedagogy_south"}) {
                var block=architecture.Find(name);if(!block)throw new InvalidOperationException("Missing grade block "+name);Shift(block);
            }
            var furniture=architecture.Find("Mobiliário inicial — 0");
            if(furniture)foreach(Transform child in furniture)if(RaisedRoom(child.position))Shift(child);
            var courtyard=architecture.Find("covered_courtyard");
            if(courtyard)foreach(Transform child in courtyard) {
                if(child.name=="Piso térreo")child.position+=Vector3.up*LowFinish;
                else if(child.GetComponent<Light>()||child.name=="Luminária fluorescente")Shift(child);
            }
            var covered=architecture.Find("Passarelas cobertas");
            if(covered)foreach(Transform child in covered)
                if((child.name=="Passagem coberta"||child.GetComponent<Light>())&&RaisedHall(child.position))Shift(child);
            foreach(var child in photoGroup.Cast<Transform>().ToArray()) {
                string n=child.name;
                if(n=="Hall principal — piso amplo"){UnityEngine.Object.DestroyImmediate(child.gameObject);continue;}
                if(n=="Fachada — luz do poste lateral") {
                    // The exterior pole stays at its original ground level.
                    child.LookAt(P(1421,1343,3.8f));continue;
                }
                if(n=="Pátio — pilar branco") {
                    if(PdfX(child.position)>1600){var s=child.localScale;s.y+=Rise;child.localScale=s;child.position+=Vector3.up*(Rise*.5f);moved++;}
                    else Shift(child);
                    continue;
                }
                bool hall=n.StartsWith("Hall principal —")||n.StartsWith("Hall —")||n.StartsWith("Fachada —")
                    ||n.StartsWith("Pórtico branco")||n.StartsWith("Portão aberto —")||n.StartsWith("Pátio — cobertura lateral")
                    ||n.StartsWith("Mesa de convivência —")||n=="Mesa — pé"||n=="Banco — pé"
                    ||n.StartsWith("Placa — CEEP")||n.StartsWith("Placa — CENTRO ESTADUAL")
                    ||n.StartsWith("Placa — DIREÇÃO")||n.StartsWith("Placa — PEDAGOGIA");
                if(hall||(n.StartsWith("Grade azul —")&&RaisedRoom(child.position))
                    ||(n=="Passarela — luminária sob cobertura"&&RaisedHall(child.position)))Shift(child);
                if(n=="Hall — faixa tátil azul") {
                    // Do not leave a high tactile strip floating across the cut stair mouth.
                    child.position=P(1421,(1343+983)*.5f,.049f+Rise);
                    var s=child.localScale;s.z=(1343-983)*Scale;child.localScale=s;
                }
            }
            var detailHall=detailsGroup.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Corredor principal — bancos e vasos");
            if(detailHall)Shift(detailHall);
        }

        static void Platforms()
        {
            // Three rectangles leave the complete low-to-high refectory flight empty.
            Slab("Hall — plataforma oeste da boca",1348,963.08f,1409.9f,983,HighTop);
            Slab("Hall — plataforma leste da boca",1445.3f,963.08f,1493.8f,983,HighTop);
            Slab("Hall — plataforma principal",1348,983,1493.8f,1343,HighTop);
            Slab("Hall — plataforma lateral oeste",1318.48f,989,1348,1174.76f,HighTop);
            Slab("Oeste — patamar superior",1318.48f,963.08f,1348,989,HighTop);
            Slab("Leste — patamar perto da cantina",1493.8f,963.08f,1555,990,HighTop);
            Slab("Leste — patamar da parede lateral",1493.8f,1160,1705.48f,1174.70f,HighTop);
        }
        static void EastCornerStairs()
        {
            // Each level comprises two touching strips. The corner remains one
            // continuous L-shaped terrace without coplanar overlapping boxes.
            const float tread=4.625f,firstReturn=1141.5f;
            for(int level=1;level<=4;level++) {
                float outerX=1512.3f-(level-1)*tread,innerX=1512.3f-level*tread;
                float nearY=firstReturn+(level-1)*tread,farY=firstReturn+level*tread;
                float top=HighTop*level/4f;
                Slab("Leste — degrau em L principal "+level,innerX,990,outerX,nearY,top);
                Slab("Leste — degrau em L parede "+level,innerX,nearY,1705.48f,farY,top);
            }
            var cornerFeet=new List<Vector3>();
            Line(cornerFeet,Foot(P(1517,1137,LowFinish)),Foot(P(1512.33f,1141.47f,LowFinish)));
            for(int level=1;level<=4;level++) {
                float top=HighTop*level/4f;
                Line(cornerFeet,Foot(P(1512.3f-(level-1)*tread-.03f,firstReturn+(level-1)*tread+.03f,top)),
                    Foot(P(1512.3f-level*tread+.03f,firstReturn+level*tread-.03f,top)));
            }
            Line(cornerFeet,cornerFeet[cornerFeet.Count-1],Foot(P(1480,1160.1f,HighTop)));
            routes.Add(new SchoolCirculation.Route{name="Leste_Escada_Canto_Diagonal",feet=cornerFeet.ToArray(),requireAscending=true});
        }
        static void WestReturnRamp()
        {
            const float low=.04f, middle=.342f, westX=1286.48f, eastX=1308.6f;
            // Flat shared entrance permits a lateral approach from the lower
            // pedestrian path, without walking through a guardrail or a riser.
            Slab("Oeste — encontro baixo da rampa e escada",1275.88f,1005,1297.08f,1021,low);
            Slab("Oeste — retorno amplo da rampa",1275.88f,1099.9f,1319.2f,1131.68f,middle);
            Slab("Oeste — desembarque alto da rampa",1298,1016,1319.2f,1034,HighTop);
            Ramp("Oeste_Rampa",P(westX,1021,low),P(westX,1099.9f,middle),21.2f*Scale,
                P(westX,1013,low),P(westX,1110,middle),low);
            var outward=routes[routes.Count-1];
            Ramp("Oeste_Rampa_Retorno",P(eastX,1099.9f,middle),P(eastX,1034,HighTop),21.2f*Scale,
                P(eastX,1110,middle),P(eastX,1023,HighTop),middle);
            var returning=routes[routes.Count-1];
            var through= new List<Vector3>();
            Line(through,Foot(P(1260,1013,.025f)),Foot(P(westX,1013,low)));
            foreach(var foot in outward.feet)Add(through,foot);
            Line(through,through[through.Count-1],Foot(P(eastX,1110,middle)));
            foreach(var foot in returning.feet)Add(through,foot);
            Line(through,through[through.Count-1],Foot(P(1340,1023,HighTop)));
            routes.Add(new SchoolCirculation.Route{name="Oeste_Rampa_Percurso_Completo",feet=through.ToArray(),requireAscending=true});
            var highExit=returning.feet.ToList();
            Line(highExit,highExit[highExit.Count-1],Foot(P(1340,1023,HighTop)));
            returning.feet=highExit.ToArray();
            Guard("Oeste — retorno sul",P(1275.88f,1132.24f,middle),P(1319.2f,1132.24f,middle));
            Guard("Oeste — retorno lateral oeste",P(1275.31f,1099.9f,middle),P(1275.31f,1132.24f,middle));
            Guard("Oeste — retorno lateral leste",P(1319.77f,1099.9f,middle),P(1319.77f,1132.24f,middle));
            Guard("Oeste — desembarque proteção lateral",P(1297.44f,1016,HighTop),P(1297.44f,1034,HighTop));
            Stairs("Oeste_Escada",P(1297.08f,995,0),P(1318.48f,995,HighTop),40f*Scale,
                P(1291,995,.025f),P(1340,995,HighTop),.025f,true,false);
            // Extend the proof across the complete straight approach, rather
            // than proving only the short stair and requiring a turn afterward.
            var steps=routes[routes.Count-1];var straight=new List<Vector3>();
            Line(straight,Foot(P(1260,995,.025f)),steps.feet[0]);
            foreach(var foot in steps.feet)Add(straight,foot);
            steps.feet=straight.ToArray();
            foreach(float y in new[]{983f,1007f}) {
                Vector3 shift=P(0,y)-P(0,995);
                routes.Add(new SchoolCirculation.Route{name="Oeste_Escada_Largura_"+y.ToString("F0"),
                    feet=steps.feet.Select(p=>p+shift).ToArray(),requireAscending=true});
            }
            WestStairHandrail();
        }
        static void WestStairHandrail()
        {
            // Posts stand on the actual tread tops; this handrail belongs to
            // the stair rather than the former guard across the high platform.
            const float edge=1013.8f, tread=5.35f;
            Vector3 first=Vector3.zero,last=Vector3.zero;
            for(int level=1;level<=4;level++) {
                Vector3 foot=P(1297.08f+tread*(level-.5f),edge,HighTop*level/4f);
                var top=foot+Vector3.up*.90f;
                RoundTube("Oeste escada — corrimão montante "+level,foot,top,.019f,true);
                if(level==1)first=top;if(level==4)last=top;
            }
            Vector3 direction=last-first;direction.Normalize();
            Vector3 a=first-direction*.16f,b=last+direction*.16f;
            RoundTube("Oeste escada — corrimão inclinado",a,b,.020f,true);
            // Short rounded returns keep the photographed pipe shape and
            // avoid a square handrail jutting into the approaches.
            foreach(Vector3 end in new[]{a,b}) {
                Vector3 previous=end;
                float sign=end==a?-1:1;
                for(int i=1;i<=8;i++) {
                    float angle=Mathf.PI*.5f*i/8;
                    Vector3 point=end+direction*(sign*.10f*Mathf.Sin(angle))
                        +Vector3.down*(.10f*(1-Mathf.Cos(angle)));
                    RoundTube("Oeste escada — retorno arredondado",previous,point,.020f,false);previous=point;
                }
            }
        }
        static void RoundTube(string name,Vector3 start,Vector3 end,float radius,bool collision)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(group,false);
            go.transform.position=(start+end)*.5f;go.transform.rotation=Quaternion.FromToRotation(Vector3.up,end-start);
            go.transform.localScale=new Vector3(radius*2,Vector3.Distance(start,end)*.5f,radius*2);go.isStatic=true;
            go.GetComponent<MeshRenderer>().sharedMaterial=blue;
            if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        static void GroundPassage()
        {
            var feet=new List<Vector3>();
            Line(feet,Foot(P(1260,1160,.025f)),Foot(P(1260,930,.025f)));
            Line(feet,Foot(P(1260,930,.025f)),Foot(P(1290,930,.025f)));
            routes.Add(new SchoolCirculation.Route{name="Oeste_Passagem_Baixa_Fundo",feet=feet.ToArray(),requireAscending=false});
            Slab("Oeste — passagem normal junto do jardim",1034,1144,1310,1169,.025f);
            var side=new List<Vector3>();
            Line(side,Foot(P(1308,1160,.025f)),Foot(P(1036,1160,.025f)));
            routes.Add(new SchoolCirculation.Route{name="Oeste_Passagem_Normal_Fim_Rampa",feet=side.ToArray(),requireAscending=false});
            // The last photographic crop shows a normal walking path beside
            // the garden, independent of both the staircase and the U ramp.
            var garden=new List<Vector3>();
            Line(garden,Foot(P(1260,1082,.025f)),Foot(P(1260,1160,.025f)));
            Line(garden,garden[garden.Count-1],Foot(P(1072,1160,.025f)));
            Line(garden,garden[garden.Count-1],Foot(P(1072,1085,.025f)));
            routes.Add(new SchoolCirculation.Route{name="Oeste_Passagem_Jardim_Administracao",feet=garden.ToArray(),requireAscending=false});
        }
        static void Slab(string name,float x0,float y0,float x1,float y1,float top)
        {
            const float bottom=-.07f;
            Surface(name,P((x0+x1)*.5f,(y0+y1)*.5f,(top+bottom)*.5f),
                new Vector3((x1-x0)*Scale,top-bottom,(y1-y0)*Scale),Quaternion.identity);
        }
        static GameObject Surface(string name,Vector3 center,Vector3 size,Quaternion rotation)
        {
            var go=Cube(name,center,size,rotation,floor,true);
            go.AddComponent<SurfaceType>().surface=GroundSurface.Concrete;return go;
        }

        static void Ramp(string name,Vector3 start,Vector3 end,float width,Vector3 approach,Vector3 exit,float minimumWalkingTop)
        {
            Vector3 forward=end-start;forward.y=0;float run=forward.magnitude;forward/=run;
            var mesh=new Mesh{name="HallGrade_"+name};float half=width*.5f;const float bottom=-.07f;
            mesh.vertices=new[]{new Vector3(-half,start.y,0),new Vector3(half,start.y,0),new Vector3(-half,end.y,run),new Vector3(half,end.y,run),
                new Vector3(-half,bottom,0),new Vector3(half,bottom,0),new Vector3(-half,bottom,run),new Vector3(half,bottom,run)};
            mesh.triangles=new[]{0,2,1,1,2,3,4,5,6,5,7,6,4,0,5,5,0,1,6,7,2,7,3,2,4,6,0,6,2,0,5,1,7,7,1,3};
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=meshFolder+"/"+name+".asset";AssetDatabase.CreateAsset(mesh,path);
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var go=new GameObject(name);go.transform.SetParent(group,false);go.transform.position=new Vector3(start.x,0,start.z);
            go.transform.rotation=Quaternion.LookRotation(forward);go.isStatic=true;
            go.AddComponent<MeshFilter>().sharedMesh=saved;go.AddComponent<MeshRenderer>().sharedMaterial=floor;
            go.AddComponent<MeshCollider>().sharedMesh=saved;go.AddComponent<SurfaceType>().surface=GroundSurface.Concrete;
            var feet=new List<Vector3>();Line(feet,Foot(approach),Foot(new Vector3(start.x,Mathf.Max(start.y,minimumWalkingTop),start.z)));
            int count=Mathf.Max(1,Mathf.CeilToInt(run/RouteSpacing));
            for(int i=0;i<=count;i++){var p=Vector3.Lerp(start,end,(float)i/count);p.y=Mathf.Max(p.y,minimumWalkingTop);Add(feet,Foot(p));}
            Line(feet,Foot(end),Foot(exit));
            routes.Add(new SchoolCirculation.Route{name=name,feet=feet.ToArray(),requireAscending=true});
            transitions.Add(new Transition{name=name,ramp=true,width=width,run=run,low=start.y,high=end.y,slopeDegrees=Mathf.Atan2(end.y-start.y,run)*Mathf.Rad2Deg});
            SideGuards(name,start,end,width);
        }
        static void Stairs(string name,Vector3 start,Vector3 end,float width,Vector3 approach,Vector3 exit,float lowWalkingTop,bool createMeshes=true,bool guards=true)
        {
            const int count=4;Vector3 forward=end-start;forward.y=0;float run=forward.magnitude;forward/=run;
            float riser=(end.y-start.y)/count,tread=run/count;
            if(riser>.20f)throw new InvalidOperationException("Hall grade riser exceeds0.20m.");
            for(int i=0;i<count;i++) {
                float top=start.y+riser*(i+1);Vector3 center=start+forward*(tread*(i+.5f));center.y=(top-.07f)*.5f;
                if(createMeshes)Surface(name+" — degrau "+(i+1),center,new Vector3(width,top+.07f,tread),Quaternion.LookRotation(forward));
            }
            var feet=new List<Vector3>();var before=start-forward*.002f;before.y=lowWalkingTop;
            Line(feet,Foot(approach),Foot(before));
            for(int i=0;i<count;i++) {
                Vector3 a=start+forward*(i*tread+.002f),b=start+forward*((i+1)*tread-.002f);
                a.y=b.y=start.y+riser*(i+1);Line(feet,Foot(a),Foot(b));
            }
            Line(feet,Foot(end),Foot(exit));
            routes.Add(new SchoolCirculation.Route{name=name,feet=feet.ToArray(),requireAscending=true});
            transitions.Add(new Transition{name=name,width=width,run=run,low=start.y,high=end.y,risers=count,riser=riser,slopeDegrees=Mathf.Atan2(end.y-start.y,run)*Mathf.Rad2Deg});
            if(guards)SideGuards(name,start,end,width);
        }

        static void SideGuards(string name,Vector3 start,Vector3 end,float width)
        {
            var direction=end-start;direction.y=0;direction.Normalize();
            var offset=Vector3.Cross(Vector3.up,direction)*(width*.5f+.04f);
            Guard(name+" — lado A",start-offset,end-offset);Guard(name+" — lado B",start+offset,end+offset);
        }
        static void EdgeGuards()
        {
            float outside=.04f/Scale;
            // The returning ramp exits sideways through this opening. A full
            // continuous high guard would block the only accessible exit.
            Guard("Oeste — borda alta norte",P(1318.48f-outside,963.08f,HighTop),P(1318.48f-outside,970,HighTop));
            Guard("Oeste — borda alta sul",P(1318.48f-outside,1042,HighTop),P(1318.48f-outside,1174.76f,HighTop));
            Guard("Oeste — patamar norte",P(1318.48f,963.08f-outside,HighTop),P(1348,963.08f-outside,HighTop));
            Guard("Leste — patamar cantina norte",P(1493.8f,963.08f-outside,HighTop),P(1555,963.08f-outside,HighTop));
            Guard("Leste — canto cantina externo",P(1555+outside,963.08f,HighTop),P(1555+outside,967,HighTop));
        }
        static void Guard(string name,Vector3 start,Vector3 end)
        {
            float length=Vector3.Distance(start,end);if(length<.05f)return;
            foreach(float h in new[]{1f,.52f,.12f})Beam(name+" — barra",start+Vector3.up*h,end+Vector3.up*h,h==1?.038f:.026f);
            int n=Mathf.Max(1,Mathf.CeilToInt(new Vector2(end.x-start.x,end.z-start.z).magnitude/1.20f));
            for(int i=0;i<=n;i++)Cube(name+" — montante",Vector3.Lerp(start,end,(float)i/n)+Vector3.up*.5f,new Vector3(.038f,1,.038f),Quaternion.identity,blue,false);
            var blocker=Cube(name+" — proteção física",(start+end)*.5f+Vector3.up*.5f,new Vector3(.035f,1,length),Quaternion.LookRotation(end-start),blue,true);
            UnityEngine.Object.DestroyImmediate(blocker.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(blocker.GetComponent<MeshFilter>());
        }
        static void Beam(string name,Vector3 start,Vector3 end,float width)=>Cube(name,(start+end)*.5f,new Vector3(width,width,Vector3.Distance(start,end)),Quaternion.LookRotation(end-start),blue,false);
        static GameObject Cube(string name,Vector3 center,Vector3 size,Quaternion rotation,Material material,bool collision)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(group,false);
            go.transform.position=center;go.transform.rotation=rotation;go.transform.localScale=size;go.isStatic=true;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static Vector3 Foot(Vector3 top)=>top+Vector3.up*FootClearance;
        static void Line(List<Vector3> feet,Vector3 start,Vector3 end)
        {
            int n=Mathf.Max(1,Mathf.CeilToInt(new Vector2(end.x-start.x,end.z-start.z).magnitude/RouteSpacing));
            for(int i=0;i<=n;i++)Add(feet,Vector3.Lerp(start,end,(float)i/n));
        }
        static void Add(List<Vector3> feet,Vector3 point){if(feet.Count==0||(feet[feet.Count-1]-point).sqrMagnitude>.00000001f)feet.Add(point);}
        static void EnsureFolder(string path)
        {
            var parts=path.Split('/');string parent=parts[0];
            for(int i=1;i<parts.Length;i++){string next=parent+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(parent,parts[i]);parent=next;}
        }
    }
}
