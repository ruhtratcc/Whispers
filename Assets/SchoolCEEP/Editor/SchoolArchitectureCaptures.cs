// Ferramenta da escola para construção ou acabamento.

using System;
using System.Linq;
namespace SchoolCEEP.Editor { public static class SchoolArchitectureCaptures { public static object Capture() {
var views=new[]{"opposite-block-high-layout","opposite-block-low-layout","upper-right-corridor-layout","refectory-interior-layout",
    "canteen-hall-wall-layout","ramp-walls-roof-layout","west-ramp-joints-layout","workshop-connection-layout","court-interior-layout"};
// The first headless URP request may show unloaded textures/materials.
SchoolCEEP.Editor.SchoolReviewPass.Capture(views[0]);
foreach(var view in views)SchoolCEEP.Editor.SchoolReviewPass.Capture(view);
SchoolCEEP.Editor.SchoolReviewPass.Capture("architecture-admin-layout",SchoolCEEP.Editor.SchoolMapBuilder.Point(1335,1259,2.25f),SchoolCEEP.Editor.SchoolMapBuilder.Point(1080,1259,2.25f));
SchoolCEEP.Editor.SchoolReviewPass.Capture("architecture-pedagogy-layout",SchoolCEEP.Editor.SchoolMapBuilder.Point(1509,1260,2.25f),SchoolCEEP.Editor.SchoolMapBuilder.Point(1685,1260,2.25f));
SchoolCEEP.Editor.SchoolReviewPass.Capture("architecture-kitchen-layout",SchoolCEEP.Editor.SchoolMapBuilder.Point(1490,875,1.7f),SchoolCEEP.Editor.SchoolMapBuilder.Point(1588,940,1.7f));
SchoolCEEP.Editor.SchoolReviewPass.Capture("architecture-library-layout",SchoolCEEP.Editor.SchoolMapBuilder.Point(2004,827,1.65f),SchoolCEEP.Editor.SchoolMapBuilder.Point(2004,985,1.65f));
SchoolCEEP.Editor.SchoolReviewPass.Capture("architecture-auditorium-layout",SchoolCEEP.Editor.SchoolMapBuilder.Point(1140,877,1.65f),SchoolCEEP.Editor.SchoolMapBuilder.Point(1140,1048,1.65f));
SchoolCEEP.Editor.SchoolReviewPass.Capture("architecture-bathroom-layout",SchoolCEEP.Editor.SchoolMapBuilder.Point(874,744,1.65f),SchoolCEEP.Editor.SchoolMapBuilder.Point(945,752,1.65f));
SchoolCEEP.Editor.SchoolReviewPass.Capture("architecture-canteen-roof-layout",SchoolCEEP.Editor.SchoolMapBuilder.Point(1368,842,1.75f),SchoolCEEP.Editor.SchoolMapBuilder.Point(1457,818,5.80f));
SchoolCEEP.Editor.SchoolReviewPass.Capture("architecture-west-approach-roof-layout",SchoolCEEP.Editor.SchoolMapBuilder.Point(700,770,3.4f),SchoolCEEP.Editor.SchoolMapBuilder.Point(870,785,6.8f));
SchoolCEEP.Editor.SchoolReviewPass.Capture("architecture-high-corridor-ceiling-layout",SchoolCEEP.Editor.SchoolMapBuilder.Point(483,914,3.16f),SchoolCEEP.Editor.SchoolMapBuilder.Point(483,1080,5.4f));
SchoolCEEP.Editor.SchoolReviewPass.Capture("upper-classroom-door-layout");
SchoolCEEP.Editor.SchoolReviewPass.Capture("architecture-central-ground-door-layout",SchoolCEEP.Editor.SchoolMapBuilder.Point(978,863,1.65f),SchoolCEEP.Editor.SchoolMapBuilder.Point(980,893,1.65f));
SchoolCEEP.Editor.SchoolReviewPass.Capture("architecture-central-upper-door-layout",SchoolCEEP.Editor.SchoolMapBuilder.Point(1038,861,5.46f),SchoolCEEP.Editor.SchoolMapBuilder.Point(1040,892,5.46f));
SchoolReviewPass.Capture("architecture-central-stair-envelope-layout",SchoolMapBuilder.Point(1074,822,1.65f),SchoolMapBuilder.Point(989,791,2.40f));
SchoolReviewPass.Capture("architecture-east-stair-envelope-layout",SchoolMapBuilder.Point(2250,763,2.20f),SchoolMapBuilder.Point(2335,827,4.80f));
SchoolReviewPass.Capture("architecture-canteen-open-vestibule-layout",SchoolMapBuilder.Point(1430,828,1.65f),SchoolMapBuilder.Point(1430,777,1.65f));
SchoolReviewPass.Capture("architecture-near-canteen-low-ramp-layout",SchoolMapBuilder.Point(1740,780,1.65f),SchoolMapBuilder.Point(1668,719,.65f));
SchoolReviewPass.Capture("architecture-west-low-grades-layout",SchoolMapBuilder.Point(688,900,2.15f),SchoolMapBuilder.Point(483,792,.70f));
SchoolReviewPass.Capture("architecture-entry-mini-stairs-layout",SchoolMapBuilder.Point(1421,1404,1.65f),SchoolMapBuilder.Point(1421,1323,1.45f));
SchoolReviewPass.Capture("architecture-canteen-front-door-removed-layout",SchoolMapBuilder.Point(1437,717,1.65f),SchoolMapBuilder.Point(1390.78f,691,1.65f));
SchoolReviewPass.Capture("architecture-library-garden-limit-layout",SchoolMapBuilder.Point(2295,1231,2.00f),SchoolMapBuilder.Point(1970,1110,1.00f));
return "29 architectural views captured";

} } }
