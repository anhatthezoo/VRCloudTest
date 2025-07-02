using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System;

public class CloudsRenderFeature : ScriptableRendererFeature
{
    [Serializable]
    private class CloudsRenderFeatureSettings
    {
        public LayerMask cloudsLayer;
        public Shader blitShader;
        public Shader blitShaderPancake;
    }

    private CloudsRenderPass cloudsPass;
    private Material blitMaterial;
    private CloudsBlitPass blitPass;

    [SerializeField]
    CloudsRenderFeatureSettings settings;

    /// <inheritdoc/>
    public override void Create()
    {
        cloudsPass = new CloudsRenderPass(settings.cloudsLayer);

        if (UnityEngine.XR.XRSettings.enabled)
        {
            blitMaterial = new Material(settings.blitShader);
        }
        else
        {
            blitMaterial = new Material(settings.blitShaderPancake);
        }

        blitPass = new CloudsBlitPass(blitMaterial);

        cloudsPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
        blitPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(cloudsPass);
        renderer.EnqueuePass(blitPass);
    }

    protected override void Dispose(bool disposing)
    {
        if (Application.isPlaying)
        {
            Destroy(blitMaterial);
        }
        else
        {
            DestroyImmediate(blitMaterial);
        }
    }
}
