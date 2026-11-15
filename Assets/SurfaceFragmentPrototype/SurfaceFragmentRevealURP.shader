Shader "Surface Fragment Prototype/URP Reveal"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0
        _RevealProgress ("Reveal Progress", Range(0, 1)) = 1
        _ScatterDistance ("Scatter Distance", Float) = 2
        _ScatterRotation ("Scatter Rotation (Radians)", Float) = 2
        _WaveStrength ("Wave Strength", Float) = 0.2
        _TransitionWidth ("Transition Width", Float) = 0.4
        _RevealChaos ("Reveal Chaos", Float) = 0.8
        [HideInInspector] _MotionTime ("Motion Time", Float) = 0
        [HideInInspector] _RevealMaxDistance ("Reveal Max Distance", Float) = 1
        [HideInInspector] _BoundaryCount ("Boundary Count", Integer) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _Cutoff;
            float _RevealProgress;
            float _ScatterDistance;
            float _ScatterRotation;
            float _WaveStrength;
            float _TransitionWidth;
            float _RevealChaos;
            float _MotionTime;
            float _RevealMaxDistance;
            int _BoundaryCount;
            float4 _Boundaries[16];
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            float2 uv : TEXCOORD0;
            float3 fragmentCenter : TEXCOORD1;
            float2 metadata : TEXCOORD2;
        };

        half4 SurfaceColor(float2 uv, half4 vertexColor, float interior)
        {
            half4 exterior = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor * vertexColor;
            exterior.a = lerp(exterior.a, 1.0h, interior);
            return exterior;
        }

        float Hash(float seed)
        {
            return frac(sin(seed * 127.1) * 43758.5453);
        }

        float GetReveal(float3 centerOS, float random)
        {
            if (_RevealProgress <= 0.0) return 0.0;
            if (_RevealProgress >= 1.0) return 1.0;

            float2 centerXZ = TransformObjectToWorld(centerOS).xz;
            float minimumSqr = 3.402823466e+38;
            for (int index = 0; index < _BoundaryCount; index++)
            {
                float4 segment = _Boundaries[index];
                float2 direction = segment.zw - segment.xy;
                float projection = saturate(dot(centerXZ - segment.xy, direction) / dot(direction, direction));
                float2 offset = centerXZ - (segment.xy + projection * direction);
                minimumSqr = min(minimumSqr, dot(offset, offset));
            }

            float width = max(_TransitionWidth, 0.001);
            float chaos = max(_RevealChaos, 0.0);
            float delay = Hash(random + 5.0) * chaos;
            float flightWidth = width + lerp(0.5, 1.0, Hash(random + 6.0)) * chaos;
            float front = (_RevealMaxDistance + width + 2.0 * chaos) * _RevealProgress;
            return smoothstep(0.0, 1.0, saturate((front - sqrt(minimumSqr) - delay) / flightWidth));
        }

        float3 Rotate(float3 value, float3 axis, float angle)
        {
            float sine;
            float cosine;
            sincos(angle, sine, cosine);
            return value * cosine + cross(axis, value) * sine + axis * dot(axis, value) * (1.0 - cosine);
        }

        void Animate(Attributes input, out float3 positionOS, out float3 normalOS, out float reveal)
        {
            float random = input.metadata.x;
            reveal = GetReveal(input.fragmentCenter, random);
            float3 direction = SafeNormalize(float3(Hash(random + 1.0), Hash(random + 2.0), Hash(random + 3.0)) * 2.0 - 1.0);
            float3 axis = SafeNormalize(float3(Hash(random + 7.0), Hash(random + 8.0), Hash(random + 9.0)) * 2.0 - 1.0);
            float envelope = 4.0 * reveal * (1.0 - reveal);
            float chaosBlend = saturate(_RevealChaos / max(_TransitionWidth, 0.001));
            float angle = (1.0 - reveal) * _ScatterRotation * (random * 2.0 - 1.0);
            angle += sin(_MotionTime * (1.2 + random) + random * 23.0) * (_ScatterRotation * 0.15 * chaosBlend * envelope);
            float scale = pow(reveal, 0.55);
            float3 relativePosition = Rotate(input.positionOS.xyz - input.fragmentCenter, axis, angle) * scale;
            float3 wave = sin(float3(1.7, 2.3, 1.9) * _MotionTime + float3(3.1, 4.3, 3.7) * reveal + random * 19.0);
            positionOS = input.fragmentCenter + relativePosition + direction * _ScatterDistance * (1.0 - reveal);
            positionOS += wave * ((_WaveStrength + _ScatterDistance * 0.12 * chaosBlend) * envelope);
            normalOS = Rotate(input.normalOS, axis, angle);
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
                float2 uv : TEXCOORD2;
                float2 revealInterior : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                half3 vertexLighting : TEXCOORD5;
                half4 color : COLOR;
            };

            half3 DirectLight(Light light, half3 normalWS)
            {
                return light.color * (saturate(dot(normalWS, light.direction)) * light.distanceAttenuation * light.shadowAttenuation);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionOS;
                float3 normalOS;
                float reveal;
                Animate(input, positionOS, normalOS, reveal);
                VertexPositionInputs positions = GetVertexPositionInputs(positionOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.revealInterior = float2(reveal, input.metadata.y);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    output.vertexLighting = VertexLighting(output.positionWS, output.normalWS);
                #else
                    output.vertexLighting = half3(0.0h, 0.0h, 0.0h);
                #endif
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                clip(input.revealInterior.x - 0.001);
                half3 normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(frontFace, 1.0h, -1.0h);
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half4 shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #endif
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, shadowMask);
                half3 lighting = max(SampleSH(normalWS), 0.0h) + DirectLight(mainLight, normalWS) + input.vertexLighting;
                #if defined(_ADDITIONAL_LIGHTS)
                    uint pixelLightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                        [loop] for (uint lightIndex = 0u; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                            lighting += DirectLight(light, normalWS);
                        }
                    #endif
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                        lighting += DirectLight(light, normalWS);
                    LIGHT_LOOP_END
                #endif
                half4 surface = SurfaceColor(input.uv, input.color, input.revealInterior.y);
                clip(surface.a - _Cutoff);
                return half4(MixFog(surface.rgb * lighting, input.fogFactor), 1.0h);
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
                float reveal : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float interior : TEXCOORD2;
                half4 color : COLOR;
            };

            ShadowVaryings ShadowVert(Attributes input)
            {
                ShadowVaryings output;
                float3 positionOS;
                float3 normalOS;
                float reveal;
                Animate(input, positionOS, normalOS, reveal);
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                normalWS *= dot(normalWS, lightDirectionWS) < 0.0 ? -1.0 : 1.0;
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                output.reveal = reveal;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.interior = input.metadata.y;
                output.color = input.color;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                clip(input.reveal - 0.001);
                clip(SurfaceColor(input.uv, input.color, input.interior).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
