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
        public TextureHandle writeTex;
        public TextureHandle historyTex;
        public TextureHandle cameraColorTex;
    }

    private const string tempPassName = "Volumetric Clouds Temp Blit Pass";
    private const string finalPassName = "Volumetric Clouds Final Blit Pass";
    private const string tempTexName = "Volumetric Clouds Blit Temp Texture";
    private Material blitMat;
    private RTHandle writeRT;
    private RTHandle historyRT;

    public CloudsBlitPass(Material blitMat)
    {
        this.blitMat = blitMat;
    }

    public void SetRenderTargets(RTHandle writeRT, RTHandle historyRT)
    {
        this.writeRT = writeRT;
        this.historyRT = historyRT;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

        if (resourceData.isActiveTargetBackBuffer)
        {
            return;
        }

        // if (!frameData.Contains<CloudsRenderPass.CloudsRenderData>())
        // {
        //     return;
        // }

        // CloudsRenderPass.CloudsRenderData renderData = frameData.Get<CloudsRenderPass.CloudsRenderData>();

        // TextureHandle cloudsRenderTex = renderData.cloudsRenderTex;
        TextureHandle cameraColorTex = resourceData.activeColorTexture;
        TextureHandle writeTex = renderGraph.ImportTexture(writeRT);
        TextureHandle historyTex = renderGraph.ImportTexture(historyRT);

        if (!cameraColorTex.IsValid() || !writeTex.IsValid())
        {
            return;
        }

        TextureDesc tempTexDesc = cameraColorTex.GetDescriptor(renderGraph);
        tempTexDesc.name = tempTexName;
        tempTexDesc.depthBufferBits = 0;
        TextureHandle tempTex = renderGraph.CreateTexture(tempTexDesc);

        using (var builder = renderGraph.AddRasterRenderPass<PassData>(tempPassName, out var passData, profilingSampler))
        {
            builder.UseTexture(writeTex);
            builder.UseTexture(historyTex);
            builder.UseTexture(cameraColorTex);

            passData.writeTex = writeTex;
            passData.historyTex = historyTex;
            passData.cameraColorTex = cameraColorTex;

            builder.SetRenderAttachment(tempTex, 0);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
        }

        renderGraph.AddBlitPass(tempTex, cameraColorTex, Vector2.one, Vector2.zero, passName: finalPassName);

        // RenderGraphUtils.BlitMaterialParameters blitParams = new(writeTex, cameraColorTex, blitMat, 0);
        // renderGraph.AddBlitPass(blitParams, finalPassName);
    }

    void ExecutePass(PassData data, RasterGraphContext context)
    {
        blitMat.SetTexture("_CameraColor", data.cameraColorTex);
        blitMat.SetTexture("_HistoryTex", data.historyTex);
        Blitter.BlitTexture(context.cmd, data.writeTex, new Vector4(1, 1, 0, 0), blitMat, 0);
    }
}
