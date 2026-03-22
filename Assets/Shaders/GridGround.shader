Shader "Blob3D/GridGround"
{
    // Ground grid shader with gradient coloring, subtle reflection, and distance fade.
    // URP compatible. Agar.io-style grid with polished visuals.

    Properties
    {
        _BaseColor ("Base Color", Color) = (0.08, 0.10, 0.14, 1.0)
        _GridColor ("Grid Color", Color) = (0.15, 0.18, 0.25, 1.0)
        _CenterColor ("Center Color", Color) = (0.12, 0.15, 0.22, 1.0)
        _GridSize ("Grid Size", Float) = 8.0
        _GridWidth ("Grid Width", Range(0.001, 0.1)) = 0.008
        _FadeDistance ("Fade Distance", Float) = 150.0
        _GradientRadius ("Gradient Radius", Float) = 120.0
        _ReflectIntensity ("Reflect Intensity", Range(0, 0.5)) = 0.12
        _AOFadeStart ("AO Fade Start", Float) = 80.0
        _AOIntensity ("AO Intensity", Range(0, 1)) = 0.35
        _CausticIntensity ("Caustic Intensity", Range(0, 0.5)) = 0.15
        _CausticScale ("Caustic Scale", Float) = 0.08
        _CausticSpeed ("Caustic Speed", Float) = 0.5
        _CausticColor ("Caustic Color", Color) = (0.3, 0.5, 0.8, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _GridColor;
                half4 _CenterColor;
                float _GridSize;
                float _GridWidth;
                float _FadeDistance;
                float _GradientRadius;
                half _ReflectIntensity;
                float _AOFadeStart;
                half _AOIntensity;
                half _CausticIntensity;
                float _CausticScale;
                float _CausticSpeed;
                half4 _CausticColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 worldXZ = input.positionWS.xz;
                float dist = length(worldXZ);

                // Thinner, anti-aliased grid lines
                float2 gridUV = worldXZ / _GridSize;
                float2 grid = abs(frac(gridUV - 0.5) - 0.5);
                float2 fw = fwidth(gridUV);
                float2 gridAA = smoothstep(fw * 0.5, fw * 1.5, grid);
                float gridMask = 1.0 - min(gridAA.x, gridAA.y);

                // Distance fade for grid lines
                float gridFade = 1.0 - saturate(dist / _FadeDistance);
                gridFade = gridFade * gridFade; // Quadratic falloff for smoother fade

                // Radial gradient — lighter at center, darker at edges
                float gradientT = saturate(dist / _GradientRadius);
                gradientT = gradientT * gradientT; // Smooth quadratic curve
                half3 baseColor = lerp(_CenterColor.rgb, _BaseColor.rgb, gradientT);

                // Compose grid on top of gradient base
                half3 color = lerp(baseColor, _GridColor.rgb, gridMask * gridFade);

                // Animated caustic pattern — layered sine waves for underwater feel
                float2 caustUV = worldXZ * _CausticScale;
                float c1 = sin(caustUV.x * 3.7 + _Time.y * _CausticSpeed) *
                           sin(caustUV.y * 3.3 + _Time.y * _CausticSpeed * 0.6);
                float c2 = sin(caustUV.x * 5.1 - _Time.y * _CausticSpeed * 1.4) *
                           sin(caustUV.y * 4.7 + _Time.y * _CausticSpeed * 0.8);
                float c3 = sin((caustUV.x + caustUV.y) * 2.3 + _Time.y * _CausticSpeed * 0.4) * 0.5;
                float caustic = saturate(c1 + c2 + c3) * _CausticIntensity;
                // Caustics stronger near center, fade with distance
                caustic *= (1.0 - gradientT);
                color += caustic * _CausticColor.rgb;

                // Ambient occlusion-like darkening near distance fade
                float aoT = saturate((dist - _AOFadeStart) / (_FadeDistance - _AOFadeStart));
                aoT = aoT * aoT; // Smooth falloff
                color *= lerp(1.0, 1.0 - _AOIntensity, aoT);

                // Subtle reflective quality — fake reflection using view angle
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float NdotV = saturate(dot(normalWS, viewDirWS));
                // Fresnel-based reflection at grazing angles
                float reflectFresnel = pow(1.0 - NdotV, 4.0) * _ReflectIntensity;
                // Sample ambient as reflection color
                float3 reflectDir = reflect(-viewDirWS, normalWS);
                half3 envColor = SampleSH(reflectDir);
                color += envColor * reflectFresnel * gridFade;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
