Shader "Blob3D/BoundaryRipple"
{
    // Animated energy barrier / water's edge boundary shader.
    // Renders as a ring with animated wave/ripple effect, inner glow, and fade.
    // Used by FieldBoundary via MeshRenderer on a procedural ring mesh.
    // URP compatible, mobile-friendly (no texture samples).

    Properties
    {
        _Color ("Base Color", Color) = (0.2, 0.6, 1.0, 0.6)
        _PulseColor ("Pulse Color", Color) = (1.0, 0.3, 0.3, 0.8)
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.0
        _RippleSpeed ("Ripple Speed", Float) = 2.0
        _RippleFreq ("Ripple Frequency", Float) = 8.0
        _RippleAmp ("Ripple Amplitude", Float) = 0.3
        _GlowIntensity ("Glow Intensity", Range(0, 3)) = 1.5
        _FadeEdge ("Fade Edge Softness", Range(0.01, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "BoundaryRipple"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _PulseColor;
                half _PulseAmount;
                float _RippleSpeed;
                float _RippleFreq;
                float _RippleAmp;
                half _GlowIntensity;
                half _FadeEdge;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                // UV.x = angular position (0..1), UV.y = radial (0=inner, 1=outer)
                float3 posOS = input.positionOS.xyz;

                // Animate vertex Y with ripple wave
                float angle = input.uv.x * 6.28318; // 2*PI
                float wave = sin(angle * _RippleFreq + _Time.y * _RippleSpeed) * _RippleAmp;
                wave += sin(angle * _RippleFreq * 0.7 - _Time.y * _RippleSpeed * 1.3) * _RippleAmp * 0.5;
                posOS.y += wave * input.uv.y; // Only displace outer edge

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Radial fade: bright at center of ring band, fade at edges
                float edgeFade = smoothstep(0.0, _FadeEdge, uv.y) * smoothstep(1.0, 1.0 - _FadeEdge, uv.y);

                // Animated shimmer along angular direction
                float shimmer = sin(uv.x * 50.0 + _Time.y * _RippleSpeed * 3.0) * 0.5 + 0.5;
                shimmer = shimmer * 0.3 + 0.7; // Subtle brightness variation

                // Blend base color and pulse color
                half3 color = lerp(_Color.rgb, _PulseColor.rgb, _PulseAmount);

                // Add glow
                color *= _GlowIntensity * shimmer;

                // Alpha: base alpha modulated by edge fade
                float alpha = lerp(_Color.a, _PulseColor.a, _PulseAmount) * edgeFade;

                // Inner glow contribution (brighter near inner edge)
                float innerGlow = smoothstep(0.5, 0.0, uv.y) * 0.4;
                color += innerGlow * color;

                color = MixFog(color, input.fogFactor);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
