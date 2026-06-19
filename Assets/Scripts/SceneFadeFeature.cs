using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP 17 (Unity 6) Renderer Feature: full-screen alpha fade via Render Graph.
/// Reads _FadeProgress (global float set by SceneFadeBroadcaster) and multiplies
/// every pixel's alpha so the whole virtual scene fades in/out over passthrough.
///
/// Setup:
///   1. Create a Material → assign Hidden/SceneFade shader.
///   2. Open PC_Renderer / Mobile_Renderer asset.
///   3. Add Renderer Feature → SceneFadeFeature, drag in the material.
/// </summary>
public class SceneFadeFeature : ScriptableRendererFeature
{
    [Tooltip("Material using the Hidden/SceneFade shader.")]
    public Material fadeMaterial;

    [Tooltip("When in the frame the blit runs.")]
    public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

    FadePass _pass;

    public override void Create()
    {
        _pass = new FadePass(injectionPoint);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (fadeMaterial == null) return;
        if (renderingData.cameraData.cameraType == CameraType.SceneView) return;

        _pass.material = fadeMaterial;
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing) { }

    // ─────────────────────────────────────────────────────────────────────────

    class FadePass : ScriptableRenderPass
    {
        public Material material;

        class PassData
        {
            public Material      material;
            public TextureHandle source;
            public TextureHandle temp;
        }

        public FadePass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData   = frameData.Get<UniversalCameraData>();

            TextureHandle source = resourceData.activeColorTexture;

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "_SceneFadeTemp", false);

            using (var builder = renderGraph.AddUnsafePass<PassData>("SceneFade", out var passData))
            {
                passData.material = material;
                passData.source   = source;
                passData.temp     = temp;

                builder.UseTexture(source, AccessFlags.ReadWrite);
                builder.UseTexture(temp,   AccessFlags.ReadWrite);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
                {
                    // GetNativeCommandBuffer lets us use classic cmd.Blit inside a render graph pass
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                    // cmd.Blit sets _MainTex → SceneFade shader multiplies alpha by _FadeProgress
                    cmd.Blit(data.source, data.temp, data.material, 0);
                    cmd.Blit(data.temp,   data.source);
                });
            }
        }
    }
}
