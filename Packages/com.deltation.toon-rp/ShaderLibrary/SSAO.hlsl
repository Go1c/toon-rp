﻿#ifndef TOON_RP_SSAO
#define TOON_RP_SSAO

#if defined(_TOON_RP_SSAO) || defined(_TOON_RP_SSAO_PATTERN)
#define TOON_RP_SSAO_ANY
#endif // _TOON_RP_SSAO || _TOON_RP_SSAO_PATTERN

#include "Textures.hlsl"
#include "Math.hlsl"

TEXTURE2D(_ToonRP_SSAOTexture);
SAMPLER(sampler_ToonRP_SSAOTexture);
float2 _ToonRP_SSAO_Ramp;
TEXTURE2D(_ToonRP_SSAO_Pattern);
SAMPLER(sampler_ToonRP_SSAO_Pattern);
float3 _ToonRP_SSAO_PatternScale;

float SampleAmbientOcclusionRaw(float2 screenUv)
{
    #if UNITY_UV_STARTS_AT_TOP
    if (_ProjectionParams.x > 0.0)
    {
        screenUv.y = 1 - screenUv.y;
    }
    #endif // UNITY_UV_STARTS_AT_TOP

    return SAMPLE_TEXTURE2D(_ToonRP_SSAOTexture, sampler_ToonRP_SSAOTexture, screenUv).x;
}

float GetAmbientOcclusionPattern(const float3 positionWs)
{
    const float2 uv = EncodePositionToUv(positionWs, _ToonRP_SSAO_PatternScale);
    return SAMPLE_TEXTURE2D(_ToonRP_SSAO_Pattern, sampler_ToonRP_SSAO_Pattern, uv);
}

float SampleAmbientOcclusion(const float2 screenUv, const float3 positionWs)
{
    #ifdef TOON_RP_SSAO_ANY
    const float aoRaw = SampleAmbientOcclusionRaw(screenUv);
    const float ao = ComputeRamp(aoRaw, _ToonRP_SSAO_Ramp);
    
    #ifdef _TOON_RP_SSAO_PATTERN
    const float pattern = GetAmbientOcclusionPattern(positionWs);
    return lerp(ao, 1, pattern);
    #else // !_TOON_RP_SSAO_PATTERN
    return ao;
    #endif // _TOON_RP_SSAO_PATTERN

    #else // !TOON_RP_SSAO_ANY
    return 1;
    #endif // TOON_RP_SSAO_ANY
}

#endif // TOON_RP_SSAO