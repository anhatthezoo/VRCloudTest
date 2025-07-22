using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine;

public class CloudsRenderPass : ScriptableRenderPass
{
    private class PassData
    {

    }

    public class CloudsRenderData : ContextItem
    {
        public TextureHandle cloudsRenderTex;

        public override void Reset()
        {
            cloudsRenderTex = TextureHandle.nullHandle;
        }
    }

    private const string renderPassName = "Volumetric Clouds Render Pass";
    private const string renderTexName = "Volumetric Clouds Render Texture";
    private Material renderMat;
    private Texture3D cloudBase;
    private Texture3D cloudDetail;
    private Texture2D curlNoise;
    private Texture2D densityLUT;
    private Texture2D weatherData;

    public CloudsRenderPass(
        Material renderMat,
        Texture3D cloudBase, Texture3D cloudDetail, Texture2D curlNoise,
        Texture2D densityLUT, Texture2D weatherData)
    {
        this.renderMat = renderMat;
        this.cloudBase = cloudBase;
        this.cloudDetail = cloudDetail;
        this.curlNoise = curlNoise;
        this.densityLUT = densityLUT;
        this.weatherData = weatherData;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

        if (resourceData.isActiveTargetBackBuffer)
        {
            return;
        }

        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        var customData = frameData.Create<CloudsRenderData>();

        TextureHandle cameraColorTex = resourceData.activeColorTexture;
        TextureHandle cameraDepthTex = resourceData.activeDepthTexture;

        TextureDesc cloudsRenderTexDesc = cameraColorTex.GetDescriptor(renderGraph);
        cloudsRenderTexDesc.name = renderTexName;
        cloudsRenderTexDesc.width /= 2;
        cloudsRenderTexDesc.height /= 2;
        cloudsRenderTexDesc.depthBufferBits = 0;
        cloudsRenderTexDesc.colorFormat = GraphicsFormat.R16G16B16A16_UNorm;
        // cloudsRenderTexDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;

        TextureHandle cloudsRenderTex = renderGraph.CreateTexture(cloudsRenderTexDesc);
        customData.cloudsRenderTex = cloudsRenderTex;

        renderMat.SetTexture("_CloudBase", cloudBase);
        renderMat.SetTexture("_CloudDetail", cloudDetail);
        renderMat.SetTexture("_CurlNoise", curlNoise);
        renderMat.SetTexture("_DensityLUT", densityLUT);
        renderMat.SetTexture("_WeatherData", weatherData);

        RenderGraphUtils.BlitMaterialParameters blitParams = new(cameraDepthTex, cloudsRenderTex, renderMat, 0);
        renderGraph.AddBlitPass(blitParams, renderPassName);
    }
} 