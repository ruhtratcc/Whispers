// Ferramenta da escola para construção ou acabamento.

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>
    /// Photo finish for the central U stair only. Call after SchoolCirculation and
    /// SchoolPhotoPass, before batching. Existing treads and routes remain intact;
    /// only the two exit guards projecting into the corridor are removed.
    /// SchoolCentralEnclosurePass subsequently adds continuous
    /// collision to four structural masonry skins; round wall rails stay visual.
    /// </summary>
    public static class SchoolCentralStairPhotoPass
    {
        const string GroupName = "Escada central — acabamento das fotos";
        const string Materials = "Assets/SchoolCEEP/Materials/";
        const float Scale = SchoolCirculation.PdfScale;
        const float Upper = SchoolCirculation.FirstFloor;
        const float WestMin = 990.76f, WestMax = 1014.52f;
        const float EastMin = 1022.96f, EastMax = 1046.68f;
        const float SouthY = 809.24f, TurnY = 739.12f, NorthY = 722.12f;
        const float LowerBand = 1.18f, EnclosureHeight = 7.10f;
        const float RailHeight = 1.05f, RailDiameter = .043f, RailInset = .040f;

        [Serializable]
        public sealed class Report
        {
            public bool photographicDimensionsEstimated = true;
            public bool originalTreadsPreserved, originalCollidersPreserved, originalRoutesPreserved;
            public bool eastStairRampAndPatiosUntouched = true;
            public int originalCollidersBefore, originalCollidersAfter, addedColliders, removedProtrudingGuardColliders;
            public int nativeTreads, paleNosings, guardGroups, hiddenGuardRenderers;
            public int masonryPanels, roundRailRuns, wallBrackets, meshes, wallLights;
            public float westFlightWidthMeters, eastFlightWidthMeters, minimumRoundRailClearanceMeters;
            public float northCrossingCenterClearanceMeters, upperGuardHeightMeters, enclosureHeightMeters, ceilingUndersideMeters;
            public string affectedStair = "Main_U_Stair";
            public string[] photoSources, notes;
        }

        public static Report LastReport { get; private set; }
        static Report report;
        static Transform architecture, details;
        static Material white, wallTiles, floorTiles, railSteel, treadDark, pale;
        static string meshFolder;

        public static Report Apply(Transform root, Transform parent)
        {
            if (!root || !parent) throw new ArgumentNullException("Provide school architecture and a photographic-detail parent.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Finish the central stair outside Play mode.");
            if ((root.lossyScale - Vector3.one).sqrMagnitude > .000001f
                || (parent.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                throw new InvalidOperationException("Central stair photo finish requires unit-scale architecture and parent.");
            var main = root.Find("Escadas e rampas/School_Circulation/Main_U_Stair");
            if (!main) throw new InvalidOperationException("Run SchoolCirculation.Build before the central stair photo finish.");
            var treads = main.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.name.StartsWith("Tread_", StringComparison.Ordinal)).ToArray();
            if (treads.Length != 20)
                throw new InvalidOperationException("Expected the registered central U stair's twenty native treads.");
            Vector3[] positions = treads.Select(r => r.transform.position).ToArray();
            Vector3[] scales = treads.Select(r => r.transform.localScale).ToArray();
            Quaternion[] rotations = treads.Select(r => r.transform.rotation).ToArray();
            var routeIds = SchoolCirculation.Routes.Select(r => r.name).ToArray();
            int before = main.GetComponentsInChildren<Collider>(true).Length;

            architecture = root;
            var previous = parent.Find(GroupName);
            if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            details = new GameObject(GroupName) { isStatic = true }.transform;
            details.SetParent(parent, false);
            meshFolder = "Assets/SchoolCEEP/Meshes/CentralStairPhoto-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(meshFolder);
            report = new Report
            {
                originalCollidersBefore = before,
                westFlightWidthMeters = (WestMax - WestMin) * Scale,
                eastFlightWidthMeters = (EastMax - EastMin) * Scale,
                minimumRoundRailClearanceMeters = (EastMax - EastMin) * Scale - 2f * (RailInset + RailDiameter * .5f),
                northCrossingCenterClearanceMeters = (730.62f - NorthY) * Scale - RailInset - RailDiameter * .5f,
                upperGuardHeightMeters = 1.10f, enclosureHeightMeters = EnclosureHeight, ceilingUndersideMeters = 7.05f,
                photoSources = new[]
                {
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-c28aba58-0ea0-48f2-a74d-bc6de7cc9629.png",
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-0259766a-4770-444b-8fd6-f4306e91d56b.png",
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-8a7461d8-610e-4797-9ad8-262907233ab4.png",
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-5a942b5c-cac3-45e8-bd47-9c33243e586a.png",
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-856cd646-7f0d-4e55-a0c5-14fa1a58bd61.png"
                },
                notes = new[]
                {
                    "Only Main_U_Stair is finished. The east U stair, four-lane ramp, patios, doors, grades and story/audio roots are untouched.",
                    "P01/P02 registered central stair opening is approximately x987.52..1049.92 / y722.12..809.24; native flights are x990.76..1014.52 and x1022.96..1046.68, with the turn north of y739.12.",
                    "Twenty original tread objects, transforms and colliders are retained. Dark finish and thin pale noses are visual only; native tread tops remain the walking surface.",
                    "The two upper-exit guards projecting one metre into the transverse corridor are removed, including their twelve colliders. Remaining flight/turn guard renderers are hidden and their collision preserved. This finish stage adds no colliders; SchoolCentralEnclosurePass subsequently adds four continuous masonry colliders.",
                    "Outer side masonry lies in the 0.2286m margins outside the complete tread widths. The central partition occupies the 0.5955m gap between flights and stops at y739.12, leaving the north crossing y730.62 open. Side finishes remain outside tread widths; the north finish follows the existing outer guard band.",
                    "A thin ceiling finish over the stair void has its underside at7.05m, matching the adjacent upper ceiling. No intermediate ceiling is added above the flights.",
                    "The north wall follows the existing outer guard's 0.075m band. No side guards or wall rails extend the upper exit into the transverse corridor; the confirmed mini wall at the lower-flight void remains. Dimensions and finishes are estimates from the photos.",
                    "Round rails project 0.0615m into each flight, within the old 0.0625m toe-guard envelope. Heights and collar details are approximate; the original guard collision is retained and the following enclosure stage supplies continuous wall collision.",
                    "PhotoCentral wall/floor/white/steel materials are reused when available. Native capsule traversal after integration, including the U route and adjacent corridor, remains required. No Play or new narrative events are used."
                }
            };
            MaterialsSetup();
            RemoveProtrudingExitGuards(main);
            HideOldGuardVisuals(main);
            FinishTreads(treads);
            foreach (var landing in main.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.name == "North_Mid_Landing" || r.name == "Ground_South_Approach" || r.name == "Upper_South_Exit"))
            { landing.sharedMaterial = floorTiles; SchoolCentralPhotoPass.WorldUV(landing.GetComponent<MeshFilter>(),.60f,.60f); }
            Enclosure();
            FlightFinish(WestMin, WestMax, SouthY, TurnY, 0f, Upper * .5f, "Lance oeste");
            FlightFinish(EastMin, EastMax, TurnY, SouthY, Upper * .5f, Upper, "Lance leste");
            TurnFinish();
            WallLamp(P(998f,NorthY+1.2f,3.22f));
            WallLamp(P(EastMax-1f,773f,5.68f));

            report.nativeTreads = treads.Length;
            report.originalTreadsPreserved = treads.Select((r, i) => r.transform.position == positions[i]
                && r.transform.localScale == scales[i] && r.transform.rotation == rotations[i]).All(v => v);
            report.originalCollidersAfter = main.GetComponentsInChildren<Collider>(true).Length;
            report.addedColliders = details.GetComponentsInChildren<Collider>(true).Length;
            report.originalCollidersPreserved = report.originalCollidersBefore - report.removedProtrudingGuardColliders == report.originalCollidersAfter && report.addedColliders == 0;
            report.originalRoutesPreserved = routeIds.SequenceEqual(SchoolCirculation.Routes.Select(r => r.name));
            if (!report.originalTreadsPreserved || !report.originalCollidersPreserved || !report.originalRoutesPreserved)
                throw new InvalidOperationException("Central stair photo finish changed registered walking geometry.");
            LastReport = report;
            return report;
        }

        static Material Mat(string name, Color color, float smoothness = .12f, float metallic = 0f)
        {
            string path = Materials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic); AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void MaterialsSetup()
        {
            white = Mat("PhotoCentralWhite", new Color(.76f, .75f, .71f));
            wallTiles = Mat("PhotoCentralWallTiles", new Color(.31f, .285f, .26f), .23f);
            floorTiles = Mat("PhotoCentralFloorTiles", new Color(.34f, .335f, .31f), .20f);
            railSteel = Mat("PhotoCentralRailSteel", new Color(.105f, .12f, .12f), .34f, .65f);
            treadDark = Mat("PhotoCentralStairTread", new Color(.105f, .11f, .105f), .16f);
            pale = Mat("PhotoCentralStairNosing", new Color(.52f, .505f, .46f), .16f);
        }

        static Vector3 P(float x, float y, float height = 0f) => architecture.TransformPoint(SchoolCirculation.PdfPoint(x, y, height));
        static Vector3 Axis(Vector3 value) => architecture.TransformDirection(value);

        static GameObject Cube(string name, Vector3 center, Vector3 size, Material material, Quaternion rotation)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name; item.isStatic = true; item.transform.SetParent(details, false);
            item.transform.position = center; item.transform.rotation = rotation; item.transform.localScale = size;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            report.meshes++; return item;
        }

        static void RemoveProtrudingExitGuards(Transform main)
        {
            foreach(string name in new[]{"Upper_Exit_Left","Upper_Exit_Right"})
            {
                var guard=main.Find(name);
                if(!guard)throw new InvalidOperationException("Missing upper exit guard: "+name);
                int count=guard.GetComponentsInChildren<Collider>(true).Length;
                if(count!=6)throw new InvalidOperationException("Expected six corridor-projecting colliders per exit guard.");
                report.removedProtrudingGuardColliders+=count;
                UnityEngine.Object.DestroyImmediate(guard.gameObject);
            }
        }

        static void HideOldGuardVisuals(Transform main)
        {
            string[] names = { "West_Flight/Flight_Left", "West_Flight/Flight_Right", "East_Flight/Flight_Left", "East_Flight/Flight_Right",
                "North_Mid_Edge", "North_West_Edge", "North_East_Edge" };
            foreach (string path in names)
            {
                var guard = main.Find(path);
                if (!guard) throw new InvalidOperationException("Missing registered central stair guard: " + path);
                report.guardGroups++;
                foreach (var renderer in guard.GetComponentsInChildren<MeshRenderer>(true))
                { renderer.enabled = false; report.hiddenGuardRenderers++; }
            }
        }

        static void FinishTreads(MeshRenderer[] treads)
        {
            foreach (var tread in treads)
            {
                tread.sharedMaterial = treadDark;
                Vector3 size = tread.transform.localScale;
                Vector3 leading = tread.transform.position + tread.transform.up * (size.y * .5f + .003f)
                    - tread.transform.forward * (size.z * .5f - .0225f);
                Cube("Degrau — nariz claro", leading, new Vector3(size.x - .025f, .006f, .045f), pale, tread.transform.rotation);
                report.paleNosings++;
            }
        }

        static void Enclosure()
        {
            float run = (SouthY - NorthY) * Scale;
            // Outer walls occupy the registered side margins, beyond the tread edges.
            foreach (var side in new[] { new[] { WestMin, -1f }, new[] { EastMax, 1f } })
            {
                Vector3 center = P(side[0], (NorthY + SouthY) * .5f, EnclosureHeight * .5f) + Axis(Vector3.right) * (side[1] * .112f);
                Cube("Escada — alvenaria lateral branca", center, new Vector3(.20f, EnclosureHeight, run), white, architecture.rotation);
                report.masonryPanels++;
            }
            // This divider never crosses the physical north turn.
            Cube("Escada — divisória central sólida", P((WestMax + EastMin) * .5f, (TurnY + SouthY) * .5f, EnclosureHeight * .5f),
                new Vector3((EastMin - WestMax) * Scale - .024f, EnclosureHeight, (SouthY - TurnY) * Scale), white, architecture.rotation);
            report.masonryPanels++;
            Cube("Escada — forro superior branco", P((987.52f + 1049.92f) * .5f, (NorthY + SouthY) * .5f, 7.09f),
                new Vector3((1049.92f - 987.52f) * Scale, .08f, run), white, architecture.rotation);
            Cube("Escada — parede norte branca", P((WestMin + EastMax) * .5f, NorthY, EnclosureHeight * .5f),
                new Vector3((EastMax - WestMin) * Scale + .40f, EnclosureHeight, .075f), white, architecture.rotation);
            report.masonryPanels++;
        }

        static void FlightFinish(float xmin, float xmax, float fromY, float toY, float fromHeight, float toHeight, string label)
        {
            foreach (var side in new[] { new[] { xmin, 1f }, new[] { xmax, -1f } })
            {
                Vector3 intoLane = Axis(Vector3.right) * side[1];
                Vector3 a = P(side[0], fromY, fromHeight), b = P(side[0], toY, toHeight);
                // Finish is flush with masonry and entirely outside the tread width.
                Panel(label + " — revestimento inferior", a - intoLane * .006f, b - intoLane * .006f, .010f, LowerBand, wallTiles);
                WallRail(label + " — corrimão redondo", a, b, intoLane);
            }
        }

        static void TurnFinish()
        {
            float level = Upper * .5f;
            Vector3 n0 = P(WestMin, NorthY, level), n1 = P(EastMax, NorthY, level);
            Vector3 w0 = P(WestMin, NorthY, level), w1 = P(WestMin, TurnY, level);
            Vector3 e0 = P(EastMax, NorthY, level), e1 = P(EastMax, TurnY, level);
            Panel("Patamar norte — revestimento de fundo", n0 + Axis(Vector3.back) * .0435f,
                n1 + Axis(Vector3.back) * .0435f, .010f, LowerBand, wallTiles);
            WallRail("Patamar norte — corrimão de fundo", n0, n1, Axis(Vector3.back));
            Panel("Patamar norte — revestimento oeste", w0 - Axis(Vector3.right) * .006f,
                w1 - Axis(Vector3.right) * .006f, .010f, LowerBand, wallTiles);
            WallRail("Patamar norte — corrimão oeste", w0, w1, Axis(Vector3.right));
            Panel("Patamar norte — revestimento leste", e0 - Axis(Vector3.left) * .006f,
                e1 - Axis(Vector3.left) * .006f, .010f, LowerBand, wallTiles);
            WallRail("Patamar norte — corrimão leste", e0, e1, Axis(Vector3.left));
        }

        static void WallRail(string name, Vector3 a, Vector3 b, Vector3 intoLane)
        {
            Vector3 ra = a + intoLane * RailInset + Vector3.up * RailHeight;
            Vector3 rb = b + intoLane * RailInset + Vector3.up * RailHeight;
            Tube(name, ra, rb, RailDiameter, railSteel); report.roundRailRuns++;
            // Short returns and brackets are mounted into the adjacent masonry.
            Tube(name + " — retorno", ra, ra - intoLane * (RailInset + .008f), RailDiameter, railSteel);
            Tube(name + " — retorno", rb, rb - intoLane * (RailInset + .008f), RailDiameter, railSteel);
            int intervals = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 1.25f));
            for (int i = 0; i <= intervals; i++)
            {
                Vector3 foot = Vector3.Lerp(a, b, (float)i / intervals) + Vector3.up * RailHeight;
                Tube(name + " — suporte", foot - intoLane * .008f, foot + intoLane * (RailInset - RailDiameter * .5f), .018f, railSteel);
                Tube(name + " — base na alvenaria", foot - intoLane * .014f, foot - intoLane * .006f, .072f, railSteel);
                report.wallBrackets++;
            }
        }

        static void WallLamp(Vector3 position)
        {
            var glow=Mat("PhotoCentralStairLamp",new Color(.77f,.79f,.73f));
            glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(.82f,.86f,.77f)*1.2f);EditorUtility.SetDirty(glow);
            Cube("Escada — luminária de parede",position,new Vector3(.16f,.12f,.07f),glow,architecture.rotation);
            var go=new GameObject("Escada — luz de parede");go.transform.SetParent(details,false);go.transform.position=position;
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.intensity=.65f;light.range=5.2f;light.color=new Color(.85f,.86f,.76f);light.shadows=LightShadows.None;report.wallLights++;
        }

        static void Tube(string name, Vector3 a, Vector3 b, float diameter, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            item.name = name; item.isStatic = true; item.transform.SetParent(details, false);
            item.transform.position = (a + b) * .5f;
            item.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            item.transform.localScale = new Vector3(diameter, Vector3.Distance(a, b) * .5f, diameter);
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>()); report.meshes++;
        }

        static void Panel(string name, Vector3 a, Vector3 b, float thickness, float height, Material material)
        {
            Vector3 direction = b - a; direction.y = 0f;
            Vector3 normal = Vector3.Cross(Vector3.up, direction.normalized) * (thickness * .5f);
            Vector3[] corners = { a - normal, a + normal, b + normal, b - normal,
                a - normal + Vector3.up * height, a + normal + Vector3.up * height,
                b + normal + Vector3.up * height, b - normal + Vector3.up * height };
            int[][] faces = { new[] { 0, 3, 7, 4 }, new[] { 1, 5, 6, 2 }, new[] { 4, 7, 6, 5 },
                new[] { 0, 1, 2, 3 }, new[] { 0, 4, 5, 1 }, new[] { 3, 2, 6, 7 } };
            Vector3 center = (a + b) * .5f + Vector3.up * (height * .5f);
            var vertices = new Vector3[24]; var uv = new Vector2[24]; var triangles = new int[36];
            for (int face = 0; face < 6; face++)
            {
                int v = face * 4, t = face * 6;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 point = corners[faces[face][i]];
                    vertices[v + i] = point - center;
                    uv[v + i] = new Vector2(Vector3.Dot(point - a, direction.normalized)/.60f, point.y/.30f);
                }
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = name }; mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, meshFolder + "/Panel-" + report.masonryPanels.ToString("D2") + ".asset");
            var item = new GameObject(name) { isStatic = true }; item.transform.SetParent(details, false);
            item.transform.position = center; item.transform.rotation = Quaternion.identity;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
            report.masonryPanels++; report.meshes++;
        }
    }
}
