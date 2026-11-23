// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorGame.Editor
{
    /// <summary>Native Editor integration. No auto-run; the authorized installation pass calls these APIs.</summary>
    public static class VintageCameraSetup
    {
        public const string ShaderPath = "Assets/HorrorGameplay/Shaders/VintageCamera.shader";

        public static VintageCameraEffect Configure(Camera camera, int chapterIndex)
        {
            if (!camera) throw new ArgumentNullException(nameof(camera));
            var effect = camera.GetComponent<VintageCameraEffect>();
            if (!effect) effect = camera.gameObject.AddComponent<VintageCameraEffect>();
            effect.ConfigureForChapter(chapterIndex);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            EditorUtility.SetDirty(effect);
            EditorUtility.SetDirty(camera.GetUniversalAdditionalCameraData());
            return effect;
        }

        public static int EnsureRendererFeatures()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (!shader) throw new InvalidOperationException("Import VintageCamera.shader before configuring the renderer.");
            var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity.ToString() == "Error").ToArray();
            if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors.Select(m => m.message)));
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (!data) continue;
                var existing = data.rendererFeatures.OfType<VintageCameraRendererFeature>().ToArray();
                if (existing.Length > 1) throw new InvalidOperationException("Duplicate vintage renderer features: " + path);
                var feature = existing.FirstOrDefault();
                if (!feature)
                {
                    feature = ScriptableObject.CreateInstance<VintageCameraRendererFeature>();
                    feature.name = "Whispers — analogue chapter image";
                    AssetDatabase.AddObjectToAsset(feature, data);
                    data.rendererFeatures.Add(feature);
                }
                feature.shader = shader;
                feature.SetActive(true);
                feature.Create();
                EditorUtility.SetDirty(feature);
                data.SetDirty();
                EditorUtility.SetDirty(data);
                count++;
            }
            if (count == 0) throw new InvalidOperationException("No UniversalRendererData asset found in Assets.");
            AssetDatabase.SaveAssets();
            return count;
        }

        /// <summary>Image noise/chroma is supplied by the camera pass. Preserve exposure, light and colour grading.</summary>
        public static void ClearDuplicateImageEffects(Volume volume)
        {
            if (!volume || !volume.sharedProfile) return;
            var profile = volume.sharedProfile;
            if (profile.TryGet<FilmGrain>(out var grain))
            {
                grain.intensity.Override(0);
                EditorUtility.SetDirty(grain);
            }
            if (profile.TryGet<ChromaticAberration>(out var chromatic))
            {
                chromatic.intensity.Override(0);
                EditorUtility.SetDirty(chromatic);
            }
            EditorUtility.SetDirty(profile);
        }

        /// <summary>Camera.CopyFrom does not copy MonoBehaviours. Capture helpers call this on temporary cameras.</summary>
        public static void CopyPreviewConfiguration(Camera source, Camera preview)
        {
            if (!source || !preview) return;
            var original = source.GetComponent<VintageCameraEffect>();
            if (!original) return;
            var effect = preview.GetComponent<VintageCameraEffect>();
            if (!effect) effect = preview.gameObject.AddComponent<VintageCameraEffect>();
            EditorUtility.CopySerialized(original, effect);
            effect.ConfigureForChapter(original.chapterIndex);
        }

        public static string[] Validate(Camera camera, int expectedChapter)
        {
            var errors = new List<string>();
            var effect = camera ? camera.GetComponent<VintageCameraEffect>() : null;
            if (!effect) errors.Add("Camera has no VintageCameraEffect.");
            else
            {
                if (effect.chapterIndex != expectedChapter) errors.Add("Incorrect serialized chapter index.");
                bool expected = expectedChapter > 0 && camera.gameObject.scene.name != effect.prologueSceneName;
                if (effect.ChapterAllowed != expected) errors.Add("Chapter image gate failed.");
                if (!expected && effect.EffectiveIntensity > 0) errors.Add("Prologue received the image filter.");
            }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (!shader || !shader.isSupported) errors.Add("Vintage shader unavailable or unsupported.");
            else foreach (var message in ShaderUtil.GetShaderMessages(shader))
                if (message.severity.ToString() == "Error") errors.Add(message.message);
            return errors.ToArray();
        }
    }
}
