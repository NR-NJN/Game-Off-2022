Shader "Custom/HeadlightBeam"
{
    Properties
    {
        _Color ("Color", Color) = (0.6, 0.8, 1, 1)
        _Intensity ("Intensity", Float) = 0.6
        _EdgeSoftness ("Edge Softness", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                half fade : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                output.fade = input.color.a;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half facing = abs(dot(normalize(input.normalWS), normalize(input.viewWS)));
                half strength = pow(facing, _EdgeSoftness) * input.fade * input.fade;
                return half4(_Color.rgb * _Intensity * strength, 1);
            }
            ENDHLSL
        }
    }
}
