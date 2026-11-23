// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SchoolCEEP.Editor
{
    /// <summary>
    /// Photographic enclosure of the registered central four-lane ramp only.
    /// Run after SchoolCentralEnclosurePass.Apply and before static batching.
    /// Walking decks, turns, original guard collision and door poses are retained.
    /// </summary>
    public static class SchoolRampPhotoPass
    {
        public const int SourceRevision = 3;
        public const string ReturnGapName = "Rampa — fecho lateral do retorno intermediário";
        public const float ReturnGapNorth = 892.075f, ReturnGapSouth = 907.20f, ReturnGapThickness = .075f;
        const string GroupName = "Rampa central — acabamento das fotos";
        const string Materials = "Assets/SchoolCEEP/Materials/";
        const float Scale = SchoolCirculation.PdfScale, Upper = SchoolCirculation.FirstFloor;
        const float West = 971.92f, East = 1052.08f, North = 920.60f, South = 1034f;
        const float LandingNorth = 892.16f, LandingSouth = 1062.28f;
        const float WallThickness = .15f, WallHeight = 1.05f, CapHeight = .05f;
        const float RailDiameter = .043f, RailProjection = .035f;
        const float RoofWest = 966.64f, RoofEast = 1076.16f, RoofSouth = 1067.20f;
        // The eave underside meets the existing7.32m upper roof exactly.
        const float Eaves = 7.32f, Ridge = 8.90f;
        static readonly string[] OuterGuards =
        {
            "Lane_1_Right", "Lane_4_Right", "Turn_West_1", "Turn_East_3",
            "Turn_Outer_1", "Turn_Outer_2", "Turn_Outer_3", "Upper_Hall_Connector_Right"
        };
        static readonly string[] DeckNames =
        {
            "Inclined_Lane_1", "Inclined_Lane_2", "Inclined_Lane_3", "Inclined_Lane_4",
            "Ground_North_Entry", "Turn_1", "Turn_2", "Turn_3", "Upper_North_Landing", "Upper_Hall_Connector"
        };

        [Serializable] public sealed class Report
        {
            public bool photographicDimensionsEstimated = true;
            public bool originalDecksPreserved, originalCollidersPreserved, originalRoutesPreserved, existingDoorIdsAndPosesPreserved;
            public int nativeDecks, originalCollidersBefore, originalCollidersAfter, addedWallColliders;
            public int hiddenOuterGuardGroups, hiddenOuterGuardRenderers, internalGuardGroupsRetained;
            public int sourceRevision = SourceRevision, hiddenInnerGuardGroups, hiddenInnerGuardRenderers, interiorMasonryPanels, interiorWallMountedRailRuns;
            public int returnGapPanels, returnGapRailRuns;
            public int masonryPanels, capPanels, wallMountedRailRuns, wallRailBrackets, surfaceGrooves;
            public int roofPanels, translucentBands, roofTrusses, roofCorrugations, roofPurlins, generatedMeshes;
            public float parapetAboveWalkingSurfaceMeters = WallHeight + CapHeight;
            public float roofEavesMeters = Eaves, roofRidgeMeters = Ridge, minimumRoofClearanceOverUpperMeters = Eaves - .20f - Upper;
            public float westEntryWallStartsPdfY = 907.20f, upperOuterWallStartsPdfY = SchoolCentralEnclosurePass.BoundaryY + 1.8f;
            public string affectedRamp = "Central_Four_Lane_Ramp";
            public string[] photoSources, notes;
        }

        public static Report LastReport { get; private set; }
        static Report report;
        static Transform architecture, details;
        static Material masonry, pale, silver, roof, steel, amber, concrete;
        static string meshFolder;

        public static Report Apply(Transform root, Transform parent)
        {
            if (!root || !parent) throw new ArgumentNullException("Provide architecture and a ramp-detail parent.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Ramp photo finish requires Edit mode.");
            if ((root.lossyScale - Vector3.one).sqrMagnitude > .000001f || (parent.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                throw new InvalidOperationException("Ramp photo finish requires unit-scale parents.");
            var ramp = root.Find("Escadas e rampas/School_Circulation/Central_Four_Lane_Ramp");
            if (!ramp) throw new InvalidOperationException("Build the registered central ramp first.");
            var decks = DeckNames.Select(n => ramp.Find(n)).ToArray();
            if (decks.Any(t => !t)) throw new InvalidOperationException("Expected all ten registered central ramp decks.");
            var deckPositions = decks.Select(t => t.position).ToArray();
            var deckRotations = decks.Select(t => t.rotation).ToArray();
            var deckScales = decks.Select(t => t.localScale).ToArray();
            var originalColliders = ramp.GetComponentsInChildren<Collider>(true);
            var routes = SchoolCirculation.Routes.Select(r => new SchoolCirculation.Route
                { name = r.name, feet = (Vector3[])r.feet.Clone(), requireAscending = r.requireAscending }).ToArray();
            var doors = root.GetComponentsInChildren<InteractiveDoor>(true);
            var doorIds = doors.Select(d => d.persistentId).ToArray();
            var doorPositions = doors.Select(d => d.transform.position).ToArray();
            var doorRotations = doors.Select(d => d.transform.rotation).ToArray();
            var doorScales = doors.Select(d => d.transform.localScale).ToArray();

            architecture = root;
            var previous = parent.Find(GroupName);
            if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            details = new GameObject(GroupName) { isStatic = true }.transform;
            details.SetParent(parent, false);
            string folder = "RampPhoto-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            meshFolder = "Assets/SchoolCEEP/Meshes/" + folder;
            if (!AssetDatabase.IsValidFolder("Assets/SchoolCEEP/Meshes")) AssetDatabase.CreateFolder("Assets/SchoolCEEP", "Meshes");
            AssetDatabase.CreateFolder("Assets/SchoolCEEP/Meshes", folder);
            report = new Report
            {
                nativeDecks = decks.Length, originalCollidersBefore = originalColliders.Length,
                photoSources = new[]
                {
                    "work/SchoolCEEP/Upper-classrooms-ramp-reference/Reference-6.png",
                    "work/SchoolCEEP/Upper-classrooms-ramp-reference/Reference-7.png",
                    "work/SchoolCEEP/Ramp-both-sides-reference/Correction.png",
                    "work/SchoolCEEP/Ramp-gap-reference/Gap-1.png",
                    "work/SchoolCEEP/Ramp-gap-reference/Gap-2.png"
                },
                notes = new[]
                {
                    "Only the central four-lane ramp receives this photographic enclosure. All ten registered decks, their transforms, every original collider, all three global circulation routes and all door IDs/poses are preserved.",
                    "The user now confirms masonry on both sides of the ramp lanes. Inner dividers follow the higher adjacent deck with rails at0.70/1.00m above each separate lane; all original guard colliders are retained behind the new finish.",
                    "The missing1.067m upper return side at PDFx989/y892.075..907.20 is filled with7.5cm masonry in the old toe-guard envelope. It joins the northern return and existing divider; its base follows the1.905m landing underside and leaves the ground doorway sweep and passage below clear. Two wall-mounted rails follow the east-facing landing only.",
                    "Masonry lies outside the complete native walking widths. Round rail projection stays within the previous guard's toe envelope. The outer guard renderers are hidden; their physical safety collision is retained behind the finish.",
                    "The west ground approach starts its masonry after PDF y907.20, leaving the +95degree ground-door sweep free. The upper east wall starts at the already-approved guard trim y889.76. Neither northern doorway is enclosed across its opening.",
                    "The elevated north return wall stands on its 1.905m deck, leaving the volume beneath that return open. Southern end walls extend down to grade, producing the taller solid outer face visible from the garden.",
                    "The high roof spans the existing gap between the adjacent facades. Two corrugated slopes, exposed triangular steel trusses and amber translucent strips follow the new opposite-end photograph. The roof begins immediately after the approved central roof strip; no low intermediate ceiling is added above the ramp.",
                    "Roof eave underside7.32m meets the adjacent native roof, ridge8.90m. Wall dimensions, strip spacing and finishes are visual estimates from the photos, not surveyed dimensions. Integration must validate the ramp route, all three turns and both doorway swings with native physics before saving."
                }
            };
            SetupMaterials();
            foreach (string name in OuterGuards)
            {
                var guard = ramp.Find(name);
                if (!guard) throw new InvalidOperationException("Missing original outer guard: " + name);
                foreach (var renderer in guard.GetComponentsInChildren<MeshRenderer>(true))
                { renderer.enabled = false; report.hiddenOuterGuardRenderers++; }
                report.hiddenOuterGuardGroups++;
            }
            report.internalGuardGroupsRetained = ramp.Cast<Transform>().Count(t => !OuterGuards.Contains(t.name)
                && t.GetComponentsInChildren<Transform>().Any(c => c.name == "Handrail"));

            FinishDecks(decks);
            PerimeterWalls();
            InteriorWalls(ramp);
            CloseReturnGap();
            HighRoof();
            report.originalCollidersAfter = ramp.GetComponentsInChildren<Collider>(true).Length;
            report.originalCollidersPreserved = originalColliders.All(c => c)
                && report.originalCollidersBefore == report.originalCollidersAfter;
            report.originalDecksPreserved = decks.Select((t, i) => t.position == deckPositions[i]
                && t.rotation == deckRotations[i] && t.localScale == deckScales[i]).All(v => v);
            report.originalRoutesPreserved = routes.Length == SchoolCirculation.Routes.Length
                && routes.Select((r, i) => r.name == SchoolCirculation.Routes[i].name
                    && r.requireAscending == SchoolCirculation.Routes[i].requireAscending
                    && r.feet.SequenceEqual(SchoolCirculation.Routes[i].feet)).All(v => v);
            report.existingDoorIdsAndPosesPreserved = doors.Select((d, i) => d && d.persistentId == doorIds[i]
                && d.transform.position == doorPositions[i] && d.transform.rotation == doorRotations[i]
                && d.transform.localScale == doorScales[i]).All(v => v);
            report.addedWallColliders = details.GetComponentsInChildren<Collider>(true).Length;
            if (!report.originalDecksPreserved || !report.originalCollidersPreserved || !report.originalRoutesPreserved
                || !report.existingDoorIdsAndPosesPreserved)
                throw new InvalidOperationException("Ramp photo finish changed registered walking geometry or door identities.");
            LastReport = report;
            return report;
        }

        static Material Mat(string name, Color color, float smooth = .12f, float metal = 0f)
        {
            string path = Materials + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", smooth); mat.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(mat); return mat;
        }

        static void SetupMaterials()
        {
            masonry = Mat("PhotoRampMasonry", new Color(.75f, .735f, .685f));
            var plaster = AssetDatabase.LoadAssetAtPath<Material>(Materials + "PhotoPlasterWhite.mat");
            if (plaster && plaster.GetTexture("_BaseMap"))
            { masonry.SetTexture("_BaseMap", plaster.GetTexture("_BaseMap")); masonry.SetTextureScale("_BaseMap", new Vector2(2f, 2f)); }
            pale = Mat("PhotoRampCap", new Color(.61f, .585f, .525f), .17f);
            silver = Mat("PhotoRampWallRailSilver", new Color(.56f, .57f, .555f), .43f, .70f);
            steel = AssetDatabase.LoadAssetAtPath<Material>(Materials + "PhotoRoofSteel.mat");
            roof = AssetDatabase.LoadAssetAtPath<Material>(Materials + "PhotoRoofGrey.mat");
            concrete = Mat("PhotoRampBroomConcrete", new Color(.43f, .425f, .40f), .09f);
            ConcreteBroomTexture();
            amber = Mat("PhotoRampTranslucentRoofAmber", new Color(.64f, .47f, .26f, .60f), .21f);
            amber.SetFloat("_Surface", 1f); amber.SetFloat("_ZWrite", 0f);
            amber.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); amber.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            amber.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); amber.SetOverrideTag("RenderType", "Transparent");
            amber.SetShaderPassEnabled("ShadowCaster", false); amber.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(amber);
            if (!steel || !roof) throw new InvalidOperationException("Run the photographic school material setup before ramp finish.");
        }

        static Vector3 P(float x, float y, float h = 0f) => architecture.TransformPoint(SchoolCirculation.PdfPoint(x, y, h));
        static Vector3 Axis(Vector3 direction) => architecture.TransformDirection(direction);

        static void PerimeterWalls()
        {
            float q = Upper / 4f;
            // Signed inward direction fixes the solid wall outside the full lane width.
            Wall("Rampa — mureta oeste da entrada", P(West, 907.20f), P(West, North), Axis(Vector3.right), 0f, 0f, -.01f);
            Wall("Rampa — mureta oeste inclinada", P(West, North), P(West, South), Axis(Vector3.right), 0f, q, -.01f);
            Wall("Rampa — mureta oeste do retorno baixo", P(West, South), P(West, LandingSouth), Axis(Vector3.right), q, q, -.01f);
            float trim = SchoolCentralEnclosurePass.BoundaryY + 1.8f;
            Wall("Rampa — mureta leste da entrada superior", P(East, trim), P(East, North), Axis(Vector3.left), Upper, Upper, -.01f);
            Wall("Rampa — mureta leste inclinada", P(East, North), P(East, South), Axis(Vector3.left), Upper, 3f * q, -.01f);
            Wall("Rampa — mureta leste do retorno alto", P(East, South), P(East, LandingSouth), Axis(Vector3.left), 3f * q, 3f * q, -.01f);
            // Ends meet side-wall caps without extending into the U-turn routes.
            Wall("Rampa — parede sul do retorno baixo", P(West, LandingSouth), P(1010.24f, LandingSouth), Axis(Vector3.forward), q, q, -.01f);
            Wall("Rampa — parede sul do retorno alto", P(1010.24f, LandingSouth), P(East, LandingSouth), Axis(Vector3.forward), 3f * q, 3f * q, -.01f);
            Wall("Rampa — mureta norte do retorno intermediário", P(989f, LandingNorth), P(1031.44f, LandingNorth), Axis(Vector3.back), 2f * q, 2f * q, 2f * q - .16f, true);
        }

        static void Wall(string name, Vector3 a, Vector3 b, Vector3 inward, float footA, float footB, float bottom, bool trimEnds = false)
        {
            inward.y = 0f; inward.Normalize();
            var direction = b - a; direction.y = 0f; direction.Normalize();
            // Only the north return borders doorway sweeps: trim its ends by4mm.
            // Other segments meet exactly, without an8mm finish gap at each joint.
            if (trimEnds) { a += direction * .004f; b -= direction * .004f; }
            Vector3 innerOffset = -inward * .005f, outerOffset = -inward * (.005f + WallThickness);
            Vector3[] vertices =
            {
                At(a + innerOffset, bottom), At(b + innerOffset, bottom), At(b + outerOffset, bottom), At(a + outerOffset, bottom),
                At(a + innerOffset, footA + WallHeight), At(b + innerOffset, footB + WallHeight),
                At(b + outerOffset, footB + WallHeight), At(a + outerOffset, footA + WallHeight)
            };
            MeshSolid(name, vertices, masonry, true); report.masonryPanels++;
            Vector3[] cap = (Vector3[])vertices.Clone();
            for (int i = 0; i < 4; i++) { cap[i] = vertices[i + 4]; cap[i + 4] = vertices[i + 4] + Vector3.up * CapHeight; }
            MeshSolid(name + " — capeamento", cap, pale, false); report.capPanels++;
            foreach (float height in new[] { .70f, 1.00f })
            {
                Vector3 railA = At(a + inward * (RailProjection - RailDiameter * .5f), footA + height);
                Vector3 railB = At(b + inward * (RailProjection - RailDiameter * .5f), footB + height);
                Round(name + " — corrimão preso na alvenaria", railA, railB, RailDiameter, silver);
                report.wallMountedRailRuns++;
                int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 1.30f));
                for (int i = 0; i <= count; i++)
                {
                    Vector3 point = Vector3.Lerp(railA, railB, i / (float)count);
                    Vector3 wallPoint = point - inward * (RailProjection - RailDiameter * .5f + .006f);
                    Round(name + " — suporte curto do corrimão", wallPoint, point, .020f, silver);
                    Round(name + " — roseta na parede", wallPoint - inward * .002f, wallPoint + inward * .004f, .065f, silver);
                    report.wallRailBrackets++;
                }
            }
        }

        static void InteriorWalls(Transform ramp)
        {
            float q = Upper / 4f;
            InteriorWall("Rampa — divisória entre lances 1 e 2", 989f, North, South, 0f, q, 2f*q, q);
            InteriorWall("Rampa — divisória entre lances 2 e 3", 1010.24f, North, South, 2f*q, q, 2f*q, 3f*q);
            InteriorWall("Rampa — divisória entre lances 3 e 4", 1031.44f, North, South, 2f*q, 3f*q, Upper, 3f*q);
            // Stop dividers at the walking turns; only the non-crossing sides continue through the landings.
            InteriorWall("Rampa — divisória lateral da entrada", 989f, 907.20f, North, 0f, 0f, 2f*q, 2f*q);
            InteriorWall("Rampa — divisória sul entre retornos", 1010.24f, South, LandingSouth, q, q, 3f*q, 3f*q);
            InteriorWall("Rampa — divisória lateral da ligação superior", 1031.44f, SchoolCentralEnclosurePass.BoundaryY+1.8f, North, 2f*q, 2f*q, Upper, Upper);
            foreach (var guard in ramp.Cast<Transform>().Where(t=>!OuterGuards.Contains(t.name)
                && t.GetComponentsInChildren<Transform>().Any(c=>c.name=="Handrail")))
            {
                foreach(var renderer in guard.GetComponentsInChildren<MeshRenderer>(true))
                { renderer.enabled=false; report.hiddenInnerGuardRenderers++; }
                report.hiddenInnerGuardGroups++;
            }
        }
        static void InteriorWall(string name, float x, float north, float south, float westA, float westB, float eastA, float eastB)
        {
            var a=P(x,north); var b=P(x,south); var half=Axis(Vector3.right)*.05f;
            float bottomA=Mathf.Min(westA,eastA)-.16f, bottomB=Mathf.Min(westB,eastB)-.16f;
            float topA=Mathf.Max(westA,eastA)+WallHeight, topB=Mathf.Max(westB,eastB)+WallHeight;
            Vector3[] vertices={At(a-half,bottomA),At(b-half,bottomB),At(b+half,bottomB),At(a+half,bottomA),
                At(a-half,topA),At(b-half,topB),At(b+half,topB),At(a+half,topA)};
            MeshSolid(name,vertices,masonry,true);report.masonryPanels++;report.interiorMasonryPanels++;
            var cap=(Vector3[])vertices.Clone();
            for(int i=0;i<4;i++){cap[i]=vertices[i+4];cap[i+4]=vertices[i+4]+Vector3.up*CapHeight;}
            MeshSolid(name+" — capeamento",cap,pale,false);report.capPanels++;
            for(int side=-1;side<=1;side+=2)foreach(float height in new[]{.70f,1.00f})
            {
                var inward=Axis(Vector3.right)*side;
                float hA=(side<0?westA:eastA)+height,hB=(side<0?westB:eastB)+height;
                float offset=.05f+RailProjection-RailDiameter*.5f;
                var ra=At(a+inward*offset,hA);var rb=At(b+inward*offset,hB);
                Round(name+" — corrimão preso na alvenaria",ra,rb,RailDiameter,silver);
                report.wallMountedRailRuns++;report.interiorWallMountedRailRuns++;
                int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/1.30f));
                for(int i=0;i<=count;i++)
                {
                    var point=Vector3.Lerp(ra,rb,i/(float)count);var wallPoint=point-inward*(offset-.05f+.006f);
                    Round(name+" — suporte curto do corrimão",wallPoint,point,.020f,silver);
                    Round(name+" — roseta na parede",wallPoint-inward*.002f,wallPoint+inward*.004f,.065f,silver);report.wallRailBrackets++;
                }
            }
        }

        static Vector3 At(Vector3 position, float h) { position.y = architecture.position.y + h; return position; }

        static void CloseReturnGap()
        {
            // Turn_West_2 was visually hidden with the old steel guards, while the
            // adjacent masonry started farther south to clear the ground doorway.
            // This elevated infill fits the old75mm toe envelope and does not span
            // the ground entry. Its6mm northern overlap closes the masonry joint.
            float foot = Upper * .5f;
            var a = P(989f, ReturnGapNorth); var b = P(989f, ReturnGapSouth);
            var half = Axis(Vector3.right) * (ReturnGapThickness * .5f);
            float bottom = foot - .16f, top = foot + WallHeight;
            Vector3[] vertices = { At(a-half,bottom), At(b-half,bottom), At(b+half,bottom), At(a+half,bottom),
                At(a-half,top), At(b-half,top), At(b+half,top), At(a+half,top) };
            MeshSolid(ReturnGapName, vertices, masonry, true); report.masonryPanels++; report.returnGapPanels++;
            var cap = (Vector3[])vertices.Clone();
            for(int i=0;i<4;i++) { cap[i]=vertices[i+4]; cap[i+4]=vertices[i+4]+Vector3.up*CapHeight; }
            MeshSolid(ReturnGapName+" — capeamento",cap,pale,false); report.capPanels++;
            var inward = Axis(Vector3.right);
            float offset = ReturnGapThickness*.5f+RailProjection-RailDiameter*.5f;
            foreach(float height in new[]{.70f,1.00f})
            {
                var ra=At(a+inward*offset,foot+height); var rb=At(b+inward*offset,foot+height);
                Round(ReturnGapName+" — corrimão preso na alvenaria",ra,rb,RailDiameter,silver);
                report.wallMountedRailRuns++; report.returnGapRailRuns++;
                int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/1.30f));
                for(int i=0;i<=count;i++)
                {
                    var point=Vector3.Lerp(ra,rb,i/(float)count); var wallPoint=point-inward*(offset-ReturnGapThickness*.5f+.006f);
                    Round(ReturnGapName+" — suporte curto do corrimão",wallPoint,point,.020f,silver);
                    Round(ReturnGapName+" — roseta na parede",wallPoint-inward*.002f,wallPoint+inward*.004f,.065f,silver);
                    report.wallRailBrackets++;
                }
            }
        }

        static void FinishDecks(Transform[] decks)
        {
            foreach (var deck in decks)
            {
                deck.GetComponent<MeshRenderer>().sharedMaterial = concrete;
                SchoolCentralPhotoPass.WorldUV(deck.GetComponent<MeshFilter>(), .30f, .18f);
            }
        }

        static void ConcreteBroomTexture()
        {
            // Filter the shallow concrete grooves as a surface finish rather than thin raised boxes.
            // Mipmaps prevent distant lanes from breaking into black dotted lines.
            string path = Materials + "PhotoRampBroomTexture.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture)
            {
                const int width = 64, height = 128;
                texture = new Texture2D(width, height, TextureFormat.RGB24, true)
                    { name = "PhotoRampBroomTexture", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
                var pixels = new Color[width * height]; var random = new System.Random(6184);
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    float distance = Mathf.Abs((y + .5f) / height - .5f);
                    float stripe = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.011f, .040f, distance));
                    float shade = .96f - stripe * .13f + ((float)random.NextDouble() - .5f) * .024f;
                    pixels[y * width + x] = new Color(shade, shade, shade);
                }
                texture.SetPixels(pixels); texture.Apply(true); AssetDatabase.CreateAsset(texture, path);
            }
            concrete.SetTexture("_BaseMap", texture); concrete.SetTextureScale("_BaseMap", Vector2.one); EditorUtility.SetDirty(concrete);
        }

        static void HighRoof()
        {
            float roofNorth = SchoolCentralEnclosurePass.BoundaryY + 1.065f;
            float centerX = (RoofWest + RoofEast) * .5f;
            float[] bands = { 922f, 968f, 1014f };
            var cuts = new List<float> { roofNorth };
            foreach (float at in bands) { cuts.Add(at - 4.5f); cuts.Add(at + 4.5f); }
            cuts.Add(RoofSouth);
            for (int i = 0; i < cuts.Count - 1; i++)
            {
                bool translucent = i % 2 == 1;
                for (int side = -1; side <= 1; side += 2)
                {
                    float eaveX = side < 0 ? RoofWest : RoofEast;
                    var shape = new[]
                    {
                        P(eaveX, cuts[i], Eaves), P(eaveX, cuts[i + 1], Eaves), P(centerX, cuts[i + 1], Ridge), P(centerX, cuts[i], Ridge),
                        P(eaveX, cuts[i], Eaves + .055f), P(eaveX, cuts[i + 1], Eaves + .055f), P(centerX, cuts[i + 1], Ridge + .055f), P(centerX, cuts[i], Ridge + .055f)
                    };
                    MeshSolid(translucent ? "Rampa — faixa translúcida âmbar" : "Rampa — telhado alto de duas águas", shape, translucent ? amber : roof, false);
                    report.roofPanels++; if (translucent) report.translucentBands++;
                }
            }
            for (int i = 0; i <= 4; i++)
            {
                float y = Mathf.Lerp(roofNorth, RoofSouth, i / 4f);
                var a = P(RoofWest, y, Eaves - .15f); var b = P(RoofEast, y, Eaves - .15f); var peak = P(centerX, y, Ridge - .14f);
                Beam("Rampa — banzo inferior da tesoura alta", a, b, .085f, steel);
                Beam("Rampa — banzo superior oeste", a, peak, .065f, steel);
                Beam("Rampa — banzo superior leste", peak, b, .065f, steel);
                for (int j = 1; j < 8; j++)
                {
                    Vector3 lower = Vector3.Lerp(a, b, j / 8f);
                    Vector3 upper = lower + Vector3.up * ((Ridge - Eaves + .01f) * (1f - Mathf.Abs(j / 4f - 1f)));
                    Beam("Rampa — montante da tesoura", lower, upper, .032f, steel);
                    if (j < 7)
                    {
                        Vector3 next = Vector3.Lerp(a, b, (j + 1) / 8f);
                        Vector3 nextUpper = next + Vector3.up * ((Ridge - Eaves + .01f) * (1f - Mathf.Abs((j + 1) / 4f - 1f)));
                        Beam("Rampa — diagonal da tesoura", j % 2 == 0 ? lower : upper, j % 2 == 0 ? nextUpper : next, .030f, steel);
                    }
                }
                report.roofTrusses++;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float eaveX = side < 0 ? RoofWest : RoofEast;
                foreach (float t in new[] { 0f, .33f, .67f, 1f })
                {
                    float x = Mathf.Lerp(eaveX, centerX, t), h = Mathf.Lerp(Eaves, Ridge, t) - .06f;
                    Beam("Rampa — terça sob a telha", P(x, roofNorth, h), P(x, RoofSouth, h), .045f, steel); report.roofPurlins++;
                }
            }
            int ribs = Mathf.CeilToInt((RoofSouth - roofNorth) * Scale / .27f);
            for (int i = 0; i <= ribs; i++)
            {
                float y = Mathf.Lerp(roofNorth, RoofSouth, i / (float)ribs);
                bool translucent = bands.Any(at => Mathf.Abs(at - y) <= 4.5f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Beam("Rampa — ondulação da telha", P(side < 0 ? RoofWest : RoofEast, y, Eaves + .071f),
                        P(centerX, y, Ridge + .071f), .014f, translucent ? amber : roof); report.roofCorrugations++;
                }
            }
            Beam("Rampa — cumeeira metálica", P(centerX, roofNorth, Ridge + .092f), P(centerX, RoofSouth, Ridge + .092f), .065f, roof);
            // Cables stay at roof level; no poles occupy any of the landings or lanes.
            for (int i = 0; i < 2; i++)
                Round("Rampa — conduíte sob a tesoura", P(centerX - 6f + 12f * i, roofNorth, Eaves - .18f),
                    P(centerX - 6f + 12f * i, RoofSouth, Eaves - .18f), .020f, steel);
        }

        static GameObject Cube(string name, Vector3 position, Vector3 size, Material mat, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.isStatic = true;
            go.transform.SetParent(details, false); go.transform.position = position; go.transform.rotation = rotation; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        static void Beam(string name, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            if ((b - a).sqrMagnitude < .000001f) return;
            Cube(name, (a + b) * .5f, new Vector3(thickness, thickness, Vector3.Distance(a, b)), mat, Quaternion.LookRotation(b - a, Vector3.up));
        }
        static void Round(string name, Vector3 a, Vector3 b, float diameter, Material mat)
        {
            if ((b - a).sqrMagnitude < .000001f) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; go.isStatic = true;
            go.transform.SetParent(details, false); go.transform.position = (a + b) * .5f;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            go.transform.localScale = new Vector3(diameter, Vector3.Distance(a, b) * .5f, diameter);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        static void MeshSolid(string name, Vector3[] corners, Material mat, bool collision)
        {
            // Six separately-normalled quad faces preserve hard masonry edges and roof undersides.
            int[][] faces = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 },
                new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 } };
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            Vector3 center = corners.Aggregate(Vector3.zero, (sum, v) => sum + v) / 8f;
            foreach (int[] face in faces)
            {
                int start = vertices.Count;
                for (int j = 0; j < 4; j++) vertices.Add(corners[face[j]] - center);
                float w = Vector3.Distance(corners[face[0]], corners[face[1]]), h = Vector3.Distance(corners[face[1]], corners[face[2]]);
                uv.AddRange(new[] { new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h) });
                Vector3 normal = Vector3.Cross(corners[face[1]] - corners[face[0]], corners[face[2]] - corners[face[0]]);
                Vector3 faceCenter = face.Aggregate(Vector3.zero, (sum, index) => sum + corners[index]) * .25f;
                bool outward = Vector3.Dot(normal, faceCenter - center) > 0f;
                triangles.AddRange(outward ? new[] { start, start + 1, start + 2, start, start + 2, start + 3 }
                    : new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path = meshFolder + "/Ramp-" + (++report.generatedMeshes).ToString("D3") + ".asset";
            AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject(name) { isStatic = true }; go.transform.SetParent(details, false); go.transform.position = center;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collision) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
    }
}
