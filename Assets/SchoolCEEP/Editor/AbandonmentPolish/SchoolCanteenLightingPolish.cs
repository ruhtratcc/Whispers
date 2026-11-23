// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SchoolCEEP.Editor
{
    /// <summary>Remove the bright default sky reflection from unpowered indoor serving props.
    /// Scene-owned clones preserve the office, road and authored supernatural lighting.</summary>
    public static class SchoolCanteenLightingPolish
    {
        const string Folder = "Assets/SchoolCEEP/Materials/AbandonmentPolish/Canteen";
        [Serializable] public sealed class Report
        {
            public int renderers, materials, poweredFixtures, glossySkyReflections;
            public bool architecturePreserved, sceneOnly;
            public string[] materialPaths;
        }

        public static Report Apply(Transform architecture)
        {
            if (!architecture || architecture.gameObject.scene.path != SchoolIntegrationPass.ScenePath
                || EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(architecture))
                throw new InvalidOperationException("Edit the loaded school outside Play.");
            Folders(Folder);
            string before = SchoolChapterThreePass.ArchitectureSignature(architecture);
            var copy = new Dictionary<Material, Material>();
            int changedRenderers = 0;
            foreach (var renderer in architecture.GetComponentsInChildren<MeshRenderer>(true))
            {
                var assigned = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < assigned.Length; i++)
                {
                    var source = assigned[i];
                    if (!source) continue;
                    string name = source.name;
                    // Reapplying updates our own materials, never creates another copy chain.
                    if (name.StartsWith("CanteenNight-")) name = name.Substring("CanteenNight-".Length);
                    if (!name.StartsWith("PhotoRefectory")) continue;
                    if (!copy.TryGetValue(source, out var target))
                    {
                        string path = Folder + "/CanteenNight-" + name + ".mat";
                        target = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!target) { target = UnityEngine.Object.Instantiate(source); target.name = "CanteenNight-" + name; AssetDatabase.CreateAsset(target, path); }
                        target.DisableKeyword("_EMISSION");
                        if (target.HasProperty("_EmissionColor")) target.SetColor("_EmissionColor", Color.black);
                        target.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                        // No reflection probe exists inside the cantina. The global sky fallback
                        // made polished steel/glass conspicuous even behind walls and without a torch.
                        target.SetFloat("_EnvironmentReflections", 0);
                        target.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                        target.SetFloat("_Smoothness", name.Contains("Glass") ? .18f : name.Contains("Stainless") ? .23f : .12f);
                        // Preserve actual direct specular response to the player's flashlight.
                        target.SetFloat("_SpecularHighlights", 1);
                        target.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                        if (name.Contains("Stainless")) target.SetFloat("_Metallic", .72f);
                        if (name.Contains("Glass")) target.SetFloat("_Metallic", 0);
                        EditorUtility.SetDirty(target); copy.Add(source, target);
                    }
                    assigned[i] = target; changed = true;
                }
                if (!changed) continue;
                renderer.sharedMaterials = assigned;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorUtility.SetDirty(renderer); changedRenderers++;
            }
            string after = SchoolChapterThreePass.ArchitectureSignature(architecture);
            if (before != after) throw new InvalidOperationException("Canteen materials must not change physical architecture.");
            EditorSceneManager.MarkSceneDirty(architecture.gameObject.scene);
            return new Report {
                renderers = changedRenderers, materials = copy.Values.Distinct().Count(),
                materialPaths = copy.Values.Distinct().Select(AssetDatabase.GetAssetPath).ToArray(),
                architecturePreserved = true, sceneOnly = true,
                glossySkyReflections = architecture.GetComponentsInChildren<MeshRenderer>(true)
                    .Count(r => r.sharedMaterials.Any(m => m && m.name.StartsWith("CanteenNight-") && !m.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"))),
                poweredFixtures = architecture.GetComponentsInChildren<Light>(true)
                    .Count(l => l.name.StartsWith("Refeitório") && l.enabled)
            };
        }

        static void Folders(string path)
        {
            string current="Assets";
            foreach(string piece in path.Substring(7).Split('/'))
            { string next=current+"/"+piece;if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,piece);current=next; }
        }
    }
}
