﻿#ifndef TOON_RP_POST_PROCESSING_STACK
#define TOON_RP_POST_PROCESSING_STACK

#include "ToonRPPostProcessingStackCommon.hlsl"

struct appdata
{
    float3 position : POSITION;
    float2 uv : TEXCOORD0;
};

struct v2f
{
    float4 positionCs : SV_POSITION;
    float2 uv : TEXCOORD0;
};

v2f VS(const appdata IN)
{
    v2f OUT;
    OUT.uv = IN.uv;
    OUT.positionCs = TransformObjectToHClip(IN.position);
    return OUT;
}

#if defined(_FXAA_LOW)
#include "ToonRPFXAALowQuality.hlsl"
#elif defined(_FXAA_HIGH)
#include "ToonRPFXAAHighQuality.hlsl"
#endif

#include "ToonRPToneMapping.hlsl"
#include "ToonRPVignette.hlsl"
#include "ToonRPLookupTable.hlsl"
#include "ToonRPFilmGrain.hlsl"

float4 PS(const v2f IN) : SV_TARGET
{
    float3 color;
    const float2 uv = IN.uv;

    #ifdef _FXAA
    color = ApplyFxaa(uv);
    #else // !_FXAA
    color = SampleSource(uv);
    #endif // _FXAA

    #ifdef _TONE_MAPPING
    color = ApplyToneMapping(color);
    #else // !_TONE_MAPPING
    color = saturate(color);
    #endif // _TONE_MAPPING

    #ifdef _VIGNETTE
    color = ApplyVignette(color, uv);
    #endif // _VIGNETTE

    #ifdef _LOOKUP_TABLE
    color = ApplyLookupTable(color);
    #endif // _LOOKUP_TABLE

    #ifdef _FILM_GRAIN
    color = ApplyFilmGrain(uv, color);
    #endif // _FILM_GRAIN

    return float4(color, 1.0f);
}

#endif // TOON_RP_POST_PROCESSING_STACK