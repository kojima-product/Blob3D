Shader "Blob3D/BlobSurface"
{
    // Translucent gel/jelly blob shader with physically-based subsurface scattering,
    // refraction, internal bubbles, caustics, iridescence, surface ripples, and
    // contact shadows. URP compatible, mobile-friendly.

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

        // New realism properties
        _IridescenceStrength ("Iridescence Strength", Range(0, 1)) = 0.15
        _BubbleIntensity ("Bubble Intensity", Range(0, 1)) = 0.25
        _BubbleScale ("Bubble Scale", Range(1, 20)) = 8.0
        _BubbleSpeed ("Bubble Speed", Range(0, 3)) = 0.8
        _CausticStrength ("Caustic Strength", Range(0, 1)) = 0.3
        _ContactShadowStrength ("Contact Shadow Strength", Range(0, 1)) = 0.4
        _RippleSpeed ("Ripple Speed", Range(0, 5)) = 2.0
        _RippleScale ("Ripple Scale", Range(1, 15)) = 6.0
        _RippleAmount ("Ripple Amount", Range(0, 0.03)) = 0.005
        _WetSpecPower ("Wet Specular Power", Range(64, 1024)) = 512
        _WetSpecIntensity ("Wet Specular Intensity", Range(0, 3)) = 1.5
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
                float4 tangentOS : TANGENT;
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
                float4 screenPos : TEXCOORD5;
                float3 tangentWS : TEXCOORD6;
                float3 bitangentWS : TEXCOORD7;
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
                half _IridescenceStrength;
                half _BubbleIntensity;
                half _BubbleScale;
                half _BubbleSpeed;
                half _CausticStrength;
                half _ContactShadowStrength;
                half _RippleSpeed;
                half _RippleScale;
                half _RippleAmount;
                half _WetSpecPower;
                half _WetSpecIntensity;
            CBUFFER_END

            // --- Noise utilities (computed in vertex shader where possible) ---

            // Simple 3D hash for procedural noise
            float hash31(float3 p)
            {
                p = frac(p * float3(443.8975, 397.2973, 491.1871));
                p += dot(p, p.yzx + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            // Smooth value noise (3D)
            float valueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // Smoothstep

                float n000 = hash31(i);
                float n100 = hash31(i + float3(1, 0, 0));
                float n010 = hash31(i + float3(0, 1, 0));
                float n110 = hash31(i + float3(1, 1, 0));
                float n001 = hash31(i + float3(0, 0, 1));
                float n101 = hash31(i + float3(1, 0, 1));
                float n011 = hash31(i + float3(0, 1, 1));
                float n111 = hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);

                return lerp(nxy0, nxy1, f.z);
            }

            // Voronoi distance for bubble/cell pattern (returns distance to nearest cell center)
            float voronoiDistance(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float minDist = 1.0;

                // Check 3x3x3 neighborhood
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int z = -1; z <= 1; z++)
                        {
                            float3 neighbor = float3(x, y, z);
                            float3 cellCenter = neighbor + hash31(i + neighbor) * 0.8 + 0.1;
                            float d = length(f - cellCenter);
                            minDist = min(minDist, d);
                        }
                    }
                }
                return minDist;
            }

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

                // Surface ripple waves — water-tension-like ripples across surface
                float ripplePhase = length(posOS.xz) * _RippleScale - _Time.y * _RippleSpeed;
                float ripple1 = sin(ripplePhase) * _RippleAmount;
                float ripple2 = sin(ripplePhase * 1.7 + 2.1) * _RippleAmount * 0.5;
                float ripple3 = sin(posOS.y * _RippleScale * 1.3 + _Time.y * _RippleSpeed * 0.7) * _RippleAmount * 0.3;
                posOS += normalOS * (ripple1 + ripple2 + ripple3);

                // Gravity-based slime deformation: wider base, narrower top (dome shape)
                float heightFactor = posOS.y;
                float gravityBulge = -heightFactor * 0.12;
                float2 xzDir = normalize(posOS.xz + 0.001);
                posOS.xz += xzDir * gravityBulge;
                posOS.y *= 0.88;

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                output.positionOS = posOS;
                output.screenPos = ComputeScreenPos(posInputs.positionCS);
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float3 tangentWS = normalize(input.tangentWS);
                float3 bitangentWS = normalize(input.bitangentWS);

                // Main light
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float NdotV = saturate(dot(normalWS, viewDirWS));

                // ====== SURFACE RIPPLE NORMAL PERTURBATION ======
                // Perturb normal with animated ripple pattern for water-like surface tension
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.001);
                float rippleNX = sin(input.positionOS.x * _RippleScale + _Time.y * _RippleSpeed) * 0.02
                               + sin(input.positionOS.z * _RippleScale * 1.3 - _Time.y * _RippleSpeed * 0.8) * 0.015;
                float rippleNZ = sin(input.positionOS.z * _RippleScale + _Time.y * _RippleSpeed * 1.1) * 0.02
                               + sin(input.positionOS.x * _RippleScale * 0.9 + _Time.y * _RippleSpeed * 0.6) * 0.015;
                float3 rippleNormal = normalize(normalWS + tangentWS * rippleNX + bitangentWS * rippleNZ);
                normalWS = rippleNormal;

                // Recompute dot products with perturbed normal
                NdotL = saturate(dot(normalWS, mainLight.direction));
                NdotV = saturate(dot(normalWS, viewDirWS));

                // ====== ENHANCED FRESNEL ======
                float fresnel = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;

                // ====== PHYSICALLY-BASED SUBSURFACE SCATTERING ======
                // Burley normalized diffuse with SSS approximation
                float3 sssLightDir = normalize(mainLight.direction + normalWS * _SubsurfaceDistortion);
                float sssDot = saturate(dot(viewDirWS, -sssLightDir));
                // Multi-lobe SSS: forward scattering (narrow) + back scattering (wide)
                float sssForward = pow(sssDot, 6.0) * 0.6;
                float sssBack = pow(sssDot, 2.0) * 0.4;
                float sssFalloff = sssForward + sssBack;

                // Wrap lighting with energy conservation
                float wrapDiffuse = saturate((dot(normalWS, mainLight.direction) + _SubsurfaceWrap) / (1.0 + _SubsurfaceWrap));
                wrapDiffuse *= wrapDiffuse; // Quadratic falloff for softer wrap

                // Thickness approximation from view angle (thinner at edges)
                float thickness = NdotV * 0.7 + 0.3;
                float subsurface = (sssFalloff * thickness + wrapDiffuse * 0.3) * _SubsurfaceIntensity;

                // ====== BASE DIFFUSE ======
                half3 diffuse = _BaseColor.rgb * (wrapDiffuse * 0.7 + 0.3) * mainLight.color;

                // Subsurface color contribution with depth-dependent tinting
                half3 sss = _SubsurfaceColor.rgb * subsurface * mainLight.color;

                // ====== REFRACTION (distort background through gel) ======
                float3 refractDir = refract(-viewDirWS, normalWS, 0.92);
                float refractShift = (dot(refractDir.xz, float2(1.0, 0.7)) * 0.5 + 0.5);
                // Chromatic aberration in refraction — R, G, B refract slightly differently
                float3 refractDirR = refract(-viewDirWS, normalWS, 0.90);
                float3 refractDirG = refract(-viewDirWS, normalWS, 0.92);
                float3 refractDirB = refract(-viewDirWS, normalWS, 0.94);
                half3 refractTint;
                refractTint.r = lerp(_BaseColor.r, _SubsurfaceColor.r, dot(refractDirR.xz, float2(1.0, 0.7)) * 0.5 + 0.5);
                refractTint.g = lerp(_BaseColor.g, _SubsurfaceColor.g, dot(refractDirG.xz, float2(1.0, 0.7)) * 0.5 + 0.5);
                refractTint.b = lerp(_BaseColor.b, _SubsurfaceColor.b, dot(refractDirB.xz, float2(1.0, 0.7)) * 0.5 + 0.5);
                half3 refractionColor = refractTint * _RefractionStrength * (1.0 - NdotV) * 1.5;

                // ====== DEPTH COLOR SHIFT ======
                half3 depthColor = lerp(_BaseColor.rgb, _SubsurfaceColor.rgb * 0.6, (1.0 - NdotV) * _DepthColorShift);
                diffuse = lerp(diffuse, depthColor * mainLight.color, (1.0 - NdotV) * 0.4);

                // ====== INNER GLOW WITH PULSING ======
                float depthGlow = pow(1.0 - NdotV, 1.5);
                half3 innerGlow = _BaseColor.rgb * _InnerGlow * depthGlow;
                float glowPulse = sin(_Time.y * _PulseSpeed * 2.0) * 0.15 + 0.85;
                innerGlow *= glowPulse;

                // ====== INTERNAL BUBBLES / PARTICLES ======
                // Animated voronoi cells create the illusion of tiny bubbles inside the gel
                float3 bubbleCoord = input.positionOS * _BubbleScale + float3(0, _Time.y * _BubbleSpeed, 0);
                float bubbleDist = voronoiDistance(bubbleCoord);
                // Sharp bubble highlights where cell edges are thin
                float bubbleEdge = smoothstep(0.05, 0.15, bubbleDist);
                float bubbleHighlight = (1.0 - bubbleEdge) * _BubbleIntensity;
                // Add secondary smaller bubbles for depth
                float3 bubbleCoord2 = input.positionOS * _BubbleScale * 2.3 + float3(_Time.y * _BubbleSpeed * 0.3, 0, _Time.y * _BubbleSpeed * 0.5);
                float bubbleDist2 = voronoiDistance(bubbleCoord2);
                float bubbleHighlight2 = (1.0 - smoothstep(0.05, 0.12, bubbleDist2)) * _BubbleIntensity * 0.4;
                // Bubbles are more visible toward the interior (use NdotV as depth proxy)
                float bubbleDepthMask = (1.0 - NdotV) * 0.6 + 0.4;
                half3 bubbleColor = mainLight.color * (bubbleHighlight + bubbleHighlight2) * bubbleDepthMask;

                // ====== IRIDESCENCE (thin-film interference approximation) ======
                // Color shifts based on viewing angle — like oil on water or soap film
                float iridAngle = 1.0 - NdotV;
                float iridPhase = iridAngle * 6.2832 * 2.0; // Two full cycles across the surface
                half3 iridescence = half3(
                    sin(iridPhase) * 0.5 + 0.5,
                    sin(iridPhase + 2.094) * 0.5 + 0.5,  // 120 degrees offset
                    sin(iridPhase + 4.189) * 0.5 + 0.5   // 240 degrees offset
                );
                // Iridescence is strongest at grazing angles
                float iridMask = pow(iridAngle, 2.0) * _IridescenceStrength;
                iridescence = lerp(half3(1, 1, 1), iridescence, iridMask);

                // ====== CHROMATIC RIM ======
                float rimBase = pow(1.0 - NdotV, _RimPower);
                half3 chromaRim = half3(
                    pow(1.0 - NdotV, _RimPower - _ChromaticSpread * 8.0),
                    pow(1.0 - NdotV, _RimPower),
                    pow(1.0 - NdotV, _RimPower + _ChromaticSpread * 8.0)
                );
                half3 rimColor = _RimColor.rgb * chromaRim * fresnel;

                // ====== WET/GLOSSY SPECULAR (GGX approximation) ======
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float VdotH = saturate(dot(viewDirWS, halfDir));

                // Primary specular — tight GGX-like highlight for wet gel
                float roughness = 1.0 - _Smoothness;
                float roughSq = max(roughness * roughness, 0.002);
                float denom = NdotH * NdotH * (roughSq - 1.0) + 1.0;
                float D = roughSq / (3.14159 * denom * denom);
                // Schlick fresnel for specular
                float F = 0.04 + 0.96 * pow(1.0 - VdotH, 5.0);
                float specGGX = D * F * _WetSpecIntensity;
                half3 specular = mainLight.color * min(specGGX, 8.0); // Clamp to prevent fireflies

                // Secondary broad specular for wet sheen
                float3 halfDir2 = normalize(mainLight.direction * 0.85 + viewDirWS + float3(0.05, 0.1, 0));
                float spec2 = pow(saturate(dot(normalWS, halfDir2)), _Smoothness * 64.0) * _Smoothness * 0.25;
                specular += mainLight.color * spec2;

                // ====== ENVIRONMENT REFLECTION (responds to movement via ripple normal) ======
                float3 reflectDir = reflect(-viewDirWS, normalWS);
                half3 envColor = SAMPLE_GI(float2(0,0), SampleSH(reflectDir), normalWS);
                half3 envReflect = envColor * _EnvReflectIntensity * _Smoothness;
                // Fresnel-gated reflection (stronger at edges like real dielectric)
                float envFresnel = 0.04 + 0.96 * pow(1.0 - NdotV, 5.0);
                envReflect *= envFresnel;

                // ====== CAUSTIC PATTERN (projected onto ground beneath blob) ======
                // Caustics visible on the bottom hemisphere of the blob
                float causticMask = saturate(-input.positionOS.y * 2.0 + 0.3);
                float2 causticUV = input.positionWS.xz * 3.0;
                float caustic1 = sin(causticUV.x * 5.0 + _Time.y * 1.5) * sin(causticUV.y * 4.0 + _Time.y * 1.1);
                float caustic2 = sin(causticUV.x * 3.7 - _Time.y * 0.9) * sin(causticUV.y * 5.3 + _Time.y * 1.3);
                float causticPattern = saturate((caustic1 + caustic2) * 0.5 + 0.3);
                half3 causticColor = mainLight.color * causticPattern * _CausticStrength * causticMask * NdotL;

                // ====== CONTACT SHADOW (darken where blob meets ground) ======
                // Ground proximity: darken bottom of blob for contact shadow illusion
                float groundProximity = saturate(1.0 - (input.positionOS.y + 0.5) * 2.5);
                groundProximity = groundProximity * groundProximity; // Quadratic falloff
                float contactShadow = 1.0 - groundProximity * _ContactShadowStrength;

                // ====== ADDITIONAL LIGHTS ======
                half3 additionalLightContrib = half3(0, 0, 0);
                #ifdef _ADDITIONAL_LIGHTS
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < pixelLightCount; ++i)
                {
                    Light addLight = GetAdditionalLight(i, input.positionWS);
                    float addNdotL = saturate(dot(normalWS, addLight.direction));
                    float addWrap = saturate((dot(normalWS, addLight.direction) + _SubsurfaceWrap) / (1.0 + _SubsurfaceWrap));
                    additionalLightContrib += _BaseColor.rgb * addWrap * addLight.color * addLight.distanceAttenuation * 0.5;
                    // SSS from additional lights
                    float3 addSSSDir = normalize(addLight.direction + normalWS * _SubsurfaceDistortion);
                    float addSSS = pow(saturate(dot(viewDirWS, -addSSSDir)), 4.0) * _SubsurfaceIntensity * 0.3;
                    additionalLightContrib += _SubsurfaceColor.rgb * addSSS * addLight.color * addLight.distanceAttenuation;
                }
                #endif

                // ====== FINAL COMPOSITION ======
                half3 finalColor = diffuse * iridescence
                                 + sss
                                 + refractionColor
                                 + innerGlow
                                 + rimColor
                                 + specular
                                 + envReflect
                                 + bubbleColor
                                 + causticColor
                                 + additionalLightContrib;

                // Apply contact shadow
                finalColor *= contactShadow;

                // Apply fog
                finalColor = MixFog(finalColor, input.fogFactor);

                // Fresnel-based opacity: edges opaque, center translucent (gel/jelly look)
                float alpha = _Opacity * (0.65 + fresnel * 0.35);
                // Bubbles add slight opacity where they appear
                alpha += (bubbleHighlight + bubbleHighlight2) * 0.1;
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
                half _IridescenceStrength;
                half _BubbleIntensity;
                half _BubbleScale;
                half _BubbleSpeed;
                half _CausticStrength;
                half _ContactShadowStrength;
                half _RippleSpeed;
                half _RippleScale;
                half _RippleAmount;
                half _WetSpecPower;
                half _WetSpecIntensity;
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
