// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;
using S = SchoolCEEP.Editor.SchoolMapBuilder.Span;
using L = SchoolCEEP.Editor.SchoolMapBuilder.Level;

namespace SchoolCEEP.Editor
{
    // Authorised architectural redesign. CAD remains a reference for the site envelope,
    // not a requirement to reproduce annotation lines as walls or orphaned doors.
    public static class SchoolArchitectureRepairPass
    {
        public const float WestLeft = 461.4f, WestRight = 504.4f;
        const float Scale = .07055555556f;
        public const string Source = "coherent school architecture revision";
        public const string RetiredDoor = "coherent school architecture — retired unsupported duplicate door";
        static readonly List<RoomSpec> rooms = new List<RoomSpec>();
        static readonly List<WallSpec> designWalls = new List<WallSpec>();
        static Report report;
        static Transform furniture;
        static Material white, steel, wood, navy, green, porcelain;
        [Serializable] public class RoomSpec
        { public string name, block, kind; public int level; public float[] box; public bool openSouth; public float openEastAfterY; }
        [Serializable] public class WallSpec
        { public int level; public S wall; }
        [Serializable] public class Report
        {
            public bool authoredLayout = true, dimensionsEstimated = true;
            public int oldWallSpans, rebuiltWallSpans, reassignedDoors, newDoors, rooms, usefulFurnitureGroups;
            public int replacedFloatingFloorJoints, roofFrames, relocatedHallObjects, wallTopClosures;
            public float westCorridorClearWidthMeters = (WestRight-WestLeft)*Scale-.20f;
            public RoomSpec[] roomLayout; public WallSpec[] intendedWalls;
            public string[] notes;
        }
        static Vector3 P(float x,float y,float h=0)=>SchoolMapBuilder.Point(x,y,h);
        static S Wall(string block,bool vertical,float p,float a,float b)=>new S{
            block=block,vertical=vertical,p=p,a=a,b=b,thickness=.20f/Scale,source=Source};
        static void Put(L level,S wall)
        {level.walls=level.walls.Concat(new[]{wall}).ToArray();designWalls.Add(new WallSpec{level=level.index,wall=wall});}
        static void Reset(L level,string block)
        {report.oldWallSpans+=level.walls.Count(w=>w.block==block);level.walls=level.walls.Where(w=>w.block!=block).ToArray();}
        static void Box(L l,string block,float x0,float y0,float x1,float y1)
        {Put(l,Wall(block,true,x0,y0,y1));Put(l,Wall(block,true,x1,y0,y1));Put(l,Wall(block,false,y0,x0,x1));Put(l,Wall(block,false,y1,x0,x1));}
        static void Door(L l,int slot,bool vertical,float axis,float center,float width=1.10f)
        {
            var d=l.doors[slot];d.vertical=vertical;d.p=axis;d.a=center-width/Scale*.5f;d.b=center+width/Scale*.5f;d.width=d.b-d.a;
            d.provisional=true;d.source=Source;report.reassignedDoors++;
        }
        static int NewDoor(L l,string block,bool vertical,float axis,float center,float width=1.1f)
        {
            var d=Wall(block,vertical,axis,center-width/Scale*.5f,center+width/Scale*.5f);
            d.provisional=true;d.width=d.b-d.a;l.doors=l.doors.Concat(new[]{d}).ToArray();report.newDoors++;return l.doors.Length-1;
        }
        static void Pair(L l,int first,int second,bool vertical,float axis,float center,float totalWidth=1.8f)
        {
            float half=totalWidth/Scale*.5f;
            Door(l,first,vertical,axis,center-half*.5f,totalWidth*.5f);
            Door(l,second,vertical,axis,center+half*.5f,totalWidth*.5f);
            foreach(int i in new[]{first,second}){l.doors[i].provisional=false;l.doors[i].source="quarter-swing CAD curve — redesigned paired doorway";}
        }
        static void Window(L l,string block,bool vertical,float axis,float a,float b)
        {l.windows=l.windows.Concat(new[]{Wall(block,vertical,axis,a,b)}).ToArray();}
        static void Room(string name,string block,string kind,int floor,float x0,float y0,float x1,float y1)
        {rooms.Add(new RoomSpec{name=name,block=block,kind=kind,level=floor,box=new[]{x0,y0,x1,y1}});}

        public static void Prepare(SchoolMapBuilder.Layout layout)
        {
            rooms.Clear();designWalls.Clear();report=new Report();
            West(layout.levels[0]);MainNorth(layout);Administration(layout.levels[0]);Pedagogy(layout.levels[0]);
            Services(layout.levels[0]);Library(layout.levels[0]);Workshops(layout.levels[0]);EastNorth(layout.levels[0]);
            SouthWings(layout);EastTeaching(layout);MiddleCirculation(layout);
            // Uniform wall ownership, then openings cut from those walls. No independent
            // jamb stumps or duplicate CAD faces survive in reconstructed sectors.
            foreach(var l in layout.levels)
            {
                // Glazing is part of the same envelope and cannot cover a relocated
                // door. Unite coincident window records, then cut the real aperture.
                var glazing=new List<S>();
                foreach(var group in l.windows.GroupBy(w=>w.block+"/"+w.vertical+"/"+Mathf.Round(w.p*10)))
                {
                    var sorted=group.OrderBy(w=>w.a).ToArray();var union=new List<S>();
                    foreach(var w in sorted)
                    {var last=union.LastOrDefault();if(last!=null&&w.a<=last.b+.05f)last.b=Mathf.Max(last.b,w.b);else union.Add(new S{block=w.block,vertical=w.vertical,p=w.p,a=w.a,b=w.b,thickness=w.thickness,source=w.source});}
                    foreach(var w in union)
                    {
                        var pieces=new List<Vector2>{new Vector2(w.a,w.b)};
                        foreach(var d in l.doors.Where(d=>d.source!=RetiredDoor&&d.block==w.block&&d.vertical==w.vertical&&Mathf.Abs(d.p-w.p)<4))
                        {float half=(Mathf.Max(.9f,(d.b-d.a)*Scale)*.5f+.07f)/Scale,mid=(d.a+d.b)*.5f;var next=new List<Vector2>();
                            foreach(var p in pieces){if(mid+half<=p.x||mid-half>=p.y){next.Add(p);continue;}if(mid-half>p.x)next.Add(new Vector2(p.x,mid-half));if(mid+half<p.y)next.Add(new Vector2(mid+half,p.y));}pieces=next;}
                        foreach(var p in pieces)if(p.y-p.x>.08f)glazing.Add(new S{block=w.block,vertical=w.vertical,p=w.p,a=p.x,b=p.y,thickness=w.thickness,source=w.source});
                    }
                }
                l.windows=glazing.Where(w=>(w.b-w.a)*Scale>=.70f).ToArray();
            }
            report.rooms=rooms.Count;report.roomLayout=rooms.ToArray();report.intendedWalls=designWalls.ToArray();
            report.notes=new[]{
                "User authorises a coherent school redesign, including invented functions where reference is missing. Envelope, photographed circulation and previous explicit access corrections remain the site reference.",
                "Each reconstructed sector has one owner per wall axis, explicit room partitions and door cuts. Western high corridor is straight and 2.83m clear, with nine rooms and aligned accesses.",
                "Laboratory/classroom banks, administration, pedagogy, kitchen, library and workshop rooms are enclosed deliberately. Bathrooms, pantry, office, storage and auditorium functions replace empty or contradictory generic boxes.",
                "The canteen has a high inclined metal roof with visible rafters. The taller partition remains at the main hall roof height. All dimensions are estimates, not a surveyed reconstruction."};
        }
        // Cut only after corridor coordinates have their final metric width.
        public static void Carve(SchoolMapBuilder.Layout layout)
        {
            foreach(var l in layout.levels)
            {
                l.windows=l.windows.Where(w=>(w.b-w.a)*Scale>=.70f).ToArray();
                var result=new List<S>();
                foreach(var w in l.walls)
                {
                    if(w.source!=Source){result.Add(w);continue;}
                    var pieces=new List<Vector2>{new Vector2(w.a,w.b)};
                    var cuts=l.doors.Where(d=>d.source!=RetiredDoor&&d.block==w.block&&d.vertical==w.vertical&&Mathf.Abs(d.p-w.p)<4f).ToArray();
                    foreach(var d in cuts)
                    {
                        float half=(SchoolMapBuilder.DoorWidth(l,d)*.5f+.065f)/Scale,mid=(d.a+d.b)*.5f;
                        var next=new List<Vector2>();
                        foreach(var p in pieces)
                        {if(mid+half<=p.x||mid-half>=p.y){next.Add(p);continue;}if(mid-half>p.x)next.Add(new Vector2(p.x,mid-half));if(mid+half<p.y)next.Add(new Vector2(mid+half,p.y));}
                        pieces=next;
                    }
                    foreach(var p in pieces)if(p.y-p.x>.08f)result.Add(Wall(w.block,w.vertical,w.p,p.x,p.y));
                }
                l.walls=result.ToArray();
            }
            report.rebuiltWallSpans=layout.levels.Sum(l=>l.walls.Count(w=>w.source==Source));
            File.WriteAllText(SchoolMapBuilder.Output+"/School-architecture-redesign.json",JsonUtility.ToJson(report,true));
        }
        static void West(L l)
        {
            const string block="west_south";Reset(l,block);l.windows=l.windows.Where(w=>w.block!=block).ToArray();
            const float outerLeft=368.92f,outerRight=597.04f,north=844.58f,south=1355;
            Put(l,Wall(block,true,outerLeft,north,south));Put(l,Wall(block,true,outerRight,north,south));
            Put(l,Wall(block,false,south,outerLeft,outerRight));
            Put(l,Wall(block,false,north,outerLeft,WestLeft));Put(l,Wall(block,false,north,WestRight,outerRight));
            Put(l,Wall(block,true,WestLeft,north,south));Put(l,Wall(block,true,WestRight,north,south));
            // Entrance gate remains in the vestibule; side rooms start after it.
            Pair(l,0,70,false,892.82f,482.9f,1.8f);
            Put(l,Wall(block,false,892.82f,outerLeft,outerRight));
            float[] left={892.82f,985.256f,1077.692f,1170.128f,1262.564f,1355};
            float[] right={892.82f,1008.365f,1123.91f,1239.455f,1355};
            int[] ld={57,58,59,60,61},rd={62,63,64,65};
            string[] ln={"Informática — laboratório A","Informática — laboratório B","Grêmio estudantil","Sala de apoio aos alunos","Depósito técnico"};
            string[] rn={"Instalações elétricas","Topografia e desenho","Oficina de manutenção","Sala de projetos"};
            for(int i=0;i<5;i++)
            {if(i>0)Put(l,Wall(block,false,left[i],outerLeft,WestLeft));Door(l,ld[i],true,WestLeft,left[i]+24);
                Window(l,block,true,outerLeft,left[i]+15,left[i+1]-15);Room(ln[i],block,i<2?"lab":"support",0,outerLeft+2,left[i]+2,WestLeft-2,left[i+1]-2);}
            for(int i=0;i<4;i++)
            {if(i>0)Put(l,Wall(block,false,right[i],WestRight,outerRight));Door(l,rd[i],true,WestRight,right[i]+25);
                Window(l,block,true,outerRight,right[i]+15,right[i+1]-15);Room(rn[i],block,"lab",0,WestRight+2,right[i]+2,outerRight-2,right[i+1]-2);}
            const string low="west_north";Reset(l,low);l.windows=l.windows.Where(w=>w.block!=low).ToArray();
            Box(l,low,402.88f,278.36f,502.12f,732.08f);
            float[] breaks={278.36f,428.84f,581.96f,732.08f};
            for(int i=0;i<3;i++)
            {if(i>0)Put(l,Wall(low,false,breaks[i],402.88f,502.12f));Door(l,51+i,true,502.12f,breaks[i+1]-24);
                Window(l,low,true,402.88f,breaks[i]+18,breaks[i+1]-18);
                Room(i<2?"Meio ambiente — laboratório "+(i+1):"Sala de uso múltiplo",low,i<2?"lab":"class",0,405,breaks[i]+2,500,breaks[i+1]-2);}
        }
        static void MainNorth(SchoolMapBuilder.Layout layout)
        {
            foreach(var l in layout.levels)
            {
                string lab="main_laboratories_north",cls="main_classrooms_north";
                Reset(l,lab);Reset(l,cls);
                float left=l.index==0?829.60f:831.64f,right=862.30f;
                // Retain the authored south stair void; the western room bank ends at727.
                Put(l,Wall(lab,true,647.68f,337.22f,727));Put(l,Wall(lab,true,959.74f,337.22f,805));
                Put(l,Wall(lab,true,left,337.22f,727));Put(l,Wall(lab,true,right,337.22f,805));
                Put(l,Wall(lab,false,337.22f,647.68f,959.74f));
                Put(l,Wall(lab,false,727,647.68f,left));Put(l,Wall(lab,false,805,right,959.74f));
                foreach(float y in new[]{425.6f,511.4f,597.24f,640.28f})Put(l,Wall(lab,false,y,647.68f,left));
                foreach(float y in new[]{467.6f,596.72f,724f})Put(l,Wall(lab,false,y,right,959.74f));
                // The two ground WC doors serve separate rooms, rather than one empty box.
                if(l.index==0)
                {
                    Put(l,Wall(lab,false,763,right,959.74f));
                    Door(l,48,true,left,407);Door(l,14,true,left,490);
                    Pair(l,15,75,true,left,572,1.5f);Pair(l,16,76,true,left,618,1.5f);Pair(l,13,74,true,left,696,1.5f);
                    Door(l,12,true,right,442);Door(l,11,true,right,572);Door(l,10,true,right,698);
                    Door(l,18,true,right,744);Door(l,17,true,right,783);
                }
                else
                {
                    NewDoor(l,lab,true,left,402);
                    foreach(var d in l.doors.Where(d=>d.block==lab&&d.vertical&&d.p>820&&d.p<850))d.p=left;
                    foreach(var d in l.doors.Where(d=>d.block==lab&&d.vertical&&d.p>850&&d.p<880))d.p=right;
                    // Duplicate outer-wall doors had no exterior floor at this level.
                    // Keep their registry slots while retiring their physical leaves.
                    foreach(int slot in new[]{19,20,21,22}){l.doors[slot].source=RetiredDoor;l.doors[slot].provisional=true;}
                }
                // Sashes follow one structural axis, instead of floating in parallel faces.
                foreach(var w in l.windows.Where(w=>w.block==lab&&w.vertical))
                {if(w.p<680)w.p=647.68f;else if(w.p>930)w.p=959.74f;else w.p=w.p<846?left:right;}
                float cLeft=l.index==0?1177.72f:1179.34f,cRight=l.index==0?1210.08f:1209.96f;
                Put(l,Wall(cls,true,1076.8f,337.22f,805));Put(l,Wall(cls,true,1390.78f,337.22f,805));
                Put(l,Wall(cls,true,cLeft,337.22f,805));Put(l,Wall(cls,true,cRight,337.22f,805));
                Put(l,Wall(cls,false,337.22f,1076.8f,1390.78f));
                Put(l,Wall(cls,false,805,1076.8f,cLeft));Put(l,Wall(cls,false,805,cRight,1390.78f));
                foreach(float y in new[]{467.6f,596.72f,724f})Put(l,Wall(cls,false,y,1076.8f,cLeft));
                foreach(float y in new[]{425.6f,511.4f,597.24f})Put(l,Wall(cls,false,y,cRight,1390.78f));
                foreach(var d in l.doors.Where(d=>d.block==cls&&d.vertical))
                {if(d.p>1160&&d.p<1190)d.p=cLeft;else if(d.p>1190&&d.p<1230)d.p=cRight;else if(d.p>1380)d.p=1390.78f;}
                foreach(var w in l.windows.Where(w=>w.block==cls&&w.vertical))w.p=w.p<1230?1076.8f:1390.78f;
                foreach(var w in l.windows.Where(w=>w.block==cls&&!w.vertical))
                {w.p=805;w.a=Mathf.Max(1076.8f,w.a);w.b=Mathf.Min(1390.78f,w.b);}
                Window(l,cls,false,805,cRight+12,1377);
                if(l.index==0){Door(l,1,true,cRight,401);Door(l,20,true,cRight,487);Door(l,19,true,cRight,573);Door(l,4,true,cRight,700);l.doors[50].source=RetiredDoor;l.doors[50].provisional=true;
                    l.windows=l.windows.Where(w=>!(w.block==cls&&!w.vertical&&Mathf.Abs(w.p-805)<.1f&&w.a>1230)).ToArray();}
                // Small rooms are sensible storage/support rooms. Large ground laboratories
                // marked07/02 are standard classrooms, matching the user's photographic standard.
                string[] westLab=l.index==0?new[]{"Laboratório 11B","Laboratório 11A","Laboratório 10A","Sala de preparação técnica","Laboratório 09"}:
                    new[]{"Laboratório 25","Laboratório 24","Laboratório 23","Preparação de química","Laboratório 22"};
                float[] rows={337.22f,425.6f,511.4f,597.24f,640.28f,727};
                for(int i=0;i<5;i++)Room(westLab[i],lab,i==3?"support":"lab",l.index,650,rows[i]+2,left-2,rows[i+1]-2);
                string[] eastLab=l.index==0?new[]{"Laboratório 08","Sala 07","Almoxarifado de ciências","Sanitário feminino","Sanitário masculino"}:
                    new[]{"Laboratório 21","Sala 20","Sala 19","Almoxarifado de química"};
                float[] eRows=l.index==0?new[]{337.22f,467.6f,596.72f,724f,763f,805}:new[]{337.22f,467.6f,596.72f,724f,805};
                for(int i=0;i<eastLab.Length;i++)Room(eastLab[i],lab,l.index==0?(i==0?"lab":i==1?"class":i>=3?"bathroom":"support"):(i==0?"lab":i<3?"class":"support"),l.index,right+2,eRows[i]+2,957.5f,eRows[i+1]-2);
                float[] cw={337.22f,467.6f,596.72f,724f,805},ce={337.22f,425.6f,511.4f,597.24f,805};
                for(int i=0;i<4;i++)
                {
                    Room(i==3?"Depósito didático":("Sala "+(l.index==0?6-i:18-i)),cls,i==3?"support":"class",l.index,1079,cw[i]+2,cLeft-2,cw[i+1]-2);
                    Room("Sala "+(l.index==0?new[]{"03A","03B","03","02"}[i]:(15-i).ToString()),cls,"class",l.index,cRight+2,ce[i]+2,1388.5f,ce[i+1]-2);
                }
            }
        }
        static void Administration(L l)
        {
            const string b="administration_south";Reset(l,b);l.windows=l.windows.Where(w=>w.block!=b).ToArray();
            Box(l,b,1064.44f,1183.76f,1348.884f,1326);
            Put(l,Wall(b,false,1241,1064.44f,1301));Put(l,Wall(b,false,1277,1064.44f,1348.884f));
            float[] xs={1064.44f,1143.293f,1222.147f,1301};string[] names={"Direção","Secretaria","Sala dos professores","Arquivo administrativo","Atendimento","Sala de reuniões"};
            for(int i=0;i<3;i++)
            {
                if(i>0){Put(l,Wall(b,true,xs[i],1183.76f,1241));Put(l,Wall(b,true,xs[i],1277,1326));}
                Door(l,22+i*2,false,1241,(xs[i]+xs[i+1])*.5f);Door(l,21+i*2,false,1277,(xs[i]+xs[i+1])*.5f);
                Window(l,b,false,1183.76f,xs[i]+12,xs[i+1]-12);Window(l,b,false,1326,xs[i]+12,xs[i+1]-12);
                Room(names[i],b,"office",0,xs[i]+2,1185.76f,xs[i+1]-2,1239);
                Room(names[3+i],b,i==0?"storage":"office",0,xs[i]+2,1279,xs[i+1]-2,1324);
            }
            Put(l,Wall(b,true,1301,1183.76f,1241));Put(l,Wall(b,true,1301,1277,1326));
            Pair(l,27,28,true,1348.884f,1259,1.8f);
            Window(l,b,true,1348.884f,1192,1230);Window(l,b,true,1348.884f,1286,1316);
        }
        static void Pedagogy(L l)
        {
            const string b="pedagogy_south";Reset(l,b);l.windows=l.windows.Where(w=>w.block!=b).ToArray();
            Box(l,b,1493.8f,1183.46f,1705.48f,1326.26f);
            Put(l,Wall(b,false,1242,1493.8f,1705.48f));Put(l,Wall(b,false,1278,1493.8f,1705.48f));
            var slots=l.doors.Select((d,i)=>new{d,i}).Where(v=>v.d.block==b&&v.i!=34&&v.i!=35).Select(v=>v.i).ToArray();
            string[] names={"Coordenação pedagógica","Orientação educacional","Atendimento individual","Apoio de inclusão","Sala de planejamento","Arquivo pedagógico","Sanitário acessível","Copa dos funcionários"};
            for(int i=0;i<4;i++)
            {
                float a=Mathf.Lerp(1493.8f,1705.48f,i/4f),z=Mathf.Lerp(1493.8f,1705.48f,(i+1)/4f);
                if(i>0){Put(l,Wall(b,true,a,1183.46f,1242));Put(l,Wall(b,true,a,1278,1326.26f));}
                Door(l,slots[i],false,1242,(a+z)*.5f,1.0f);Door(l,slots[4+i],false,1278,(a+z)*.5f,1.0f);
                Window(l,b,false,1183.46f,a+9,z-9);Window(l,b,false,1326.26f,a+9,z-9);
                Room(names[i],b,"office",0,a+2,1185.46f,z-2,1240);
                Room(names[4+i],b,i==1?"storage":i==2?"bathroom":i==3?"kitchen":"office",0,a+2,1280,z-2,1324.26f);
            }
            Pair(l,34,35,true,1493.8f,1260,1.8f);
        }
        static void Services(L l)
        {
            const string b="central_east_services";Reset(l,b);
            l.windows=l.windows.Where(w=>w.block!=b||!w.vertical).ToArray();
            Box(l,b,1320.64f,808.64f,1605.64f,960.52f);
            Put(l,Wall(b,true,1464.16f,808.64f,960.52f));Put(l,Wall(b,false,853.921f,1464.16f,1605.64f));
            Door(l,38,true,1464.16f,889.975f);Door(l,39,false,853.921f,1543.82f);Door(l,40,true,1320.64f,837,1.4f);
            NewDoor(l,b,false,808.64f,1430,1.40f);
            // Hall entrance is owned by main_hall; reserve its complete paired aperture.
            l.walls=l.walls.Where(w=>!(w.source==Source&&w.block==b&&!w.vertical&&Mathf.Abs(w.p-960.52f)<.02f)).ToArray();
            Put(l,Wall(b,false,960.52f,1320.64f,1409.90f));Put(l,Wall(b,false,960.52f,1445.30f,1605.64f));
            Window(l,b,true,1464.16f,913.16f,941.48f);
            Window(l,b,true,1605.64f,817,840);Window(l,b,true,1605.64f,900,932);
            // Re-anchor the native south sashes to the continuous facade.
            foreach(var w in l.windows.Where(w=>w.block==b&&!w.vertical&&w.p>950))w.p=960.52f;
            Room("Cozinha da cantina",b,"kitchen",0,1466.16f,855.921f,1603.64f,958.52f);
            Room("Despensa e serviço",b,"storage",0,1466.16f,810.64f,1603.64f,851.921f);
        }
        static void Library(L l)
        {
            const string b="proposed_library";Reset(l,b);Box(l,b,1900.66f,776.585f,2108.32f,983.24f);
            Pair(l,46,47,false,776.585f,2004.4f,1.94f);
            foreach(var w in l.windows.Where(w=>w.block==b)){if(w.vertical)w.p=1900.66f;else w.p=776.585f;}
            Window(l,b,true,2108.32f,808,855);Window(l,b,true,2108.32f,892,946);
            Room("Biblioteca",b,"library",0,1902.66f,778.585f,2106.32f,981.24f);
        }
        static void Workshops(L l)
        {
            const string b="proposed_northeast_laboratories";Reset(l,b);
            // Six deep technical rooms and one open automotive work bay. Its low
            // front wall, large sliding gate and open right access remain photo-owned.
            float[] xs={2111.68f,2196,2282.8f,2369.32f,2453.38f,2538.48f,2623.48f,2709.64f};
            Put(l,Wall(b,true,xs[0],316.4f,526.88f));Put(l,Wall(b,false,316.4f,xs[0],xs[7]));
            Put(l,Wall(b,true,xs[7],316.4f,422.36f));
            for(int i=1;i<6;i++)Put(l,Wall(b,true,xs[i],316.4f,526.88f));
            Put(l,Wall(b,true,xs[6],316.4f,453.56f));Put(l,Wall(b,false,422.36f,xs[6],xs[7]));
            Put(l,Wall(b,false,526.88f,xs[0],2539.5f));
            string[] names={"Laboratório de materiais","Laboratório de ensaios","Oficina de ferramentas","Laboratório de construção","Sala de desenho técnico","Oficina Automotiva"};
            for(int i=0;i<6;i++)
            {Room(names[i],b,"lab",0,xs[i]+2,318.4f,xs[i+1]-2,524.88f);if(i==5){rooms.Last().openSouth=true;rooms.Last().openEastAfterY=453.56f;}}
            Room("Depósito da oficina",b,"storage",0,xs[6]+2,318.4f,xs[7]-2,420.36f);
            Pair(l,43,77,false,526.88f,(xs[0]+xs[1])*.5f,1.94f);
            Pair(l,45,79,false,526.88f,(xs[1]+xs[2])*.5f,1.94f);
            Pair(l,44,78,false,526.88f,(xs[2]+xs[3])*.5f,1.94f);
            NewDoor(l,b,false,526.88f,(xs[3]+xs[4])*.5f);NewDoor(l,b,false,526.88f,(xs[4]+xs[5])*.5f);
            Door(l,49,false,422.36f,2667,1.1f);
            // Door bays replace glazing where an access now exists.
            var windows=new List<S>();
            foreach(var w in l.windows.Where(w=>w.block==b))
            {
                if(w.vertical)continue;
                w.p=w.p<400?316.4f:526.88f;
                var ds=l.doors.Where(d=>d.block==b&&!d.vertical&&Mathf.Abs(d.p-w.p)<1).ToArray();
                if(ds.Any(d=>d.a<w.b+1&&d.b>w.a-1))continue;
                windows.Add(w);
            }
            l.windows=l.windows.Where(w=>w.block!=b).Concat(windows).ToArray();
        }
        static void EastNorth(L l)
        {
            const string b="east_north_classrooms";Reset(l,b);l.windows=l.windows.Where(w=>w.block!=b).ToArray();
            Box(l,b,1545.28f,315.32f,1643.8f,723.56f);
            float[] ys={315.32f,417.44f,519.44f,621.44f,672.5f,723.56f};
            for(int i=1;i<5;i++)Put(l,Wall(b,false,ys[i],1545.28f,1643.8f));
            for(int i=0;i<3;i++){Door(l,54+i,true,1643.8f,ys[i]+81);Window(l,b,true,1545.28f,ys[i]+15,ys[i+1]-15);
                Room("Sala "+(52-i),b,"class",0,1547.28f,ys[i]+2,1641.8f,ys[i+1]-2);}
            for(int i=3;i<5;i++){NewDoor(l,b,true,1643.8f,(ys[i]+ys[i+1])*.5f,1.0f);Window(l,b,true,1545.28f,ys[i]+10,ys[i+1]-10);
                Room(i==3?"Sanitário feminino — ala leste":"Sanitário masculino — ala leste",b,"bathroom",0,1547.28f,ys[i]+2,1641.8f,ys[i+1]-2);}
        }
        static void SouthWings(SchoolMapBuilder.Layout layout)
        {
            foreach(var l in layout.levels)
            {
                foreach(string b in new[]{"main_coordination_south","main_auditorium_south"})
                {
                    float x0=b=="main_coordination_south"?830.92f:1078.916f,x1=b=="main_coordination_south"?958.404f:1207.78f;
                    if(b=="main_coordination_south"||l.index==0)
                    {
                        Reset(l,b);Box(l,b,x0,848.0f,x1,1062.08f);
                        foreach(var w in l.windows.Where(w=>w.block==b&&w.vertical))w.p=w.p<(x0+x1)*.5f?x0:x1;
                        foreach(var w in l.windows.Where(w=>w.block==b&&!w.vertical&&w.p>1050))w.p=1062.08f;
                    }
                    else
                    {
                        Reset(l,b);Box(l,b,x0,848,x1,1062.08f);
                        Put(l,Wall(b,true,1116.92f,848,936.074f));Put(l,Wall(b,false,936.074f,x0,x1));
                        // Keep both leaves north of the perpendicular ramp
                        // enclosure; the former free edge entered that wall.
                        Pair(l,13,30,true,x0,872,1.8f);
                        Door(l,23,true,1116.92f,859.54f);Door(l,24,false,936.074f,1097.92f);
                        foreach(var w in l.windows.Where(w=>w.block==b&&w.vertical))w.p=x1;
                    }
                    Room(b=="main_coordination_south"?(l.index==0?"Coordenação e atendimento":"Laboratório 28"):
                        (l.index==0?"Auditório":"Sala 27B"),b,b=="main_coordination_south"?(l.index==0?"office":"lab"):(l.index==0?"assembly":"class"),l.index,
                        x0+2,l.index==1&&b=="main_auditorium_south"?938:850,x1-2,1060.08f);
                    if(l.index==1&&b=="main_auditorium_south")Room("Sala 27A",b,"class",1,1118.92f,850,1205.78f,931.32f);
                }
            }
        }
        static void EastTeaching(SchoolMapBuilder.Layout layout)
        {
            // Keep the functional stair envelope. Rebuild room banks around it,
            // removing disconnected dimension lines and filling missing room edges.
            foreach(var l in layout.levels)
            {
                const string b="proposed_east_teaching";Reset(l,b);
                Put(l,Wall(b,true,2290.84f,824.84f,1048));Put(l,Wall(b,true,2393,824.84f,1048));
                Put(l,Wall(b,true,2418,774.6f,1048));Put(l,Wall(b,true,2520.4f,774.6f,1048));
                foreach(float y in new[]{824.84f,927,1048}){Put(l,Wall(b,false,y,2290.84f,2393));Put(l,Wall(b,false,y,2418,2520.4f));}
                Put(l,Wall(b,false,774.6f,2418,2520.4f));
                foreach(var d in l.doors.Where(d=>d.block==b&&d.vertical)){if(d.p<2400)d.p=2393;else d.p=2418;}
                for(int i=0;i<2;i++)
                {
                    float a=i==0?824.84f:927,z=i==0?927:1048;
                    Window(l,b,true,2290.84f,a+16,z-16);Window(l,b,true,2520.4f,a+16,z-16);
                    Room(l.index==0?"Laboratório "+(70+i):"Sala "+(74+i),b,l.index==0?"lab":"class",l.index,2292.84f,a+2,2391,z-2);
                    Room(l.index==0?"Laboratório "+(72+i):"Sala "+(76+i),b,l.index==0?"lab":"class",l.index,2420,a+2,2518.4f,z-2);
                }
                // The accessible WC uses the north-east room, with a real corridor access.
                NewDoor(l,b,true,2418,813,1.10f);
                Room("Sanitário acessível — ala técnica",b,"bathroom",l.index,2420,776.6f,2518.4f,822.84f);
            }
        }
        public static void AdaptRoomCatalog(SchoolMapBuilder.Rooms data)
        {
            var source=rooms.Count>0?rooms.ToArray():JsonUtility.FromJson<Report>(File.ReadAllText(SchoolMapBuilder.Output+"/School-architecture-redesign.json")).roomLayout;
            data.rooms=source.Select(r=>new SchoolMapBuilder.Room{name=r.name,kind=r.kind,level=r.level,box=(float[])r.box.Clone()}).ToArray();
        }
        static void MiddleCirculation(SchoolMapBuilder.Layout layout)
        {
            // Teaching banks and south rooms own their boundaries. The cross
            // corridor needs only its western portal, not parallel CAD fragments
            // inside those rooms or a closed end towards the main covered walkway.
            foreach(var l in layout.levels)
            {
                const string b="main_middle_circulation";
                var rampEnclosures=l.walls.Where(w=>w.block==b&&w.source==SchoolCentralEnclosurePass.Source).ToArray();
                Reset(l,b);
                l.walls=l.walls.Concat(rampEnclosures).ToArray();
                l.windows=l.windows.Where(w=>w.block!=b).ToArray();
                Put(l,Wall(b,true,832.264f,805,853));
            }
        }
        public static void Apply(Transform architecture,Transform parent)
        {
            furniture=parent;Setup();Physics.SyncTransforms();
            // Old generic benches and boards no longer define a room's function.
            foreach(var group in new[]{architecture.Find("Mobiliário inicial — 0"),architecture.Find("Mobiliário inicial — 1"),architecture.Find("Biblioteca — mobiliário inicial")}.Where(t=>t))
                foreach(Transform child in group.Cast<Transform>().ToArray())if(!child.GetComponent<Light>()&&!child.name.StartsWith("Luminária"))UnityEngine.Object.DestroyImmediate(child.gameObject);
            foreach(var spec in rooms.Where(r=>r.kind!="class"))Furnish(spec);
            CloseGroundWallTops(architecture);CanteenRoof(architecture);ClearHallEntrances(architecture);Physics.SyncTransforms();
            File.WriteAllText(SchoolMapBuilder.Output+"/School-architecture-redesign.json",JsonUtility.ToJson(report,true));
        }
        static Material Mat(string name,Color color,float metallic=0)
        {
            string path=SchoolMapBuilder.Folder+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.20f);EditorUtility.SetDirty(m);return m;
        }
        static void Setup()
        {white=Mat("ArchitectureFurnitureWhite",new Color(.72f,.72f,.67f));steel=Mat("ArchitectureFurnitureSteel",new Color(.18f,.21f,.22f),.5f);
            wood=Mat("ArchitectureFurnitureWood",new Color(.34f,.25f,.15f));navy=Mat("ArchitectureFurnitureBlue",new Color(.035f,.10f,.26f));
            green=Mat("ArchitectureFurnitureGreen",new Color(.03f,.17f,.10f));porcelain=Mat("ArchitecturePorcelain",new Color(.86f,.86f,.79f));}
        static GameObject Cube(Transform parent,string name,Vector3 center,Vector3 size,Material mat,bool collision=false)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,true);go.transform.position=center;go.transform.localScale=size;
            go.isStatic=true;go.GetComponent<Renderer>().sharedMaterial=mat;if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
        static void Beam(Transform parent,string name,Vector3 a,Vector3 b,float thick,Material mat)
        {var g=Cube(parent,name,(a+b)*.5f,new Vector3(thick,thick,Vector3.Distance(a,b)),mat);g.transform.rotation=Quaternion.LookRotation(b-a);}
        static void Table(Transform t,Vector3 p,Vector2 size,string name)
        {
            Cube(t,name,p+Vector3.up*.76f,new Vector3(size.x,.07f,size.y),white,true);
            foreach(float x in new[]{-size.x*.40f,size.x*.40f})foreach(float z in new[]{-size.y*.37f,size.y*.37f})
                Cube(t,"Pé metálico",p+new Vector3(x,.36f,z),new Vector3(.05f,.72f,.05f),steel);
        }
        static void Chair(Transform t,Vector3 p,Quaternion q)
        {Cube(t,"Cadeira — assento",p+Vector3.up*.45f,new Vector3(.42f,.05f,.42f),navy);
            var back=Cube(t,"Cadeira — encosto",p+q*new Vector3(0,.72f,-.18f),new Vector3(.42f,.43f,.05f),navy);back.transform.rotation=q;
            foreach(float x in new[]{-.17f,.17f})foreach(float z in new[]{-.17f,.17f})Cube(t,"Cadeira — estrutura",p+q*new Vector3(x,.22f,z),new Vector3(.03f,.44f,.03f),steel);}
        static void Shelf(Transform t,Vector3 p,float width,string name,bool faceNorth=false)
        {
            var bounds=new GameObject(name+" — volume físico");bounds.transform.SetParent(t,false);bounds.transform.position=p+Vector3.up*.98f;
            bounds.isStatic=true;bounds.AddComponent<BoxCollider>().size=new Vector3(width,1.96f,.43f);
            Cube(t,name+" — fundo",p+new Vector3(0,.98f,faceNorth?-.19f:.19f),new Vector3(width,1.96f,.035f),wood);
            foreach(float side in new[]{-1f,1f})Cube(t,name+" — lateral",p+new Vector3(side*(width*.5f-.025f),.98f,0),new Vector3(.05f,1.96f,.43f),wood);
            for(int i=0;i<5;i++)
            {
                Cube(t,name+" — prateleira",p+new Vector3(0,.16f+i*.40f,0),new Vector3(width,.045f,.43f),white);
                if(name.Contains("livros"))for(int j=0;j<12;j++)Cube(t,"Biblioteca — livro",p+new Vector3(-width*.43f+j*width*.075f,.31f+i*.40f,-.01f),new Vector3(width*.06f,.26f,.27f),j%3==0?navy:j%3==1?green:wood);
            }
        }
        static void Furnish(RoomSpec r)
        {
            var g=new GameObject(r.name+" — função escolar").transform;g.SetParent(furniture,false);report.usefulFurnitureGroups++;
            float[] b=SchoolCampusConnectionsPass.Box(r.block,r.box);b=SchoolMapBuilder.CorridorBox(r.block,r.level,b);
            float h=SchoolMapBuilder.OccupiedFloorHeight(r.block,r.level);
            Vector3 nw=P(b[0],b[1],h),se=P(b[2],b[3],h);float width=se.x-nw.x,depth=nw.z-se.z;
            Vector3 center=(nw+se)*.5f;
            var roomDoors=furniture.parent.GetComponentsInChildren<InteractiveDoor>(true).Where(d=>d.leafCollider
                && Mathf.Abs(d.leafCollider.bounds.min.y-h)<.16f
                && d.leafCollider.bounds.center.x>=nw.x-.40f&&d.leafCollider.bounds.center.x<=se.x+.40f
                && d.leafCollider.bounds.center.z>=se.z-.40f&&d.leafCollider.bounds.center.z<=nw.z+.40f).ToArray();
            bool leftEntrance=roomDoors.Any(d=>Mathf.Abs(d.leafCollider.bounds.center.x-nw.x)<.45f);
            bool southEntrance=roomDoors.Any(d=>Mathf.Abs(d.leafCollider.bounds.center.z-se.z)<.45f);
            bool northEntrance=roomDoors.Any(d=>Mathf.Abs(d.leafCollider.bounds.center.z-nw.z)<.45f);
            if(r.kind=="bathroom")
            {
                // Fixtures occupy the back wall, leaving a full central turning area.
                int count=r.name.Contains("acessível")?1:Mathf.Clamp(Mathf.FloorToInt(((leftEntrance?depth:width)-1.1f)/1.25f),1,3);
                for(int i=0;i<count;i++)
                {
                    Vector3 p=leftEntrance?new Vector3(se.x-.72f,h,se.z+.85f+i*1.25f):new Vector3(se.x-.85f-i*1.25f,h,se.z+.72f);
                    var cabin=new GameObject("Sanitário — conjunto de cabine").transform;cabin.SetParent(g,false);cabin.position=p;
                    Cube(cabin,"Sanitário — base",p+Vector3.up*.20f,new Vector3(.40f,.40f,.57f),porcelain,true);
                    Cube(cabin,"Sanitário — assento",p+Vector3.up*.43f,new Vector3(.46f,.06f,.62f),porcelain);
                    Cube(cabin,"Sanitário — caixa",p+new Vector3(0,.68f,-.24f),new Vector3(.46f,.65f,.17f),porcelain);
                    if(i<count-1)Cube(cabin,"Sanitário — divisória de cabine",p+new Vector3(-.62f,1.0f,.12f),new Vector3(.07f,2f,1.40f),white,true);
                    if(leftEntrance)cabin.rotation=Quaternion.Euler(0,-90,0);
                }
                Vector3 sink=northEntrance?new Vector3(nw.x+.48f,h,center.z):new Vector3(center.x,h,nw.z-.48f);
                var sinkGroup=new GameObject("Sanitário — conjunto de lavatórios").transform;sinkGroup.SetParent(g,false);sinkGroup.position=sink;
                float sinkWidth=Mathf.Min((northEntrance?depth:width)-1,1.5f);
                Cube(sinkGroup,"Sanitário — bancada de lavatórios",sink+Vector3.up*.82f,new Vector3(sinkWidth,.15f,.48f),porcelain,true);
                Cube(sinkGroup,"Sanitário — espelho",sink+new Vector3(0,1.55f,.23f),new Vector3(sinkWidth,.85f,.025f),steel);
                foreach(float x in new[]{-sinkWidth*.3f,sinkWidth*.3f})Cube(sinkGroup,"Sanitário — torneira",sink+new Vector3(x,.98f,.10f),new Vector3(.035f,.19f,.06f),steel);
                if(northEntrance)sinkGroup.rotation=Quaternion.Euler(0,-90,0);
                if(r.name.Contains("acessível")){Beam(g,"Sanitário — barra de apoio",new Vector3(nw.x+.30f,h+.75f,se.z+.25f),new Vector3(nw.x+.30f,h+.75f,se.z+1.20f),.045f,steel);}
            }
            else if(r.kind=="library")
            {
                for(int i=0;i<6;i++)Shelf(g,new Vector3(nw.x+1.15f+i*1.9f,h,se.z+.35f),1.55f,"Biblioteca — estante de livros",true);
                for(int row=0;row<2;row++)for(int column=0;column<2;column++)
                {Vector3 p=new Vector3(nw.x+3+column*5.5f,h,nw.z-4.5f-row*4.5f);Table(g,p,new Vector2(2.2f,1.2f),"Biblioteca — mesa de leitura");
                    foreach(float x in new[]{-.65f,.65f}){Chair(g,p+new Vector3(x,0,-1.05f),Quaternion.identity);Chair(g,p+new Vector3(x,0,1.05f),Quaternion.Euler(0,180,0));}}
                Table(g,new Vector3(se.x-2,h,nw.z-1.2f),new Vector2(2.1f,.70f),"Biblioteca — balcão de empréstimos");
            }
            else if(r.kind=="assembly")
            {
                Vector3 stage=new Vector3(center.x,h,se.z+1.5f);Cube(g,"Auditório — palco baixo",stage+Vector3.up*.12f,new Vector3(width-1,.24f,2f),wood,true);
                Cube(g,"Auditório — tela",stage+new Vector3(0,2.0f,-.8f),new Vector3(width-2,1.8f,.05f),white);
                for(int row=0;row<7;row++)for(int col=0;col<6;col++)
                {float x=center.x+(col<3?-.9f-(2-col)*.75f:.9f+(col-3)*.75f);Chair(g,new Vector3(x,h,nw.z-3-row*1.15f),Quaternion.Euler(0,180,0));}
            }
            else if(r.kind=="kitchen")
            {
                Vector3 back=new Vector3(center.x,h,se.z+.45f);
                Cube(g,"Cozinha — bancada de preparo",back+Vector3.up*.88f,new Vector3(Mathf.Max(1.5f,width-1),.14f,.70f),white,true);
                Cube(g,"Cozinha — armário sob bancada",back+Vector3.up*.40f,new Vector3(Mathf.Max(1.5f,width-1),.75f,.64f),steel,true);
                if(depth>5)
                {Cube(g,"Cozinha — fogão industrial",new Vector3(se.x-.68f,h+.43f,center.z),new Vector3(1.05f,.86f,1.5f),steel,true);
                    Cube(g,"Cozinha — coifa",new Vector3(se.x-.68f,h+2.10f,center.z),new Vector3(1.25f,.35f,1.7f),steel);
                    Cube(g,"Cozinha — duto da coifa até a cobertura",new Vector3(se.x-.68f,h+2.79f,center.z),new Vector3(.45f,1.05f,.50f),steel);
                    Cube(g,"Cozinha — refrigerador",new Vector3(nw.x+.8f,h+1,nw.z-.8f),new Vector3(1.10f,2f,.80f),white,true);
                    Table(g,center,new Vector2(2.1f,.8f),"Cozinha — ilha de preparo");}
                Cube(g,"Cozinha — cuba da pia",back+new Vector3(-.5f,.95f,0),new Vector3(.65f,.10f,.48f),steel);
                Cube(g,"Cozinha — torneira",back+new Vector3(-.5f,1.10f,.22f),new Vector3(.035f,.28f,.04f),steel);
            }
            else if(r.kind=="storage"||r.name.Contains("Depósito")||r.name.Contains("Almoxarifado")||r.name.Contains("Arquivo")||r.name.Contains("Despensa"))
            {
                int count=Mathf.Max(1,Mathf.FloorToInt((width-.6f)/1.5f));
                for(int i=0;i<count;i++)
                {Vector3 p=new Vector3(nw.x+.8f+i*1.5f,h,southEntrance?nw.z-.35f:se.z+.35f);Shelf(g,p,1.25f,"Armazenamento — estante",!southEntrance);
                    for(int k=0;k<3;k++)Cube(g,"Armazenamento — caixa",p+new Vector3((k-1)*.33f,.55f,0),new Vector3(.28f,.31f,.31f),white);}
            }
            else if(r.kind=="office"||r.kind=="support")
            {
                Vector3 desk=new Vector3(center.x,h,southEntrance?nw.z-1.65f:se.z+1.35f);Table(g,desk,new Vector2(Mathf.Min(1.8f,width-1),.75f),"Apoio — mesa de trabalho");
                Chair(g,desk+Vector3.back*.8f,Quaternion.identity);
                Cube(g,"Apoio — monitor",desk+new Vector3(.35f,1.04f,0),new Vector3(.42f,.32f,.07f),steel);
                Shelf(g,new Vector3(se.x-.7f,h,nw.z-.45f),1.0f,"Apoio — armário");
                Chair(g,new Vector3(nw.x+.7f,h,se.z+1.5f),Quaternion.Euler(0,90,0));
            }
            else
            {
                // Perimeter workstations leave a continuous central aisle and doorway strip.
                for(int i=0;i<Mathf.Min(4,Mathf.FloorToInt((depth-2)/2f));i++)
                {
                    Vector3 p=new Vector3(leftEntrance?se.x-1.0f:nw.x+1.0f,h,nw.z-1.8f-i*2);
                    Table(g,p,new Vector2(1.45f,.70f),"Laboratório — bancada técnica");
                    if(r.name.Contains("Informática")){Cube(g,"Informática — monitor",p+Vector3.up*1.0f,new Vector3(.45f,.34f,.06f),steel);Chair(g,p+Vector3.back*.85f,Quaternion.identity);}
                    else Cube(g,"Laboratório — equipamento de bancada",p+Vector3.up*.95f,new Vector3(.43f,.32f,.40f),steel);
                }
                Shelf(g,new Vector3(leftEntrance?se.x-1:nw.x+1,h,se.z+.36f),1.5f,"Laboratório — armário de material",true);
                Cube(g,"Laboratório — quadro",new Vector3(center.x,h+1.75f,nw.z-.08f),new Vector3(Mathf.Min(3,width-1),1.20f,.055f),green);
            }
        }
        static void CloseGroundWallTops(Transform architecture)
        {
            // Two-storey slabs have their underside at3.61m. Seal the26cm band
            // above the original3.35m masonry without moving any opening.
            foreach(string name in new[]{"main_laboratories_north","main_classrooms_north","main_middle_circulation","main_coordination_south","main_auditorium_south","proposed_east_teaching"})
            {
                var block=architecture.Find(name);if(!block)continue;
                foreach(var c in block.GetComponentsInChildren<BoxCollider>(true).Where(c=>(c.name.StartsWith("Parede")||c.name.StartsWith("Verga"))&&Mathf.Abs(c.bounds.max.y-3.35f)<.015f).ToArray())
                {
                    var b=c.bounds;var renderer=c.GetComponent<MeshRenderer>();var mat=renderer?renderer.sharedMaterials.LastOrDefault():white;
                    Cube(block,"Parede — fecho até a laje",new Vector3(b.center.x,3.48f,b.center.z),new Vector3(b.size.x,.26f,b.size.z),mat?mat:white,true);report.wallTopClosures++;
                }
            }
        }
        static void CanteenRoof(Transform architecture)
        {
            var block=architecture.Find("central_east_services");var old=block.Find("Cobertura alta da cantina");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var detail=architecture.Find("Detalhes do refeitório");
            if(detail)foreach(var t in detail.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Refeitório — nervura inferior")||t.name.StartsWith("Refeitório — banzo")||t.name.StartsWith("Refeitório — montante")||t.name.StartsWith("Refeitório — diagonal")).ToArray())if(t)UnityEngine.Object.DestroyImmediate(t.gameObject);
            var g=new GameObject("Cantina — cobertura inclinada alta").transform;g.SetParent(architecture,false);
            var metal=AssetDatabase.LoadAssetAtPath<Material>(SchoolMapBuilder.Folder+"/Materials/PhotoRoofGrey.mat");
            const float west=1318.48f,east=1464.16f,north=801.68f,south=960.52f;
            const float ridge=1421f;
            float Height(float x)=>x<=ridge?5.10f+(x-1345f)/(ridge-1345f)*1.30f:6.40f-(x-ridge)/(1497f-ridge)*1.30f;
            if(detail)
            {
                foreach(Transform t in detail.Cast<Transform>().Where(t=>t.name=="Refeitório — corpo de plafon"||t.name=="Refeitório — difusor luminoso"||t.name=="Refeitório — iluminação sob tesoura"||t.name=="Refeitório — eletroduto suspenso").ToArray())
                {
                    if(t.name=="Refeitório — eletroduto suspenso"){UnityEngine.Object.DestroyImmediate(t.gameObject);continue;}
                    float x=1413+t.position.x/Scale,y=1628-t.position.z/Scale;
                    float rise=Height(x)-.30f-3.82f;t.position+=Vector3.up*rise;
                    var light=t.GetComponent<Light>();if(light)light.range=9;
                    if(t.name=="Refeitório — corpo de plafon")Beam(g,"Cantina — fixação de luminária",t.position,P(x,y,Height(x)-.07f),.025f,steel);
                }
            }
            Beam(g,"Cantina — eletroduto longitudinal fixado",P(1392,808.64f,Height(1392)-.30f),P(1392,south,Height(1392)-.30f),.025f,white);
            Cube(g,"Cantina — caixa de ligação na parede",P(1392,809.5f,Height(1392)-.30f),new Vector3(.14f,.12f,.09f),white);
            foreach(float y in new[]{832f,870f,908f,945f})Beam(g,"Cantina — abraçadeira do eletroduto",P(1392,y,Height(1392)-.30f),P(1392,y,Height(1392)-.10f),.018f,steel);
            // Both pitches share the hall's actual ridge and eaves, so the joining
            // wall and roof meet without a rectangular slab or a daylight gap.
            foreach(var range in new[]{new[]{west,ridge},new[]{ridge,east}})
            {
                Vector3 a=P(range[0],(north+south)*.5f,Height(range[0])),b=P(range[1],(north+south)*.5f,Height(range[1]));
                var sheet=Cube(g,"Cantina — telha metálica inclinada",(a+b)*.5f,new Vector3(Vector3.Distance(a,b)+.02f,.12f,(south-north)*Scale+.20f),metal,true);
                sheet.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
            }
            foreach(float y in new[]{north+3,832f,870f,908f,945f,south-2})
            {
                Vector3 a=P(west,y,Height(west)-.23f),peak=P(ridge,y,Height(ridge)-.23f),b=P(east,y,Height(east)-.23f);
                Beam(g,"Cantina — caibro inclinado esquerdo",a,peak,.12f,steel);Beam(g,"Cantina — caibro inclinado direito",peak,b,.12f,steel);
                Beam(g,"Cantina — tirante aparente",a,b,.065f,steel);report.roofFrames++;
                for(int i=1;i<5;i++){float x=Mathf.Lerp(west,east,i/5f);Vector3 bottom=Vector3.Lerp(a,b,i/5f);Beam(g,"Cantina — montante da estrutura",bottom,P(x,y,Height(x)-.23f),.04f,steel);}
            }
            foreach(float x in new[]{west+3,1360f,1390f,ridge,1440f,east-3})Beam(g,"Cantina — terça longitudinal",P(x,north,Height(x)-.11f),P(x,south,Height(x)-.11f),.085f,steel);
            foreach(var r in block.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Parede — reboco"||r.name.StartsWith("Verga da ")).ToArray())
            {
                var bounds=r.bounds;float x0=1413+bounds.min.x/Scale,x1=1413+bounds.max.x/Scale,y=1628-bounds.center.z/Scale;
                if(x0>1465||y>957||bounds.max.y<3.30f)continue;
                x1=Mathf.Min(x1,east);
                if(bounds.size.x>bounds.size.z)
                {
                    var ranges=x0<ridge&&x1>ridge?new[]{new[]{x0,ridge},new[]{ridge,x1}}:new[]{new[]{x0,x1}};
                    foreach(var range in ranges)RoofWall(g,P(range[0],y).x,P(range[1],y).x,bounds.min.z,bounds.max.z,bounds.max.y,Height(range[0])-.06f,Height(range[1])-.06f,white);
                }
                else
                {float top=Height((x0+x1)*.5f)-.06f,extra=top-bounds.max.y;if(extra>.005f)Cube(g,"Cantina — fecho superior",new Vector3(bounds.center.x,bounds.max.y+extra*.5f,bounds.center.z),new Vector3(bounds.size.x,extra,bounds.size.z),white,true);}
            }
            // Close the short western end of the hall partition outside its old
            // 1345..1497 gable footprint, at this roof's matching slope.
            RoofWall(g,P(1320.64f,0).x,P(1345f,0).x,P(0,south).z-.12f,P(0,south).z+.12f,4.20f,Height(1320.64f),5.10f,white);
        }
        static void ClearHallEntrances(Transform architecture)
        {
            Physics.SyncTransforms();
            var entrances=architecture.GetComponentsInChildren<InteractiveDoor>(true).Where(d=>new[]{"school.door.0.27","school.door.0.28","school.door.0.34","school.door.0.35"}.Contains(d.persistentId)).ToArray();
            var reserved=entrances.Select(d=>{var b=d.leafCollider.bounds;b.Expand(new Vector3(3.4f,1f,.90f));return b;}).ToArray();
            foreach(var t in architecture.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Pilar cinza — borda do corredor"||t.name=="Banco de concreto azul").ToArray())
            {
                var colliders=t.GetComponentsInChildren<Collider>();if(colliders.Length==0)continue;
                var bounds=colliders[0].bounds;foreach(var c in colliders.Skip(1))bounds.Encapsulate(c.bounds);
                if(!reserved.Any(b=>b.Intersects(bounds)))continue;
                foreach(float shift in new[]{2.2f,-2.2f,3.6f,-3.6f})
                {
                    var moved=new Bounds(bounds.center+Vector3.forward*shift,bounds.size);
                    if(reserved.Any(b=>b.Intersects(moved)))continue;
                    if(moved.min.z<P(0,1347).z||moved.max.z>P(0,960).z)continue;
                    t.position+=Vector3.forward*shift;report.relocatedHallObjects++;break;
                }
            }
        }
        static void RoofWall(Transform parent,float x0,float x1,float z0,float z1,float bottom,float top0,float top1,Material mat)
        {
            // Horizontal facade wedge; rotate the existing wedge construction
            // mathematically by exchanging X/Z, then persist the actual mesh.
            if(Mathf.Max(top0,top1)<=bottom+.01f)return;
            var v=new[]{new Vector3(x0,bottom,z0),new Vector3(x1,bottom,z0),new Vector3(x0,bottom,z1),new Vector3(x1,bottom,z1),
                new Vector3(x0,Mathf.Max(bottom,top0),z0),new Vector3(x1,Mathf.Max(bottom,top1),z0),new Vector3(x0,Mathf.Max(bottom,top0),z1),new Vector3(x1,Mathf.Max(bottom,top1),z1)};
            SaveWallMesh(parent,"Cantina — fecho inclinado contínuo",v,mat);
        }

        static void Wedge(Transform parent,string name,float x0,float x1,float z0,float z1,float bottom,float top0,float top1,Material mat)
        {
            top0=Mathf.Max(bottom+.005f,top0);top1=Mathf.Max(bottom+.005f,top1);
            var v=new[]{new Vector3(x0,bottom,z0),new Vector3(x1,bottom,z0),new Vector3(x0,bottom,z1),new Vector3(x1,bottom,z1),
                new Vector3(x0,top0,z0),new Vector3(x1,top0,z0),new Vector3(x0,top1,z1),new Vector3(x1,top1,z1)};
            SaveWallMesh(parent,name,v,mat);
        }
        static void SaveWallMesh(Transform parent,string name,Vector3[] v,Material mat)
        {
            var mesh=new Mesh{name=name,vertices=v,triangles=new[]{0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,1,5,4,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,3,7,5}};
            var triangles=mesh.triangles;for(int i=0;i<triangles.Length;i+=3){int tmp=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=tmp;}mesh.triangles=triangles;
            mesh.uv=v.Select(p=>new Vector2(p.z,p.y)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=SchoolMapBuilder.Folder+"/Meshes/Architecture-"+Guid.NewGuid().ToString("N")+".asset";AssetDatabase.CreateAsset(mesh,path);
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.isStatic=true;go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=mat;go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
    }
}
