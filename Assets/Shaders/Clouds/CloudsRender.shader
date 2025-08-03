Shader "Clouds/CloudsRender"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off Cull Off

        Pass
        {
            HLSLPROGRAM
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl" 
                #include "Math.hlsl"

                #pragma enable_d3d11_debug_symbols

                #define PLANET_RADIUS float(6000000.)
                #define PLANET_CENTER float3(0, -PLANET_RADIUS, 0)
                #define CLOUD_VOLUME_THICKNESS float(3000.0)
                #define CLOUD_LAYER_BEGIN float(2000.0)
                #define CLOUD_INNER_SHELL_RADIUS (PLANET_RADIUS + CLOUD_LAYER_BEGIN)
                #define CLOUD_OUTER_SHELL_RADIUS (CLOUD_INNER_SHELL_RADIUS + CLOUD_VOLUME_THICKNESS)
                #define CLOUD_SCALE (1.0 / (CLOUD_LAYER_BEGIN + CLOUD_VOLUME_THICKNESS))

                #define CLOUD_LAYER_END float(5000.0)
                #define CLOUDS_AMBIENT_TOP float3(255, 255, 255) * (1. / 255.0)
                #define CLOUDS_AMBIENT_BOTTOM float3(93, 106, 115) * (1. / 255.0)
                #define COVERAGE_MULTIPLIER float(1.)

                // x: scale
                // y,z : x, y offsets
                #define WEATHER_TEX_MOD float3(0.00001, 0.5, 0.5)
                #define SAMPLE_RANGE uint2(64, 128)

                Texture3D _CloudBase;
                SamplerState sampler_CloudBase;
                Texture3D _CloudDetail;
                SamplerState sampler_CloudDetail;
                Texture2D _CurlNoise;
                SamplerState sampler_CurlNoise;
                Texture2D _WeatherData;
                SamplerState sampler_WeatherData;

                SAMPLER(sampler_BlitTexture);

                uint _FrameCount;
                float _DensityThreshold;
                float _HighFreqNoiseStrength;

                #include "CloudsRender.hlsl"
                #pragma vertex Vert
                #pragma fragment frag
            ENDHLSL
        }
    }
}
