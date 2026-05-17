Shader "Project/UI/FrostedGlassImage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BlurTex ("Blurred Camera Texture", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FrostedTint ("Frosted Tint", Color) = (0.95686275,0.95686275,0.9411765,0.28)
        _Opacity ("Opacity", Range(0, 1)) = 0.78
        _Saturation ("Saturation", Range(0, 2)) = 0.85
        _Brightness ("Brightness", Range(0, 2)) = 1.08
        _FlipY ("Flip Y", Float) = 0
        _UseReferenceScreenRect ("Use Reference Screen Rect", Float) = 0
        _ReferenceScreenRect ("Reference Screen Rect", Vector) = (0,0,1,1)
        _ReferenceUvRect ("Reference UV Rect", Vector) = (0,0,1,1)
        _UseWorldProjection ("Use World Projection", Float) = 0
        _PicoProjection ("Pico Projection", Vector) = (1,1,0.5,0.5)
        _PicoFrameSize ("Pico Frame Size", Vector) = (1,1,1,1)
        _ProjectionDepthSign ("Projection Depth Sign", Float) = -1
        _ProjectionUvTransform ("Projection UV Transform", Vector) = (1,1,0,0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "FrostedGlass"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 screenPosition : TEXCOORD2;
                float3 actualWorldPosition : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _BlurTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            fixed4 _FrostedTint;
            half _Opacity;
            half _Saturation;
            half _Brightness;
            half _FlipY;
            half _UseReferenceScreenRect;
            float4 _ReferenceScreenRect;
            float4 _ReferenceUvRect;
            half _UseWorldProjection;
            float4x4 _WorldToPicoCamera;
            float4 _PicoProjection;
            float4 _PicoFrameSize;
            half _ProjectionDepthSign;
            float4 _ProjectionUvTransform;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(output.worldPosition);
                output.screenPosition = ComputeScreenPos(output.vertex);
                output.actualWorldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.color = input.color * _Color;

                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 spriteSample = (tex2D(_MainTex, input.texcoord) + _TextureSampleAdd) * input.color;
                half alpha = spriteSample.a * _Opacity;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                float2 screenUv = input.screenPosition.xy / input.screenPosition.w;
                float2 cameraUv = lerp(
                    screenUv,
                    (screenUv - _ReferenceScreenRect.xy) / max(_ReferenceScreenRect.zw, float2(0.0001, 0.0001)),
                    saturate(_UseReferenceScreenRect));
                cameraUv = saturate(cameraUv);
                cameraUv.y = lerp(cameraUv.y, 1.0 - cameraUv.y, saturate(_FlipY));
                cameraUv = _ReferenceUvRect.xy + cameraUv * _ReferenceUvRect.zw;

                float3 cameraPoint = mul(_WorldToPicoCamera, float4(input.actualWorldPosition, 1.0)).xyz;
                float depth = cameraPoint.z * _ProjectionDepthSign;
                float validProjection = step(0.0001, depth);
                float safeDepth = max(depth, 0.0001);
                float2 projectedPixel = float2(
                    (_PicoProjection.x * cameraPoint.x / safeDepth) + _PicoProjection.z,
                    _PicoProjection.w - (_PicoProjection.y * cameraPoint.y / safeDepth));
                float2 projectedUv = float2(
                    projectedPixel.x * _PicoFrameSize.z,
                    1.0 - (projectedPixel.y * _PicoFrameSize.w));
                projectedUv.y = lerp(projectedUv.y, 1.0 - projectedUv.y, saturate(_FlipY));
                projectedUv = projectedUv * _ProjectionUvTransform.xy + _ProjectionUvTransform.zw;
                cameraUv = lerp(cameraUv, saturate(projectedUv), saturate(_UseWorldProjection) * validProjection);

                fixed3 blurred = tex2D(_BlurTex, cameraUv).rgb;
                half luminance = dot(blurred, half3(0.2126, 0.7152, 0.0722));
                blurred = lerp(luminance.xxx, blurred, _Saturation);
                blurred *= _Brightness;

                fixed3 tinted = lerp(blurred, _FrostedTint.rgb, _FrostedTint.a);
                tinted *= input.color.rgb;

                return fixed4(tinted, alpha);
            }
            ENDCG
        }
    }
}
