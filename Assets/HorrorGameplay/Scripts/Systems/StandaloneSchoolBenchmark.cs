// Executa a medição técnica da escola para diagnóstico.

#if WOTP_BENCHMARK
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HorrorGame
{
    // This type is compiled only into the disposable benchmark player.
    public sealed class StandaloneSchoolBenchmark : MonoBehaviour
    {
        [Serializable] sealed class Measurement
        {
            public string area, screenshot, state, chapter;
            public Vector3 feet, camera, forward;
            public int frames;
            public double averageFrameMs, medianFrameMs, p95FrameMs, p99FrameMs, fps;
            public bool drawCounterAvailable, triangleCounterAvailable, cpuCounterAvailable, gcCounterAvailable, gpuTimingAvailable;
            public double averageDrawCalls, averageTriangles, averageMainThreadMs, averageGcBytes, averageGpuFrameMs;
            public bool frameTimingCpuAvailable, frameTimingMainThreadAvailable, imageInformation;
            public double averageFrameTimingCpuMs, averageFrameTimingMainThreadMs, imageMeanBrightness;
            public int imageMaximumChannel, renderedCallbacks;
        }
        [Serializable] sealed class Report
        {
            public bool passed;
            public string error, unity, gpu, cpu, graphicsApi, product, scene, persistentDataPath;
            public int width, height, qualityLevel, vSyncCount, targetFrameRate;
            public bool developmentBuild;
            public bool gpuOcclusion;
            public int hudCanvasesRedirected;
            public string scope = "Standalone release player; actual gameplay camera renders into HDR1600x900. Only endCameraRendering callbacks from that camera are timed (wall time between rendered frames). Screen-space overlay canvases temporarily use that real camera to retain UI cost. Real flashlight/VHS/mist/audio/gameplay update remain active; opening progression alone is suspended. RT readback and PNG validation occur outside measured samples.";
            public List<Measurement> measurements = new List<Measurement>();
        }
        struct Sample
        {
            public double frame, gpu, timingCpu, timingMainThread;
            public long draw, triangles, cpu, gc;
        }
        struct Pose
        {
            public string name;
            public Vector3 feet, target;
            public Pose(string name, Vector3 feet, Vector3 target) { this.name = name; this.feet = feet; this.target = target; }
        }
        static readonly Pose[] Poses = {
            new Pose("central-corridor", new Vector3(-.1f,.7f,30), new Vector3(.56f,5,35.84f)),
            new Pose("canteen", new Vector3(-2,.65f,45.4f), new Vector3(-4.37f,5.7f,49.95f)),
            new Pose("court", new Vector3(88,.2f,25), new Vector3(95.53f,3.0f,17.34f))
        };
        readonly Report report = new Report();
        string output;
        GameSession game;
        readonly FrameTiming[] gpuTiming = new FrameTiming[1];
        ProfilerRecorder draw, triangles, cpu, gc;
        readonly List<Sample> samples = new List<Sample>(65536);
        sealed class CanvasState { public Canvas canvas; public RenderMode mode; public Camera camera; public float distance; }
        readonly List<CanvasState> canvases = new List<CanvasState>();
        Camera realCamera;
        RenderTexture renderTarget, originalTarget;
        float originalAspect;
        bool originalCameraEnabled, collecting;
        bool requestedOcclusion, originalOcclusion;
        UniversalRenderPipelineAsset pipeline;
        int renderedCallbacks;
        double lastRenderedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-wotpBenchmarkOutput");
            if (index < 0 || index + 1 >= args.Length) return;
            if (!Application.productName.EndsWith("Benchmark", StringComparison.Ordinal))
            {
                Debug.LogError("Refusing benchmark without an isolated product identity.");
                Application.Quit(2); return;
            }
            string directory = args[index + 1];
            if (!Path.IsPathRooted(directory)) { Debug.LogError("Benchmark output must be absolute."); Application.Quit(2); return; }
            var host = new GameObject("Standalone benchmark (build-only)");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<StandaloneSchoolBenchmark>();
            runner.output = Path.GetFullPath(directory);
            Directory.CreateDirectory(runner.output);
            runner.requestedOcclusion=Array.IndexOf(args,"--wotp-occlusion")>=0;
            PlayerPrefs.SetInt("Game.ResumeRequested", 0);
            PlayerPrefs.SetInt("Game.StoryCarryRequested", 0);
            SceneManager.sceneLoaded += RedirectSave;
            Application.runInBackground = true;
        }
        static void RedirectSave(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var save in root.GetComponentsInChildren<SaveSystem>(true)) save.fileName = "benchmark-save.json";
        }
        IEnumerator Start()
        {
            if (string.IsNullOrEmpty(output)) yield break;
            Application.logMessageReceived += CaptureError;
            double deadline = Time.realtimeSinceStartupAsDouble + 40;
            while ((!GameSession.Instance || !GameSession.Instance.narrative || !GameSession.Instance.narrative.IsInitialized)
                && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            game = GameSession.Instance;
            if (!game || !game.narrative || !game.narrative.IsInitialized || !game.player || !game.save || !game.save.flashlight)
            { Finish("School gameplay did not initialize."); yield break; }
            report.unity = Application.unityVersion; report.gpu = SystemInfo.graphicsDeviceName;
            report.cpu = SystemInfo.processorType; report.graphicsApi = SystemInfo.graphicsDeviceType.ToString();
            report.product = Application.productName; report.persistentDataPath = Application.persistentDataPath;
            report.scene = SceneManager.GetActiveScene().name; report.developmentBuild = Debug.isDebugBuild;
            pipeline=UniversalRenderPipeline.asset;
            if(pipeline){originalOcclusion=pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras;if(requestedOcclusion)pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras=true;}
            report.gpuOcclusion=pipeline&&pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras;
            report.width = Screen.width; report.height = Screen.height; report.qualityLevel = QualitySettings.GetQualityLevel();
            report.vSyncCount=QualitySettings.vSyncCount;report.targetFrameRate=Application.targetFrameRate;
            if (report.developmentBuild || report.scene != "SchoolCEEP" || report.width != 1600 || report.height != 900)
            { Finish("Expected release SchoolCEEP player at 1600x900."); yield break; }
            game.input.Actions.Disable();
            var opening = FindAnyObjectByType<SchoolOpeningFlow>();
            if (opening) opening.enabled = false;
            UnityEngine.Random.InitState(72138);
            realCamera=game.player.view.playerCamera;
            originalTarget=realCamera.targetTexture;originalAspect=realCamera.aspect;originalCameraEnabled=realCamera.enabled;
            renderTarget=new RenderTexture(1600,900,24,RenderTextureFormat.DefaultHDR);
            renderTarget.Create();realCamera.targetTexture=renderTarget;realCamera.aspect=1600f/900;realCamera.enabled=true;
            foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                if(canvas.renderMode==RenderMode.ScreenSpaceOverlay)
                {
                    canvases.Add(new CanvasState{canvas=canvas,mode=canvas.renderMode,camera=canvas.worldCamera,distance=canvas.planeDistance});
                    canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=realCamera;canvas.planeDistance=Mathf.Max(realCamera.nearClipPlane+.05f,1);
                }
            report.hudCanvasesRedirected=canvases.Count;
            RenderPipelineManager.endCameraRendering+=Rendered;
            draw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            foreach (var pose in Poses)
            {
                if (!TrySupportedFeet(pose.feet, out var feet)) { Finish("No clear supported player capsule near " + pose.name); yield break; }
                Vector3 delta = pose.target - (feet + Vector3.up * game.player.EyeHeight);
                game.player.Warp(feet, Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg, -Mathf.Asin(delta.normalized.y)*Mathf.Rad2Deg);
                game.player.view.Snap(); Physics.SyncTransforms();
                game.save.flashlight.Restore(game.save.flashlight.capacitySeconds, true);
                int beforeWarm=renderedCallbacks;
                double warmed = Time.realtimeSinceStartupAsDouble + 5;
                while (Time.realtimeSinceStartupAsDouble < warmed) { FrameTimingManager.CaptureFrameTimings(); yield return null; }
                if(renderedCallbacks-beforeWarm<2){Finish("Gameplay camera did not render the HDR target during warmup.");yield break;}
                samples.Clear();collecting=true;int beforeSamples=renderedCallbacks;
                double end = Time.realtimeSinceStartupAsDouble + 12;
                while (Time.realtimeSinceStartupAsDouble < end)
                {
                    FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                    if (game.State != GameState.Playing || !game.CanControl || !game.save.flashlight.IsOn || !string.IsNullOrEmpty(report.error))
                    { Finish(report.error ?? "Game state changed while benchmarking."); yield break; }
                }
                collecting=false;
                if (samples.Count < 60) { Finish("Insufficient frame samples."); yield break; }
                var measure = Summarize(samples);
                measure.area=pose.name; measure.feet=game.player.transform.position;
                var camera=game.player.view.playerCamera.transform;
                measure.camera=camera.position; measure.forward=camera.forward;
                measure.state=game.State.ToString(); measure.chapter=game.narrative.Progress.chapterId;
                measure.screenshot=Path.Combine(output,pose.name+".png");
                measure.renderedCallbacks=renderedCallbacks-beforeSamples;
                CaptureTarget(measure);
                if(!measure.imageInformation&&!measure.drawCounterAvailable){Finish("HDR target has no image information or valid draw counter.");yield break;}
                report.measurements.Add(measure);
                File.WriteAllText(Path.Combine(output,"benchmark.json"),JsonUtility.ToJson(report,true));
            }
            Finish(null);
        }
        void Rendered(ScriptableRenderContext context,Camera camera)
        {
            if(camera!=realCamera||camera.targetTexture!=renderTarget)return;
            double now=Time.realtimeSinceStartupAsDouble;
            renderedCallbacks++;
            double elapsed=now-lastRenderedAt;lastRenderedAt=now;
            if(!collecting)return;
            if(samples.Count==samples.Capacity){report.error="Rendered sample capacity exceeded.";collecting=false;return;}
            bool timed=FrameTimingManager.GetLatestTimings(1,gpuTiming)>0;
            samples.Add(new Sample{frame=elapsed*1000,draw=draw.Valid?draw.LastValue:0,triangles=triangles.Valid?triangles.LastValue:0,
                cpu=cpu.Valid?cpu.LastValue:0,gc=gc.Valid?gc.LastValue:0,gpu=timed?gpuTiming[0].gpuFrameTime:0,
                timingCpu=timed?gpuTiming[0].cpuFrameTime:0,timingMainThread=timed?gpuTiming[0].cpuMainThreadFrameTime:0});
        }
        void CaptureTarget(Measurement measure)
        {
            var previous=RenderTexture.active;Texture2D image=null;
            try
            {
                RenderTexture.active=renderTarget;image=new Texture2D(renderTarget.width,renderTarget.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,renderTarget.width,renderTarget.height),0,0);image.Apply();
                var pixels=image.GetPixels32();long brightness=0;int minimum=255,maximum=0;
                foreach(var pixel in pixels){int channel=Mathf.Max(pixel.r,Mathf.Max(pixel.g,pixel.b));minimum=Mathf.Min(minimum,channel);maximum=Mathf.Max(maximum,channel);brightness+=channel;}
                measure.imageMaximumChannel=maximum;measure.imageMeanBrightness=brightness/(double)pixels.Length;
                measure.imageInformation=maximum>8&&maximum-minimum>2;
                File.WriteAllBytes(measure.screenshot,ImageConversion.EncodeToPNG(image));
            }
            finally{RenderTexture.active=previous;if(image)Destroy(image);}
        }
        bool TrySupportedFeet(Vector3 hint, out Vector3 feet)
        {
            int mask=~LayerMask.GetMask("Player","Ignore Raycast");
            float radius=game.player.Controller.radius, height=game.player.standingHeight;
            for(int candidate=0;candidate<33;candidate++)
            {
                float angle=candidate*Mathf.PI/4, distance=candidate==0?0:(1+(candidate-1)/8)*.20f;
                var point=hint+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*distance;
                if(!Physics.Raycast(point+Vector3.up,Vector3.down,out var ground,3,mask,QueryTriggerInteraction.Ignore)||ground.normal.y<.75f)continue;
                var supported=ground.point+Vector3.up*.05f;
                if(Physics.CheckCapsule(supported+Vector3.up*(radius+.02f),supported+Vector3.up*(height-radius-.02f),radius,mask,QueryTriggerInteraction.Ignore))continue;
                feet=supported;return true;
            }
            feet=default;return false;
        }
        Measurement Summarize(List<Sample> samples)
        {
            var ordered=new double[samples.Count]; double frame=0, draws=0,tris=0,cpus=0,gcs=0,gpus=0,timingCpu=0,timingMain=0;int gpuCount=0,timingCpuCount=0,timingMainCount=0;
            for(int i=0;i<samples.Count;i++)
            {
                var sample=samples[i];ordered[i]=sample.frame;frame+=sample.frame;draws+=sample.draw;tris+=sample.triangles;cpus+=sample.cpu;gcs+=sample.gc;
                if(sample.gpu>0){gpus+=sample.gpu;gpuCount++;}
                if(sample.timingCpu>0){timingCpu+=sample.timingCpu;timingCpuCount++;}if(sample.timingMainThread>0){timingMain+=sample.timingMainThread;timingMainCount++;}
            }
            Array.Sort(ordered);
            double average=frame/samples.Count;
            return new Measurement {frames=samples.Count,averageFrameMs=average,medianFrameMs=ordered[ordered.Length/2],
                p95FrameMs=ordered[(int)((ordered.Length-1)*.95)],p99FrameMs=ordered[(int)((ordered.Length-1)*.99)],fps=1000/average,
                drawCounterAvailable=draw.Valid&&draws>0,triangleCounterAvailable=triangles.Valid&&tris>0,cpuCounterAvailable=cpu.Valid&&cpus>0,
                gcCounterAvailable=gc.Valid,gpuTimingAvailable=gpuCount>0,averageDrawCalls=draws/samples.Count,averageTriangles=tris/samples.Count,
                averageMainThreadMs=cpus/samples.Count/1000000,averageGcBytes=gcs/samples.Count,averageGpuFrameMs=gpuCount>0?gpus/gpuCount:0,
                frameTimingCpuAvailable=timingCpuCount>0,frameTimingMainThreadAvailable=timingMainCount>0,
                averageFrameTimingCpuMs=timingCpuCount>0?timingCpu/timingCpuCount:0,averageFrameTimingMainThreadMs=timingMainCount>0?timingMain/timingMainCount:0};
        }
        void CaptureError(string message,string stack,LogType type)
        { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.error=message; }
        void Finish(string error)
        {
            if(!string.IsNullOrEmpty(error))report.error=error;
            report.passed=string.IsNullOrEmpty(report.error)&&report.measurements.Count==Poses.Length;
            collecting=false;RenderPipelineManager.endCameraRendering-=Rendered;
            draw.Dispose();triangles.Dispose();cpu.Dispose();gc.Dispose();
            foreach(var state in canvases)if(state.canvas){state.canvas.renderMode=state.mode;state.canvas.worldCamera=state.camera;state.canvas.planeDistance=state.distance;}
            if(realCamera){realCamera.targetTexture=originalTarget;realCamera.aspect=originalAspect;realCamera.enabled=originalCameraEnabled;}
            if(renderTarget){renderTarget.Release();Destroy(renderTarget);}
            if(pipeline)pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras=originalOcclusion;
            SceneManager.sceneLoaded-=RedirectSave;Application.logMessageReceived-=CaptureError;
            if(!string.IsNullOrEmpty(output))File.WriteAllText(Path.Combine(output,"benchmark.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.passed?0:1);
        }
    }
}
#endif
