Shader "Blob3D/GridGround"
{
    // Natural terrain shader with grass patches, subtle height undulation,
    // earthy color variation, and distance fog. URP compatible.
    // Designed for mobile performance with minimal texture samples.

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

        // Terrain properties
        _GrassColor1 ("Grass Color 1", Color) = (0.15, 0.28, 0.08, 1.0)
        _GrassColor2 ("Grass Color 2", Color) = (0.22, 0.38, 0.12, 1.0)
        _DirtColor ("Dirt Color", Color) = (0.18, 0.14, 0.09, 1.0)
        _GrassPatchScale ("Grass Patch Scale", Float) = 0.04
        _GrassBladeScale ("Grass Blade Scale", Float) = 0.25
        _GrassDensity ("Grass Density", Range(0, 1)) = 0.6
        _TerrainBumpScale ("Terrain Bump Scale", Float) = 0.015
        _TerrainBumpFreq ("Terrain Bump Frequency", Float) = 0.05
        _HeightDisplacement ("Height Displacement", Float) = 0.8
        _HeightFrequency ("Height Frequency", Float) = 0.02
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
                half4 _GrassColor1;
                half4 _GrassColor2;
                half4 _DirtColor;
                float _GrassPatchScale;
                float _GrassBladeScale;
                half _GrassDensity;
                float _TerrainBumpScale;
                float _TerrainBumpFreq;
                float _HeightDisplacement;
                float _HeightFrequency;
            CBUFFER_END

            // Simple hash functions for procedural noise (no texture lookups)
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Value noise for smooth terrain variation
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // Smoothstep

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Fractal brownian motion — 2 octaves for mobile performance
            float fbm2(float2 p)
            {
                float v = 0.0;
                v += 0.5 * valueNoise(p);
                p *= 2.03;
                v += 0.25 * valueNoise(p);
                return v / 0.75;
            }

            // Multi-octave terrain height function
            float terrainHeight(float2 xz)
            {
                float h = 0.0;
                h += fbm2(xz * _HeightFrequency) * _HeightDisplacement;
                h += fbm2(xz * _HeightFrequency * 3.7) * _HeightDisplacement * 0.2;
                return h;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Apply gentle vertex displacement for terrain undulation
                float3 posOS = input.positionOS.xyz;
                float3 posWS = TransformObjectToWorld(posOS);

                // Displace Y based on world XZ position
                float heightOffset = terrainHeight(posWS.xz);
                posOS.y += heightOffset;

                // Recalculate normal from displaced surface (finite differences)
                float eps = 0.5;
                float3 posWSNew = TransformObjectToWorld(posOS);
                float hL = terrainHeight(posWSNew.xz - float2(eps, 0));
                float hR = terrainHeight(posWSNew.xz + float2(eps, 0));
                float hD = terrainHeight(posWSNew.xz - float2(0, eps));
                float hU = terrainHeight(posWSNew.xz + float2(0, eps));
                float3 terrainNormal = normalize(float3(hL - hR, 2.0 * eps, hD - hU));

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                output.normalWS = TransformObjectToWorldNormal(terrainNormal);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 worldXZ = input.positionWS.xz;
                float dist = length(worldXZ);

                // --- Grass patch mask using layered noise ---
                float patchNoise = fbm2(worldXZ * _GrassPatchScale);
                float grassMask = smoothstep(1.0 - _GrassDensity - 0.1, 1.0 - _GrassDensity + 0.1, patchNoise);

                // Grass blade-like detail: high frequency directional hash
                float bladeDetail = hash21(floor(worldXZ * _GrassBladeScale * 40.0));
                float bladeMask = smoothstep(0.3, 0.7, bladeDetail) * grassMask;

                // Height-based grass color variation: higher areas are lighter/sunlit
                float grassHeight = terrainHeight(worldXZ);
                float heightTint = saturate(grassHeight / (_HeightDisplacement + 0.001));

                // Blend between two grass tones with blade variation + height shift
                half3 grassColor = lerp(_GrassColor1.rgb, _GrassColor2.rgb, bladeMask);
                grassColor = lerp(grassColor, grassColor * 1.25 + half3(0.03, 0.05, 0.0), heightTint * 0.5);

                // Dirt/earth color where grass is absent
                half3 dirtColor = _DirtColor.rgb * (0.9 + 0.1 * hash21(floor(worldXZ * 2.0)));

                // Terrain base color: mix grass and dirt
                half3 terrainBase = lerp(dirtColor, grassColor, grassMask);

                // --- Original grid overlay (subtle, faded) ---
                float2 gridUV = worldXZ / _GridSize;
                float2 grid = abs(frac(gridUV - 0.5) - 0.5);
                float2 fw = fwidth(gridUV);
                float2 gridAA = smoothstep(fw * 0.5, fw * 1.5, grid);
                float gridMask = 1.0 - min(gridAA.x, gridAA.y);

                // Distance fade for grid lines
                float gridFade = 1.0 - saturate(dist / _FadeDistance);
                gridFade = gridFade * gridFade;

                // Radial gradient — lighter at center, darker at edges
                float gradientT = saturate(dist / _GradientRadius);
                gradientT = gradientT * gradientT;

                // Blend terrain base with radial gradient
                half3 baseColor = lerp(_CenterColor.rgb, _BaseColor.rgb, gradientT);
                half3 color = lerp(terrainBase, baseColor, 0.35); // 65% terrain, 35% original gradient

                // Very faint grid overlay
                color = lerp(color, _GridColor.rgb, gridMask * gridFade * 0.3);

                // Animated caustic pattern — height-aware, only in low areas (puddles)
                float terrainH = terrainHeight(worldXZ);
                float causticHeightMask = saturate(1.0 - terrainH / (_HeightDisplacement * 0.5 + 0.001));
                causticHeightMask = causticHeightMask * causticHeightMask; // Concentrate in valleys
                float2 caustUV = worldXZ * _CausticScale;
                float c1 = sin(caustUV.x * 3.7 + _Time.y * _CausticSpeed) *
                           sin(caustUV.y * 3.3 + _Time.y * _CausticSpeed * 0.6);
                float c2 = sin(caustUV.x * 5.1 - _Time.y * _CausticSpeed * 1.4) *
                           sin(caustUV.y * 4.7 + _Time.y * _CausticSpeed * 0.8);
                float c3 = sin((caustUV.x + caustUV.y) * 2.3 + _Time.y * _CausticSpeed * 0.4) * 0.5;
                float caustic = saturate(c1 + c2 + c3) * _CausticIntensity * 0.5;
                caustic *= (1.0 - gradientT) * causticHeightMask;
                color += caustic * _CausticColor.rgb;

                // Ambient occlusion-like darkening near distance fade
                float aoT = saturate((dist - _AOFadeStart) / (_FadeDistance - _AOFadeStart));
                aoT = aoT * aoT;
                color *= lerp(1.0, 1.0 - _AOIntensity, aoT);

                // Micro-bump shading from terrain normal for surface detail
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Simple directional light shading based on terrain normal
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                color *= (0.7 + 0.3 * NdotL); // Subtle directional shading

                // Roughness variation: dirt areas are rougher, grass is smoother
                float surfaceRoughness = lerp(0.85, 0.55, grassMask); // Dirt=0.85, Grass=0.55
                // Low areas (puddles) are very smooth/reflective
                surfaceRoughness = lerp(surfaceRoughness, 0.2, causticHeightMask * 0.6);
                float smoothFactor = 1.0 - surfaceRoughness;

                // Subtle reflective quality — Fresnel-based, modulated by roughness
                float NdotV = saturate(dot(normalWS, viewDirWS));
                float reflectFresnel = pow(1.0 - NdotV, 4.0) * _ReflectIntensity * smoothFactor;
                float3 reflectDir = reflect(-viewDirWS, normalWS);
                half3 envColor = SampleSH(reflectDir);
                color += envColor * reflectFresnel * gridFade;

                // Roughness-based specular dampening on grass vs dirt
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float microSpec = pow(NdotH, lerp(16.0, 128.0, smoothFactor)) * smoothFactor * 0.15;
                color += mainLight.color * microSpec;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
