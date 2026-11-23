// Ferramenta de preparação ou verificação usada no Editor.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>Offline preparation only. Never replaces scene objects or changes a source image.</summary>
    public static class ModelReplacementTools
    {
        public const string Incoming = "Assets/ThirdParty/Sketchfab_Incoming";

        [Serializable] public sealed class PackedMaps
        {
            public string metallicSmoothness, occlusion, normal;
        }

        [Serializable] public sealed class IncomingReport
        {
            public string[] configured, skipped;
            public string note = "Only model import settings changed. No scene integration, license registration or material replacement was performed.";
        }

        /// <summary>ARM = R:AO, G:roughness, B:metal. URP = R:metal, A:smoothness.</summary>
        public static PackedMaps PackArm(string sourcePath, string outputFolder,
            string normalPath = null, bool normalIsDirectX = false, int maxTextureSize = 1024)
        {
            string output = AssetPath(outputFolder);
            string source = ImagePath(sourcePath);
            string normalSource = string.IsNullOrWhiteSpace(normalPath) ? null : ImagePath(normalPath);
            if (normalIsDirectX && normalSource == null)
                throw new ArgumentException("DirectX conversion requires a normal-map source.");
            if (maxTextureSize != 1024 && maxTextureSize != 2048)
                throw new ArgumentOutOfRangeException(nameof(maxTextureSize), "Use 1K, or 2K for a close-up asset.");
            var result = new PackedMaps {
                metallicSmoothness = output + "/MetallicSmoothness.png",
                occlusion = output + "/Occlusion.png",
                normal = normalSource == null ? null : output + "/Normal_nor_gl.png"
            };
            var paths = new[] { result.metallicSmoothness, result.occlusion, result.normal }.Where(p => p != null).ToArray();
            // All validation and image decoding precede the first output write.
            foreach (string target in paths)
            {
                string full = Absolute(target);
                if (File.Exists(full) || string.Equals(full, source, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(full, normalSource, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Refusing to overwrite a source or existing derivative: " + target);
            }
            Texture2D arm = null, nrm = null;
            byte[] metalBytes, aoBytes, normalBytes = null;
            try
            {
                arm = Decode(source);
                var pixels = arm.GetPixels32();
                var metallic = new Color32[pixels.Length];
                var ao = new Color32[pixels.Length];
                for (int i = 0; i < pixels.Length; i++)
                {
                    var p = pixels[i];
                    metallic[i] = new Color32(p.b, 0, 0, (byte)(255 - p.g));
                    ao[i] = new Color32(p.r, p.r, p.r, 255);
                }
                metalBytes = Encode(metallic, arm.width, arm.height);
                aoBytes = Encode(ao, arm.width, arm.height);
                if (normalSource != null)
                {
                    nrm = Decode(normalSource);
                    var np = nrm.GetPixels32();
                    if (normalIsDirectX)
                        for (int i = 0; i < np.Length; i++) np[i].g = (byte)(255 - np[i].g);
                    normalBytes = Encode(np, nrm.width, nrm.height);
                }
            }
            finally
            {
                if (arm) UnityEngine.Object.DestroyImmediate(arm);
                if (nrm) UnityEngine.Object.DestroyImmediate(nrm);
            }
            Directory.CreateDirectory(Absolute(output));
            var written = new List<string>();
            try
            {
                File.WriteAllBytes(Absolute(result.metallicSmoothness), metalBytes); written.Add(result.metallicSmoothness);
                File.WriteAllBytes(Absolute(result.occlusion), aoBytes); written.Add(result.occlusion);
                if (normalBytes != null) { File.WriteAllBytes(Absolute(result.normal), normalBytes); written.Add(result.normal); }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (string target in written) ConfigureTexture(target, target == result.normal, maxTextureSize);
            }
            catch
            {
                foreach (string target in written)
                {
                    if (!AssetDatabase.DeleteAsset(target) && File.Exists(Absolute(target))) File.Delete(Absolute(target));
                }
                throw;
            }
            return result;
        }

        public static Material CreateUrpMaterial(string materialPath, PackedMaps maps, string baseColorAssetPath = null)
        {
            materialPath = AssetPath(materialPath);
            if (!materialPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) || maps == null)
                throw new ArgumentException("Use a new .mat path and packed maps.");
            if (File.Exists(Absolute(materialPath))) throw new IOException("Existing material preserved: " + materialPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit is not available.");
            var metallic = RequiredTexture(maps.metallicSmoothness);
            var occlusion = RequiredTexture(maps.occlusion);
            var normal = string.IsNullOrEmpty(maps.normal) ? null : RequiredTexture(maps.normal);
            var diffuse = string.IsNullOrEmpty(baseColorAssetPath) ? null : RequiredTexture(baseColorAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(Absolute(materialPath)));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(materialPath), enableInstancing = true };
            mat.SetTexture("_BaseMap", diffuse); mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_MetallicGlossMap", metallic); mat.SetTexture("_OcclusionMap", occlusion);
            mat.SetTexture("_BumpMap", normal); mat.SetFloat("_Metallic", 1); mat.SetFloat("_Smoothness", 1);
            mat.SetFloat("_OcclusionStrength", 1); mat.SetFloat("_BumpScale", 1);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.EnableKeyword("_OCCLUSIONMAP");
            if (normal) mat.EnableKeyword("_NORMALMAP"); else mat.DisableKeyword("_NORMALMAP");
            mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            mat.DisableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.black);
            AssetDatabase.CreateAsset(mat, materialPath);
            return mat;
        }

        /// <summary>Configure only licensed FBX/OBJ/DAE incoming assets; GLB/glTF need an explicit importer.</summary>
        public static IncomingReport ConfigureIncoming(string incomingRoot = Incoming)
        {
            string root = AssetPath(incomingRoot);
            if (root != Incoming && !root.StartsWith(Incoming + "/", StringComparison.Ordinal))
                throw new ArgumentException("Only Sketchfab_Incoming may be configured.");
            if (!Directory.Exists(Absolute(root)))
                return new IncomingReport { configured = Array.Empty<string>(), skipped = Array.Empty<string>() };
            var configured = new List<string>(); var skipped = new List<string>();
            foreach (string full in Directory.GetFiles(Absolute(root), "*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(full).ToLowerInvariant();
                if (!new[] { ".fbx", ".obj", ".dae", ".gltf", ".glb", ".blend" }.Contains(ext)) continue;
                string asset = ToAssetPath(full);
                string tail = asset.Substring(Incoming.Length + 1);
                int slash = tail.IndexOf('/');
                if (slash <= 0) { skipped.Add(asset + " — put each object in its own named folder with license/source."); continue; }
                string objectRoot = Incoming + "/" + tail.Substring(0, slash);
                if (!HasClearLicense(objectRoot)) { skipped.Add(asset + " — missing/unsupported license; supply CC0 or CC-BY source/license."); continue; }
                if (ext == ".gltf" || ext == ".glb" || ext == ".blend")
                {
                    skipped.Add(asset + " — no supported automatic conversion; preserve original and prepare native FBX/OBJ or static glTF conversion separately.");
                    continue;
                }
                var importer = AssetImporter.GetAtPath(asset) as ModelImporter;
                if (!importer) { skipped.Add(asset + " — Unity ModelImporter unavailable."); continue; }
                importer.globalScale = 1; importer.useFileScale = true;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.addCollider = false; importer.isReadable = false;
                importer.importCameras = false; importer.importLights = false;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.SaveAndReimport(); configured.Add(asset);
            }
            return new IncomingReport { configured = configured.ToArray(), skipped = skipped.ToArray() };
        }

        [MenuItem("Tools/Whispers of the Past/Modelos/Empacotar ARM selecionado")]
        static void PackSelected()
        {
            string source = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(source)) { Debug.LogWarning("Select an ARM texture asset first."); return; }
            string chosen = EditorUtility.OpenFolderPanel("Pasta nova para os mapas URP", Application.dataPath, "");
            if (string.IsNullOrEmpty(chosen)) return;
            try { Debug.Log(JsonUtility.ToJson(PackArm(source, ToAssetPath(chosen)), true)); }
            catch (Exception error) { Debug.LogError(error.Message); }
        }

        [MenuItem("Tools/Whispers of the Past/Modelos/Configurar Sketchfab Incoming")]
        static void ConfigureMenu() => Debug.Log(JsonUtility.ToJson(ConfigureIncoming(), true));

        static bool HasClearLicense(string objectRoot)
        {
            foreach (string path in Directory.GetFiles(Absolute(objectRoot), "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(path).ToLowerInvariant();
                if (!(name.StartsWith("license") || name.StartsWith("licence") || name.StartsWith("source.") || name.StartsWith("provenance."))) continue;
                if (new FileInfo(path).Length > 1024 * 1024) continue;
                string text = File.ReadAllText(path);
                if (Regex.IsMatch(text, @"CC[\s-]*BY[\s-]*(?:NC|ND)|non.?commercial|no.?derivatives|licenses/by-(?:nc|nd)", RegexOptions.IgnoreCase)) continue;
                if (Regex.IsMatch(text, @"\bCC0\b|creativecommons\.org/publicdomain/zero|creativecommons\.org/licenses/by/[34]\.0|\bCC[\s-]*BY(?![\s-]*(?:NC|ND|SA))\b|Creative Commons Attribution (?:3|4)\.0", RegexOptions.IgnoreCase)) return true;
            }
            return false;
        }

        static string ImagePath(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Missing image source.");
            string full = Path.GetFullPath(Path.IsPathRooted(source) ? source : Absolute(source));
            string ext = Path.GetExtension(full).ToLowerInvariant();
            if (!File.Exists(full) || !new[] { ".png", ".jpg", ".jpeg" }.Contains(ext))
                throw new ArgumentException("Image source must be an existing PNG/JPEG: " + source);
            return full;
        }

        static string AssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Missing Assets path.");
            path = path.Replace('\\', '/').TrimEnd('/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Split('/').Any(p => p == ".." || p == "."))
                throw new ArgumentException("Use a project-relative path strictly inside Assets.");
            string full = Path.GetFullPath(Absolute(path));
            if (!full.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output must stay inside Assets.");
            return path;
        }

        static string Absolute(string asset) => Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, asset));
        static string ToAssetPath(string full)
        {
            full = Path.GetFullPath(full);
            string assets = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(assets, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose a folder inside Assets.");
            return "Assets/" + full.Substring(assets.Length).Replace('\\', '/');
        }
        static Texture2D Decode(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
            { UnityEngine.Object.DestroyImmediate(texture); throw new InvalidDataException("Could not decode image: " + path); }
            return texture;
        }
        static byte[] Encode(Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try { texture.SetPixels32(pixels); texture.Apply(false, false); return ImageConversion.EncodeToPNG(texture); }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        static void ConfigureTexture(string path, bool normal, int maxSize)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidDataException("No texture importer for " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.convertToNormalmap = false; importer.sRGBTexture = false;
            importer.alphaIsTransparency = false; importer.mipmapEnabled = true;
            importer.isReadable = false; importer.maxTextureSize = maxSize;
            importer.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
        static Texture2D RequiredTexture(string path)
        {
            path = AssetPath(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture) throw new FileNotFoundException("Prepared texture not imported: " + path);
            return texture;
        }
    }
}
#endif
