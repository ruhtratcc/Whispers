// Verifica .

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HorrorGame.Tests
{
    public sealed partial class DesktopOfficePlayTests
    {
        const string SharpnessDirectory = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/PCGames20261029/Sharpness";

        [Serializable] sealed class SharpnessReport
        {
            public string utc, colorSpace, graphicsApi;
            public int frameStart, frameEnd;
            public RenderTargetEvidence renderTarget;
            public CameraEvidence screenCamera, worldCamera;
            public CanvasEvidence canvas;
            public WorldScreenEvidence physicalScreen;
            public int activeRects, fractionalRectCount, adjustedGraphicRects, fractionalAdjustedGraphicRects, graphicsVertices, fractionalGraphicVertices;
            public int textVertices, fractionalTextVertices, autoSizingTexts, extraPaddingTexts, fractionalFontSizes, shadows;
            public RectEvidence[] fractionalRectSamples, fractionalAdjustedGraphicSamples;
            public FontEvidence[] fonts;
            public SpriteEvidence[] sprites;
            public float crtIntensity, renderScale;
            public int pipelineMsaa;
            public string upscalingFilter;
            public bool personalSaveAccessed;
        }
        [Serializable] sealed class RenderTargetEvidence
        {
            public int width, height, aa, aniso, mipmapCount;
            public bool useMipMap, autoGenerateMips, sRGB;
            public string filter, wrap, graphicsFormat;
        }
        [Serializable] sealed class CameraEvidence
        {
            public string name, aa;
            public bool orthographic, hdr, msaa, postProcessing;
            public float orthographicSize, fov, aspect;
            public int width, height;
            public Vector4 pixelRect;
        }
        [Serializable] sealed class CanvasEvidence
        {
            public string mode, scalerMode;
            public float scaleFactor, referencePixelsPerUnit;
            public bool pixelPerfect;
            public Vector2 size, referenceResolution;
            public Vector3 localScale, lossyScale;
        }
        [Serializable] sealed class WorldScreenEvidence
        {
            public Vector2 lowerLeft, upperRight, projectedPixelSize;
            public float sourcePixelsPerScreenPixelX, sourcePixelsPerScreenPixelY, fractionOfOutputArea;
            public Vector3 seatPosition, surfacePosition;
        }
        [Serializable] sealed class RectEvidence
        {
            public string path;
            public Vector2 size, anchoredPosition, pivot, projectedLowerLeft, projectedUpperRight;
        }
        [Serializable] sealed class FontEvidence
        {
            public string name, renderMode, populationMode, filter, shader;
            public int atlasWidth, atlasHeight, padding;
            public float samplingPointSize, outlineWidth;
        }
        [Serializable] sealed class SpriteEvidence
        {
            public string name, asset, filter, wrap, compression, mesh;
            public Vector2 size;
            public float pixelsPerUnit;
            public int mipmapCount, aniso;
        }

        [UnityTest, Category("CRTSharpness"), Timeout(120000)]
        public IEnumerator DesktopSharpnessRecordsTheRealRenderChainAndCurrentImages()
        {
            yield return Enter();
            yield return ReadMail("email.07");
            Canvas.ForceUpdateCanvases();
            foreach (var text in pc.DesktopRoot.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            int frameStart = Time.frameCount;
            yield return null; yield return null;
            Assert.That(Time.frameCount, Is.GreaterThanOrEqualTo(frameStart + 2), "The proof must use advancing Play frames.");

            var texture = pc.ScreenTexture;
            var canvas = pc.DesktopRoot.GetComponent<Canvas>();
            var scaler = canvas.GetComponent<CanvasScaler>();
            var worldCamera = game.player.view.playerCamera;
            var rects = pc.DesktopRoot.GetComponentsInChildren<RectTransform>().Where(r => r.gameObject.activeInHierarchy).ToArray();
            var fractional = rects.Select(RectInfo).Where(FractionalRect).ToArray();
            var adjusted = pc.DesktopRoot.GetComponentsInChildren<Graphic>()
                .Where(g => g.gameObject.activeInHierarchy && !(g is TMP_Text)).Select(AdjustedGraphicInfo).ToArray();
            var texts = pc.DesktopRoot.GetComponentsInChildren<TMP_Text>().Where(t => t.gameObject.activeInHierarchy).ToArray();
            var pipeline = UniversalRenderPipeline.asset;
            int graphicVertices = 0, fractionalGraphicVertices = 0, textVertices = 0, fractionalTextVertices = 0;
            {
                foreach (var graphic in pc.DesktopRoot.GetComponentsInChildren<Graphic>().Where(g => g.gameObject.activeInHierarchy))
                {
                    var mesh=graphic.canvasRenderer.GetMesh();if(!mesh)continue;
                    bool isText = graphic is TMP_Text;
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        Vector2 point = RectTransformUtility.WorldToScreenPoint(pc.ScreenCamera, graphic.transform.TransformPoint(vertex));
                        bool fractionalVertex = !Integral(point.x) || !Integral(point.y);
                        if (isText) { textVertices++; if (fractionalVertex) fractionalTextVertices++; }
                        else { graphicVertices++; if (fractionalVertex) fractionalGraphicVertices++; }
                    }
                }
            }

            var report = new SharpnessReport
            {
                utc = DateTime.UtcNow.ToString("O"), colorSpace = QualitySettings.activeColorSpace.ToString(),
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), frameStart = frameStart, frameEnd = Time.frameCount,
                renderTarget = new RenderTargetEvidence { width = texture.width, height = texture.height, aa = texture.antiAliasing,
                    aniso = texture.anisoLevel, mipmapCount = texture.mipmapCount, useMipMap = texture.useMipMap,
                    autoGenerateMips = texture.autoGenerateMips, sRGB = texture.sRGB, filter = texture.filterMode.ToString(),
                    wrap = texture.wrapMode.ToString(), graphicsFormat = texture.graphicsFormat.ToString() },
                screenCamera = CameraInfo(pc.ScreenCamera), worldCamera = CameraInfo(worldCamera),
                canvas = new CanvasEvidence { mode = canvas.renderMode.ToString(), scalerMode = scaler.uiScaleMode.ToString(),
                    scaleFactor = canvas.scaleFactor, referencePixelsPerUnit = canvas.referencePixelsPerUnit,
                    pixelPerfect = canvas.pixelPerfect, size = pc.DesktopRoot.rect.size, referenceResolution = scaler.referenceResolution,
                    localScale = pc.DesktopRoot.localScale, lossyScale = pc.DesktopRoot.lossyScale },
                physicalScreen = PhysicalScreenInfo(worldCamera), activeRects = rects.Length,
                fractionalRectCount = fractional.Length, fractionalRectSamples = fractional.Take(30).ToArray(),
                adjustedGraphicRects = adjusted.Length, fractionalAdjustedGraphicRects = adjusted.Count(FractionalRect),
                fractionalAdjustedGraphicSamples = adjusted.Where(FractionalRect).Take(30).ToArray(),
                graphicsVertices = graphicVertices, fractionalGraphicVertices = fractionalGraphicVertices,
                textVertices = textVertices, fractionalTextVertices = fractionalTextVertices,
                autoSizingTexts = texts.Count(t => t.enableAutoSizing), extraPaddingTexts = texts.Count(t => t.extraPadding),
                fractionalFontSizes = texts.Count(t => !Integral(t.fontSize)),
                shadows = pc.DesktopRoot.GetComponentsInChildren<Shadow>().Count(s => s.isActiveAndEnabled),
                fonts = texts.Select(t => t.font).Where(f => f).Distinct().Select(FontInfo).ToArray(),
                sprites = pc.DesktopRoot.GetComponentsInChildren<Image>().Where(i => i.sprite).Select(i => i.sprite).Distinct().Select(SpriteInfo).ToArray(),
                crtIntensity = pc.crtMaterial.GetFloat("_Intensity"), renderScale = pipeline ? pipeline.renderScale : 1,
                pipelineMsaa = pipeline ? pipeline.msaaSampleCount : 1, upscalingFilter = pipeline ? pipeline.upscalingFilter.ToString() : "Unknown",
                personalSaveAccessed = false
            };
            Directory.CreateDirectory(SharpnessDirectory);
            File.WriteAllText(Path.Combine(SharpnessDirectory, "sharpness-current.json"), JsonUtility.ToJson(report, true));
            yield return SharpnessCapture();
            Assert.That(texture.width * 3, Is.EqualTo(texture.height * 4));
            Assert.That(texture.antiAliasing, Is.EqualTo(1));
            Assert.That(canvas.worldCamera, Is.SameAs(pc.ScreenCamera));
            Assert.That(canvas.scaleFactor, Is.EqualTo(1).Within(.0001f));
            Assert.That(pc.ScreenCamera.orthographic, Is.True);
            Assert.That(pc.ScreenCamera.allowMSAA, Is.False);
            AssertFinalSharpnessConfiguration(report);
            yield return CaptureFilterComparison();
            yield return CaptureFullHdReference();
            Assert.That(game.hud.objectiveGroup.gameObject.activeSelf,Is.False,"The world objective must not cover the CRT.");
            pc.Close(); yield return Until(() => !pc.IsSeated && game.CanControl, "sharpness inspection return");
            Assert.That(pc.ScreenCamera.enabled, Is.False);
            Assert.That(game.hud.objectiveGroup.gameObject.activeSelf,Is.True);
            Assert.That(game.player.view.enabled,Is.True);Assert.That(game.player.view.playerCamera.fieldOfView,Is.EqualTo(49).Within(.02f));
        }

        CameraEvidence CameraInfo(Camera camera)
        {
            var additional = camera.GetComponent<UniversalAdditionalCameraData>();
            return new CameraEvidence { name = camera.name, orthographic = camera.orthographic, orthographicSize = camera.orthographicSize,
                fov = camera.fieldOfView, aspect = camera.aspect, width = camera.pixelWidth, height = camera.pixelHeight,
                hdr = camera.allowHDR, msaa = camera.allowMSAA, postProcessing = additional && additional.renderPostProcessing,
                aa = additional ? additional.antialiasing.ToString() : "None", pixelRect = new Vector4(camera.pixelRect.x, camera.pixelRect.y, camera.pixelRect.width, camera.pixelRect.height) };
        }

        RectEvidence RectInfo(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return new RectEvidence { path = SharpPath(rect), size = rect.rect.size, anchoredPosition = rect.anchoredPosition, pivot = rect.pivot,
                projectedLowerLeft = RectTransformUtility.WorldToScreenPoint(pc.ScreenCamera, corners[0]),
                projectedUpperRight = RectTransformUtility.WorldToScreenPoint(pc.ScreenCamera, corners[2]) };
        }
        static bool FractionalRect(RectEvidence item) => !Integral(item.projectedLowerLeft.x) || !Integral(item.projectedLowerLeft.y)
            || !Integral(item.projectedUpperRight.x) || !Integral(item.projectedUpperRight.y);
        static bool Integral(float value) => Mathf.Abs(value - Mathf.Round(value)) <= .025f;
        static string SharpPath(Transform item) => item.parent ? SharpPath(item.parent) + "/" + item.name : item.name;

        RectEvidence AdjustedGraphicInfo(Graphic graphic)
        {
            var r = graphic.GetPixelAdjustedRect();
            return new RectEvidence { path = SharpPath(graphic.transform), size = r.size,
                anchoredPosition = graphic.rectTransform.anchoredPosition, pivot = graphic.rectTransform.pivot,
                projectedLowerLeft = RectTransformUtility.WorldToScreenPoint(pc.ScreenCamera,
                    graphic.transform.TransformPoint(new Vector3(r.xMin, r.yMin, 0))),
                projectedUpperRight = RectTransformUtility.WorldToScreenPoint(pc.ScreenCamera,
                    graphic.transform.TransformPoint(new Vector3(r.xMax, r.yMax, 0))) };
        }

        WorldScreenEvidence PhysicalScreenInfo(Camera camera)
        {
            var mesh = pc.screenSurface.GetComponent<MeshFilter>().sharedMesh;
            var bounds = mesh.bounds;
            var points = new List<Vector2>();
            foreach (float x in new[] { bounds.min.x, bounds.max.x })
                foreach (float y in new[] { bounds.min.y, bounds.max.y })
                    points.Add(camera.WorldToScreenPoint(pc.screenSurface.TransformPoint(new Vector3(x, y, bounds.center.z))));
            var min = new Vector2(points.Min(p => p.x), points.Min(p => p.y));
            var max = new Vector2(points.Max(p => p.x), points.Max(p => p.y));
            Vector2 size = max - min;
            return new WorldScreenEvidence { lowerLeft = min, upperRight = max, projectedPixelSize = size,
                sourcePixelsPerScreenPixelX = pc.ScreenTexture.width / Mathf.Max(1, size.x),
                sourcePixelsPerScreenPixelY = pc.ScreenTexture.height / Mathf.Max(1, size.y),
                fractionOfOutputArea = size.x * size.y / Mathf.Max(1, camera.pixelWidth * camera.pixelHeight),
                seatPosition = pc.seatCameraPose.position, surfacePosition = pc.screenSurface.position };
        }

        static FontEvidence FontInfo(TMP_FontAsset font)
        {
            var texture = font.atlasTexture;
            return new FontEvidence { name = font.name, renderMode = font.atlasRenderMode.ToString(), populationMode = font.atlasPopulationMode.ToString(),
                filter = texture ? texture.filterMode.ToString() : "None", atlasWidth = texture ? texture.width : 0,
                atlasHeight = texture ? texture.height : 0, samplingPointSize = font.faceInfo.pointSize, padding = font.atlasPadding,
                shader = font.material && font.material.shader ? font.material.shader.name : "None",
                outlineWidth = font.material && font.material.HasProperty("_OutlineWidth") ? font.material.GetFloat("_OutlineWidth") : 0 };
        }

        static SpriteEvidence SpriteInfo(Sprite sprite)
        {
            var texture = sprite.texture;
            string path = AssetDatabase.GetAssetPath(texture); var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var settings = new TextureImporterSettings(); if (importer) importer.ReadTextureSettings(settings);
            return new SpriteEvidence { name = sprite.name, asset = path, size = sprite.rect.size, pixelsPerUnit = sprite.pixelsPerUnit,
                filter = texture.filterMode.ToString(), wrap = texture.wrapMode.ToString(), mipmapCount = texture.mipmapCount, aniso = texture.anisoLevel,
                compression = importer ? importer.textureCompression.ToString() : "Runtime", mesh = importer ? settings.spriteMeshType.ToString() : "Runtime" };
        }

        IEnumerator SharpnessCapture()
        {
            yield return null; yield return null;
            var old = RenderTexture.active;
            var image = new Texture2D(pc.ScreenTexture.width, pc.ScreenTexture.height, TextureFormat.RGB24, false);
            try { RenderTexture.active = pc.ScreenTexture; image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(SharpnessDirectory, "sharpness-current-desktop.png"), image.EncodeToPNG()); }
            finally { RenderTexture.active = old; UnityEngine.Object.Destroy(image); }
            string path = Path.Combine(SharpnessDirectory, "sharpness-current-physical.png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            yield return Until(() => File.Exists(path) && new FileInfo(path).Length > 0, "sharpness physical frame", 10);
        }
    }
}
#endif
