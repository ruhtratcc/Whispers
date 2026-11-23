// Gera a apresentação do efeito analógico no menu.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SchoolMenu
{
    public sealed class AnalogSignal : MonoBehaviour
    {
        [Header("Recorded image")]
        [SerializeField] public RawImage noiseLayer, scanlines, bandStatic;
        [SerializeField] public CanvasGroup trackingBand;
        [SerializeField] public RectTransform bandTransform;
        [SerializeField] public Volume filmVolume;
        [SerializeField, Range(0, .15f)] float noiseOpacity = .008f;
        [SerializeField, Range(0, .15f)] float scanlineOpacity = .05f;
        [SerializeField] float noiseRate = 12f;
        [Header("Rolling tracking line")]
        [SerializeField] Vector2 firstSweepDelay = new Vector2(7f, 11f);
        [SerializeField] Vector2 disturbanceInterval = new Vector2(11f, 18f);
        [SerializeField, Min(.1f)] float disturbanceDuration = 1.65f;
        [SerializeField, Range(0, 1)] float trackingOpacity = .30f;
        [SerializeField, Range(0, 1)] float chromaticBaseline = .055f;
        [SerializeField] float scanlineDrift = .006f;
        [Header("Something in the corridor")]
        [SerializeField] public Camera menuCamera;
        [SerializeField] public GameObject distantEyes;
        Texture2D noiseTexture, trackingTexture;
        ChromaticAberration chromatic;
        LensDistortion distortion;
        float nextNoise, nextDisturbance, sweepStarted, eyeCrossing;
        bool disturbanceActive, crossingHandled, eyesVisible;

        public bool EyesVisible => eyesVisible;
        public bool IsSweeping => disturbanceActive;

        void Awake()
        {
            noiseTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            noiseTexture.name = "Menu analog noise (runtime)";
            noiseTexture.filterMode = FilterMode.Point;
            noiseTexture.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color32[128 * 128];
            var random = new System.Random(2741);
            for (int i = 0; i < pixels.Length; i++)
            {
                byte value = (byte)random.Next(0, 256);
                pixels[i] = new Color32(value, value, value, 255);
            }
            noiseTexture.SetPixels32(pixels);
            noiseTexture.Apply(false, true);
            if (noiseLayer)
            {
                noiseLayer.texture = noiseTexture;
                noiseLayer.color = new Color(1, 1, 1, noiseOpacity);
            }
            CreateTrackingTexture();
            if (bandStatic) { bandStatic.texture = trackingTexture; bandStatic.color = Color.white; }
            if (scanlines) scanlines.color = new Color(1, 1, 1, scanlineOpacity);
            if (filmVolume)
            {
                filmVolume.profile.TryGet(out chromatic);
                filmVolume.profile.TryGet(out distortion);
            }
            if (trackingBand) trackingBand.alpha = 0;
            eyesVisible = false;
            if (distantEyes) distantEyes.SetActive(false);
            nextDisturbance = Time.unscaledTime + Random.Range(firstSweepDelay.x, firstSweepDelay.y);
        }

        // Public entry point also allows a deterministic preview from the Editor.
        public void TriggerTrackingSweep()
        {
            if (disturbanceActive) return;
            disturbanceActive = true;
            crossingHandled = false;
            sweepStarted = Time.unscaledTime;
            eyeCrossing = .5f;
            if (menuCamera && distantEyes)
                eyeCrossing = Mathf.Clamp01(1f - menuCamera.WorldToViewportPoint(distantEyes.transform.position).y);
            nextDisturbance = sweepStarted + disturbanceDuration + Random.Range(disturbanceInterval.x, disturbanceInterval.y);
        }

        void Update()
        {
            float time = Time.unscaledTime;
            if (noiseLayer && time >= nextNoise)
            {
                nextNoise = time + 1f / Mathf.Max(1f, noiseRate);
                noiseLayer.uvRect = new Rect(Random.value, Random.value, 9f, 6f);
                if (bandStatic) bandStatic.uvRect = new Rect(Random.value, 0, 2.5f, 1f);
            }
            if (scanlines) scanlines.uvRect = new Rect(0, time * scanlineDrift, 1f, Screen.height / 3f);
            if (time >= nextDisturbance && !disturbanceActive) TriggerTrackingSweep();
            if (!disturbanceActive) return;

            float progress = Mathf.Clamp01((time - sweepStarted) / Mathf.Max(.1f, disturbanceDuration));
            if (bandTransform)
            {
                var parent = bandTransform.parent as RectTransform;
                float height = parent ? parent.rect.height : Screen.height;
                bandTransform.anchoredPosition = new Vector2(0, Mathf.Lerp(height * .5f, -height * .5f, progress));
            }
            float edgeFade = Mathf.Min(Mathf.Clamp01(progress * 15f), Mathf.Clamp01((1f - progress) * 15f));
            float signalVariation = .65f + .35f * Mathf.PerlinNoise(time * 19f, 2.3f);
            if (trackingBand) trackingBand.alpha = trackingOpacity * edgeFade * signalVariation;
            if (chromatic) chromatic.intensity.value = chromaticBaseline + .025f * edgeFade;
            if (distortion) distortion.intensity.value = -.007f * edgeFade;

            // Change only once, while the line crosses the eyes' projected height.
            if (!crossingHandled && progress >= eyeCrossing)
            {
                crossingHandled = true;
                eyesVisible = !eyesVisible;
                if (distantEyes) distantEyes.SetActive(eyesVisible);
            }
            if (progress >= 1f)
            {
                disturbanceActive = false;
                if (trackingBand) trackingBand.alpha = 0;
                if (chromatic) chromatic.intensity.value = chromaticBaseline;
                if (distortion) distortion.intensity.value = 0;
            }
        }

        // A damaged tape has a soft dropout band and broken, uneven tracking noise.
        void CreateTrackingTexture()
        {
            const int width = 512, height = 64;
            trackingTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            trackingTexture.name = "Soft tape tracking (runtime)";
            trackingTexture.filterMode = FilterMode.Bilinear;
            trackingTexture.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[width * height];
            var random = new System.Random(7301);
            for (int x = 0; x < width; x++)
            {
                float centre = 31f + Mathf.Sin(x * .045f) * 1.1f + (float)random.NextDouble() * 1.2f;
                float dropout = .25f + .75f * Mathf.PerlinNoise(x * .038f, 3.1f);
                for (int y = 0; y < height; y++)
                {
                    float distance = y - centre;
                    float line = Mathf.Exp(-distance * distance / 2.4f) * .65f * dropout;
                    float smear = Mathf.Exp(-distance * distance / 90f) * .09f;
                    float echoDistance = distance - 6f;
                    float echo = Mathf.Exp(-echoDistance * echoDistance / 1.4f) * .08f * dropout;
                    float staticNoise = .45f + (float)random.NextDouble() * .55f;
                    pixels[y * width + x] = new Color(.68f, .70f, .67f, (line + smear + echo) * staticNoise);
                }
            }
            trackingTexture.SetPixels(pixels);
            trackingTexture.Apply(false, true);
        }

        void OnDestroy()
        {
            if (noiseTexture) Destroy(noiseTexture);
            if (trackingTexture) Destroy(trackingTexture);
        }
    }
}

