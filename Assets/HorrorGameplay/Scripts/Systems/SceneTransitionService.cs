// Coordena a troca assíncrona de cenário, os fades e o bloqueio temporário de entrada.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace HorrorGame
{
    /// <summary>One native load at a time; a held load is drained before a different destination.</summary>
    public sealed class SceneTransitionService : MonoBehaviour
    {
        public const float FadeSeconds = .35f, InputLockSeconds = .5f;
        static SceneTransitionService instance;
        public static bool ControlsBlocked => instance && instance.transitioning;
        public static bool IsDiscarding(string scene) => instance && instance.discardingScene == scene;
        public static bool IsPreloaded(string scene) => instance && instance.pendingScene == scene && instance.pending != null && instance.pending.progress >= .9f;
        public static string PendingScene => instance ? instance.pendingScene : null;
        public static int NativeLoadCount { get; private set; }
        AsyncOperation pending;
        string pendingScene, deferredPreload, discardingScene;
        Coroutine warming;
        bool transitioning, rendered,returnRequested;
        string arrivingScene;
        UnityEngine.ThreadPriority savedPriority;
        float savedVolume;
        CanvasGroup curtain;
        TMP_Text waiting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { instance = null; NativeLoadCount = 0; }
        static SceneTransitionService Ensure()
        {
            if (instance) return instance;
            var go = new GameObject("Scene transition service");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SceneTransitionService>();
            return instance;
        }
        public static bool Preload(string scene, bool automatic = false)
        {
#if UNITY_EDITOR
            // Existing isolated fixtures unload scenes directly; explicit preload tests opt in themselves.
            if (automatic && SaveSystem.TestSaveDirectory != null) return false;
#endif
            if (string.IsNullOrEmpty(scene) || scene == SceneManager.GetActiveScene().name || !Application.CanStreamedLevelBeLoaded(scene)) return false;
            var service = Ensure();
            if (service.transitioning) { service.deferredPreload = scene; return true; }
            if (service.pending != null) return service.pendingScene == scene;
            service.savedPriority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = SceneManager.GetActiveScene().name=="MainMenu"?UnityEngine.ThreadPriority.High:UnityEngine.ThreadPriority.Low;
            service.pending = service.StartNative(scene);
            if (service.pending == null) { Application.backgroundLoadingPriority = service.savedPriority; return false; }
            service.pendingScene = scene;
            LoadingMetrics.PreloadBegin(scene);
            service.warming = service.StartCoroutine(service.Warm());
            return true;
        }
        AsyncOperation StartNative(string scene)
        {
            try
            {
                var operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                if (operation != null) { operation.allowSceneActivation = false; NativeLoadCount++; }
                return operation;
            }
            catch (Exception error) { Debug.LogWarning("Could not prepare scene: " + error.Message); return null; }
        }
        IEnumerator Warm()
        {
            while (pending != null && pending.progress < .9f && !pending.isDone) yield return null;
            warming = null;
            LoadingMetrics.PreloadReady(pendingScene);
            if (!transitioning) Application.backgroundLoadingPriority = savedPriority;
        }
        public static bool Load(string scene,bool alreadyBlack=false)
        {
            if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene)) return false;
            var service = Ensure();
            if (service.transitioning) { if(scene=="MainMenu"&&service.arrivingScene!=null&&service.arrivingScene!="MainMenu"){service.returnRequested=true;return true;}return false; }
            service.transitioning = true;
            LoadingMetrics.Request(scene);
            LoadingMetrics.AwaitFade = true;
            service.StartCoroutine(service.Transition(scene,false,alreadyBlack));
            return true;
        }
        void CreateCurtain()
        {
            if (curtain) return;
            var root = new GameObject("Loading curtain", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32700;
            var scaler=root.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
            root.AddComponent<UnityEngine.UI.GraphicRaycaster>(); curtain = root.GetComponent<CanvasGroup>(); curtain.alpha = 0;
            var panel = new GameObject("Black", typeof(RectTransform), typeof(UnityEngine.UI.Image)); panel.transform.SetParent(root.transform, false);
            var rect = (RectTransform)panel.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<UnityEngine.UI.Image>().color = Color.black;
            var text = new GameObject("Waiting", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(root.transform, false);
            waiting = text.GetComponent<TMP_Text>(); waiting.text = "Um momento..."; waiting.fontSize = 15; waiting.color = new Color(.75f,.75f,.75f); waiting.alignment = TextAlignmentOptions.Center; waiting.raycastTarget = false;
            var area = (RectTransform)text.transform; area.anchorMin = area.anchorMax = new Vector2(.5f,.12f); area.sizeDelta = new Vector2(500,50); area.anchoredPosition = Vector2.zero;
            waiting.gameObject.SetActive(false);
        }
        IEnumerator Fade(float target)
        {
            float alpha = curtain.alpha, audio = AudioListener.volume;
            for (float elapsed = 0; elapsed < FadeSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / FadeSeconds); curtain.alpha = Mathf.Lerp(alpha,target,t);
                AudioListener.volume = Mathf.Lerp(audio,target > .5f ? 0 : savedVolume,t);
                yield return null;
            }
            curtain.alpha = target; AudioListener.volume = target > .5f ? 0 : savedVolume;
        }
        IEnumerator Transition(string destination,bool redirect=false,bool alreadyBlack=false)
        {
            var priority = pending != null ? savedPriority : Application.backgroundLoadingPriority;
            if(!redirect)savedVolume = AudioListener.volume;
            else {LoadingMetrics.Request(destination);LoadingMetrics.AwaitFade=true;}
            CreateCurtain(); curtain.blocksRaycasts = true;
            if(alreadyBlack){curtain.alpha=1;AudioListener.volume=0;}else yield return Fade(1);
            Time.timeScale = 1; AudioListener.pause = false;
            Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.High;
            if (warming != null) { StopCoroutine(warming); warming = null; }
            double waitingSince = Time.realtimeSinceStartupAsDouble;
            // Unity cannot cancel a load held at 90%; finish it hidden without starting its game session.
            if (pending != null && pendingScene != destination)
            {
                while (pending.progress < .9f && !pending.isDone) { ShowWaiting(waitingSince); yield return null; }
                LoadingMetrics.PreloadReady(pendingScene);
                discardingScene = pendingScene;
                pending.allowSceneActivation = true;
                while (!pending.isDone) { ShowWaiting(waitingSince); yield return null; }
                pending = null; pendingScene = null; discardingScene = null;
            }
            if (pending == null)
            {
                pending = StartNative(destination); pendingScene = destination;
                if (pending == null) { Fail(priority); yield break; }
            }
            LoadingMetrics.Observe(pending);
            while (!pending.isDone && pending.progress < .9f) { ShowWaiting(waitingSince); yield return null; }
            LoadingMetrics.PreloadReady(pendingScene);
            arrivingScene = destination; rendered = false;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += Rendered;
            pending.allowSceneActivation = true;
            while (!pending.isDone) yield return null;
            pending = null; pendingScene = null;
            double activated = Time.realtimeSinceStartupAsDouble;
            while (true) {
                if(returnRequested){returnRequested=false;deferredPreload=null;UnityEngine.Rendering.RenderPipelineManager.endCameraRendering-=Rendered;arrivingScene=null;Application.backgroundLoadingPriority=priority;yield return Transition("MainMenu",true);yield break;}
                if(SessionReady(destination)&&rendered)break;
                if(Time.realtimeSinceStartupAsDouble-activated>30){Fail(priority);yield break;}
                ShowWaiting(waitingSince);yield return null;
            }
            waiting.gameObject.SetActive(false);
            yield return Fade(0);
            while (Time.realtimeSinceStartupAsDouble - activated < InputLockSeconds) yield return null;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= Rendered;
            curtain.blocksRaycasts = false; transitioning = false; arrivingScene = null;
            Application.backgroundLoadingPriority = priority;
            LoadingMetrics.FadeFinished();
            var next = deferredPreload; deferredPreload = null;
            if (!string.IsNullOrEmpty(next)) Preload(next, true);
        }
        bool SessionReady(string scene)
        {
            var game = GameSession.Instance;
            return scene == "MainMenu" || game && game.gameObject.scene.name == scene && !game.IsRestoringCheckpoint && (!game.narrative || game.narrative.IsInitialized);
        }
        void Rendered(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
        { if (camera.cameraType == CameraType.Game && camera.gameObject.scene.name == arrivingScene) rendered = true; }
        void ShowWaiting(double since) { if (Time.realtimeSinceStartupAsDouble - since > 1) waiting.gameObject.SetActive(true); }
        void Fail(UnityEngine.ThreadPriority priority)
        {
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= Rendered;
            pending = null; pendingScene = null; discardingScene = null; deferredPreload = null;returnRequested=false;
            curtain.alpha = 0; curtain.blocksRaycasts = false; waiting.gameObject.SetActive(false);
            AudioListener.volume = savedVolume; Application.backgroundLoadingPriority = priority; transitioning = false;
            GameSession.Instance?.hud?.Notify("Não foi possível abrir a próxima área. Pressione Esc para voltar.");
        }
        void OnDestroy()
        {
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= Rendered;
            if (instance == this) instance = null;
        }
    }
}
