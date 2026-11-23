// Controla a interação com o espelho de memórias da versão anterior.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorGame
{
    /// <summary>A nearby reflection lags behind João, then smiles independently. It never collides.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolMemoryMirror : MonoBehaviour
    {
        public Camera playerCamera;
        public Renderer surface;
        public Transform reflectionPlane, reflectionProxy;
        public GameObject smile;
        public NarrativeDirector director;
        public bool Finished { get; private set; }
        public float ObservedSeconds { get; private set; }

        struct Sample { public float time; public Vector3 position; public Quaternion rotation; }
        readonly Queue<Sample> samples = new Queue<Sample>();
        Sample delayed;
        Camera reflectionCamera;
        RenderTexture target;
        Material originalMaterial, mirrorMaterial;
        Quaternion proxyRotation;
        float renderAge;
        bool renderingUnavailable;

        void OnEnable()
        {
            if (reflectionProxy) proxyRotation = reflectionProxy.localRotation;
            Restore(false);
        }

        public void Restore(bool done)
        {
            Finished = done;
            ObservedSeconds = renderAge = 0;
            samples.Clear();
            if (smile) smile.SetActive(false);
            if (reflectionProxy) reflectionProxy.localRotation = proxyRotation;
        }

        void Update()
        {
            bool active = director && director.IsInitialized && director.NextSignal == "memories.mirror"
                && director.game && director.game.CanControl && !director.SequencePlaying;
            bool observing = active && IsObserved();
            AdvanceObservation(Time.deltaTime, observing);
            if (!observing || renderingUnavailable) return;
            var pose = new Sample { time = ObservedSeconds, position = playerCamera.transform.position,
                rotation = playerCamera.transform.rotation };
            if (samples.Count == 0) delayed = pose;
            samples.Enqueue(pose);
            while (samples.Count > 0 && samples.Peek().time <= ObservedSeconds - .4f) delayed = samples.Dequeue();
            if (reflectionProxy)
            {
                var body = reflectionProxy.parent;
                if (body && director.game.player)
                {
                    body.position = delayed.position - Vector3.up * director.game.player.EyeHeight;
                    body.rotation = Quaternion.Euler(0, delayed.rotation.eulerAngles.y, 0);
                }
                float yaw = Mathf.DeltaAngle(reflectionPlane.eulerAngles.y + 180, delayed.rotation.eulerAngles.y);
                reflectionProxy.localRotation = proxyRotation * Quaternion.Euler(
                    Mathf.Clamp(-Mathf.DeltaAngle(0, delayed.rotation.eulerAngles.x), -20, 20), Mathf.Clamp(-yaw, -28, 28), 0);
            }
            renderAge += Time.deltaTime;
            if (renderAge < .1f) return;
            renderAge = 0;
            RenderReflection();
        }

        public void AdvanceObservation(float seconds, bool observing)
        {
            if (Finished || !observing || seconds <= 0) return;
            ObservedSeconds += seconds;
            if (smile) smile.SetActive(ObservedSeconds >= 2.4f);
            Finished = ObservedSeconds >= 3.4f;
        }

        bool IsObserved()
        {
            if (!playerCamera || !surface || !reflectionPlane || !surface.gameObject.activeInHierarchy) return false;
            Vector3 delta = reflectionPlane.position - playerCamera.transform.position;
            if (delta.magnitude > 3.5f || delta.sqrMagnitude < .01f
                || Vector3.Angle(playerCamera.transform.forward, delta) > 30) return false;
            return !Physics.Raycast(playerCamera.transform.position, delta.normalized, out var hit, delta.magnitude - .04f,
                ~(LayerMask.GetMask("Player") | (1 << 30) | (1 << 31)), QueryTriggerInteraction.Ignore)
                || hit.transform == surface.transform || hit.transform.IsChildOf(reflectionPlane);
        }

        void PrepareReflection()
        {
            if (reflectionCamera) return;
            target = new RenderTexture(384, 512, 24, RenderTextureFormat.ARGB32) {
                name = "School mirror — delayed memory", hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 1, useMipMap = false
            };
            if (!target.Create()) throw new InvalidOperationException("Mirror render target unavailable.");
            var cameraObject = new GameObject("School mirror — reflection camera") { hideFlags = HideFlags.HideAndDontSave };
            reflectionCamera = cameraObject.AddComponent<Camera>();
            reflectionCamera.CopyFrom(playerCamera);
            reflectionCamera.enabled = false;
            reflectionCamera.cullingMask = (playerCamera.cullingMask | (1 << 31)) & ~(1 << 30);
            reflectionCamera.targetTexture = target;
            reflectionCamera.aspect = 384f / 512;
            reflectionCamera.nearClipPlane = .04f;
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            originalMaterial = surface.sharedMaterial;
            if (!originalMaterial) throw new InvalidOperationException("Mirror surface material missing.");
            mirrorMaterial = new Material(originalMaterial) { hideFlags = HideFlags.HideAndDontSave };
            mirrorMaterial.SetTexture(mirrorMaterial.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", target);
            surface.sharedMaterial = mirrorMaterial;
        }

        void RenderReflection()
        {
            bool invert = GL.invertCulling;
            try
            {
                PrepareReflection();
                Matrix4x4 reflection = ReflectionMatrix(reflectionPlane.position, reflectionPlane.forward);
                reflectionCamera.transform.SetPositionAndRotation(reflection.MultiplyPoint(delayed.position),
                    Quaternion.LookRotation(reflection.MultiplyVector(delayed.rotation * Vector3.forward),
                        reflection.MultiplyVector(delayed.rotation * Vector3.up)));
                reflectionCamera.worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1, 1, -1))
                    * Matrix4x4.TRS(delayed.position, delayed.rotation, Vector3.one).inverse * reflection;
                reflectionCamera.ResetProjectionMatrix();
                Vector3 planePosition = reflectionCamera.worldToCameraMatrix.MultiplyPoint(reflectionPlane.position + reflectionPlane.forward * .02f);
                Vector3 planeNormal = reflectionCamera.worldToCameraMatrix.MultiplyVector(reflectionPlane.forward).normalized;
                reflectionCamera.projectionMatrix = reflectionCamera.CalculateObliqueMatrix(new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, -Vector3.Dot(planePosition, planeNormal)));
                GL.invertCulling = !invert;
                // Preview requests provide frame setup without changing the live GPU-driven pipeline.
                var request = new RenderPipeline.StandardRequest { destination = target, isPreview = true };
                if (RenderPipeline.SupportsRenderRequest(reflectionCamera, request))
                    RenderPipeline.SubmitRenderRequest(reflectionCamera, request);
                else throw new InvalidOperationException("The current render pipeline cannot render this mirror.");
            }
            catch (Exception error)
            {
                // Keep the last valid reflection instead of breaking the chapter or allocating targets each frame.
                renderingUnavailable = true;
                Debug.LogWarning("School mirror kept its last frame: " + error.Message, this);
            }
            finally { GL.invertCulling = invert; }
        }

        public static Matrix4x4 ReflectionMatrix(Vector3 point, Vector3 normal)
        {
            normal.Normalize();
            float distance = Vector3.Dot(point, normal);
            var matrix = Matrix4x4.identity;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++) matrix[row, col] -= 2 * normal[row] * normal[col];
                matrix[row, 3] = 2 * distance * normal[row];
            }
            return matrix;
        }

        void OnDisable()
        {
            if (surface && mirrorMaterial && surface.sharedMaterial == mirrorMaterial) surface.sharedMaterial = originalMaterial;
            if (mirrorMaterial) Destroy(mirrorMaterial);
            if (reflectionCamera) Destroy(reflectionCamera.gameObject);
            if (target) { target.Release(); Destroy(target); }
            mirrorMaterial = null; reflectionCamera = null; target = null;
            renderingUnavailable = false;
            Restore(Finished);
        }
    }
}
