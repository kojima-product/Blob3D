Shader "Blob3D/BlobSurface"
{
    // Translucent gel/jelly blob shader with subsurface scattering, pseudo-refraction,
    // organic pulsing, and chromatic rim highlights. URP compatible.

    Properties
    {
        _BaseColor ("Base Color", Color) = (0.2, 0.7, 1.0, 1.0)
        _SubsurfaceColor ("Subsurface Color", Color) = (1.0, 0.3, 0.1, 1.0)
        _FresnelPower ("Fresnel Power", Range(0.5, 8.0)) = 3.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 3)) = 1.2
        _SubsurfaceIntensity ("Subsurface Intensity", Range(0, 3)) = 0.8
        _SubsurfaceDistortion ("Subsurface Distortion", Range(0, 1)) = 0.5
        _SubsurfaceWrap ("Subsurface Wrap", Range(0, 1)) = 0.6
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _InnerGlow ("Inner Glow", Range(0, 1)) = 0.4
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(1, 8)) = 2.5
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1.5
        _PulseAmount ("Pulse Amount", Range(0, 0.1)) = 0.01
        _WobbleSpeed ("Wobble Speed", Range(0, 10)) = 3.0
        _WobbleAmount ("Wobble Amount", Range(0, 0.1)) = 0.008
        _EnvReflectIntensity ("Env Reflect Intensity", Range(0, 1)) = 0.35
        _Opacity ("Opacity", Range(0.5, 1.0)) = 0.82
        _RefractionStrength ("Refraction Strength", Range(0, 0.15)) = 0.06
        _ChromaticSpread ("Chromatic Rim Spread", Range(0, 0.3)) = 0.08
        _DepthColorShift ("Depth Color Shift", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float3 positionOS : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SubsurfaceColor;
                half _FresnelPower;
                half _FresnelIntensity;
                half _SubsurfaceIntensity;
                half _SubsurfaceDistortion;
                half _SubsurfaceWrap;
                half _Smoothness;
                half _Metallic;
                half _InnerGlow;
                half4 _RimColor;
                half _RimPower;
                half _PulseSpeed;
                half _PulseAmount;
                half _WobbleSpeed;
                half _WobbleAmount;
                half _EnvReflectIntensity;
                half _Opacity;
                half _RefractionStrength;
                half _ChromaticSpread;
                half _DepthColorShift;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;

                // Breathing pulse — gentle sine-based scale oscillation
                float pulse = sin(_Time.y * _PulseSpeed) * _PulseAmount;
                posOS += normalOS * pulse;

                // Organic wobble — different frequencies per axis for living feel
                float wobbleX = sin(_Time.y * _WobbleSpeed + posOS.y * 4.0) * _WobbleAmount;
                float wobbleY = sin(_Time.y * _WobbleSpeed * 1.3 + posOS.x * 3.5) * _WobbleAmount * 0.7;
                float wobbleZ = sin(_Time.y * _WobbleSpeed * 0.9 + posOS.z * 4.5) * _WobbleAmount * 0.8;
                posOS += normalOS * (wobbleX + wobbleY + wobbleZ);

                // Gravity-based slime deformation: wider base, narrower top (dome shape)
                float heightFactor = posOS.y; // -0.5 to 0.5 for unit sphere
                float gravityBulge = -heightFactor * 0.12; // Push bottom out, pull top in
                float2 xzDir = normalize(posOS.xz + 0.001);
                posOS.xz += xzDir * gravityBulge;
                posOS.y *= 0.88; // Flatten vertically by 12%

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                output.positionOS = posOS;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Main light
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float NdotV = saturate(dot(normalWS, viewDirWS));

                // Enhanced fresnel — stronger edge glow for glossy jelly
                float fresnel = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;

                // Improved subsurface scattering — light wrapping around the blob
                float3 sssLightDir = mainLight.direction + normalWS * _SubsurfaceDistortion;
                float sssDot = saturate(dot(viewDirWS, -sssLightDir));
                float sssFalloff = pow(sssDot, 3.0);

                // Wrap lighting — light wraps around edges for translucent feel
                float wrapDiffuse = saturate((dot(normalWS, mainLight.direction) + _SubsurfaceWrap) / (1.0 + _SubsurfaceWrap));

                float subsurface = (sssFalloff + wrapDiffuse * 0.3) * _SubsurfaceIntensity;

                // Base color with wrapped diffuse lighting
                half3 diffuse = _BaseColor.rgb * (wrapDiffuse * 0.7 + 0.3) * mainLight.color;

                // Subsurface color contribution
                half3 sss = _SubsurfaceColor.rgb * subsurface * mainLight.color;

                // Pseudo-refraction — distort color based on view angle through the gel
                float3 refractDir = refract(-viewDirWS, normalWS, 0.92);
                float refractShift = (dot(refractDir.xz, float2(1.0, 0.7)) * 0.5 + 0.5);
                half3 refractTint = lerp(_BaseColor.rgb, _SubsurfaceColor.rgb, refractShift * _RefractionStrength * 10.0);
                half3 refractionColor = refractTint * _RefractionStrength * (1.0 - NdotV);

                // Depth-based color shift — deeper areas shift toward subsurface color
                half3 depthColor = lerp(_BaseColor.rgb, _SubsurfaceColor.rgb * 0.6, (1.0 - NdotV) * _DepthColorShift);
                diffuse = lerp(diffuse, depthColor * mainLight.color, (1.0 - NdotV) * 0.4);

                // Inner glow — depth-based glow for organic feel
                float depthGlow = pow(1.0 - NdotV, 1.5);
                half3 innerGlow = _BaseColor.rgb * _InnerGlow * depthGlow;

                // Pulsing inner glow — subtle breathing luminance
                float glowPulse = sin(_Time.y * _PulseSpeed * 2.0) * 0.15 + 0.85;
                innerGlow *= glowPulse;

                // Chromatic rim — rainbow dispersion at edges for glass/gel feel
                float rimBase = pow(1.0 - NdotV, _RimPower);
                half3 chromaRim = half3(
                    pow(1.0 - NdotV, _RimPower - _ChromaticSpread * 8.0),
                    pow(1.0 - NdotV, _RimPower),
                    pow(1.0 - NdotV, _RimPower + _ChromaticSpread * 8.0)
                );
                half3 rimColor = _RimColor.rgb * chromaRim * fresnel;

                // Specular approximation — sharper for glossy jelly
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float spec = pow(saturate(dot(normalWS, halfDir)), _Smoothness * 256.0) * _Smoothness;
                half3 specular = mainLight.color * spec * 0.7;

                // Secondary specular highlight for wet look (offset angle)
                float3 halfDir2 = normalize(mainLight.direction * 0.8 + viewDirWS + float3(0.1, 0.2, 0));
                float spec2 = pow(saturate(dot(normalWS, halfDir2)), _Smoothness * 128.0) * _Smoothness * 0.3;
                specular += mainLight.color * spec2;

                // Environment reflection sampling — glossy jelly surface
                float3 reflectDir = reflect(-viewDirWS, normalWS);
                half3 envColor = SAMPLE_GI(float2(0,0), SampleSH(reflectDir), normalWS);
                half3 envReflect = envColor * _EnvReflectIntensity * _Smoothness;
                envReflect *= saturate(fresnel + 0.15);

                // Final composition
                half3 finalColor = diffuse + sss + refractionColor + innerGlow + rimColor + specular + envReflect;

                // Apply fog
                finalColor = MixFog(finalColor, input.fogFactor);

                // Fresnel-based opacity: edges opaque, center translucent (gel/jelly look)
                float alpha = _Opacity * (0.65 + fresnel * 0.35);
                alpha = saturate(alpha);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // Depth pass for proper transparency sorting
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SubsurfaceColor;
                half _FresnelPower;
                half _FresnelIntensity;
                half _SubsurfaceIntensity;
                half _SubsurfaceDistortion;
                half _SubsurfaceWrap;
                half _Smoothness;
                half _Metallic;
                half _InnerGlow;
                half4 _RimColor;
                half _RimPower;
                half _PulseSpeed;
                half _PulseAmount;
                half _WobbleSpeed;
                half _WobbleAmount;
                half _EnvReflectIntensity;
                half _Opacity;
                half _RefractionStrength;
                half _ChromaticSpread;
                half _DepthColorShift;
            CBUFFER_END

            DepthVaryings depthVert(DepthAttributes input)
            {
                DepthVaryings output;
                float3 posOS = input.positionOS.xyz;
                float pulse = sin(_Time.y * _PulseSpeed) * _PulseAmount;
                posOS += input.normalOS * pulse;
                output.positionCS = TransformObjectToHClip(posOS);
                return output;
            }

            half4 depthFrag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
