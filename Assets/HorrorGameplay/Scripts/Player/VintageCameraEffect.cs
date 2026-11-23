// Configura o filtro de vídeo antigo conforme o capítulo e a tensão.

using UnityEngine;

namespace HorrorGame
{
    /// <summary>Aplica o efeito de fita aos capítulos. O começo no escritório mantém a imagem limpa.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class VintageCameraEffect : MonoBehaviour
    {
        [Header("Story gate")]
        [SerializeField, Min(0)] public int chapterIndex;
        [SerializeField] public string prologueSceneName = "Chapter1Office";
        [Header("Subtle analogue image")]
        [SerializeField, Range(0, 1)] public float intensity = .72f;
        [SerializeField, Range(0, .08f)] public float grain = .022f;
        [SerializeField, Range(0, 2)] public float chromaticPixels = .65f;
        [SerializeField, Range(0, .1f)] public float scanlines = .025f;
        [SerializeField, Range(0, 2)] public float trackingPixels = .85f;
        [SerializeField, Range(0, .4f)] public float softness = .12f;
        [SerializeField, Min(.05f)] public float fadeSeconds = .7f;
        [Header("School tape treatment (scene only)")]
        [SerializeField] public bool schoolTreatment;
        [SerializeField, Range(0, 1)] public float colourBleed = .22f;
        [SerializeField, Range(12, 60)] public float trackingInterval = 23f;
        [SerializeField, Range(0, .5f), Tooltip("Zero disables scheduled tracking faults; story pulses still work.")]
        public float trackingDuration = .24f;
        [Header("Horror image grade (explicit road/school opt-in)")]
        [SerializeField] public bool horrorTreatment;
        [SerializeField, Range(.5f, 1)] public float saturation = .68f;
        [SerializeField, Range(0, .4f)] public float contrast = .16f;
        [SerializeField, Range(0, .004f)] public float blackLift = .0012f;
        [SerializeField, Range(0, .2f)] public float paletteTint = .08f;
        [SerializeField, Range(0, .25f)] public float imageVignette = .12f;
        [Header("Composite colour signal")]
        [SerializeField] public bool compositeSignal;
        [SerializeField, Range(0, 48), Tooltip("Horizontal colour trail at 720p; luminance stays sharp.")]
        public float chromaTrailPixels = 28;
        [SerializeField, Range(120, 720), Tooltip("Noise raster only. The image and UI retain their full resolution.")]
        public int noiseLines = 240;

        float blend, signalTime, interference, interferenceRemaining, darknessDistortion;
        Material material;
        MaterialPropertyBlock parameters;
        static readonly int Signal = Shader.PropertyToID("_VintageSignal");
        static readonly int TextureEffects = Shader.PropertyToID("_VintageTextureEffects");
        static readonly int Clock = Shader.PropertyToID("_VintageClock");
        static readonly int Resolution = Shader.PropertyToID("_VintageResolution");
        static readonly int Tape = Shader.PropertyToID("_VintageTape");
        static readonly int Grade = Shader.PropertyToID("_VintageGrade");
        static readonly int Lens = Shader.PropertyToID("_VintageLens");
        static readonly int Composite = Shader.PropertyToID("_VintageComposite");
        static readonly int Darkness = Shader.PropertyToID("_VintageDarkness");

        public bool ChapterAllowed => chapterIndex > 0 && gameObject.scene.name != prologueSceneName;
        public bool SchoolTreatmentAllowed => schoolTreatment && gameObject.scene.name == "SchoolCEEP";
        // A configuração vale para estas câmeras da cena. As câmeras do menu e do prólogo mantêm seus ajustes.
        public bool HorrorTreatmentAllowed => horrorTreatment && ChapterAllowed && isActiveAndEnabled
            && (gameObject.scene.name == "SchoolCEEP" || gameObject.scene.name == "RoadJourney");
        public float EffectiveIntensity => ChapterAllowed && isActiveAndEnabled ? intensity * blend : 0;
        public float SignalTime => signalTime;
        public float Interference => SchoolTreatmentAllowed ? interference : 0;
        public float DarknessDistortion => SchoolTreatmentAllowed && ChapterAllowed && isActiveAndEnabled ? darknessDistortion : 0;

        public void SetDarknessDistortion(float strength)
            => darknessDistortion = float.IsNaN(strength) || float.IsInfinity(strength) ? 0 : Mathf.Clamp01(strength);

        void OnEnable() => blend = ChapterAllowed ? 1 : 0;
        void Update()
        {
            // O efeito de fita para com o jogo, inclusive na pausa e na leitura de documentos.
            AdvanceSignal(Time.deltaTime, Time.timeScale <= 0);
        }

        /// <summary>Avança a fita usando o tempo do jogo. Na pausa ou durante a leitura, a fita fica parada.</summary>
        public void AdvanceSignal(float deltaTime, bool paused)
        {
            if (paused || deltaTime <= 0 || !isActiveAndEnabled) return;
            signalTime += deltaTime;
            blend = Mathf.MoveTowards(blend, ChapterAllowed ? 1 : 0,
                deltaTime / Mathf.Max(.05f, fadeSeconds));
            interferenceRemaining = Mathf.Max(0, interferenceRemaining - deltaTime);
            if (interferenceRemaining <= 0) interference = Mathf.MoveTowards(interference, 0, deltaTime * 5);
        }

        public void ConfigureForChapter(int index, bool immediate = true)
        {
            chapterIndex = Mathf.Max(0, index);
            if (immediate) blend = ChapterAllowed ? 1 : 0;
        }

        /// <summary>Aplica o ajuste de cor quando ativado. As demais câmeras mantêm sua aparência original.</summary>
        public void ConfigureHorrorTreatment()
        {
            horrorTreatment = true;
            compositeSignal = true;
            intensity = .96f; grain = .040f; chromaticPixels = 0;
            scanlines = 0; trackingPixels = .5f; softness = .04f;
            saturation = .72f; contrast = .18f; blackLift = .0006f;
            paletteTint = .10f; imageVignette = .06f;
            colourBleed = .9f; chromaTrailPixels = 28; noiseLines = 240;
            trackingInterval = 23; trackingDuration = 0;
        }

        /// <summary>Configura falhas breves de imagem na escola, mantendo os detalhes visíveis.</summary>
        public void ConfigureSchoolTreatment()
        {
            ConfigureHorrorTreatment();
            schoolTreatment = true;
            grain = .044f;
        }

        public void PulseInterference(float strength = .65f, float duration = .18f)
        {
            if (!SchoolTreatmentAllowed || EffectiveIntensity <= 0) return;
            interference = Mathf.Max(interference, Mathf.Clamp01(strength));
            interferenceRemaining = Mathf.Max(interferenceRemaining, Mathf.Clamp(duration, .05f, .45f));
        }

        internal Material GetMaterial(Shader shader)
        {
            if (!shader || !shader.isSupported) return null;
            if (material && material.shader != shader) ReleaseMaterial();
            if (!material) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return material;
        }

        internal MaterialPropertyBlock GetParameters(int width, int height)
        {
            if (parameters == null) parameters = new MaterialPropertyBlock();
            parameters.Clear();
            float comfort = PresentationSettings.ReduceEffects ? .2f : 1;
            parameters.SetVector(Signal, new Vector4(EffectiveIntensity, grain * comfort, chromaticPixels * comfort, softness * comfort));
            parameters.SetVector(TextureEffects, new Vector4(scanlines * comfort, trackingPixels * comfort, 0, 0));
            parameters.SetVector(Tape, SchoolTreatmentAllowed || HorrorTreatmentAllowed
                ? new Vector4(colourBleed * comfort, trackingInterval, trackingDuration * comfort, Interference * comfort)
                : Vector4.zero);
            parameters.SetVector(Grade, HorrorTreatmentAllowed
                ? new Vector4(saturation, contrast, blackLift, paletteTint)
                : new Vector4(1, 0, 0, 0));
            parameters.SetVector(Lens, HorrorTreatmentAllowed
                ? new Vector4(imageVignette, 1, 0, 0) : Vector4.zero);
            parameters.SetVector(Composite, HorrorTreatmentAllowed && compositeSignal
                ? new Vector4(1, Mathf.Clamp(chromaTrailPixels, 0, 48) * comfort, Mathf.Clamp(noiseLines, 120, 720), 0) : Vector4.zero);
            parameters.SetFloat(Clock, signalTime);
            parameters.SetFloat(Darkness, DarknessDistortion * comfort);
            parameters.SetVector(Resolution, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
            return parameters;
        }

        void OnDestroy() => ReleaseMaterial();
        void ReleaseMaterial()
        {
            if (!material) return;
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            material = null;
        }
    }
}
