﻿Shader "Hidden/Toon RP/SSAO"
{
	Properties
	{
	}
	SubShader
	{
	    ZTest Off
		ZWrite Off
	    Cull Off
	    
	    HLSLINCLUDE

	    #pragma enable_d3d11_debug_symbols

	    #include "../../ShaderLibrary/Common.hlsl"

	    #pragma vertex VS
		#pragma fragment PS

	    struct v2f
		{
            float4 positionCs : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

	    v2f VS(const uint vertexId : SV_VertexID)
        {
            v2f OUT;
            OUT.positionCs = float4(
                vertexId == 1 ? 3.0 : -1.0,
		        vertexId <= 1 ? -1.0 : 3.0,
		        0.0, 1.0
	        );
            float2 uv = float2(
	            vertexId == 1 ? 2.0 : 0.0,
		        vertexId <= 1 ? 0.0 : 2.0
	        );
            #if UNITY_UV_STARTS_AT_TOP
            uv.y = 1 - uv.y;
            #endif // UNITY_UV_STARTS_AT_TOP
	        OUT.uv = uv;
            return OUT;
        }
	    
	    ENDHLSL
	     
		Pass
		{
		    Name "Toon RP SSAO"
			
			HLSLPROGRAM

			#pragma enable_d3d11_debug_symbols
			
            #include "../../ShaderLibrary/DepthNormals.hlsl"

			#define MAX_SAMPLES_COUNT 64
			#define NORMAL_BIAS 0.0
            #define DEPTH_BIAS 0.01

			CBUFFER_START(ToonRpSsao)
			float2 _ToonRP_SSAO_NoiseScale;
			int _ToonRP_SSAO_KernelSize;
			float4 _ToonRP_SSAO_Samples[MAX_SAMPLES_COUNT];
			float _ToonRP_SSAO_Radius;
			float _ToonRP_SSAO_Power;
			CBUFFER_END

			TEXTURE2D(_NoiseTexture);
			SAMPLER(sampler_NoiseTexture);

			float3 ScreenSpaceUvToNdc(const float2 uv, const float zNdc)
            {
                float3 positionNdc;
                positionNdc.xy = uv * 2 - 1;
                #if UNITY_UV_STARTS_AT_TOP
                positionNdc.y *= -1;
                #endif // UNITY_UV_STARTS_AT_TOP
                positionNdc.z = zNdc;
                return positionNdc;
            }

			float4 RestorePositionVs(float3 positionNdc, const float4x4 inverseProjection)
            {
                float4 positionVs = mul(inverseProjection, float4(positionNdc, 1.0));
                positionVs.xyz /= positionVs.w;
			    positionVs.w = 1;
                return positionVs;
            }

			float4 PS(const v2f IN) : SV_TARGET
            {
                // Adapted from https://github.com/Delt06/dx12-renderer
                // Originally based on https://learnopengl.com/Advanced-Lighting/SSAO
                const float2 uv = IN.uv;
                const float3 normalWs = SampleNormalsTexture(uv);

                const float zNdc = SampleDepthTexture(uv);
                
                #ifdef UNITY_REVERSED_Z
                if (zNdc == 0.0)
                #else // !UNITY_REVERSED_Z
                if (zNdc == 1.0)
                #endif // UNITY_REVERSED_Z
                {
                    return 1;
                }

                const float3 positionNdc = ScreenSpaceUvToNdc(uv, zNdc);
                const float4 positionVs = RestorePositionVs(positionNdc, UNITY_MATRIX_I_P);
                const float3 positionWs = mul(UNITY_MATRIX_I_V, positionVs).xyz + normalWs * NORMAL_BIAS;

                const float3 randomVector = float3(SAMPLE_TEXTURE2D_LOD(_NoiseTexture, sampler_NoiseTexture, uv * _ToonRP_SSAO_NoiseScale, 0).xy, 0);

                const float3 tangent = normalize(randomVector - normalWs * dot(randomVector, normalWs));
                const float3 bitangent = cross(normalWs, tangent);
                const float3x3 tbn = float3x3(tangent, bitangent, normalWs);

                float occlusion = 0.0;

                UNITY_LOOP
                for (int i = 0; i < _ToonRP_SSAO_KernelSize; ++i)
                {
                    const float3 samplePositionWs = positionWs + normalWs * _ToonRP_SSAO_Radius + mul(tbn, _ToonRP_SSAO_Samples[i].xyz) * _ToonRP_SSAO_Radius;

                    float4 samplePositionCs = TransformWorldToHClip(samplePositionWs);
                    samplePositionCs /= samplePositionCs.w;

                    float2 sampleScreenSpaceUv = samplePositionCs.xy;
                    sampleScreenSpaceUv = sampleScreenSpaceUv * 0.5 + 0.5;

                    // check if we are out of screen
                    if (any(sampleScreenSpaceUv < 0) || any(sampleScreenSpaceUv > 1))
                    {
                        continue;
                    }
                    
                    #if UNITY_UV_STARTS_AT_TOP
                    sampleScreenSpaceUv.y = 1 - sampleScreenSpaceUv.y;
                    #endif // UNITY_UV_STARTS_AT_TOP

                    const float sampleDepthZNdc = SampleDepthTexture(sampleScreenSpaceUv);
                    const float3 sampleDepthPositionNdc = float3(samplePositionCs.xy, sampleDepthZNdc);
                    float3 sampleDepthPositionVs = RestorePositionVs(sampleDepthPositionNdc, UNITY_MATRIX_I_P).xyz;

                    float3 samplePositionVs = TransformWorldToView(samplePositionWs);

                    const float distance = abs(positionVs.z - sampleDepthPositionVs.z);
                    const float rangeCheck = smoothstep(0.0, 1.0, _ToonRP_SSAO_Radius / (0.000001 + distance));
                    occlusion += samplePositionVs.z <= sampleDepthPositionVs.z + DEPTH_BIAS ? rangeCheck : 0;
                }

                occlusion = 1.0 - occlusion / _ToonRP_SSAO_KernelSize;
                return float4(pow(abs(occlusion), _ToonRP_SSAO_Power), 1.0f, 1.0f, 1.0f);
            }

			ENDHLSL
		}
	    
	    Pass 
	    {
	        Name "Toon RP SSAO Blur"
	        
	        HLSLPROGRAM

	        #pragma enable_d3d11_debug_symbols

	        #include "../../ShaderLibrary/Textures.hlsl"

	        float2 _ToonRP_SSAO_Blur_Direction;
	        TEXTURE2D(_ToonRP_SSAO_Blur_SourceTex);
	        SAMPLER(sampler_ToonRP_SSAO_Blur_SourceTex);
	        DECLARE_TEXEL_SIZE(_ToonRP_SSAO_Blur_SourceTex);

	        float Blur(const float2 uv)
            {
	            float result = 0.0;
                const float offsets[] =
                {
		            -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
	            };
                const float weights[] =
	            {
		            0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
	            };
	            for (int i = 0; i < 5; i++)
	            {
                    const float2 offset = offsets[i] * _ToonRP_SSAO_Blur_Direction * _ToonRP_SSAO_Blur_SourceTex_TexelSize.xy;
		            result += SAMPLE_TEXTURE2D_LOD(_ToonRP_SSAO_Blur_SourceTex, sampler_ToonRP_SSAO_Blur_SourceTex, uv + offset, 0).r * weights[i];
	            }
                return result;
            }

			float4 PS(const v2f IN) : SV_TARGET
            {
                return float4(Blur(IN.uv), 0.0f, 0.0f, 0.0f); 
            }
	        
	        ENDHLSL
	    }
	}
}