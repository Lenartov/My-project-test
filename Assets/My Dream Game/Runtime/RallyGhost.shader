Shader "MyDreamGame/ReferenceCar"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.4, 0.7, 0.8, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 towardEye = normalize(_WorldSpaceCameraPos - input.positionWS);
                half rim = 1.0 - saturate(dot(normal, towardEye));
                half sky = saturate(normal.y * 0.5 + 0.5);
                half3 color = _Tint.rgb * (0.48 + 0.44 * sky + 0.20 * rim * rim);
                half fog = InitializeInputDataFog(float4(input.positionWS, 1.0), 0.0);
                return half4(MixFog(color, fog), _Opacity * (0.64 + 0.34 * rim));
            }
            ENDHLSL
        }
    }
}
