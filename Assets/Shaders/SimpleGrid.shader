Shader "Custom/SynthwaveGrid_AA"
{
    Properties
    {
        _GridSize ("Grid Size", Float) = 10.0
        _LineThickness ("Line Thickness (Pixels)", Range(0.5, 4.0)) = 1.5
        
        [HDR] _GridColor ("Neon Grid Color", Color) = (1, 0, 1, 1)
        _GlowIntensity ("Glow Intensity Boost", Float) = 2.0
        _BackgroundColor ("Background Color", Color) = (0.05, 0.0, 0.1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0; 
            };

            CBUFFER_START(UnityPerMaterial)
                float _GridSize;
                float _LineThickness;
                float _GlowIntensity;
                half4 _GridColor;
                half4 _BackgroundColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz); 
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 worldUV = IN.worldPos.xz / _GridSize; 
                worldUV.y -= _Time.y * 0.5;
                
                float2 dUV = fwidth(worldUV);
                float2 targetWidth = dUV * _LineThickness;
                
                float2 gridCoord = abs(frac(worldUV - 0.5) - 0.5);

                float2 gridLines = smoothstep(targetWidth, 0.0, gridCoord);
                float gridMask = max(gridLines.x, gridLines.y);

                half4 emissiveGrid = _GridColor * _GlowIntensity;
                half4 finalColor = lerp(_BackgroundColor, emissiveGrid, gridMask);

                return finalColor;
            }

            ENDHLSL
        }
    }
}
