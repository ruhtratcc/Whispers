// Ferramenta da escola para construção ou acabamento.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HorrorGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    // Rebuildable architecture. PDF geometry is data, never executable instructions.
    public static class SchoolMapBuilder
    {
        public const string Folder = "Assets/SchoolCEEP";
        public const string Output = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs";
        [Serializable] public class Layout { public float metersPerPoint, floorHeight; public float[] origin; public Region[] regions; public Level[] levels; }
        [Serializable] public class Region { public string name; public float[] box; public bool upper; }
        [Serializable] public class Level { public int index; public string name; public Span[] walls, doors, windows; }
        [Serializable] public class Span { public string block,source; public bool vertical,provisional; public float p, a, b, thickness, width; }
        [Serializable] public class Rooms { public Room[] rooms; }
        [Serializable] public class Room { public string name; public int level; public float[] box; public string kind; }
        [Serializable] public class Report { public string scene, status; public int walls, windows, doors, lights, rooms, staticRenderers, redundantTrimCollidersOmitted, provisionalFurnitureRelocated, provisionalFurnitureOmitted; public float metersPerPoint, floorHeight; public SchoolIntegrationPass.IntegrationReport integration; }
        static Layout layout;
        static Report report;
        static Transform root;
        static Material plaster, lowerPaint, concrete, tile, metal, timber, glass, roof, white, grass, asphalt, glow, blackboard;
        static GameObject doorPrefab;
        static string meshRevision;
        static readonly Dictionary<string,Transform> groups = new Dictionary<string,Transform>();
        static readonly Dictionary<Transform,Bounds> provisionalDeskAreas = new Dictionary<Transform,Bounds>();
        static readonly float[] MainStair = {987.52f,722.12f,1049.92f,809.24f};
        static readonly float[] EastStair = {2290.84f,774.6f,2393.08f,824.84f};
        // Registered single classroom accesses, including the four upper east teaching rooms.
        // These slots are retained by the central and emergency append-only passes.
        public static readonly int[] UpperClassroomDoorSlots = {0,1,2,4,5,8,9,10,11,23,24,25,26,27,28,29};
        public const float UpperClassroomDoorMinimumWidth = 1.10f;
        public const float WestHighWallHeight = 4.95f;
        public static float NativeBaseHeight(string block,int level) => level*3.81f+
            (level==0?(block==SchoolNearCanteenGradePass.Block?SchoolNearCanteenGradePass.Floor:
                block=="west_north"?SchoolWestBlockAccessPass.LowFloor:0f):0f);
        static float WallHeight(string block) => block=="west_south"?WestHighWallHeight:3.35f;
        const string WestUpperStripWindow = "photo west high window strip";
        public static float OccupiedFloorHeight(string block,int level) => level*3.81f+
            (level==0?(block=="west_south"?SchoolWestBlockAccessPass.HighFloor:
                block=="west_north"?SchoolWestBlockAccessPass.LowFloor:
                block==SchoolNearCanteenGradePass.Block?SchoolNearCanteenGradePass.Floor:
                block=="administration_south"||block=="pedagogy_south"?.60f:0f):0f);
        public static Vector3 Point(float x,float y,float height=0) => new Vector3((x-1413)*.07055555556f,height,(1628-y)*.07055555556f);

        // Keep the plan's outer envelope while redistributing the two room banks.
        // All coordinates passed here are original registered PDF coordinates.
        public static float CorridorX(string block,int floor,float x) {
            float outerLeft,outerRight,left,right;
            if(block=="main_laboratories_north") {
                // All exterior faces/sashes remain in fixed perimeter bands.
                outerLeft=650f;outerRight=959f;
                left=floor==0?831.40f:832.36f;right=860.68f;
            } else if(block=="main_classrooms_north") {
                outerLeft=1078f;outerRight=1389f;
                left=floor==0?1177.72f:1179.34f;right=floor==0?1207.24f:1206.64f;
            } else return x;
            if(x<=outerLeft||x>=outerRight)return x;
            float center=(left+right)*.5f;
            // A 3.04m separation of wall axes leaves about 2.8m clear after
            // accounting for the CAD wall faces and their physical thickness.
            float half=3.04f/.07055555556f*.5f,newLeft=center-half,newRight=center+half;
            if(x<left)return Mathf.Lerp(outerLeft,newLeft,(x-outerLeft)/(left-outerLeft));
            if(x>right)return Mathf.Lerp(newRight,outerRight,(x-right)/(outerRight-right));
            return Mathf.Lerp(newLeft,newRight,(x-left)/(right-left));
        }
        public static float[] CorridorBox(string block,int floor,float[] box) =>
            new[]{CorridorX(block,floor,box[0]),box[1],CorridorX(block,floor,box[2]),box[3]};
        static bool HasAdaptedCorridor(string block) => block=="main_laboratories_north"||block=="main_classrooms_north";
        static void ApplyCorridorWidths() {
            foreach(var level in layout.levels) {
                foreach(var spans in new[]{level.walls,level.doors,level.windows})foreach(var span in spans) {
                    if(!HasAdaptedCorridor(span.block))continue;
                    if(span.vertical)span.p=CorridorX(span.block,level.index,span.p);
                    else {
                        span.a=CorridorX(span.block,level.index,span.a);
                        span.b=CorridorX(span.block,level.index,span.b);
                        span.width=span.b-span.a;
                    }
                }
                // Cut the actual masonry to the same width as its door, in both orientations.
                // The user clarified that the narrow upper doors are classroom doors.
                var carved=new List<Span>();
                var classroomDoors=level.doors.Where((d,i)=>d.source!=SchoolArchitectureRepairPass.RetiredDoor&&level.index==1&&UpperClassroomDoorSlots.Contains(i)).ToArray();
                foreach(var wall in level.walls) {
                    bool originalHorizontal=!wall.vertical&&HasAdaptedCorridor(wall.block);
                    var openings=level.doors.Where(d=>d.source!=SchoolArchitectureRepairPass.RetiredDoor&&d.vertical==wall.vertical&&d.block==wall.block
                        &&Mathf.Abs(d.p-wall.p)<(classroomDoors.Contains(d)?6f:3f)
                        &&(originalHorizontal||classroomDoors.Contains(d))).ToArray();
                    if(openings.Length==0){carved.Add(wall);continue;}
                    var pieces=new List<Vector2>{new Vector2(wall.a,wall.b)};
                    foreach(var door in openings) {
                        float minimum=classroomDoors.Contains(door)?UpperClassroomDoorMinimumWidth:.90f;
                        float jambGap=classroomDoors.Contains(door)?.055f:door.source==SchoolUpperEmergencyPass.SourceMetal?.065f:0f;
                        float half=(Mathf.Max(minimum,(door.b-door.a)*.07055555556f)*.5f+jambGap)/.07055555556f,mid=(door.a+door.b)*.5f;
                        var remainder=new List<Vector2>();
                        foreach(var piece in pieces) {
                            if(mid+half<=piece.x||mid-half>=piece.y){remainder.Add(piece);continue;}
                            if(mid-half>piece.x)remainder.Add(new Vector2(piece.x,mid-half));
                            if(mid+half<piece.y)remainder.Add(new Vector2(mid+half,piece.y));
                        }
                        pieces=remainder;
                    }
                    foreach(var piece in pieces)if(piece.y-piece.x>.08f)
                        carved.Add(new Span{block=wall.block,source=wall.source,vertical=wall.vertical,p=wall.p,a=piece.x,b=piece.y,thickness=wall.thickness});
                }
                level.walls=carved.ToArray();
            }
        }

        static void PrepareUpperClassroomDoors() {
            var upper=layout.levels.Single(l=>l.index==1);
            // These CAD openings sit beside perpendicular partitions. Keep the wider
            // leaf on the same wall, with 18–26cm relocation along it so its end cannot
            // enter the neighboring partition. The new aperture still covers the CAD hole.
            foreach(int index in new[]{8,9,10,11,25}) {upper.doors[index].a-=2.5f;upper.doors[index].b-=2.5f;}
            // The three east-bank partitions extend another 5–6cm into the old hole.
            foreach(int index in new[]{9,10,11}) {upper.doors[index].a-=1.25f;upper.doors[index].b-=1.25f;}
            upper.doors[23].a+=2.5f;upper.doors[23].b+=2.5f;
        }

        [MenuItem("Whispers of Past/School/Build from CEEP plans")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before building the school.");
            var old = SceneManager.GetActiveScene();
            if (old.isDirty) throw new InvalidOperationException("Save the current scene before building the school.");
            EnsureFolders();Directory.CreateDirectory(Output);
            layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Folder+"/Data/school-layout.json"));
            if(layout==null || layout.levels.Length!=2 || !Mathf.Approximately(layout.floorHeight,3.81f)) throw new InvalidDataException("Invalid school layout.");
            AddHallOpenings();
            SchoolCentralEnclosurePass.Prepare(layout);
            SchoolUpperEmergencyPass.Prepare(layout);
            PrepareUpperClassroomDoors();
            SchoolWorkshopPhotoPass.Prepare(layout);
            File.WriteAllText(Output+"/School-colored-plan-report.json",JsonUtility.ToJson(SchoolUserPlanPass.Prepare(layout),true));
            PrepareWestExteriorOpening();
            SchoolRoomRepairsPass.Prepare(layout);
            SchoolColumnRepairPass.Prepare(layout);
            SchoolArchitectureRepairPass.Prepare(layout);
            SchoolCanteenVestibulePass.Prepare(layout);
            ApplyCorridorWidths();
            SchoolArchitectureRepairPass.Carve(layout);
            PrepareWestPhotoWindows();
            SchoolCampusConnectionsPass.Prepare(layout);
            SchoolStairEnvelopePass.Prepare(layout);
            PrepareDoorWindowJambs();
            SchoolGeometryPass.Prepare(layout);
            if(File.Exists(SchoolIntegrationPass.ScenePath)) {
                Directory.CreateDirectory(Output+"/School-backups");
                File.Copy(SchoolIntegrationPass.ScenePath,Output+"/School-backups/SchoolCEEP-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            report=new Report {scene=SchoolIntegrationPass.ScenePath,status="building",metersPerPoint=layout.metersPerPoint,floorHeight=layout.floorHeight};
            groups.Clear(); provisionalDeskAreas.Clear(); root=new GameObject("CEEP — arquitetura das plantas").transform;
            CreateMaterials();
            doorPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HorrorGameplay/Prefabs/Door.prefab");
            if(!doorPrefab) throw new FileNotFoundException("Current interactive Door prefab is required.");
            BuildSite();
            foreach(var region in layout.regions) BuildFloors(region);
            foreach(var level in layout.levels) {
                foreach(var wall in level.walls) BuildWall(wall,level.index);
                foreach(var window in level.windows) BuildWindow(window,level.index);
                int i=0; foreach(var door in level.doors) BuildDoor(door,level.index,i++);
            }
            BuildCoveredWalkways();
            SchoolCirculation.Build(Group("Escadas e rampas"),concrete,metal);
            SchoolCampusConnectionsPass.ShiftEastStair(Group("Escadas e rampas"),SchoolCirculation.LastReport);
            File.WriteAllText(Output+"/School-circulation-report.json",JsonUtility.ToJson(SchoolCirculation.LastReport,true));
            // One full landing connects both south rooms and the ramp's top.
            Slab(Group("Escadas e rampas"),"Galeria superior — patamar norte da rampa",new float[]{961.36f,847.52f,1076.16f,892.16f},3.81f,.18f,concrete);
            BuildRooms();
            SchoolPhotoPass.Apply(root,Group("Arquitetura das fotos"));
            SchoolWorkshopPhotoPass.Build(Group("proposed_northeast_laboratories"));
            File.WriteAllText(Output+"/School-photo-details-report.json",JsonUtility.ToJson(SchoolPhotoDetails.Build(Group("Detalhes das fotos")),true));
            ConfigureLighting();
            File.WriteAllText(Output+"/School-central-photo-report.json",JsonUtility.ToJson(SchoolCentralPhotoPass.Apply(root,Group("Detalhes do central")),true));
            File.WriteAllText(Output+"/School-central-stair-photo-report.json",JsonUtility.ToJson(SchoolCentralStairPhotoPass.Apply(root,Group("Detalhes da escada central")),true));
            File.WriteAllText(Output+"/School-central-enclosure-report.json",JsonUtility.ToJson(SchoolCentralEnclosurePass.Apply(root,Group("Paredes da ligação central e rampa")),true));
            File.WriteAllText(Output+"/School-ramp-photo-report.json",JsonUtility.ToJson(SchoolRampPhotoPass.Apply(root,Group("Acabamento fotográfico da rampa central")),true));
            // The new lateral photos establish a local raised entrance platform,
            // separate from the plan's 3.81m occupied-storey height.
            SchoolHallGrade.Apply(root,Group("Arquitetura das fotos"),Group("Detalhes das fotos"),Group("Desníveis do hall"));
            File.WriteAllText(Output+"/School-front-photo-report.json",JsonUtility.ToJson(SchoolFrontPhotoProps.Build(Group("Detalhes das novas fotos")),true));
            File.WriteAllText(Output+"/School-canteen-connector-report.json",JsonUtility.ToJson(SchoolCanteenConnector.Build(Group("Ligação cantina e bloco")),true));
            SchoolCanteenHeightPass.Apply(root);
            File.WriteAllText(Output+"/School-refectory-photo-report.json",JsonUtility.ToJson(SchoolRefectoryPhotoPass.Build(root,Group("Detalhes do refeitório")),true));
            var pairedDoors=SchoolDoorPairPass.Apply(root,layout);
            File.WriteAllText(Output+"/School-classroom-photo-report.json",JsonUtility.ToJson(SchoolClassroomPhotoPass.Apply(root,Group("Acabamento das salas de aula"),layout),true));
            File.WriteAllText(Output+"/School-upper-emergency-report.json",JsonUtility.ToJson(SchoolUpperEmergencyPass.Apply(root),true));
            SchoolWestBlockAccessPass.Apply(root,Group(SchoolWestBlockAccessPass.GroupName));
            File.WriteAllText(Output+"/School-opposite-block-photo-report.json",JsonUtility.ToJson(SchoolOppositeBlockPhotoPass.Build(root,Group("Acabamento dos blocos opostos à quadra")),true));
            SchoolCampusConnectionsPass.Build(root,Group("Passarelas e quadra — correções das fotos"));
            SchoolCanteenVestibulePass.Apply(root);
            SchoolStairEnvelopePass.Apply(root);
            SchoolNearCanteenGradePass.Apply(root);
            SchoolArchitectureRepairPass.Apply(root,Group("Cômodos — revisão arquitetônica"));
            SchoolPlayableBoundsPass.Apply(root);
            SchoolDoorDetailPass.Apply(root);
            CleanProvisionalFurniture();
            SchoolMasonryOptimizationPass.Apply(root);
            pairedDoors=SchoolDoorPairPass.ValidateInteraction(root,pairedDoors);
            File.WriteAllText(Output+"/School-door-pairs-report.json",JsonUtility.ToJson(pairedDoors,true));
            if(!pairedDoors.passed)throw new InvalidOperationException("School double-door validation failed; inspect School-door-pairs-report.json.");
            var gameplay=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HorrorGameplay/Prefabs/GameplayRoot.prefab"),scene);
            gameplay.name="GameplayRoot";
            // Scene-owned profile preserves all pre-existing menu and office profiles.
            var atmosphere=gameplay.GetComponentInChildren<HorrorAtmosphere>(true);
            var profile=UnityEngine.Object.Instantiate(atmosphere.volume.sharedProfile); profile.name="SchoolAtmosphere";
            string profilePath=Folder+"/Materials/SchoolAtmosphere.asset";
            SaveAsset(profile,profilePath);
            atmosphere.volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            PrefabUtility.RecordPrefabInstancePropertyModifications(atmosphere.volume);
            var cam=gameplay.GetComponentInChildren<FirstPersonMotor>(true).view.playerCamera;
            cam.farClipPlane=280; cam.backgroundColor=new Color(.014f,.022f,.034f); cam.clearFlags=CameraClearFlags.SolidColor;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(cam);
            EditorSceneManager.SaveScene(scene,SchoolIntegrationPass.ScenePath);
            report.integration=SchoolIntegrationPass.Apply(gameplay,Point(1413,1588,.05f));
            File.WriteAllText(Output+"/School-horror-atmosphere-report.json",JsonUtility.ToJson(SchoolHorrorAtmospherePass.Apply(root),true));
            SchoolOpeningPass.Apply(root);
            BatchStaticGeometry();
            // Preserve the authorized third chapter on later explicit school rebuilds.
            if(AssetDatabase.LoadAssetAtPath<HorrorGame.StoryBook>("Assets/SchoolCEEP/Data/ChapterThree/ChapterThreeStory.asset"))
                SchoolChapterThreePass.Apply(root);
            if(AssetDatabase.LoadAssetAtPath<HorrorGame.StoryBook>("Assets/SchoolCEEP/Data/ChapterFour/SchoolStoryThroughChapterFour.asset"))
                SchoolChapterFourPass.Apply(root);
            if(AssetDatabase.LoadAssetAtPath<HorrorGame.StoryBook>("Assets/SchoolCEEP/Data/ChapterFive/SchoolStoryThroughChapterFive.asset"))
                SchoolChapterFivePass.Apply(root);
            report.lights=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Light>(true).Length);
            report.status=report.integration.passed?"ready":"integration failed";
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            // Reapply only the prepared assets explicitly approved for this project.
            if(File.Exists("Assets/models/mobiliario/SchoolChair_01/MeshData.json"))
                SchoolReadyAssetPass.Install();
            if(AssetDatabase.LoadAssetAtPath<HorrorGame.StoryBook>("Assets/SchoolCEEP/Data/FinalChapters/SchoolCompleteStory.asset"))
            {
                SchoolFinalChaptersPass.Apply(root);
                AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"/School-build-report.json",JsonUtility.ToJson(report,true));
            Selection.activeGameObject=gameplay.GetComponentInChildren<FirstPersonMotor>(true).gameObject;
            Debug.Log("SchoolCEEP saved. "+report.walls+" walls, "+report.doors+" doors, "+report.windows+" windows.");
        }
        static void EnsureFolders() { foreach(var path in new[]{Folder,Folder+"/Data",Folder+"/Editor",Folder+"/Scenes",Folder+"/Materials",Folder+"/Meshes"}) Directory.CreateDirectory(path); }
        static void AddHallOpenings() {
            // The photos establish the administration side windows. The pedagogy
            // window and extra swing are also visible in the native PDF vectors,
            // but were outside the extractor's strict line-pair/arc tolerance.
            var ground=layout.levels[0];
            // The 990cm dimension inside the refectory's facade was mistaken for
            // a full wall. Native PDF ticks and the new photos prove this is a dimension.
            ground.walls=ground.walls.Where(s=>!(s.block=="central_east_services"&&!s.vertical
                &&Mathf.Abs(s.p-947.12f)<.02f&&Mathf.Abs(s.a-1320.64f)<.02f&&Mathf.Abs(s.b-1463.08f)<.02f)).ToArray();
            // Original plan crops show diagonal dimension ticks at the open
            // south entrances (208cm), and a 3240cm line down the west aisle.
            // Remove only those registered dimension spans, preserving jambs
            // and every adjacent structural wall.
            ground.walls=ground.walls.Where(s=>!(
                s.block=="main_laboratories_north"&&!s.vertical&&Mathf.Abs(s.p-791.84f)<.02f
                    &&Mathf.Abs(s.a-831.64f)<.02f&&Mathf.Abs(s.b-860.68f)<.02f
                ||s.block=="main_classrooms_north"&&!s.vertical&&Mathf.Abs(s.p-795.32f)<.02f
                    &&Mathf.Abs(s.a-1177.72f)<.02f&&Mathf.Abs(s.b-1207.24f)<.02f
                ||s.block=="west_south"&&s.vertical&&Mathf.Abs(s.p-483.16f)<.02f
                    &&s.a>=894.31f&&s.b<=1304.61f)).ToArray();
            // P01 shows two open south approaches to the workshop hall. The
            // single thin facade baseline across each gap is not masonry.
            // Adjacent sash/sill spans remain unchanged.
            ground.walls=ground.walls.Where(s=>!(s.block=="proposed_northeast_laboratories"&&!s.vertical
                &&Mathf.Abs(s.p-526.88f)<.02f&&(Mathf.Abs(s.a-2222.8f)<.02f&&Mathf.Abs(s.b-2258.2f)<.02f
                    ||Mathf.Abs(s.a-2307f)<.02f&&Mathf.Abs(s.b-2342.56f)<.02f))).ToArray();
            // Native P01: 1460cm spans the open library; 12.15m and 590cm
            // are vertical dimension leaders beside the kitchen, not partitions.
            ground.walls=ground.walls.Where(s=>!(
                s.block=="proposed_library"&&!s.vertical&&Mathf.Abs(s.p-819.8f)<.02f
                    &&Mathf.Abs(s.a-1901.68f)<.02f&&Mathf.Abs(s.b-2107.24f)<.02f
                ||s.block=="central_east_services"&&s.vertical
                    &&(Mathf.Abs(s.p-1439.68f)<.02f||Mathf.Abs(s.p-1449.64f)<.02f)
                    &&s.a>=808.63f&&s.b<=960.93f)).ToArray();
            ground.windows=ground.windows.Concat(new[]{
                new Span{block="administration_south",vertical=true,p=1348.48f,a=1194.32f,b=1218.82f},
                new Span{block="administration_south",vertical=true,p=1348.48f,a=1223f,b=1247.5f},
                new Span{block="pedagogy_south",vertical=true,p=1498.12f,a=1194.32f,b=1229.84f}
            }).ToArray();
            ground.doors=ground.doors.Concat(new[]{new Span{block="pedagogy_south",vertical=true,p=1491.719f,a=1241.243f,b=1251.121f,width=9.878f}}).ToArray();
        }
        static Transform Group(string name) {
            if(groups.TryGetValue(name,out var g)) return g;
            g=new GameObject(name).transform; g.SetParent(root,false); groups[name]=g; return g;
        }
        static Material Mat(string name,Color color,float smooth=.1f,float metallic=0) {
            var path=Folder+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m) {m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",smooth); m.SetFloat("_Metallic",metallic);
            EditorUtility.SetDirty(m); return m;
        }
        static void CreateMaterials() {
            plaster=Mat("Reboco envelhecido",new Color(.56f,.55f,.49f));
            lowerPaint=Mat("Pintura verde antiga",new Color(.17f,.24f,.22f));
            concrete=Mat("Concreto",new Color(.34f,.35f,.34f));
            tile=Mat("Piso cerâmico",new Color(.40f,.42f,.39f),.17f);
            metal=Mat("Ferro oxidado",new Color(.095f,.12f,.125f),.2f,.4f);
            timber=Mat("Portas antigas",new Color(.21f,.17f,.105f));
            glass=Mat("Vidro escuro",new Color(.025f,.07f,.08f),.72f,.25f);
            glass.SetColor("_BaseColor",new Color(.08f,.14f,.15f,.20f));
            glass.SetFloat("_Surface",1);glass.SetFloat("_ZWrite",0);
            glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            glass.SetOverrideTag("RenderType","Transparent");glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;glass.SetShaderPassEnabled("ShadowCaster",false);
            roof=Mat("Cobertura",new Color(.20f,.22f,.21f));
            white=Mat("Sinalização",new Color(.70f,.69f,.58f));
            grass=Mat("Solo e grama",new Color(.12f,.18f,.12f));
            asphalt=Mat("Asfalto",new Color(.105f,.11f,.105f));
            blackboard=Mat("Quadro escolar",new Color(.055f,.085f,.065f));
            glow=Mat("Luminárias",new Color(.74f,.69f,.52f));
            glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor",new Color(1f,.82f,.57f)*1.5f);
        }
        static GameObject Cube(Transform parent,string name,Vector3 pos,Vector3 scale,Material material,bool collision=true) {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false);
            go.transform.position=pos; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=material;
            go.isStatic=true; if(!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static GameObject Slab(Transform parent,string name,float[] b,float top,float thickness,Material mat,bool collision=true) {
            var pos=Point((b[0]+b[2])/2,(b[1]+b[3])/2,top-thickness/2);
            var go=Cube(parent,name,pos,new Vector3((b[2]-b[0])*.07055556f,thickness,(b[3]-b[1])*.07055556f),mat,collision);
            if(collision) go.AddComponent<SurfaceType>().surface=mat==tile?GroundSurface.Tile:GroundSurface.Concrete;
            return go;
        }
        static void CutFloor(Transform parent,float[] box,float[] hole,float top,string name) {
            if(hole[0]>=box[2]||hole[2]<=box[0]||hole[1]>=box[3]||hole[3]<=box[1]) {Slab(parent,name,box,top,.18f,tile);return;}
            float x0=Mathf.Max(box[0],hole[0]),x1=Mathf.Min(box[2],hole[2]),y0=Mathf.Max(box[1],hole[1]),y1=Mathf.Min(box[3],hole[3]);
            foreach(var b in new[]{new[]{box[0],box[1],x0,box[3]},new[]{x1,box[1],box[2],box[3]},new[]{x0,box[1],x1,y0},new[]{x0,y1,x1,box[3]}})
                if(b[2]-b[0]>.1f&&b[3]-b[1]>.1f) Slab(parent,name,b,top,.18f,tile);
        }
        static void BuildFloors(Region region) {
            var parent=Group(region.name); var b=region.box;
            float nativeBase=NativeBaseHeight(region.name,0);
            Slab(parent,"Piso térreo",b,nativeBase,.18f,tile);
            if(region.upper) {
                var hole=region.name=="proposed_east_teaching"?SchoolCampusConnectionsPass.Box(region.name,EastStair):MainStair;
                if(region.name=="main_laboratories_north") {
                    // This south-west corner is an exterior walkway, not an upper room.
                    var notch=new[]{b[0],727f,CorridorX(region.name,1,829.48f),b[3]};
                    CutFloor(parent,b,notch,3.81f,"Primeiro pavimento");
                } else CutFloor(parent,b,hole,3.81f,"Primeiro pavimento");
                // Avoid sealing the central stair volume below its upper landing.
                if(region.name=="main_laboratories_north")CutCeiling(parent,b,new[]{b[0],727f,CorridorX(region.name,0,829.48f),b[3]},3.55f);
                else CutCeiling(parent,b,hole,3.55f);
            }
            if(region.name=="main_laboratories_north") {
                float edge=CorridorX(region.name,1,829.48f);
                Slab(parent,"Cobertura (sem acesso previsto)",new[]{b[0],b[1],b[2],727f},7.32f,.2f,roof);
                Slab(parent,"Cobertura (sem acesso previsto)",new[]{edge,727f,b[2],b[3]},7.32f,.2f,roof);
            } else if(region.name=="central_east_services") {
                Slab(parent,"Cobertura alta da cantina",new[]{b[0],b[1],1464.16f,b[3]},SchoolCanteenHeightPass.RoofTop,.2f,roof);
                Slab(parent,"Cobertura dos serviços",new[]{1464.16f,b[1],b[2],b[3]},3.52f,.2f,roof);
            } else Slab(parent,"Cobertura (sem acesso previsto)",b,(region.upper?7.32f:region.name=="west_south"?5.12f:3.52f)+nativeBase,.2f,roof);
            // Broad site lighting is reflected by architectural eaves.
            if(region.name=="covered_courtyard") {
                for(int x=0;x<3;x++)for(int z=0;z<2;z++)
                    Cube(parent,"Pilar",Point(Mathf.Lerp(b[0]+7,b[2]-7,x/2f),Mathf.Lerp(b[1]+7,b[3]-7,z),1.7f),new Vector3(.3f,3.4f,.3f),concrete);
            }
        }
        static void CutCeiling(Transform parent,float[] box,float[] hole,float top) {
            // Floors themselves are ceilings below. Only add the shallow finish outside the void.
            if(hole[0]>=box[2]||hole[2]<=box[0]||hole[1]>=box[3]||hole[3]<=box[1]) {Slab(parent,"Forro",box,top,.08f,plaster,false);return;}
            float x0=Mathf.Max(box[0],hole[0]),x1=Mathf.Min(box[2],hole[2]),y0=Mathf.Max(box[1],hole[1]),y1=Mathf.Min(box[3],hole[3]);
            foreach(var b in new[]{new[]{box[0],box[1],x0,box[3]},new[]{x1,box[1],box[2],box[3]},new[]{x0,box[1],x1,y0},new[]{x0,y1,x1,box[3]}})
                if(b[2]-b[0]>.1f&&b[3]-b[1]>.1f) Slab(parent,"Forro",b,top,.08f,plaster,false);
        }
        static void WallPart(Span s,float a,float b,float bottom,float height,Material mat,string name) {
            if(b-a<.08f||height<.02f)return;
            // The old skirting occupied the wall itself. Its coplanar faces
            // flickered into black teeth in distant corridor views; it added
            // neither a physical boundary nor a visible intentional finish.
            if(name=="Rodapé"){report.redundantTrimCollidersOmitted++;return;}
            var pos=s.vertical?Point(s.p,(a+b)/2,bottom+height/2):Point((a+b)/2,s.p,bottom+height/2);
            var size=s.vertical?new Vector3(Mathf.Max(.14f,s.thickness*.07055556f),height,(b-a)*.07055556f):new Vector3((b-a)*.07055556f,height,Mathf.Max(.14f,s.thickness*.07055556f));
            Cube(Group(s.block),name,pos,size,mat,true);
        }
        static void PrepareWestExteriorOpening() {
            // The user explicitly removed this exterior side wall, independently
            // of the CAD dimension exclusions. Occupied laboratories end before y727.
            var ground=layout.levels.Single(l=>l.index==0);
            ground.walls=ground.walls.Where(w=>!(w.block=="main_laboratories_north"&&!w.vertical
                &&Mathf.Abs(w.p-810.68f)<.02f&&Mathf.Abs(w.a-626.08f)<.02f&&Mathf.Abs(w.b-819.28f)<.02f)).ToArray();
        }
        static void PrepareWestPhotoWindows() {
            // The lower block photo shows narrow glazed strips above the yellow
            // tiles. Cut its registered facade spans at head height; door slots
            // and all capsule-height masonry remain unchanged.
            var ground=layout.levels.Single(l=>l.index==0);
            var added=new List<Span>();
            foreach(var wall in ground.walls.Where(w=>w.block=="west_north"&&w.vertical
                &&(Mathf.Abs(w.p-502.12f)<.1f||Mathf.Abs(w.p-496.36f)<.15f))) {
                float length=(wall.b-wall.a)*.07055556f;
                if(length<1.4f)continue;
                float inset=.14f/.07055556f;
                added.Add(new Span { block=wall.block,vertical=true,p=wall.p,
                    a=wall.a+inset,b=wall.b-inset,source=WestUpperStripWindow,thickness=wall.thickness });
            }
            ground.windows=ground.windows.Concat(added).ToArray();
        }
        static void PrepareDoorWindowJambs() {
            const float scale=.07055555556f, jamb=.10f;
            // Leave a full-height masonry post beside every doorway. Window
            // clipping previously stopped at the aperture itself, so a frame
            // beside a window met glass at handle height instead of masonry.
            foreach(var level in layout.levels) {
                var windows=new List<Span>();
                foreach(var window in level.windows) {
                    var pieces=new List<Vector2>{new Vector2(window.a,window.b)};
                    foreach(var door in level.doors.Where(d=>d.source!=SchoolArchitectureRepairPass.RetiredDoor
                        &&d.source!=SchoolUpperEmergencyPass.SourceOpenPassage&&d.block==window.block
                        &&d.vertical==window.vertical&&Mathf.Abs(d.p-window.p)<4f)) {
                        float mid=(door.a+door.b)*.5f,half=(DoorWidth(level,door)*.5f+.065f+jamb)/scale;
                        var remaining=new List<Vector2>();
                        foreach(var piece in pieces) {
                            if(mid+half<=piece.x||mid-half>=piece.y){remaining.Add(piece);continue;}
                            if(mid-half>piece.x)remaining.Add(new Vector2(piece.x,mid-half));
                            if(mid+half<piece.y)remaining.Add(new Vector2(mid+half,piece.y));
                        }
                        pieces=remaining;
                    }
                    foreach(var piece in pieces.Where(p=>(p.y-p.x)*scale>=.70f))
                        windows.Add(new Span{block=window.block,source=window.source,vertical=window.vertical,
                            provisional=window.provisional,p=window.p,a=piece.x,b=piece.y,
                            thickness=window.thickness,width=piece.y-piece.x});
                }
                level.windows=windows.ToArray();
            }
        }
        static void BuildWall(Span s,int floor) {
            var holes=layout.levels[floor].windows.Where(w=>w.block==s.block&&w.vertical==s.vertical&&Mathf.Abs(w.p-s.p)<4&&w.b>s.a&&w.a<s.b).OrderBy(w=>w.a).ToArray();
            float cursor=s.a,baseY=NativeBaseHeight(s.block,floor);
            foreach(var w in holes) {
                float a=Mathf.Max(cursor,w.a),b=Mathf.Min(s.b,w.b);
                if(b<=a)continue;
                PaintWall(s,cursor,a,baseY);
                bool highStrip=w.source==WestUpperStripWindow;
                if(highStrip)PaintWall(s,a,b,baseY,2.35f);
                else WallPart(s,a,b,baseY,1.05f,lowerPaint,"Peitoril");
                float head=highStrip?3.08f:2.58f;
                WallPart(s,a,b,baseY+head,WallHeight(s.block)-head,plaster,"Verga da janela");
                cursor=b;
            }
            PaintWall(s,cursor,s.b,baseY); report.walls++;
        }
        static void PaintWall(Span s,float a,float b,float y,float height=3.35f) {
            if(Mathf.Approximately(height,3.35f))height=WallHeight(s.block);
            WallPart(s,a,b,y,1.1f,lowerPaint,"Parede — pintura inferior");
            WallPart(s,a,b,y+1.1f,height-1.1f,plaster,"Parede — reboco");
            WallPart(s,a,b,y+.01f,.09f,metal,"Rodapé");
        }
        static void BuildWindow(Span s,int floor) {
            if((s.b-s.a)*.07055556f<.7f)return;
            float y=NativeBaseHeight(s.block,floor);
            var parent=Group(s.block);
            bool strip=s.source==WestUpperStripWindow;
            float centerHeight=strip?2.715f:1.81f,halfHeight=strip?.365f:.77f;
            var center=s.vertical?Point(s.p,(s.a+s.b)/2,y+centerHeight):Point((s.a+s.b)/2,s.p,y+centerHeight);
            float span=(s.b-s.a)*.07055556f;
            var size=s.vertical?new Vector3(.04f,strip?.70f:1.45f,span):new Vector3(span,strip?.70f:1.45f,.04f);
            Cube(parent,"Janela — vidro opaco provisório",center,size,glass);
            for(int i=0;i<2;i++) {
                var p=center+Vector3.up*(i==0?-halfHeight:halfHeight);
                Cube(parent,"Caixilho",p,s.vertical?new Vector3(.10f,.07f,span+.08f):new Vector3(span+.08f,.07f,.10f),metal,false);
            }
            int panels=Mathf.Max(2,Mathf.CeilToInt(span/.75f));
            for(int i=0;i<=panels;i++) {
                float v=Mathf.Lerp(s.a,s.b,(float)i/panels);
                var p=s.vertical?Point(s.p,v,y+centerHeight):Point(v,s.p,y+centerHeight);
                Cube(parent,"Divisão do caixilho",p,new Vector3(.06f,strip?.76f:1.55f,.06f),metal,false);
            }
            report.windows++;
        }
        public static bool IsPairedDoor(Level level,Span s) => !s.provisional&&s.source!=null&&s.source.StartsWith("quarter-swing CAD curve")
            &&level.doors.Any(d=>d!=s&&!d.provisional&&d.source!=null&&d.source.StartsWith("quarter-swing CAD curve")
                &&d.block==s.block&&d.vertical==s.vertical&&Mathf.Abs(d.p-s.p)<.05f
                &&(Mathf.Abs(d.a-s.b)<.05f||Mathf.Abs(d.b-s.a)<.05f));
        public static float DoorWidth(Level level,Span s)
        {
            float native=(s.b-s.a)*.07055556f;
            int index=Array.IndexOf(level.doors,s);
            return IsPairedDoor(level,s)?native:Mathf.Max(level.index==1&&UpperClassroomDoorSlots.Contains(index)?UpperClassroomDoorMinimumWidth:.90f,native);
        }
        static void BuildDoor(Span s,int floor,int index) {
            if(s.source==SchoolArchitectureRepairPass.RetiredDoor)return;
            // Keep the plan record and every following persistent ID while leaving the confirmed ground passage open.
            if(floor==0&&s.source==SchoolUpperEmergencyPass.SourceOpenPassage)return;
            bool paired=IsPairedDoor(layout.levels[floor],s);
            float width=DoorWidth(layout.levels[floor],s),y=NativeBaseHeight(s.block,floor);
            float start=(s.a+s.b)/2-width/.07055556f/2;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(doorPrefab); go.name="Porta "+floor+"-"+index;
            go.transform.SetParent(Group(s.block),false);
            go.transform.position=s.vertical?Point(s.p,start,y):Point(start,s.p,y);
            go.transform.rotation=Quaternion.Euler(0,s.vertical?90:0,0); // +local X maps PDF's +Y.
            // Small free-edge clearance for the ramp landing's raised toe guard during the ground leaf sweep.
            float leafWidth=width-(s.source==SchoolCentralEnclosurePass.Source&&floor==0?.01f:0f);
            float doorHeight=s.source==SchoolCentralEnclosurePass.Source&&floor==1?SchoolCentralEnclosurePass.UpperDoorHeightMeters:2.28f;
            go.transform.localScale=new Vector3(leafWidth/1.2f,doorHeight/2.1f,1);
            var door=go.GetComponent<InteractiveDoor>(); door.persistentId="school.door."+floor+"."+index;door.locked=false;
            // Clear adjacent room approaches at ground level and the upper ramp rails.
            if(s.source==SchoolCentralEnclosurePass.Source)door.openAngle=floor==0?95f:-95f;
            if(s.source==SchoolUpperEmergencyPass.SourceMetal)door.openAngle=-95f;
            if(floor==1&&UpperClassroomDoorSlots.Contains(index))door.openAngle=90f;
            if(floor==1&&s.block=="main_auditorium_south"&&index==23)door.openAngle=-95f;
            // The wider eastern classroom leaves must open into their rooms;
            // their old westward arc occupied the narrow east teaching corridor.
            if(floor==1&&s.block=="proposed_east_teaching"&&s.vertical&&Mathf.Abs(s.p-2418f)<.1f)
                door.openAngle=-90f;
            if(paired&&floor==0&&s.block=="west_south"&&!s.vertical&&Mathf.Abs(s.p-892.82f)<.02f)
                door.openAngle=-95f;
            // The eastern bank of this narrow west aisle opens into its rooms;
            // opening the provisional leaves across the aisle sealed the route.
            if(floor==0&&s.block=="west_south"&&s.provisional&&s.vertical&&Mathf.Abs(s.p-490.6f)<.1f)
                door.openAngle=-95f;
            if(s.source==SchoolArchitectureRepairPass.Source&&s.vertical)
            {
                if(s.block=="west_south")door.openAngle=s.p>482.9f?-95f:95f;
                else if(s.block=="main_laboratories_north")door.openAngle=s.p>CorridorX(s.block,floor,846)?-95f:95f;
                else if(s.block=="proposed_east_teaching")door.openAngle=s.p>2400?-95f:95f;
            }
            if(s.vertical&&(s.block=="main_laboratories_north"||s.block=="main_classrooms_north"))
            {
                float center=CorridorX(s.block,floor,s.block=="main_laboratories_north"?846:1193);
                if(Mathf.Abs(Point(s.p,0).x-Point(center,0).x)<2.1f)door.openAngle=s.p>center?-95f:95f;
            }
            // This administration door otherwise swings across the neighboring
            // door 0-23's approach. Match the CAD position rather than array order.
            if(floor==0&&s.block=="administration_south"&&!s.vertical&&Mathf.Abs(s.p-1251.961f)<.1f&&Mathf.Abs((s.a+s.b)*.5f-1199.538f)<.1f)
                door.openAngle=-95f;
            // The northern classroom entrance opens outward, keeping the
            // first classroom's side-door approach clear inside the corridor.
            if(floor==0&&s.block=="main_classrooms_north"&&!s.vertical&&Mathf.Abs(s.p-337.22f)<.1f&&Mathf.Abs((s.a+s.b)*.5f-CorridorX(s.block,floor,1192.475f))<.1f)
                door.openAngle=-95f;
            door.displayName="porta"; door.playerMask=1<<9;
            foreach(var renderer in go.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial=renderer.name.Contains("Handle")?metal:timber;
            var handle=go.transform.Find("Handle");if(handle&&handle.GetComponent<Collider>())UnityEngine.Object.DestroyImmediate(handle.GetComponent<Collider>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(door);
            var over=new Span{block=s.block,vertical=s.vertical,p=s.p,thickness=2.5f};
            if(floor==0&&s.block=="west_south") {
                float end=start+width/.07055556f;
                WallPart(over,start-.065f/.07055556f,end+.065f/.07055556f,y+2.96f,WestHighWallHeight-2.96f,plaster,"Verga da porta");
                var center=s.vertical?Point(s.p,(start+end)*.5f,y+2.64f):Point((start+end)*.5f,s.p,y+2.64f);
                Cube(Group(s.block),"Bandeira de vidro — bloco alto",center,s.vertical?new Vector3(.045f,.64f,width+.13f):new Vector3(width+.13f,.64f,.045f),glass);
                for(int side=0;side<2;side++) {
                    Cube(Group(s.block),"Caixilho da bandeira — bloco alto",center+Vector3.up*(side==0?-.32f:.32f),
                        s.vertical?new Vector3(.08f,.06f,width+.06f):new Vector3(width+.06f,.06f,.08f),metal,false);
                    var post=s.vertical?Point(s.p,side==0?start:end,y+2.64f):Point(side==0?start:end,s.p,y+2.64f);
                    Cube(Group(s.block),"Caixilho da bandeira — bloco alto",post,new Vector3(.06f,.64f,.06f),metal,false);
                }
            } else WallPart(over,start-.065f/.07055556f,start+(width+.065f)/.07055556f,y+doorHeight+.02f,3.35f-doorHeight-.02f,plaster,"Verga da porta");
            report.doors++;
        }
        static void BuildSite() {
            var g=Group("Terreno e acessos");
            GroundSlab(g,"Terreno",new[]{210f,130f,3090f,1680f},-.08f,.3f,grass);
            GroundSlab(g,"Circulação externa",new[]{261f,175f,3037f,1628f},-.01f,.12f,concrete);
            Slab(g,"Rua — fundo visual",new[]{210f,1628f,3090f,1780f},-.14f,.13f,asphalt);
            var p=Group("Muro do colégio");
            Boundary(p,261,175,3037,175);Boundary(p,261,175,261,1628);Boundary(p,3037,175,3037,1628);
            Boundary(p,261,1628,1374,1628);Boundary(p,1452,1628,3037,1628);
            for(int i=0;i<2;i++) Cube(p,"Pilar do portão",Point(i==0?1374:1452,1628,1.45f),new Vector3(.4f,2.9f,.4f),plaster);
            Slab(g,"Estacionamento oeste",new[]{351f,1375f,1348f,1614f},.015f,.09f,asphalt);
            Slab(g,"Estacionamento leste",new[]{1495f,1367f,2023f,1614f},.015f,.09f,asphalt);
            for(int row=0;row<2;row++)for(int i=0;i<21;i++) {
                float x=Mathf.Lerp(370,1318,i/20f),py=row==0?1400:1580;
                Slab(g,"Marca de vaga",new[]{x,py,x+1.2f,py+65f},.064f,.003f,white,false);
            }
            foreach(var b in new[]{new[]{1740f,558f,2044f,723f},new[]{1728f,1064f,2168f,1340f},new[]{651f,1100f,985f,1340f},new[]{2670f,568f,2960f,740f}}) {
                Slab(g,"Jardim",b,.025f,.08f,grass);
                for(int i=0;i<3;i++)Tree(Point(Mathf.Lerp(b[0]+26,b[2]-26,i/2f),(b[1]+b[3])/2),g);
            }
            BuildCourt();
            Slab(g,"Pátio descoberto",new[]{2244f,1207f,2519f,1546f},.02f,.12f,tile);
        }
        public static void GroundSlab(Transform parent,string name,float[] box,float top,float thickness,Material mat) {
            var pieces=new List<float[]>{box};
            foreach(var hole in SchoolNearCanteenGradePass.GroundHoles) {
                var next=new List<float[]>();
                foreach(var b in pieces) {
                    float x0=Mathf.Max(b[0],hole[0]),x1=Mathf.Min(b[2],hole[2]),y0=Mathf.Max(b[1],hole[1]),y1=Mathf.Min(b[3],hole[3]);
                    if(x1<=x0||y1<=y0){next.Add(b);continue;}
                    foreach(var p in new[]{new[]{b[0],b[1],x0,b[3]},new[]{x1,b[1],b[2],b[3]},new[]{x0,b[1],x1,y0},new[]{x0,y1,x1,b[3]}})
                        if(p[2]-p[0]>.01f&&p[3]-p[1]>.01f)next.Add(p);
                }
                pieces=next;
            }
            foreach(var p in pieces)Slab(parent,name,p,top,thickness,mat);
        }
        static void Boundary(Transform parent,float x,float y,float xx,float yy) {
            var a=Point(x,y);var b=Point(xx,yy);var go=Cube(parent,"Muro",(a+b)/2+Vector3.up, new Vector3(.22f,2,(b-a).magnitude),plaster);
            go.transform.rotation=Quaternion.LookRotation(b-a);
        }
        static void Tree(Vector3 pos,Transform parent) {
            var trunk=GameObject.CreatePrimitive(PrimitiveType.Cylinder);trunk.name="Árvore — volume provisório";trunk.transform.SetParent(parent,false);trunk.transform.position=pos+Vector3.up*2;
            trunk.transform.localScale=new Vector3(.45f,2,.45f);trunk.GetComponent<Renderer>().sharedMaterial=timber;trunk.isStatic=true;
            var crown=GameObject.CreatePrimitive(PrimitiveType.Sphere);crown.name="Copa provisória";crown.transform.SetParent(parent,false);crown.transform.position=pos+Vector3.up*4.9f;crown.transform.localScale=new Vector3(4,3.5f,4);crown.GetComponent<Renderer>().sharedMaterial=grass;UnityEngine.Object.DestroyImmediate(crown.GetComponent<Collider>());crown.isStatic=true;
        }
        static void BuildCourt() {
            var g=Group("Quadra");var center=Point(2766.88f,1382.26f); const float w=24.1f,d=30.4f;
            Cube(g,"Quadra — dimensões anotadas 24,10 × 30,40m",center+Vector3.up*.035f,new Vector3(w,.08f,d),lowerPaint).AddComponent<SurfaceType>().surface=GroundSurface.Concrete;
            for(int i=0;i<2;i++) {
                float z=center.z+(i==0?-d/2+.65f:d/2-.65f);
                Cube(g,"Linha de fundo",new Vector3(center.x,.079f,z),new Vector3(w-1,.005f,.06f),white,false);
                float x=center.x+(i==0?-w/2+.55f:w/2-.55f);
                Cube(g,"Linha lateral",new Vector3(x,.079f,center.z),new Vector3(.06f,.005f,d-1.3f),white,false);
                var goal=Cube(g,"Trave",new Vector3(center.x,.045f,z),Vector3.one,metal,false);UnityEngine.Object.DestroyImmediate(goal.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(goal.GetComponent<MeshFilter>());
                for(int j=0;j<2;j++) Cube(goal.transform,"Poste",new Vector3(center.x+(j==0?-1.5f:1.5f),1.06f,z),new Vector3(.07f,2.04f,.07f),white);
                Cube(goal.transform,"Travessão",new Vector3(center.x,2.05f,z),new Vector3(3.07f,.07f,.07f),white);
            }
            Cube(g,"Linha central",center+Vector3.up*.079f,new Vector3(w-1,.005f,.06f),white,false);
            // A mesh fence conveys the court's perimeter without blocking the western entrance.
            for(int i=0;i<2;i++) {
                float x=center.x+(i==0?-w/2:w/2);
                for(int j=0;j<=10;j++) Cube(g,"Poste da grade",new Vector3(x,1.5f,center.z-d/2+d*j/10),new Vector3(.06f,3,.06f),metal);
                for(int j=0;j<9;j++) {
                    if(i==0&&j<5)continue;
                    Cube(g,"Grade horizontal",new Vector3(x,.32f+j*.32f,center.z),new Vector3(.025f,.025f,d),metal,false);
                }
            }
        }
        static void Covered(Transform g,float[] b) {
            Slab(g,"Passagem coberta",b,.035f,.12f,tile);
            Slab(g,"Telhado da passarela",b,3.48f,.14f,roof);
            bool longX=b[2]-b[0]>b[3]-b[1];float length=(longX?b[2]-b[0]:b[3]-b[1])*.07055556f;
            int n=Mathf.Max(1,Mathf.CeilToInt(length/6));
            for(int i=0;i<=n;i++)for(int side=0;side<2;side++) {
                float x=longX?Mathf.Lerp(b[0]+3,b[2]-3,(float)i/n):(side==0?b[0]+3:b[2]-3);
                float y=longX?(side==0?b[1]+3:b[3]-3):Mathf.Lerp(b[1]+3,b[3]-3,(float)i/n);
                Cube(g,"Pilar da passarela",Point(x,y,1.68f),new Vector3(.18f,3.3f,.18f),concrete);
            }
            for(int i=0;i<n;i++) Lamp(g,longX?Point(Mathf.Lerp(b[0],b[2],(i+.5f)/n),(b[1]+b[3])/2,3.18f):Point((b[0]+b[2])/2,Mathf.Lerp(b[1],b[3],(i+.5f)/n),3.18f),.8f,9,true);
        }
        static void BuildCoveredWalkways() {
            var g=Group("Passarelas cobertas");
            Covered(g,new[]{1388f,1343f,1438f,1628f});
            Covered(g,new[]{1366f,1174.76f,1454f,1343f});
            Covered(g,new[]{1455f,746f,2645f,775f});
            Covered(g,new[]{961f,809.24f,1076f,851.12f});
            Covered(g,new[]{1399.56f,774f,1455f,809f});
        }
        static void BuildRooms() {
            var data=JsonUtility.FromJson<Rooms>(File.ReadAllText(Folder+"/Data/rooms.json"));
            SchoolArchitectureRepairPass.AdaptRoomCatalog(data);
            SchoolCampusConnectionsPass.AdaptRooms(data);
            foreach(var room in data.rooms) {
                report.rooms++;var b=room.box;float y=room.level*3.81f;
                float cx=(b[0]+b[2])*.5f,cy=(b[1]+b[3])*.5f;
                var occupiedRegion=layout.regions.FirstOrDefault(r=>cx>=r.box[0]&&cx<=r.box[2]&&cy>=r.box[1]&&cy<=r.box[3]);
                if(occupiedRegion!=null)y=NativeBaseHeight(occupiedRegion.name,room.level);
                var corridorRegion=layout.regions.FirstOrDefault(r=>HasAdaptedCorridor(r.name)&&cx>=r.box[0]&&cx<=r.box[2]&&cy>=r.box[1]&&cy<=r.box[3]);
                if(corridorRegion!=null)b=CorridorBox(corridorRegion.name,room.level,b);
                var g=Group("Mobiliário inicial — "+room.level);
                var center=Point((b[0]+b[2])/2,(b[1]+b[3])/2,y);
                bool westHigh=room.level==0&&cx>368&&cx<598&&cy>844&&cy<1356;
                Lamp(g,center+Vector3.up*(westHigh?4.785f:3.15f),1.4f,11,true);
                if(room.kind=="class"||room.kind=="lab") {
                    float x=room.kind=="lab"?b[0]+22:b[0]+20;
                    // Keep the perimeter and the central walking line clear.
                    for(int row=0;row<2;row++)for(int column=0;column<2;column++) {
                        var pos=Point(x+column*22,b[1]+30+row*25,y);
                        if(pos.x>Point(b[2]-16,b[1]).x||pos.z<Point(b[0],b[3]-16).z)continue;
                        if(BlocksDoorApproach(pos,new Vector3(room.kind=="lab"?1.30f:.78f,1,room.kind=="lab"?.70f:1.25f)))continue;
                        Desk(g,pos,room.kind=="lab",b);
                    }
                    Cube(g,"Quadro negro",Point((b[0]+b[2])/2,b[1]+6,y+1.65f),new Vector3(Mathf.Min(3,(b[2]-b[0])*.05f),1.05f,.08f),blackboard);
                }
            }
            var library=Group("Biblioteca — mobiliário inicial");
            for(int i=0;i<5;i++) {
                var pos=Point(1920+i*31,SchoolCampusConnectionsPass.Y("proposed_library",937),1.0f);
                Cube(library,"Estante",pos,new Vector3(1.2f,2,.38f),timber);
                for(int j=0;j<5;j++)Cube(library,"Prateleira",pos+new Vector3(0,-.8f+j*.4f,-.22f),new Vector3(1.15f,.04f,.42f),metal,false);
            }
        }
        // Reserve standing and turning space on both sides of real apertures.
        // Furniture is provisional; it must never obstruct the plan's circulation.
        static bool BlocksDoorApproach(Vector3 pos,Vector3 size) {
            var bounds=new Bounds(new Vector3(pos.x,0,pos.z),new Vector3(size.x+.84f,2,size.z+.84f));
            foreach(var door in root.GetComponentsInChildren<InteractiveDoor>(true)) {
                if(!door.leafCollider)continue;
                var leaf=door.leafCollider;
                var center=leaf.transform.TransformPoint(leaf.center);
                float baseY=center.y-Mathf.Abs(leaf.size.y*leaf.transform.lossyScale.y)*.5f;
                if(Mathf.Abs(baseY-pos.y)>.1f)continue;
                center.y=0;
                var normal=leaf.transform.forward;normal.y=0;normal.Normalize();
                if(bounds.IntersectRay(new Ray(center-normal*2,normal),out float distance)&&distance<=4)return true;
            }
            return false;
        }
        static void Desk(Transform g,Vector3 pos,bool lab,float[] roomBox) {
            var set=new GameObject("Conjunto — bancada inicial").transform;set.SetParent(g,false);set.position=pos;
            var a=Point(roomBox[0],roomBox[1]);var b=Point(roomBox[2],roomBox[3]);
            provisionalDeskAreas.Add(set,new Bounds((a+b)*.5f,new Vector3(b.x-a.x,0,a.z-b.z)));
            g=set;
            Cube(g,lab?"Bancada de laboratório":"Carteira",pos+Vector3.up*.76f,new Vector3(lab?1.30f:.78f,.07f,lab?.7f:.55f),timber);
            for(int i=0;i<2;i++)for(int j=0;j<2;j++) Cube(g,"Pé da bancada",pos+new Vector3((i==0?-.28f:.28f),.36f,(j==0?-.20f:.20f)),new Vector3(.035f,.72f,.035f),metal,false);
            if(!lab) {
                Cube(g,"Assento",pos+new Vector3(0,.42f,-.60f),new Vector3(.44f,.045f,.42f),timber);
                Cube(g,"Encosto",pos+new Vector3(0,.69f,-.81f),new Vector3(.44f,.42f,.045f),timber);
                for(int i=0;i<2;i++) Cube(g,"Pé da cadeira",pos+new Vector3(i==0?-.18f:.18f,.20f,-.6f),new Vector3(.035f,.40f,.34f),metal,false);
            }
        }
        // Coarse room boxes must not place a bench inside a real partition.
        // Move the complete provisional set together; classroom photo furniture has its own validated layout.
        static void CleanProvisionalFurniture() {
            Physics.SyncTransforms();
            var physical=root.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy).ToArray();
            var obstacles=physical.Where(c=>c.name.StartsWith("Parede")||c.name=="Peitoril"||c.name.StartsWith("Verga")
                ||c.name.StartsWith("Janela")||c.name=="Bancada de laboratório"||c.name=="Carteira"||c.name=="Assento"
                ||c.name=="Encosto"||c.name=="Estante"||c.name.StartsWith("Sala — cadeira")||c.name.StartsWith("Sala — carteira")).ToArray();
            var offsets=(from x in new[]{0f,.3f,-.3f,.6f,-.6f,.9f,-.9f,1.2f,-1.2f,1.5f,-1.5f}
                from z in new[]{0f,.3f,-.3f,.6f,-.6f,.9f,-.9f,1.2f,-1.2f,1.5f,-1.5f}
                select new Vector3(x,0,z)).OrderBy(p=>p.sqrMagnitude).ToArray();
            foreach(var entry in provisionalDeskAreas) {
                var set=entry.Key;if(!set)continue;
                var bodies=set.GetComponentsInChildren<BoxCollider>().Where(c=>c.enabled).ToArray();
                if(bodies.Length==0)continue;
                bool Clear()=>!bodies.Any(c=>obstacles.Any(w=>w&&!w.transform.IsChildOf(set)&&c.bounds.Intersects(w.bounds)
                    &&Physics.ComputePenetration(c,c.transform.position,c.transform.rotation,w,w.transform.position,w.transform.rotation,out _,out float depth)&&depth>.001f));
                if(Clear())continue;
                var original=set.position;bool found=false;
                foreach(var offset in offsets.Skip(1)) {
                    set.position=original+offset;Physics.SyncTransforms();
                    var b=bodies[0].bounds;var room=entry.Value;
                    if(b.min.x<room.min.x+.06f||b.max.x>room.max.x-.06f||b.min.z<room.min.z+.06f||b.max.z>room.max.z-.06f)continue;
                    if(BlocksDoorApproach(set.position,new Vector3(b.size.x,1,b.size.z)))continue;
                    if(Clear()){found=true;report.provisionalFurnitureRelocated++;break;}
                }
                if(!found){UnityEngine.Object.DestroyImmediate(set.gameObject);report.provisionalFurnitureOmitted++;}
            }
            Physics.SyncTransforms();
        }
        static void Lamp(Transform parent,Vector3 pos,float intensity,float range,bool fixture) {
            if(fixture) Cube(parent,"Luminária fluorescente",pos+Vector3.up*.1f,new Vector3(1.1f,.07f,.17f),glow,false);
            var go=new GameObject("Luz de ambiente");go.transform.SetParent(parent,false);go.transform.position=pos-Vector3.up*.15f;
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.range=range;light.intensity=intensity;light.color=new Color(.84f,.86f,.74f);light.shadows=LightShadows.None; report.lights++;
        }
        static void ConfigureLighting() {
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.14f,.18f,.24f);RenderSettings.ambientEquatorColor=new Color(.07f,.095f,.10f);RenderSettings.ambientGroundColor=new Color(.035f,.05f,.04f);
            RenderSettings.skybox=null;RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.007f;RenderSettings.fogColor=new Color(.027f,.045f,.055f);
            var go=new GameObject("Luar");go.transform.SetParent(root,false);go.transform.rotation=Quaternion.Euler(48,-35,0);
            var moon=go.AddComponent<Light>();moon.type=LightType.Directional;moon.color=new Color(.49f,.64f,.79f);moon.intensity=.55f;moon.shadows=LightShadows.Soft;report.lights++;
            var g=Group("Iluminação externa");
            foreach(var xy in new[]{new[]{1370f,1510f},new[]{1515f,1370f},new[]{1730f,985f},new[]{2128f,720f},new[]{2552f,1190f},new[]{2960f,1510f},new[]{665f,1338f}}) {
                Cube(g,"Poste",Point(xy[0],xy[1],2.8f),new Vector3(.10f,5.6f,.10f),metal);
                Lamp(g,Point(xy[0],xy[1],5.5f),8f,19,true);
            }
            // Unoccupied roof-only blocks retain ground-level lighting. Corridor light is deliberately subdued.
            foreach(var region in layout.regions) {
                var b=region.box;
                if(region.name=="main_laboratories_north"||region.name=="main_classrooms_north") {
                    float x=region.name=="main_laboratories_north"?846:1193;
                    for(int floor=0;floor<2;floor++)for(int i=0;i<5;i++) Lamp(Group(region.name),Point(CorridorX(region.name,floor,x),375+i*88,floor*3.81f+3.13f),.85f,8,true);
                } else if(region.name=="covered_courtyard")
                    Lamp(Group(region.name),Point(1421,1068.92f,3.12f),2.1f,17,true);
                else if(region.name.Contains("administration")||region.name.Contains("pedagogy")||region.name.Contains("services"))
                    Lamp(Group(region.name),Point((b[0]+b[2])/2,(b[1]+b[3])/2,3.12f),2.1f,17,true);
            }
        }
        static void SaveAsset(UnityEngine.Object asset,string path) {
            var existing=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if(existing) {EditorUtility.CopySerialized(asset,existing);UnityEngine.Object.DestroyImmediate(asset);EditorUtility.SetDirty(existing);}
            else AssetDatabase.CreateAsset(asset,path);
        }
        static void BatchStaticGeometry() {
            // Colliders remain individual, while static surfaces become one draw per material and group.
            // Keep prior mesh assets intact so scene backups retain their original geometry.
            meshRevision=Folder+"/Meshes/Build-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(meshRevision);
            int index=0;
            foreach(var parent in groups.Values) {
                // TextMeshPro owns its generated mesh and required renderer.
                // Combining those glyphs would destroy components it depends on.
                // Face-specific photo finishes use several submeshes. The single-material
                // batch would drop those faces and their colors; keep those renderers intact.
                var filters=parent.GetComponentsInChildren<MeshFilter>().Where(f=>f.gameObject.isStatic&&f.sharedMesh&&f.GetComponent<MeshRenderer>()&&f.GetComponent<MeshRenderer>().enabled&&f.GetComponent<MeshRenderer>().sharedMaterials.Length==1&&!f.GetComponent<TMPro.TMP_Text>()).ToArray();
                foreach(var set in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial)) {
                    if(!set.Key||set.Count()<2)continue;
                    var mesh=new Mesh {name="School static surfaces "+index,indexFormat=IndexFormat.UInt32};
                    mesh.CombineMeshes(set.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=parent.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
                    string path=meshRevision+"/School-"+index+++".asset";SaveAsset(mesh,path);
                    var go=new GameObject("Superfícies agrupadas — "+set.Key.name);go.transform.SetParent(parent,false);go.isStatic=true;
                    go.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);go.AddComponent<MeshRenderer>().sharedMaterial=set.Key;
                    foreach(var f in set) {UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);}
                    report.staticRenderers++;
                }
            }
        }
    }
}
