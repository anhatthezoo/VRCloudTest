using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

class CloudsBlitPass : ScriptableRenderPass
{
    private class PassData
    {
        public TextureHandle cloudsRenderTex;
        public TextureHandle srcCamTex;
    }

    private Material blitMaterial;
    private const string tempPassName = "Volumetric Clouds Temp Blit Pass";
    private const string finalPassName = "Volumetric Clouds Final Blit Pass";

    public CloudsBlitPass(Material blitMaterial)
    {
        this.blitMaterial = blitMaterial;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        if (resourceData.isActiveTargetBackBuffer)
        {
            return;
        }

        if (!frameData.Contains<CloudsRenderPass.CloudsRenderPassData>())
        {
            return;
        }

        CloudsRenderPass.CloudsRenderPassData customData = frameData.Get<CloudsRenderPass.CloudsRenderPassData>();
        TextureHandle cloudsRenderTex = customData.cloudsRenderTex;
        TextureHandle srcCamTex = resourceData.activeColorTexture;

        if (!srcCamTex.IsValid() || !cloudsRenderTex.IsValid())
        {
            return;
        }

        TextureDesc tempTexDesc = srcCamTex.GetDescriptor(renderGraph);
        tempTexDesc.name = "Volumetric Clouds Blit Temp Texture";
        tempTexDesc.depthBufferBits = 0;
        TextureHandle tempTex = renderGraph.CreateTexture(tempTexDesc);

        using (var builder = renderGraph.AddRasterRenderPass<PassData>(tempPassName, out var passData, profilingSampler))
        {
            // We have to draw to a temporary texture and blit the temp texture to the screen at the end since we 
            // have to use builder.UseTexture (read only)
            builder.UseTexture(cloudsRenderTex);
            builder.UseTexture(srcCamTex);
            passData.cloudsRenderTex = cloudsRenderTex;
            passData.srcCamTex = srcCamTex;

            builder.SetRenderAttachment(tempTex, 0);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
        }

        renderGraph.AddBlitPass(tempTex, srcCamTex, Vector2.one, Vector2.zero, passName: finalPassName);
    }

    void ExecutePass(PassData data, RasterGraphContext context)
    {
        blitMaterial.SetTexture("_CameraTex", data.srcCamTex);
        Blitter.BlitTexture(context.cmd, data.cloudsRenderTex, new Vector4(1, 1, 0, 0), blitMaterial, 0);
    }
}