Shader "Custom/BoostRadialFX"
{
    Properties
    {
        _Tint ("Tint Color", Color) = (1,1,1,1)

        // Boost controls
        _BoostIntensity   ("Boost Intensity", Range(0,1))   = 0
        _InnerRadius      ("Inner Radius",    Range(0,1))   = 0.5
        _Feather          ("Feather",         Range(0.01,1))= 0.25
        _BlurStrength     ("Blur Strength",   Range(0,0.2)) = 0.05
        _DistortionStrength ("Distortion Strength", Range(-0.5,0.5)) = 0.08

        // Edge shaping
        _EdgePower        ("Edge Power",      Range(0.5,4)) = 2.0

        // Chromatic aberration
        _ChromaticOffset   ("Chromatic Offset",   Range(0,0.02)) = 0.004
        _ChromaticStrength ("Chromatic Strength", Range(0,2))    = 0.8
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Opaque" }

        Pass
        {
            Name "FullScreenBoost"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _Tint;
            float  _BoostIntensity;
            float  _InnerRadius;
            float  _Feather;
            float  _BlurStrength;
            float  _DistortionStrength;

            float  _EdgePower;

            float  _ChromaticOffset;
            float  _ChromaticStrength;

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv     = input.texcoord;
                float2 center = float2(0.5, 0.5);

                float2 offset = uv - center;
                float  dist   = length(offset);

                // Avoid NaN
                float2 dir = normalize(offset + 1e-6);

                // ---------- Edge mask (ring) ----------
                float distNorm = (dist - _InnerRadius) / max(_Feather, 1e-4);
                distNorm = saturate(distNorm);

                // smoother falloff then sharpen with power
                distNorm = smoothstep(0.0, 1.0, distNorm);
                distNorm = pow(distNorm, _EdgePower);

                float edgeBoost = distNorm * _BoostIntensity;

                // ---------- Lens distortion ----------
                float distortionFactor = 1.0 + _DistortionStrength * edgeBoost;
                float2 distortedOffset = offset * distortionFactor;
                float2 uvDistorted     = center + distortedOffset;
                uvDistorted = saturate(uvDistorted);

                // ---------- Base scene color ----------
                float4 col0 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvDistorted);

                // ---------- Radial blur (edge-weighted) ----------
                float blurScale = _BlurStrength * edgeBoost;

                float2 uv1 = saturate(uvDistorted + dir * (blurScale * 1.0));
                float2 uv2 = saturate(uvDistorted + dir * (blurScale * 2.0));
                float2 uv3 = saturate(uvDistorted + dir * (blurScale * 3.0));

                float4 c1 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv1);
                float4 c2 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv2);
                float4 c3 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv3);

                // slight weighting for nicer blur
                float4 blurred = (c1 * 0.5 + c2 * 0.3 + c3 * 0.2);

                float4 blurredMix = lerp(col0, blurred, 0.7);
                float4 finalCol   = lerp(col0, blurredMix, edgeBoost);

                // ---------- Chromatic aberration (edge-weighted) ----------
                float caAmount = _ChromaticOffset * edgeBoost;

                float2 uvR = saturate(uvDistorted + dir * caAmount);
                float2 uvG = uvDistorted;
                float2 uvB = saturate(uvDistorted - dir * caAmount);

                float r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvR).r;
                float g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvG).g;
                float b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvB).b;

                float3 caColor = float3(r, g, b);

                finalCol.rgb = lerp(finalCol.rgb, caColor, edgeBoost * _ChromaticStrength);

                return finalCol * _Tint;
            }

            ENDHLSL
        }
    }
}
