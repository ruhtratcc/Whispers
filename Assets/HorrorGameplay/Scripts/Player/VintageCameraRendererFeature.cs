// Aplica o filtro de imagem no pipeline URP.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace HorrorGame
{
    /// <summary>No URP 17 e Unity 6, aplica o efeito somente às câmeras principais marcadas.</summary>
    public sealed class VintageCameraRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] public Shader shader;
        VintagePass pass;

        public override void Create()
        {
            pass = new VintagePass(this)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
                requiresIntermediateTexture = true
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || !shader || !shader.isSupported) return;
            var cameraData = renderingData.cameraData;
            if (cameraData.renderType != CameraRenderType.Base || cameraData.cameraType != CameraType.Game) return;
            var effect = cameraData.camera.GetComponent<VintageCameraEffect>();
            if (!effect || effect.EffectiveIntensity <= .0001f) return;
            renderer.EnqueuePass(pass);
        }

        sealed class VintagePass : ScriptableRenderPass
        {
            readonly VintageCameraRendererFeature owner;
            public VintagePass(VintageCameraRendererFeature owner) => this.owner = owner;

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var camera = frameData.Get<UniversalCameraData>();
                var resources = frameData.Get<UniversalResourceData>();
                var effect = camera.camera.GetComponent<VintageCameraEffect>();
                if (!effect || effect.EffectiveIntensity <= .0001f || resources.isActiveTargetBackBuffer) return;
                var material = effect.GetMaterial(owner.shader);
                var source = resources.activeColorTexture;
                if (!material || !source.IsValid()) return;

                // Mantém as configurações de realidade virtual, resolução dinâmica e formato da imagem.
                var descriptor = renderGraph.GetTextureDesc(source);
                descriptor.name = "Whispers Vintage Camera Color";
                descriptor.clearBuffer = false;
                descriptor.depthBufferBits = DepthBits.None;
                descriptor.msaaSamples = MSAASamples.None;
                descriptor.bindTextureMS = false;
                var destination = renderGraph.CreateTexture(descriptor);
                var size = renderGraph.GetRenderTargetInfo(source);
                var blit = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0)
                {
                    propertyBlock = effect.GetParameters(size.width, size.height),
                    sourceTexturePropertyID = Shader.PropertyToID("_BlitTexture")
                };
                renderGraph.AddBlitPass(blit, "Whispers analogue camera");
                // Aplica o efeito depois do ajuste de cor. A interface fica fora do efeito de fita.
                resources.cameraColor = destination;
            }
        }
    }
}
