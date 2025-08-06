using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class CloudRenderPass : ScriptableRenderPass
{
    private CloudRenderFeature.CloudRenderSettings settings;
    private CloudRenderFeature.CloudRTHandles rtHandles;
    private Light mainLight;
    private int currentRenderCount = 0;

    private class PassData
    {
        public ComputeShader shader;
        public TextureHandle outTex;
        public int rtWidth;
        public int rtHeight;
        public Camera cam;
        public TextureHandle baseTex;
        public TextureHandle detailTex;
        public TextureHandle curlTex;
        public TextureHandle weatherTex;
        public float densityThreshold;
        public float coverageMultiplier;
        public float highFreqNoiseStrength;
    }

    public CloudRenderPass(CloudRenderFeature.CloudRenderSettings settings, CloudRenderFeature.CloudRTHandles rtHandles)
    {
        this.settings = settings;
        this.rtHandles = rtHandles;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        CloudRenderController controller = settings.controllerObj.GetComponent<CloudRenderController>();

        if (resourceData.isActiveTargetBackBuffer)
        {
            return;
        }

        if (currentRenderCount < 6)
        {
            currentRenderCount++;
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            var camRTHandle = resourceData.activeColorTexture;
            var rtDesc = camRTHandle.GetDescriptor(renderGraph);
            rtDesc.enableRandomWrite = true;
            rtDesc.depthBufferBits = 0;
            rtDesc.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;

            TextureHandle outTex = renderGraph.CreateTexture(rtDesc);

            TextureHandle baseTex = renderGraph.ImportTexture(rtHandles.baseRTHandle);
            TextureHandle detailTex = renderGraph.ImportTexture(rtHandles.detailRTHandle);
            TextureHandle curlTex = renderGraph.ImportTexture(rtHandles.curlRTHandle);
            TextureHandle weatherTex = renderGraph.ImportTexture(rtHandles.weatherRTHandle);

            using (var builder = renderGraph.AddComputePass("CloudComputePass", out PassData passData))
            {
                builder.UseTexture(outTex, AccessFlags.Write);
                builder.UseTexture(baseTex, AccessFlags.Read);
                builder.UseTexture(detailTex, AccessFlags.Read);
                builder.UseTexture(curlTex, AccessFlags.Read);
                builder.UseTexture(weatherTex, AccessFlags.Read);
                builder.EnableAsyncCompute(true);

                passData.shader = settings.shader;
                passData.outTex = outTex;
                passData.rtWidth = rtDesc.width;
                passData.rtHeight = rtDesc.height;
                passData.cam = cameraData.camera;
                passData.baseTex = baseTex;
                passData.detailTex = detailTex;
                passData.curlTex = curlTex;
                passData.weatherTex = weatherTex;
                passData.densityThreshold = settings.densityThreshold;
                passData.coverageMultiplier = settings.coverageMultiplier;
                passData.highFreqNoiseStrength = settings.highFreqNoiseStrength;

                builder.SetRenderFunc((PassData data, ComputeGraphContext context) => ExecutePass(data, context));
            }

            renderGraph.AddBlitPass(outTex, camRTHandle, Vector2.one, Vector2.zero, passName: "CloudComputeBlitPass");

            if (currentRenderCount == 6)
            {
                currentRenderCount = 0;
            }
        }
    }

    void ExecutePass(PassData data, ComputeGraphContext context)
    {
        // Debug.Log("[CloudRenderPass]: Rendering cubemap face");

        var mainKernel = data.shader.FindKernel("CSMain");
        data.shader.GetKernelThreadGroupSizes(mainKernel, out var xGroupSize, out var yGroupSize, out _);
        context.cmd.SetComputeTextureParam(data.shader, mainKernel, "OutRT", data.outTex);
        context.cmd.SetComputeIntParam(data.shader, "_RTWidth", data.rtWidth);
        context.cmd.SetComputeIntParam(data.shader, "_RTHeight", data.rtHeight);
        context.cmd.SetComputeMatrixParam(data.shader, "_CameraToWorld", data.cam.cameraToWorldMatrix);
        context.cmd.SetComputeMatrixParam(data.shader, "_CameraInverseProjection", data.cam.projectionMatrix.inverse);

        context.cmd.SetComputeTextureParam(data.shader, mainKernel, "_CloudBase", data.baseTex);
        context.cmd.SetComputeTextureParam(data.shader, mainKernel, "_CloudDetail", data.detailTex);
        context.cmd.SetComputeTextureParam(data.shader, mainKernel, "_CurlNoise", data.curlTex);
        context.cmd.SetComputeTextureParam(data.shader, mainKernel, "_WeatherMap", data.weatherTex);
        context.cmd.SetComputeFloatParam(data.shader, "_DensityThreshold", data.densityThreshold);
        context.cmd.SetComputeFloatParam(data.shader, "_CoverageMultiplier", data.coverageMultiplier);
        context.cmd.SetComputeFloatParam(data.shader, "_HighFreqNoiseStrength", data.highFreqNoiseStrength);

        context.cmd.DispatchCompute(
            data.shader, mainKernel,
            Mathf.CeilToInt(data.rtWidth / (float)xGroupSize),
            Mathf.CeilToInt(data.rtHeight / (float)yGroupSize),
            1
        );
    }
}