using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System;
using UnityEngine.Experimental.Rendering;

public class CloudsRenderFeature : ScriptableRendererFeature
{
    [Serializable]
    public class CloudsRenderFeatureSettings
    {
        [Header("Core")]
        public Shader renderShader;
        public Shader blitShader;
        public Texture3D cloudBase;
        public Texture3D cloudDetail;
        public Texture2D curlNoise;
        public Texture2D weatherData;

        [Header("Tweaks")]
        [Range(0.0f, 1.0f)]
        public float densityThreshold = 0.01f;

        [Range(0.0f, 0.05f)]
        public float highFreqNoiseStrength = 0.0002f;
    }

    [SerializeField]
    private CloudsRenderFeatureSettings settings;

    private CloudsRenderPass cloudsRenderPass;
    private CloudsBlitPass cloudsBlitPass;
    private Material cloudsRenderMat;
    private Material cloudsBlitMat;
    private RTHandle[] cloudRTs = new RTHandle[2];
    private int currentRTIdx = 0;


    public override void Create()
    {
        cloudsRenderMat = new Material(settings.renderShader);
        cloudsRenderPass = new CloudsRenderPass(cloudsRenderMat, settings);

        cloudsBlitMat = new Material(settings.blitShader);
        cloudsBlitPass = new CloudsBlitPass(cloudsBlitMat);

        cloudsRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
        cloudsBlitPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        RenderTextureDescriptor cloudsRTDesc = renderingData.cameraData.cameraTargetDescriptor;
        cloudsRTDesc.width /= 2;
        cloudsRTDesc.height /= 2;
        cloudsRTDesc.depthBufferBits = 0;
        cloudsRTDesc.colorFormat = RenderTextureFormat.ARGB32;

        RenderingUtils.ReAllocateHandleIfNeeded(ref cloudRTs[0], cloudsRTDesc, name: "_CloudRT_0");
        RenderingUtils.ReAllocateHandleIfNeeded(ref cloudRTs[1], cloudsRTDesc, name: "_CloudRT_1");

        cloudsBlitPass.ConfigureInput(ScriptableRenderPassInput.Motion);
        cloudsBlitPass.SetRenderTargets(cloudRTs[currentRTIdx], cloudRTs[(currentRTIdx + 1) % 2]);
        // cloudsBlitPass.ConfigureInput(ScriptableRenderPassInput.Color);
        cloudsRenderPass.SetRenderTargets(cloudRTs[currentRTIdx], cloudRTs[(currentRTIdx + 1) % 2]);

        renderer.EnqueuePass(cloudsRenderPass);
        renderer.EnqueuePass(cloudsBlitPass);

        currentRTIdx = (currentRTIdx + 1) % 2;
    }

    protected override void Dispose(bool disposing)
    {
        cloudRTs[0]?.Release();
        cloudRTs[1]?.Release();

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
