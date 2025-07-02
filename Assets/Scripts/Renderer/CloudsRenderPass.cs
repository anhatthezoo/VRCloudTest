using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using System.Collections.Generic;


class CloudsRenderPass : ScriptableRenderPass
{
    private class PassData
    {
        public RendererListHandle rendererListHandle;
    }

    public class CloudsRenderPassData : ContextItem
    {
        public TextureHandle cloudsRenderTex;

        public override void Reset()
        {
            cloudsRenderTex = TextureHandle.nullHandle;
        }
    }

    private LayerMask cloudsLayer;
    private LayerMask defaultCamMask;
    private List<ShaderTagId> shaderTagIds = new List<ShaderTagId>();

    public CloudsRenderPass(LayerMask cloudsLayer)
    {
        this.cloudsLayer = cloudsLayer;
    }

    private void InitRendererList(CullingResults cullResults, ContextContainer frameData, ref PassData passData, RenderGraph renderGraph)
    {
        UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        UniversalLightData lightData = frameData.Get<UniversalLightData>();

        var sortFlags = cameraData.defaultOpaqueSortFlags;
        var renderQueueRange = RenderQueueRange.all;
        var filterSettings = new FilteringSettings(renderQueueRange, cloudsLayer);
        
        var forwardOnlyShaderTagIds = new ShaderTagId[]
        {
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("LightweightForward")
        };

        shaderTagIds.Clear();

        foreach (ShaderTagId sid in forwardOnlyShaderTagIds)
        {
            shaderTagIds.Add(sid);
        }

        var drawSettings = RenderingUtils.CreateDrawingSettings(shaderTagIds, universalRenderingData, cameraData, lightData, sortFlags);
        var param = new RendererListParams(cullResults, drawSettings, filterSettings);
        passData.rendererListHandle = renderGraph.CreateRendererList(param);

        cameraData.camera.cullingMask = defaultCamMask;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        const string passName = "Volumetric Clouds Pass";

        using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
            {
                return;
            }

            CloudsRenderPassData customData = frameData.Create<CloudsRenderPassData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            CullContextData cullContextData = frameData.Get<CullContextData>();

            defaultCamMask = cameraData.camera.cullingMask;
            cameraData.camera.cullingMask = cloudsLayer;

            cameraData.camera.TryGetCullingParameters(out var cullingParameters);
            var cullingResults = cullContextData.Cull(ref cullingParameters);

            InitRendererList(cullingResults, frameData, ref passData, renderGraph);

            if (!passData.rendererListHandle.IsValid())
            {
                return;
            }

            builder.UseRendererList(passData.rendererListHandle);

            TextureHandle srcCamTex = resourceData.activeColorTexture;
            TextureDesc cloudsRenderTexDesc = srcCamTex.GetDescriptor(renderGraph);
            cloudsRenderTexDesc.name = "Clouds Render Texture";
            cloudsRenderTexDesc.depthBufferBits = 0;
            cloudsRenderTexDesc.width /= 4;
            cloudsRenderTexDesc.height /= 4;
            cloudsRenderTexDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
            TextureHandle cloudsRenderTex = renderGraph.CreateTexture(cloudsRenderTexDesc);

            customData.cloudsRenderTex = cloudsRenderTex;

            builder.SetRenderAttachment(cloudsRenderTex, 0);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
        }
    }

    static void ExecutePass(PassData data, RasterGraphContext context)
    {
        context.cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1, 0);
        context.cmd.DrawRendererList(data.rendererListHandle);
    }
}