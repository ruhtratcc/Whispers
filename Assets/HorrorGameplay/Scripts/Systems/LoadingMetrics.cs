// Mede etapas de carregamento para diagnóstico.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HorrorGame
{
    public static class LoadingMetrics
    {
        [Serializable]
        public sealed class Section
        {
            public string name;
            public int calls;
            public double milliseconds;
        }

        [Serializable]
        public sealed class Sample
        {
            public string label, from, to, environment;
            public double request, loadIssued, ready90 = -1, activated = -1, sessionReady = -1, firstRender = -1, playable = -1, fadeEnd = -1, preloadStarted = -1, preloadReady = -1;
            public long startBytes, peakBytes, reservedPeakBytes;
            public double worstFrameMs;
            public bool ready90Observed;
            public List<Section> sections = new List<Section>();
        }

        [Serializable]
        public sealed class Report
        {
            public string unity, gpu, cpu, product;
            public bool development, autoConnectProfiler;
            public int width, height;
            public List<Sample> samples = new List<Sample>();
            public List<PreloadSample> preloads = new List<PreloadSample>();
            public List<DiskSample> diskWrites = new List<DiskSample>();
        }

        [Serializable]
        public sealed class DiskSample
        {
            public string scene;
            public double milliseconds;
            public bool success;
        }

        [Serializable]
        public sealed class PreloadSample
        {
            public string scene, source;
            public double started, ready = -1, worstFrameMs;
            public long startBytes, peakBytes;
        }

        static PreloadSample preload;
        public static bool Enabled { get; private set; }

        public static string Label;
        public static Sample Current { get; private set; }
        public static Report Data { get; private set; }

        public static bool AwaitFade;
        static LoadingMetricsHost host;
        static double epoch;
        static string output;
        static readonly Dictionary<string, Section> sections = new Dictionary<string, Section>();
        public static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Enabled = false;
            preload = null;
            host = null;
            Current = null;
            Data = null;
            Label = null;
            AwaitFade = false;
            sections.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int n = Array.IndexOf(args, "--wotp-loading-output");
            if (!UnityEngine.Debug.isDebugBuild || n < 0 || n + 1 >= args.Length)
                return;
            if (!Application.productName.EndsWith("Loading Benchmark", StringComparison.Ordinal))
                return;
            output = Path.GetFullPath(args[n + 1]);
            Directory.CreateDirectory(output);
            Enabled = true;
            epoch = Now;
            Data = new Report
            {
                unity = Application.unityVersion,
                gpu = SystemInfo.graphicsDeviceName,
                cpu = SystemInfo.processorType,
                product = Application.productName,
                development = UnityEngine.Debug.isDebugBuild,
                width = Screen.width,
                height = Screen.height
            };
            host = new GameObject("Loading measurement (development only)").AddComponent<LoadingMetricsHost>();
            UnityEngine.Object.DontDestroyOnLoad(host.gameObject);
            SceneManager.sceneLoaded += Loaded;
            RenderPipelineManager.endCameraRendering += Rendered;
        }

        public readonly struct Timing : IDisposable
        {
            readonly string name;
            readonly double start;
            public Timing(string name)
            {
                this.name = name;
                start = Enabled ? Now : 0;
            }

            public void Dispose()
            {
                if (start == 0)
                    return;
                double elapsed = (Now - start) * 1000;
                if (!sections.TryGetValue(name, out var s))
                {
                    s = new Section
                    {
                        name = name
                    };
                    sections.Add(name, s);
                }

                s.calls++;
                s.milliseconds += elapsed;
            }
        }

        public static Timing Measure(string name) => new Timing(name);
        public static void Request(string destination)
        {
            if (!Enabled)
                return;
            if (Current != null && Current.to == destination && Current.playable < 0)
                return;
            sections.Clear();
            Current = new Sample
            {
                label = Label ?? "transition",
                from = SceneManager.GetActiveScene().name,
                to = destination,
                environment = Application.isEditor ? "Editor (não representa a build)" : "Development Build",
                request = Now - epoch,
                startBytes = Profiler.GetTotalAllocatedMemoryLong()
            };
            Data.samples.Add(Current);
            AwaitFade = false;
            Memory();
            UnityEngine.Debug.Log("[Loading] request " + Current.from + " -> " + destination);
        }

        public static void Observe(AsyncOperation operation)
        {
            if (!Enabled || operation == null)
                return;
            if (Current != null)
                Current.loadIssued = Now - epoch;
            bool releaseGate = !AwaitFade;
            if (releaseGate)
                operation.allowSceneActivation = false;
            host.StartCoroutine(Watch(operation, releaseGate));
        }

        static IEnumerator Watch(AsyncOperation operation, bool releaseGate)
        {
            while (!operation.isDone && operation.progress < .9f)
                yield return null;
            if (Current != null && operation.progress >= .9f)
            {
                Current.ready90 = Now - epoch;
                Current.ready90Observed = !operation.isDone;
            }

            Memory();
            if (releaseGate)
                operation.allowSceneActivation = true;
        }

        public static void SessionReady()
        {
            if (Enabled && Current != null)
                Current.sessionReady = Now - epoch;
        }

        public static void FadeFinished()
        {
            if (Enabled && Current != null)
                Current.fadeEnd = Now - epoch;
        }

        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (!Enabled || Current == null || scene.name != Current.to)
                return;
            Current.activated = Now - epoch;
            Memory();
        }

        static void Rendered(ScriptableRenderContext context, Camera camera)
        {
            if (!Enabled || Current == null || Current.firstRender >= 0 || camera.gameObject.scene.name != Current.to || camera.cameraType != CameraType.Game)
                return;
            if (Current.to != "MainMenu" && (!GameSession.Instance || GameSession.Instance.IsRestoringCheckpoint || !GameSession.Instance.narrative || !GameSession.Instance.narrative.IsInitialized))
                return;
            Current.firstRender = Now - epoch;
            Memory();
        }

        static void Memory()
        {
            if (Current == null)
                return;
            Current.peakBytes = Math.Max(Current.peakBytes, Profiler.GetTotalAllocatedMemoryLong());
            Current.reservedPeakBytes = Math.Max(Current.reservedPeakBytes, Profiler.GetTotalReservedMemoryLong());
        }

        public static void Tick()
        {
            if (Enabled && preload != null && preload.ready < 0)
            {
                preload.peakBytes = Math.Max(preload.peakBytes, Profiler.GetTotalAllocatedMemoryLong());
                preload.worstFrameMs = Math.Max(preload.worstFrameMs, Time.unscaledDeltaTime * 1000);
            }

            if (!Enabled || Current == null || Current.playable >= 0)
                return;
            Memory();
            Current.worstFrameMs = Math.Max(Current.worstFrameMs, Time.unscaledDeltaTime * 1000);
            bool ready = Current.to == "MainMenu" ? Current.activated >= 0 && Current.sessionReady >= 0 : GameSession.Instance && GameSession.Instance.gameObject.scene.name == Current.to && GameSession.Instance.narrative && GameSession.Instance.narrative.IsInitialized && GameSession.Instance.CanControl;
            if (ready && Current.firstRender >= 0 && (!AwaitFade || Current.fadeEnd >= 0))
            {
                Current.playable = Now - epoch;
                if (Current.fadeEnd < 0)
                    Current.fadeEnd = Current.playable;
                foreach (var pair in sections)
                    Current.sections.Add(new Section { name = pair.Key, calls = pair.Value.calls, milliseconds = pair.Value.milliseconds });
                Flush();
            }
        }

        public static void PreloadBegin(string scene)
        {
            if (!Enabled)
                return;
            preload = new PreloadSample
            {
                scene = scene,
                source = SceneManager.GetActiveScene().name,
                started = Now - epoch,
                startBytes = Profiler.GetTotalAllocatedMemoryLong()
            };
            Data.preloads.Add(preload);
        }

        public static void PreloadReady(string scene)
        {
            if (Enabled && preload != null && preload.scene == scene && preload.ready < 0)
            {
                preload.ready = Now - epoch;
                preload.peakBytes = Math.Max(preload.peakBytes, Profiler.GetTotalAllocatedMemoryLong());
            }
        }

        public static void DiskWrite(string scene, double milliseconds, bool success)
        {
            if (Enabled)
                Data.diskWrites.Add(new DiskSample { scene = scene, milliseconds = milliseconds, success = success });
        }

        public static void Flush()
        {
            if (!Enabled)
                return;
            File.WriteAllText(Path.Combine(output, "loading-metrics.json"), JsonUtility.ToJson(Data, true));
        }
    }

    [DefaultExecutionOrder(32000)]
    public sealed class LoadingMetricsHost : MonoBehaviour
    {
        void Update() => LoadingMetrics.Tick();
    }
}
