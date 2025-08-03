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
    private RTHandle writeRT;
    private RTHandle historyRT;
    private CloudsRenderFeature.CloudsRenderFeatureSettings settings;

    public CloudsRenderPass(
        Material renderMat, CloudsRenderFeature.CloudsRenderFeatureSettings settings)
    {
        this.renderMat = renderMat;
        this.settings = settings;
    }

    private void UpdateMaterial()
    {
        renderMat.SetInt("_FrameCount", Time.frameCount);

        renderMat.SetTexture("_CloudBase", settings.cloudBase);
        renderMat.SetTexture("_CloudDetail", settings.cloudDetail);
        renderMat.SetTexture("_CurlNoise", settings.curlNoise);
        renderMat.SetTexture("_WeatherData", settings.weatherData);

        renderMat.SetFloat("_DensityThreshold", settings.densityThreshold);
        renderMat.SetFloat("_HighFreqNoiseStrength", settings.highFreqNoiseStrength);
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

        // var customData = frameData.Create<CloudsRenderData>();

        TextureHandle cameraColorTex = resourceData.activeColorTexture;
        TextureHandle cameraDepthTex = resourceData.activeDepthTexture;

        TextureHandle writeTex = renderGraph.ImportTexture(writeRT);
        TextureHandle historyTex = renderGraph.ImportTexture(historyRT);

        // TextureDesc cloudsRenderTexDesc = cameraColorTex.GetDescriptor(renderGraph);
        // cloudsRenderTexDesc.name = renderTexName;
        // cloudsRenderTexDesc.width /= 2;
        // cloudsRenderTexDesc.height /= 2;
        // cloudsRenderTexDesc.depthBufferBits = 0;
        // cloudsRenderTexDesc.colorFormat = GraphicsFormat.R16G16B16A16_UNorm;

        // TextureHandle cloudsRenderTex = renderGraph.CreateTexture(cloudsRenderTexDesc);
        // customData.cloudsRenderTex = cloudsRenderTex;

        UpdateMaterial();

        RenderGraphUtils.BlitMaterialParameters blitParams = new(historyTex, writeTex, renderMat, 0);
        renderGraph.AddBlitPass(blitParams, renderPassName);
    }
} 