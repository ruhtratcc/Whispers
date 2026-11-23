// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SchoolCEEP.Editor
{
    /// <summary>Import verified static glTF preparation into native URP assets, without a runtime importer.</summary>
    public static class SchoolArtAssetImporter
    {
        const string Models = "Assets/SchoolCEEP/Models/AbandonmentPolish";
        const string Textures = "Assets/textures/AbandonmentPolish";
        const string Materials = "Assets/SchoolCEEP/Materials/AbandonmentPolish";
        static readonly string[] Ids = { "ceiling_fan", "wall_clock", "metal_trash_can", "plastic_broom" };
        [Serializable] public sealed class MaterialInfo
        {
            public string name, diffuse, normal, metallicSmoothness, occlusion, alphaMode;
            public Color color = Color.white;
            public float metallic, smoothness, alphaCutoff = .5f;
            public bool doubleSided;
        }
        [Serializable] public sealed class Catalogue
        {
            public string id, textureGroup;
            public Vector3 size;
            public SchoolReadyAssetPass.MeshPart[] parts, distantParts;
            public MaterialInfo[] materials;
        }
        [Serializable] public sealed class Report
        {
            public int models, materials, nearTriangles, distantTriangles, normalMaps, textures;
            public string[] prefabs;
            public bool nativeMeshes, noRuntimeImporter, allEmissionOff;
        }

        public static Report Import() => ImportModels(Models, Textures, Materials, Ids, true);

        /// <summary>Reuse the verified native importer for a separate prepared asset set.</summary>
        public static Report ImportModels(string modelFolder, string textureFolder, string materialFolder,
            string[] ids, bool includeExistingWallSets = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Import outside Play.");
            if (ids == null || ids.Length == 0 || new[] { modelFolder, textureFolder, materialFolder }
                .Any(p => string.IsNullOrWhiteSpace(p) || !p.StartsWith("Assets/", StringComparison.Ordinal)
                    || p.Split('/').Any(part => part == "..")))
                throw new ArgumentException("Use explicit prepared folders inside Assets.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Folder(materialFolder);
            var report = new Report { nativeMeshes = true, noRuntimeImporter = true, allEmissionOff = true };
            var paths = new List<string>();
            var configuredTextures = new HashSet<string>(StringComparer.Ordinal);
            var uniqueMaterials = new HashSet<Material>();
            foreach (var id in ids)
            {
                string folder = modelFolder + "/" + id;
                var data = JsonUtility.FromJson<Catalogue>(File.ReadAllText(folder + "/MeshData.json"));
                if (data?.parts == null || data.materials == null || data.parts.Length == 0) throw new InvalidDataException("Missing prepared model: " + id);
                string textureId = string.IsNullOrEmpty(data.textureGroup) ? id : data.textureGroup;
                if (configuredTextures.Add(textureId)) ConfigureTextures(textureId, report, textureFolder);
                var mats = data.materials.Select((info, i) => MakeMaterial(textureId, i, info, null, materialFolder, textureFolder)).ToArray();
                uniqueMaterials.UnionWith(mats);
                var preview = EditorSceneManager.NewPreviewScene();
                var root = new GameObject(id + " — ready model");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
                try
                {
                    var near = Meshes(root.transform, folder, data.parts, mats, "LOD0", out int nearTriangles);
                    report.nearTriangles += nearTriangles;
                    if (data.distantParts != null && data.distantParts.Length > 0)
                    {
                        var distant = Meshes(root.transform, folder, data.distantParts, mats, "LOD1", out int distantTriangles);
                        report.distantTriangles += distantTriangles;
                        var lod = root.AddComponent<LODGroup>();
                        lod.SetLODs(new[] { new LOD(.085f, near), new LOD(.006f, distant) });
                        lod.fadeMode = LODFadeMode.None; lod.RecalculateBounds();
                    }
                    string path = folder + "/" + id + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(root, path); paths.Add(path); report.models++;
                }
                finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
            }
            if (includeExistingWallSets) foreach (string id in new[] { "worn_cracked_plaster", "worn_mossy_plasterwall" })
            {
                ConfigureTextures(id, report, textureFolder);
                var info = new MaterialInfo { name = id, color = Color.white, metallic = 0, smoothness = .65f,
                    diffuse = id + "_diff_1k.jpg", normal = id + "_nor_gl_1k.jpg",
                    metallicSmoothness = "MetallicSmoothness.png", occlusion = "Occlusion.png" };
                string name = id == "worn_cracked_plaster" ? "WornCrackedPlaster" : "WornMossyPlasterwall";
                uniqueMaterials.Add(MakeMaterial(id, 0, info, materialFolder + "/" + name + ".mat", materialFolder, textureFolder));
            }
            report.materials = uniqueMaterials.Count;
            report.prefabs = paths.ToArray(); AssetDatabase.SaveAssets(); return report;
        }

        static void ConfigureTextures(string id, Report report, string textureFolder = Textures)
        {
            string dir = textureFolder + "/" + id;
            if (!Directory.Exists(dir)) return;
            foreach (string filename in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                .Where(p => p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
            {
                string path = filename.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) throw new InvalidDataException("No texture importer: " + path);
                bool normal = path.Contains("_nor_gl_");
                bool mask = path.Contains("MetallicSmoothness") || path.Contains("Occlusion") || path.Contains("_arm_") || path.Contains("AlphaMask");
                bool alpha = path.EndsWith(".png") && (path.Contains("MetallicSmoothness") || path.Contains("Alpha") || path.Contains("BaseColor") || path.Contains("RGBA"));
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.convertToNormalmap = false; importer.sRGBTexture = !normal && !mask;
                importer.mipmapEnabled = true; importer.maxTextureSize = 1024;
                importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 2;
                importer.textureCompression = TextureImporterCompression.Compressed; importer.isReadable = false;
                importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = alpha && !path.Contains("MetallicSmoothness");
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Standalone", overridden = true,
                    maxTextureSize = 1024, compressionQuality = 75, format = normal ? TextureImporterFormat.BC5 : alpha ? TextureImporterFormat.BC7 : TextureImporterFormat.DXT1 });
                importer.SaveAndReimport(); report.textures++; if (normal) report.normalMaps++;
            }
        }

        static Material MakeMaterial(string id, int index, MaterialInfo info, string requestedPath = null,
            string materialFolder = Materials, string textureFolder = Textures)
        {
            string path = requestedPath ?? materialFolder + "/" + id + "-" + index + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.name = requestedPath != null ? Path.GetFileNameWithoutExtension(path) : id + " — " + info.name;
            string dir = textureFolder + "/" + id + "/";
            mat.SetTexture("_BaseMap", Texture(dir, info.diffuse)); mat.SetColor("_BaseColor", info.color);
            mat.SetTexture("_BumpMap", Texture(dir, info.normal)); mat.SetFloat("_BumpScale", .8f);
            mat.SetTexture("_MetallicGlossMap", Texture(dir, info.metallicSmoothness));
            mat.SetTexture("_OcclusionMap", Texture(dir, info.occlusion)); mat.SetFloat("_OcclusionStrength", .88f);
            mat.SetFloat("_Metallic", info.metallic); mat.SetFloat("_Smoothness", info.smoothness * .8f);
            SetKeyword(mat, "_NORMALMAP", !string.IsNullOrEmpty(info.normal));
            SetKeyword(mat, "_METALLICSPECGLOSSMAP", !string.IsNullOrEmpty(info.metallicSmoothness));
            SetKeyword(mat, "_OCCLUSIONMAP", !string.IsNullOrEmpty(info.occlusion));
            mat.DisableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.black);
            mat.SetFloat("_EnvironmentReflections", 0); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.SetFloat("_Cull", info.doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            bool blend = info.alphaMode == "BLEND", clip = info.alphaMode == "MASK";
            mat.SetFloat("_Surface", blend ? 1 : 0); mat.SetFloat("_Blend", 0);
            mat.SetFloat("_AlphaClip", clip ? 1 : 0); mat.SetFloat("_Cutoff", info.alphaCutoff);
            mat.SetFloat("_ZWrite", blend ? 0 : 1);
            mat.SetFloat("_SrcBlend", blend ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            mat.SetFloat("_DstBlend", blend ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); mat.SetFloat("_DstBlendAlpha", blend ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            SetKeyword(mat, "_SURFACE_TYPE_TRANSPARENT", blend); SetKeyword(mat, "_ALPHATEST_ON", clip);
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON"); mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.SetOverrideTag("RenderType", blend ? "Transparent" : clip ? "TransparentCutout" : "Opaque");
            mat.renderQueue = blend ? (int)RenderQueue.Transparent : clip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            mat.SetShaderPassEnabled("ShadowCaster", !blend); mat.enableInstancing = true; mat.doubleSidedGI = info.doubleSided;
            if (id == "wall_clock" && info.name == "wall_clock_glass") ConfigureClockGlass(mat);
            EditorUtility.SetDirty(mat); return mat;
        }

        // glTF transmission=1 is approximated by very clear glass in URP Lit.
        // Preserved specular otherwise turns its translucent face into a white
        // disc under the player's flashlight, obscuring the separate dial/hands.
        static void ConfigureClockGlass(Material material)
        {
            material.SetColor("_BaseColor", new Color(.85f,.85f,.85f,.045f));
            material.SetFloat("_BlendModePreserveSpecular", 0);
            material.SetFloat("_SpecularHighlights", 0);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_Smoothness", .12f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(material);
        }

        public static string RetuneClockGlass()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Retune glass outside Play mode.");
            string path = Materials + "/wall_clock-1.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) throw new InvalidDataException("Imported clock glass missing.");
            ConfigureClockGlass(material); return path;
        }
        static Texture2D Texture(string folder, string filename)
        {
            if (string.IsNullOrEmpty(filename)) return null;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + Path.GetFileName(filename));
            if (!texture) throw new FileNotFoundException("Prepared material map missing", folder + filename); return texture;
        }
        static Renderer[] Meshes(Transform parent, string folder, SchoolReadyAssetPass.MeshPart[] parts, Material[] materials, string level, out int triangles)
        {
            triangles = 0; var result = new List<Renderer>();
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.vertices == null || part.normals.Length != part.vertices.Length || part.uv.Length != part.vertices.Length
                    || part.material < 0 || part.material >= materials.Length || part.triangles.Length % 3 != 0
                    || part.triangles.Any(t => t < 0 || t >= part.vertices.Length)) throw new InvalidDataException("Invalid mesh preparation: " + part.name);
                var mesh = new Mesh { name = part.name, indexFormat = part.vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.vertices = part.vertices; mesh.normals = part.normals; mesh.uv = part.uv; mesh.triangles = part.triangles;
                mesh.RecalculateBounds(); mesh.RecalculateTangents(); triangles += part.triangles.Length / 3;
                string path = folder + "/" + level + "-" + i.ToString("D2") + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); }
                else AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject(level + " — " + part.name); go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = materials[part.material];
                renderer.receiveShadows = true; renderer.shadowCastingMode = materials[part.material].renderQueue >= 3000 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off; result.Add(renderer);
            }
            return result.ToArray();
        }
        static void SetKeyword(Material m, string name, bool value) { if (value) m.EnableKeyword(name); else m.DisableKeyword(name); }
        static void Folder(string path)
        { if (AssetDatabase.IsValidFolder(path)) return; string parent = path.Substring(0, path.LastIndexOf('/')); Folder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1)); }
    }
}
