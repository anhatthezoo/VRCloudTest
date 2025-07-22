using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System;

public class CloudsRenderFeature : ScriptableRendererFeature
{
    [Serializable]
    public class CloudsRenderFeatureSettings
    {
        public Shader renderShader;
        public Shader blitShader;
        public Texture3D cloudBase;
        public Texture3D cloudDetail;
        public Texture2D curlNoise;
        public Texture2D densityLUT;
        public Texture2D weatherData;
    }

    [SerializeField]
    private CloudsRenderFeatureSettings settings;

    private CloudsRenderPass cloudsRenderPass;
    private CloudsBlitPass cloudsBlitPass;
    private Material cloudsRenderMat;
    private Material cloudsBlitMat;


    public override void Create()
    {
        cloudsRenderMat = new Material(settings.renderShader);
        cloudsRenderPass = new CloudsRenderPass(
            cloudsRenderMat,
            settings.cloudBase,
            settings.cloudDetail,
            settings.curlNoise,
            settings.densityLUT,
            settings.weatherData
        );

        cloudsBlitMat = new Material(settings.blitShader);
        cloudsBlitPass = new CloudsBlitPass(cloudsBlitMat);

        cloudsRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
        cloudsBlitPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(cloudsRenderPass);
        renderer.EnqueuePass(cloudsBlitPass);
    }

    protected override void Dispose(bool disposing)
    {
        if (Application.isPlaying)
        {
            Destroy(cloudsRenderMat);
            Destroy(cloudsBlitMat);
        }
        else
        {
            DestroyImmediate(cloudsRenderMat);
            DestroyImmediate(cloudsBlitMat);
        }
    }
}
