// Ferramenta da escola de validação.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using HorrorGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    /// <summary>Bounded whole-map Editor physics audit. Never changes permanent geometry or runs gameplay.</summary>
    public static class SchoolWholeMapAudit
    {
        [Serializable] public sealed class Target
        {
            public string name, block, kind, detail;
            public int level;
            public bool passed;
            public Vector3[] points = Array.Empty<Vector3>();
        }
        [Serializable] public sealed class Joint
        {
            public string block, detail;
            public int level;
            public float gapMeters;
            public Vector3 endpoint, intersection;
        }
        [Serializable] public sealed class Report
        {
            public bool passed, editorPhysicsOnly = true, gameplayExecuted, checkpointTouched;
            public bool temporaryDoorPosesRestored;
            public string scene, scope;
            public int blocks, occupiedUpperBlocks, namedRoomBoxes, doorLeaves, physicalDoorOpenings, doorInteriorSides, doorNetworkRoutes, explicitLinks;
            public int deliberatelyInaccessibleRooms;
            public int plannedRoutes, routesWithNativePath, inaccessibleTargets, wallJointSuspects, nativeGridQueries;
            public Target[] targets;
            public Joint[] joints;
            public SchoolValidationPass.ValidationReport physicalValidation;
            public string[] notes;
        }
        struct DoorPoint { public string name, block; public Vector3 foot, normal; public int level; }
        sealed class Context
        {
            public Scene scene;
            public Transform player;
            public CharacterController capsule;
            public Dictionary<string, Grid> grids = new Dictionary<string, Grid>();
            public int queries;
            public bool Clear(Vector3 expected, out Vector3 grounded)
            {
                queries++;
                grounded = expected;
                var hits = Physics.RaycastAll(expected + Vector3.up * .45f, Vector3.down, 1.1f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => Relevant(h.collider) && h.normal.y > .25f && Mathf.Abs(h.point.y - expected.y) <= .28f)
                    .OrderBy(h => h.distance).ToArray();
                if (hits.Length == 0 || Vector3.Angle(hits[0].normal, Vector3.up) > capsule.slopeLimit + 1f) return false;
                grounded.y = hits[0].point.y + .02f;
                float r = Mathf.Max(.01f, capsule.radius - .015f);
                Vector3 lower = grounded + Vector3.up * (r + .04f);
                Vector3 upper = grounded + Vector3.up * (capsule.height - r);
                return !Physics.OverlapCapsule(lower, upper, r, ~0, QueryTriggerInteraction.Ignore).Any(Relevant);
            }
            bool Relevant(Collider c) => c && c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy
                && c.gameObject.scene == scene && !c.transform.IsChildOf(player);
            public bool Line(Vector3 start, Vector3 end, out Vector3[] points)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start, end) / .20f));
                var list = new List<Vector3>();
                for (int i = 0; i <= n; i++)
                {
                    if (!Clear(Vector3.Lerp(start, end, i / (float)n), out Vector3 p)) { points = null; return false; }
                    list.Add(p);
                }
                points = list.ToArray(); return true;
            }
            public string Failure(Vector3 expected)
            {
                float r=Mathf.Max(.01f,capsule.radius-.015f);
                var blockers=Physics.OverlapCapsule(expected+Vector3.up*(r+.04f),expected+Vector3.up*(capsule.height-r),r,~0,QueryTriggerInteraction.Ignore)
                    .Where(Relevant).Select(c=>c.transform.parent?c.transform.parent.name+"/"+c.name:c.name).Distinct().Take(8).ToArray();
                if(blockers.Length>0)return "Endpoint capsule meets "+string.Join(", ",blockers)+".";
                return "Endpoint support/clearance or bounded connection is unavailable at "+expected+".";
            }
            public bool Route(string key, float[] box, int level, float elevation, Vector3 start, Vector3 end, out Vector3[] points)
            {
                if (Line(start, end, out points)) return true;
                foreach (Vector3 elbow in new[] { new Vector3(start.x, elevation, end.z), new Vector3(end.x, elevation, start.z) })
                    if (Line(start, elbow, out var a) && Line(elbow, end, out var b)) { points = a.Concat(b.Skip(1)).ToArray(); return true; }
                // A room footprint, widened cross-block link and exterior perimeter use distinct bounds.
                // Reusing the first room grid for a later south exit silently truncated legitimate goals.
                string gridKey=key+":"+elevation.ToString("R",CultureInfo.InvariantCulture)+":"
                    +string.Join(",",box.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)));
                if (!grids.TryGetValue(gridKey, out Grid grid))
                    grids[gridKey] = grid = new Grid(this, box, elevation);
                if(grid.Find(start,end,out points))return true;
                // A 0.2m sampling grid may miss the centre strip of a narrow,
                // valid doorway. Recheck failed routes at 0.1m before reporting
                // geometry as blocked; both paths still undergo the same native
                // standing-capsule traversal validation.
                string fineKey=gridKey+":fine";
                if(!grids.TryGetValue(fineKey,out Grid fine))
                    grids[fineKey]=fine=new Grid(this,box,elevation,.10f);
                return fine.Find(start,end,out points);
            }
        }
        // This grid is limited to one building footprint or an explicitly bounded link.
        // Lazy evaluation avoids scanning the entire terrain, and a fixed node budget prevents unbounded work.
        sealed class Grid
        {
            readonly Context context;
            readonly Vector3 origin;
            readonly int width, height;
            readonly float elevation;
            readonly float Step;
            readonly Dictionary<int, Vector3?> cells = new Dictionary<int, Vector3?>();
            public Grid(Context c, float[] box, float y, float step=.20f)
            {
                context=c; elevation=y; Step=step;
                var a = SchoolMapBuilder.Point(box[0]-8, box[3]+8, y);
                var b = SchoolMapBuilder.Point(box[2]+8, box[1]-8, y);
                origin=a;
                width=Mathf.Clamp(Mathf.CeilToInt((b.x-a.x)/Step)+1,1,1200);
                height=Mathf.Clamp(Mathf.CeilToInt((b.z-a.z)/Step)+1,1,1200);
            }
            bool Cell(int index, out Vector3 value)
            {
                if (!cells.TryGetValue(index, out Vector3? stored))
                {
                    Vector3 p=origin+new Vector3(index%width*Step,0,index/width*Step);
                    p.y=elevation;
                    stored=context.Clear(p,out var ground)?ground:(Vector3?)null;
                    cells[index]=stored;
                }
                value=stored??default;return stored.HasValue;
            }
            int Near(Vector3 target)
            {
                int x=Mathf.RoundToInt((target.x-origin.x)/Step), z=Mathf.RoundToInt((target.z-origin.z)/Step);
                var candidates=new List<KeyValuePair<float,int>>();
                for(int dz=-3;dz<=3;dz++)for(int dx=-3;dx<=3;dx++)
                {
                    int nx=x+dx,nz=z+dz;if(nx<0||nx>=width||nz<0||nz>=height)continue;
                    int id=nz*width+nx;
                    if(Cell(id,out Vector3 point)&&context.Line(target,point,out _))
                        candidates.Add(new KeyValuePair<float,int>((point-target).sqrMagnitude,id));
                }
                return candidates.Count==0?-1:candidates.OrderBy(c=>c.Key).First().Value;
            }
            public bool Find(Vector3 start, Vector3 end, out Vector3[] points)
            {
                points=null;int from=Near(start),to=Near(end);if(from<0||to<0)return false;
                var previous=new Dictionary<int,int>{{from,-1}};
                var distance=new Dictionary<int,float>{{from,0}};
                var open=new SortedSet<Tuple<float,int,int>>(Comparer<Tuple<float,int,int>>.Create((a,b)=>{
                    int order=a.Item1.CompareTo(b.Item1);if(order!=0)return order;return a.Item3.CompareTo(b.Item3);}));
                int serial=0;open.Add(Tuple.Create(0f,from,serial++));int iterations=0;
                var closed=new HashSet<int>();
                int budget=Mathf.Min(width*height,180000);
                while(open.Count>0&&iterations++<budget)
                {
                    var top=open.Min;open.Remove(top);int current=top.Item2;if(current==to)break;
                    if(!closed.Add(current)){iterations--;continue;}
                    int x=current%width,z=current/width;
                    Cell(current,out Vector3 currentPoint);
                    foreach(var offset in new[]{new Vector2Int(-1,0),new Vector2Int(1,0),new Vector2Int(0,-1),new Vector2Int(0,1)})
                    {
                        int nx=x+offset.x,nz=z+offset.y;if(nx<0||nx>=width||nz<0||nz>=height)continue;
                        int next=nz*width+nx;if(!Cell(next,out var nextPoint))continue;
                        if(closed.Contains(next))continue;
                        float cost=distance[current]+Step;
                        if(distance.TryGetValue(next,out float old)&&old<=cost)continue;
                        // Adjacent cell support/clearance is cached; the final route is sampled and moved by the real controller below.
                        distance[next]=cost;previous[next]=current;
                        float heuristic=Mathf.Abs(nx-to%width)*Step+Mathf.Abs(nz-to/width)*Step;
                        open.Add(Tuple.Create(cost+heuristic,next,serial++));
                    }
                }
                if(!previous.ContainsKey(to))return false;
                var path=new List<Vector3>{end};int cursor=to;
                while(cursor>=0){Cell(cursor,out Vector3 p);path.Add(p);cursor=previous[cursor];}
                path.Add(start);path.Reverse();points=path.ToArray();return true;
            }
        }

        public static Report Audit()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Run whole-map audit outside Play.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=SchoolIntegrationPass.ScenePath)throw new InvalidOperationException("Open SchoolCEEP before auditing.");
            var roots=scene.GetRootGameObjects();
            var player=roots.SelectMany(r=>r.GetComponentsInChildren<FirstPersonMotor>(true)).Single();
            var capsule=player.GetComponent<CharacterController>();
            var doors=roots.SelectMany(r=>r.GetComponentsInChildren<InteractiveDoor>(true)).ToArray();
            string folder=SchoolMapBuilder.Folder;
            var layout=JsonUtility.FromJson<SchoolMapBuilder.Layout>(File.ReadAllText(folder+"/Data/school-layout.json"));
            // Compare masonry with the same photo-confirmed facade replacements
            // as the builder; the automotive upper opening is intentional.
            SchoolWorkshopPhotoPass.Prepare(layout);
            SchoolCentralEnclosurePass.Prepare(layout);
            SchoolUserPlanPass.Prepare(layout);
            SchoolCampusConnectionsPass.Prepare(layout);
            var rooms=JsonUtility.FromJson<SchoolMapBuilder.Rooms>(File.ReadAllText(folder+"/Data/rooms.json"));
            SchoolArchitectureRepairPass.AdaptRoomCatalog(rooms);
            SchoolCampusConnectionsPass.AdaptRooms(rooms);
            var report=new Report{scene=scene.path,scope="All building/site regions, occupied upper blocks, redesigned named rooms, both sides of all doors and explicit circulation links.",
                blocks=layout.regions.Length,occupiedUpperBlocks=layout.regions.Count(r=>r.upper),namedRoomBoxes=rooms.rooms.Length,doorLeaves=doors.Length};
            var targets=new List<Target>();var paths=new List<SchoolValidationPass.TraversalPath>();
            var probes=new List<SchoolValidationPass.WalkProbe>();
            var saved=doors.Where(d=>d.hinge).Select(d=>new KeyValuePair<Transform,Quaternion>(d.hinge,d.hinge.localRotation)).ToArray();
            var gate=roots.SelectMany(r=>r.GetComponentsInChildren<InteractiveDrawer>(true)).SingleOrDefault(d=>d.persistentId==SchoolWorkshopPhotoPass.GateId);
            var gateFields=typeof(InteractiveDrawer).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly);
            var gateValues=gate?gateFields.Select(f=>f.GetValue(gate)).ToArray():null;
            Vector3 gatePose=gate&&gate.drawer?gate.drawer.localPosition:Vector3.zero;
            var doorPoints=new List<DoorPoint>();
            var context=new Context{scene=scene,player=player.transform,capsule=capsule};
            try
            {
                // Read the saved closed aperture before applying temporary open poses.
                var seenDoors=new HashSet<InteractiveDoor>();
                foreach(var door in doors)
                {
                    if(!door.hinge||!door.leafCollider||seenDoors.Contains(door))continue;
                    seenDoors.Add(door);
                    var leaf=door.leafCollider;float h=Mathf.Abs(leaf.size.y*leaf.transform.lossyScale.y);
                    Vector3 foot=leaf.transform.TransformPoint(leaf.center)-Vector3.up*(h*.5f-.05f);
                    Vector3 normal=leaf.transform.forward;normal.y=0;normal.Normalize();
                    string openingName=door.name;
                    if(door.pairedLeaf&&door.pairedLeaf.hinge&&door.pairedLeaf.leafCollider)
                    {
                        var pair=door.pairedLeaf;seenDoors.Add(pair);
                        // The two opposite outer jambs define the common usable aperture, regardless of leaf widths.
                        Vector3 common=(door.hinge.position+pair.hinge.position)*.5f;
                        foot.x=common.x;foot.z=common.z;openingName+=" + "+pair.name;
                    }
                    doorPoints.Add(new DoorPoint{name=openingName,block=door.transform.parent.name,foot=foot,normal=normal,level=foot.y>2?1:0});
                }
                report.physicalDoorOpenings=doorPoints.Count;
                foreach(var door in doors)if(door.hinge)door.hinge.localRotation*=Quaternion.Euler(0,door.openAngle,0);
                if(gate&&gate.drawer){if(!gate.IsOpen)gate.Interact(null);gate.RestoreState(gate.CaptureState());}
                Physics.SyncTransforms();
                foreach(var dp in doorPoints)
                {
                    for(int side=-1;side<=1;side+=2)
                    {
                        report.doorInteriorSides++;
                        Vector3 start=dp.foot+dp.normal*(side*.62f),end=dp.foot+dp.normal*(side*1.8f);
                        var region=layout.regions.FirstOrDefault(r=>r.name==dp.block);
                        if(region==null)continue;
                        // Short vestibules can be shallower than 1.8m. A clear
                        // endpoint beyond the next partition is not an approach
                        // inside this vestibule; require a supported route first.
                        bool approachFound=false;Vector3[] approach=null;
                        foreach(float distance in new[]{1.8f,1.5f,1.2f,.95f,.8f,.7f})
                        {
                            if(!context.Clear(dp.foot+dp.normal*(side*distance),out end))continue;
                            if(context.Route(dp.block+"-"+dp.level,region.box,dp.level,dp.foot.y,start,end,out approach))
                            {approachFound=true;break;}
                        }
                        string approachName="Door interior approach "+dp.name+" side "+side;
                        targets.Add(new Target{name=approachName,block=dp.block,level=dp.level,kind="door-side",passed=approachFound,
                            detail=approachFound?"Supported approach inside the adjacent room/vestibule; aperture separately crosses in both directions.":context.Failure(start),
                            points=approach??new[]{start,end}});
                        if(approachFound)paths.Add(new SchoolValidationPass.TraversalPath{name=approachName,footPositions=approach});
                    }
                }
                int roomIndex=0;
                foreach(var room in rooms.rooms)
                {
                    if(SchoolPlayableBoundsPass.IsDeliberatelyInaccessible(room.name)){report.deliberatelyInaccessibleRooms++;continue;}
                    var b=room.box;float cx=(b[0]+b[2])*.5f,cy=(b[1]+b[3])*.5f;
                    var region=layout.regions.FirstOrDefault(r=>cx>=r.box[0]&&cx<=r.box[2]&&cy>=r.box[1]&&cy<=r.box[3]);
                    if(region==null){targets.Add(new Target{name=room.name,kind="room",level=room.level,detail="Room has no matching region."});continue;}
                    b=SchoolMapBuilder.CorridorBox(region.name,room.level,b);
                    float elevation=SchoolMapBuilder.OccupiedFloorHeight(region.name,room.level);
                    var candidates=new List<Vector3>();
                foreach(float tx in new[]{.50f,.32f,.68f,.20f,.80f,.10f,.90f})foreach(float ty in new[]{.50f,.32f,.68f,.15f,.85f})
                        if(context.Clear(SchoolMapBuilder.Point(Mathf.Lerp(b[0],b[2],tx),Mathf.Lerp(b[1],b[3],ty),elevation+.04f),out var p))candidates.Add(p);
                    var matching=doorPoints.Where(d=>d.block==region.name&&d.level==room.level)
                        .OrderBy(d=>DistancePdfBox(d.foot,b)<.80f?0:1)
                        .ThenBy(d=>Vector3.Distance(d.foot,SchoolMapBuilder.Point((b[0]+b[2])*.5f,(b[1]+b[3])*.5f,elevation))).ToArray();
                    bool found=false;Vector3[] selected=null;string usedDoor="";
                    foreach(var door in matching)
                    {
                        foreach(int side in new[]{-1,1})
                        {
                            Vector3 start=door.foot+door.normal*(side*.62f);
                            if(!context.Clear(start,out start))continue;
                            foreach(var end in candidates.OrderBy(p=>Vector3.Distance(p,door.foot)))
                            {
                                if(Vector3.Distance(end,door.foot)<1.25f)continue;
                                if(context.Route(region.name+"-"+room.level,region.box,room.level,elevation+.04f,start,end,out selected))
                                {found=true;usedDoor=door.name;break;}
                            }
                            if(found)break;
                        }
                        if(found)break;
                    }
                    string name="Room "+roomIndex+++": "+room.name;
                    targets.Add(new Target{name=name,block=region.name,level=room.level,kind="room",passed=found,
                        detail=found?"Existing "+usedDoor+" reaches a supported point at least 1.25m into the approximate room box; shared open interiors may use a door elsewhere in this block.":
                        "No native clear route from any existing door in this block to an interior target; "+matching.Length+" block openings and "+candidates.Count+" clear interior candidates. Box/door association or geometry needs review; no wall was removed.",
                        points=selected??new[]{SchoolMapBuilder.Point((b[0]+b[2])*.5f,(b[1]+b[3])*.5f,elevation+.04f)}});
                    if(found)paths.Add(new SchoolValidationPass.TraversalPath{name=name,footPositions=selected});
                }
                AddLinks(context,layout,doorPoints,targets,paths);
                AddDoorNetworks(context,layout,doorPoints,targets,paths);
                // Every occupied region/floor gets explicit supported standing targets; no duplicated rooms on roof-only blocks.
                foreach(var region in layout.regions)for(int floor=0;floor<=(region.upper?1:0);floor++)
                {
                    float elevation=SchoolMapBuilder.OccupiedFloorHeight(region.name,floor);
                    bool found=false;Vector3 point=default;
                    foreach(float tx in new[]{.5f,.3f,.7f})foreach(float ty in new[]{.5f,.3f,.7f})
                    {
                        var b=region.box;
                        if(!found&&context.Clear(SchoolMapBuilder.Point(Mathf.Lerp(b[0],b[2],tx),Mathf.Lerp(b[1],b[3],ty),elevation+.04f),out point))found=true;
                    }
                    targets.Add(new Target{name="Block floor support "+region.name+" "+floor,block=region.name,level=floor,kind="block-support",passed=found,
                        detail=found?"Supported capsule clear on this occupied floor.":"No clear supported target among nine block probes."});
                    if(found)probes.Add(new SchoolValidationPass.WalkProbe{name="Block floor "+region.name+" "+floor,footPosition=point});
                }
            }
            finally
            {
                foreach(var pose in saved)if(pose.Key)pose.Key.localRotation=pose.Value;
                if(gate&&gate.drawer){for(int i=0;i<gateFields.Length;i++)gateFields[i].SetValue(gate,gateValues[i]);gate.drawer.localPosition=gatePose;}
                Physics.SyncTransforms();
                report.temporaryDoorPosesRestored=saved.All(p=>p.Key&&Quaternion.Angle(p.Key.localRotation,p.Value)<.001f);
            }
            // Include all established stairs and local grade routes, making the expanded links part of the existing proven graph.
            foreach(string file in new[]{"School-circulation-report.json","School-hall-grade-report.json","School-canteen-connector-report.json","School-west-block-access-report.json","School-campus-connections-report.json","School-canteen-vestibule-report.json","School-near-canteen-grade-report.json"})
            {
                string path=SchoolMapBuilder.Output+"/"+file;if(!File.Exists(path))continue;
                var circulation=JsonUtility.FromJson<SchoolCirculation.BuildReport>(File.ReadAllText(path));
                if(circulation?.routes!=null)paths.AddRange(circulation.routes.Select(r=>new SchoolValidationPass.TraversalPath{name=r.name,footPositions=r.feet,isStairRoute=r.requireAscending}));
            }
            report.physicalValidation=SchoolValidationPass.Validate(scene,SchoolMapBuilder.Point(1413,1588,.05f),probes.ToArray(),paths.ToArray());
            report.targets=targets.ToArray();report.plannedRoutes=targets.Count(t=>t.kind!="block-support");
            report.routesWithNativePath=targets.Count(t=>t.kind!="block-support"&&t.passed);
            report.inaccessibleTargets=targets.Count(t=>!t.passed);report.explicitLinks=targets.Count(t=>t.kind=="link");
            report.doorNetworkRoutes=targets.Count(t=>t.kind=="door-network");
            report.nativeGridQueries=context.queries;report.joints=AuditJoints(layout,scene);report.wallJointSuspects=report.joints.Length;
            report.passed=report.inaccessibleTargets==0&&report.temporaryDoorPosesRestored&&report.physicalValidation.passed;
            report.notes=new[]{"All returned routes are validated by a temporary bare CharacterController in both directions, with exact door poses restored.",
                "Interior route search is bounded per building/link; it considers actual support, props, colliders and existing capsule dimensions.",
                "A missing route is reported explicitly. Approximate room boxes and unphotographed topology are not permission to invent openings.",
                "Near-miss masonry joints suppress known door/window openings and are candidates for plan review, not automatically defects.",
                "No gameplay, narrative, checkpoint, player pose, lighting, permanent scene geometry or asset changes were performed."};
            File.WriteAllText(SchoolMapBuilder.Output+"/School-whole-map-audit.json",JsonUtility.ToJson(report,true));
            return report;
        }
        static void AddTarget(Context context,List<Target> targets,List<SchoolValidationPass.TraversalPath> paths,string name,string block,int level,
            string kind,float[] box,float elevation,Vector3 start,Vector3 end,bool requireLong)
        {
            Vector3[] points=null;
            bool okay=context.Clear(start,out start)&&context.Clear(end,out end)
                &&context.Route(block+"-"+level,box,level,elevation,start,end,out points);
            targets.Add(new Target{name=name,block=block,level=level,kind=kind,passed=okay,
                detail=okay?"Native supported capsule path found; actual movement follows in both directions.":"No route within the explicit region/link bounds. "+context.Failure(start)+" "+context.Failure(end),points=points??new[]{start,end}});
            if(okay)paths.Add(new SchoolValidationPass.TraversalPath{name=name,footPositions=points});
        }
        static void AddLinks(Context c,SchoolMapBuilder.Layout layout,List<DoorPoint> doors,List<Target> targets,List<SchoolValidationPass.TraversalPath> paths)
        {
            for(int f=0;f<2;f++)
            {
                AddPdfLink(c,targets,paths,"Laboratory corridor "+f,"main_laboratories_north",f,new[]{640f,303f,968f,852f},846.5f,350,846.5f,830,f*3.81f+.04f,true);
                AddPdfLink(c,targets,paths,"Classroom corridor "+f,"main_classrooms_north",f,new[]{1065f,303f,1400f,852f},1193f,350,1193f,830,f*3.81f+.04f,true);
                AddPdfLink(c,targets,paths,"Core cross corridor "+f,"main_middle_circulation",f,new[]{824f,807f,1212f,853f},846.5f,830,1193f,830,f*3.81f+.04f,false);
                AddPdfLink(c,targets,paths,"East teaching corridor "+f,"proposed_east_teaching",f,new[]{2266f,806f,2545f,1095f},2405,867,2405,1067,f*3.81f+.04f,false);
            }
            AddPdfLink(c,targets,paths,"West north corridor","west_north",0,new[]{372f,260f,558f,789f},531.16f,280,531.16f,742,SchoolWestBlockAccessPass.LowFloor+.04f,false);
            AddPdfLink(c,targets,paths,"West south corridor","west_south",0,new[]{351f,845f,627f,1382f},482.9f,860,482.9f,1290,SchoolWestBlockAccessPass.HighFloor+.04f,false);
            AddPdfLink(c,targets,paths,"East north corridor to lower landing","east_north_classrooms",0,new[]{1516f,297f,1702f,785f},1675,310,1675,718,SchoolNearCanteenGradePass.Floor+.04f,false);
            AddPdfLink(c,targets,paths,"East walkway entire length","east_walkway",0,new[]{1452f,736f,2648f,785f},1460,760,2640,760,.05f,false);
            AddPdfLink(c,targets,paths,"West walkway at upper stair landing","west_core_link",0,new[]{561.624f,794.45f,660f,845.48f},570,820,650,820,SchoolWestBlockAccessPass.HighFloor+.05f,false);
            AddPdfLink(c,targets,paths,"West rear passage to core cross corridor","west_back_link",0,new[]{958f,805f,1312f,937f},1260,830,1035,830,.05f,false);
            AddPdfLink(c,targets,paths,"Colored plan central circulation to ramp approach","central_ramp_link",0,new[]{825f,807f,1215f,928f},1018,830,980.46f,895.16f,.04f,false);
            for(int f=0;f<2;f++)
            {
                float x=f==0?980.46f:1041.76f;
                AddPdfLink(c,targets,paths,"Core through confirmed ramp doorway "+f,"central_ramp_link",f,new[]{960f,807f,1078f,928f},x,830,x,895.16f,f*3.81f+.04f,false);
            }
            AddPdfLink(c,targets,paths,"Colored plan library west reading aisle","library_reading_link",0,new[]{1892f,806f,2123f,1020f},2004,826,1914,982,.05f,false);
            AddPdfLink(c,targets,paths,"Colored plan north doorway of east teaching","east_teaching_north_link",0,new[]{2390f,736f,2425f,864f},2405.5f,760,2405.5f,822,.05f,false);
            AddPdfLink(c,targets,paths,"Colored plan west north room doorway and full depth","west_north_room_link",0,new[]{400f,275f,560f,432f},531.16f,409,450,287,SchoolWestBlockAccessPass.LowFloor+.04f,false);
            AddPdfLink(c,targets,paths,"Redesigned west electrical room doorway","west_south_room_link",0,new[]{455f,892f,605f,1009f},482.9f,917.82f,555,940,SchoolWestBlockAccessPass.HighFloor+.04f,false);
            AddPdfLink(c,targets,paths,"Core cross corridor to east walkway","core_east_link",0,new[]{1187f,735f,1467f,856f},1193,830,1460,760,.05f,false);
            var libraryDoor=doors.Where(d=>d.block=="proposed_library"&&d.level==0).OrderBy(d=>d.foot.z).FirstOrDefault();
            float libraryX=1413+libraryDoor.foot.x/SchoolCirculation.PdfScale;
            AddPdfLink(c,targets,paths,"Library front door connection","library_link",0,new[]{1880f,738f,2140f,1024f},libraryX,760,libraryX,842,.05f,false);
            // The native south facade has two wide open hall mouths; x2390/y440 was a closed laboratory/window target.
            AddPdfLink(c,targets,paths,"Northeast workshops south access","workshops_link",0,new[]{2098f,309f,2742f,765f},2324.78f,760,2324.78f,510,.05f,false);
            AddPdfLink(c,targets,paths,"Northeast workshops southwest hall opening","workshops_west_link",0,new[]{2098f,309f,2742f,765f},2240.5f,760,2240.5f,510,.05f,false);
            AddPdfLink(c,targets,paths,"Automotive photo-confirmed RIGHT lateral access","automotive_link",0,new[]{2610f,309f,2790f,765f},2770,760,2690,480,.05f,false);
            AddPdfLink(c,targets,paths,"Court and open patio connection","court_link",0,new[]{2235f,1055f,2944f,1555f},2405,1110,2766,1382,.06f,false);
            AddPdfLink(c,targets,paths,"Administrative hall east approach","administration_south",0,new[]{1062f,1174f,1452f,1344f},1421,1259,1290,1259,.65f,false);
            AddPdfLink(c,targets,paths,"Pedagogy west approach","pedagogy_south",0,new[]{1420f,1174f,1707f,1344f},1421,1260,1540,1260,.65f,false);
        }
        static void AddDoorNetworks(Context c,SchoolMapBuilder.Layout layout,List<DoorPoint> doors,List<Target> targets,List<SchoolValidationPass.TraversalPath> paths)
        {
            foreach(var region in layout.regions)for(int level=0;level<=(region.upper?1:0);level++)
            {
                var openings=doors.Where(d=>d.block==region.name&&d.level==level).ToArray();if(openings.Length==0)continue;
                float elevation=SchoolMapBuilder.OccupiedFloorHeight(region.name,level)+.04f;
                Vector3 anchor=default;bool anchorFound=false;
                // These anchors coincide with the independently audited corridor links.
                if(region.name=="main_laboratories_north")anchor=SchoolMapBuilder.Point(SchoolMapBuilder.CorridorX(region.name,level,846.5f),830,elevation);
                else if(region.name=="main_classrooms_north")anchor=SchoolMapBuilder.Point(SchoolMapBuilder.CorridorX(region.name,level,1193f),830,elevation);
                else if(region.name=="main_middle_circulation")anchor=SchoolMapBuilder.Point(1035,830,elevation);
                else if(region.name=="proposed_east_teaching")anchor=SchoolMapBuilder.Point(2405,867,elevation);
                else if(region.name=="west_north")anchor=SchoolMapBuilder.Point(531.16f,740,elevation);
                else if(region.name=="west_south")anchor=SchoolMapBuilder.Point(483.64f,906,elevation);
                else if(region.name=="east_north_classrooms")anchor=SchoolMapBuilder.Point(1675,740,elevation);
                else if(region.name=="administration_south")anchor=SchoolMapBuilder.Point(1320,1259,elevation);
                else if(region.name=="pedagogy_south")anchor=SchoolMapBuilder.Point(1510,1260,elevation);
                if(anchor!=default)anchorFound=c.Clear(anchor,out anchor);
                if(!anchorFound)
                {
                    var b=region.box;
                    foreach(var door in openings.OrderBy(d=>{
                        float x=1413+d.foot.x/SchoolCirculation.PdfScale,y=1628-d.foot.z/SchoolCirculation.PdfScale;
                        return Mathf.Min(Mathf.Abs(x-b[0]),Mathf.Abs(x-b[2]),Mathf.Abs(y-b[1]),Mathf.Abs(y-b[3]));}))
                    {
                        foreach(int side in new[]{-1,1})
                        {
                            Vector3 p=door.foot+door.normal*(side*.65f);
                            if(DistancePdfBox(p,b)<.04f&&c.Clear(p,out anchor)){anchorFound=true;break;}
                        }
                        if(anchorFound)break;
                    }
                }
                var bounds=(float[])region.box.Clone();
                // The real cross corridor is south of the laboratories' room
                // footprint. Include its supported anchor in the search bounds;
                // otherwise direct room doors pass while a detour through another
                // room fails before searching because its start lies off-grid.
                if(anchorFound){
                    float ax=1413+anchor.x/SchoolCirculation.PdfScale,ay=1628-anchor.z/SchoolCirculation.PdfScale;
                    bounds[0]=Mathf.Min(bounds[0],ax);bounds[2]=Mathf.Max(bounds[2],ax);
                    bounds[1]=Mathf.Min(bounds[1],ay);bounds[3]=Mathf.Max(bounds[3],ay);
                }
                // Ground rooms may each open onto the same exterior walkway; allow a narrow perimeter detour.
                // Upper grids still require physical deck support at3.81m, so no roof/outdoor route is inferred.
                if(level==0){bounds[0]-=32;bounds[1]-=32;bounds[2]+=32;bounds[3]+=32;}
                foreach(var door in openings)
                {
                    Vector3[] selected=null;bool okay=false;
                    if(anchorFound)foreach(int side in new[]{-1,1})
                    {
                        Vector3 goal=door.foot+door.normal*(side*.62f);
                        if(c.Clear(goal,out goal)&&c.Route("door-network-"+region.name+"-"+level,bounds,level,elevation,anchor,goal,out selected)){okay=true;break;}
                    }
                    string name="Block entrance network "+region.name+" "+level+" -> "+door.name;
                    targets.Add(new Target{name=name,block=region.name,level=level,kind="door-network",passed=okay,
                        detail=okay?"Existing opening joins the shared supported block/corridor anchor; its aperture separately crosses in both directions.":
                            "Opening cannot join the shared block/corridor anchor within bounded physical floor support. Check separate exterior entrances, approximate block association and structural obstructions.",
                        points=selected??new[]{anchor,door.foot}});
                    if(okay)paths.Add(new SchoolValidationPass.TraversalPath{name=name,footPositions=selected});
                }
            }
        }
        static void AddPdfLink(Context c,List<Target> targets,List<SchoolValidationPass.TraversalPath> paths,string name,string block,int level,
            float[] box,float ax,float ay,float bx,float by,float elevation,bool remap)
        {
            if(remap){ax=SchoolMapBuilder.CorridorX(block,level,ax);bx=SchoolMapBuilder.CorridorX(block,level,bx);}
            AddTarget(c,targets,paths,name,block,level,"link",box,elevation,SchoolMapBuilder.Point(ax,ay,elevation),SchoolMapBuilder.Point(bx,by,elevation),true);
        }
        static float DistancePdfBox(Vector3 world,float[] b)
        {
            float x=1413+world.x/SchoolCirculation.PdfScale,y=1628-world.z/SchoolCirculation.PdfScale;
            return new Vector2(Mathf.Max(b[0]-x,0,x-b[2]),Mathf.Max(b[1]-y,0,y-b[3])).magnitude*SchoolCirculation.PdfScale;
        }
        static Joint[] AuditJoints(SchoolMapBuilder.Layout layout,Scene scene)
        {
            // The saved prepared layout is the authoritative redesigned geometry.
            // Comparing current native walls to old CAD axes reports phantom gaps.
            string baselinePath=SchoolMapBuilder.Output+"/School-geometry-baseline.json";
            bool adapted=File.Exists(baselinePath);
            if(adapted)layout=JsonUtility.FromJson<SchoolGeometryPass.Baseline>(File.ReadAllText(baselinePath)).after;
            var result=new List<Joint>();float scale=SchoolCirculation.PdfScale;
            foreach(var level in layout.levels)
            {
                // Clone axes and remap without mutating source data or running the builder.
                var walls=level.walls.Select(w=>new SchoolMapBuilder.Span{block=w.block,vertical=w.vertical,
                    p=adapted?w.p:w.vertical?SchoolMapBuilder.CorridorX(w.block,level.index,w.p):w.p,
                    a=adapted?w.a:w.vertical?w.a:SchoolMapBuilder.CorridorX(w.block,level.index,w.a),
                    b=adapted?w.b:w.vertical?w.b:SchoolMapBuilder.CorridorX(w.block,level.index,w.b),thickness=w.thickness}).ToArray();
                foreach(var w in walls)foreach(var other in walls)
                {
                    if(w.block!=other.block||w.vertical==other.vertical||other.p>=w.a&&other.p<=w.b||w.p<other.a||w.p>other.b)continue;
                    float endpoint=Mathf.Abs(w.a-other.p)<Mathf.Abs(w.b-other.p)?w.a:w.b;
                    float gap=(Mathf.Abs(endpoint-other.p)-Mathf.Max(.14f/scale,other.thickness)*.5f)*scale;
                    if(gap<=.025f||gap>=.22f)continue;
                    float x=w.vertical?w.p:endpoint,y=w.vertical?endpoint:w.p;
                    float jx=w.vertical?w.p:other.p,jy=w.vertical?other.p:w.p;
                    bool opening=level.doors.Concat(level.windows).Any(d=>{
                        float p=adapted?d.p:d.vertical?SchoolMapBuilder.CorridorX(d.block,level.index,d.p):d.p;
                        float a=adapted?d.a:d.vertical?d.a:SchoolMapBuilder.CorridorX(d.block,level.index,d.a);
                        float b=adapted?d.b:d.vertical?d.b:SchoolMapBuilder.CorridorX(d.block,level.index,d.b);
                        return d.block==w.block&&Vector2.Distance(new Vector2(jx,jy),d.vertical?new Vector2(p,Mathf.Clamp(jy,a,b)):new Vector2(Mathf.Clamp(jx,a,b),p))*scale<.75f;});
                    if(opening)continue;
                    // A second extracted face or already-joined native piece can seal a raw-axis near miss.
                    // Check the midpoint of the apparent face-to-endpoint gap against actual masonry colliders.
                    float face=other.p+Mathf.Sign(endpoint-other.p)*Mathf.Max(.14f/scale,other.thickness)*.5f;
                    float middle=(endpoint+face)*.5f;
                    float inside=endpoint+Mathf.Sign((w.a+w.b)*.5f-endpoint)*.025f/scale;
                    Vector3 wallPoint=SchoolMapBuilder.Point(w.vertical?w.p:inside,w.vertical?inside:w.p,SchoolMapBuilder.OccupiedFloorHeight(w.block,level.index)+1.6f);
                    if(!Physics.OverlapSphere(wallPoint,.012f,~0,QueryTriggerInteraction.Ignore)
                        .Any(c=>c&&c.enabled&&c.gameObject.scene==scene&&c.name.StartsWith("Parede")))continue;
                    Vector3 gapPoint=SchoolMapBuilder.Point(w.vertical?w.p:middle,w.vertical?middle:w.p,SchoolMapBuilder.OccupiedFloorHeight(w.block,level.index)+1.6f);
                    if(Physics.OverlapSphere(gapPoint,.012f,~0,QueryTriggerInteraction.Ignore)
                        .Any(c=>c&&c.enabled&&c.gameObject.scene==scene&&(c.name.StartsWith("Parede")||c.name=="Peitoril")))continue;
                    result.Add(new Joint{block=w.block,level=level.index,gapMeters=gap,
                        endpoint=SchoolMapBuilder.Point(x,y,SchoolMapBuilder.OccupiedFloorHeight(w.block,level.index)),intersection=SchoolMapBuilder.Point(jx,jy,SchoolMapBuilder.OccupiedFloorHeight(w.block,level.index)),
                        detail="Perpendicular masonry axes nearly meet outside a known opening; confirm native faces and PDF role before extending."});
                }
            }
            return result.ToArray();
        }
    }
}
