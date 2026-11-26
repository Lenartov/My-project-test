Shader "RealtimeCardPrototype/ShockRadius"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-25" "RenderType" = "Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float distanceFromCenter = length(input.uv * 2.0 - 1.0);
                float fadeAtEdge = 1.0 - smoothstep(0.94, 1.0, distanceFromCenter);
                float fill = 0.062 * (1.0 - smoothstep(0.12, 0.90, distanceFromCenter));
                float rim = 0.095 * smoothstep(0.84, 0.94, distanceFromCenter);
                half3 tint = lerp(half3(0.17, 0.36, 0.90), half3(0.32, 0.64, 0.97), saturate(distanceFromCenter));
                return half4(tint, (fill + rim) * fadeAtEdge);
            }
            ENDHLSL
        }
    }
}
