// Desenha o pulso e os contornos pelo pipeline URP.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HorrorGame
{
    public sealed class WhistlePulseRendererFeature : ScriptableRendererFeature
    {
        public Shader shader;
        Material material;
        PulsePass pass;
        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (shader)
                material = CoreUtils.CreateEngineMaterial(shader);
            pass = new PulsePass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing,
                requiresIntermediateTexture = true
            };
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            var c = data.cameraData;
            if (!material || c.renderType != CameraRenderType.Base || c.cameraType != CameraType.Game)
                return;
            var sense = c.camera.GetComponent<WhistleSenseCamera>();
            if (!sense || !sense.Active)
                return;
            pass.material = material;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);
        sealed class PulsePass : ScriptableRenderPass
        {
            public Material material;
            sealed class Data
            {
                public TextureHandle source, depth;
                public Material material;
                public Vector4 origin, ring;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                var camera = frame.Get<UniversalCameraData>().camera;
                var sense = camera.GetComponent<WhistleSenseCamera>();
                if (!sense || !sense.Active || resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid())
                    return;
                var s = sense.owner;
                var desc = graph.GetTextureDesc(resources.activeColorTexture);
                desc.name = "Whistle depth pulse colour";
                desc.clearBuffer = false;
                desc.depthBufferBits = DepthBits.None;
                desc.msaaSamples = MSAASamples.None;
                desc.bindTextureMS = false;
                var dest = graph.CreateTexture(desc);
                using (var builder = graph.AddRasterRenderPass<Data>("Whistle school depth ring", out var data))
                {
                    data.source = resources.activeColorTexture;
                    data.depth = resources.cameraDepthTexture;
                    data.material = material;
                    float elapsed = s.config.effectDuration - s.VisualAge;
                    float progress = Mathf.Clamp01(elapsed / Mathf.Max(.6f, s.config.pulseExpansionSeconds));
                    var p = s.PulseOrigin;
                    data.origin = new Vector4(p.x, p.y, p.z, progress * s.config.range);
                    data.ring = new Vector4(.65f, s.VisualEnvelope * (1 - progress) * .5f, 0, 0);
                    builder.UseTexture(data.source);
                    builder.UseTexture(data.depth);
                    builder.SetRenderAttachment(dest, 0);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((Data d, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture("_WhistleDepth", d.depth);
                        context.cmd.SetGlobalVector("_WhistleOrigin", d.origin);
                        context.cmd.SetGlobalVector("_WhistleRing", d.ring);
                        Blitter.BlitTexture(context.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 0);
                    });
                }

                resources.cameraColor = dest;
            }
        }
    }
}
