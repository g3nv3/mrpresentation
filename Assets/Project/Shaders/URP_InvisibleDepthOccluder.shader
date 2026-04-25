Shader "Custom/URP/InvisibleDepthOccluder"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _DepthOffsetFactor ("Depth Offset Factor", Float) = 0
        _DepthOffsetUnits ("Depth Offset Units", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry-1"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "DepthOnlyOccluder"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ZWrite On
            ZTest [_ZTest]
            Cull [_Cull]
            ColorMask 0
            Offset [_DepthOffsetFactor], [_DepthOffsetUnits]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
