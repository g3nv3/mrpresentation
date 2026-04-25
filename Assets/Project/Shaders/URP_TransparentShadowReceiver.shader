Shader "Custom/URP/TransparentShadowReceiver"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color (RGBA)", Color) = (1,1,1,0.6)
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.25
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 1.0
        _ShadowAffectAlpha ("Shadow Affect Alpha", Range(0, 1)) = 0.6
        [Toggle] _ZWrite ("ZWrite", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ForwardTransparent"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                half _AmbientStrength;
                half _ShadowStrength;
                half _ShadowAffectAlpha;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half shadowAtten = saturate(mainLight.shadowAttenuation);
                half3 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 albedo = texColor * _BaseColor.rgb;

                // Make shadows visible on transparent surfaces: darken color and optionally raise alpha in shadowed regions.
                half shadowDark = (1.0h - shadowAtten) * _ShadowStrength;
                half3 litColor = albedo * (1.0h - shadowDark);
                half3 finalColor = lerp(albedo, litColor, 1.0h - _AmbientStrength);
                half finalAlpha = saturate(_BaseColor.a + shadowDark * _ShadowAffectAlpha * (1.0h - _BaseColor.a));

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}
