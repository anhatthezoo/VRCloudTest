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

                #pragma vertex Vert
                #pragma fragment frag

                #define CLOUD_LAYER_BEGIN float(2000.0)
                #define CLOUD_LAYER_END float(5000.0)
                #define CLOUD_VOLUME_THICKNESS (CLOUD_LAYER_END - CLOUD_LAYER_BEGIN)
                #define CLOUD_SCALE (1.0 / CLOUD_LAYER_END)
                #define CLOUDS_AMBIENT_TOP float3(255, 255, 255) * (1.5 / 255.0)
                #define CLOUDS_AMBIENT_BOTTOM float3(132, 170, 208) * (1.0 / 255.0)
                #define PLANET_CENTER float3(0, 0, 0)

                // x: scale
                // y,z : x, y offsets
                #define WEATHER_TEX_MOD float3(0.00001, 0.5, 0.5)
                #define SAMPLE_RANGE uint2(64, 128)

                static const float3 NOISE_KERNEL[] = {
                    float3(0.295787, 0.952936, -0.066508),
                    float3(0.206517, 0.959319, 0.192502),
                    float3(0.087877, 0.994795, -0.051581),
                    float3(-0.214029, 0.93444, 0.28463),
                    float3(-0.289995, 0.952647, -0.091464),
                    float3(0.022786, 0.970513, -0.239968)
                };

                Texture3D _CloudBase;
                SamplerState sampler_CloudBase;
                Texture3D _CloudDetail;
                SamplerState sampler_CloudDetail;
                Texture2D _CurlNoise;
                SamplerState sampler_CurlNoise;
                Texture2D _DensityLUT;
                SamplerState sampler_DensityLUT;

                Texture2D _WeatherData;
                SamplerState sampler_WeatherData;

                real GetSceneDepth(float2 uv) {
                    real depth;
                    #if UNITY_REVERSED_Z
                        depth = SampleSceneDepth(uv);
                    #else
                        depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                    #endif

                    return depth;
                }

                float getHeightFractionForPoint(float3 pos) {
                    float heightFraction = (pos.y - CLOUD_LAYER_BEGIN) / (CLOUD_LAYER_END - CLOUD_LAYER_BEGIN);

                    return saturate(heightFraction);
                }

                float3 sampleWeather(float3 pos) {
                    float2 uv = (pos.xz * WEATHER_TEX_MOD.x) + WEATHER_TEX_MOD.yz;
                    return _WeatherData.Sample(sampler_WeatherData, uv).rgb;
                }

                float getLayerDensityByType(float heightFrac, float type) {
                    float cumulus = max(0.0, remap(heightFrac, 0.0, 0.2, 0.0, 1.0) * remap(heightFrac, 0.7, 0.9, 1.0, 0.0));
                    float stratocumulus = max(0.0, remap(heightFrac, 0.0, 0.2, 0.0, 1.0) * remap(heightFrac, 0.2, 0.7, 1.0, 0.0)); 
                    float stratus = max(0.0, remap(heightFrac, 0.0, 0.1, 0.0, 1.0) * remap(heightFrac, 0.2, 0.3, 1.0, 0.0)); 

                    float d1 = lerp(stratus, stratocumulus, clamp(type * 2.0, 0.0, 1.0));
                    float d2 = lerp(stratocumulus, cumulus, clamp((type - 0.5) * 2.0, 0.0, 1.0));
                    return lerp(d1, d2, type);
                }

                float sampleCloudDensity(float3 pos, float heightFrac, float3 weatherData) {
                    pos += heightFrac * float3(1.0, 0, 0) * 500.0;
                    pos += (float3(1.0, 0, 0) + float3(0.0, 0.1, 0.0)) * (_Time.y) * 10.0;
                    pos *= CLOUD_SCALE;

                    float4 lowFreqNoises = _CloudBase.Sample(sampler_CloudBase, pos * 1.4).rgba;
                    float lowFreqFBM = 
                        (lowFreqNoises.g * 0.625) + 
                        (lowFreqNoises.b * 0.125) + 
                        (lowFreqNoises.a * 0.25);
                    float baseCloud = remap(lowFreqNoises.r, -(1.0 - lowFreqFBM), 1.0, 0.0, 1.0);
                    float layerDensity = getLayerDensityByType(heightFrac, 0.65);
                    baseCloud *= layerDensity;

                    float cloudCoverage = weatherData.r;
                    float cloudProbability = saturate(cloudCoverage - 0.001);
                    float baseCloudWithCoverage = remap(baseCloud, cloudProbability, 1.0, 0.0, 1.0);
                    baseCloudWithCoverage *= cloudCoverage;

                    float2 curlNoise = _CurlNoise.Sample(sampler_CurlNoise, pos.xz * 0.01 + (0.001 * _Time.y));
                    pos.xz += curlNoise.xy * (1.0 - heightFrac);

                    float3 highFreqNoises = _CloudDetail.Sample(sampler_CloudDetail, pos).rgb;
                    float highFreqFBM = 
                        (highFreqNoises.r * 0.625) +
                        (highFreqNoises.g * 0.125) + 
                        (highFreqNoises.b * 0.25);

                    float highFreqNoiseMod = lerp(highFreqFBM, 1.0 - highFreqFBM, saturate(heightFrac * 10.0));
                    // float finalCloud = remap(baseCloudWithCoverage, highFreqNoiseMod * 0.2, 1.0, 0.0, 1.0);
                    float finalCloud = pow(saturate(baseCloudWithCoverage - highFreqNoiseMod * 0.2), 1.5);

                    return saturate(finalCloud * 2.0);
                }

                float HenyeyGreenstein(float g, float mu) {
                    float g2 = g * g;
                    return (1.0 - g2) / (pow(1.0 + g2 - 2.0 * g * mu, 1.5) * 4.0 * PI);
                }

                float sampleDensityAlongCone(float3 startPos, float3 sunDir) {
                    float lightStepSize = 64.;
                    float3 lightStep = sunDir * lightStepSize;
                    float coneSpread = length(lightStep);
                    float densityAlongCone = 0.0;
                    float3 pos = startPos;
                    float heightFrac;

                    for (int i = 0; i < 6; i++) {
                        pos += lightStep + (coneSpread * NOISE_KERNEL[i] * float(i));
                        heightFrac = getHeightFractionForPoint(pos);

                        if (heightFrac > 0.95 || densityAlongCone > 0.95) break;

                        float3 weatherData = sampleWeather(pos);
                        densityAlongCone += max(0, sampleCloudDensity(pos, heightFrac, weatherData));
                    }

                    return densityAlongCone * lightStepSize;
                }

                float computeLightEnergy(float lightDensity, float mu, float precipitation) {
                    float scatterAmount = lerp(0.008, 1.0, smoothstep(0.96, 0.0, mu));
                    // float beers = exp(-lightDensity * precipitation);
                    float beers = exp(-lightDensity * precipitation) 
                                + 0.5 * scatterAmount * exp(-0.1 * lightDensity) 
                                + scatterAmount * 0.4 * exp(-0.02 * lightDensity);
                    float powder = 1.0 - exp(-lightDensity * 2.0);
                    float phase = lerp(HenyeyGreenstein(0.8, mu), HenyeyGreenstein(-0.2, mu), 0.5);

                    return 2.0 * beers * powder * phase;
                }

                float4 compute(float3 traceStart, float3 traceEnd, float3 eyeDir) {
                    float3 traceDir = normalize(traceEnd - traceStart);
                    float traceLength = length(traceEnd - traceStart);
                    uint sampleCount = lerp(
                        SAMPLE_RANGE.x, 
                        SAMPLE_RANGE.y, 
                        saturate((traceLength - CLOUD_VOLUME_THICKNESS) / CLOUD_VOLUME_THICKNESS));
                    float stepSize = traceLength / float(sampleCount);
                    float3 pos = traceStart;
                    const Light mainLight = GetMainLight();
                    float sunHeight = saturate(mainLight.direction.y);
                    float mu = dot(normalize(mainLight.direction), normalize(eyeDir));
                    float3 scattering = float3(0.0, 0.0, 0.0);
                    float transmittance = 1.0;

                    [loop]
                    for (uint i = 0; i < sampleCount; i++) {
                        float heightFrac = getHeightFractionForPoint(pos);
                        float3 weatherData = sampleWeather(pos);

                        float cloudDensity = sampleCloudDensity(pos, heightFrac, weatherData);

                        if (cloudDensity > 0.001) {
                            float sunAttenuation = saturate(pow(sunHeight + 0.1, 1.4));

                            float lightDensity = sampleDensityAlongCone(pos, mainLight.direction);
                            float lightEnergy = computeLightEnergy(lightDensity, mu, 1.0);

                            float3 ambient = lerp(CLOUDS_AMBIENT_BOTTOM, CLOUDS_AMBIENT_TOP, heightFrac);
                            ambient *= sunAttenuation;

                            float T = exp(-cloudDensity * stepSize);
                            float3 luminance = (ambient + (mainLight.color * 3.0) * lightEnergy) * cloudDensity;
                            float3 integScatter = (luminance - luminance * T) / cloudDensity;
                            
                            scattering += transmittance * integScatter;
                            transmittance *= T;

                            if (transmittance < 0.05) {
                                transmittance = 0.0;
                                break;
                            }
                        }

                        pos += stepSize * traceDir;
                    }
                    
                    return float4(scattering, transmittance);
                }

                float4 frag(Varyings IN) : SV_TARGET {
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                    real depth = GetSceneDepth(IN.texcoord);
                    bool depthPresent = Linear01Depth(depth, _ZBufferParams) < 1.0;

                    float3 camPos = GetCameraPositionWS();
                    float3 fragPos = ComputeWorldSpacePosition(IN.texcoord, depth, UNITY_MATRIX_I_VP);
                    float3 eyeDir = normalize(fragPos - camPos);    

                    float lh = 0.0;
                    float didHitLowerLayer = rayPlaneIntersection(
                        float3(0, 1, 0),
                        float3(0, CLOUD_LAYER_BEGIN, 0),
                        camPos,
                        eyeDir,
                        lh
                    );

                    float uh = 0.0;
                    float didHitUpperLayer = rayPlaneIntersection(
                        float3(0, 1, 0),
                        float3(0, CLOUD_LAYER_END, 0),
                        camPos,
                        eyeDir,
                        uh
                    );

                    if (!didHitLowerLayer && !didHitUpperLayer) {
                        return float4(0, 0, 0, 1);
                    }

                    float3 lowerLayerHit = camPos + (eyeDir * lh);
                    float3 upperLayerHit = camPos + (eyeDir * uh); 

                    if (depthPresent && (distance(fragPos, camPos) < distance(lowerLayerHit, camPos))) {
                        return float4(0, 0, 0, 1);
                    }

                    float4 cloudResult = compute(lowerLayerHit, upperLayerHit, eyeDir);
                    // float fogFactor = 1.0 - saturate(abs(eyeDir.y) / 0.15);
                    // fogFactor = smoothstep(0.0, 1.0, fogFactor);
                    // float3 skyColor = _GlossyEnvironmentColor.xyz;
                    // cloudResult.rgb = lerp(cloudResult.rgb, skyColor, fogFactor);
                    // cloudResult.a = lerp(cloudResult.a, 0.0, fogFactor);

                    return cloudResult;
                }
            ENDHLSL
        }
    }
}
