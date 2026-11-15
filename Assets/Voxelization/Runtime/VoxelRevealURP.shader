Shader "Voxelization/URP Voxel Reveal"
{
    Properties
    {
        _RevealProgress ("Reveal Progress", Range(0, 1)) = 0
        _RevealOrigin ("Reveal Origin", Vector) = (0, 0, 0, 0)
        _ScatterDistance ("Scatter Distance", Float) = 1
        _WaveStrength ("Wave Strength", Float) = 0.1
        _TransitionWidth ("Transition Width", Float) = 0.15
        _RevealChaos ("Reveal Chaos", Float) = 0.75
        _ColorIntensity ("Color Intensity", Range(0, 2)) = 1
        _Saturation ("Saturation", Range(0, 2)) = 1
        [HideInInspector] _RevealMaxDistance ("Reveal Max Distance", Float) = 1
        [HideInInspector] _RevealBoundaryCount ("Reveal Boundary Count", Integer) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        #define MAX_REVEAL_BOUNDARIES 16

        CBUFFER_START(UnityPerMaterial)
            float _RevealProgress;
            float3 _RevealOrigin;
            float _ScatterDistance;
            float _WaveStrength;
            float _TransitionWidth;
            float _RevealChaos;
            float _ColorIntensity;
            float _Saturation;
            float _RevealMaxDistance;
            int _RevealBoundaryCount;
            float4 _RevealBoundaries[MAX_REVEAL_BOUNDARIES];
        CBUFFER_END

        struct VoxelAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            float3 voxelCenter : TEXCOORD1;
            float2 voxelRandom : TEXCOORD2;
        };

        float Hash11(float value)
        {
            return frac(sin(value * 127.1) * 43758.5453123);
        }

        float3 GetRevealSource(float3 centerWS)
        {
            if (_RevealBoundaryCount == 0)
            {
                return _RevealOrigin;
            }

            float minimumSqr = 3.402823466e+38;
            float2 nearestPoint = centerWS.xz;
            for (int index = 0; index < _RevealBoundaryCount; index++)
            {
                float4 segment = _RevealBoundaries[index];
                float2 direction = segment.zw - segment.xy;
                float projection = saturate(dot(centerWS.xz - segment.xy, direction) / dot(direction, direction));
                float2 segmentPoint = segment.xy + direction * projection;
                float2 offset = centerWS.xz - segmentPoint;
                float distanceSqr = dot(offset, offset);
                if (distanceSqr < minimumSqr)
                {
                    minimumSqr = distanceSqr;
                    nearestPoint = segmentPoint;
                }
            }

            return float3(nearestPoint.x, centerWS.y, nearestPoint.y);
        }

        float GetReveal(float3 centerWS, float3 sourceWS, float random)
        {
            if (_RevealProgress <= 0.0)
            {
                return 0.0;
            }

            if (_RevealProgress >= 1.0)
            {
                return 1.0;
            }

            float distanceFromOrigin = distance(centerWS, sourceWS);
            float width = max(_TransitionWidth, 0.001);
            float chaos = max(_RevealChaos, 0.0);
            float delay = Hash11(random + 5.0) * chaos;
            float flightWidth = width + lerp(0.5, 1.0, Hash11(random + 6.0)) * chaos;
            // Two chaos widths bound both the latest start and the longest flight.
            float front = (_RevealMaxDistance + width + 2.0 * chaos) * _RevealProgress;
            return smoothstep(0.0, 1.0, saturate((front - distanceFromOrigin - delay) / flightWidth));
        }

        float3 GetAnimatedPosition(VoxelAttributes input, float reveal, float3 sourceWS)
        {
            float random = input.voxelRandom.x;
            float chaosBlend = saturate(max(_RevealChaos, 0.0) / max(_TransitionWidth, 0.001));
            float3 fromOrigin = input.voxelCenter - TransformWorldToObject(sourceWS);
            float3 hashDirection = float3(
                Hash11(random + 1.0) * 2.0 - 1.0,
                Hash11(random + 2.0) * 2.0 - 1.0,
                Hash11(random + 3.0) * 2.0 - 1.0);
            float3 radialDirection = SafeNormalize(fromOrigin + hashDirection * 0.5);
            float3 scatterDirection = SafeNormalize(lerp(radialDirection, SafeNormalize(hashDirection), chaosBlend));
            float wave = sin(_Time.y * 7.0 + random * 18.0) * _WaveStrength * (1.0 - reveal);
            float cubeScale = lerp(reveal, pow(reveal, lerp(0.35, 0.7, random)), chaosBlend);
            float3 collapsedPosition = input.voxelCenter + (input.positionOS.xyz - input.voxelCenter) * cubeScale;
            float3 animatedPosition = collapsedPosition + scatterDirection * _ScatterDistance * (1.0 - reveal);
            float flightEnvelope = 4.0 * reveal * (1.0 - reveal);
            float3 flightOffset = sin(_Time.y * float3(3.1, 4.3, 3.7) + hashDirection * 12.0 + reveal * 6.2831853);
            animatedPosition += flightOffset * (_ScatterDistance * 0.4 * chaosBlend * flightEnvelope);
            animatedPosition.y += wave;
            return animatedPosition;
        }
        ENDHLSL

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half reveal : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                half3 vertexLighting : TEXCOORD4;
            };

            half3 EvaluateDirectLight(Light light, half3 normalWS)
            {
                half diffuse = saturate(dot(normalWS, light.direction));
                return light.color * (diffuse * light.distanceAttenuation * light.shadowAttenuation);
            }

            Varyings Vert(VoxelAttributes input)
            {
                Varyings output;
                float3 centerWS = TransformObjectToWorld(input.voxelCenter);
                float3 sourceWS = GetRevealSource(centerWS);
                float reveal = GetReveal(centerWS, sourceWS, input.voxelRandom.x);
                float3 animatedPosition = GetAnimatedPosition(input, reveal, sourceWS);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(animatedPosition);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.reveal = reveal;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    output.vertexLighting = VertexLighting(positionInputs.positionWS, output.normalWS);
                #else
                    output.vertexLighting = half3(0.0h, 0.0h, 0.0h);
                #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.reveal - 0.001);
                half3 normalWS = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half4 shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, shadowMask);
                half3 lighting = max(SampleSH(normalWS), 0.0h) + EvaluateDirectLight(mainLight, normalWS) + input.vertexLighting;

                #if defined(_ADDITIONAL_LIGHTS)
                    uint pixelLightCount = GetAdditionalLightsCount();

                    #if USE_CLUSTER_LIGHT_LOOP
                        [loop] for (uint lightIndex = 0u; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                            lighting += EvaluateDirectLight(light, normalWS);
                        }
                    #endif

                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                        lighting += EvaluateDirectLight(light, normalWS);
                    LIGHT_LOOP_END
                #endif

                half3 albedo = input.color.rgb * _ColorIntensity;
                half luminance = dot(albedo, half3(0.2126h, 0.7152h, 0.0722h));
                albedo = lerp(luminance.xxx, albedo, _Saturation);
                half3 color = albedo * lighting;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                half reveal : TEXCOORD0;
            };

            ShadowVaryings ShadowVert(VoxelAttributes input)
            {
                ShadowVaryings output;
                float3 centerWS = TransformObjectToWorld(input.voxelCenter);
                float3 sourceWS = GetRevealSource(centerWS);
                float reveal = GetReveal(centerWS, sourceWS, input.voxelRandom.x);
                float3 animatedPosition = GetAnimatedPosition(input, reveal, sourceWS);
                float3 positionWS = TransformObjectToWorld(animatedPosition);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                output.reveal = reveal;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_TARGET
            {
                clip(input.reveal - 0.001);
                return 0;
            }
            ENDHLSL
        }
    }
}
