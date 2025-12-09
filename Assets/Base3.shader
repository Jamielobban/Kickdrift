Shader "Custom/ToonThreeBandEtchRim_Noise"
{
    Properties
    {
        _BaseMap        ("Base Map", 2D)               = "white" {}
        _BaseColor      ("Base Color", Color)          = (1,1,1,1)

        _ShadowColor    ("Shadow Color (core)", Color) = (0,0,0,1)

        _RampThreshold  ("Ramp Threshold", Range(0,1))    = 0.5
        _RampSmoothness ("Ramp Smoothness", Range(0,0.5)) = 0.05

        _HatchTex       ("Hatch Texture", 2D)          = "gray" {}
        _HatchOpacity   ("Hatch Opacity", Range(0,5))  = 1
        _HatchTiling    ("Hatch Tiling", Vector)       = (8,8,0,0)

        // LIGHT RAMP BANDS (shadow / mid / lit)
        _ShadowEnd      ("Shadow End", Range(0,1))        = 0.3
        _EtchEnd        ("Shadow Etch End", Range(0,1))   = 0.6

        _EtchDarkFactor ("Etch Dark Factor", Range(0,1))  = 0.5
        _EtchStrength   ("Etch Strength", Range(0,2))     = 1

        // RIM: solid under-band + hatch stripes band
        [HDR]_RimEtchColor  ("Rim Etch Color", Color)     = (2,2,0,1)
        [HDR]_RimSolidColor ("Rim Solid Color", Color)    = (1,1,1,1)
        _RimPower       ("Rim Power", Range(0.1,8))       = 2

        // inner solid band (under the stripes)
        _RimSolidStart  ("Rim Solid Start", Range(0,1))   = 0.6
        _RimSolidEnd    ("Rim Solid End", Range(0,1))     = 0.8

        // stripe band (slightly further out)
        _RimBandStart   ("Rim Stripe Start", Range(0,1))  = 0.7
        _RimBandEnd     ("Rim Stripe End", Range(0,1))    = 0.95

        _RimEtchStrength("Rim Etch Strength", Range(0,2)) = 1

        [Toggle(_USE_NOISE)] _NoiseToggle ("Use Noise", Float) = 1
        _NoiseScale     ("Noise Scale", Float)           = 1
        _NoiseStrength  ("Noise Strength", Range(0,1))   = 0.1
        _NoiseSpeed     ("Noise Speed", Float)           = 1

        _GlobalColor    ("Global Color", Color)          = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            Blend One Zero

            HLSLPROGRAM

            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #pragma shader_feature_local _USE_NOISE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_HatchTex);
            SAMPLER(sampler_HatchTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowColor;

                float  _RampThreshold;
                float  _RampSmoothness;

                float4 _HatchTex_ST;
                float  _HatchOpacity;
                float4 _HatchTiling;

                float  _ShadowEnd;
                float  _EtchEnd;
                float  _EtchDarkFactor;
                float  _EtchStrength;

                float4 _RimEtchColor;
                float4 _RimSolidColor;
                float  _RimPower;
                float  _RimSolidStart;
                float  _RimSolidEnd;
                float  _RimBandStart;
                float  _RimBandEnd;
                float  _RimEtchStrength;

                float  _NoiseScale;
                float  _NoiseStrength;
                float  _NoiseSpeed;

                float4 _GlobalColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float2 uvBase      : TEXCOORD1;
                float2 uvHatch     : TEXCOORD2;
                float3 positionWS  : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
            };

            // ===== simplex noise =====
            float3 mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float4 mod289(float4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float4 permute(float4 x){ return mod289((x * 34.0 + 1.0) * x); }
            float4 taylorInvSqrt(float4 r) { return 1.79284291400159 - 0.85373472095314 * r; }

            void snoise_float(float3 v, out float output)
            {
                const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);

                float3 i  = floor(v + dot(v, C.yyy));
                float3 x0 = v - i + dot(i, C.xxx);

                float3 g = step(x0.yzx, x0.xyz);
                float3 l = 1.0 - g;
                float3 i1 = min(g.xyz, l.zxy);
                float3 i2 = max(g.xyz, l.zxy);

                float3 x1 = x0 - i1 + C.xxx;
                float3 x2 = x0 - i2 + C.yyy;
                float3 x3 = x0 - 0.5;

                i = mod289(i);
                float4 p =
                    permute(permute(permute(i.z + float4(0.0, i1.z, i2.z, 1.0))
                    + i.y + float4(0.0, i1.y, i2.y, 1.0))
                    + i.x + float4(0.0, i1.x, i2.x, 1.0));

                float4 j = p - 49.0 * floor(p * (1.0 / 49.0));

                float4 x_ = floor(j * (1.0 / 7.0));
                float4 y_ = floor(j - 7.0 * x_);

                float4 x = x_ * (2.0 / 7.0) + 0.5 / 7.0 - 1.0;
                float4 y = y_ * (2.0 / 7.0) + 0.5 / 7.0 - 1.0;

                float4 h = 1.0 - abs(x) - abs(y);

                float4 b0 = float4(x.xy, y.xy);
                float4 b1 = float4(x.zw, y.zw);

                float4 s0 = floor(b0) * 2.0 + 1.0;
                float4 s1 = floor(b1) * 2.0 + 1.0;
                float4 sh = -step(h, float4(0,0,0,0));

                float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
                float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

                float3 g0 = float3(a0.xy, h.x);
                float3 g1 = float3(a0.zw, h.y);
                float3 g2 = float3(a1.xy, h.z);
                float3 g3 = float3(a1.zw, h.w);

                float4 norm = taylorInvSqrt(float4(dot(g0,g0), dot(g1,g1),
                                                   dot(g2,g2), dot(g3,g3)));
                g0 *= norm.x;
                g1 *= norm.y;
                g2 *= norm.z;
                g3 *= norm.w;

                float4 m = max(0.6 - float4(dot(x0,x0),
                                            dot(x1,x1),
                                            dot(x2,x2),
                                            dot(x3,x3)), 0.0);
                m = m * m;
                m = m * m;

                float4 px = float4(dot(x0,g0),
                                   dot(x1,g1),
                                   dot(x2,g2),
                                   dot(x3,g3));
                output = 42.0 * dot(m, px);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 posWS    = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                normalWS = normalize(normalWS);

                #ifdef _USE_NOISE
                float timeOffset = _Time.y * _NoiseSpeed;
                float3 noisePos  = posWS * _NoiseScale + float3(0.0, 0.0, timeOffset);

                float noise;
                snoise_float(noisePos, noise);
                posWS += normalWS * (noise * _NoiseStrength);
                #endif

                OUT.positionWS  = posWS;
                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.normalWS    = normalWS;

                OUT.uvBase  = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.uvHatch = TRANSFORM_TEX(IN.uv, _HatchTex);

                OUT.shadowCoord = TransformWorldToShadowCoord(posWS);

                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);

                // main light
                Light mainLight = GetMainLight(IN.shadowCoord);
                float3 L = normalize(mainLight.direction);
                float ndotl = saturate(dot(N, L));
                float lit   = ndotl * mainLight.shadowAttenuation;

                // toon ramp coord
                float t0 = _RampThreshold - _RampSmoothness;
                float t1 = _RampThreshold + _RampSmoothness;
                float coord = smoothstep(t0, t1, lit);

                // base color
                float4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uvBase);
                float4 baseCol = baseTex * _BaseColor;

                float3 litColor   = baseCol.rgb;
                float3 shadowFlat = _ShadowColor.rgb;

                float3 bandColor;
                if (coord <= _ShadowEnd)
                    bandColor = shadowFlat;
                else if (coord >= _EtchEnd)
                    bandColor = litColor;
                else
                    bandColor = litColor;

                // ===== hatch sample -> stroke mask =====
                float2 hatchUV     = IN.uvHatch * _HatchTiling.xy;
                float  hatchSample = SAMPLE_TEXTURE2D(_HatchTex, sampler_HatchTex, hatchUV).r;

                // DARK lines (low value) -> strokes = 1
                float strokeMask = hatchSample < 0.5 ? 1.0 : 0.0;

                // ===== SHADOW-SIDE ETCH =====
                float inShadowEtchBand =
                    step(_ShadowEnd, coord) * (1.0 - step(_EtchEnd, coord));

                float bandT    = saturate((coord - _ShadowEnd) /
                                          max(1e-5, _EtchEnd - _ShadowEnd));
                float bandFade = 1.0 - bandT;

                float shadowEtchMask =
                    strokeMask * inShadowEtchBand * bandFade *
                    _EtchStrength * _HatchOpacity;

                shadowEtchMask = saturate(shadowEtchMask);

                float3 darkTarget  = lerp(bandColor, shadowFlat, _EtchDarkFactor);
                float3 finalColor  = lerp(bandColor, darkTarget, shadowEtchMask);

                // ===== RIM: SOLID INNER BAND + STRIPES BAND =====
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float nv   = saturate(dot(N, V));
                float edge = 1.0 - nv;
                float fres = pow(edge, _RimPower);   // 0 front, 1 silhouette

                // 1) SOLID UNDER-BAND
                float solidBandMask =
                    step(_RimSolidStart, fres) * (1.0 - step(_RimSolidEnd, fres));

                float3 rimSolidCol = _RimSolidColor.rgb;
                finalColor = lerp(finalColor, rimSolidCol, solidBandMask);

                // 2) STRIPE BAND (on top, slightly further out)
                float stripeBandMask =
                    step(_RimBandStart, fres) * (1.0 - step(_RimBandEnd, fres));

                float rimEtchMask =
                    strokeMask * stripeBandMask * _RimEtchStrength * _HatchOpacity;
                rimEtchMask = saturate(rimEtchMask);

                float3 rimEtchCol = _RimEtchColor.rgb;
                finalColor = lerp(finalColor, rimEtchCol, rimEtchMask);

                // global tint
                finalColor *= _GlobalColor.rgb;
                float alpha = baseCol.a * _GlobalColor.a;

                return float4(finalColor, alpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
