// Verifica SchoolSignalChangesRenderedColourWithoutFoggingBlackOrBlurringDetail, RestoreTemporaryScene.

using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("GameplayPolish")]
    public sealed class VintageSignalRenderPlayTests
    {
        const int Width = 640, Height = 360;
        Scene temporaryScene;

        [UnityTest]
        public IEnumerator SchoolSignalChangesRenderedColourWithoutFoggingBlackOrBlurringDetail()
        {
            var previousScene = SceneManager.GetActiveScene();
            var previousTarget = RenderTexture.active;
            var pipeline = UniversalRenderPipeline.asset;
            Assert.IsNotNull(pipeline, "This proof requires the project's real URP pipeline.");
            var previousResident = pipeline.gpuResidentDrawerMode;
            GameObject root = null;
            Material material = null;
            Texture2D chart = null, clean = null, treated = null, distressed = null;
            RenderTexture target = null;
            VolumeProfile profile = null;
            float previousScale = Time.timeScale;
            try
            {
                var school = SceneManager.GetSceneByName("SchoolCEEP");
                if (!school.IsValid() || !school.isLoaded)
                    school = temporaryScene = SceneManager.CreateScene("SchoolCEEP");
                SceneManager.SetActiveScene(school);
                Time.timeScale = 1;
                pipeline.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
                root = new GameObject("Generated signal chart (temporary)");
                root.transform.position = new Vector3(10000, 10000, 10000);
                var cameraObject = new GameObject("Signal chart game camera");
                cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 1;
                camera.aspect = (float)Width / Height;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 10;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.volumeLayerMask = 1 << 31;
                data.volumeTrigger = camera.transform;
                data.antialiasing = AntialiasingMode.None;
                var effect = cameraObject.AddComponent<VintageCameraEffect>();
                effect.ConfigureSchoolTreatment();
                effect.ConfigureForChapter(2);
                Assert.IsTrue(effect.HorrorTreatmentAllowed);

                var volumeObject = new GameObject("Signal chart post-processing");
                volumeObject.transform.SetParent(root.transform, false);
                volumeObject.layer = 31;
                var volume = volumeObject.AddComponent<Volume>();
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                volume.sharedProfile = profile;
                volume.isGlobal = true;
                volume.priority = 10000;
                var grade = profile.Add<ColorAdjustments>(true);
                grade.postExposure.Override(.2f);
                var tonemapping = profile.Add<Tonemapping>(true);
                tonemapping.mode.Override(TonemappingMode.Neutral);

                chart = MakeChart();
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                Assert.IsNotNull(shader);
                material = new Material(shader);
                material.SetTexture("_BaseMap", chart);
                material.SetColor("_BaseColor", Color.white);
                var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                panel.name = "Our procedural colour and luminance chart";
                panel.transform.SetParent(root.transform, false);
                panel.transform.localPosition = new Vector3(0, 0, 5);
                panel.transform.localScale = new Vector3(camera.aspect * 2, 2, 1);
                panel.layer = 31;
                panel.GetComponent<MeshRenderer>().sharedMaterial = material;
                Object.Destroy(panel.GetComponent<Collider>());
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                effect.enabled = false;
                clean = Capture(camera, target);
                effect.enabled = true;
                effect.ConfigureForChapter(2);
                // Same signal clock and chart; only the effect is toggled between these actual URP renders.
                treated = Capture(camera, target);
                SaveDiagnostic(clean, "Signal-chart-clean.png");
                SaveDiagnostic(treated, "Signal-chart-treated.png");
                var baseline = clean.GetPixels32();
                var result = treated.GetPixels32();
                float baselineBlack = MeanLuma(baseline, 10, 40, 30, 330);
                float resultBlack = MeanLuma(result, 10, 40, 30, 330);
                float baselineWhite = MeanLuma(baseline, 500, 540, 140, 220);
                Assert.Less(baselineBlack, .02f, "The generated black patch must render as black.");
                Assert.Greater(baselineWhite, .5f, "The chart must actually be visible, not an unrendered target.");
                float change = MeanDifference(baseline, result, 180, 470, 140, 220);
                Assert.Greater(change, .008f,
                    "The real output must change by more than two 8-bit colour codes on average in midtones/colour bars.");
                Assert.Less(resultBlack, .02f, "The analogue treatment must not turn black into a grey fog overlay.");
                float cleanDetail = EdgeContrast(baseline);
                float signalDetail = EdgeContrast(result);
                Assert.Greater(cleanDetail, .1f, "The chart's four-pixel luminance bars must render sharply.");
                Assert.Greater(signalDetail, cleanDetail * .65f,
                    "At least 65% of the luminance edge contrast must survive so doorway/clue detail remains readable.");
                TestContext.WriteLine("GPU chart: mean colour change=" + change + "; black=" + resultBlack
                    + "; detail contrast=" + signalDetail + " (clean=" + cleanDetail + ").");
                effect.SetDarknessDistortion(1);
                distressed = Capture(camera, target);
                SaveDiagnostic(distressed, "Signal-chart-darkness.png");
                Assert.That(MeanDifference(result, distressed.GetPixels32(), 80, 560, 40, 320), Is.GreaterThan(.012f),
                    "Exhausted darkness must visibly distort the real rendered scene.");
                effect.SetDarknessDistortion(0);
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (root) Object.Destroy(root);
                if (material) Object.Destroy(material);
                if (chart) Object.Destroy(chart);
                if (clean) Object.Destroy(clean);
                if (treated) Object.Destroy(treated);
                if (distressed) Object.Destroy(distressed);
                if (profile)
                {
                    foreach (var component in profile.components) if (component) Object.Destroy(component);
                    Object.Destroy(profile);
                }
                if (target) { target.Release(); Object.Destroy(target); }
                pipeline.gpuResidentDrawerMode = previousResident;
                Time.timeScale = previousScale;
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            }
            yield break;
        }

        [UnityTearDown]
        public IEnumerator RestoreTemporaryScene()
        {
            // TearDown runs even when a pixel assertion fails inside the iterator's try/finally.
            if (temporaryScene.IsValid() && temporaryScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(temporaryScene);
            temporaryScene = default;
        }

        static Texture2D MakeChart()
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] bars = { Color.black, new Color(.04f, .04f, .04f), new Color(.25f, .25f, .25f),
                new Color(.5f, .5f, .5f), new Color(.7f, .38f, .18f), new Color(.18f, .55f, .68f),
                new Color(.9f, .9f, .9f), new Color(.68f, .2f, .35f) };
            var pixels = new Color32[Width * Height];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var color = bars[x / 80];
                // Symmetric top/bottom detail zones make this independent of render-target UV orientation.
                if (x >= 160 && (y < 65 || y >= Height - 65))
                    color = (x / 4) % 2 == 0 ? new Color(.85f, .85f, .85f) : Color.black;
                pixels[y * Width + x] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        static Texture2D Capture(Camera camera, RenderTexture target)
        {
            var request = new RenderPipeline.StandardRequest { destination = target, isPreview = false };
            Assert.IsTrue(RenderPipeline.SupportsRenderRequest(camera, request));
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            pixels.Apply();
            return pixels;
        }

        static float Luma(Color32 pixel) => (.2126f * pixel.r + .7152f * pixel.g + .0722f * pixel.b) / 255;

        static float MeanLuma(Color32[] pixels, int x0, int x1, int y0, int y1)
        {
            float total = 0;
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) total += Luma(pixels[y * Width + x]);
            return total / ((x1 - x0) * (y1 - y0));
        }

        static float MeanDifference(Color32[] before, Color32[] after, int x0, int x1, int y0, int y1)
        {
            float total = 0;
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
            {
                var a = before[y * Width + x]; var b = after[y * Width + x];
                total += (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)) / (255f * 3);
            }
            return total / ((x1 - x0) * (y1 - y0));
        }

        static float EdgeContrast(Color32[] pixels)
        {
            float total = 0;
            for (int y = 30; y < 55; y++)
            for (int x = 180; x < 600; x++)
                total += Mathf.Abs(Luma(pixels[y * Width + x]) - Luma(pixels[y * Width + x - 1]));
            return total / (25 * 420);
        }

        static void SaveDiagnostic(Texture2D image, string file)
        {
            const string folder = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/ReferenceGame20261022/Visual";
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, file), image.EncodeToPNG());
        }
    }
}
