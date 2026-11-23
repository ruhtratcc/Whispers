// Ferramenta da escola para construção ou acabamento.

using System;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>
    /// Furnishes the photo-confirmed refectory without changing registered walls,
    /// native doors, local grades, roofs or the existing connector routes.
    /// Run after SchoolCanteenConnector.Build and before static mesh batching.
    /// </summary>
    public static class SchoolRefectoryPhotoPass
    {
        const string Block = "central_east_services";
        const string GroupName = "Refeitório — detalhes das fotos";
        const float Scale = .07055555556f;
        const float Floor = .007f;
        const float West = 1320.64f, East = 1463.08f, North = 808.64f, South = 958.88f;
        const string Materials = "Assets/SchoolCEEP/Materials/";

        [Serializable]
        public sealed class Report
        {
            public bool photographicDimensionsEstimated = true;
            public bool originalWallCollidersPreserved = true;
            public bool originalDoorsAndGradesPreserved = true;
            public int tableSets, attachedBenches, servingBays, carts, noticeboards;
            public int fixtures, roofFrames, roofRibs, addedColliders, meshes;
            public int interactiveGrilleLeaves;
            public float eastCirculationWidthMeters, roofUndersideMeters, connectorCeilingMeters;
            public string[] photoSources, notes;
        }

        static Transform root;
        static Report report;
        static Material white, cream, navyTile, steel, stainless, roof, grey, granite, paper, cork, glass, glow;

        public static Report Build(Transform architecture, Transform parent)
        {
            if (!architecture || !parent) throw new ArgumentNullException("Provide school architecture and a photographic-detail group.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build refectory details outside Play mode.");
            var serviceBlock = architecture.Find(Block);
            var connector = architecture.Find("Ligação cantina e bloco");
            if (!serviceBlock || !connector)
                throw new InvalidOperationException("Run school architecture, photographic materials and SchoolCanteenConnector before this pass.");
            var previous = parent.Find(GroupName);
            if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            root = Group(parent, GroupName);
            report = new Report
            {
                eastCirculationWidthMeters = (East - 1392f) * Scale - .84f,
                roofUndersideMeters = 4.16f,
                connectorCeilingMeters = 2.50f,
                photoSources = new[]
                {
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-41b23435-dd06-4d5c-954f-599c6411c85a.png",
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-a1942138-9acd-4f18-8b83-83031c71845b.png",
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-f42475ae-c27a-4275-8527-b5049a71996d.png",
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-e54792f1-1460-4c10-a9e3-19cf573d059d.png",
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-b97873f8-d13c-4f11-803f-f8fb0da2e682.png",
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-b9ff1785-5fab-4df6-ad5d-0af409068e0f.png",
                    "C:/Users/ruhtra/AppData/Local/Temp/codex-clipboard-8711a2dc-0511-4d1b-8c8a-d9a5b5753d03.png"
                },
                notes = new[]
                {
                    "The user identifies these seven photographs as the refectory; the last photograph shows its connection with the classroom blocks, not Automotive Maintenance.",
                    "The PDF crop labels the refectory within approximately x1320.64..1463.08 / y808.64..958.88. rooms.json does not contain a refectory row; placement therefore uses the registered service footprint.",
                    "Six cream table/bench assemblies occupy the west bank, leaving a broad east circulation strip and the existing north entrance/door approaches. Native colliders are used only for table tops, benches, two parked carts and the bin.",
                    "Serving bays are shallow visual recesses over existing solid wall spans. Neither the wall colliders nor native doors are removed. Kitchen detail is partial scenic dressing, with no invented traversable kitchen opening.",
                    "The two glass service bays are associated with solid east-wall spans y896.353..913.16 and y941.48..958.88. The closed corrugated hatch is narrower than the photograph because its northern registered solid span is short; association and sizes are estimates.",
                    "Exposed dark steel trusses and corrugated sheet detail sit below the original roof, preserving its footprint and collision. Its clear height is now an estimated4.20m, with the wall against the hall rising to that hall roof profile; neither height was surveyed.",
                    "A smooth white finish is added below the connector's existing sloped roof. Its existing lights are moved under that finish. The fixed front grilles, native west refectory door and low routes remain unchanged; an open folded side grille is visual only.",
                    "No people, new gameplay interactions, story triggers, audio sources, signs copied from photographs, Meshy assets or menu/office changes are added. Validate the whole map with native capsule traversal after integration."
                }
            };
            MaterialsSetup();
            SurfaceFinish(serviceBlock);
            RoofDetail();
            Tables(architecture);
            ServingBays();
            // Park against solid service spans, outside the kitchen openings.
            Cart(P(1448f, 904.8f, Floor));
            Cart(P(1448f, 950f, Floor));
            Bin(P(1458f, 955f, Floor));
            Noticeboard(P(1322.7f, 917f, 1.72f), Quaternion.Euler(0f, 90f, 0f), 1.42f, .95f);
            ConnectorFinish(connector);
            DoorFinish(architecture);
            return report;
        }

        static Material Existing(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
            if (!material) throw new InvalidOperationException("Missing school photographic material " + name);
            return material;
        }

        static Material Mat(string name, Color color, float smooth = .1f, float metallic = 0f)
        {
            var value = AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
            if (!value)
            {
                value = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(value, Materials + name + ".mat");
            }
            value.SetColor("_BaseColor", color);
            value.SetFloat("_Smoothness", smooth);
            value.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(value);
            return value;
        }

        static void MaterialsSetup()
        {
            white = Existing("PhotoPlasterWhite");
            cream = Mat("PhotoRefectoryCream", new Color(.76f, .75f, .68f), .34f);
            steel = Existing("PhotoRoofSteel");
            stainless = Mat("PhotoRefectoryStainless", new Color(.48f, .51f, .51f), .60f, .82f);
            roof = Existing("PhotoRoofGrey");
            grey = Mat("PhotoRefectoryRubber", new Color(.025f, .029f, .031f), .20f);
            navyTile = Mat("PhotoRefectoryBlueTiles", Color.white, .50f);
            granite = Mat("PhotoRefectoryGranite", new Color(.58f, .57f, .53f), .33f);
            paper = Mat("PhotoRefectoryNoticePaper", new Color(.76f, .75f, .66f));
            cork = Mat("PhotoRefectoryNoticeBlue", new Color(.04f, .115f, .29f), .15f);
            glow = Mat("PhotoRefectoryLamp", new Color(.75f, .76f, .70f), .18f);
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(.75f, .83f, .77f) * 1.05f);
            glass = Mat("PhotoRefectoryServiceGlass", new Color(.42f, .54f, .54f, .24f), .72f, .1f);
            glass.SetFloat("_Surface", 1f); glass.SetFloat("_ZWrite", 0f);
            glass.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.renderQueue = 3000;
            glass.SetShaderPassEnabled("ShadowCaster", false);
            TileTexture(navyTile);
            GraniteTexture(granite);
        }

        static void TileTexture(Material material)
        {
            const string path = Materials + "PhotoRefectoryTileGrid.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture)
            {
                const int size = 128, cell = 16;
                texture = new Texture2D(size, size, TextureFormat.RGB24, true) { name = "Small ceramic tile joints", wrapMode = TextureWrapMode.Repeat };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float value = .92f + Mathf.PerlinNoise(x * .09f, y * .09f) * .07f;
                    pixels[y * size + x] = x % cell < 1 || y % cell < 1
                        ? new Color(.52f, .54f, .56f)
                        : new Color(.055f, .12f, .29f) * value;
                }
                texture.SetPixels(pixels); texture.Apply(true, false); AssetDatabase.CreateAsset(texture, path);
            }
            material.SetTexture("_BaseMap", texture); material.SetTextureScale("_BaseMap", Vector2.one);
        }

        static void GraniteTexture(Material material)
        {
            const string path = Materials + "PhotoRefectoryGraniteGrain.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture)
            {
                const int size = 128;
                texture = new Texture2D(size, size, TextureFormat.RGB24, true) { name = "Granite grain", wrapMode = TextureWrapMode.Repeat };
                var random = new System.Random(1437); var pixels = new Color[size * size];
                for (int i = 0; i < pixels.Length; i++)
                {
                    float value = .70f + (float)random.NextDouble() * .27f;
                    if (i % 19 == 0) value *= .61f;
                    pixels[i] = new Color(value, value, value);
                }
                texture.SetPixels(pixels); texture.Apply(true, false); AssetDatabase.CreateAsset(texture, path);
            }
            material.SetTexture("_BaseMap", texture); material.SetTextureScale("_BaseMap", Vector2.one * 4f);
        }

        static Transform Group(Transform parent, string name)
        {
            var value = new GameObject(name) { isStatic = true }.transform;
            value.SetParent(parent, false); return value;
        }

        static Vector3 P(float x, float y, float height = 0f) => SchoolMapBuilder.Point(x, y, height);

        static GameObject Cube(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material, bool collision = false)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name; item.isStatic = true; item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition; item.transform.localScale = size;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            else report.addedColliders++;
            report.meshes++; return item;
        }

        static GameObject Round(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material, bool sphere = false)
        {
            var item = GameObject.CreatePrimitive(sphere ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
            item.name = name; item.isStatic = true; item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition; item.transform.localScale = size;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            report.meshes++; return item;
        }

        static GameObject Beam(Transform parent, string name, Vector3 a, Vector3 b, float width, Material material)
        {
            var item = Cube(parent, name, (a + b) * .5f, new Vector3(width, width, Vector3.Distance(a, b)), material);
            item.transform.localRotation = Quaternion.LookRotation(b - a); return item;
        }

        static void SurfaceFinish(Transform serviceBlock)
        {
            foreach (var renderer in serviceBlock.GetComponentsInChildren<MeshRenderer>())
            {
                var center = renderer.transform.position;
                float x = center.x / Scale + 1413f, y = 1628f - center.z / Scale;
                if (x < West - 3f || x > East + 3f || y < North - 4f || y > South + 4f) continue;
                if (renderer.name == "Parede — pintura inferior" || renderer.name == "Peitoril") renderer.sharedMaterial = navyTile;
            }
            Cube(root, "Refeitório — acabamento granilite sobre piso existente", P((West + East) * .5f, (North + South) * .5f, Floor * .5f),
                new Vector3((East - West) * Scale, Floor, (South - North) * Scale), granite);
        }

        static void RoofDetail()
        {
            // The raised roof underside is4.20m; photographed steel details stay below it.
            for (float x = West + 2f; x < East - 2f; x += 3f)
            {
                Beam(root, "Refeitório — nervura inferior da telha", P(x, North + 2f, 4.16f), P(x, South - 2f, 4.16f), .024f, roof);
                report.roofRibs++;
            }
            foreach (float y in new[] { 832f, 870f, 908f, 945f })
            {
                var a = P(West + 2f, y, 3.905f);
                var b = P(East - 2f, y, 3.905f);
                var peak = (a + b) * .5f + Vector3.up * .20f;
                Beam(root, "Refeitório — banzo inferior da tesoura", a, b, .06f, steel);
                Beam(root, "Refeitório — banzo superior esquerdo", a, peak, .055f, steel);
                Beam(root, "Refeitório — banzo superior direito", peak, b, .055f, steel);
                for (int i = 1; i < 8; i++)
                {
                    var bottom = Vector3.Lerp(a, b, i / 8f);
                    var top = bottom + Vector3.up * (.20f * (1f - Mathf.Abs(i / 4f - 1f)));
                    Beam(root, "Refeitório — montante da tesoura", bottom, top, .029f, steel);
                    if (i < 7)
                    {
                        var other = Vector3.Lerp(a, b, (i + 1) / 8f);
                        Beam(root, "Refeitório — diagonal da tesoura", i % 2 == 0 ? bottom : top,
                            i % 2 == 0 ? other + Vector3.up * (.20f * (1f - Mathf.Abs((i + 1) / 4f - 1f))) : other, .022f, steel);
                    }
                }
                report.roofFrames++;
            }
            Beam(root, "Refeitório — eletroduto suspenso", P(1392f, North + 4f, 3.82f), P(1392f, South - 4f, 3.82f), .025f, cream);
            foreach (var at in new[] { new Vector2(1351f, 860f), new Vector2(1425f, 860f), new Vector2(1351f, 920f), new Vector2(1425f, 920f) })
                Fixture(root, P(at.x, at.y, 3.82f), .72f, 6f);
        }

        static void Tables(Transform architecture)
        {
            foreach (float x in new[] { 1350f, 1392f }) foreach (float y in new[] { 865f, 899f, 933f })
            {
                Vector3 position = P(x, y, Floor);
                if (DoorApproach(architecture, position, new Vector3(1.68f, .8f, 2.04f))) continue;
                var table = Group(root, "Refeitório — mesa com dois bancos metálicos"); table.localPosition = position;
                Cube(table, "Tampo claro laminado", new Vector3(0f, .755f, 0f), new Vector3(.74f, .045f, 2.02f), cream, true);
                Cube(table, "Borda fina do tampo", new Vector3(0f, .728f, 0f), new Vector3(.71f, .020f, 1.98f), stainless);
                foreach (float side in new[] { -1f, 1f })
                {
                    Cube(table, "Banco acoplado — assento claro", new Vector3(side * .67f, .43f, 0f), new Vector3(.31f, .043f, 2.02f), cream, true);
                    Cube(table, "Banco acoplado — travessa longitudinal", new Vector3(side * .67f, .382f, 0f), new Vector3(.024f, .04f, 1.82f), stainless);
                    foreach (float z in new[] { -.74f, .74f })
                    {
                        Cube(table, "Mesa — pé tubular", new Vector3(side * .30f, .345f, z), new Vector3(.036f, .69f, .036f), stainless);
                        Cube(table, "Banco — pé tubular", new Vector3(side * .67f, .198f, z), new Vector3(.036f, .396f, .036f), stainless);
                        Cube(table, "Ligação mesa e banco", new Vector3(side * .47f, .09f, z), new Vector3(.37f, .035f, .04f), stainless);
                        Cube(table, "Sapata de borracha", new Vector3(side * .67f, .014f, z), new Vector3(.06f, .028f, .07f), grey);
                    }
                    report.attachedBenches++;
                }
                report.tableSets++;
            }
        }

        static bool DoorApproach(Transform architecture, Vector3 position, Vector3 size)
        {
            var area = new Bounds(new Vector3(position.x, 0f, position.z), new Vector3(size.x + .90f, 2f, size.z + .90f));
            foreach (var door in architecture.GetComponentsInChildren<InteractiveDoor>(true))
            {
                if (!door.leafCollider) continue;
                var leaf = door.leafCollider; var center = leaf.transform.TransformPoint(leaf.center);
                if (center.y - Mathf.Abs(leaf.size.y * leaf.transform.lossyScale.y) * .5f > .2f) continue;
                center.y = 0f; var normal = leaf.transform.forward; normal.y = 0f; normal.Normalize();
                if (area.IntersectRay(new Ray(center - normal * 2f, normal), out float distance) && distance <= 4f) return true;
            }
            return false;
        }

        static Transform ServingFrame(float pdfY, float width, string name)
        {
            var bay = Group(root, name); bay.localPosition = P(1464.16f, pdfY) + Vector3.left * .20f;
            bay.localRotation = Quaternion.Euler(0f, -90f, 0f);
            Cube(bay, "Nicho — painel recuado", new Vector3(0f, 1.80f, -.017f), new Vector3(width, 1.24f, .025f), grey);
            foreach (float x in new[] { -width * .5f, width * .5f })
                Cube(bay, "Caixilho metálico lateral", new Vector3(x, 1.80f, .06f), new Vector3(.055f, 1.32f, .10f), stainless);
            foreach (float h in new[] { 1.15f, 2.45f })
                Cube(bay, "Caixilho metálico horizontal", new Vector3(0f, h, .06f), new Vector3(width + .055f, .055f, .10f), stainless);
            Cube(bay, "Peitoril granítico de atendimento", new Vector3(0f, 1.135f, .16f), new Vector3(width + .23f, .07f, .39f), granite);
            report.servingBays++; return bay;
        }

        static void ServingBays()
        {
            var closed = ServingFrame(815.4f, .73f, "Refeitório — guichê com persiana fechada");
            Cube(closed, "Persiana de chapa clara", new Vector3(0f, 1.80f, .035f), new Vector3(.69f, 1.22f, .025f), cream);
            for (int i = 0; i < 20; i++) Cube(closed, "Persiana — dobra horizontal", new Vector3(0f, 1.21f + i * .061f, .055f), new Vector3(.69f, .012f, .018f), stainless);
            Cube(closed, "Persiana — puxador", new Vector3(0f, 1.28f, .077f), new Vector3(.12f, .035f, .018f), stainless);
            var awning = ServingFrame(904.8f, 1.04f, "Refeitório — janela basculante de atendimento");
            KitchenBackdrop(awning, 1.04f);
            var sash = Group(awning, "Folha basculante aberta — visual"); sash.localPosition = new Vector3(0f, 2.39f, .105f);
            sash.localRotation = Quaternion.Euler(-28f, 0f, 0f);
            Cube(sash, "Vidro da basculante", new Vector3(0f, -.58f, 0f), new Vector3(.99f, 1.16f, .012f), glass);
            foreach (float x in new[] { -.51f, .51f }) Cube(sash, "Perfil da folha basculante", new Vector3(x, -.58f, 0f), new Vector3(.032f, 1.2f, .03f), stainless);
            Cube(sash, "Perfil inferior da folha basculante", new Vector3(0f, -1.17f, 0f), new Vector3(1.05f, .035f, .03f), stainless);
            var sliding = ServingFrame(950f, 1.06f, "Refeitório — janela de correr de atendimento");
            KitchenBackdrop(sliding, 1.06f);
            foreach (float x in new[] { -.26f, .26f })
                Cube(sliding, "Vidro da folha de correr", new Vector3(x, 1.80f, x < 0f ? .071f : .09f), new Vector3(.50f, 1.2f, .014f), glass);
            Cube(sliding, "Montante central da janela", new Vector3(0f, 1.80f, .10f), new Vector3(.035f, 1.25f, .025f), stainless);
        }

        static void KitchenBackdrop(Transform bay, float width)
        {
            Cube(bay, "Cozinha cênica — azulejos claros", new Vector3(0f, 1.80f, .002f), new Vector3(width - .05f, 1.16f, .018f), cream);
            for (int x = -2; x <= 2; x++) Cube(bay, "Cozinha cênica — junta vertical", new Vector3(x * (width - .08f) / 5f, 1.80f, .015f), new Vector3(.004f, 1.16f, .003f), stainless);
            for (int y = 0; y < 5; y++) Cube(bay, "Cozinha cênica — junta horizontal", new Vector3(0f, 1.25f + y * .23f, .015f), new Vector3(width - .05f, .004f, .003f), stainless);
            Cube(bay, "Cozinha cênica — bancada ao fundo", new Vector3(0f, 1.30f, .065f), new Vector3(width - .14f, .035f, .10f), stainless);
            Round(bay, "Cozinha cênica — panela de inox", new Vector3(-width * .20f, 1.44f, .07f), new Vector3(.23f, .10f, .18f), stainless);
            Round(bay, "Cozinha cênica — tampa da panela", new Vector3(-width * .20f, 1.55f, .07f), new Vector3(.25f, .009f, .20f), stainless);
            Cube(bay, "Cozinha cênica — coifa", new Vector3(0f, 2.27f, .05f), new Vector3(width * .60f, .18f, .10f), stainless);
        }

        static void Cart(Vector3 position)
        {
            var cart = Group(root, "Refeitório — carrinho expositor de inox"); cart.localPosition = position;
            cart.localScale = new Vector3(1f, 1f, 1.10f / 1.24f);
            Cube(cart, "Carrinho — bancada", new Vector3(0f, .82f, 0f), new Vector3(.66f, .055f, 1.24f), stainless, true);
            Cube(cart, "Carrinho — prateleira inferior", new Vector3(0f, .31f, 0f), new Vector3(.58f, .04f, 1.15f), stainless);
            foreach (float x in new[] { -.28f, .28f }) foreach (float z in new[] { -.52f, .52f })
            {
                Cube(cart, "Carrinho — poste tubular", new Vector3(x, .52f, z), new Vector3(.032f, .92f, .032f), stainless);
                var wheel = Round(cart, "Carrinho — rodízio de borracha", new Vector3(x, .072f, z), new Vector3(.13f, .035f, .13f), grey);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            foreach (float z in new[] { -.54f, .54f })
            {
                Vector3 previous = new Vector3(-.28f, .865f, z);
                for (int i = 1; i <= 6; i++)
                {
                    float t = i / 6f; var next = new Vector3(Mathf.Lerp(-.28f, .28f, t), .865f + Mathf.Sin(t * Mathf.PI) * .35f, z);
                    Beam(cart, "Carrinho — arco do protetor", previous, next, .022f, stainless); previous = next;
                }
            }
            Cube(cart, "Carrinho — proteção de vidro superior", new Vector3(0f, 1.12f, 0f), new Vector3(.43f, .018f, 1.08f), glass);
            Cube(cart, "Carrinho — bandeja de serviço", new Vector3(0f, .86f, .03f), new Vector3(.40f, .022f, .64f), cream);
            report.carts++;
        }

        static void Bin(Vector3 position)
        {
            var bin = Group(root, "Refeitório — lixeira clara"); bin.localPosition = position;
            Cube(bin, "Lixeira — corpo", new Vector3(0f, .33f, 0f), new Vector3(.34f, .66f, .31f), cream, true);
            Cube(bin, "Lixeira — aro preto", new Vector3(0f, .66f, 0f), new Vector3(.36f, .055f, .33f), grey);
            Cube(bin, "Lixeira — tampa", new Vector3(0f, .705f, -.026f), new Vector3(.35f, .045f, .275f), cream);
            Cube(bin, "Lixeira — pedal", new Vector3(0f, .035f, .175f), new Vector3(.17f, .022f, .07f), grey);
        }

        static void Noticeboard(Vector3 center, Quaternion rotation, float width, float height)
        {
            var board = Group(root, "Refeitório — mural de avisos"); board.localPosition = center; board.localRotation = rotation;
            Cube(board, "Mural — fundo azul", Vector3.zero, new Vector3(width, height, .027f), cork);
            foreach (float x in new[] { -width * .5f, width * .5f }) Cube(board, "Mural — moldura lateral", new Vector3(x, 0f, .012f), new Vector3(.035f, height + .06f, .055f), granite);
            foreach (float y in new[] { -height * .5f, height * .5f }) Cube(board, "Mural — moldura horizontal", new Vector3(0f, y, .012f), new Vector3(width + .07f, .035f, .055f), granite);
            var random = new System.Random(953);
            for (int i = 0; i < 8; i++)
            {
                float x = -width * .35f + i % 4 * width * .225f, y = i < 4 ? height * .21f : -height * .20f;
                var notice = Cube(board, "Mural — aviso em papel", new Vector3(x, y, .020f), new Vector3(width * .18f, height * .31f, .002f), paper);
                notice.transform.localRotation = Quaternion.Euler(0f, 0f, -3f + (float)random.NextDouble() * 6f);
                for (int line = 0; line < 4; line++) Cube(board, "Mural — linhas impressas", new Vector3(x, y + .06f - line * .035f, .023f), new Vector3(width * .125f, .008f, .001f), stainless);
            }
            report.noticeboards++;
        }

        static void ConnectorFinish(Transform connector)
        {
            const float west = 1213.2f, east = 1315.8f, rear = 809.24f, front = 851.12f;
            Cube(root, "Ligação refeitório/bloco — forro liso branco", P((west + east) * .5f, (rear + front) * .5f, 2.525f),
                new Vector3((east - west) * Scale - .16f, .05f, (front - rear) * Scale - .14f), white);
            // Keep the existing exterior roof and fixed-front grille collision.
            foreach (var value in connector.GetComponentsInChildren<MeshRenderer>().ToArray())
            {
                if (value.name == "Vestíbulo — carcaça de luminária sob telhado" || value.name == "Vestíbulo — difusor branco de luminária"
                    || value.name == "Vestíbulo — fixação da luminária") UnityEngine.Object.DestroyImmediate(value.gameObject);
            }
            foreach (var light in connector.GetComponentsInChildren<Light>())
            {
                var position = light.transform.position; position.y = 2.31f; light.transform.position = position;
                var center = root.InverseTransformPoint(position); center.y = 2.435f;
                Round(root, "Ligação refeitório/bloco — lâmpada sob forro", center, new Vector3(.10f, .135f, .10f), glow, true);
                Cube(root, "Ligação refeitório/bloco — soquete", center + Vector3.up * .051f, new Vector3(.06f, .047f, .06f), cream);
            }
            var folded = Group(root, "Ligação refeitório/bloco — grade lateral recolhida");
            folded.localPosition = P(1314.5f, 817f, .025f);
            for (int panel = 0; panel < 3; panel++)
            {
                float z = -.21f + panel * .21f;
                foreach (float offset in new[] { -.075f, 0f, .075f })
                    Cube(folded, "Grade recolhida — barra vertical", new Vector3(panel % 2 == 0 ? -.025f : .025f, 1.15f, z + offset), new Vector3(.035f, 2.30f, .02f), cork);
                foreach (float h in new[] { .10f, 1.10f, 2.25f })
                    Cube(folded, "Grade recolhida — travessa", new Vector3(0f, h, z), new Vector3(.065f, .031f, .17f), cork);
            }
            Noticeboard(P(1229f, 811f, 1.56f), Quaternion.Euler(0f, 180f, 0f), .90f, .73f);
        }

        static void DoorFinish(Transform architecture)
        {
            foreach(var door in architecture.GetComponentsInChildren<InteractiveDoor>(true))
            {
                if(!door.leafCollider)continue;
                var leaf=door.leafCollider;var c=leaf.transform.TransformPoint(leaf.center);
                float x=1413f+c.x/Scale,y=1628f-c.z/Scale;
                if(c.y>2f)continue;
                if(Mathf.Abs(x-1464.78f)<3f&&Mathf.Abs(y-889.98f)<12f)
                {
                    var painted=leaf.GetComponent<MeshRenderer>();
                    if(painted){painted.sharedMaterial=Existing("PhotoDoorCream");PrefabUtility.RecordPrefabInstancePropertyModifications(painted);}
                }
                if(Mathf.Abs(x-1320.56f)>3f||Mathf.Abs(y-836.74f)>12f)continue;
                var oldBody=leaf.GetComponent<MeshRenderer>();
                if(oldBody){oldBody.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(oldBody);}
                var grille=Group(leaf.transform,"Refeitório — grade azul interativa");
                grille.localPosition=leaf.center;grille.gameObject.isStatic=false;
                var size=leaf.size;var scale=leaf.transform.lossyScale;
                var blue=Existing("PhotoFrameBlue");
                float barX=.025f/Mathf.Abs(scale.x),barY=.032f/Mathf.Abs(scale.y),depth=.035f/Mathf.Abs(scale.z);
                int count=Mathf.Max(4,Mathf.CeilToInt(size.x*Mathf.Abs(scale.x)/.13f));
                for(int i=0;i<=count;i++)Cube(grille,"Grade móvel — barra vertical",new Vector3(Mathf.Lerp(-size.x*.48f,size.x*.48f,i/(float)count),0,0),new Vector3(barX,size.y*.98f,depth),blue);
                foreach(float h in new[]{-.48f,0f,.48f})Cube(grille,"Grade móvel — travessa",new Vector3(0,h*size.y,0),new Vector3(size.x*.99f,barY,depth),blue);
                foreach(var item in grille.GetComponentsInChildren<Transform>())item.gameObject.isStatic=false;
                report.interactiveGrilleLeaves++;
            }
        }

        static void Fixture(Transform parent, Vector3 center, float intensity, float range)
        {
            Round(parent, "Refeitório — corpo de plafon", center, new Vector3(.23f, .026f, .23f), stainless);
            Round(parent, "Refeitório — difusor luminoso", center + Vector3.down * .034f, new Vector3(.19f, .016f, .19f), glow);
            var value = new GameObject("Refeitório — iluminação sob tesoura") { isStatic = true };
            value.transform.SetParent(parent, false); value.transform.localPosition = center + Vector3.down * .09f;
            var light = value.AddComponent<Light>(); light.type = LightType.Point; light.range = range; light.intensity = intensity;
            light.color = new Color(.78f, .83f, .78f); light.shadows = LightShadows.None; report.fixtures++;
        }
    }
}
