Shader "Custom/PixelTextParticles"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _PixelSize ("Pixel Size", Float) = 0.05
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct ParticleGPU
            {
                float2 start;
                float2 target;
                float2 pos;
                float seed;
                float active;
                float mode;
                float scale;
            };

            StructuredBuffer<ParticleGPU> _Particles;

            float4 _Color;
            float _PixelSize;
            float4x4 _LocalToWorld;

            struct appdata
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
            };

            float2 GetQuadCorner(uint vertexID)
            {
                // two triangles:
                // 0,1,2 and 3,4,5
                if (vertexID == 0) return float2(-0.5, -0.5);
                if (vertexID == 1) return float2(-0.5,  0.5);
                if (vertexID == 2) return float2( 0.5,  0.5);

                if (vertexID == 3) return float2(-0.5, -0.5);
                if (vertexID == 4) return float2( 0.5,  0.5);
                return float2(0.5, -0.5);
            }

            v2f vert(appdata v)
            {
                v2f o;

                ParticleGPU p = _Particles[v.instanceID];

                float size = p.scale * _PixelSize;
                float2 corner = GetQuadCorner(v.vertexID) * size;

                float2 localXY = p.pos + corner;

                float4 localPos = float4(localXY.x, localXY.y, 0, 1);
                float4 worldPos = mul(_LocalToWorld, localPos);

                o.pos = mul(UNITY_MATRIX_VP, worldPos);

                float alpha = p.active * saturate(p.scale);
                o.color = float4(_Color.rgb, _Color.a * alpha);

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return i.color;
            }

            ENDHLSL
        }
    }
}