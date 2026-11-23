// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HorrorGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SchoolCEEP.Editor
{
    /// <summary>Dress the saved school without rebuilding its map, moving a clue or changing a native collider.</summary>
    public static class SchoolAbandonedSurfacePass
    {
        public const string GroupName = "Escola — desgaste e caixas nas circulações";
        public const string MeshFolder = "Assets/SchoolCEEP/Meshes/AbandonmentPolish";
        public const string MaterialFolder = "Assets/SchoolCEEP/Materials/AbandonmentPolish";
        const string BoxPath = "Assets/models/decoracao/cardboard_box_01/cardboard_box_01.prefab";
        const float TextureMeters = 1.8f;
        const float WallTextureScale = .65f;
        static readonly HashSet<string> WallMaterials = new HashSet<string>(StringComparer.Ordinal) {
            "Reboco envelhecido", "Pintura verde antiga", "PhotoWestWhiteWall", "PhotoPlasterWhite",
            "PhotoWestCreamWall", "PhotoClassroomWhite", "PhotoClassroomLowerGrey", "PhotoCentralWhite",
            "PhotoWallOchre", "PhotoWallTerracotta", "FrontPhotoOffWhite"
        };

        [Serializable] public sealed class Report
        {
            public int revision = 3, changedRenderers, texturedWallTriangles, retainedTriangles, boxes;
            public int rejectedBoxCandidates, routeReservations, routeSamples;
            public int supplementalRoutes, removedObstructingBoxes;
            public float sourceUVRepeatMeters = TextureMeters, wallTextureScale = WallTextureScale;
            public float textureRepeatMeters = TextureMeters / WallTextureScale;
            public float maximumColliderBoundsDelta;
            public bool protectedArchitectureAndStoryPreserved, allBoxesSupported, reservedRoutesClear;
            public string protectedBefore, protectedAfter;
            public Vector3[] boxPositions;
            public string[] notes;
        }
        sealed class Change { public MeshFilter filter; public Mesh mesh; public MeshRenderer renderer; public Material[] materials; }
        public sealed class BlueWallChange
        {
            public MeshFilter filter;
            public MeshRenderer renderer;
            public Mesh beforeMesh, afterMesh;
            public Material[] beforeMaterials, afterMaterials;
            public int selectedTriangles;
        }
        sealed class Route { public Vector3 a, b; }
        sealed class DoorReservation { public Bounds swing; public Vector3 center, normal; }
        sealed class Context
        {
            public Transform root, group;
            public Collider[] native;
            public BoxCollider[] walls;
            public readonly List<Route> routes = new List<Route>();
            public readonly List<DoorReservation> doors = new List<DoorReservation>();
            public Vector3[] clues;
            public GameObject prefab;
            public Material cracked, moss;
            public readonly List<Change> changes = new List<Change>();
            public readonly Dictionary<string, Material> variants = new Dictionary<string, Material>();
            public Report report = new Report();
        }

        public static Report Apply(Transform root)
        {
            if (!root || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply the abandonment pass to the saved school outside Play mode.");
            if (root.Find(GroupName)) return Audit(root);
            var c = CreateContext(root, null);
            c.cracked = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/WornCrackedPlaster.mat");
            c.moss = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/WornMossyPlasterwall.mat");
            c.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoxPath);
            if (!c.cracked || !c.moss || !c.prefab)
                throw new InvalidDataException("Prepare both approved plaster materials and the existing cardboard box prefab first.");
            EnsureFolder(MeshFolder);
            c.report.protectedBefore = ProtectedSignature(root, null);
            c.group = new GameObject(GroupName).transform;
            c.group.SetParent(root, false);
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true).ToArray())
                    if (renderer.gameObject.activeInHierarchy && !renderer.transform.IsChildOf(c.group)) TextureWalls(c, renderer);
                if (c.report.texturedWallTriangles == 0)
                    throw new InvalidOperationException("No native-backed wall faces were selected; do not report a surface upgrade.");
                PlaceCorridorBoxes(c);
                Physics.SyncTransforms();
                c.report = Finish(c);
                if (!c.report.protectedArchitectureAndStoryPreserved)
                    throw new InvalidOperationException("A protected native collider, light or story component changed.");
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                return c.report;
            }
            catch
            {
                foreach (var change in c.changes) if (change.filter && change.renderer)
                { change.filter.sharedMesh = change.mesh; change.renderer.sharedMaterials = change.materials; }
                if (c.group) UnityEngine.Object.DestroyImmediate(c.group.gameObject);
                Physics.SyncTransforms();
                throw;
            }
        }

        static Context CreateContext(Transform root, Transform group)
        {
            Physics.SyncTransforms();
            var c = new Context { root = root, group = group };
            c.native = root.GetComponentsInChildren<Collider>(true)
                .Where(v => v.enabled && !v.isTrigger && v.gameObject.activeInHierarchy && (!group || !v.transform.IsChildOf(group))).ToArray();
            c.walls = c.native.OfType<BoxCollider>().Where(v => IsMasonry(v.name)
                && v.bounds.size.y > .15f && Mathf.Min(v.bounds.size.x, v.bounds.size.z) < .65f).ToArray();
            c.clues = root.GetComponentsInChildren<MonoBehaviour>(true).Where(v => v &&
                (v.GetType().Name.Contains("Clue") || v.GetType().Name.Contains("Anchor") || v.GetType().Name.Contains("Pickup")))
                .Select(v => v.transform.position).ToArray();
            ReadReservations(c);
            return c;
        }
        static bool IsMasonry(string name)
        {
            var n = name.ToLowerInvariant();
            return n.StartsWith("parede") || n == "peitoril" || n.StartsWith("verga") || n.StartsWith("mureta") || n.StartsWith("muro");
        }

        // Classroom white batches also contain ceiling panels and beams. Only vertical
        // faces backed by an existing masonry volume qualify. No overlaid coplanar mesh is added.
        static bool BackedWall(Context c, Vector3 a, Vector3 b, Vector3 d, Vector3 normal, out BoxCollider backing)
        {
            backing = null;
            if (Mathf.Abs(normal.y) > .035f || Mathf.Max(Mathf.Abs(normal.x), Mathf.Abs(normal.z)) < .98f) return false;
            float height = Mathf.Max(a.y, b.y, d.y) - Mathf.Min(a.y, b.y, d.y);
            if (height < .16f) return false;
            var center = (a + b + d) / 3;
            foreach (var wall in c.walls)
            {
                var bounds = wall.bounds;
                bool thinX = bounds.size.x < bounds.size.z;
                if ((thinX ? Mathf.Abs(normal.x) : Mathf.Abs(normal.z)) < .98f) continue;
                float plane = thinX ? center.x : center.z;
                float low = thinX ? bounds.min.x : bounds.min.z, high = thinX ? bounds.max.x : bounds.max.z;
                if (Mathf.Min(Mathf.Abs(plane - low), Mathf.Abs(plane - high)) > .085f) continue;
                bounds.Expand(new Vector3(.16f, .025f, .16f));
                if (bounds.Contains(a) && bounds.Contains(b) && bounds.Contains(d)) { backing = wall; return true; }
            }
            return false;
        }

        static void TextureWalls(Context c, MeshRenderer renderer)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            var source = filter ? filter.sharedMesh : null;
            var materials = renderer.sharedMaterials;
            if (!source || !source.isReadable || !materials.Any(v => v && WallMaterials.Contains(v.name))) return;
            var positions = source.vertices; var normals = source.normals; var uv = source.uv;
            if (normals.Length != positions.Length || uv.Length != positions.Length)
                throw new InvalidDataException("Expected native wall mesh normals/UVs: " + renderer.name);
            var vertices = positions.ToList(); var newNormals = normals.ToList(); var newUV = uv.ToList();
            var sourceUV2 = source.uv2; var sourceColors = source.colors;
            var uv2 = sourceUV2.ToList(); var colors = sourceColors.ToList();
            var slots = materials.ToList(); var triangles = Enumerable.Range(0, source.subMeshCount).Select(_ => new List<int>()).ToList();
            int selected = 0, retained = 0;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                var indices = source.GetTriangles(sub);
                bool eligible = sub < materials.Length && materials[sub] && WallMaterials.Contains(materials[sub].name);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                    Vector3 a = renderer.transform.TransformPoint(positions[ia]), b = renderer.transform.TransformPoint(positions[ib]), d = renderer.transform.TransformPoint(positions[ic]);
                    var normal = Vector3.Cross(b - a, d - a).normalized;
                    BoxCollider backing = null;
                    if (!eligible || !BackedWall(c, a, b, d, normal, out backing))
                    { triangles[sub].AddRange(new[] { ia, ib, ic }); retained++; continue; }
                    var center = backing.bounds.center;
                    // The complete masonry span uses one finish; adjacent triangles
                    // cannot form a diagonal cracked/mossy texture seam.
                    bool damp = StableSeed(Mathf.FloorToInt(center.x / 7) + ":" + Mathf.FloorToInt(center.z / 7)) % 5
                        <= (center.y > 3.5f ? 1 : 0);
                    var material = WallVariant(c, materials[sub], damp);
                    int slot = slots.IndexOf(material);
                    if (slot < 0) { slot = slots.Count; slots.Add(material); triangles.Add(new List<int>()); }
                    foreach (int index in new[] { ia, ib, ic })
                    {
                        var world = renderer.transform.TransformPoint(positions[index]);
                        triangles[slot].Add(vertices.Count);
                        vertices.Add(positions[index]); newNormals.Add(normals[index]);
                        newUV.Add(new Vector2((Mathf.Abs(normal.x) > .9f ? world.z : world.x) / TextureMeters, world.y / TextureMeters));
                        if (sourceUV2.Length > 0) uv2.Add(sourceUV2[index]);
                        if (sourceColors.Length > 0) colors.Add(sourceColors[index]);
                    }
                    selected++;
                }
            }
            if (selected == 0) return;
            var mesh = new Mesh { name = "Abandoned wall surfaces — " + source.name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetNormals(newNormals); mesh.SetUVs(0, newUV);
            if (uv2.Count > 0) mesh.SetUVs(1, uv2);
            if (colors.Count > 0) mesh.SetColors(colors);
            mesh.subMeshCount = triangles.Count;
            for (int sub = 0; sub < triangles.Count; sub++) mesh.SetTriangles(triangles[sub], sub, false);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            string path = AssetDatabase.GenerateUniqueAssetPath(MeshFolder + "/Wall-" + c.report.changedRenderers.ToString("D3") + ".asset");
            AssetDatabase.CreateAsset(mesh, path);
            c.changes.Add(new Change { filter = filter, mesh = source, renderer = renderer, materials = materials });
            filter.sharedMesh = mesh; renderer.sharedMaterials = slots.ToArray();
            c.report.changedRenderers++; c.report.texturedWallTriangles += selected; c.report.retainedTriangles += retained;
        }

        static Material WallVariant(Context c, Material original, bool damp)
        {
            string key = original.name + (damp ? "-Moss" : "-Cracked");
            if (c.variants.TryGetValue(key, out var existing)) return existing;
            string path = MaterialFolder + "/Abandoned-" + key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                // The cracked publisher albedo is an alligator pattern suitable for
                // small damaged areas, but reads like stone when repeated over a room.
                // Broad painted walls use the matching mossy plaster PBR set; the
                // existing localized damaged-plaster decals retain the stronger wear.
                material = new Material(c.moss) { name = "Abandoned-" + key };
                Color color = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : Color.white;
                if (color.maxColorComponent > .68f && Mathf.Abs(color.r - color.b) < .12f) color = Color.white;
                else { color.r = Mathf.Clamp01(color.r * 1.32f); color.g = Mathf.Clamp01(color.g * 1.32f); color.b = Mathf.Clamp01(color.b * 1.32f); color.a = 1; }
                material.SetColor("_BaseColor", color);
                material.SetTextureScale("_BaseMap", Vector2.one);
                material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.black);
                AssetDatabase.CreateAsset(material, path);
            }
            NaturalPlaster(material, c.moss, damp);
            c.variants.Add(key, material); return material;
        }

        /// <summary>Material-only correction for an already dressed scene. No mesh,
        /// transform, collider, clue, box or original downloaded image is changed.</summary>
        public static int RetuneWallMaterials(Transform root)
        {
            if (!root || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Retune wall materials outside Play mode.");
            var source = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/WornMossyPlasterwall.mat");
            if (!source) throw new InvalidDataException("The approved mossy plaster PBR material is required.");
            var materials = root.GetComponentsInChildren<MeshRenderer>(true).SelectMany(v => v.sharedMaterials)
                .Where(v => v && AssetDatabase.GetAssetPath(v).StartsWith(MaterialFolder + "/Abandoned-", StringComparison.Ordinal))
                .Distinct().ToArray();
            foreach (var material in materials) NaturalPlaster(material, source, material.name.EndsWith("-Moss", StringComparison.Ordinal));
            return materials.Length;
        }

        static void NaturalPlaster(Material material, Material source, bool damp)
        {
            // Preserve the material's old white/gray/blue/ochre color band. All four
            // texture channels stay spatially matched to the same source surface.
            foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
            {
                material.SetTexture(property, source.GetTexture(property));
                material.SetTextureScale(property, Vector2.one * WallTextureScale);
                material.SetTextureOffset(property, Vector2.zero);
            }
            material.SetFloat("_BumpScale", damp ? .24f : .18f);
            material.SetFloat("_OcclusionStrength", damp ? .28f : .22f);
            material.SetFloat("_Smoothness", .12f);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_EnvironmentReflections", 0); material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.SetFloat("_SpecularHighlights", 0); material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_OCCLUSIONMAP");
            material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(material);
        }

        static void ReadReservations(Context c)
        {
            string path = SchoolMapBuilder.Output + "/Refinement/map-audit-after.json";
            if (!File.Exists(path)) path = SchoolMapBuilder.Output + "/School-whole-map-audit.json";
            if (!File.Exists(path)) throw new InvalidDataException("The existing whole-map route report is required before adding path clutter.");
            var report = JsonUtility.FromJson<SchoolWholeMapAudit.Report>(File.ReadAllText(path));
            if (report.targets == null || report.routesWithNativePath != report.plannedRoutes || report.plannedRoutes < 400)
                throw new InvalidDataException("Expected the currently validated complete school route catalogue.");
            foreach (var target in report.targets.Where(v => v.passed && v.points != null))
                for (int i = 1; i < target.points.Length; i++) c.routes.Add(new Route { a = target.points[i - 1], b = target.points[i] });
            c.report.routeReservations = report.plannedRoutes;
            // The native path-search targets omit authored stair/grade paths.
            // Reserve the same explicit paths used by the full physical audit,
            // including the low cantina connector, before placing any clutter.
            foreach (string file in new[] { "School-circulation-report.json", "School-hall-grade-report.json",
                "School-canteen-connector-report.json", "School-west-block-access-report.json",
                "School-campus-connections-report.json", "School-canteen-vestibule-report.json",
                "School-near-canteen-grade-report.json" })
            {
                string extraPath = SchoolMapBuilder.Output + "/" + file;
                if (!File.Exists(extraPath)) continue;
                var extra = JsonUtility.FromJson<SchoolCirculation.BuildReport>(File.ReadAllText(extraPath));
                foreach (var route in extra?.routes ?? Array.Empty<SchoolCirculation.Route>())
                {
                    if (route.feet == null || route.feet.Length < 2) continue;
                    for (int i = 1; i < route.feet.Length; i++)
                        c.routes.Add(new Route { a = route.feet[i - 1], b = route.feet[i] });
                    c.report.supplementalRoutes++;
                }
            }
            foreach (var door in c.root.GetComponentsInChildren<InteractiveDoor>(true))
            {
                if (!door.hinge || !door.leafCollider) continue;
                var leaf = door.leafCollider; var swing = leaf.bounds;
                for (int step = 0; step <= 18; step++) foreach (float x in new[] { -.5f, .5f }) foreach (float z in new[] { -.5f, .5f })
                {
                    var p = leaf.transform.TransformPoint(leaf.center + Vector3.Scale(leaf.size, new Vector3(x, 0, z)));
                    swing.Encapsulate(door.hinge.position + Quaternion.AngleAxis(door.openAngle * step / 18, door.hinge.up) * (p - door.hinge.position));
                }
                swing.Expand(new Vector3(.28f, .08f, .28f));
                var normal = leaf.transform.forward; normal.y = 0; normal.Normalize();
                c.doors.Add(new DoorReservation { swing = swing, center = leaf.bounds.center, normal = normal });
            }
        }

        static void PlaceCorridorBoxes(Context c)
        {
            var floors = c.native.OfType<BoxCollider>().Where(v => IsFloor(v) && v.bounds.size.y < .5f
                && Mathf.Min(v.bounds.size.x, v.bounds.size.z) >= 1.75f
                && Mathf.Min(v.bounds.size.x, v.bounds.size.z) <= 5.5f
                && Mathf.Max(v.bounds.size.x, v.bounds.size.z) >= 4f)
                .OrderBy(v => v.bounds.center.x).ThenBy(v => v.bounds.center.z).ToArray();
            var prefabBounds = LocalMeshBounds(c.prefab.transform);
            int sequence = 0;
            foreach (var floor in floors)
            {
                var bounds = floor.bounds; bool longX = bounds.size.x > bounds.size.z;
                float length = longX ? bounds.size.x : bounds.size.z;
                int count = Mathf.Clamp(Mathf.FloorToInt(length / 5), 1, 6);
                for (int index = 0; index < count && c.report.boxes < 44; index++)
                {
                    float along = (index + .55f) / count;
                    int side = index % 2 == 0 ? -1 : 1;
                    float inset = .44f;
                    var p = new Vector3(longX ? Mathf.Lerp(bounds.min.x, bounds.max.x, along) : bounds.center.x + side * (bounds.extents.x - inset),
                        bounds.max.y, !longX ? Mathf.Lerp(bounds.min.z, bounds.max.z, along) : bounds.center.z + side * (bounds.extents.z - inset));
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(c.prefab, c.group);
                    go.name = "cardboard_box_01 — circulação " + (++sequence).ToString("D2");
                    var t = go.transform; t.position = p; t.rotation = Quaternion.Euler(0, index * 37 + 11, index % 3 == 1 ? 90 : 0);
                    float scale = index % 3 == 0 ? 1.2f : .94f; t.localScale = Vector3.one * scale;
                    var body = go.AddComponent<BoxCollider>(); body.center = prefabBounds.center; body.size = prefabBounds.size; body.enabled = false;
                    var box = WorldBounds(t, prefabBounds); t.position += Vector3.up * (bounds.max.y + .012f - box.min.y);
                    box = WorldBounds(t, prefabBounds);
                    // A newly added disabled collider can still have an unsynchronised
                    // PhysX scale. Validate the same enabled shape used by the final audit.
                    body.enabled = true; Physics.SyncTransforms();
                    var nativeBox = body.bounds;
                    float delta = BoundsDelta(box, nativeBox);
                    c.report.maximumColliderBoundsDelta = Mathf.Max(c.report.maximumColliderBoundsDelta, delta);
                    if (delta > .002f || !ReservedClear(c, nativeBox) || !Supported(c, nativeBox) || !CollisionClear(c, body, nativeBox))
                    {
                        c.report.rejectedBoxCandidates++; UnityEngine.Object.DestroyImmediate(go);
                        Physics.SyncTransforms(); continue;
                    }
                    go.isStatic = true;
                    foreach (var renderer in go.GetComponentsInChildren<Renderer>(true)) renderer.receiveShadows = true;
                    c.report.boxes++; Physics.SyncTransforms();
                }
            }
        }

        static bool ReservedClear(Context c, Bounds box) => ReservedClear(c, box, out _);
        static bool ReservedClear(Context c, Bounds box, out string failure)
        {
            for (int index = 0; index < c.routes.Count; index++)
            {
                var route = c.routes[index];
                if (Mathf.Abs(route.a.y - box.min.y) < .7f && NearSegment(box, route.a, route.b, .64f))
                { failure = "Route segment " + index + ": " + route.a.ToString("F4") + " -> " + route.b.ToString("F4"); return false; }
            }
            foreach (var door in c.doors)
                if (box.Intersects(door.swing) || (Mathf.Abs(door.center.y - box.center.y) < 2
                    && NearSegment(box, door.center - door.normal * 1.9f, door.center + door.normal * 1.9f, .68f)))
                { failure = "Door swing/approach at " + door.center.ToString("F4") + "; swing=" + door.swing.ToString("F4"); return false; }
            foreach (var clue in c.clues)
                if (Mathf.Abs(clue.y - box.center.y) < 2 && HorizontalDistance(clue, box) < 1.2f)
                { failure = "Clue clearance at " + clue.ToString("F4"); return false; }
            failure = null;
            return true;
        }
        static float HorizontalDistance(Vector3 p, Bounds b)
        { float x = Mathf.Max(b.min.x - p.x, 0, p.x - b.max.x), z = Mathf.Max(b.min.z - p.z, 0, p.z - b.max.z); return Mathf.Sqrt(x * x + z * z); }
        static bool NearSegment(Bounds b, Vector3 a, Vector3 end, float radius)
        {
            float begin = 0, finish = 1; var delta = end - a;
            return Clip(-delta.x, a.x - b.min.x + radius, ref begin, ref finish)
                && Clip(delta.x, b.max.x + radius - a.x, ref begin, ref finish)
                && Clip(-delta.z, a.z - b.min.z + radius, ref begin, ref finish)
                && Clip(delta.z, b.max.z + radius - a.z, ref begin, ref finish);
        }
        static bool Clip(float p, float q, ref float begin, ref float end)
        {
            if (Mathf.Abs(p) < .00001f) return q >= 0;
            float r = q / p;
            if (p < 0) { if (r > end) return false; begin = Mathf.Max(begin, r); }
            else { if (r < begin) return false; end = Mathf.Min(end, r); }
            return true;
        }
        static bool IsFloor(Collider body)
        {
            var n = body.name.ToLowerInvariant();
            var bounds = body.bounds;
            return bounds.size.y < .55f && bounds.size.x > .5f && bounds.size.z > .5f
                && !n.Contains("rampa") && !n.Contains("escada") && !n.Contains("degrau") && !n.Contains("patamar")
                && !n.Contains("cobertura") && !n.Contains("forro") && (n.Contains("piso") || n.Contains("pavimento")
                || n.Contains("passarela") || n.Contains("passeio") || n.Contains("corredor") || n.Contains("plataforma"));
        }
        static bool Supported(Context c, Bounds box) => Supported(c, box, out _);
        static bool Supported(Context c, Bounds box, out string failure)
        {
            float? height = null;
            foreach (var p in new[] { box.center, new Vector3(box.min.x, 0, box.min.z), new Vector3(box.max.x, 0, box.min.z),
                new Vector3(box.min.x, 0, box.max.z), new Vector3(box.max.x, 0, box.max.z) })
            {
                var hit = Physics.RaycastAll(new Vector3(p.x, box.min.y + .15f, p.z), Vector3.down, .35f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => c.native.Contains(h.collider) && IsFloor(h.collider) && h.normal.y > .98f).OrderBy(h => h.distance).FirstOrDefault();
                if (!hit.collider)
                { failure = "No level native floor at support probe " + new Vector3(p.x, box.min.y, p.z).ToString("F4"); return false; }
                if (Mathf.Abs(hit.point.y - (height ?? hit.point.y)) > .03f || box.min.y - hit.point.y < .005f || box.min.y - hit.point.y > .03f)
                {
                    failure = "Floor=" + hit.collider.name + "; probe=" + new Vector3(p.x, box.min.y, p.z).ToString("F4")
                        + "; hit=" + hit.point.ToString("F4") + "; clearance=" + (box.min.y - hit.point.y).ToString("F6", CultureInfo.InvariantCulture)
                        + "; previousY=" + (height?.ToString("F6", CultureInfo.InvariantCulture) ?? "none"); return false;
                }
                height = hit.point.y;
            }
            failure = null;
            return true;
        }
        static bool CollisionClear(Context c, BoxCollider body, Bounds box) => CollisionClear(c, body, box, out _);
        static bool CollisionClear(Context c, BoxCollider body, Bounds box, out string failure)
        {
            foreach (var other in Physics.OverlapBox(box.center, box.extents + Vector3.one * .012f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!other.enabled || other == body || IsFloor(other)) continue;
                if (Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                    other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > .003f)
                {
                    failure = "Collider=" + other.name + "; parent=" + (other.transform.parent ? other.transform.parent.name : "none")
                        + "; depth=" + depth.ToString("F6", CultureInfo.InvariantCulture) + "; bounds=" + other.bounds.ToString("F4"); return false;
                }
            }
            failure = null;
            return true;
        }
        static float BoundsDelta(Bounds predicted, Bounds actual)
            => Mathf.Max((predicted.min - actual.min).magnitude, (predicted.max - actual.max).magnitude);
        static Bounds LocalMeshBounds(Transform root)
        {
            bool found = false; var bounds = new Bounds();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue; var local = filter.sharedMesh.bounds;
                foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                {
                    var p = root.InverseTransformPoint(filter.transform.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; } else bounds.Encapsulate(p);
                }
            }
            if (!found) throw new InvalidDataException("The approved box prefab has no mesh bounds."); return bounds;
        }
        static Bounds WorldBounds(Transform root, Bounds local)
        {
            var result = new Bounds(root.TransformPoint(local.center), Vector3.zero);
            foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                result.Encapsulate(root.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
            return result;
        }

        static Report Finish(Context c)
        {
            Physics.SyncTransforms();
            foreach (var body in c.group.GetComponentsInChildren<BoxCollider>(true))
            {
                bool supported = Supported(c, body.bounds, out string supportFailure);
                bool reserved = ReservedClear(c, body.bounds, out string reservationFailure);
                bool clear = CollisionClear(c, body, body.bounds, out string collisionFailure);
                float delta = BoundsDelta(WorldBounds(body.transform, new Bounds(body.center, body.size)), body.bounds);
                c.report.maximumColliderBoundsDelta = Mathf.Max(c.report.maximumColliderBoundsDelta, delta);
                if (!body.enabled || !supported || !reserved || !clear || delta > .002f)
                    throw new InvalidOperationException("Corridor box validation failed: " + body.name
                        + "; enabled=" + body.enabled + "; supported=" + supported + "; reserved=" + reserved + "; clear=" + clear
                        + "; boundsDelta=" + delta.ToString("F6", CultureInfo.InvariantCulture) + "; native=" + body.bounds.ToString("F4")
                        + "; predicted=" + WorldBounds(body.transform, new Bounds(body.center, body.size)).ToString("F4")
                        + "; support=" + supportFailure + "; reservation=" + reservationFailure + "; collision=" + collisionFailure);
            }
            var placed = c.group.GetComponentsInChildren<Collider>(true);
            foreach (var route in c.routes)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(route.a, route.b) / .24f));
                for (int i = 0; i <= steps; i++)
                {
                    var p = Vector3.Lerp(route.a, route.b, i / (float)steps);
                    if (Physics.OverlapCapsule(p + Vector3.up * .36f, p + Vector3.up * 1.44f, .33f, ~0, QueryTriggerInteraction.Ignore).Any(v => placed.Contains(v)))
                        throw new InvalidOperationException("New boxes block a previously validated standing route.");
                    c.report.routeSamples++;
                }
            }
            c.report.boxes = placed.Length;
            c.report.boxPositions = placed.Select(v => v.transform.position).ToArray();
            c.report.protectedAfter = ProtectedSignature(c.root, c.group);
            c.report.protectedArchitectureAndStoryPreserved = c.report.protectedBefore == c.report.protectedAfter;
            c.report.allBoxesSupported = c.report.reservedRoutesClear = true;
            c.report.notes = new[] {
                "Broad walls use the matched approved Poly Haven mossy plaster PBR set: original metric UV repeat 1.8 m, material scale .65 gives a calibrated visible repeat about 2.77 m; gentle normals (.18/.24), AO (.22/.28) and matte response preserve old two-tone hues. Dense cracked source preserved but removed from broad-room albedo; existing localized damage decals remain.",
                "Only vertical triangles backed by existing named masonry are reassigned. Native mesh vertices/colliders, horizontal ceilings, beams, windows, door panels and story components remain unchanged; mixed batches keep their other submeshes.",
                "Existing approved cardboard_box_01 is placed at narrow circulation floor edges, with a matching fitted collider. Five-point level support, native penetration, actual door swings and clue clearance reject unsafe candidates.",
                "Saved search routes and explicit authored stair/grade connectors are reserved at 1.28 m width and sampled against new boxes. Ramps, steps and landings receive no box clutter.",
                "No map rebuild, runtime script, entity collision, new light or automatic scene save. Apply is idempotent through its owned scene group." };
            return c.report;
        }

        public static Report Audit(Transform root)
        {
            var group = root ? root.Find(GroupName) : null;
            if (!group) throw new InvalidOperationException("No applied abandonment surface group to audit.");
            var c = CreateContext(root, group); c.report.protectedBefore = ProtectedSignature(root, group);
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>(); if (!filter || !filter.sharedMesh) continue;
                var materials = renderer.sharedMaterials; bool changed = false;
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                {
                    int count = filter.sharedMesh.GetTriangles(i).Length / 3;
                    if (i < materials.Length && materials[i] && materials[i].name.StartsWith("Abandoned-", StringComparison.Ordinal))
                    { changed = true; c.report.texturedWallTriangles += count; }
                    else c.report.retainedTriangles += count;
                }
                if (changed) c.report.changedRenderers++;
            }
            return Finish(c);
        }

        /// <summary>Remove only this pass's clutter that intersects an authored passage.</summary>
        public static Report RetuneRouteClearance(Transform root)
        {
            var group = root ? root.Find(GroupName) : null;
            if (!group || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Retune applied school clutter outside Play mode.");
            Physics.SyncTransforms();
            var c = CreateContext(root, group);
            c.report.protectedBefore = ProtectedSignature(root, group);
            var blocked = group.GetComponentsInChildren<BoxCollider>(true)
                .Where(body => !ReservedClear(c, body.bounds, out _)).ToArray();
            if (blocked.Length > 5)
                throw new InvalidOperationException("Unexpectedly broad clutter removal; inspect passage reservations first.");
            foreach (var body in blocked) UnityEngine.Object.DestroyImmediate(body.gameObject);
            if (blocked.Length > 0) EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            var result = Audit(root);
            result.removedObstructingBoxes = blocked.Length;
            if (ProtectedSignature(root, group) != c.report.protectedBefore)
                throw new InvalidOperationException("Route cleanup changed protected architecture or story.");
            return result;
        }
        /// <summary>Separates blue masonry paint from the shared metallic window-frame material.
        /// The caller can persist the complete replacement journal before any scene assignment.</summary>
        public static BlueWallChange[] ApplyBlueWalls(Transform root, Action<BlueWallChange[]> beforeApply = null)
        {
            if (!root || EditorUtility.IsPersistent(root) || EditorApplication.isPlayingOrWillChangePlaymode
                || root.gameObject.scene.path != SchoolIntegrationPass.ScenePath)
                throw new InvalidOperationException("Apply blue paint only to the saved school outside Play mode.");
            const string originalPath = "Assets/SchoolCEEP/Materials/PhotoFrameBlue.mat";
            const string paintPath = MaterialFolder + "/Night/Night-PhotoFrameBlueWall.mat";
            var original = AssetDatabase.LoadAssetAtPath<Material>(originalPath);
            if (!original) throw new InvalidDataException("The original blue frame material is missing.");
            Physics.SyncTransforms();
            var c = new Context { root = root, walls = root.GetComponentsInChildren<BoxCollider>(true)
                .Where(v => v.enabled && !v.isTrigger && v.gameObject.activeInHierarchy && IsMasonry(v.name)
                    && v.bounds.size.y > .15f && Mathf.Min(v.bounds.size.x, v.bounds.size.z) < .65f).ToArray() };
            EnsureFolder(MaterialFolder + "/Night"); EnsureFolder(MeshFolder + "/Night");
            var paint = AssetDatabase.LoadAssetAtPath<Material>(paintPath);
            if (!paint)
            {
                paint = new Material(original) { name = "Night-PhotoFrameBlueWall" };
                var color = original.GetColor("_BaseColor");
                float grey = color.r * .2126f + color.g * .7152f + color.b * .0722f;
                paint.SetColor("_BaseColor", Color.Lerp(color, new Color(grey, grey, grey, color.a), .35f));
                paint.SetFloat("_Metallic", 0); paint.SetFloat("_Smoothness", .1f);
                paint.SetFloat("_EnvironmentReflections", 0); paint.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                paint.SetFloat("_SpecularHighlights", 0); paint.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                AssetDatabase.CreateAsset(paint, paintPath);
            }
            var changes = new List<BlueWallChange>();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                if (!renderer.gameObject.activeInHierarchy || !materials.Contains(original)) continue;
                var filter = renderer.GetComponent<MeshFilter>(); var source = filter ? filter.sharedMesh : null;
                if (!source || !source.isReadable) throw new InvalidDataException("Blue paint source is not readable: " + renderer.name);
                var positions = source.vertices; var selected = new List<int>();
                var retained = new List<int>[source.subMeshCount];
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    retained[sub] = new List<int>();
                    if (source.GetTopology(sub) != MeshTopology.Triangles)
                        throw new InvalidDataException("Expected triangle topology in blue paint source.");
                    var indices = source.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                        var a = renderer.transform.TransformPoint(positions[ia]);
                        var b = renderer.transform.TransformPoint(positions[ib]);
                        var d = renderer.transform.TransformPoint(positions[ic]);
                        float horizontalSpan = Mathf.Max(Mathf.Max(a.x, b.x, d.x) - Mathf.Min(a.x, b.x, d.x),
                            Mathf.Max(a.z, b.z, d.z) - Mathf.Min(a.z, b.z, d.z));
                        bool wall = sub < materials.Length && materials[sub] == original
                            && horizontalSpan >= .25f
                            && BackedWall(c, a, b, d, Vector3.Cross(b - a, d - a).normalized, out _);
                        (wall ? selected : retained[sub]).AddRange(new[] { ia, ib, ic });
                    }
                }
                if (selected.Count == 0) continue;
                // Instantiate preserves every vertex attribute and all UV channels.
                // Only triangle-to-material membership changes; frames keep their original slot.
                var mesh = UnityEngine.Object.Instantiate(source); mesh.name = "Night blue paint — " + source.name;
                int paintSlot = source.subMeshCount;
                mesh.subMeshCount = paintSlot + 1;
                for (int sub = 0; sub < paintSlot; sub++) mesh.SetTriangles(retained[sub], sub, false);
                mesh.SetTriangles(selected, paintSlot, false);
                if (materials.Length != source.subMeshCount)
                    throw new InvalidDataException("Blue paint source has ambiguous material slots: " + renderer.name);
                var slots = materials.Concat(new[] { paint }).ToArray();
                string path = AssetDatabase.GenerateUniqueAssetPath(MeshFolder + "/Night/BlueWall-" + changes.Count.ToString("D3") + ".asset");
                AssetDatabase.CreateAsset(mesh, path);
                changes.Add(new BlueWallChange { filter = filter, renderer = renderer, beforeMesh = source, afterMesh = mesh,
                    beforeMaterials = materials, afterMaterials = slots, selectedTriangles = selected.Count / 3 });
            }
            var planned = changes.ToArray(); string protectedBefore = ProtectedSignature(root, null);
            try
            {
                AssetDatabase.SaveAssets(); beforeApply?.Invoke(planned);
                foreach (var change in planned)
                { change.filter.sharedMesh = change.afterMesh; change.renderer.sharedMaterials = change.afterMaterials; }
                if (protectedBefore != ProtectedSignature(root, null))
                    throw new InvalidOperationException("Blue paint changed a native collider, light or story component.");
                if (planned.Length > 0) EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                return planned;
            }
            catch
            {
                foreach (var change in planned)
                { change.filter.sharedMesh = change.beforeMesh; change.renderer.sharedMaterials = change.beforeMaterials; }
                throw;
            }
        }

        public static string CaptureProtectedState(Transform root) => ProtectedSignature(root, null);

        static string ProtectedSignature(Transform root, Transform group)
        {
            var rows = new List<string>();
            foreach (var collider in root.GetComponentsInChildren<Collider>(true).Where(v => !group || !v.transform.IsChildOf(group)))
                rows.Add("C|" + collider.GetEntityId() + "|" + EditorJsonUtility.ToJson(collider) + "|" + collider.transform.localToWorldMatrix.ToString("R", CultureInfo.InvariantCulture));
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true).Where(v => v && (!group || !v.transform.IsChildOf(group))))
                rows.Add("S|" + script.GetEntityId() + "|" + EditorJsonUtility.ToJson(script) + "|" + script.transform.localToWorldMatrix.ToString("R", CultureInfo.InvariantCulture));
            foreach (var light in root.GetComponentsInChildren<Light>(true)) rows.Add("L|" + light.GetEntityId() + "|" + EditorJsonUtility.ToJson(light));
            rows.Sort(StringComparer.Ordinal);
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", rows)))).Replace("-", "");
        }
        static int StableSeed(string value) { unchecked { int hash = 709; foreach (char ch in value) hash = hash * 31 + ch; return hash & int.MaxValue; } }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
