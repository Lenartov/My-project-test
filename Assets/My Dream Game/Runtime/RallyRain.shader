Shader "MyDreamGame/Rain"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float distanceToEye : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                output.uv = input.uv;
                output.distanceToEye = distance(_WorldSpaceCameraPos, positionWS);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half streak = saturate(1.0 - abs(input.uv.x * 2.0 - 1.0));
                half taper = saturate(1.0 - abs(input.uv.y * 2.0 - 1.0));
                half distanceFade = smoothstep(0.7, 2.2, input.distanceToEye) *
                    (1.0 - smoothstep(10.0, 24.0, input.distanceToEye));
                return half4(input.color.rgb, input.color.a * streak * taper * distanceFade);
            }
            ENDHLSL
        }
    }
}
