// Verifica .

#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace HorrorGame.Tests
{
    public sealed partial class DesktopOfficePlayTests
    {
        // Call after collecting the final report, before pc.Close().
        void AssertFinalSharpnessConfiguration(SharpnessReport report)
        {
            var rt = pc.ScreenTexture;
            Assert.That(new Vector2Int(rt.width, rt.height),
                Is.EqualTo(new Vector2Int(1024, 768)).Or.EqualTo(new Vector2Int(1280, 960)));
            Assert.That(rt.antiAliasing, Is.EqualTo(1));
            Assert.That(rt.useMipMap || rt.autoGenerateMips, Is.False);
            Assert.That(rt.mipmapCount, Is.EqualTo(1));
            Assert.That(rt.anisoLevel, Is.Zero);
            Assert.That(rt.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(rt.sRGB, Is.EqualTo(QualitySettings.activeColorSpace == ColorSpace.Linear));
            Assert.That(rt.filterMode, Is.EqualTo(report.physicalScreen.projectedPixelSize.y+.5f<rt.height?FilterMode.Bilinear:FilterMode.Point));

            var canvas = pc.DesktopRoot.GetComponent<Canvas>();
            var scaler = canvas.GetComponent<CanvasScaler>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(canvas.worldCamera, Is.SameAs(pc.ScreenCamera));
            Assert.That(canvas.scaleFactor, Is.EqualTo(1).Within(.0001f));
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ConstantPixelSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(rt.width, rt.height)));
            Assert.That(canvas.pixelPerfect, Is.True);
            Assert.That(pc.ScreenCamera.orthographic, Is.True);
            Assert.That(pc.ScreenCamera.orthographicSize, Is.EqualTo(rt.height * .5f).Within(.0001f));
            Assert.That(pc.ScreenCamera.pixelWidth, Is.EqualTo(rt.width));
            Assert.That(pc.ScreenCamera.pixelHeight, Is.EqualTo(rt.height));
            Assert.That(pc.ScreenCamera.allowMSAA || pc.ScreenCamera.allowHDR, Is.False);
            var cameraData = pc.ScreenCamera.GetComponent<UniversalAdditionalCameraData>();
            Assert.That(cameraData, Is.Not.Null);
            Assert.That(cameraData.renderPostProcessing, Is.False);
            Assert.That(cameraData.antialiasing, Is.EqualTo(AntialiasingMode.None));

            Assert.That(report.autoSizingTexts, Is.Zero);
            Assert.That(report.extraPaddingTexts, Is.Zero);
            Assert.That(report.fractionalFontSizes, Is.Zero);
            foreach (var font in new[] { pc.content.normalFont, pc.content.childFont })
            {
                Assert.That(font, Is.Not.Null);
                Assert.That(font.atlasTexture.width, Is.GreaterThanOrEqualTo(2048), font.name);
                Assert.That(font.atlasTexture.height, Is.GreaterThanOrEqualTo(2048), font.name);
                Assert.That(font.faceInfo.pointSize, Is.GreaterThanOrEqualTo(72), font.name);
                Assert.That(font.atlasRenderMode.ToString(), Does.Contain("SDF"), font.name);
                if (font.material.HasProperty("_OutlineWidth"))
                    Assert.That(font.material.GetFloat("_OutlineWidth"), Is.Zero, font.name);
            }
            foreach (var sprite in report.sprites)
            {
                Assert.That(sprite.filter, Is.EqualTo(FilterMode.Point.ToString()), sprite.asset);
                Assert.That(sprite.mipmapCount, Is.EqualTo(1), sprite.asset);
                Assert.That(sprite.compression, Is.EqualTo(TextureImporterCompression.Uncompressed.ToString()), sprite.asset);
                Assert.That(sprite.mesh, Is.EqualTo(SpriteMeshType.FullRect.ToString()), sprite.asset);
            }
            Assert.That(pc.crtMaterial.HasProperty("_ChromaticPixels"), Is.True);
            Assert.That(pc.crtMaterial.GetFloat("_ChromaticPixels"), Is.Zero);
            Assert.That(pc.crtMaterial.HasProperty("_Curvature"), Is.True);
            Assert.That(pc.crtMaterial.GetFloat("_Curvature"), Is.InRange(0, .001f));
            float coverage = report.physicalScreen.projectedPixelSize.y / report.worldCamera.height;
            Assert.That(coverage, Is.InRange(.96f, 1f), "The complete screen should occupy 96–100% of seated view height.");
            // Raw rect and natural glyph fractions remain evidence, not failure conditions.
            Assert.That(report.fractionalAdjustedGraphicRects, Is.Zero,
                "The pixel-adjusted outer edges must land on the RT pixel grid.");
        }

        // Optional diagnostic hook in the same test: yield return CaptureFilterComparison();
        // It changes only this runtime RenderTexture, then restores it even if capture fails.
        IEnumerator CaptureFilterComparison()
        {
            var rt = pc.ScreenTexture;
            FilterMode original = rt.filterMode;
            try
            {
                foreach (FilterMode filter in new[] { FilterMode.Point, FilterMode.Bilinear })
                {
                    rt.filterMode = filter;
                    int start = Time.frameCount;
                    yield return null; yield return null;
                    Assert.That(Time.frameCount, Is.GreaterThanOrEqualTo(start + 2));
                    string path = Path.Combine(SharpnessDirectory, "sharpness-filter-" + filter.ToString().ToLowerInvariant() + "-physical.png");
                    if (File.Exists(path)) File.Delete(path);
                    ScreenCapture.CaptureScreenshot(path);
                    yield return Until(() => File.Exists(path) && new FileInfo(path).Length > 0, "physical filter comparison " + filter, 10);
                }
            }
            finally { if (rt) rt.filterMode = original; }
        }
        IEnumerator CaptureFullHdReference()
        {
            var cam=game.player.view.playerCamera;var previous=cam.targetTexture;
            var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32){antiAliasing=1,useMipMap=false,autoGenerateMips=false,anisoLevel=0,wrapMode=TextureWrapMode.Clamp};target.Create();
            try
            {
                cam.targetTexture=target;int start=Time.frameCount;yield return null;yield return null;yield return null;
                Assert.That(Time.frameCount,Is.GreaterThanOrEqualTo(start+3));Assert.That(cam.pixelHeight,Is.EqualTo(1080));Assert.That(pc.ScreenTexture.filterMode,Is.EqualTo(FilterMode.Point));
                var old=RenderTexture.active;var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
                try{RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(SharpnessDirectory,"sharpness-fullhd-reference.png"),pixels.EncodeToPNG());}
                finally{RenderTexture.active=old;UnityEngine.Object.Destroy(pixels);}
            }
            finally{cam.targetTexture=previous;target.Release();UnityEngine.Object.Destroy(target);}
            yield return null;yield return null;
        }
    }
}
#endif
