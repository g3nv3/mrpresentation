Shader "Hidden/Project/KawaseBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Offset ("Offset", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Offset;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 offset = _MainTex_TexelSize.xy * _Offset;

                fixed4 color = tex2D(_MainTex, input.uv) * 0.16;
                color += tex2D(_MainTex, input.uv + float2(offset.x, 0.0)) * 0.12;
                color += tex2D(_MainTex, input.uv + float2(-offset.x, 0.0)) * 0.12;
                color += tex2D(_MainTex, input.uv + float2(0.0, offset.y)) * 0.12;
                color += tex2D(_MainTex, input.uv + float2(0.0, -offset.y)) * 0.12;
                color += tex2D(_MainTex, input.uv + float2(offset.x, offset.y)) * 0.09;
                color += tex2D(_MainTex, input.uv + float2(-offset.x, offset.y)) * 0.09;
                color += tex2D(_MainTex, input.uv + float2(offset.x, -offset.y)) * 0.09;
                color += tex2D(_MainTex, input.uv + float2(-offset.x, -offset.y)) * 0.09;

                return color;
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 offset = _MainTex_TexelSize.xy * 0.75;

                fixed4 color = tex2D(_MainTex, input.uv) * 0.25;
                color += tex2D(_MainTex, input.uv + float2(offset.x, 0.0)) * 0.125;
                color += tex2D(_MainTex, input.uv + float2(-offset.x, 0.0)) * 0.125;
                color += tex2D(_MainTex, input.uv + float2(0.0, offset.y)) * 0.125;
                color += tex2D(_MainTex, input.uv + float2(0.0, -offset.y)) * 0.125;
                color += tex2D(_MainTex, input.uv + float2(offset.x, offset.y)) * 0.0625;
                color += tex2D(_MainTex, input.uv + float2(-offset.x, offset.y)) * 0.0625;
                color += tex2D(_MainTex, input.uv + float2(offset.x, -offset.y)) * 0.0625;
                color += tex2D(_MainTex, input.uv + float2(-offset.x, -offset.y)) * 0.0625;

                return color;
            }
            ENDCG
        }
    }
}
