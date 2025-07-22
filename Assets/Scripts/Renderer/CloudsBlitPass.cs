using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine;

public class CloudsBlitPass : ScriptableRenderPass
{
    private class PassData
    {
        public TextureHandle cloudsRenderTex;
        public TextureHandle cameraColorTex;
    }

    private const string tempPassName = "Volumetric Clouds Temp Blit Pass";
    private const string finalPassName = "Volumetric Clouds Final Blit Pass";
    private const string tempTexName = "Volumetric Clouds Blit Temp Texture";
    private Material blitMat;

    public CloudsBlitPass(Material blitMat)
    {
        this.blitMat = blitMat;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

        if (resourceData.isActiveTargetBackBuffer)
        {
            return;
        }

        if (!frameData.Contains<CloudsRenderPass.CloudsRenderData>())
        {
            return;
        }

        CloudsRenderPass.CloudsRenderData renderData = frameData.Get<CloudsRenderPass.CloudsRenderData>();

        TextureHandle cloudsRenderTex = renderData.cloudsRenderTex;
        TextureHandle cameraColorTex = resourceData.activeColorTexture;

        if (!cameraColorTex.IsValid() || !cloudsRenderTex.IsValid())
        {
            return;
        }

        TextureDesc tempTexDesc = cameraColorTex.GetDescriptor(renderGraph);
        tempTexDesc.name = tempTexName;
        tempTexDesc.depthBufferBits = 0;
        TextureHandle tempTex = renderGraph.CreateTexture(tempTexDesc);

        using (var builder = renderGraph.AddRasterRenderPass<PassData>(tempPassName, out var passData, profilingSampler))
        {
            // We have to draw to a temporary texture and blit the temp texture to the screen at the end since we 
            // have to use builder.UseTexture (read only)
            builder.UseTexture(cloudsRenderTex);
            builder.UseTexture(cameraColorTex);
            passData.cloudsRenderTex = cloudsRenderTex;
            passData.cameraColorTex = cameraColorTex;

            builder.SetRenderAttachment(tempTex, 0);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
        }

        renderGraph.AddBlitPass(tempTex, cameraColorTex, Vector2.one, Vector2.zero, passName: finalPassName);
    }

    void ExecutePass(PassData data, RasterGraphContext context)
    {
        blitMat.SetTexture("_CameraColor", data.cameraColorTex);
        Blitter.BlitTexture(context.cmd, data.cloudsRenderTex, new Vector4(1, 1, 0, 0), blitMat, 0);
    }
}
