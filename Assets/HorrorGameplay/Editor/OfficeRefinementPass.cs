// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace HorrorGame.Editor
{
    /// <summary>Targeted, repeatable refinement of the existing office. No scene regeneration.</summary>
    public static class OfficeRefinementPass
    {
        const string ScenePath = "Assets/ChapterOneOffice/Scenes/Chapter1Office.unity";
        const string AssetRoot = "Assets/ChapterOneOffice/";
        const string RootName = "Escritório de João de Claus — Capítulo 1";
        static Transform room;
        static int world;
        static Material timber, darkWood, metal, leather, brass, black, paper, plaster;

        // An explicit CLI import creates this one-shot request. Ordinary project reloads do nothing.
        [InitializeOnLoadMethod]
        static void ApplyExplicitImportRequest()
        {
            const string requestPath = AssetRoot + "Documentation/Office-refine.request";
            if (!File.Exists(requestPath)) return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(requestPath)) return;
                File.Delete(requestPath);
                try { WhenStopped(() => { Apply(); ValidateAndCapture(); }); }
                catch (Exception e)
                {
                    File.WriteAllText(AssetRoot + "Documentation/Office-refinement-error.txt", e.ToString());
                    Debug.LogException(e);
                }
            };
        }

        [MenuItem("Tools/Whispers of Past/Refine office %#o")]
        public static void ApplyRequested() => WhenStopped(Apply);

        static void WhenStopped(Action action)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
            { action(); return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
            void Poll()
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                EditorApplication.update -= Poll;
                try { action(); } catch (Exception e) { Debug.LogException(e); }
            }
            // Registered only for this explicit request, never by InitializeOnLoad.
            EditorApplication.update += Poll;
        }

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Pare o Play Mode antes de melhorar o escritório.");
            if (SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Abra Chapter1Office antes de executar este passe.");
            room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName)?.transform;
            if (!room) throw new InvalidOperationException("Não foi encontrada a raiz do escritório existente.");
            world = LayerMask.NameToLayer("World");
            if (world < 0) world = 0;
            EnsureFolder("Models"); EnsureFolder("Materials");
            timber = LoadMat("Walnut grain"); darkWood = LoadMat("Dark walnut");
            metal = LoadMat("Aged gunmetal"); leather = LoadMat("Brown leather");
            brass = LoadMat("Tarnished brass"); black = LoadMat("Black bakelite");
            paper = LoadMat("Aged paper"); plaster = LoadMat("Plaster — worn ochre");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Refinar escritório de João");
            Undo.RegisterFullObjectHierarchyUndo(room.gameObject, "Refinar escritório de João");

            OrientDesk();
            PositionFurniture();
            RebuildChairs();
            RebuildBin();
            OpenWindowAndLightExterior();
            FixLightingArtifacts();

            // New meshes are persistent assets; no runtime generation or gameplay modification.
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("Office refinement complete.\n" + Inspect());
        }

        static void EnsureFolder(string name)
        {
            if (!AssetDatabase.IsValidFolder(AssetRoot.TrimEnd('/') + "/" + name))
                AssetDatabase.CreateFolder(AssetRoot.TrimEnd('/'), name);
        }

        static Material LoadMat(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "Materials/" + name + ".mat");
            if (!material) throw new InvalidOperationException("Material do escritório ausente: " + name);
            return material;
        }

        static Transform Find(string name) => room.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        static Transform Require(string name) => Find(name) ? Find(name) : throw new InvalidOperationException("Objeto do escritório ausente: " + name);

        static Transform Group(string name, Transform parent, Vector3 position = default, Vector3 angles = default)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false); t.localPosition = position; t.localRotation = Quaternion.Euler(angles);
            t.gameObject.layer = world;
            return t;
        }

        static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        static void RemoveNamed(string name)
        {
            foreach (var t in room.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray())
                if (t) UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, Vector3 angles = default, bool collider = false)
        {
            var g = GameObject.CreatePrimitive(type); g.name = name; g.layer = world;
            g.transform.SetParent(parent, false); g.transform.localPosition = position;
            g.transform.localRotation = Quaternion.Euler(angles); g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
            return g;
        }

        static GameObject MeshObject(string name, Mesh mesh, Material material, Transform parent, Vector3 position, Vector3 angles = default)
        {
            var g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            g.layer = world; g.transform.SetParent(parent, false); g.transform.localPosition = position;
            g.transform.localRotation = Quaternion.Euler(angles);
            g.GetComponent<MeshFilter>().sharedMesh = mesh; g.GetComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
            return g;
        }

        static void Rod(string name, Vector3 a, Vector3 b, float radius, Material material, Transform parent)
        {
            var g = Primitive(name, PrimitiveType.Cylinder, parent, (a + b) * .5f,
                new Vector3(radius * 2, Vector3.Distance(a, b) * .5f, radius * 2), material);
            g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        }

        static void Curve(string name, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float radius, Material material, Transform parent, int segments = 9)
        {
            Vector3 previous = a;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments, u = 1 - t;
                Vector3 next = u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
                Rod(name, previous, next, radius, material, parent); previous = next;
            }
        }

        static Mesh SaveMesh(string filename, Func<Mesh> make)
        {
            string path = AssetRoot + "Models/OfficeRefined_" + filename + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh) return mesh;
            mesh = make(); mesh.name = filename; AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        // Four rounded perimeter rings form a cushion with a domed top and softened edges.
        static Mesh Cushion(string name, float width, float depth, float height, float radius)
        {
            return SaveMesh(name, () =>
            {
                const int perCorner = 10, count = perCorner * 4;
                float[] heights = { -.5f, -.34f, .28f, .5f };
                float[] insets = { .018f, 0, 0, .022f };
                var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
                for (int ring = 0; ring < heights.Length; ring++)
                {
                    float hx = width * .5f - insets[ring], hz = depth * .5f - insets[ring];
                    float r = Mathf.Min(radius, Mathf.Min(hx, hz) * .8f);
                    for (int corner = 0; corner < 4; corner++)
                    {
                        float start = corner * Mathf.PI * .5f;
                        float cx = corner == 0 || corner == 3 ? hx - r : -hx + r;
                        float cz = corner < 2 ? hz - r : -hz + r;
                        for (int j = 0; j < perCorner; j++)
                        {
                            float angle = start + j / (float)(perCorner - 1) * Mathf.PI * .5f;
                            Vector3 p = new Vector3(cx + Mathf.Cos(angle) * r, heights[ring] * height, cz + Mathf.Sin(angle) * r);
                            vertices.Add(p); uv.Add(new Vector2(p.x / width + .5f, p.z / depth + .5f));
                        }
                    }
                }
                void T(int a, int b, int c, Vector3 outward)
                {
                    if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), outward) < 0)
                    { int swap = b; b = c; c = swap; }
                    tri.Add(a); tri.Add(b); tri.Add(c);
                }
                for (int ring = 0; ring < heights.Length - 1; ring++) for (int i = 0; i < count; i++)
                {
                    int a = ring * count + i, b = ring * count + (i + 1) % count, c = b + count, d = a + count;
                    Vector3 outwards = new Vector3(vertices[a].x, 0, vertices[a].z);
                    T(a, b, c, outwards); T(a, c, d, outwards);
                }
                int bottom = vertices.Count; vertices.Add(new Vector3(0, -.5f * height, 0)); uv.Add(new Vector2(.5f, .5f));
                int top = vertices.Count; vertices.Add(new Vector3(0, .54f * height, 0)); uv.Add(new Vector2(.5f, .5f));
                for (int i = 0; i < count; i++)
                {
                    T(bottom, i, (i + 1) % count, Vector3.down);
                    T(top, 3 * count + i, 3 * count + (i + 1) % count, Vector3.up);
                }
                var m = new Mesh(); m.SetVertices(vertices); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
                m.RecalculateNormals(); m.RecalculateBounds(); return m;
            });
        }

        static Mesh Torus(string name, float radius, float tube)
        {
            return SaveMesh(name, () =>
            {
                const int sides = 48, section = 8;
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
                for (int i = 0; i <= sides; i++) for (int j = 0; j <= section; j++)
                {
                    float a = i * Mathf.PI * 2 / sides, b = j * Mathf.PI * 2 / section;
                    v.Add(new Vector3(Mathf.Cos(a) * (radius + Mathf.Cos(b) * tube), Mathf.Sin(b) * tube,
                        Mathf.Sin(a) * (radius + Mathf.Cos(b) * tube)));
                    uv.Add(new Vector2(i / (float)sides, j / (float)section));
                }
                for (int i = 0; i < sides; i++) for (int j = 0; j < section; j++)
                {
                    int a = i * (section + 1) + j, b = a + section + 1;
                    triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
                var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(triangles, 0);
                m.RecalculateNormals(); m.RecalculateBounds(); return m;
            });
        }

        static void OrientDesk()
        {
            var desk = Require("Mesa principal — João de Claus");
            desk.localRotation = Quaternion.Euler(0, 180, 0);
            var plaque = Require("Identificação do investigador");
            plaque.localPosition = new Vector3(.15f, .90f, .46f);
            plaque.localRotation = Quaternion.Euler(0, 180, 0);
            // The monitor and keyboard face local -Z; rotating their desk turns them toward João's chair.
            var computer = Require("Computador e teclado"); computer.localRotation = Quaternion.Euler(0, -7, 0);
        }

        static void PositionFurniture()
        {
            var clock = Require("Relógio de parede");
            clock.localPosition = new Vector3(.24f, 2.66f, 3.51f); clock.localRotation = Quaternion.identity;
            var cabinets = room.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Arquivo de metal").OrderBy(t => t.GetSiblingIndex()).ToArray();
            if (cabinets.Length != 2) throw new InvalidOperationException("Esperados dois arquivos de metal existentes.");
            for (int i = 0; i < cabinets.Length; i++)
            {
                cabinets[i].localPosition = new Vector3(-3.48f, 0, i == 0 ? -2.05f : -1.38f);
                cabinets[i].localRotation = Quaternion.Euler(0, -90, 0);
            }
        }

        static void RebuildChairs()
        {
            var chairs = room.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "Cadeira de João" || t.name == "Cadeira de visitante").ToArray();
            if (chairs.Length != 3) throw new InvalidOperationException("Esperadas três cadeiras existentes.");
            foreach (var c in chairs)
            {
                bool office = c.name == "Cadeira de João";
                ClearChildren(c);
                MeshObject("Assento de couro com bordas arredondadas", Cushion("ChairSeat_v2", .54f, .51f, .10f, .075f), leather, c, new Vector3(0, .48f, 0));
                MeshObject("Base de madeira do assento", Cushion("ChairSeatWood_v2", .52f, .49f, .035f, .055f), darkWood, c, new Vector3(0, .418f, .003f));
                MeshObject("Encosto de couro inclinado", Cushion(office ? "ChairBackOffice_v2" : "ChairBackVisitor_v2", .50f, office ? .61f : .54f, .088f, .065f), leather, c,
                    new Vector3(0, office ? .88f : .845f, .258f), new Vector3(-82, 0, 0));
                for (int s = -1; s <= 1; s += 2)
                {
                    Curve("Suporte curvo do encosto", new Vector3(s * .21f, .42f, .19f), new Vector3(s * .21f, .63f, .22f),
                        new Vector3(s * .21f, .92f, .33f), new Vector3(s * .21f, 1.11f, .36f), .015f, office ? metal : timber, c);
                    Curve("Braço curvo de madeira", new Vector3(s * .287f, .645f, -.19f), new Vector3(s * .29f, .705f, -.04f),
                        new Vector3(s * .29f, .69f, .17f), new Vector3(s * .287f, .66f, .23f), .017f, timber, c);
                    MeshObject("Apoio acolchoado do braço", Cushion("ChairArmPad_v2", .073f, .30f, .036f, .025f), leather, c, new Vector3(s * .286f, .70f, .015f));
                    Rod("Suporte dianteiro do braço", new Vector3(s * .282f, .423f, -.125f), new Vector3(s * .282f, .66f, -.125f), .012f, office ? metal : timber, c);
                    Rod("Suporte traseiro do braço", new Vector3(s * .282f, .433f, .18f), new Vector3(s * .282f, .674f, .18f), .012f, office ? metal : timber, c);
                }
                if (office)
                {
                    Rod("Pistão central", new Vector3(0, .115f, 0), new Vector3(0, .418f, 0), .025f, metal, c);
                    Rod("Capa do pistão", new Vector3(0, .135f, 0), new Vector3(0, .28f, 0), .039f, black, c);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * Mathf.PI * 2 / 5;
                        Vector3 radial = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                        Vector3 end = radial * .32f + Vector3.up * .08f;
                        Curve("Pé da base estrela", new Vector3(0, .17f, 0), radial * .10f + Vector3.up * .145f,
                            radial * .25f + Vector3.up * .075f, end, .024f, black, c, 5);
                        Rod("Pino do rodízio", end, new Vector3(end.x, .047f, end.z), .008f, metal, c);
                        Vector3 axle = Vector3.Cross(Vector3.up, radial).normalized;
                        foreach (float side in new[] { -1f, 1f })
                        {
                            var wheel = Primitive("Roda vertical do rodízio", PrimitiveType.Cylinder, c,
                                new Vector3(end.x, .041f, end.z) + axle * side * .019f, new Vector3(.074f, .012f, .074f), black);
                            wheel.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axle);
                        }
                    }
                }
                else
                {
                    foreach (float x in new[] { -.215f, .215f }) foreach (float z in new[] { -.178f, .178f })
                        Rod("Pé de madeira", new Vector3(x * 1.08f, .025f, z * 1.08f), new Vector3(x, .425f, z), .019f, darkWood, c);
                    foreach (float side in new[] { -1f, 1f })
                        Rod("Travessa da cadeira", new Vector3(side * .221f, .20f, -.18f), new Vector3(side * .221f, .20f, .18f), .012f, timber, c);
                }
                var colliders = c.GetComponents<BoxCollider>();
                if (colliders.Length == 0) colliders = new[] { c.gameObject.AddComponent<BoxCollider>() };
                colliders[0].center = new Vector3(0, .59f, .055f); colliders[0].size = new Vector3(.63f, 1.18f, .64f);
                for (int i = 1; i < colliders.Length; i++) UnityEngine.Object.DestroyImmediate(colliders[i]);
                c.gameObject.layer = world;
                if (office) c.localRotation = Quaternion.identity;
            }
        }

        static Mesh CrumpledPaper(int seed)
        {
            return SaveMesh("CrumpledPaper_" + seed, () =>
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var mesh = UnityEngine.Object.Instantiate(sphere.GetComponent<MeshFilter>().sharedMesh);
                UnityEngine.Object.DestroyImmediate(sphere);
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 p = vertices[i];
                    float n = Mathf.PerlinNoise(p.x * 11 + seed * 5.17f, p.z * 9 + p.y * 7 + seed * 2.31f);
                    vertices[i] = Vector3.Scale(p * (.7f + n * .5f), new Vector3(1, .78f, .92f));
                }
                mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            });
        }

        static void RebuildBin()
        {
            RemoveNamed("Lixeira — cesto de arame");
            // Original bin/papers were standalone primitive children; preserve all unrelated documents.
            foreach (var t in room.Cast<Transform>().Where(t => t.name == "Lixeira" || t.name == "Papel amassado").ToArray())
                UnityEngine.Object.DestroyImmediate(t.gameObject);
            var bin = Group("Lixeira — cesto de arame", room, new Vector3(1.36f, 0, 1.73f));
            Primitive("Base metálica", PrimitiveType.Cylinder, bin, new Vector3(0, .014f, 0), new Vector3(.235f, .009f, .235f), metal);
            MeshObject("Aro inferior", Torus("BinBottomRim_v2", .116f, .005f), metal, bin, new Vector3(0, .03f, 0));
            MeshObject("Aro superior aberto", Torus("BinTopRim_v2", .156f, .006f), metal, bin, new Vector3(0, .365f, 0));
            for (int i = 0; i < 36; i++)
            {
                float a = i * Mathf.PI * 2 / 36;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Rod("Arame vertical", radial * .116f + Vector3.up * .03f, radial * .155f + Vector3.up * .365f, .0018f, metal, bin);
            }
            foreach (float height in new[] { .12f, .23f })
                MeshObject("Aro de reforço", Torus("BinRing_" + (int)(height * 100), Mathf.Lerp(.116f, .155f, (height - .03f) / .335f), .0023f), metal, bin, new Vector3(0, height, 0));
            for (int i = 0; i < 13; i++)
            {
                float a = i * 2.399f, r = i % 4 == 0 ? .03f : .079f;
                var p = MeshObject("Papel amassado no cesto", CrumpledPaper(i % 4), paper, bin,
                    new Vector3(Mathf.Cos(a) * r, .095f + (i / 4) * .065f, Mathf.Sin(a) * r), new Vector3(i * 31, i * 47, i * 23));
                p.transform.localScale = new Vector3(.078f, .079f, .084f);
            }
            var collider = bin.gameObject.AddComponent<BoxCollider>(); collider.center = new Vector3(0, .188f, 0); collider.size = new Vector3(.327f, .38f, .327f);
        }

        static Material ExteriorMaterial(string name, Color color, bool emissive = false)
        {
            string path = AssetRoot + "Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            m = new Material(Shader.Find(emissive ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", .1f); m.name = name;
            AssetDatabase.CreateAsset(m, path); return m;
        }

        static void OpenWindowAndLightExterior()
        {
            RemoveNamed("Trechos da parede direita — abertura real");
            // This name also exists in the vestibule: restrict changes to the office's direct child.
            var wall = room.Cast<Transform>().FirstOrDefault(t => t.name == "Parede direita");
            if (wall) UnityEngine.Object.DestroyImmediate(wall.gameObject);
            var parts = Group("Trechos da parede direita — abertura real", room);
            void Part(string name, Vector3 pos, Vector3 size)
            {
                var g = Primitive(name, PrimitiveType.Cube, parts, pos, size, plaster, collider: true);
                g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            }
            Part("Parede ao lado da entrada", new Vector3(3.96f, 1.52f, -1.945f), new Vector3(.18f, 3.04f, 3.21f));
            Part("Parede ao lado da estante", new Vector3(3.96f, 1.52f, 2.945f), new Vector3(.18f, 3.04f, 1.61f));
            Part("Parede abaixo da janela", new Vector3(3.96f, .40f, .90f), new Vector3(.18f, .80f, 2.48f));
            Part("Parede acima da janela", new Vector3(3.96f, 2.82f, .90f), new Vector3(.18f, .44f, 2.48f));
            // Invisible pane blocks the player and thrown props, while allowing rendered light in.
            var pane = Group("Colisão do vidro da janela", parts, new Vector3(3.95f, 1.7f, .9f));
            var paneCollider = pane.gameObject.AddComponent<BoxCollider>();
            paneCollider.size = new Vector3(.035f, 1.8f, 2.48f);

            // Existing wall decoration crossed the previous fake window; split it at the new opening.
            foreach (var t in room.Cast<Transform>().Where(t => (t.name == "Lambril" || t.name == "Friso") && t.localPosition.x > 3).ToArray())
                UnityEngine.Object.DestroyImmediate(t.gameObject);
            Primitive("Lambril abaixo da janela", PrimitiveType.Cube, parts, new Vector3(3.84f, .435f, .1f), new Vector3(.04f, .69f, 7.3f), LoadMat("Muted olive paint"));
            foreach (var section in new[] { new Vector2(-1.945f, 3.21f), new Vector2(2.945f, 1.61f) })
            {
                Primitive("Faixa superior do lambril", PrimitiveType.Cube, parts, new Vector3(3.84f, .855f, section.x), new Vector3(.04f, .13f, section.y), LoadMat("Muted olive paint"));
                Primitive("Friso interrompido pela janela", PrimitiveType.Cube, parts, new Vector3(3.84f, 1, section.x), new Vector3(.065f, .05f, section.y), timber);
            }
            RemoveNamed("Escuridão externa");
            RemoveNamed("Faixa de luz da persiana");
            RemoveNamed("Luz fria da rua");
            RemoveNamed("Exterior visual — iluminação da rua");
            var exterior = Group("Exterior visual — iluminação da rua", room);
            var facade = ExteriorMaterial("Night exterior plaster", new Color(.052f, .061f, .081f));
            var sky = ExteriorMaterial("Night exterior background", new Color(.012f, .022f, .043f), true);
            var bulb = ExteriorMaterial("Streetlamp warm glass", new Color(1.7f, 1.13f, .59f), true);
            // A shallow backdrop only; no traversable exterior or additional chapter map.
            Primitive("Fundo noturno discreto", PrimitiveType.Cube, exterior, new Vector3(7.7f, 2.0f, .9f), new Vector3(.035f, 4.6f, 6.2f), sky);
            Primitive("Fachada distante", PrimitiveType.Cube, exterior, new Vector3(7.2f, 1.8f, 2.28f), new Vector3(.16f, 3.6f, 2.1f), facade);
            Rod("Poste visto da janela", new Vector3(5.3f, -.2f, 1.98f), new Vector3(5.3f, 3.08f, 1.98f), .031f, metal, exterior);
            Rod("Braço do poste", new Vector3(5.3f, 3.08f, 1.98f), new Vector3(4.95f, 3.08f, 1.98f), .023f, metal, exterior);
            Primitive("Cúpula do poste", PrimitiveType.Cylinder, exterior, new Vector3(4.95f, 3.025f, 1.98f), new Vector3(.27f, .047f, .27f), metal);
            Primitive("Vidro luminoso do poste", PrimitiveType.Sphere, exterior, new Vector3(4.95f, 2.97f, 1.98f), new Vector3(.13f, .10f, .13f), bulb);

            var beamRoot = Group("Luz externa entrando pelas persianas", exterior, new Vector3(4.70f, 2.48f, .40f));
            beamRoot.localRotation = Quaternion.LookRotation(new Vector3(1.15f, .40f, 1.05f) - beamRoot.localPosition);
            var light = beamRoot.gameObject.AddComponent<Light>(); light.type = LightType.Spot;
            light.color = new Color(.53f, .67f, .91f); light.intensity = 32; light.range = 10;
            light.spotAngle = 106; light.innerSpotAngle = 65; light.shadows = LightShadows.Soft;
            light.shadowBias = .013f; light.shadowNormalBias = .045f; light.shadowNearPlane = .10f;
            var glowRoot = Group("Brilho fraco do poste", exterior, new Vector3(4.95f, 2.87f, 1.98f));
            var glow = glowRoot.gameObject.AddComponent<Light>(); glow.type = LightType.Point;
            glow.color = new Color(1, .74f, .42f); glow.intensity = 2.5f; glow.range = 2.8f; glow.shadows = LightShadows.None;
        }

        static void FixLightingArtifacts()
        {
            // Seven shadowed point lights require 42 cubemap faces; keep local fills smooth and
            // reserve shadow-map space for the external spot that actually projects the blinds.
            foreach (var light in room.GetComponentsInChildren<Light>(true))
                if (light.type == LightType.Point) light.shadows = LightShadows.None;
            foreach (var t in room.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Tábua de assoalho"))
            {
                var r = t.GetComponent<Renderer>(); if (!r) continue;
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = true;
            }
        }

        public static string Inspect()
        {
            var office = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (!office) return "Office root absent; active scene: " + SceneManager.GetActiveScene().path;
            var all = office.GetComponentsInChildren<Transform>(true);
            Transform Named(string name) => all.FirstOrDefault(t => t.name == name);
            var desk = Named("Mesa principal — João de Claus"); var clock = Named("Relógio de parede");
            var spot = Named("Luz externa entrando pelas persianas");
            return "Scene: " + SceneManager.GetActiveScene().path
                + "\nDesk yaw: " + (desk ? desk.localEulerAngles.y.ToString("F1") : "missing")
                + "\nClock: " + (clock ? clock.localPosition.ToString("F2") : "missing")
                + "\nChairs: " + all.Count(t => t.name == "Cadeira de João" || t.name == "Cadeira de visitante")
                + "\nCabinets: " + all.Count(t => t.name == "Arquivo de metal")
                + "\nOpen bin: " + (Named("Aro superior aberto") != null)
                + "\nExternal light: " + (spot ? spot.localPosition.ToString("F2") : "missing")
                + "\nInteractive notes: " + office.GetComponentsInChildren<CollectibleItem>(true).Length
                + "\nDoors: " + office.GetComponentsInChildren<InteractiveDoor>(true).Length
                + "\nDrawers: " + office.GetComponentsInChildren<InteractiveDrawer>(true).Length
                + "\nColliders: " + office.GetComponentsInChildren<Collider>(true).Length;
        }

        [Serializable]
        sealed class OfficeReport
        {
            public string scene;
            public string generatedUtc;
            public string inspection;
            public bool passed;
            public string[] errors;
            public string[] screenshots;
            public PoseInfo[] cameraPoses;
        }

        [Serializable]
        sealed class PoseInfo
        {
            public string name;
            public Vector3 position;
            public Vector3 target;
        }

        [MenuItem("Tools/Whispers of Past/Office validation and previews %#v")]
        public static void ValidateRequested() => WhenStopped(ValidateAndCapture);

        public static void ValidateAndCapture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Pare o Play Mode antes de validar o cenário.");
            if (SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Abra Chapter1Office antes de validar o cenário.");
            room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName)?.transform;
            if (!room) throw new InvalidOperationException("Raiz do escritório ausente.");
            var errors = new List<string>();
            void Check(bool condition, string message) { if (!condition) errors.Add(message); }
            var desk = Find("Mesa principal — João de Claus");
            Check(desk && Mathf.Abs(Mathf.DeltaAngle(desk.localEulerAngles.y, 180)) < .1f, "Desk does not face João's chair.");
            var computer = Find("Computador e teclado");
            Check(computer && Vector3.Dot(computer.TransformDirection(Vector3.back), Vector3.forward) > .97f, "Monitor faces away from João.");
            var clock = Find("Relógio de parede");
            Check(clock && clock.position.y > 2.4f && Mathf.Abs(clock.position.x) < .3f, "Clock overlaps furniture.");
            var cabinets = room.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Arquivo de metal").ToArray();
            Check(cabinets.Length == 2 && cabinets.All(t => Mathf.Abs(t.localPosition.x + 3.48f) < .02f
                && Mathf.Abs(Mathf.DeltaAngle(t.localEulerAngles.y, -90)) < .1f), "Cabinets are not against the left wall.");
            Check(Find("Trechos da parede direita — abertura real") != null && Find("Escuridão externa") == null, "Window opening is blocked by the old backdrop.");
            var originalWall = room.Cast<Transform>().FirstOrDefault(t => t.name == "Parede direita");
            Check(!originalWall, "Original solid right wall still blocks the window.");
            var windowPane = Find("Colisão do vidro da janela");
            Check(windowPane && windowPane.GetComponent<BoxCollider>() && !windowPane.GetComponent<Renderer>(), "Invisible window collision pane is absent.");
            var windowSpot = Find("Luz externa entrando pelas persianas");
            Check(windowSpot && windowSpot.position.x > 4 && windowSpot.GetComponent<Light>().shadows != LightShadows.None, "External shadowed light is absent.");
            Check(Find("Aro superior aberto") != null && Find("Arame vertical") != null, "Open wire bin is absent.");
            Check(room.GetComponentsInChildren<CollectibleItem>(true).Length == 2, "The original collectible notes changed.");
            Check(room.GetComponentsInChildren<InteractiveDoor>(true).Length == 1, "The original door changed.");
            Check(room.GetComponentsInChildren<InteractiveDrawer>(true).Length == 1, "The original drawer changed.");

            var poses = new[]
            {
                new PoseInfo { name = "Office-refined", position = new Vector3(-1.28f, 1.68f, -2.45f), target = new Vector3(.1f, 1.13f, 2.25f) },
                new PoseInfo { name = "Office-refined-desk", position = new Vector3(-1.56f, 1.53f, 2.16f), target = new Vector3(.13f, 1.06f, 1.11f) },
                new PoseInfo { name = "Office-refined-window", position = new Vector3(1.43f, 1.60f, -.71f), target = new Vector3(3.83f, 1.50f, .91f) },
                new PoseInfo { name = "Office-refined-chair", position = new Vector3(.96f, 1.30f, 2.82f), target = new Vector3(0, .66f, 2.20f) }
            };
            string directory = AssetRoot + "Documentation/"; Directory.CreateDirectory(directory);
            var screenshots = new List<string>();
            foreach (var pose in poses)
            {
                string path = directory + pose.name + ".png";
                try { CaptureCamera(pose.position, pose.target, path); screenshots.Add(path); }
                catch (Exception e) { errors.Add("Preview " + pose.name + ": " + e.Message); Debug.LogException(e); }
            }
            var report = new OfficeReport
            {
                scene = SceneManager.GetActiveScene().path,
                generatedUtc = DateTime.UtcNow.ToString("O"), inspection = Inspect(),
                passed = errors.Count == 0, errors = errors.ToArray(), screenshots = screenshots.ToArray(), cameraPoses = poses
            };
            File.WriteAllText(directory + "Office-refinement-report.json", JsonUtility.ToJson(report, true));
            AssetDatabase.Refresh();
            Debug.Log("Office validation " + (report.passed ? "PASS" : "FAILED") + ": " + screenshots.Count + " previews; " + errors.Count + " errors. Report: " + directory + "Office-refinement-report.json");
        }

        static void CaptureCamera(Vector3 position, Vector3 target, string path)
        {
            var source = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)
                .FirstOrDefault(c => c.CompareTag("MainCamera"));
            var go = EditorUtility.CreateGameObjectWithHideFlags("Office temporary preview camera", HideFlags.HideAndDontSave, typeof(Camera));
            RenderTexture rt = null; Texture2D image = null;
            var previous = RenderTexture.active;
            try
            {
                var camera = go.GetComponent<Camera>();
                if (source) camera.CopyFrom(source);
                camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .03f, .035f); camera.fieldOfView = 67;
                camera.nearClipPlane = .055f; camera.farClipPlane = 35; camera.aspect = 16f / 9;
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true; data.renderShadows = true;
                var originalData = source ? source.GetComponent<UniversalAdditionalCameraData>() : null;
                if (originalData) { data.volumeLayerMask = originalData.volumeLayerMask; data.antialiasing = originalData.antialiasing; }
                else data.volumeLayerMask = ~0;
                rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Office preview" };
                rt.Create(); camera.targetTexture = rt;
                if (GraphicsSettings.currentRenderPipeline)
                {
                    var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                    if (!RenderPipeline.SupportsRenderRequest(camera, request))
                        throw new InvalidOperationException("Current render pipeline does not support the camera preview request.");
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else camera.Render();
                RenderTexture.active = rt;
                image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (image) UnityEngine.Object.DestroyImmediate(image);
                if (rt) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
