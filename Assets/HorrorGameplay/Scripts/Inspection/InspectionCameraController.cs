// Posiciona a câmera e controla giro e zoom.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorGame
{
    public sealed class InspectionCameraController : MonoBehaviour
    {
        public Camera Camera { get; private set; }
        public RenderTexture Texture { get; private set; }
        public bool BackVisible => item && Vector3.Dot(item.TransformDirection(Vector3.back), Camera.transform.forward) > .3f;
        public bool DetailVisible => Zoom01 > .65f;
        public float Zoom01 => Mathf.InverseLerp(1.35f, .48f, distance);
        public bool FullscreenZoom { get; private set; }
        public float ZoomPresentation01 => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.40f, .68f, Zoom01));
        public Vector2 PanOffset => panOffset;
        public float LightIntensity { get; set; } = 1;

        Transform item;
        Light key;
        Quaternion desiredRotation;
        Vector2 inertia, panOffset, desiredPan;
        Vector3 entrance;
        Bounds visualBounds;
        float distance = 1.1f, desiredDistance = 1.1f;
        int viewportWidth, viewportHeight, screenWidth, screenHeight;
        bool textureFullscreen, paper, manualRotation;
        GameObject stage;
        public void Open(InspectionItemData data, int layer, Vector2 origin)
        {
            foreach (var c in data.model.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(c is TMPro.TMP_Text))
                    throw new System.InvalidOperationException("Inspection models must contain visuals only: " + c.GetType().Name);
            stage = new GameObject("Inspection stage (temporary)");
            stage.layer = layer;
            stage.transform.SetParent(transform, false);
            stage.transform.position = new Vector3(512, 128, 512);
            var cameraObject = new GameObject("Inspection camera");
            cameraObject.layer = layer;
            cameraObject.transform.SetParent(stage.transform, false);
            Camera = cameraObject.AddComponent<Camera>();
            Camera.transform.localPosition = new Vector3(0, 0, -1.1f);
            Camera.cullingMask = 1 << layer;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Color.clear;
            Camera.nearClipPlane = .025f;
            Camera.farClipPlane = 4;
            Camera.fieldOfView = 32;
            Camera.allowHDR = false;
            Camera.allowMSAA = false;
            Camera.depth = 80;
            var additional = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            additional.renderPostProcessing = false;
            additional.requiresColorOption = CameraOverrideOption.Off;
            additional.requiresDepthOption = CameraOverrideOption.Off;
            additional.renderShadows = true;
            FullscreenZoom = false;
            screenWidth = screenHeight = 0;
            viewportWidth = Mathf.Max(1, Screen.width);
            viewportHeight = Mathf.Max(1, Screen.height);
            RefreshTexture();
            var clone = Instantiate(data.model, stage.transform);
            clone.name = data.title + " (inspection only)";
            foreach (var t in clone.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
            // A visual prefab must never clone a save ID, collider or narrative component.
            foreach (var c in clone.GetComponentsInChildren<Collider>(true))
                Destroy(c);
            foreach (var c in clone.GetComponentsInChildren<Rigidbody>(true))
                Destroy(c);
            item = clone.transform;
            var surface = clone.GetComponent<MeshRenderer>();
            if (surface && surface.sharedMaterials.Length >= 2)
            {
                if (data.frontTexture && surface.sharedMaterials[0].GetTexture("_BaseMap") != data.frontTexture)
                {
                    var properties = new MaterialPropertyBlock();
                    properties.SetTexture("_BaseMap", data.frontTexture);
                    properties.SetVector("_BaseMap_ST", new Vector4(1, 1, 0, 0));
                    surface.SetPropertyBlock(properties, 0);
                }

                if (data.backTexture && surface.sharedMaterials[1].GetTexture("_BaseMap") != data.backTexture)
                {
                    var properties = new MaterialPropertyBlock();
                    properties.SetTexture("_BaseMap", data.backTexture);
                    properties.SetVector("_BaseMap_ST", new Vector4(1, 1, 0, 0));
                    surface.SetPropertyBlock(properties, 1);
                }
            }

            item.localPosition = Vector3.zero;
            item.localRotation = Quaternion.identity;
            desiredRotation = Quaternion.identity;
            inertia = Vector2.zero;
            panOffset = desiredPan = Vector2.zero;
            paper = data.frontTexture || data.backTexture;
            manualRotation = false;
            visualBounds = FindVisualBounds();
            distance = desiredDistance = 1.1f;
            entrance = new Vector3((origin.x - .5f) * .8f, (origin.y - .5f) * .8f - .16f, .25f);
            key = CreateLight("Inspection key", LightType.Directional, new Vector3(30, -25, 0), 1.25f, layer);
            key.shadows = LightShadows.Soft;
            CreateLight("Inspection fill", LightType.Directional, new Vector3(-20, 155, 0), .35f, layer);
        }

        public void SetViewportSize(int width, int height)
        {
            if (width <= 0 || height <= 0 || !Camera)
                return;
            viewportWidth = width;
            viewportHeight = height;
            RefreshTexture();
        }

        void RefreshTexture()
        {
            int width = viewportWidth, height = viewportHeight;
            if (screenWidth == width && screenHeight == height && textureFullscreen == FullscreenZoom)
                return;
            float scale = FullscreenZoom ? Mathf.Min(1, 2048f / Mathf.Max(width, height)) : Mathf.Min(1, 2048f / Mathf.Max(width, height), Mathf.Sqrt(1024f * 1024 / ((float)width * height)));
            int textureWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            int textureHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            Camera.aspect = (float)width / height;
            if (!Texture || Texture.width != textureWidth || Texture.height != textureHeight)
            {
                var replacement = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Inspection item",
                    antiAliasing = 1
                };
                if (!replacement.Create())
                {
                    Destroy(replacement);
                    throw new System.InvalidOperationException("Could not allocate the inspection render texture.");
                }

                var previous = Texture;
                Texture = replacement;
                Camera.targetTexture = Texture;
                if (previous)
                {
                    previous.Release();
                    Destroy(previous);
                }
            }

            screenWidth = width;
            screenHeight = height;
            textureFullscreen = FullscreenZoom;
        }

        Bounds FindVisualBounds()
        {
            Bounds bounds = default;
            bool found = false;
            foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponent<TMPro.TMP_Text>())
                    continue;
                var local = renderer.localBounds;
                var matrix = item.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (found)
                        bounds.Encapsulate(point);
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                }
            }

            return bounds;
        }

        Light CreateLight(string name, LightType type, Vector3 angles, float intensity, int layer)
        {
            var g = new GameObject(name);
            g.layer = layer;
            g.transform.SetParent(stage.transform, false);
            g.transform.localEulerAngles = angles;
            var light = g.AddComponent<Light>();
            light.type = type;
            light.intensity = intensity;
            light.color = new Color(1, .94f, .84f);
            light.cullingMask = 1 << layer;
            return light;
        }

        public void Animate(float alpha)
        {
            if (!item)
                return;
            float ease = alpha * alpha * (3 - 2 * alpha);
            item.localPosition = Vector3.Lerp(entrance, Vector3.zero, ease);
            item.localScale = Vector3.one * Mathf.Lerp(.035f, 1, ease);
        }

        public void Drag(Vector2 delta)
        {
            if (!float.IsFinite(delta.x) || !float.IsFinite(delta.y))
                return;
            if (FullscreenZoom)
            {
                Pan(delta);
                return;
            }

            if (delta == Vector2.zero)
                return;
            manualRotation = true;
            inertia = Vector2.ClampMagnitude(delta, 80);
            Rotate(inertia);
        }

        public void Pan(Vector2 pixelDelta)
        {
            if (!FullscreenZoom || !Camera || !float.IsFinite(pixelDelta.x) || !float.IsFinite(pixelDelta.y))
                return;
            float unitsPerPixel = 2 * distance * Mathf.Tan(Camera.fieldOfView * .5f * Mathf.Deg2Rad) / screenHeight;
            desiredPan -= new Vector2(Mathf.Clamp(pixelDelta.x, -screenWidth, screenWidth), Mathf.Clamp(pixelDelta.y, -screenHeight, screenHeight)) * unitsPerPixel;
            ClampPan();
        }

        void ClampPan()
        {
            var matrix = Matrix4x4.TRS(item.localPosition, item.localRotation, item.localScale);
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity), max = -min;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = matrix.MultiplyPoint3x4(visualBounds.center + Vector3.Scale(visualBounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }

            float halfHeight = Mathf.Max(Camera.nearClipPlane, distance + min.z) * Mathf.Tan(Camera.fieldOfView * .5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * Camera.aspect;
            // Keep zero reachable, and stop at the paper edge instead of panning into empty space.
            desiredPan.x = Mathf.Clamp(desiredPan.x, Mathf.Min(0, min.x + halfWidth), Mathf.Max(0, max.x - halfWidth));
            desiredPan.y = Mathf.Clamp(desiredPan.y, Mathf.Min(0, min.y + halfHeight), Mathf.Max(0, max.y - halfHeight));
        }

        void Rotate(Vector2 delta) => desiredRotation = Quaternion.AngleAxis(-delta.x * .25f, Vector3.up) * Quaternion.AngleAxis(delta.y * .25f, Vector3.right) * desiredRotation;
        public void Flip()
        {
            desiredRotation = Quaternion.AngleAxis(180, Vector3.up) * desiredRotation;
            inertia = Vector2.zero;
            desiredPan = Vector2.zero;
        }

        public void Zoom(float delta)
        {
            if (float.IsFinite(delta))
                desiredDistance = Mathf.Clamp(desiredDistance - delta * .12f, .48f, 1.35f);
        }

        public void ResetZoom()
        {
            desiredDistance = 1.1f;
            desiredPan = Vector2.zero;
        }

        public void Advance(float dt, bool dragging)
        {
            if (!item || !Camera || !float.IsFinite(dt) || dt <= 0)
                return;
            distance = Mathf.Lerp(distance, desiredDistance, 1 - Mathf.Exp(-dt * 14));
            bool previousFullscreen = FullscreenZoom;
            if (FullscreenZoom)
            {
                if (Zoom01 <= .48f)
                    FullscreenZoom = false;
            }
            else if (Zoom01 >= .68f)
                FullscreenZoom = true;
            if (FullscreenZoom != previousFullscreen)
                inertia = Vector2.zero;
            RefreshTexture();
            if (!dragging && !FullscreenZoom)
            {
                inertia *= Mathf.Exp(-dt * 14);
                Rotate(inertia * dt * 30);
            }

            if (paper && !manualRotation && ZoomPresentation01 > 0)
            {
                var back = Quaternion.AngleAxis(180, Vector3.up);
                desiredRotation = Quaternion.Angle(desiredRotation, Quaternion.identity) < Quaternion.Angle(desiredRotation, back) ? Quaternion.identity : back;
            }

            item.localRotation = Quaternion.Slerp(item.localRotation, desiredRotation, 1 - Mathf.Exp(-dt * 18));
            if (FullscreenZoom)
                ClampPan();
            else
                desiredPan = Vector2.zero;
            panOffset = Vector2.Lerp(panOffset, desiredPan, 1 - Mathf.Exp(-dt * 18));
            Camera.transform.localPosition = new Vector3(panOffset.x, panOffset.y, -distance);
            if (key)
                key.intensity = 1.25f * LightIntensity;
        }

        public void SetVisible(bool value)
        {
            if (Camera)
                Camera.enabled = value;
        }

        public void Close()
        {
            if (Camera)
            {
                Camera.enabled = false;
                Camera.targetTexture = null;
            }

            if (Texture)
            {
                Texture.Release();
                Destroy(Texture);
            }

            if (stage)
                Destroy(stage);
            Camera = null;
            Texture = null;
            item = null;
            stage = null;
            FullscreenZoom = false;
            panOffset = desiredPan = Vector2.zero;
        }

        void OnDestroy() => Close();
    }
}
