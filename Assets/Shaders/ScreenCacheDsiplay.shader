Shader "Custom/ScreenCacheDisplay"
{
    SubShader
    {
        // Changed tags to overlay layer configuration
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }

        Pass
        {
            Name "ForwardLit"
            
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0; // Track incoming canvas vertex data safely
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            Texture2D<float4> _ScreenCacheTex;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv; // Pass coordinates along cleanly
                return output;
            };

            float4 frag(Varyings input) : SV_Target
            {
                // Direct lookup: canvas texture mapping aligns seamlessly with our texture cache resolution
                int2 pixelCoords = int2(input.uv * _ScreenParams.xy);

                float4 raymarchedFrameColor = _ScreenCacheTex.Load(int3(pixelCoords, 0));

                if (raymarchedFrameColor.a < 0.001) discard;

                return raymarchedFrameColor;
            }
            ENDHLSL
        }
    }
}