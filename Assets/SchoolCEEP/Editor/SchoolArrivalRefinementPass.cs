// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    /// <summary>Saved-scene polish only: replace the two parking slabs and their lines, keep the actual entrance route.</summary>
    public static class SchoolArrivalRefinementPass
    {
        public const string GroupName = "Escola — pátio de chegada refinado";
        const string Folder = "Assets/SchoolCEEP/Meshes/ArrivalRefinement";
        [Serializable] public sealed class Report
        {
            public int parkingSlabs, parkingLines, removedTriangles, changedBatches, signsRemoved;
            public bool repeated, entrancePreserved;
            public string[] assets;
        }
        public static Report Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != SchoolIntegrationPass.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Start arrival polish from the clean saved school outside Play.");
            var root = scene.GetRootGameObjects().Single(g => g.name == "CEEP — arquitetura das plantas").transform;
            string reportPath = SchoolMapBuilder.Output + "/School-arrival-refinement.json";
            if (root.Find(GroupName)) return JsonUtility.FromJson<Report>(File.ReadAllText(reportPath));
            var terrain = root.Find("Terreno e acessos");
            var parts = terrain.Cast<Transform>().Where(t => t.name.StartsWith("Estacionamento ") || t.name == "Marca de vaga").ToArray();
            var slabs = parts.Where(t => t.name.StartsWith("Estacionamento ")).ToArray();
            if (slabs.Length != 2 || parts.Count(t => t.name == "Marca de vaga") != 42)
                throw new InvalidOperationException("Parking selection no longer matches the saved scene; no changes made.");
            // Prove every selected cube face by its complete native volume before changing a batch.
            var bounds = parts.Select(t => new Bounds(t.position, t.lossyScale)).ToArray();
            var plans = new List<(MeshFilter filter, Mesh source, int[] kept, int removed)>();
            int faces = 0;
            foreach (var filter in terrain.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh && f.name.StartsWith("Superfícies agrupadas — ")))
            {
                var mesh = filter.sharedMesh;
                if (mesh.subMeshCount != 1) throw new InvalidOperationException("Expected original one-material terrain batches.");
                var vertices = mesh.vertices.Select(filter.transform.TransformPoint).ToArray();
                var indices = mesh.triangles; var keep = new List<int>(); int removed = 0;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    bool selected = bounds.Any(b => In(b, vertices[indices[i]]) && In(b, vertices[indices[i + 1]]) && In(b, vertices[indices[i + 2]]));
                    if (selected) removed++; else { keep.Add(indices[i]); keep.Add(indices[i + 1]); keep.Add(indices[i + 2]); }
                }
                if (removed > 0) { plans.Add((filter, mesh, keep.ToArray(), removed)); faces += removed; }
            }
            if (faces != parts.Length * 12) throw new InvalidOperationException("Parking face proof incomplete: " + faces + "/" + parts.Length * 12 + ". No changes made.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var savedAssets = new List<string>(); int n = 0;
            foreach (var p in plans)
            {
                var copy = UnityEngine.Object.Instantiate(p.source); copy.name = p.source.name + " — sem estacionamento";
                copy.SetTriangles(p.kept, 0); copy.RecalculateBounds();
                string path = Folder + "/TerrainBatch-" + n++ + ".asset";
                if (AssetDatabase.LoadMainAssetAtPath(path)) throw new InvalidOperationException("Mesh path already used without applied group.");
                AssetDatabase.CreateAsset(copy, path); p.filter.sharedMesh = copy; EditorUtility.SetDirty(p.filter); savedAssets.Add(path);
            }
            var group = new GameObject(GroupName).transform; group.SetParent(root, false);
            var grass = AssetDatabase.LoadAssetAtPath<Material>(SchoolMapBuilder.Folder + "/Materials/Solo e grama.mat");
            foreach (var slab in slabs)
            {
                // The flat ground height stays unchanged, including the final garden/window viewing position.
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Pátio lateral — terreno sem vagas";
                ground.transform.SetParent(group, false); ground.transform.position = slab.position; ground.transform.localScale = slab.lossyScale;
                ground.GetComponent<Renderer>().sharedMaterial = grass; UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
                ground.isStatic = true;
            }
            foreach (var mark in parts.Where(t => t.name == "Marca de vaga")) mark.gameObject.SetActive(false);
            int signs = 0;
            foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(true).Where(t => t.text == "CEEP" || t.text.Contains("CENTRO ESTADUAL DE")))
            { t.gameObject.SetActive(false); signs++; }
            var effect = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HorrorGame.VintageCameraEffect>(true)).Single();
            effect.ConfigureSchoolTreatment(); EditorUtility.SetDirty(effect); PrefabUtility.RecordPrefabInstancePropertyModifications(effect);
            var report = new Report { parkingSlabs = 2, parkingLines = 42, removedTriangles = faces, changedBatches = plans.Count, signsRemoved = signs, entrancePreserved = true, assets = savedAssets.ToArray() };
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            return report;
        }
        static bool In(Bounds b, Vector3 p) => p.x >= b.min.x - .0001f && p.x <= b.max.x + .0001f
            && p.y >= b.min.y - .0001f && p.y <= b.max.y + .0001f && p.z >= b.min.z - .0001f && p.z <= b.max.z + .0001f;
    }
}
