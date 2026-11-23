// Ferramenta da escola para construção ou acabamento.

using System;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>Photo-observed props only. Call before the main builder combines static meshes.</summary>
    public static class SchoolPhotoDetails
    {
        const float Scale = .0705555556f;
        const string MaterialsFolder = "Assets/SchoolCEEP/Materials";

        [Serializable]
        public sealed class Report
        {
            public int benches, pots, planters, plants, columns, bicycleRacks, bicycles;
            public int meshes, colliders;
            public float clearMainHallAxisMeters;
            public string[] photoSources, notes;
        }

        static Report report;
        static Material blue, grey, white, navy, terracotta, mustard, earth, green, paleGreen, rubber, steel;
        static Mesh cylinderMesh;

        public static Report Build(Transform parent)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build school photo details outside Play mode.");
            EnsureFolder(MaterialsFolder);
            blue = Mat("PhotoBlueConcrete", new Color(.055f, .18f, .39f), .14f);
            grey = Mat("PhotoColumnGrey", new Color(.35f, .38f, .39f), .15f);
            white = Mat("PhotoPotIvory", new Color(.70f, .69f, .61f), .10f);
            navy = Mat("PhotoSteelNavy", new Color(.055f, .12f, .22f), .25f, .35f);
            terracotta = Mat("PhotoTerracotta", new Color(.52f, .24f, .17f), .10f);
            mustard = Mat("PhotoMustard", new Color(.59f, .43f, .18f), .12f);
            earth = Mat("PhotoEarth", new Color(.095f, .074f, .050f), .02f);
            green = Mat("PhotoLeaves", new Color(.115f, .225f, .105f), .08f);
            paleGreen = Mat("PhotoAgave", new Color(.27f, .37f, .235f), .15f);
            rubber = Mat("PhotoBicycleRubber", new Color(.031f, .034f, .037f), .12f);
            steel = Mat("PhotoBicycleSteel", new Color(.33f, .35f, .36f), .25f, .70f);
            report = new Report
            {
                clearMainHallAxisMeters = (1480f - 1360f) * Scale - .60f,
                photoSources = new[]
                {
                    "C:/Users/ruhtra/Downloads/corredor principal.jpg",
                    "C:/Users/ruhtra/Downloads/corredor principal esquerda.jpg",
                    "C:/Users/ruhtra/Downloads/corredor principal direita.jpg",
                    "C:/Users/ruhtra/Pictures/parte indo pra quadra na direita .png",
                    "C:/Users/ruhtra/Pictures/corredor voltando da quadra.png",
                    "C:/Users/ruhtra/Pictures/parte da frente da quadra.png",
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-a9d30ed7-a442-4205-bbf9-13bc84b7f170.png"
                },
                notes = new[]
                {
                    "Two blue slab benches, potted foliage and two-tone columns follow the supplied hall photos; axis remains wider than6m.",
                    "Outdoor photo details are concentrated in the two east gardens and south of the terracotta teaching wing; main walkway remains clear.",
                    "Bicycle count is deliberately reduced to four; placement and plant varieties are visual approximations, not surveyed positions.",
                    "No people, roofs, facade text, external stock assets or existing-material overrides are created.",
                    "Procedural meshes must be combined/saved by the root scene builder before scene persistence; all generated visual objects are static."
                }
            };
            var props = Group(parent, "Props das sete fotos");
            MainHall(props);
            EastGardens(props);
            Bikes(props);
            return report;
        }

        static Vector3 Point(float x, float y, float height = 0f) =>
            new Vector3((x - 1413f) * Scale, height, (1628f - y) * Scale);

        static void MainHall(Transform parent)
        {
            var hall = Group(parent, "Corredor principal — bancos e vasos");
            Bench(hall, Point(1360f, 1250f, .02f), 90f);
            Bench(hall, Point(1480f, 1205f, .02f), 90f);
            Pot(hall, Point(1360f, 1228f, .02f), .46f, .28f, white, .83f, false);
            Pot(hall, Point(1360f, 1272f, .02f), .39f, .23f, rubber, .65f, false);
            Pot(hall, Point(1480f, 1228f, .02f), .47f, .29f, blue, .98f, true);
            Pot(hall, Point(1480f, 1272f, .02f), .34f, .21f, rubber, .61f, true);
            float[] depths = { 1191f, 1220f, 1260f, 1326f };
            foreach (float x in new[] { 1356f, 1486f })
                foreach (float y in depths)
                {
                    var column = Group(hall, "Pilar cinza — borda do corredor");
                    Vector3 basePoint = Point(x, y);
                    Cube(column, "Base pintada cinza", basePoint + Vector3.up * .70f,
                        new Vector3(.24f, 1.40f, .24f), grey, true);
                    Cube(column, "Fuste claro", basePoint + Vector3.up * 2.95f,
                        new Vector3(.22f, 3.10f, .22f), white, true);
                    Cube(column, "Friso navy", basePoint + new Vector3(.121f, 1.0f, 0f),
                        new Vector3(.008f, .48f, .20f), navy, false);
                    report.columns++;
                }
        }

        static void Bench(Transform parent, Vector3 origin, float yaw)
        {
            var bench = Group(parent, "Banco de concreto azul");
            bench.localPosition = origin;
            bench.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Cube(bench, "Laje do assento", new Vector3(0f, .535f, 0f), new Vector3(2.15f, .15f, .54f), blue, true);
            Cube(bench, "Borda superior", new Vector3(0f, .614f, 0f), new Vector3(2.11f, .014f, .50f), blue, false);
            foreach (float x in new[] { -.73f, .73f })
            {
                Cube(bench, "Pé maciço azul", new Vector3(x, .24f, 0f), new Vector3(.27f, .48f, .46f), blue, true);
                Cube(bench, "Sapata", new Vector3(x, .035f, 0f), new Vector3(.34f, .07f, .50f), blue, false);
            }
            report.benches++;
        }

        static void EastGardens(Transform parent)
        {
            var gardens = Group(parent, "Jardins laterais — plantas das fotos");
            Planter(gardens, Point(1772f, 697f, .025f), 2.55f, .88f, 0f);
            Planter(gardens, Point(2008f, 688f, .025f), 2.10f, .78f, 90f);
            Planter(gardens, Point(1753f, 1190f, .025f), 2.45f, .80f, 90f);
            Planter(gardens, Point(2132f, 1115f, .025f), 2.35f, .83f, 90f);
            foreach (var xy in new[]
            {
                new Vector2(1833f, 703f), new Vector2(1941f, 684f),
                new Vector2(1784f, 1089f), new Vector2(1857f, 1114f),
                new Vector2(2090f, 1265f), new Vector2(1776f, 1310f)
            })
                Rosette(gardens, Point(xy.x, xy.y, .03f), 1.10f, 1.0f);
            // Potted plants beside the bicycle approach, well south of the east-west passage.
            Pot(gardens, Point(2244f, 1093f, .025f), .52f, .32f, terracotta, 1.12f, true);
            Pot(gardens, Point(2350f, 1093f, .025f), .48f, .29f, white, .90f, false);
        }

        static void Planter(Transform parent, Vector3 position, float length, float depth, float yaw)
        {
            var planter = Group(parent, "Canteiro baixo de alvenaria");
            planter.localPosition = position;
            planter.localRotation = Quaternion.Euler(0f, yaw, 0f);
            const float height = .22f, edge = .09f;
            Cube(planter, "Terra do canteiro", new Vector3(0f, .12f, 0f),
                new Vector3(length - edge, .07f, depth - edge), earth, false);
            foreach (float z in new[] { -depth / 2f, depth / 2f })
                Cube(planter, "Borda terracota", new Vector3(0f, height / 2f, z),
                    new Vector3(length + edge, height, edge), terracotta, true);
            foreach (float x in new[] { -length / 2f, length / 2f })
                Cube(planter, "Fecho do canteiro", new Vector3(x, height / 2f, 0f),
                    new Vector3(edge, height, depth), terracotta, true);
            Cube(planter, "Rebordo ocre", new Vector3(0f, height + .01f, depth / 2f),
                new Vector3(length + edge, .04f, edge + .035f), mustard, false);
            Rosette(planter, new Vector3(-length * .25f, .16f, 0f), .79f, .72f);
            Rosette(planter, new Vector3(length * .25f, .16f, 0f), .83f, .75f);
            report.planters++;
        }

        static void Pot(Transform parent, Vector3 origin, float height, float radius,
            Material material, float plantHeight, bool spiky)
        {
            var pot = Group(parent, "Vaso com folhagem");
            pot.localPosition = origin;
            MeshObject(pot, "Vaso cônico oco", HollowPot(height, radius), Vector3.zero, Quaternion.identity, Vector3.one, material);
            var blocker = pot.gameObject.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, height / 2f, 0f);
            blocker.size = new Vector3(radius * 1.8f, height, radius * 1.8f);
            report.colliders++;
            Cylinder(pot, "Terra visível", new Vector3(0f, height - .045f, 0f), radius * .83f, .035f, earth);
            if (spiky) Rosette(pot, new Vector3(0f, height - .02f, 0f), plantHeight, radius * 2.2f);
            else
            {
                Vector3 stemBase = new Vector3(0f, height - .025f, 0f);
                for (int i = 0; i < 7; i++)
                {
                    float angle = i * 137.5f;
                    Quaternion direction = Quaternion.Euler(0f, angle, 0f);
                    Vector3 tip = stemBase + direction * new Vector3(.12f + .025f * (i % 3), plantHeight * (.65f + .05f * i), .08f);
                    Line(pot, "Haste", stemBase, tip, .012f, green);
                    MeshObject(pot, "Folha larga", Leaf(plantHeight * .55f, radius * .55f, .18f),
                        tip - Vector3.up * .13f, direction * Quaternion.Euler(20f + i * 3f, 0f, 0f), Vector3.one, green);
                }
                report.plants++;
            }
            report.pots++;
        }

        static void Rosette(Transform parent, Vector3 origin, float height, float radius)
        {
            var plant = Group(parent, "Agave e folhagem em roseta");
            plant.localPosition = origin;
            for (int i = 0; i < 9; i++)
            {
                float length = height * (.75f + .055f * (i % 4));
                float lean = 30f + 7f * (i % 3);
                MeshObject(plant, "Folha lanceolada", Leaf(length, radius * .12f, radius * .16f),
                    Vector3.zero, Quaternion.Euler(lean, i * 137.5f, 0f), Vector3.one, i % 3 == 0 ? green : paleGreen);
            }
            report.plants++;
        }

        static void Bikes(Transform parent)
        {
            var area = Group(parent, "Bicicletário junto da ala terracota");
            area.localPosition = Point(2300f, 1100f, .015f);
            var rack = Group(area, "Bicicletário de aço galvanizado");
            foreach (float z in new[] { -.31f, .31f })
                Line(rack, "Trilho de base", new Vector3(-2.05f, .055f, z), new Vector3(2.05f, .055f, z), .032f, steel);
            for (int i = 0; i < 6; i++)
            {
                float x = -1.72f + i * .69f;
                Vector3 previous = new Vector3(x, .05f, -.29f);
                Line(rack, "Montante", previous, new Vector3(x, .60f, -.29f), .025f, steel);
                previous = new Vector3(x, .60f, -.29f);
                for (int j = 1; j <= 8; j++)
                {
                    float angle = Mathf.PI * j / 8f;
                    Vector3 next = new Vector3(x, .60f + Mathf.Sin(angle) * .29f, -Mathf.Cos(angle) * .29f);
                    Line(rack, "Arco do suporte", previous, next, .025f, steel);
                    previous = next;
                }
                Line(rack, "Montante", previous, new Vector3(x, .05f, .29f), .025f, steel);
            }
            report.bicycleRacks++;
            for (int i = 0; i < 4; i++)
            {
                var bike = Group(area, "Bicicleta estacionada " + (i + 1));
                bike.localPosition = new Vector3(-1.41f + i * .70f, 0f, .02f);
                bike.localRotation = Quaternion.Euler(0f, 90f + (i % 2 == 0 ? -4f : 3f), 0f);
                Bicycle(bike, i % 2 == 0 ? navy : terracotta);
                var blocker = bike.gameObject.AddComponent<BoxCollider>();
                blocker.center = new Vector3(.025f, .55f, 0f);
                blocker.size = new Vector3(1.88f, 1.10f, .46f);
                report.colliders++;
                report.bicycles++;
            }
        }

        static void Bicycle(Transform bike, Material paint)
        {
            Vector3 rear = new Vector3(-.59f, .36f, 0f), front = new Vector3(.62f, .36f, 0f);
            foreach (Vector3 hub in new[] { rear, front })
            {
                MeshObject(bike, "Pneu", Torus(.33f, .034f), hub, Quaternion.identity, Vector3.one, rubber);
                MeshObject(bike, "Aro de metal", Torus(.296f, .013f), hub, Quaternion.identity, Vector3.one, steel);
                for (int i = 0; i < 8; i++)
                {
                    float a = Mathf.PI * 2f * i / 8f;
                    Line(bike, "Raio", hub, hub + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * .291f, .0045f, steel);
                }
                Line(bike, "Eixo", hub - Vector3.forward * .075f, hub + Vector3.forward * .075f, .024f, steel);
            }
            Vector3 crank = new Vector3(-.075f, .34f, 0f), seat = new Vector3(-.18f, .81f, 0f), neck = new Vector3(.39f, .83f, 0f);
            Line(bike, "Tubo inferior", crank, neck, .022f, paint);
            Line(bike, "Tubo superior", seat, neck, .020f, paint);
            Line(bike, "Tubo do selim", crank, seat, .024f, paint);
            Line(bike, "Triângulo traseiro inferior", rear, crank, .016f, paint);
            Line(bike, "Triângulo traseiro superior", rear, seat, .016f, paint);
            Line(bike, "Garfo", front, neck, .019f, steel);
            Line(bike, "Espigão", seat, new Vector3(-.21f, .93f, 0f), .014f, steel);
            Cube(bike, "Selim", new Vector3(-.21f, .95f, 0f), new Vector3(.27f, .065f, .17f), rubber, false);
            Line(bike, "Mesa do guidão", neck, new Vector3(.41f, 1.025f, 0f), .016f, steel);
            Line(bike, "Guidão", new Vector3(.41f, 1.025f, -.21f), new Vector3(.41f, 1.025f, .21f), .012f, steel);
            foreach (float z in new[] { -.22f, .22f })
                Line(bike, "Manopla", new Vector3(.41f, 1.025f, z), new Vector3(.31f, 1.025f, z), .018f, rubber);
            MeshObject(bike, "Coroa", Torus(.085f, .010f), crank + Vector3.forward * .04f, Quaternion.identity, Vector3.one, steel);
            Line(bike, "Corrente inferior", rear + Vector3.forward * .05f, crank + new Vector3(0f, -.07f, .05f), .008f, steel);
            Line(bike, "Corrente superior", rear + Vector3.forward * .05f, crank + new Vector3(0f, .07f, .05f), .008f, steel);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 pedal = crank + new Vector3(.085f * side, -.075f * side, .10f * side);
                Line(bike, "Pedivela", crank + Vector3.forward * (.10f * side), pedal, .012f, steel);
                Cube(bike, "Pedal", pedal, new Vector3(.08f, .035f, .13f), rubber, false);
            }
        }

        static Material Mat(string name, Color color, float smoothness, float metallic = 0f)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("URP Lit shader is required.");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            string parent = path.Substring(0, split);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
        }

        static Transform Group(Transform parent, string name)
        {
            var item = new GameObject(name) { isStatic = true };
            item.transform.SetParent(parent, false);
            return item.transform;
        }

        static GameObject Cube(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool collider)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name; item.isStatic = true;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = center;
            item.transform.localScale = size;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            else report.colliders++;
            report.meshes++;
            return item;
        }

        static void Cylinder(Transform parent, string name, Vector3 center, float radius, float height, Material material)
        {
            if (!cylinderMesh) cylinderMesh = SolidCylinder();
            MeshObject(parent, name, cylinderMesh, center, Quaternion.identity, new Vector3(radius * 2f, height, radius * 2f), material);
        }

        static void Line(Transform parent, string name, Vector3 start, Vector3 end, float radius, Material material)
        {
            if (!cylinderMesh) cylinderMesh = SolidCylinder();
            MeshObject(parent, name, cylinderMesh, (start + end) / 2f,
                Quaternion.FromToRotation(Vector3.up, end - start),
                new Vector3(radius * 2f, Vector3.Distance(start, end), radius * 2f), material);
        }

        static void MeshObject(Transform parent, string name, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            var item = new GameObject(name) { isStatic = true };
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localRotation = rotation;
            item.transform.localScale = scale;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
            report.meshes++;
        }

        static Mesh SolidCylinder()
        {
            const int count = 12;
            var vertices = new Vector3[count * 2 + 2];
            var triangles = new int[count * 12];
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                vertices[i] = new Vector3(Mathf.Cos(angle) * .5f, -.5f, Mathf.Sin(angle) * .5f);
                vertices[count + i] = vertices[i] + Vector3.up;
                int next = (i + 1) % count, t = i * 12;
                triangles[t] = i; triangles[t + 1] = count + i; triangles[t + 2] = next;
                triangles[t + 3] = next; triangles[t + 4] = count + i; triangles[t + 5] = count + next;
                triangles[t + 6] = count * 2; triangles[t + 7] = i; triangles[t + 8] = next;
                triangles[t + 9] = count * 2 + 1; triangles[t + 10] = count + next; triangles[t + 11] = count + i;
            }
            vertices[count * 2] = Vector3.down * .5f;
            vertices[count * 2 + 1] = Vector3.up * .5f;
            return MakeMesh("Photo detail cylinder", vertices, triangles);
        }

        static Mesh HollowPot(float height, float radius)
        {
            const int count = 16;
            var vertices = new Vector3[count * 4];
            var triangles = new int[count * 24];
            for (int ring = 0; ring < 4; ring++)
            {
                float r = ring == 0 ? radius * .66f : ring == 1 ? radius : ring == 2 ? radius * .88f : radius * .55f;
                float y = ring == 0 ? 0f : ring == 3 ? .075f : height;
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    vertices[ring * count + i] = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
                }
            }
            for (int ring = 0; ring < 4; ring++)
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count, following = (ring + 1) % 4;
                    int a = ring * count + i, b = following * count + i;
                    int c = ring * count + next, d = following * count + next;
                    int t = (ring * count + i) * 6;
                    triangles[t] = a; triangles[t + 1] = b; triangles[t + 2] = c;
                    triangles[t + 3] = c; triangles[t + 4] = b; triangles[t + 5] = d;
                }
            return MakeMesh("Photo hollow tapered pot", vertices, triangles);
        }

        static Mesh Leaf(float length, float width, float curve)
        {
            var frontVertices = new[]
            {
                Vector3.zero, new Vector3(-width * .62f, length * .24f, curve * .12f),
                new Vector3(width * .62f, length * .24f, curve * .12f),
                new Vector3(-width, length * .57f, curve * .55f),
                new Vector3(width, length * .57f, curve * .55f),
                new Vector3(-width * .45f, length * .84f, curve * .89f),
                new Vector3(width * .45f, length * .84f, curve * .89f),
                new Vector3(0f, length, curve)
            };
            int[] front = { 0, 1, 2, 1, 3, 2, 2, 3, 4, 3, 5, 4, 4, 5, 6, 5, 7, 6 };
            // Separate back-face vertices preserve opposite normals for URP lighting.
            var vertices = new Vector3[frontVertices.Length * 2];
            Array.Copy(frontVertices, vertices, frontVertices.Length);
            Array.Copy(frontVertices, 0, vertices, frontVertices.Length, frontVertices.Length);
            var triangles = new int[front.Length * 2];
            Array.Copy(front, triangles, front.Length);
            for (int i = 0; i < front.Length; i += 3)
            {
                triangles[front.Length + i] = front[i + 2] + frontVertices.Length;
                triangles[front.Length + i + 1] = front[i + 1] + frontVertices.Length;
                triangles[front.Length + i + 2] = front[i] + frontVertices.Length;
            }
            return MakeMesh("Photo lanceolate leaf", vertices, triangles);
        }

        static Mesh Torus(float radius, float tube)
        {
            const int sides = 24, cross = 6;
            var vertices = new Vector3[sides * cross];
            var triangles = new int[sides * cross * 6];
            for (int i = 0; i < sides; i++)
                for (int j = 0; j < cross; j++)
                {
                    float a = i * Mathf.PI * 2f / sides, b = j * Mathf.PI * 2f / cross;
                    float r = radius + Mathf.Cos(b) * tube;
                    vertices[i * cross + j] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, Mathf.Sin(b) * tube);
                    int ni = (i + 1) % sides, nj = (j + 1) % cross, t = (i * cross + j) * 6;
                    triangles[t] = i * cross + j; triangles[t + 1] = ni * cross + j; triangles[t + 2] = i * cross + nj;
                    triangles[t + 3] = i * cross + nj; triangles[t + 4] = ni * cross + j; triangles[t + 5] = ni * cross + nj;
                }
            return MakeMesh("Photo bicycle wheel ring", vertices, triangles);
        }

        static Mesh MakeMesh(string name, Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
