Shader "Clouds/CloudsBlit"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "LightMode" = "MotionVectors" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

                #pragma vertex Vert
                #pragma fragment frag

                static const int2 NEIGHBOR_OFFSETS[] = {
                    int2(-1, -1), int2(-1, 1),
                    int2(1, -1), int2(1, 1),
                    int2(1, 0), int2(0, -1),
                    int2(0, 1), int2(-1, 0)
                };

                TEXTURE2D_X(_CameraColor);
                SAMPLER(sampler_CameraColor);

                TEXTURE2D_X(_MotionVectorTexture);
                SAMPLER(sampler_MotionVectorTexture);

                TEXTURE2D_X(_HistoryTex);
                SAMPLER(sampler_HistoryTex);

                SAMPLER(sampler_BlitTexture);

                float4 frag(Varyings IN) : SV_TARGET {
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                    float4 cameraColor = SAMPLE_TEXTURE2D_X(_CameraColor, sampler_CameraColor, IN.texcoord);

                    float2 motionVector = SAMPLE_TEXTURE2D_X(_MotionVectorTexture, sampler_MotionVectorTexture, IN.texcoord);
                    float2 historyUV = IN.texcoord - motionVector;
                    // float2 historyUV = IN.texcoord;
                    float4 historyColor = SAMPLE_TEXTURE2D_X(_HistoryTex, sampler_HistoryTex, historyUV);

                    float4 currentColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, IN.texcoord);

                    float4 colorAvg = currentColor;
                    float4 colorVar = currentColor * currentColor;

                    for (int i = 0; i < 8; i++) {
                        float4 neighborColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, int2(IN.positionCS.xy) + NEIGHBOR_OFFSETS[i]);
                        colorAvg += neighborColor;
                        colorVar += neighborColor * neighborColor;
                    }

                    colorAvg /= 9.0;
                    colorVar /= 9.0;
                    float gColorBoxSigma = 0.75;
                    float4 sigma = sqrt(max(float4(0, 0, 0, 0), colorVar - colorAvg * colorAvg));
                    float4 colorMin = colorAvg - gColorBoxSigma * sigma;
                    float4 colorMax = colorAvg + gColorBoxSigma * sigma;

                    historyColor = clamp(historyColor, colorMin, colorMax);
                    // float4 cloudColor = lerp(currentColor, historyColor, 0.5);
                    float4 cloudColor = currentColor;

                    float3 finalColor = cameraColor.rgb * cloudColor.a + cloudColor.rgb;

                    return float4(finalColor, 1.0);
                }

            ENDHLSL
        }
    }
}
