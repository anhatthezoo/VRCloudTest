static const float3 NOISE_KERNEL[] = {
    float3(0.295787, 0.952936, -0.066508),
    float3(0.206517, 0.959319, 0.192502),
    float3(0.087877, 0.994795, -0.051581),
    float3(-0.214029, 0.93444, 0.28463),
    float3(-0.289995, 0.952647, -0.091464),
    float3(0.022786, 0.970513, -0.239968)
};

static const uint BAYER_MATRIX[] = {
    0, 8, 2, 10,
    12, 4, 14, 6,
    3, 11, 1, 9,
    15, 7, 13, 5
}; 

real GetSceneDepth(float2 uv) {
    real depth;
    #if UNITY_REVERSED_Z
        depth = SampleSceneDepth(uv);
    #else
        depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
    #endif

    return depth;
}

bool isValidPixel(float2 coord, uint frame) {
    uint2 uv = uint2(coord);
    uint idx = frame % 16u;
    return (((uv.x + 4u * uv.y) % 16u) == BAYER_MATRIX[idx]);
}

float getHeightFractionForPoint(float3 pos) {
    float heightFraction = round((distance(pos, PLANET_CENTER) - CLOUD_INNER_SHELL_RADIUS)) / (CLOUD_OUTER_SHELL_RADIUS - CLOUD_INNER_SHELL_RADIUS);

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

float sampleCloudDensity(float3 pos, float heightFrac, float3 weatherData, bool fast) {
    pos += heightFrac * float3(1.0, 0, 0) * 500.0;
    pos += (float3(1.0, 0, 0) + float3(0.0, 0.1, 0.0)) * (_Time.y) * 3.0;
    pos *= CLOUD_SCALE / 1.;

    float4 lowFreqNoises = _CloudBase.Sample(sampler_CloudBase, pos * 1.).rgba; 
    float lowFreqFBM = 
        (lowFreqNoises.g * .625) + 
        (lowFreqNoises.b * .25) + 
        (lowFreqNoises.a * .125);

    float baseCloud = remap(lowFreqNoises.r, -(1.0 - lowFreqFBM), 1.0, 0.0, 1.0);
    float layerDensity = getLayerDensityByType(heightFrac, .5);
    
    baseCloud = smoothstep(0.4, 0.65, baseCloud);
    baseCloud *= layerDensity;

    float cloudCoverage = weatherData.r * COVERAGE_MULTIPLIER;
    baseCloud = remap(baseCloud, saturate(cloudCoverage), 1.0, 0.0, 1.0);
    baseCloud *= cloudCoverage;

    if (!fast) {
        float2 curlNoise = _CurlNoise.Sample(sampler_CurlNoise, pos.xz * 0.01);
        pos.xz += curlNoise.xy * (1.0 - heightFrac);
        
        float3 highFreqNoises = _CloudDetail.Sample(sampler_CloudDetail, pos * 2.).rgb;
        float highFreqFBM = 
            (highFreqNoises.r * 0.625) +
            (highFreqNoises.g * 0.25) + 
            (highFreqNoises.b * 0.125);
            
        float highFreqNoiseMod = lerp(highFreqFBM, 1.0 - highFreqFBM, saturate(heightFrac * 10.0));
        float finalCloud = remap(baseCloud, highFreqNoiseMod * _HighFreqNoiseStrength, 1.0, 0.0, 1.0);

        return saturate(finalCloud);
    }

    return saturate(baseCloud);
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
        float3 weatherData = sampleWeather(pos);

        if (densityAlongCone < 0.3) {
            densityAlongCone += max(0, sampleCloudDensity(pos, heightFrac, weatherData, false));
        } else {
            densityAlongCone += max(0, sampleCloudDensity(pos, heightFrac, weatherData, true));
        }
    }

    return densityAlongCone * lightStepSize;
}

float computeLightEnergy(float lightDensity, float heightFrac, float mu) {
    float beers = max(exp(-lightDensity * 2.), (exp(-lightDensity * 0.25 * 2.0) * 0.25));
    float depthProbability = 0.05 + pow(lightDensity, remap(heightFrac, 0.15, .85, .3, 1.0));
    float verticalProbability = pow(remap(heightFrac, 0.07, .14, 0.15, 1.0), 0.8);
    float inScatter = saturate(depthProbability * verticalProbability);
    float phase = lerp(HenyeyGreenstein(0.6, mu), HenyeyGreenstein(-0.2, mu), 0.5);

    return 2.0 * beers * inScatter * phase;
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
    float sunHeight = saturate(GetMainLight().direction.y);
    float mu = dot(normalize(mainLight.direction), normalize(eyeDir));

    float cloudTest = 0.0;
    int zeroDensitySampleCount = 0;

    float3 scattering = float3(0.0, 0.0, 0.0);
    float transmittance = 1.0;

    [loop]
    for (uint i = 0; i < sampleCount; i++) {
        float heightFrac = getHeightFractionForPoint(pos);
        float3 weatherData = sampleWeather(pos);

        if (cloudTest > 0.0) {
            float density = sampleCloudDensity(pos, heightFrac, weatherData, false);

            if (density == 0.0) {
                zeroDensitySampleCount++;
            }

            if (zeroDensitySampleCount != 6) {
                if (density > _DensityThreshold) {
                    float lightDensity = sampleDensityAlongCone(pos, mainLight.direction);
                    float3 ambient = lerp(CLOUDS_AMBIENT_BOTTOM, CLOUDS_AMBIENT_TOP, smoothstep(0.0, 1.0, heightFrac));

                    float T = exp(-density * stepSize);
                    float lightEnergy = computeLightEnergy(lightDensity, heightFrac, mu);

                    float3 luminance = (ambient + (mainLight.color * 2.5) * lightEnergy) * density;
                    float3 integScatter = (luminance - luminance * T) / max(0.0000001, density); 
                    
                    scattering += transmittance * integScatter;                      
                    transmittance *= T;

                    if (transmittance < 0.05) {
                        transmittance = 0.0;
                        break;
                    }
                }

                pos += stepSize * traceDir;
            } else {
                cloudTest = 0.0;
                zeroDensitySampleCount = 0;
            }
        } else {
            cloudTest = sampleCloudDensity(pos, heightFrac, weatherData, true);

            if (cloudTest == 0.0) {
                pos += stepSize * traceDir;
            }
        }
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
    float3 upDir = normalize(camPos - PLANET_CENTER);

    if (dot(upDir, eyeDir) < 0.0)
    {
        return float4(0, 0, 0, 1);
    }

    float2 ih = float2(0.0, 0.0);
    bool didHitInnerLayer = raySphereIntersection(
        camPos,
        eyeDir,
        PLANET_CENTER,
        CLOUD_INNER_SHELL_RADIUS,
        ih
    );

    float2 oh = float2(0.0, 0.0);
    bool didHitOuterLayer = raySphereIntersection(
        camPos,
        eyeDir,
        PLANET_CENTER,
        CLOUD_OUTER_SHELL_RADIUS,
        oh
    );
    
    float3 innerLayerHit = camPos + (eyeDir * ih.x);
    float3 outerLayerHit = camPos + (eyeDir * oh.x); 

    float3 traceStart;
    float3 traceEnd;
    float camToPlanetDist = distance(camPos, PLANET_CENTER);

    if (camToPlanetDist < CLOUD_INNER_SHELL_RADIUS) {
        traceStart = innerLayerHit;
        traceEnd = outerLayerHit;

        if (depthPresent && (distance(fragPos, camPos) < distance(traceStart, camPos))) {
            return float4(0, 0, 0, 1);
        }
    } 

    float4 cloudResult;
    // float4 prevCloud = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, IN.texcoord);
    
    // if (!isValidPixel(IN.positionCS.xy, _FrameCount)) {
        // return prevCloud;
        // return float4(0, 0, 0, 1);
    // } else {
        cloudResult = compute(traceStart, traceEnd, eyeDir);

        float horizonFade = smoothstep(0.0, 0.15, dot(upDir, eyeDir));
        cloudResult = lerp(float4(0, 0, 0, 1), cloudResult, horizonFade);
        // cloudResult = lerp(prevCloud, cloudResult, 0.5);
    // }
    
    return cloudResult;
}