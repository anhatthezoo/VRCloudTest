using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CloudRenderFeature : ScriptableRendererFeature
{
    [Serializable]
    public class CloudRenderSettings
    {
        public GameObject controllerObj;
        public ComputeShader shader;
        [Header("Base")]
        public Texture3D cloudBase;
        [Range(0.0f, 1.0f)]
        public float densityThreshold = 0.0f;
        [Header("Detail")]
        public Texture3D cloudDetail;
        public Texture2D curlNoise;
        [Range(0.0f, 0.05f)]
        public float highFreqNoiseStrength = 0.0002f;
        [Header("Weather")]
        public Texture2D weatherMap;
        [Range(0.0f, 2.0f)]
        public float coverageMultiplier = 1.0f;
    }

    public class CloudRTHandles
    {
        public RTHandle baseRTHandle;
        public RTHandle detailRTHandle;
        public RTHandle curlRTHandle;
        public RTHandle weatherRTHandle;
    }

    [SerializeField]
    private CloudRenderSettings settings;
    private CloudRTHandles rtHandles;
    private CloudRenderPass renderPass;

    public override void Create()
    {
        Debug.Log("[CloudRenderFeature]: Initializing");
        if (!settings.controllerObj || !settings.shader)
        {
            Debug.LogWarning("[CloudRenderFeature]: Missing controller or shader. Aborting.");
            return;
        }

        if (!settings.cloudBase)
        {
            Debug.LogWarning("[CloudRenderFeature]: Missing base texture. Aborting.");
            return;
        }

        if (!settings.cloudDetail)
        {
            Debug.LogWarning("[CloudRenderFeature]: Missing detail texture. Aborting.");
            return;
        }

        if (!settings.curlNoise)
        {
            Debug.LogWarning("[CloudRenderFeature]: Missing detail texture. Aborting.");
            return;
        }

        if (!settings.weatherMap)
        {
            Debug.LogWarning("[CloudRenderFeature]: Missing weather map. Aborting.");
            return;
        }

        rtHandles = new CloudRTHandles();
        rtHandles.baseRTHandle = RTHandles.Alloc(settings.cloudBase);
        rtHandles.detailRTHandle = RTHandles.Alloc(settings.cloudDetail);
        rtHandles.curlRTHandle = RTHandles.Alloc(settings.curlNoise);
        rtHandles.weatherRTHandle = RTHandles.Alloc(settings.weatherMap);

        renderPass = new CloudRenderPass(settings, rtHandles);
        renderPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.camera.cullingMask == (1 << LayerMask.NameToLayer("Clouds")))
        {
            renderer.EnqueuePass(renderPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        RTHandles.Release(rtHandles.baseRTHandle);
        RTHandles.Release(rtHandles.detailRTHandle);
        RTHandles.Release(rtHandles.curlRTHandle);
        RTHandles.Release(rtHandles.weatherRTHandle);
    }
}
