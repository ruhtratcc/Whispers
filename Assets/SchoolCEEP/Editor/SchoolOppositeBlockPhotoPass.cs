// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>Finishes for the two western blocks. Run after their access/grade pass and before static batching.</summary>
    public static class SchoolOppositeBlockPhotoPass
    {
        public const int SourceRevision = 5;
        public const string HighBlock = "west_south", LowBlock = "west_north";
        const float Scale = .07055555556f;
        const string Materials = "Assets/SchoolCEEP/Materials/";
        const string Meshes = "Assets/SchoolCEEP/Meshes/PhotoWestBlocks/";
        static readonly HashSet<string> Masonry = new HashSet<string>(new[] {
            "Parede — pintura inferior", "Parede — reboco", "Peitoril", "Verga da janela", "Verga da porta" });
        static Material cream, white, yellow, brown, wood, darkDoor, steel, opal, lightFloor, concrete;
        static Transform parent;
        static Report report;
        static int meshNumber;
        static string meshBuildFolder;

        [Serializable] public sealed class BlockResult
        {
            public string block, association, circulation;
            public float nativeFloorTopMeters, nativeRoofUndersideMeters, ceilingFinishUndersideMeters;
            public float corridorCenterPdfX, wallFinishTopAboveFloorMeters, tileSizeMeters;
            public int wallPanels, corridorFaceTriangles, floorSurfaces, floorJointLines;
            public int ceilingPanels, beams, wallFixtures, nativeWindows, nativeWindowFrames, nativeDoorLeaves;
            public int columns, benches, gardenGroups;
        }
        [Serializable] public sealed class Report
        {
            public int sourceRevision = SourceRevision;
            public bool dimensionsEstimated = true, probablePlanAssociation = true;
            public bool originalCollidersPreserved, doorIdsAndInteractionsPreserved, doorPosesPreserved;
            public bool existingClassroomStandardPreserved = true, noAdditionalColliders, existingLightComponentsPreserved;
            public int nativeCollidersBefore, nativeCollidersAfter, nativeDoorLeavesBefore, nativeDoorLeavesAfter;
            public int decorativeRenderers, materialAssets, meshAssets, lightComponentsBefore, lightComponentsAfter;
            public string meshAssetFolder;
            public BlockResult high, low;
            public string[] notes;
        }
        sealed class ColliderState
        {
            public Collider collider; public bool enabled, trigger; public Vector3 position, scale; public Quaternion rotation;
            public Vector3 boxCenter, boxSize; public Mesh mesh;
            public ColliderState(Collider c)
            {
                collider=c; enabled=c.enabled; trigger=c.isTrigger; position=c.transform.position;
                scale=c.transform.lossyScale; rotation=c.transform.rotation;
                if(c is BoxCollider b){boxCenter=b.center;boxSize=b.size;}
                if(c is MeshCollider m)mesh=m.sharedMesh;
            }
            public bool Preserved()
            {
                if(!collider||collider.enabled!=enabled||collider.isTrigger!=trigger
                    ||Vector3.Distance(collider.transform.position,position)>.00001f
                    ||Vector3.Distance(collider.transform.lossyScale,scale)>.00001f
                    ||Quaternion.Angle(collider.transform.rotation,rotation)>.0001f)return false;
                if(collider is BoxCollider b&&(b.center!=boxCenter||b.size!=boxSize))return false;
                return !(collider is MeshCollider m)||m.sharedMesh==mesh;
            }
        }
        sealed class DoorState
        {
            public InteractiveDoor door; public string id; public Vector3 position, scale; public Quaternion rotation;
            public BoxCollider leaf; public float angle; public bool locked;
            public DoorState(InteractiveDoor d)
            {door=d;id=d.persistentId;position=d.transform.position;rotation=d.transform.rotation;
                scale=d.transform.lossyScale;leaf=d.leafCollider;angle=d.openAngle;locked=d.locked;}
            public bool IdentityPreserved()=>door&&door.persistentId==id&&door.leafCollider==leaf&&door.openAngle==angle&&door.locked==locked;
            public bool PosePreserved()=>door&&Vector3.Distance(door.transform.position,position)<.00001f
                &&Vector3.Distance(door.transform.lossyScale,scale)<.00001f&&Quaternion.Angle(door.transform.rotation,rotation)<.0001f;
        }

        public static Report Build(Transform architecture, Transform details)
        {
            if(!architecture||!details)throw new ArgumentNullException("Architecture and western photo detail group are required.");
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Build western block finishes outside Play mode.");
            var high=architecture.Find(HighBlock);var low=architecture.Find(LowBlock);
            if(!high||!low)throw new InvalidOperationException("Both registered western blocks must exist.");
            var colliders=architecture.GetComponentsInChildren<Collider>(true).Select(c=>new ColliderState(c)).ToArray();
            var doors=architecture.GetComponentsInChildren<InteractiveDoor>(true).Select(d=>new DoorState(d)).ToArray();
            parent=details;meshNumber=0;
            meshBuildFolder=Meshes+"Build-r"+SourceRevision+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+"/";
            report=new Report {nativeCollidersBefore=colliders.Length,nativeDoorLeavesBefore=doors.Length,
                lightComponentsBefore=architecture.GetComponentsInChildren<Light>(true).Length,
                meshAssetFolder=meshBuildFolder,
                notes=new[] {
                    "User identifies the two new interior photographs as the high block and the yellow-tiled external corridor as the lower block. The west_south/high and west_north/low association follows the registered western access and remains a plan interpretation.",
                    "This pass reads each native floor and roof after the separate west access/grade pass. It never translates blocks, native doors, floors, roofs, colliders or furniture.",
                    "Cream/white high corridor walls, a pale 30cm floor grid, dark brown native doors, wooden ceiling panels between white crossbeams and small oval wall fixtures follow the high-block photos.",
                    "The lower external corridor receives yellow 15cm tiles on existing masonry, brown/cream columns, wooden soffit, a narrow bench beside the wall and planting outside the walking strip.",
                    "Masonry finishes replace only triangles on the actual corridor-facing native wall surfaces. Door/window gaps and room-facing triangles retain their geometry and existing materials. No painted glass or new opening is invented on solid lintels.",
                    "Native upper-strip windows in the lower block and glazed door transoms in the high block are refined only after the builder has created their real apertures. This finish pass creates no new opening or glazed cover on solid masonry.",
                    "The first two white/blue facade photographs remain unassigned to these two western blocks. No principal-building facade, central ramp, standard classroom, light source, gameplay or door interaction is changed.",
                    "All new details are non-colliding. Sizes and finish heights are photographic estimates; original collider geometry, door identifiers and poses are checked after the pass." } };
            PrepareMaterials();
            report.high=FinishBlock(high,true);report.low=FinishBlock(low,false);
            report.nativeCollidersAfter=architecture.GetComponentsInChildren<Collider>(true).Length;
            report.nativeDoorLeavesAfter=architecture.GetComponentsInChildren<InteractiveDoor>(true).Length;
            report.originalCollidersPreserved=report.nativeCollidersAfter==colliders.Length&&colliders.All(c=>c.Preserved());
            report.doorIdsAndInteractionsPreserved=report.nativeDoorLeavesAfter==doors.Length&&doors.All(d=>d.IdentityPreserved());
            report.doorPosesPreserved=doors.All(d=>d.PosePreserved());
            report.noAdditionalColliders=details.GetComponentsInChildren<Collider>(true).Length==0;
            report.lightComponentsAfter=architecture.GetComponentsInChildren<Light>(true).Length;
            report.existingLightComponentsPreserved=report.lightComponentsBefore==report.lightComponentsAfter;
            if(!report.originalCollidersPreserved||!report.doorIdsAndInteractionsPreserved||!report.doorPosesPreserved
                ||!report.noAdditionalColliders||!report.existingLightComponentsPreserved)
                throw new InvalidOperationException("Western finish pass changed native physical or interaction state.");
            return report;
        }

        static BlockResult FinishBlock(Transform block,bool high)
        {
            float floorTop=NativeFloorTop(block),roofBottom=NativeRoofBottom(block);
            var result=new BlockResult {block=block.name,association="probable plan mapping, user-confirmed photo level",
                circulation=high?"internal corridor, rooms on both sides":"external corridor along one room bank",
                nativeFloorTopMeters=floorTop,nativeRoofUndersideMeters=roofBottom,
                ceilingFinishUndersideMeters=roofBottom-.052f,corridorCenterPdfX=high?482.9f:531.16f,
                wallFinishTopAboveFloorMeters=high?1.25f:2.15f,tileSizeMeters=high?.30f:.15f};
            if(roofBottom-floorTop<2.65f)throw new InvalidOperationException("Western corridor roof leaves insufficient height for the decorative ceiling.");
            foreach(var renderer in block.GetComponentsInChildren<MeshRenderer>(true).ToArray())
            {
                if(renderer.name=="Piso térreo")
                {renderer.sharedMaterial=high?lightFloor:concrete;if(high)WorldUV(renderer.GetComponent<MeshFilter>(),.30f,.30f,"PaleFloor");result.floorSurfaces++;}
                if(Masonry.Contains(renderer.name)&&IsCorridorMasonry(renderer,high))
                {
                    int triangles=RepaintNativeCorridorFace(renderer,high,floorTop,result.wallFinishTopAboveFloorMeters);
                    if(triangles>0){result.wallPanels++;result.corridorFaceTriangles+=triangles;}
                }
                bool nativeFrame=renderer.name=="Caixilho"||renderer.name=="Divisão do caixilho";
                bool highTransomFrame=high&&renderer.name=="Caixilho da bandeira — bloco alto";
                if((nativeFrame&&IsAtCorridor(renderer.bounds,high))||highTransomFrame)
                {renderer.sharedMaterial=high?steel:brown;result.nativeWindowFrames++;}
            }
            result.nativeWindows=block.GetComponentsInChildren<MeshRenderer>(true).Count(r=>
                r.name.StartsWith("Janela —")&&IsAtCorridor(r.bounds,high)||high&&r.name=="Bandeira de vidro — bloco alto");
            foreach(var door in block.GetComponentsInChildren<InteractiveDoor>(true))
            {
                if(!door.leafCollider)continue;
                var renderer=door.leafCollider.GetComponent<MeshRenderer>();if(renderer)renderer.sharedMaterial=darkDoor;
                foreach(var metal in door.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.Contains("Handle")))metal.sharedMaterial=steel;
                result.nativeDoorLeaves++;
            }
            if(high)HighFloorGrid(floorTop,result);
            Ceiling(high,floorTop,roofBottom,result);
            if(high)WallFixtures(block,floorTop,result);else LowColumnsBenchGarden(block,floorTop,roofBottom,result);
            return result;
        }
        static float NativeFloorTop(Transform block)
        {
            var floor=block.GetComponentsInChildren<BoxCollider>(true).SingleOrDefault(c=>c.name=="Piso térreo");
            if(!floor)throw new InvalidOperationException("Missing native ground floor for "+block.name);
            return floor.transform.TransformPoint(floor.center).y+Mathf.Abs(floor.size.y*floor.transform.lossyScale.y)*.5f;
        }
        static float NativeRoofBottom(Transform block)
        {
            var roof=block.GetComponentsInChildren<BoxCollider>(true).SingleOrDefault(c=>c.name=="Cobertura (sem acesso previsto)");
            if(!roof)throw new InvalidOperationException("Missing native roof for "+block.name);
            return roof.transform.TransformPoint(roof.center).y-Mathf.Abs(roof.size.y*roof.transform.lossyScale.y)*.5f;
        }
        static bool IsAtCorridor(Bounds b,bool high)
        {
            float x=1413+b.center.x/Scale,y=1628-b.center.z/Scale;
            return high?y>=844&&y<=1366&&(Mathf.Abs(x-SchoolArchitectureRepairPass.WestLeft)<1.7f||Mathf.Abs(x-SchoolArchitectureRepairPass.WestRight)<1.7f)
                :y>=268&&y<=744&&Mathf.Abs(x-502.12f)<1.7f;
        }
        static bool IsCorridorMasonry(MeshRenderer renderer,bool high)
        {var b=renderer.bounds;return b.size.x<b.size.z&&IsAtCorridor(b,high);}

        // Split the native visible wall triangles at the finish band. Positions and silhouette remain identical.
        static int RepaintNativeCorridorFace(MeshRenderer renderer,bool high,float floorTop,float bandHeight)
        {
            var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)return 0;
            var source=filter.sharedMesh;var oldMaterials=renderer.sharedMaterials;
            int originalCount=source.subMeshCount,lowerIndex=originalCount,upperIndex=originalCount+1;
            var lists=Enumerable.Range(0,originalCount+2).Select(i=>new List<int>()).ToArray();
            var verts=new List<Vector3>();var normals=new List<Vector3>();var uvs=new List<Vector2>();
            Vector3[] sv=source.vertices,sn=source.normals;Vector2[] su=source.uv;
            float x=1413+renderer.bounds.center.x/Scale;
            Vector3 inward=high?(x<482.9f?Vector3.right:Vector3.left):Vector3.right;
            float split=floorTop+bandHeight;int selected=0;
            for(int sub=0;sub<originalCount;sub++)
            {
                var tris=source.GetTriangles(sub);
                for(int t=0;t<tris.Length;t+=3)
                {
                    var triangle=new List<Vertex>(3);
                    for(int v=0;v<3;v++)
                    {int i=tris[t+v];triangle.Add(new Vertex{position=sv[i],normal=sn.Length==sv.Length?sn[i]:Vector3.up,uv=su.Length==sv.Length?su[i]:Vector2.zero});}
                    Vector3 worldNormal=renderer.transform.TransformDirection(triangle[0].normal).normalized;
                    // The narrow exposed jamb returns belong to this same finish.
                    // Keep only the opposite, room-facing surface in its original material.
                    bool inwardFace=Vector3.Dot(worldNormal,inward)>=.98f;
                    bool jambReturn=Mathf.Abs(worldNormal.z)>=.98f;
                    if(!inwardFace&&!jambReturn)
                    {Emit(triangle,sub,verts,normals,uvs,lists);continue;}
                    selected++;
                    foreach(var v in triangle)
                    {Vector3 world=renderer.transform.TransformPoint(v.position);v.uv=new Vector2(world.z/(high?.30f:.15f),world.y/(high?.30f:.15f));}
                    Emit(Clip(triangle,renderer.transform,split,true),lowerIndex,verts,normals,uvs,lists);
                    Emit(Clip(triangle,renderer.transform,split,false),upperIndex,verts,normals,uvs,lists);
                }
            }
            if(selected==0)return 0;
            var mesh=new Mesh{name="Western block native corridor face finish"};
            mesh.SetVertices(verts);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.subMeshCount=lists.Length;
            for(int i=0;i<lists.Length;i++)mesh.SetTriangles(lists[i],i);mesh.RecalculateBounds();
            filter.sharedMesh=SaveMesh(mesh,"NativeFace-"+(meshNumber++).ToString("D4"));
            var mats=new Material[lists.Length];
            for(int i=0;i<originalCount;i++)mats[i]=oldMaterials[Mathf.Min(i,oldMaterials.Length-1)];
            mats[lowerIndex]=high?cream:yellow;mats[upperIndex]=white;renderer.sharedMaterials=mats;
            return selected;
        }
        sealed class Vertex {public Vector3 position,normal;public Vector2 uv;}
        static List<Vertex> Clip(List<Vertex> polygon,Transform transform,float split,bool below)
        {
            var result=new List<Vertex>();if(polygon.Count==0)return result;
            for(int i=0;i<polygon.Count;i++)
            {
                var a=polygon[i];var b=polygon[(i+1)%polygon.Count];
                float ay=transform.TransformPoint(a.position).y-split,by=transform.TransformPoint(b.position).y-split;
                bool ai=below?ay<=.000001f:ay>=-.000001f,bi=below?by<=.000001f:by>=-.000001f;
                if(ai)result.Add(a);
                if(ai!=bi)
                {float f=ay/(ay-by);result.Add(new Vertex{position=Vector3.Lerp(a.position,b.position,f),normal=Vector3.Lerp(a.normal,b.normal,f).normalized,uv=Vector2.Lerp(a.uv,b.uv,f)});}
            }
            return result;
        }
        static void Emit(List<Vertex> polygon,int sub,List<Vector3> verts,List<Vector3> normals,List<Vector2> uvs,List<int>[] lists)
        {
            if(polygon.Count<3)return;
            for(int i=1;i<polygon.Count-1;i++)foreach(var v in new[]{polygon[0],polygon[i],polygon[i+1]})
            {lists[sub].Add(verts.Count);verts.Add(v.position);normals.Add(v.normal);uvs.Add(v.uv);}
        }
        static void HighFloorGrid(float top,BlockResult result)
        {
            // A mipmapped material carries the 30cm joints. Thin coplanar cubes
            // previously flickered into black dashes at oblique camera angles.
            result.floorJointLines=0;
        }
        static void Ceiling(bool high,float floorTop,float roofBottom,BlockResult result)
        {
            float x0=high?SchoolArchitectureRepairPass.WestLeft:502.12f,x1=high?SchoolArchitectureRepairPass.WestRight:554.8f,y0=high?860:278,y1=high?1355:740;
            float run=(y1-y0)*Scale;int bays=Mathf.Max(1,Mathf.CeilToInt(run/3.70f));
            for(int i=0;i<bays;i++)
            {
                float a=Mathf.Lerp(y0,y1,(float)i/bays),b=Mathf.Lerp(y0,y1,(float)(i+1)/bays);
                var panel=Cube(high?"Oeste alto — forro de madeira":"Oeste baixo — beiral com forro de madeira",
                    P((x0+x1)/2,(a+b)/2,roofBottom-.027f),new Vector3((x1-x0)*Scale,.05f,(b-a)*Scale-.025f),wood);
                WorldUV(panel.GetComponent<MeshFilter>(),.12f,1.8f,"Ceiling-"+high+"-"+i);result.ceilingPanels++;
            }
            for(int i=0;i<=bays;i++)
            {
                float y=Mathf.Lerp(y0,y1,(float)i/bays);
                Cube(high?"Oeste alto — travessa branca":"Oeste baixo — travessa creme",P((x0+x1)/2,y,roofBottom-.105f),new Vector3((x1-x0)*Scale,.16f,.14f),high?white:cream);result.beams++;
            }
        }
        static void WallFixtures(Transform block,float floorTop,BlockResult result)
        {
            var faces=block.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Parede — reboco"&&IsCorridorMasonry(r,true)&&r.bounds.size.z>1.0f)
                .OrderBy(r=>r.bounds.center.z).ToArray();
            foreach(var renderer in faces.Where((r,i)=>i%3==0))
            {
                var b=renderer.bounds;bool west=b.center.x<P(482.9f,0).x;
                float side=west?1:-1;var center=new Vector3(west?b.max.x+.042f:b.min.x-.042f,floorTop+2.60f,b.center.z);
                Sphere("Oeste alto — base da luminária oval",center,new Vector3(.055f,.30f,.19f),steel);
                Sphere("Oeste alto — luminária oval opalina",center+Vector3.right*side*.023f,new Vector3(.065f,.255f,.155f),opal);
                result.wallFixtures++;
            }
        }
        static void LowColumnsBenchGarden(Transform block,float floorTop,float roofBottom,BlockResult result)
        {
            foreach(float y in new[]{280f,421.67f,571.33f,721f})
            {
                var point=P(502.12f,y,floorTop+1.6f);
                var support=block.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r=>Masonry.Contains(r.name)
                    &&IsCorridorMasonry(r,false)&&r.bounds.min.z<=point.z&&r.bounds.max.z>=point.z);
                if(!support)continue;
                float height=roofBottom-floorTop-.055f;
                Cube("Oeste baixo — acabamento de pilar castanho",new Vector3(support.bounds.max.x+.014f,floorTop+height/2,point.z),new Vector3(.025f,height,.19f),brown);
                result.columns++;
            }
            // The bench fits between the masonry and the registered walking strip x516.28..546.04.
            Vector3 seat=P(509.5f,472,floorTop+.45f);
            for(int i=0;i<3;i++)Cube("Oeste baixo — ripa do banco",seat+Vector3.right*(-.115f+i*.115f),new Vector3(.09f,.045f,1.45f),wood);
            foreach(float z in new[]{-.55f,.55f})Cube("Oeste baixo — apoio do banco",seat+new Vector3(0,-.22f,z),new Vector3(.29f,.40f,.045f),steel);
            result.benches=1;
            foreach(float y in new[]{333f,474f,623f})
            {
                // Garden lies east of the lower block, beyond both the slab edge and pedestrian strip.
                Vector3 c=P(566,y,floorTop+.16f);
                Cube("Oeste baixo — borda do canteiro",c,new Vector3(.75f,.12f,2.7f),brown);
                Cube("Oeste baixo — terra do canteiro",c+Vector3.up*.06f,new Vector3(.69f,.04f,2.6f),Mat("PhotoWestGardenSoil",new Color(.18f,.14f,.08f)));
                var green=Mat("PhotoWestGardenLeaf",new Color(.17f,.26f,.09f));
                for(int i=0;i<5;i++)Sphere("Oeste baixo — folhagem do jardim",c+new Vector3((i%2==0?-.12f:.12f),.23f,-1+i*.49f),new Vector3(.47f,.52f,.59f),green);
                result.gardenGroups++;
            }
        }
        static void PrepareMaterials()
        {
            cream=Mat("PhotoWestCreamWall",new Color(.70f,.64f,.44f));white=Mat("PhotoWestWhiteWall",new Color(.80f,.79f,.73f));
            yellow=Mat("PhotoWestYellowTiles",Color.white,.25f);TileTexture(yellow,"PhotoWestYellowTileGrid",new Color(.77f,.54f,.13f));
            brown=Mat("PhotoWestBrownColumn",new Color(.37f,.32f,.24f));wood=Mat("PhotoWestWoodCeiling",Color.white,.14f);
            WoodTexture(wood);darkDoor=Mat("PhotoWestDarkBrownDoor",new Color(.105f,.065f,.035f),.18f);
            steel=Mat("PhotoWestFixtureSteel",new Color(.12f,.115f,.10f),.28f,.35f);
            opal=Mat("PhotoWestOvalOpal",new Color(.83f,.85f,.78f));opal.EnableKeyword("_EMISSION");opal.SetColor("_EmissionColor",new Color(.80f,.85f,.78f)*1.1f);
            EditorUtility.SetDirty(opal);lightFloor=Mat("PhotoWestPaleFloor",Color.white,.17f);TileTexture(lightFloor,"PhotoWestPaleFloorGrid",new Color(.65f,.63f,.56f));
            concrete=Mat("PhotoWestLowerConcrete",new Color(.44f,.435f,.39f),.12f);
        }
        static void TileTexture(Material material,string name,Color color)
        {
            var tex=Texture(name,(x,y)=>x<2||y<2?new Color(.25f,.23f,.17f):color+Color.white*(Mathf.PerlinNoise(x*.22f,y*.22f)*.02f-.01f));
            material.SetTexture("_BaseMap",tex);material.SetTextureScale("_BaseMap",Vector2.one);EditorUtility.SetDirty(material);
        }
        static void WoodTexture(Material material)
        {
            var tex=Texture("PhotoWestWoodSlatGrain",(x,y)=>x<3?new Color(.075f,.055f,.025f):
                new Color(.32f,.205f,.115f)+new Color(1f,.7f,.4f)*(Mathf.PerlinNoise(x*.15f,y*.027f)*.07f-.03f));
            material.SetTexture("_BaseMap",tex);material.SetTextureScale("_BaseMap",Vector2.one);EditorUtility.SetDirty(material);
        }
        static Texture2D Texture(string name,Func<int,int,Color> pixel)
        {
            string path=Materials+name+".asset";var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(!tex){tex=new Texture2D(128,128,TextureFormat.RGB24,true){name=name,wrapMode=TextureWrapMode.Repeat};AssetDatabase.CreateAsset(tex,path);}
            var colors=new Color[128*128];for(int y=0;y<128;y++)for(int x=0;x<128;x++)colors[y*128+x]=pixel(x,y);
            tex.SetPixels(colors);tex.Apply();EditorUtility.SetDirty(tex);return tex;
        }
        static Material Mat(string name,Color color,float smooth=.08f,float metallic=0)
        {
            string path=Materials+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);report.materialAssets++;}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metallic);EditorUtility.SetDirty(m);return m;
        }
        static void WorldUV(MeshFilter filter,float across,float along,string key)
        {
            var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);var v=mesh.vertices;var uv=new Vector2[v.Length];
            for(int i=0;i<v.Length;i++){Vector3 p=filter.transform.TransformPoint(v[i]);uv[i]=new Vector2(p.x/across,p.z/along);}
            mesh.uv=uv;filter.sharedMesh=SaveMesh(mesh,key);
        }
        static Mesh SaveMesh(Mesh mesh,string key)
        {
            if(!AssetDatabase.IsValidFolder(Meshes.TrimEnd('/')))AssetDatabase.CreateFolder("Assets/SchoolCEEP/Meshes","PhotoWestBlocks");
            // Mesh topology changes between finish revisions. Give each build its
            // own assets so an existing Mesh cannot retain earlier render buffers
            // after CopySerialized, and saved scenes keep their original meshes.
            if(!AssetDatabase.IsValidFolder(meshBuildFolder.TrimEnd('/')))
                AssetDatabase.CreateFolder(Meshes.TrimEnd('/'),meshBuildFolder.TrimEnd('/').Substring(Meshes.Length));
            string path=meshBuildFolder+key+".asset";
            AssetDatabase.CreateAsset(mesh,path);report.meshAssets++;return mesh;
        }
        static Vector3 P(float x,float y,float h=0)=>SchoolMapBuilder.Point(x,y,h);
        static GameObject Cube(string name,Vector3 position,Vector3 size,Material material)=>Primitive(PrimitiveType.Cube,name,position,size,material);
        static GameObject Sphere(string name,Vector3 position,Vector3 size,Material material)=>Primitive(PrimitiveType.Sphere,name,position,size,material);
        static GameObject Primitive(PrimitiveType type,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.isStatic=true;report.decorativeRenderers++;return go;
        }
    }
}
