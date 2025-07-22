Shader "Clouds/CloudsBlit"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

                #pragma vertex Vert
                #pragma fragment frag

                TEXTURE2D_X(_CameraColor);
                SAMPLER(sampler_CameraColor);

                SAMPLER(sampler_BlitTexture);

                float4 frag(Varyings IN) : SV_TARGET {
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                    float4 cameraColor = SAMPLE_TEXTURE2D_X(_CameraColor, sampler_CameraColor, IN.texcoord);
                    float4 cloudsTex = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, IN.texcoord);
                    float3 cloudsScattering = cloudsTex.rgb;
                    float3 cloudsTransmittance = cloudsTex.a;

                    float3 finalColor = cameraColor.rgb * cloudsTransmittance + cloudsScattering;

                    return float4(finalColor, cameraColor.a);
                }

            ENDHLSL
        }
    }
}
