// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HorrorGame.Editor
{
    /// <summary>Polishes existing props without recreating the room or adding story events.</summary>
    public static class OfficeDetailPolishPass
    {
        const string ScenePath = "Assets/ChapterOneOffice/Scenes/Chapter1Office.unity";
        const string AssetRoot = "Assets/ChapterOneOffice/";
        const string RootName = "Escritório de João de Claus — Capítulo 1";
        static Transform room, desk;
        static Material walnut, darkWood, metal, brass, bakelite, paper, ceramic;
        static TMP_FontAsset font;
        static int world;

        static void Initialize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Abra Chapter1Office, fora do Play Mode, antes de polir os objetos.");
            room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName)?.transform;
            if (!room) throw new InvalidOperationException("A raiz do escritório não foi encontrada.");
            desk = Require("Mesa principal — João de Claus");
            world = LayerMask.NameToLayer("World"); if (world < 0) world = 0;
            walnut = Material("Walnut grain"); darkWood = Material("Dark walnut");
            metal = Material("Aged gunmetal"); brass = Material("Tarnished brass");
            bakelite = Material("Black bakelite"); paper = Material("Aged paper"); ceramic = Material("Ivory plastic");
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/HorrorMenu/TMP/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (!font) throw new InvalidOperationException("A fonte existente do escritório não foi encontrada.");
        }

        public static void Apply()
        {
            Initialize();
            var noteIds = room.GetComponentsInChildren<CollectibleItem>(true).Select(c => c.persistentId).OrderBy(s => s).ToArray();
            var drawer = room.GetComponentsInChildren<InteractiveDrawer>(true).Single();
            string drawerId = drawer.persistentId;
            string doorId = room.GetComponentsInChildren<InteractiveDoor>(true).Single().persistentId;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Polir telefone e gaveta do escritório");
            Undo.RegisterFullObjectHierarchyUndo(room.gameObject, "Polir telefone e gaveta do escritório");
            RebuildTelephone();
            RebuildDrawers(drawer);
            RebuildMugAndPen();
            if (drawer.persistentId != drawerId || room.GetComponentsInChildren<InteractiveDoor>(true).Single().persistentId != doorId
                || !noteIds.SequenceEqual(room.GetComponentsInChildren<CollectibleItem>(true).Select(c => c.persistentId).OrderBy(s => s)))
                throw new InvalidOperationException("Identificadores persistentes do escritório foram alterados.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(group);
            Debug.Log("Office detail polish complete: rotary phone, hollow drawer cabinets, handles, open mug and pen; original interaction IDs preserved.");
        }

        static Material Material(string name)
        {
            var value = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "Materials/" + name + ".mat");
            return value ? value : throw new InvalidOperationException("Material ausente: " + name);
        }
        static Transform Find(string name) => room.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        static Transform Require(string name) => Find(name) ? Find(name) : throw new InvalidOperationException("Objeto ausente: " + name);
        static void RemoveDirect(Transform parent, params string[] names)
        {
            foreach (var child in parent.Cast<Transform>().Where(t => names.Contains(t.name)).ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        static Transform Group(string name, Transform parent, Vector3 pos = default, Vector3 angles = default)
        {
            var result = new GameObject(name).transform; result.SetParent(parent, false);
            result.localPosition = pos; result.localRotation = Quaternion.Euler(angles); result.gameObject.layer = parent.gameObject.layer;
            return result;
        }
        static GameObject MeshObject(string name, Mesh mesh, Material material, Transform parent, Vector3 pos = default, Vector3 angles = default)
        {
            var go = Group(name, parent, pos, angles).gameObject;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            // Children of the moving drawer must never enter static batching.
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            return go;
        }
        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material material, bool collision = false)
        {
            var go = MeshObject(name, RoundedBox("Box_" + Key(size), size, Mathf.Min(.008f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * .2f)), material, parent, pos);
            if (collision) { var c = go.AddComponent<BoxCollider>(); c.size = size; go.layer = world; }
            return go;
        }
        static string Key(Vector3 v) => string.Join("_", new[] { v.x, v.y, v.z }.Select(f => f.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)));
        static Mesh AssetMesh(string key, Func<Mesh> create)
        {
            string path = AssetRoot + "Models/OfficeDetail_" + key + "_v1.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing) return existing;
            var mesh = create(); mesh.name = "Office detail " + key;
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        sealed class Geometry
        {
            public readonly List<Vector3> points = new List<Vector3>();
            readonly List<int> faces = new List<int>();
            public int Vertex(Vector3 p) { points.Add(p); return points.Count - 1; }
            public void Triangle(int a, int b, int c, Vector3 outward)
            {
                Vector3 n = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
                if (n.sqrMagnitude < 1e-18f) return;
                if (Vector3.Dot(n, outward) < 0) { int swap = b; b = c; c = swap; }
                faces.Add(a); faces.Add(b); faces.Add(c);
            }
            public void Quad(int a, int b, int c, int d, Vector3 outward) { Triangle(a, b, c, outward); Triangle(a, c, d, outward); }
            public Mesh Build()
            {
                var mesh = new Mesh(); if (points.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(points); mesh.SetTriangles(faces, 0);
                mesh.SetUVs(0, points.Select(p => new Vector2(p.x * 4 + p.z * 2, p.y * 4 + p.z * 2)).ToList());
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            }
        }

        static Vector2[] RoundedOutline(float width, float depth, float corner, int arc = 10)
        {
            var result = new List<Vector2>();
            for (int q = 0; q < 4; q++)
            {
                float angle = q * Mathf.PI * .5f;
                Vector2 center = new Vector2(q == 0 || q == 3 ? width * .5f - corner : -width * .5f + corner,
                    q < 2 ? depth * .5f - corner : -depth * .5f + corner);
                for (int j = 0; j <= arc; j++)
                {
                    float a = angle + j * Mathf.PI * .5f / arc;
                    result.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * corner);
                }
            }
            return result.ToArray();
        }

        static Mesh RoundedBox(string name, Vector3 size, float chamfer)
        {
            return AssetMesh(name, () =>
            {
                var g = new Geometry();
                float r = Mathf.Min(chamfer, Mathf.Min(size.x, size.z) * .2f);
                var ring = RoundedOutline(size.x, size.z, r, 5);
                var inset = RoundedOutline(size.x - r, size.z - r, r * .5f, 5);
                var outlines = new[] { inset, ring, ring, inset };
                var heights = new[] { -size.y * .5f, -size.y * .5f + r * .4f, size.y * .5f - r * .4f, size.y * .5f };
                for (int k = 0; k < 4; k++) foreach (var p in outlines[k]) g.Vertex(new Vector3(p.x, heights[k], p.y));
                int n = ring.Length;
                for (int k = 0; k < 3; k++) for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    var p = g.points[k * n + i];
                    g.Quad(k * n + i, k * n + j, (k + 1) * n + j, (k + 1) * n + i, new Vector3(p.x, 0, p.z));
                }
                int bottom = g.Vertex(new Vector3(0, heights[0], 0)), top = g.Vertex(new Vector3(0, heights[3], 0));
                for (int i = 0; i < n; i++) { int j = (i + 1) % n; g.Triangle(bottom, i, j, Vector3.down); g.Triangle(top, n * 3 + i, n * 3 + j, Vector3.up); }
                return g.Build();
            });
        }

        static Mesh Lathe(string name, Vector2[] profile, int sectors = 48)
        {
            return AssetMesh(name, () =>
            {
                var g = new Geometry();
                foreach (var p in profile) for (int i = 0; i < sectors; i++)
                {
                    float a = i * Mathf.PI * 2 / sectors; g.Vertex(new Vector3(Mathf.Cos(a) * p.x, p.y, Mathf.Sin(a) * p.x));
                }
                for (int k = 0; k < profile.Length - 1; k++) for (int i = 0; i < sectors; i++)
                {
                    int j = (i + 1) % sectors; float a = (i + .5f) * Mathf.PI * 2 / sectors;
                    float dy = profile[k + 1].y - profile[k].y, dr = profile[k + 1].x - profile[k].x;
                    g.Quad(k * sectors + i, k * sectors + j, (k + 1) * sectors + j, (k + 1) * sectors + i,
                        new Vector3(Mathf.Cos(a) * dy, -dr, Mathf.Sin(a) * dy));
                }
                return g.Build();
            });
        }

        static Mesh Tube(string name, Vector3[] path, float radius, float flatten = 1, int sides = 10)
        {
            return AssetMesh(name, () =>
            {
                var g = new Geometry();
                for (int k = 0; k < path.Length; k++)
                {
                    Vector3 tangent = (path[Mathf.Min(k + 1, path.Length - 1)] - path[Mathf.Max(k - 1, 0)]).normalized;
                    Vector3 across = Vector3.Cross(tangent, Mathf.Abs(tangent.y) > .94f ? Vector3.forward : Vector3.up).normalized;
                    Vector3 up = Vector3.Cross(across, tangent).normalized;
                    for (int i = 0; i < sides; i++)
                    { float a = i * Mathf.PI * 2 / sides; g.Vertex(path[k] + (across * Mathf.Cos(a) + up * Mathf.Sin(a) * flatten) * radius); }
                }
                for (int k = 0; k < path.Length - 1; k++) for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    Vector3 center = (path[k] + path[k + 1]) * .5f;
                    Vector3 outward = (g.points[k * sides + i] + g.points[k * sides + j] + g.points[(k + 1) * sides + j] + g.points[(k + 1) * sides + i]) * .25f - center;
                    g.Quad(k * sides + i, k * sides + j, (k + 1) * sides + j, (k + 1) * sides + i, outward);
                }
                int start = g.Vertex(path[0]), end = g.Vertex(path[path.Length - 1]);
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides; g.Triangle(start, i, j, path[0] - path[1]);
                    g.Triangle(end, (path.Length - 1) * sides + i, (path.Length - 1) * sides + j, path[path.Length - 1] - path[path.Length - 2]);
                }
                return g.Build();
            });
        }

        static Mesh TelephoneShell()
        {
            return AssetMesh("TelephoneShell", () =>
            {
                var g = new Geometry();
                var widths = new[] { .282f, .312f, .304f, .274f, .253f };
                var depths = new[] { .232f, .264f, .257f, .224f, .203f };
                var heights = new[] { .012f, .018f, .036f, .095f, .102f };
                int count = 0;
                for (int k = 0; k < widths.Length; k++)
                {
                    var outline = RoundedOutline(widths[k], depths[k], k == 4 ? .034f : .042f);
                    count = outline.Length;
                    foreach (var p in outline) g.Vertex(new Vector3(p.x, heights[k] + (k > 2 ? p.y * .22f : 0), p.y));
                }
                for (int k = 0; k < widths.Length - 1; k++) for (int i = 0; i < count; i++)
                { int j = (i + 1) % count; var p = g.points[k * count + i]; g.Quad(k * count + i, k * count + j, (k + 1) * count + j, (k + 1) * count + i, new Vector3(p.x, 0, p.z)); }
                int low = g.Vertex(new Vector3(0, .012f, 0)), high = g.Vertex(new Vector3(0, .102f, 0));
                for (int i = 0; i < count; i++)
                { int j = (i + 1) % count; g.Triangle(low, i, j, Vector3.down); g.Triangle(high, count * 4 + i, count * 4 + j, new Vector3(0, 1, -.22f)); }
                return g.Build();
            });
        }

        static Mesh DialPlate()
        {
            return AssetMesh("RotaryPlateWithRealFingerHoles", () =>
            {
                var g = new Geometry();
                const int segments = 11, sideSamples = 9;
                float half = Mathf.PI / segments;
                for (int s = 0; s < segments; s++)
                {
                    float centerAngle = s * Mathf.PI * 2 / segments + .38f;
                    var boundary = new List<Vector2>();
                    // The sector boundary is convex around its finger hole; its top forms a ring around that hole.
                    for (int i = 0; i < sideSamples; i++) { float a = centerAngle - half + 2 * half * i / sideSamples; boundary.Add(new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * .031f); }
                    for (int i = 0; i < sideSamples; i++) { float a = centerAngle + half; boundary.Add(new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * Mathf.Lerp(.031f, .069f, i / (float)sideSamples)); }
                    for (int i = 0; i < sideSamples; i++) { float a = centerAngle + half - 2 * half * i / sideSamples; boundary.Add(new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * .069f); }
                    for (int i = 0; i < sideSamples; i++) { float a = centerAngle - half; boundary.Add(new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * Mathf.Lerp(.069f, .031f, i / (float)sideSamples)); }
                    Vector2 hole = new Vector2(Mathf.Sin(centerAngle), Mathf.Cos(centerAngle)) * .049f;
                    int n = boundary.Count, start = g.points.Count;
                    foreach (float height in new[] { .003f, .009f })
                    {
                        foreach (var p in boundary) g.Vertex(new Vector3(p.x, height, p.y));
                        foreach (var p in boundary)
                        { var h = hole + (p - hole).normalized * (s == segments - 1 ? 0 : .0083f); g.Vertex(new Vector3(h.x, height, h.y)); }
                    }
                    for (int i = 0; i < n; i++)
                    {
                        int j = (i + 1) % n;
                        g.Quad(start + n * 2 + i, start + n * 2 + j, start + n * 3 + j, start + n * 3 + i, Vector3.up);
                        g.Quad(start + i, start + j, start + n + j, start + n + i, Vector3.down);
                        var p = (boundary[i] + boundary[j]) * .5f - hole;
                        g.Quad(start + i, start + j, start + n * 2 + j, start + n * 2 + i, new Vector3(p.x, 0, p.y));
                        if (s < segments - 1)
                        { Vector2 inward = hole - new Vector2(g.points[start + n + i].x, g.points[start + n + i].z); g.Quad(start + n + i, start + n + j, start + n * 3 + j, start + n * 3 + i, new Vector3(inward.x, 0, inward.y)); }
                    }
                }
                return g.Build();
            });
        }

        static void RebuildTelephone()
        {
            var phone = Require("Telefone — peça central do capítulo");
            RemoveDirect(phone, "Built-in telephone visual", "Telefone clássico — modelo refinado");
            phone.localPosition = new Vector3(phone.localPosition.x, .8285f, phone.localPosition.z);
            var visual = Group("Telefone clássico — modelo refinado", phone);
            MeshObject("Carcaça curva de baquelite", TelephoneShell(), bakelite, visual);
            MeshObject("Emenda da base", RoundedBox("TelephoneBottomSeam", new Vector3(.292f, .012f, .244f), .013f), bakelite, visual, new Vector3(0, .012f, 0));
            foreach (float x in new[] { -.095f, .095f }) foreach (float z in new[] { -.075f, .076f })
                MeshObject("Pé de borracha", Lathe("TelephoneRubberFoot", new[] { new Vector2(0, 0), new Vector2(.017f, 0), new Vector2(.017f, .007f), new Vector2(0, .007f) }, 20), bakelite, visual, new Vector3(x, .002f, z));

            var dial = Group("Disco inclinado com dez perfurações", visual, new Vector3(0, .095f, -.038f), new Vector3(-12.4f, 0, 0));
            MeshObject("Anel de números em marfim", Lathe("DialNumberRing", new[] { new Vector2(.029f, 0), new Vector2(.083f, 0), new Vector2(.083f, .003f), new Vector2(.029f, .003f), new Vector2(.029f, 0) }), paper, dial);
            MeshObject("Fundo negro dos furos", Lathe("DialDarkBacking", new[] { new Vector2(0, -.001f), new Vector2(.070f, -.001f), new Vector2(.070f, .001f), new Vector2(0, .001f) }), bakelite, dial);
            MeshObject("Placa rotativa perfurada", DialPlate(), brass, dial);
            MeshObject("Aro central cromado", Lathe("DialCenterBezel", new[] { new Vector2(.022f, .008f), new Vector2(.030f, .008f), new Vector2(.030f, .012f), new Vector2(.022f, .012f), new Vector2(.022f, .008f) }), metal, dial);
            MeshObject("Etiqueta central", Lathe("DialCenterCard", new[] { new Vector2(0, .008f), new Vector2(.022f, .008f), new Vector2(.022f, .010f), new Vector2(0, .010f) }), paper, dial);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2 / 11 + .38f;
                AddText("Número do disco " + i, i == 9 ? "0" : (i + 1).ToString(), dial,
                    new Vector3(Mathf.Sin(a) * .0768f, .0041f, Mathf.Cos(a) * .0768f), new Vector2(.015f, .019f), .13f);
            }
            AddText("Etiqueta da linha", "LINHA", dial, new Vector3(0, .011f, 0), new Vector2(.037f, .014f), .066f);
            MeshObject("Batente metálico do disco", Tube("RotaryFingerStop", new[] { new Vector3(-.073f, .008f, -.020f), new Vector3(-.072f, .019f, -.019f), new Vector3(-.058f, .021f, -.013f) }, .0031f, 1, 8), metal, dial);

            foreach (float x in new[] { -.098f, .098f })
            {
                Box("Berço do receptor", visual, new Vector3(x, .132f, .080f), new Vector3(.037f, .042f, .048f), bakelite);
                Box("Gancho do receptor", visual, new Vector3(x, .157f, .061f), new Vector3(.019f, .009f, .037f), metal);
            }
            var path = new List<Vector3>();
            for (int i = 0; i <= 44; i++)
            {
                float x = Mathf.Lerp(-.132f, .132f, i / 44f), ratio = x / .132f;
                path.Add(new Vector3(x, .174f + .035f * (1 - ratio * ratio), .083f + .003f * (1 - ratio * ratio)));
            }
            MeshObject("Receptor curvo", Tube("CurvedHandset", path.ToArray(), .021f, .85f, 18), bakelite, visual);
            foreach (float x in new[] { -.136f, .136f })
            {
                var end = Group(x < 0 ? "Auricular" : "Microfone", visual, new Vector3(x, .128f, .083f));
                MeshObject("Cápsula arredondada do receptor", Lathe("HandsetCup", new[] { new Vector2(0, 0), new Vector2(.035f, 0), new Vector2(.043f, .010f), new Vector2(.039f, .027f), new Vector2(.022f, .047f), new Vector2(0, .051f) }), bakelite, end);
                MeshObject("Aro da cápsula", Lathe("HandsetCupSeam", new[] { new Vector2(.039f, .007f), new Vector2(.043f, .007f), new Vector2(.043f, .010f), new Vector2(.039f, .010f), new Vector2(.039f, .007f) }), metal, end);
                // Perforations on the underside are visible if the future receiver pickup is implemented.
                var grille = Group("Grelha do receptor", end, new Vector3(0, -.001f, 0), new Vector3(180, 0, 0));
                for (int i = 0; i < 8; i++)
                { float a = i * Mathf.PI / 4; MeshObject("Perfuração da grelha", Lathe("ReceiverGrilleHole", new[] { new Vector2(0, 0), new Vector2(.0016f, 0), new Vector2(.0016f, .001f), new Vector2(0, .001f) }, 10), metal, grille, new Vector3(Mathf.Sin(a) * .015f, 0, Mathf.Cos(a) * .015f)); }
            }

            var cord = new List<Vector3>();
            // The handset lead descends into a relaxed coil that rests on the tabletop and returns to the housing.
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f; cord.Add(new Vector3(Mathf.Lerp(.177f, .196f, t), Mathf.Lerp(.155f, .018f, t), Mathf.Lerp(.084f, .041f, t)));
            }
            for (int i = 1; i <= 240; i++)
            {
                float t = i / 240f, a = t * 24 * Mathf.PI;
                Vector3 axis = new Vector3(.196f + Mathf.Sin(t * Mathf.PI) * .038f, .010f, .041f - t * .168f);
                cord.Add(axis + new Vector3(Mathf.Sin(a) * .009f, Mathf.Cos(a) * .007f + .007f, 0));
            }
            Vector3 last = cord[cord.Count - 1];
            for (int i = 1; i <= 18; i++)
            { float t = i / 18f; cord.Add(Vector3.Lerp(last, new Vector3(.14f, .035f, -.064f), t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * .006f); }
            MeshObject("Fio espiral contínuo", Tube("HandsetRelaxedCoiledCord", cord.ToArray(), .0018f, 1, 8), bakelite, visual);
            var collider = phone.GetComponent<BoxCollider>() ? phone.GetComponent<BoxCollider>() : phone.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .113f, 0); collider.size = new Vector3(.36f, .23f, .273f);
            // The phone remains a visual story prop; this pass does not add an unapproved call interaction.
        }

        static void AddText(string name, string value, Transform parent, Vector3 pos, Vector2 size, float points)
        {
            var t = Group(name, parent, pos, new Vector3(90, 0, 0)).gameObject.AddComponent<TextMeshPro>();
            t.font = font; t.text = value; t.fontSize = points; t.rectTransform.sizeDelta = size;
            t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = new Color(.11f, .105f, .088f); t.enableAutoSizing = false;
        }

        static void RebuildDrawers(InteractiveDrawer interaction)
        {
            RemoveDirect(desk, "Carcaças ocas dos gaveteiros");
            foreach (var solid in desk.Cast<Transform>().Where(t => t.name == "Gaveteiro").ToArray()) UnityEngine.Object.DestroyImmediate(solid.gameObject);
            // Replace the previous invisible solid cabinet volumes with walls matching the hollow carcasses.
            foreach (var collider in desk.GetComponents<BoxCollider>().Where(c => c.size.y > .65f && c.size.y < .79f && c.size.x < .60f).ToArray())
                UnityEngine.Object.DestroyImmediate(collider);
            var cabinets = Group("Carcaças ocas dos gaveteiros", desk);
            foreach (float x in new[] { -.8f, .8f })
            {
                var cabinet = Group("Gaveteiro oco", cabinets, new Vector3(x, 0, 0));
                foreach (float side in new[] { -.235f, .235f }) Box("Lateral fixa de madeira", cabinet, new Vector3(side, .379f, .075f), new Vector3(.020f, .716f, .79f), darkWood, true);
                Box("Fundo fixo", cabinet, new Vector3(0, .379f, .46f), new Vector3(.47f, .716f, .020f), darkWood, true);
                Box("Base fixa", cabinet, new Vector3(0, .072f, .075f), new Vector3(.47f, .040f, .77f), darkWood, true);
                Box("Travessa sob o tampo", cabinet, new Vector3(0, .722f, .075f), new Vector3(.47f, .028f, .77f), darkWood, true);
                foreach (float height in new[] { .300f, .505f })
                    Box("Divisória entre gavetas", cabinet, new Vector3(0, height, .075f), new Vector3(.47f, .016f, .77f), darkWood);
                // The outer rail halves stay fixed. The inner rail halves are children of the moving drawer.
                if (x > 0) foreach (float side in new[] { -.219f, .219f })
                    Box("Trilho fixo da gaveta", cabinet, new Vector3(side, .574f, .0f), new Vector3(.008f, .012f, .615f), metal);
            }

            var allDrawers = desk.Cast<Transform>().Where(t => t.name.StartsWith("Gaveta ", StringComparison.Ordinal)).ToArray();
            foreach (var drawer in allDrawers)
            {
                RemoveDirect(drawer, "Frente entalhada", "Painel rebaixado", "Puxador latão", "Fundo da gaveta", "Gaveta — acabamento refinado");
                var detail = Group("Gaveta — acabamento refinado", drawer);
                Box("Frente de madeira chanfrada", detail, Vector3.zero, new Vector3(.465f, .184f, .038f), walnut);
                Box("Painel interno da frente", detail, new Vector3(0, 0, -.0205f), new Vector3(.370f, .112f, .008f), darkWood);
                foreach (float side in new[] { -.071f, .071f })
                    Box("Fixação do puxador", detail, new Vector3(side, 0, -.033f), new Vector3(.018f, .023f, .014f), brass);
                MeshObject("Puxador curvo de latão", Tube("DrawerBailHandle", new[] {
                    new Vector3(-.071f, 0, -.039f), new Vector3(-.071f, -.004f, -.053f), new Vector3(-.059f, -.010f, -.061f),
                    new Vector3(.059f, -.010f, -.061f), new Vector3(.071f, -.004f, -.053f), new Vector3(.071f, 0, -.039f) }, .005f, 1, 10), brass, detail);
                if (drawer != interaction.drawer) continue;
                Box("Fundo da caixa móvel", detail, new Vector3(0, -.075f, .31f), new Vector3(.419f, .014f, .62f), darkWood);
                foreach (float side in new[] { -.203f, .203f })
                {
                    Box("Lateral da caixa móvel", detail, new Vector3(side, -.010f, .315f), new Vector3(.013f, .143f, .62f), walnut);
                    Box("Trilho móvel da gaveta", detail, new Vector3(side * 1.055f, -.036f, .310f), new Vector3(.006f, .010f, .61f), metal);
                }
                Box("Traseira da caixa móvel", detail, new Vector3(0, -.010f, .620f), new Vector3(.415f, .143f, .015f), walnut);
                var content = Group("Documentos decorativos dentro da gaveta", detail);
                for (int i = 0; i < 5; i++)
                {
                    var page = Box("Papel dentro da gaveta", content, new Vector3(-.012f + i * .0015f, -.064f + i * .0011f, .173f), new Vector3(.16f, .0011f, .22f), paper);
                    page.transform.localRotation = Quaternion.Euler(0, i * 2 - 3, 0);
                }
                // This is a moving solid front only. No new collectible or persistent ID is created.
                var collider = drawer.GetComponent<BoxCollider>();
                if (collider) { collider.center = new Vector3(0, 0, -.016f); collider.size = new Vector3(.465f, .184f, .073f); }
                interaction.localTravel = new Vector3(0, 0, -.34f);
                var serialized = new SerializedObject(interaction); serialized.FindProperty("travelSpeed").floatValue = .48f; serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(interaction);
            }
        }

        static void RebuildMugAndPen()
        {
            RemoveDirect(desk, "Caneca de café", "Café frio", "Alça da caneca", "Caneta", "Caneca de café — cerâmica oca", "Caneta de investigação");
            var mug = Group("Caneca de café — cerâmica oca", desk, new Vector3(.65f, .829f, .02f));
            MeshObject("Parede e borda aberta da caneca", Lathe("OpenCoffeeMug", new[] {
                new Vector2(0, .004f), new Vector2(.034f, .004f), new Vector2(.039f, .010f), new Vector2(.044f, .093f),
                new Vector2(.045f, .103f), new Vector2(.043f, .110f), new Vector2(.040f, .110f), new Vector2(.038f, .101f),
                new Vector2(.034f, .018f), new Vector2(0, .018f) }), ceramic, mug);
            MeshObject("Superfície de café abaixo da borda", Lathe("CoffeeSurface", new[] { new Vector2(0, .087f), new Vector2(.037f, .087f), new Vector2(.037f, .088f), new Vector2(0, .088f) }), bakelite, mug);
            var handle = new List<Vector3>();
            for (int i = 0; i <= 28; i++) { float a = Mathf.Lerp(-Mathf.PI * .5f, Mathf.PI * .5f, i / 28f); handle.Add(new Vector3(.039f + Mathf.Cos(a) * .034f, .062f + Mathf.Sin(a) * .033f, 0)); }
            MeshObject("Alça curva da caneca", Tube("CoffeeMugHandle", handle.ToArray(), .007f, 1, 12), ceramic, mug);
            var pen = Group("Caneta de investigação", desk, new Vector3(.52f, .837f, -.18f), new Vector3(0, 38, 90));
            MeshObject("Corpo da caneta", Lathe("PenBody", new[] { new Vector2(0, -.072f), new Vector2(.004f, -.057f), new Vector2(.0045f, .067f), new Vector2(.003f, .071f), new Vector2(0, .071f) }, 20), bakelite, pen);
            MeshObject("Ponta metálica", Lathe("PenNib", new[] { new Vector2(0, -.075f), new Vector2(.0041f, -.057f), new Vector2(0, -.057f) }, 20), metal, pen);
            MeshObject("Presilha metálica", Tube("PenClip", new[] { new Vector3(.004f, .065f, 0), new Vector3(.006f, .063f, 0), new Vector3(.006f, .029f, 0) }, .0011f, 1, 6), brass, pen);
        }

        [Serializable] sealed class DetailReport
        {
            public bool passed;
            public string generatedUtc;
            public string[] errors, screenshots;
            public string[] preservedNoteIds;
            public string preservedDrawerId;
            public Vector3 worldDrawerTravel;
            public int meshTriangles;
        }

        public static void ValidateAndCapture()
        {
            Initialize(); var errors = new List<string>();
            void Check(bool condition, string message) { if (!condition) errors.Add(message); }
            var phone = Find("Telefone — peça central do capítulo");
            var drawer = room.GetComponentsInChildren<InteractiveDrawer>(true).SingleOrDefault();
            Check(phone && phone.Find("Telefone clássico — modelo refinado/Carcaça curva de baquelite"), "Telefone refinado ausente.");
            Check(Find("Receptor curvo") && Find("Placa rotativa perfurada") && Find("Fio espiral contínuo"), "Receptor, disco perfurado ou fio ausente.");
            Check(Find("Disco inclinado com dez perfurações")?.GetComponentsInChildren<TextMeshPro>().Count(t => t.name.StartsWith("Número do disco ", StringComparison.Ordinal)) == 10, "Números do disco incompletos.");
            Check(room.GetComponentsInChildren<InteractiveDoor>(true).Length == 1 && room.GetComponentsInChildren<CollectibleItem>(true).Length == 2, "Porta ou documentos originais alterados.");
            Check(drawer && drawer.drawer == drawer.transform && drawer.persistentId.Length > 0, "Referência/ID da gaveta inválidos.");
            Check(drawer && drawer.drawer.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Lateral da caixa móvel") == 2 && Find("Fundo da caixa móvel"), "Caixa móvel da gaveta incompleta.");
            Check(Find("Gaveteiro") == null && desk.GetComponents<BoxCollider>().All(c => c.size.y < .4f), "Carcaça sólida antiga ainda bloqueia a abertura visual da gaveta.");
            Check(drawer && Vector3.Dot(drawer.drawer.parent.TransformVector(drawer.localTravel).normalized, Vector3.forward) > .99f, "Gaveta abre na direção errada depois de virar a mesa.");
            Check(Mathf.Abs(Mathf.DeltaAngle(desk.localEulerAngles.y, 180)) < .1f, "Orientação da mesa alterada.");
            Check(Find("Aro superior aberto") && Find("Luz externa entrando pelas persianas") && Find("Colisão do vidro da janela"), "Revisão anterior da lixeira/janela alterada.");
            Check(Find("Parede e borda aberta da caneca") != null, "Caneca oca ausente.");
            int triangles = 0;
            foreach (var filter in room.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh && f.sharedMesh.name.StartsWith("Office detail ", StringComparison.Ordinal)))
            {
                var mesh = filter.sharedMesh; var v = mesh.vertices; var t = mesh.triangles; triangles += t.Length / 3;
                Check(v.All(p => !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z)), "Vertices inválidos: " + mesh.name);
                for (int i = 0; i < t.Length; i += 3) if (Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).sqrMagnitude < 1e-18f)
                { errors.Add("Triângulo degenerado: " + mesh.name); break; }
            }
            Directory.CreateDirectory(AssetRoot + "Documentation/");
            var images = new List<string>();
            if (phone)
            {
                string path = AssetRoot + "Documentation/Office-polished-phone.png";
                Capture(phone.TransformPoint(new Vector3(-.31f, .38f, -.54f)), phone.TransformPoint(new Vector3(.04f, .12f, 0)), path, 46);
                images.Add(path);
            }
            if (drawer)
            {
                Vector3 original = drawer.drawer.localPosition;
                try
                {
                    drawer.drawer.localPosition = original + drawer.localTravel;
                    Physics.SyncTransforms();
                    string path = AssetRoot + "Documentation/Office-polished-drawer-open.png";
                    Capture(desk.TransformPoint(new Vector3(1.29f, 1.16f, -1.10f)), desk.TransformPoint(new Vector3(.78f, .57f, -.45f)), path, 53);
                    images.Add(path);
                }
                finally { drawer.drawer.localPosition = original; Physics.SyncTransforms(); }
            }
            string deskPath = AssetRoot + "Documentation/Office-polished-desk.png";
            Capture(desk.TransformPoint(new Vector3(-1.3f, 1.7f, -1.48f)), desk.TransformPoint(new Vector3(0, 1.02f, .1f)), deskPath, 57); images.Add(deskPath);
            var report = new DetailReport {
                passed = errors.Count == 0, generatedUtc = DateTime.UtcNow.ToString("O"), errors = errors.ToArray(), screenshots = images.ToArray(),
                preservedNoteIds = room.GetComponentsInChildren<CollectibleItem>(true).Select(n => n.persistentId).ToArray(),
                preservedDrawerId = drawer ? drawer.persistentId : "", worldDrawerTravel = drawer ? drawer.drawer.parent.TransformVector(drawer.localTravel) : Vector3.zero,
                meshTriangles = triangles };
            File.WriteAllText(AssetRoot + "Documentation/Office-detail-polish-report.json", JsonUtility.ToJson(report, true));
            AssetDatabase.Refresh();
            Debug.Log("Office detail polish validation " + (report.passed ? "PASS" : "FAILED") + "; " + errors.Count + " errors; " + images.Count + " previews; " + triangles + " mesh triangles.");
        }

        static void Capture(Vector3 position, Vector3 target, string path, float fov)
        {
            var source = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include).FirstOrDefault(c => c.CompareTag("MainCamera"));
            var go = EditorUtility.CreateGameObjectWithHideFlags("Office detail temporary preview", HideFlags.HideAndDontSave, typeof(Camera));
            RenderTexture rt = null; Texture2D image = null; var previous = RenderTexture.active;
            try
            {
                var camera = go.GetComponent<Camera>(); if (source) camera.CopyFrom(source);
                camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .03f, .035f);
                camera.fieldOfView = fov; camera.nearClipPlane = .018f; camera.farClipPlane = 35; camera.aspect = 16f / 9;
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
                var data = go.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true; data.renderShadows = true;
                var original = source ? source.GetComponent<UniversalAdditionalCameraData>() : null;
                data.volumeLayerMask = original ? original.volumeLayerMask : ~0;
                if (original) data.antialiasing = original.antialiasing;
                rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); rt.Create(); camera.targetTexture = rt;
                if (GraphicsSettings.currentRenderPipeline)
                {
                    var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                    if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("A URP atual não oferece captura de câmera.");
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else camera.Render();
                RenderTexture.active = rt; image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; if (image) UnityEngine.Object.DestroyImmediate(image);
                if (rt) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); } UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
