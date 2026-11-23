// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using HorrorGame;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>
    /// Photo-based low vestibule between the Auditorium and Cantina. Its placement
    /// and heights are estimates; it keeps the existing north passage and door.
    /// Call once on a fresh group after the photographic materials and local grade.
    /// </summary>
    public static class SchoolCanteenConnector
    {
        const float Scale = .07055555556f;
        const float West = 1213.2f, East = 1315.8f;
        const float Rear = 809.24f, Front = 851.12f;
        const float OpeningWest = 1245f, OpeningEast = 1275f;
        const float FloorTop = .025f, FrontRoof = 2.65f, RearRoof = 2.90f;
        const float RouteSpacing = .10f, FootClearance = .05f;
        const string MaterialFolder = "Assets/SchoolCEEP/Materials/";

        [Serializable]
        public sealed class Report
        {
            public bool positionAndHeightsEstimated = true;
            public float[] roofBox;
            public float frontPdfY, backPdfY;
            public float clearOpeningMeters, roofDepthMeters, roofSlopeDegrees, minimumRoofClearanceMeters;
            public int fixedGrilles, roofCorrugations, columns, benches, lights, renderers, colliders;
            public SchoolCirculation.Route[] routes;
            public string[] notes;
        }

        public static Report LastReport { get; private set; }
        static Transform root;
        static Material white, blue, roof, steel, floor, cream, wood;

        public static Report Build(Transform parent)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build the school vestibule outside Play mode.");
            if (parent.childCount != 0)
                throw new InvalidOperationException("Provide a fresh group for the canteen connector.");
            white = Material("PhotoPlasterWhite");
            blue = Material("PhotoFrameBlue");
            roof = Material("PhotoRoofGrey");
            steel = Material("PhotoRoofSteel");
            floor = Material("PhotoConcreteFloor");
            cream = Material("PhotoDoorCream");
            wood = Material("Portas antigas");
            root = parent;
            float depth = (Front - Rear) * Scale;
            float pitch = Mathf.Atan2(RearRoof - FrontRoof, depth) * Mathf.Rad2Deg;
            LastReport = new Report
            {
                roofBox = new[] { West, Rear, East, Front },
                frontPdfY = Front,
                backPdfY = Rear,
                clearOpeningMeters = (OpeningEast - OpeningWest) * Scale,
                roofDepthMeters = depth,
                roofSlopeDegrees = pitch,
                minimumRoofClearanceMeters = FrontRoof - .065f * .5f - FloorTop,
                notes = new[]
                {
                    "The latest photograph establishes a small white/blue covered vestibule with vertical grilles, between Auditorium and Cantina. Registered position and height remain visual estimates.",
                    "The roof covers only PDF x1213.2..1315.8 / y809.24..851.12, at the back of the courtyard where the block corridor starts. The rear edge meets the existing classroom south wall; both lateral courts remain open to the sky.",
                    "Roof front height is 2.65m and rear height is 2.90m, over approximately 2.955m depth. Position and heights remain provisional photographic estimates; the report records the actual calculated slope.",
                    "Fixed grille panels flank the clear central aperture x1245..1275, approximately 2.117m wide. There is no rear wall or new door.",
                    "The low pedestrian path continues north at x1260 to y823, then branches west into the existing block corridor at y830 and east through the existing Cantina door at y836.74. No native wall or door opening is moved or created.",
                    "A small wooden bench sits against the Cantina side at x1309 / y819, outside the central pedestrian aperture and the existing door approach. No exit or canteen signs are added.",
                    "Two weak shadowless lights attach to fixtures directly under the real roof; no ambient AudioSources, story triggers or gameplay events are added.",
                    "Use the existing validation pass with temporary native-door opening to verify both directions of the supplied physical routes."
                }
            };
            Floor();
            Columns();
            Roof(pitch, depth);
            Grille("Grade vertical fixa — esquerda", West, OpeningWest);
            Grille("Grade vertical fixa — direita", OpeningEast, East);
            Bench();
            Fixture(1233f, 830f);
            Fixture(1295f, 830f);
            var passage = new List<Vector3>();
            Line(passage, P(1260f, 977f, FloorTop + FootClearance), P(1260f, 823f, FloorTop + FootClearance));
            var doorAccess = new List<Vector3>();
            Line(doorAccess, P(1260f, 836.74f, FloorTop + FootClearance), P(1328f, 836.74f, FloorTop + FootClearance));
            var blockAccess = new List<Vector3>();
            Line(blockAccess, P(1260f, 830f, FloorTop + FootClearance), P(1193f, 830f, FloorTop + FootClearance));
            LastReport.routes = new[]
            {
                Route("Ligacao_Cantina_Passagem_Baixa", passage),
                Route("Ligacao_Cantina_Acesso_Porta_Existente", doorAccess),
                Route("Ligacao_Cantina_Corredor_Blocos", blockAccess)
            };
            Physics.SyncTransforms();
            return LastReport;
        }

        static Material Material(string name)
        {
            var value = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + name + ".mat");
            if (!value) throw new InvalidOperationException("Run SchoolPhotoPass before the canteen connector; missing material " + name + ".");
            return value;
        }
        static Vector3 P(float x, float y, float height = 0f) => SchoolMapBuilder.Point(x, y, height);
        static GameObject Cube(string name, Vector3 center, Vector3 size, Material material, bool collision = false)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name; item.isStatic = true;
            item.transform.SetParent(root, false);
            item.transform.localPosition = center;
            item.transform.localScale = size;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            else LastReport.colliders++;
            LastReport.renderers++;
            return item;
        }
        static GameObject Beam(string name, Vector3 a, Vector3 b, float width, Material material)
        {
            var item = Cube(name, (a + b) * .5f, new Vector3(width, width, Vector3.Distance(a, b)), material);
            item.transform.localRotation = Quaternion.LookRotation(b - a);
            return item;
        }
        static void Floor()
        {
            const float thickness = .08f;
            var item = Cube("Vestíbulo — piso baixo contínuo", P((West + East) * .5f, (Rear + Front) * .5f, FloorTop - thickness * .5f),
                new Vector3((East - West) * Scale, thickness, (Front - Rear) * Scale), floor, true);
            item.AddComponent<SurfaceType>().surface = GroundSurface.Concrete;
        }
        static void Columns()
        {
            foreach (float x in new[] { West, East })
            foreach (float y in new[] { Rear, Front })
            {
                float top = y == Front ? FrontRoof : RearRoof;
                Cube("Vestíbulo — pilar branco", P(x, y, (FloorTop + top) * .5f), new Vector3(.15f, top - FloorTop, .15f), white, true);
                Cube("Vestíbulo — base azul do pilar", P(x, y, FloorTop + .56f), new Vector3(.153f, 1.12f, .153f), blue);
                LastReport.columns++;
            }
        }
        static void Roof(float pitch, float depth)
        {
            float width = (East - West) * Scale;
            float length = Mathf.Sqrt(depth * depth + (RearRoof - FrontRoof) * (RearRoof - FrontRoof));
            var deck = Cube("Vestíbulo — telhado baixo de fibrocimento", P((West + East) * .5f, (Rear + Front) * .5f, (FrontRoof + RearRoof) * .5f),
                new Vector3(width + .12f, .065f, length + .12f), roof);
            deck.transform.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
            int ribs = Mathf.CeilToInt(width / .27f);
            for (int i = 0; i <= ribs; i++)
            {
                float x = Mathf.Lerp(West, East, (float)i / ribs);
                Beam("Vestíbulo — ondulação da telha", P(x, Front + .8f, FrontRoof + .040f), P(x, Rear - .8f, RearRoof + .040f), .022f, roof);
                LastReport.roofCorrugations++;
            }
            Beam("Vestíbulo — perfil frontal da cobertura", P(West, Front, FrontRoof - .09f), P(East, Front, FrontRoof - .09f), .075f, steel);
            Beam("Vestíbulo — perfil posterior da cobertura", P(West, Rear, RearRoof - .09f), P(East, Rear, RearRoof - .09f), .075f, steel);
            foreach (float x in new[] { West, East })
                Beam("Vestíbulo — apoio inclinado do telhado", P(x, Front, FrontRoof - .09f), P(x, Rear, RearRoof - .09f), .055f, steel);
            Cube("Vestíbulo — fáscia azul frontal", P((West + East) * .5f, Front + .65f, FrontRoof - .05f), new Vector3(width + .16f, .18f, .075f), blue);
        }
        static void Grille(string name, float x0, float x1)
        {
            const float height = 2.15f, thickness = .055f;
            float span = (x1 - x0) * Scale;
            float mid = (x0 + x1) * .5f;
            Vector3 center = P(mid, Front, FloorTop + height * .5f);
            // A single fixed panel collider stays wholly outside the central aperture.
            var blocker = new GameObject(name + " — colisor fixo") { isStatic = true };
            blocker.transform.SetParent(root, false); blocker.transform.localPosition = center;
            blocker.AddComponent<BoxCollider>().size = new Vector3(span, height, thickness);
            LastReport.colliders++;
            int bars = Mathf.Max(1, Mathf.CeilToInt(span / .13f));
            for (int i = 0; i <= bars; i++)
            {
                float x = Mathf.Lerp(x0, x1, (float)i / bars);
                // Outer bar centers are inset so their physical/visual width does not narrow the opening.
                x = Mathf.Clamp(x, x0 + .014f / Scale, x1 - .014f / Scale);
                Cube(name + " — barra vertical", P(x, Front, FloorTop + height * .5f), new Vector3(.028f, height, .035f), blue);
            }
            foreach (float h in new[] { .12f, 1.14f, height })
                Cube(name + " — travessa", P(mid, Front, FloorTop + h), new Vector3(span, .035f, .045f), blue);
            LastReport.fixedGrilles++;
        }
        static void Bench()
        {
            Vector3 c = P(1309f, 819f, FloorTop);
            Cube("Vestíbulo — banco de madeira junto da cantina", c + Vector3.up * .45f, new Vector3(.40f, .06f, 1.13f), wood, true);
            foreach (float z in new[] { -.40f, .40f })
            {
                Cube("Vestíbulo — pé do banco", c + new Vector3(0f, .21f, z), new Vector3(.32f, .42f, .055f), steel, true);
                Cube("Vestíbulo — apoio do banco", c + new Vector3(0f, .425f, z), new Vector3(.35f, .04f, .045f), steel);
            }
            foreach (float x in new[] { -.13f, 0f, .13f })
                Cube("Vestíbulo — junta das ripas de madeira", c + new Vector3(x, .482f, 0f), new Vector3(.007f, .003f, 1.10f), steel);
            LastReport.benches++;
        }
        static void Fixture(float x, float y)
        {
            float t = (Front - y) / (Front - Rear);
            float underside = Mathf.Lerp(FrontRoof, RearRoof, t) - .065f * .5f;
            Vector3 mount = P(x, y, underside - .038f);
            Cube("Vestíbulo — carcaça de luminária sob telhado", mount, new Vector3(.34f, .060f, .14f), steel);
            Cube("Vestíbulo — difusor branco de luminária", mount + Vector3.down * .037f, new Vector3(.30f, .020f, .11f), cream);
            Beam("Vestíbulo — fixação da luminária", P(x, y, underside), mount + Vector3.up * .030f, .025f, steel);
            var item = new GameObject("Vestíbulo — iluminação fraca sob cobertura") { isStatic = true };
            item.transform.SetParent(root, false); item.transform.localPosition = mount + Vector3.down * .10f;
            var light = item.AddComponent<Light>();
            light.type = LightType.Point; light.range = 4f; light.intensity = .55f;
            light.color = new Color(.76f, .79f, .73f); light.shadows = LightShadows.None;
            LastReport.lights++;
        }
        static void Line(List<Vector3> feet, Vector3 start, Vector3 end)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start, end) / RouteSpacing));
            for (int i = 0; i <= count; i++)
            {
                var point = Vector3.Lerp(start, end, (float)i / count);
                if (feet.Count == 0 || (feet[feet.Count - 1] - point).sqrMagnitude > .00000001f) feet.Add(point);
            }
        }
        static SchoolCirculation.Route Route(string name, List<Vector3> localFeet)
        {
            var points = new Vector3[localFeet.Count];
            for (int i = 0; i < points.Length; i++) points[i] = root.TransformPoint(localFeet[i]);
            return new SchoolCirculation.Route { name = name, feet = points, requireAscending = false };
        }
    }
}
