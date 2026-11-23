// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HorrorGame;
using HorrorGame.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    /// <summary>
    /// The road chapter borrows the saved school's entrance, not a second invented school.
    /// Only geometry is copied: school story, save objects, lights and actors stay in SchoolCEEP.
    /// Batched surfaces are reduced by triangle membership, preserving every retained vertex.
    /// </summary>
    public static class RoadEntranceRefinementPass
    {
        public const string GroupName = "Chegada — entrada literal da escola";
        public const string Output = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/Refinement";
        const string MeshFolder = "Assets/RoadChapter/Meshes/SchoolEntrance";
        const string MaterialFolder = "Assets/RoadChapter/Data/Refinement";

        [Serializable] public sealed class Patch
        {
            public string source, kind;
            public int sourceTriangles, retainedTriangles, rejectedStraddlers;
        }
        [Serializable] public sealed class Report
        {
            public bool passed, schoolFileUnchanged, noSchoolRuntimeCopied, arrivalWindowVisible;
            public string sourceScene, sourceSha256, roadScene;
            public Vector3 translation, sourceGateCenter, targetGateCenter, sourceWindow, targetWindow, seatedEye;
            public int copiedRenderers, copiedColliders, sourceTriangles, retainedTriangles, rejectedStraddlers;
            public int realEntranceTreads, gameSessions, cameras, roadInteractions;
            public float driveSeconds, gateFloor;
            public string[] removed, failures;
            public Patch[] patches;
        }

        static readonly HashSet<string> FrontGroups = new HashSet<string>(StringComparer.Ordinal)
        {
            "administration_south", "pedagogy_south", "covered_courtyard",
            "Arquitetura das fotos", "Passarelas cobertas", "Desníveis do hall"
        };
        static readonly HashSet<string> BackdropGroups = new HashSet<string>(StringComparer.Ordinal)
        {
            "main_classrooms_north", "main_middle_circulation", "Detalhes do central"
        };
        // Portal, flanking ground-floor rooms and the main covered hall. Excludes parking/site slabs.
        static readonly Bounds Front = new Bounds(new Vector3(-2.5f, 4, 23.75f), new Vector3(58, 12, 48.5f));
        // The existing upper frontal classroom glazing is visible behind the portal, as in the school.
        static readonly Bounds Backdrop = new Bounds(new Vector3(-12.5f, 4, 59.2f), new Vector3(26, 12, 8.4f));

        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Refine the saved road scene outside Play and compilation.");
            var road = SceneManager.GetActiveScene();
            if (road.path != RoadJourneyBuilder.ScenePath || road.isDirty)
                throw new InvalidOperationException("Open the saved, clean RoadJourney scene first.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save every loaded scene before copying the entrance.");
            Directory.CreateDirectory(Output);
            var flow = road.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RoadJourneyFlow>(true)).Single();
            if (!flow.gate || !flow.windowAnchor || !flow.windowVisual || !flow.windowLight || !flow.carRoot || !flow.seatedPose)
                throw new InvalidOperationException("Preserve the current road chapter references.");
            var schoolBefore = Hash(SchoolIntegrationPass.ScenePath);
            File.Copy(RoadJourneyBuilder.ScenePath, Output + "/Road-before-entrance-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".unity", true);
            var sourceScene = SceneManager.GetSceneByPath(SchoolIntegrationPass.ScenePath);
            bool openedSource = !sourceScene.IsValid() || !sourceScene.isLoaded;
            if (openedSource) sourceScene = EditorSceneManager.OpenScene(SchoolIntegrationPass.ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(road);
            Report report;
            try
            {
                var architecture = sourceScene.GetRootGameObjects().Single(g => g.name.StartsWith("CEEP", StringComparison.Ordinal)).transform;
                var schoolFlow = architecture.GetComponentInChildren<SchoolOpeningFlow>(true);
                if (!schoolFlow || !schoolFlow.entryDoor || !schoolFlow.entryDoor.pairedLeaf)
                    throw new InvalidOperationException("The reviewed real entrance requires its two original gate leaves.");
                var sourceGate = (schoolFlow.entryDoor.transform.position + schoolFlow.entryDoor.pairedLeaf.transform.position) * .5f;
                Physics.SyncTransforms();
                Vector3 targetGate = flow.gate.transform.position;
                targetGate.y = sourceGate.y;
                Vector3 translation = targetGate - sourceGate;
                // A translation only: the real entrance retains its dimensions, grade and orientation.
                if (Mathf.Abs(translation.y) > .0001f)
                    throw new InvalidOperationException("Do not flatten or rescale the school entrance.");
                var prior = road.GetRootGameObjects().FirstOrDefault(g => g.name == GroupName);
                if (prior) UnityEngine.Object.DestroyImmediate(prior);
                var copied = new GameObject(GroupName).transform;
                SceneManager.MoveGameObjectToScene(copied.gameObject, road);
                copied.position = translation;
                EnsureFolder(MeshFolder); EnsureFolder(MaterialFolder);
                var meshRevision = MeshFolder + "/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
                EnsureFolder(meshRevision);
                var patches = new List<Patch>();
                int rendererCount = 0, colliderCount = 0, triangles = 0, retained = 0, straddlers = 0;
                foreach (Transform group in architecture)
                {
                    Bounds region;
                    if (FrontGroups.Contains(group.name)) region = Front;
                    else if (BackdropGroups.Contains(group.name)) region = Backdrop;
                    else continue;
                    var outputGroup = new GameObject(group.name + " — cópia exterior").transform;
                    outputGroup.SetParent(copied, false);
                    foreach (var filter in group.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var sourceRenderer = filter.GetComponent<MeshRenderer>();
                        if (!sourceRenderer || !sourceRenderer.enabled || !filter.gameObject.activeInHierarchy || !filter.sharedMesh
                            || filter.GetComponent<TMPro.TMP_Text>()) continue;
                        if (!region.Intersects(sourceRenderer.bounds)) continue;
                        var subset = Subset(filter, region, out int before, out int kept, out int rejected);
                        triangles += before; retained += kept; straddlers += rejected;
                        if (!subset) continue;
                        string path = meshRevision + "/Entrance-" + rendererCount + ".asset";
                        AssetDatabase.CreateAsset(subset, path);
                        var visual = new GameObject(filter.name, typeof(MeshFilter), typeof(MeshRenderer));
                        visual.transform.SetParent(outputGroup, false); visual.isStatic = true;
                        visual.layer = filter.gameObject.layer;
                        visual.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        var renderer = visual.GetComponent<MeshRenderer>();
                        EditorUtility.CopySerialized(sourceRenderer, renderer);
                        renderer.enabled = true;
                        renderer.lightProbeUsage = LightProbeUsage.Off;
                        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        patches.Add(new Patch { source = Path(filter.transform, architecture), kind = "render", sourceTriangles = before,
                            retainedTriangles = kept, rejectedStraddlers = rejected });
                        rendererCount++;
                    }
                    foreach (var collider in group.GetComponentsInChildren<Collider>(true))
                    {
                        // Exterior-only scenery: no triggers, story volumes or partial collider clipping.
                        if (!collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy
                            || !Inside(region, collider.bounds.min) || !Inside(region, collider.bounds.max)) continue;
                        var clone = GeometryObject(collider.transform, outputGroup);
                        var copiedCollider = (Collider)clone.AddComponent(collider.GetType());
                        EditorUtility.CopySerialized(collider, copiedCollider);
                        var surface = collider.GetComponent<SurfaceType>();
                        if (surface) clone.AddComponent<SurfaceType>().surface = surface.surface;
                        patches.Add(new Patch { source = Path(collider.transform, architecture), kind = "collision" });
                        colliderCount++;
                    }
                }
                var removed = RemoveGenericFacade(flow, road);
                // Keep the road interaction/save contract and give it the real school's closed gate geometry.
                foreach (Transform child in flow.gate.transform.Cast<Transform>().ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                foreach (var ownCollider in flow.gate.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(ownCollider);
                flow.gate.transform.position = targetGate;
                flow.gate.transform.rotation = Quaternion.identity;
                foreach (var door in new[] { schoolFlow.entryDoor, schoolFlow.entryDoor.pairedLeaf })
                    CopyGate(door, flow.gate.transform, translation);
                flow.gate.visual = null;
                NarrowForecourt(flow, road);
                Vector3 sourceWindow = BindRealWindow(architecture, flow, translation);
                Physics.SyncTransforms();
                var seatedEye = flow.driveEnd.position + flow.driveEnd.rotation * flow.carRoot.InverseTransformPoint(flow.seatedPose.position);
                report = new Report
                {
                    sourceScene = SchoolIntegrationPass.ScenePath, sourceSha256 = schoolBefore, roadScene = road.path,
                    translation = translation, sourceGateCenter = sourceGate, targetGateCenter = targetGate,
                    sourceWindow = sourceWindow, targetWindow = flow.windowAnchor.position, seatedEye = seatedEye,
                    copiedRenderers = rendererCount, copiedColliders = colliderCount, sourceTriangles = triangles,
                    retainedTriangles = retained, rejectedStraddlers = straddlers, patches = patches.ToArray(), removed = removed,
                    gateFloor = sourceGate.y, driveSeconds = flow.driveDuration
                };
                Validate(road, copied, flow, report);
                report.schoolFileUnchanged = Hash(SchoolIntegrationPass.ScenePath) == schoolBefore;
                if (!report.schoolFileUnchanged) throw new InvalidOperationException("Source school file changed during exterior copy.");
                EditorUtility.SetDirty(flow); EditorUtility.SetDirty(flow.gate);
                AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(road); EditorSceneManager.SaveScene(road);
                File.WriteAllText(Output + "/road-entrance-copy.json", JsonUtility.ToJson(report, true));
            }
            finally
            {
                if (openedSource && sourceScene.IsValid() && sourceScene.isLoaded) EditorSceneManager.CloseScene(sourceScene, true);
                SceneManager.SetActiveScene(road);
            }
            return JsonUtility.ToJson(report, true);
        }

        static string[] RemoveGenericFacade(RoadJourneyFlow flow, Scene road)
        {
            var environment = road.GetRootGameObjects().Single(g => g.name == "Caminho — cenário exterior").transform;
            var removed = new List<string>();
            foreach (Transform child in environment.Cast<Transform>().ToArray())
            {
                if (child == flow.gate.transform || child == flow.windowAnchor || child.gameObject == flow.windowVisual || child == flow.windowLight.transform) continue;
                bool generic = child.name.StartsWith("Escola —", StringComparison.Ordinal)
                    || child.name.StartsWith("Entrada —", StringComparison.Ordinal)
                    || child.name == "Chegada — mureta do passeio" || child.name == "Chegada — limite do terreno";
                if (!generic) continue;
                removed.Add(child.name); UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            return removed.ToArray();
        }

        static void NarrowForecourt(RoadJourneyFlow flow, Scene road)
        {
            var environment = road.GetRootGameObjects().Single(g => g.name == "Caminho — cenário exterior").transform;
            var sidewalk = environment.Cast<Transform>().Single(t => t.name == "Chegada — passeio");
            var scale = sidewalk.localScale; scale.x = 8; sidewalk.localScale = scale;
            // Retain the original rear invisible boundary. Side limits have no visible masonry.
            var prior = environment.Find("Chegada — limites invisíveis laterais");
            if (prior) UnityEngine.Object.DestroyImmediate(prior.gameObject);
            var boundary = new GameObject("Chegada — limites invisíveis laterais").transform; boundary.SetParent(environment, false);
            foreach (int side in new[] { -1, 1 })
            {
                var limit = new GameObject("Limite lateral sem parede"); limit.transform.SetParent(boundary, false);
                limit.transform.position = new Vector3(flow.gate.transform.position.x + side * 4, 2, 393.5f);
                limit.AddComponent<BoxCollider>().size = new Vector3(.12f, 4, 29);
            }
            flow.driveEnd.name = "Estrada — chegada à entrada";
        }

        static Vector3 BindRealWindow(Transform architecture, RoadJourneyFlow flow, Vector3 translation)
        {
            var classroom = architecture.Find("main_classrooms_north");
            var window = classroom.GetComponentsInChildren<BoxCollider>(true)
                .Where(c => c.name == "Janela — vidro opaco provisório" && c.bounds.center.y > 4 && c.bounds.size.x > 4)
                .OrderBy(c => Mathf.Abs(c.bounds.center.z - 58.06722f)).FirstOrDefault();
            if (!window || Mathf.Abs(window.bounds.center.z - 58.06722f) > .25f)
                throw new InvalidOperationException("The reviewed upper frontal classroom glazing is missing.");
            var b = window.bounds;
            // A lit pane in the real glazing, with a small offset toward the exterior, not an invented window in a wall.
            var source = new Vector3(b.max.x - 1.5f, b.center.y + .18f, b.min.z - .025f);
            flow.windowAnchor.position = source + translation;
            flow.windowVisual.transform.SetPositionAndRotation(source + translation + Vector3.forward * .003f, Quaternion.identity);
            flow.windowVisual.transform.localScale = new Vector3(1.55f, 1.00f, .018f);
            var renderer = flow.windowVisual.GetComponent<MeshRenderer>();
            if (!renderer) throw new InvalidOperationException("Keep the road's existing window presentation object.");
            string path = MaterialFolder + "/Window-Arrival-Warm.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", new Color(.67f, .52f, .30f)); renderer.sharedMaterial = material;
            EditorUtility.SetDirty(material);
            flow.windowVisual.SetActive(false);
            flow.windowLight.transform.position = source + translation - Vector3.forward * .15f;
            flow.windowLight.enabled = false; flow.windowLight.intensity = 3; flow.windowLight.range = 5;
            return source;
        }

        static void CopyGate(InteractiveDoor source, Transform parent, Vector3 translation)
        {
            var maps = new Dictionary<Transform, Transform>();
            foreach (var sourceTransform in source.GetComponentsInChildren<Transform>(true))
            {
                var copy = new GameObject(sourceTransform.name).transform;
                if (sourceTransform == source.transform)
                {
                    copy.SetParent(parent, true); copy.position = sourceTransform.position + translation;
                    copy.rotation = sourceTransform.rotation; copy.localScale = sourceTransform.lossyScale;
                }
                else
                {
                    copy.SetParent(maps[sourceTransform.parent], false);
                    copy.localPosition = sourceTransform.localPosition; copy.localRotation = sourceTransform.localRotation; copy.localScale = sourceTransform.localScale;
                }
                copy.gameObject.layer = sourceTransform.gameObject.layer; copy.gameObject.isStatic = true;
                maps.Add(sourceTransform, copy);
                var filter = sourceTransform.GetComponent<MeshFilter>(); var renderer = sourceTransform.GetComponent<MeshRenderer>();
                if (filter && renderer)
                {
                    copy.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    EditorUtility.CopySerialized(renderer, copy.gameObject.AddComponent<MeshRenderer>());
                }
                foreach (var collider in sourceTransform.GetComponents<Collider>())
                    if (collider.enabled && !collider.isTrigger) EditorUtility.CopySerialized(collider, copy.gameObject.AddComponent(collider.GetType()));
            }
        }

        static GameObject GeometryObject(Transform source, Transform parent)
        {
            var go = new GameObject(source.name); go.transform.SetParent(parent, false);
            // parent belongs to the translated copy; source coordinates remain identical beneath it.
            go.transform.localPosition = source.position; go.transform.localRotation = source.rotation; go.transform.localScale = source.lossyScale;
            go.layer = source.gameObject.layer; go.isStatic = true; return go;
        }

        static Mesh Subset(MeshFilter filter, Bounds region, out int total, out int retained, out int straddlers)
        {
            var source = filter.sharedMesh;
            var input = source.vertices; var normals = source.normals; var uv = source.uv; var uv2 = source.uv2;
            var tangents = source.tangents; var colors = source.colors;
            var vertices = new List<Vector3>(); var outputNormals = new List<Vector3>(); var outputUv = new List<Vector2>();
            var outputUv2 = new List<Vector2>(); var outputTangents = new List<Vector4>(); var outputColors = new List<Color>();
            var map = new Dictionary<int, int>(); var groups = new List<int[]>();
            Matrix4x4 matrix = filter.transform.localToWorldMatrix, normalMatrix = matrix.inverse.transpose;
            total = retained = straddlers = 0;
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                if (source.GetTopology(submesh) != MeshTopology.Triangles) throw new InvalidOperationException("Entrance copy expects triangular surfaces.");
                var indices = source.GetTriangles(submesh); var output = new List<int>();
                for (int i = 0; i < indices.Length; i += 3)
                {
                    total++;
                    var a = matrix.MultiplyPoint3x4(input[indices[i]]); var b = matrix.MultiplyPoint3x4(input[indices[i + 1]]); var c = matrix.MultiplyPoint3x4(input[indices[i + 2]]);
                    bool ia = Inside(region, a), ib = Inside(region, b), ic = Inside(region, c);
                    if (!ia || !ib || !ic) { if (ia || ib || ic) straddlers++; continue; }
                    retained++;
                    for (int j = 0; j < 3; j++)
                    {
                        int old = indices[i + j];
                        if (!map.TryGetValue(old, out int next))
                        {
                            next = vertices.Count; map.Add(old, next); vertices.Add(matrix.MultiplyPoint3x4(input[old]));
                            if (normals.Length == input.Length) outputNormals.Add(normalMatrix.MultiplyVector(normals[old]).normalized);
                            if (uv.Length == input.Length) outputUv.Add(uv[old]);
                            if (uv2.Length == input.Length) outputUv2.Add(uv2[old]);
                            if (colors.Length == input.Length) outputColors.Add(colors[old]);
                            if (tangents.Length == input.Length)
                            {
                                var t = matrix.MultiplyVector(new Vector3(tangents[old].x, tangents[old].y, tangents[old].z)).normalized;
                                outputTangents.Add(new Vector4(t.x, t.y, t.z, tangents[old].w));
                            }
                        }
                        output.Add(next);
                    }
                }
                groups.Add(output.ToArray());
            }
            if (retained == 0) return null;
            var mesh = new Mesh { name = "Exact exterior subset — " + source.name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.subMeshCount = groups.Count;
            for (int i = 0; i < groups.Count; i++) mesh.SetTriangles(groups[i], i, false);
            if (outputNormals.Count > 0) mesh.SetNormals(outputNormals); else mesh.RecalculateNormals();
            if (outputUv.Count > 0) mesh.SetUVs(0, outputUv);
            if (outputUv2.Count > 0) mesh.SetUVs(1, outputUv2);
            if (outputTangents.Count > 0) mesh.SetTangents(outputTangents);
            if (outputColors.Count > 0) mesh.SetColors(outputColors);
            mesh.RecalculateBounds(); return mesh;
        }

        static void Validate(Scene road, Transform copied, RoadJourneyFlow flow, Report report)
        {
            var failures = new List<string>(); var roots = road.GetRootGameObjects();
            report.gameSessions = roots.Sum(g => g.GetComponentsInChildren<GameSession>(true).Length);
            report.cameras = roots.Sum(g => g.GetComponentsInChildren<Camera>(true).Length);
            report.roadInteractions = roots.Sum(g => g.GetComponentsInChildren<RoadJourneyInteraction>(true).Length);
            report.noSchoolRuntimeCopied = copied.GetComponentsInChildren<MonoBehaviour>(true).All(c => c is SurfaceType)
                && flow.gate.GetComponentsInChildren<MonoBehaviour>(true).All(c => c == flow.gate);
            report.realEntranceTreads = copied.GetComponentsInChildren<BoxCollider>(true).Count(c => c.name.StartsWith("Entrada_Hall — degrau ", StringComparison.Ordinal));
            if (report.gameSessions != 1 || report.cameras != 1 || report.roadInteractions != 4) failures.Add("Preserve one road GameSession, one camera and the four existing interaction objects.");
            if (!report.noSchoolRuntimeCopied) failures.Add("Only geometry and surface metadata may be transplanted from the school.");
            if (report.realEntranceTreads != 4) failures.Add("Copy the school's exact four entrance treads.");
            if (report.copiedRenderers < 8 || report.retainedTriangles < 100) failures.Add("School entrance copy is incomplete.");
            if (!Mathf.Approximately(flow.driveDuration, 24)) failures.Add("The arrival journey must use the shortened 24-second drive.");
            Vector3 delta = flow.windowAnchor.position - report.seatedEye;
            bool physical = !Physics.Raycast(report.seatedEye, delta.normalized, out var hit, delta.magnitude - .08f,
                flow.observationMask, QueryTriggerInteraction.Ignore);
            bool visual = !RenderedOcclusion(copied, report.seatedEye, flow.windowAnchor.position, out string visualBlocker);
            report.arrivalWindowVisible = physical && visual;
            if (!physical) failures.Add("Arrival window physically occluded by " + (hit.collider ? hit.collider.name : "unknown"));
            if (!visual) failures.Add("Arrival window visually occluded by " + visualBlocker);
            foreach (var renderer in copied.GetComponentsInChildren<Renderer>(true))
                if (!renderer.GetComponent<MeshFilter>() || !renderer.GetComponent<MeshFilter>().sharedMesh) failures.Add("Copied renderer has no saved mesh: " + renderer.name);
            report.failures = failures.ToArray(); report.passed = failures.Count == 0;
            if (!report.passed)
            {
                File.WriteAllText(Output + "/road-entrance-copy-failed.json", JsonUtility.ToJson(report, true));
                throw new InvalidOperationException(string.Join("\n", failures));
            }
        }

        static bool RenderedOcclusion(Transform copied, Vector3 from, Vector3 target, out string blocker)
        {
            Vector3 vector = target - from; float maximum = vector.magnitude - .08f; Vector3 direction = vector.normalized;
            foreach (var filter in copied.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !renderer.enabled || !filter.gameObject.activeInHierarchy || !filter.sharedMesh) continue;
                if (!renderer.bounds.IntersectRay(new Ray(from, direction), out float near) || near > maximum) continue;
                var mesh = filter.sharedMesh; var v = mesh.vertices; var mats = renderer.sharedMaterials;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var mat = s < mats.Length ? mats[s] : null;
                    if (mat && (mat.renderQueue >= 3000 || mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > .5f)) continue;
                    var indices = mesh.GetTriangles(s);
                    for (int i = 0; i < indices.Length; i += 3)
                        if (RayTriangle(from, direction, filter.transform.TransformPoint(v[indices[i]]),
                            filter.transform.TransformPoint(v[indices[i + 1]]), filter.transform.TransformPoint(v[indices[i + 2]]), out float distance)
                            && distance > .01f && distance < maximum)
                        { blocker = filter.name + " at " + distance.ToString("F2") + "m"; return true; }
                }
            }
            blocker = ""; return false;
        }

        static bool RayTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            var ab = b - a; var ac = c - a; var cross = Vector3.Cross(direction, ac); float determinant = Vector3.Dot(ab, cross);
            distance = 0; if (Mathf.Abs(determinant) < .000001f) return false;
            float inverse = 1 / determinant; var relative = origin - a; float u = Vector3.Dot(relative, cross) * inverse;
            if (u < -.00001f || u > 1.00001f) return false;
            var q = Vector3.Cross(relative, ab); float w = Vector3.Dot(direction, q) * inverse;
            if (w < -.00001f || u + w > 1.00001f) return false;
            distance = Vector3.Dot(ac, q) * inverse; return distance >= 0;
        }
        static bool Inside(Bounds bounds, Vector3 p) => p.x >= bounds.min.x - .001f && p.x <= bounds.max.x + .001f
            && p.y >= bounds.min.y - .001f && p.y <= bounds.max.y + .001f && p.z >= bounds.min.z - .001f && p.z <= bounds.max.z + .001f;
        static string Path(Transform t, Transform stop)
        { var names = new List<string>(); while (t && t != stop) { names.Add(t.name); t = t.parent; } names.Reverse(); return string.Join("/", names); }
        static string Hash(string path)
        { using (var digest = SHA256.Create()) return BitConverter.ToString(digest.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
        static void EnsureFolder(string path)
        { string current = "Assets"; foreach (var segment in path.Split('/').Skip(1)) { if (!AssetDatabase.IsValidFolder(current + "/" + segment)) AssetDatabase.CreateFolder(current, segment); current += "/" + segment; } }
    }
}
