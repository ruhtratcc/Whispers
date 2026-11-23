// Ferramenta da escola de validação.

using System;
using System.Linq;
namespace SchoolCEEP.Editor { public static class SchoolArchitectureProof { public static object Verify() {
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(scene.path!=SchoolCEEP.Editor.SchoolIntegrationPass.ScenePath)throw new System.InvalidOperationException("Open school.");
var root=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("CEEP"));
var data=UnityEngine.JsonUtility.FromJson<SchoolCEEP.Editor.SchoolArchitectureRepairPass.Report>(System.IO.File.ReadAllText(SchoolCEEP.Editor.SchoolMapBuilder.Output+"/School-architecture-redesign.json"));
var doors=root.GetComponentsInChildren<HorrorGame.InteractiveDoor>(true);
var ids=doors.Select(d=>d.persistentId).ToArray();
var physical=root.GetComponentsInChildren<UnityEngine.Collider>(true).Where(c=>c.enabled&&!c.isTrigger).ToArray();
var masonry=physical.Where(c=>c.name.StartsWith("Parede")||c.name=="Peitoril"||c.name.StartsWith("Verga")||c.name.StartsWith("Janela")||c.name.StartsWith("Bandeira de vidro")||c.name.Contains("fecho")||c.name.Contains("empena")).ToArray();
var gaps=new System.Collections.Generic.List<object>();
var roomResults=new System.Collections.Generic.List<object>();
var overlaps=new System.Collections.Generic.List<object>();
UnityEngine.Physics.SyncTransforms();
foreach(var r in data.roomLayout)
{
    float[] b=SchoolCEEP.Editor.SchoolCampusConnectionsPass.Box(r.block,r.box);
    b=SchoolCEEP.Editor.SchoolMapBuilder.CorridorBox(r.block,r.level,b);
    float h=SchoolCEEP.Editor.SchoolMapBuilder.OccupiedFloorHeight(r.block,r.level);
    var nw=SchoolCEEP.Editor.SchoolMapBuilder.Point(b[0],b[1],h);
    var se=SchoolCEEP.Editor.SchoolMapBuilder.Point(b[2],b[3],h);
    var bounds=new UnityEngine.Bounds((nw+se)*.5f+UnityEngine.Vector3.up*1.5f,new UnityEngine.Vector3(se.x-nw.x,3,nw.z-se.z));
    var accesses=doors.Where(d=>d.leafCollider&&UnityEngine.Mathf.Abs(d.leafCollider.bounds.min.y-h)<.16f&&
        new UnityEngine.Bounds(bounds.center,bounds.size+new UnityEngine.Vector3(.60f,0,.60f)).Intersects(d.leafCollider.bounds)).ToArray();
    bool standing=false;UnityEngine.Vector3 stand=UnityEngine.Vector3.zero;
    for(int iz=0;iz<9&&!standing;iz++)for(int ix=0;ix<9&&!standing;ix++)
    {
        var foot=new UnityEngine.Vector3(UnityEngine.Mathf.Lerp(nw.x+.40f,se.x-.40f,ix/8f),h+.06f,UnityEngine.Mathf.Lerp(se.z+.40f,nw.z-.40f,iz/8f));
        if(!UnityEngine.Physics.CheckCapsule(foot+UnityEngine.Vector3.up*.26f,foot+UnityEngine.Vector3.up*1.52f,.25f,~0,UnityEngine.QueryTriggerInteraction.Ignore))
        {standing=true;stand=foot;}
    }
    int supported=0,apertures=0,missing=0,intentionalOpen=0;
    bool upperSlab=r.level==0&&new[]{"main_laboratories_north","main_classrooms_north","main_coordination_south","main_auditorium_south","proposed_east_teaching"}.Contains(r.block);
    foreach(float sampleHeight in r.block=="west_south"?new[]{.60f,1.65f,2.9f,4.70f}:upperSlab?new[]{.60f,1.65f,2.9f,3.48f}:new[]{.60f,1.65f,2.9f})
    for(int side=0;side<4;side++)
    {
        var normal=side==0?UnityEngine.Vector3.left:side==1?UnityEngine.Vector3.right:side==2?UnityEngine.Vector3.forward:UnityEngine.Vector3.back;
        float length=side<2?nw.z-se.z:se.x-nw.x;
        int n=UnityEngine.Mathf.Max(3,UnityEngine.Mathf.CeilToInt(length/.30f));
        for(int i=1;i<n;i++)
        {
            float f=i/(float)n;
            var edge=new UnityEngine.Vector3(side==0?nw.x:side==1?se.x:UnityEngine.Mathf.Lerp(nw.x,se.x,f),h+sampleHeight,
                side==2?nw.z:side==3?se.z:UnityEngine.Mathf.Lerp(se.z,nw.z,f));
            bool open=side==3&&r.openSouth||side==1&&r.openEastAfterY>0&&1628-edge.z/SchoolCEEP.Editor.SchoolCirculation.PdfScale>=SchoolCEEP.Editor.SchoolCampusConnectionsPass.Y(r.block,r.openEastAfterY);
            if(open){intentionalOpen++;continue;}
            bool aperture=accesses.Any(d=>{var box=d.leafCollider.bounds;box.Expand(new UnityEngine.Vector3(.5f,.3f,.5f));return box.Contains(edge);});
            if(aperture){apertures++;continue;}
            var origin=edge-normal*.35f;
            var hits=UnityEngine.Physics.RaycastAll(origin,normal,.90f,~0,UnityEngine.QueryTriggerInteraction.Ignore);
            if(hits.Any(hit=>masonry.Contains(hit.collider))){supported++;continue;}
            missing++;gaps.Add(new{room=r.name,r.level,r.block,side,sampleHeight,point=new{edge.x,edge.y,edge.z}});
        }
    }
    var accessIds=accesses.Select(d=>d.persistentId).ToList();
    if(r.openSouth)accessIds.Add(SchoolCEEP.Editor.SchoolWorkshopPhotoPass.GateId);
    roomResults.Add(new{r.name,r.block,r.kind,r.level,standing,stand=new{stand.x,stand.y,stand.z},accesses=accessIds.ToArray(),supported,apertures,intentionalOpen,missing});
}
foreach(var d in doors)
{
    var leaf=d.leafCollider;if(!leaf)continue;
    foreach(var wall in masonry)
    {
        if(!leaf.bounds.Intersects(wall.bounds))continue;
        if(UnityEngine.Physics.ComputePenetration(leaf,leaf.transform.position,leaf.transform.rotation,wall,wall.transform.position,wall.transform.rotation,out var direction,out float depth)&&depth>.015f)
            overlaps.Add(new{door=d.persistentId,wall=wall.name,parent=wall.transform.parent.name,depth});
    }
}
var result=new{time=System.DateTime.Now,scene=scene.path,play=false,rooms=roomResults,unclosedEdgeSamples=gaps,closedDoorWallOverlaps=overlaps,
    doorCount=doors.Length,uniqueDoorIds=ids.Distinct().Count(),westClearWidth=data.westCorridorClearWidthMeters,
    flatCanteenRoofAbsent=root.transform.Find("central_east_services/Cobertura alta da cantina")==null,
    slopingCanteenRoofPresent=root.transform.Find("Cantina — cobertura inclinada alta")!=null};
System.IO.File.WriteAllText(SchoolCEEP.Editor.SchoolMapBuilder.Output+"/School-architecture-proof.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
return new{rooms=roomResults.Count,gaps=gaps.Count,overlaps=overlaps.Count,doors=doors.Length};

} } }
