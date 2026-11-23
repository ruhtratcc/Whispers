// Ferramenta de captura de imagem para revisão.

using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HorrorGame.Editor
{
    /// <summary>Captures actual URP output and uGUI without entering Play or changing saved layout.</summary>
    public static class PresentationCapture
    {
        sealed class CanvasPose
        {
            public Canvas canvas;
            public RenderMode mode;
            public Camera camera;
            public float distance;
        }

        public static void Capture(Scene scene, Camera source, string path, int width = 1600, int height = 900, Action<Camera> inspectRenderedUI = null, bool renderAsGameCamera = false)
        {
            if (!source) throw new InvalidOperationException("Câmera da prévia não encontrada.");
            var poses = new List<CanvasPose>();
            var preview = new GameObject("Presentation capture (temporary)", typeof(Camera));
            SceneManager.MoveGameObjectToScene(preview, scene);
            var previousTarget = RenderTexture.active;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                var camera = preview.GetComponent<Camera>();
                camera.CopyFrom(source);
                VintageCameraSetup.CopyPreviewConfiguration(source, camera);
                camera.enabled = false;
                camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                camera.aspect = (float)width / height;
                camera.rect = new Rect(0, 0, 1, 1);
                camera.nearClipPlane = Mathf.Min(camera.nearClipPlane, .03f);
                camera.ResetWorldToCameraMatrix();
                camera.ResetProjectionMatrix();
                var originalData = source.GetComponent<UniversalAdditionalCameraData>();
                var data = preview.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = !originalData || originalData.renderPostProcessing;
                data.volumeLayerMask = originalData ? originalData.volumeLayerMask : (LayerMask)(-1);
                data.volumeTrigger = source.transform;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                // uGUI derives its screen-space geometry from this camera's pixelRect.
                // Establish the capture target before rebuilding any Canvas or TMP mesh.
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                // A distinctive clear exposes an unrendered target in diagnostic captures.
                RenderTexture.active = target;
                GL.Clear(true, true, Color.magenta);
                RenderTexture.active = previousTarget;
                camera.targetTexture = target;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
                    poses.Add(new CanvasPose { canvas = canvas, mode = canvas.renderMode, camera = canvas.worldCamera, distance = canvas.planeDistance });
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = .35f;
                }
                Canvas.ForceUpdateCanvases();
                foreach (var root in scene.GetRootGameObjects())
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    if (text.gameObject.activeInHierarchy) text.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline)
                {
                    // StandardRequest performs URP's frame setup. Preview also emits the
                    // uGUI geometry for this temporary camera instead of only the main view.
                    var request = new RenderPipeline.StandardRequest { destination = target, isPreview = !renderAsGameCamera };
                    if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("URP sem suporte à captura.");
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else camera.Render();
                inspectRenderedUI?.Invoke(camera);
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                RequireVisiblePixels(pixels, path);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                foreach (var pose in poses)
                {
                    if (!pose.canvas) continue;
                    pose.canvas.renderMode = pose.mode;
                    pose.canvas.worldCamera = pose.camera;
                    pose.canvas.planeDistance = pose.distance;
                }
                RenderTexture.active = previousTarget;
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
                if (target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                UnityEngine.Object.DestroyImmediate(preview);
                Canvas.ForceUpdateCanvases();
            }
        }

        static void RequireVisiblePixels(Texture2D pixels, string path)
        {
            var colors = pixels.GetPixels32();
            int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0;
            var sampledColors = new HashSet<int>();
            // Keep channels separate: saturated but different colors can share max(R,G,B).
            for (int i = 0; i < colors.Length; i++)
            {
                var color = colors[i];
                minR = Mathf.Min(minR, color.r); maxR = Mathf.Max(maxR, color.r);
                minG = Mathf.Min(minG, color.g); maxG = Mathf.Max(maxG, color.g);
                minB = Mathf.Min(minB, color.b); maxB = Mathf.Max(maxB, color.b);
                if (i % 61 == 0) sampledColors.Add((color.r << 16) | (color.g << 8) | color.b);
            }
            int maximum = Mathf.Max(maxR, Mathf.Max(maxG, maxB));
            int variation = Mathf.Max(maxR - minR, Mathf.Max(maxG - minG, maxB - minB));
            if (maximum > 2 && variation >= 2) return;
            const string diagnostics = @"C:\Users\ruhtra\Documents\Codex\2026-09-29\oi\work\PresentationPolish\CaptureDiagnostics";
            Directory.CreateDirectory(diagnostics);
            string failedPath = Path.Combine(diagnostics, Path.GetFileNameWithoutExtension(path) + ".failed.png");
            File.WriteAllBytes(failedPath, pixels.EncodeToPNG());
            throw new InvalidOperationException("A captura nativa não produziu conteúdo visível (R " + minR + "–" + maxR
                + ", G " + minG + "–" + maxG + ", B " + minB + "–" + maxB + "; cores amostradas: " + sampledColors.Count
                + "). Diagnóstico: " + failedPath + ". Destino: " + path);
        }
    }
}
