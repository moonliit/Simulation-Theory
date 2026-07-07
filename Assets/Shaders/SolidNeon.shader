Shader "Custom/NeonLaser"
{
    Properties
    {
        [HDR] _NeonColor ("Neon Color", Color) = (0, 1, 1, 1)
        _Intensity ("Glow Intensity", Range(1, 10)) = 2.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "Unlit"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 _NeonColor;
            float _Intensity;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 glowColor = _NeonColor.rgb * _Intensity;
                return float4(glowColor, _NeonColor.a);
            }
            ENDHLSL
        }
    }
}