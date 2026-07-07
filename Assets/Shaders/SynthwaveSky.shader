Shader "Custom/SynthwaveSkybox"
{
    Properties
    {
        [Header(Background)]
        _TopColor ("Top Space Color", Color) = (0.02, 0.0, 0.05, 1)
        _BottomColor ("Horizon Color", Color) = (0.3, 0.0, 0.2, 1)
        
        [Header(Stars)]
        _StarDensity ("Star Density", Range(100, 500)) = 250.0
        _StarThreshold ("Star Threshold", Range(0.9, 1.0)) = 0.98
        
        [Header(Retro Sun)]
        [HDR] _SunColorTop ("Sun Color Top", Color) = (1, 0.8, 0, 1)
        [HDR] _SunColorBottom ("Sun Color Bottom", Color) = (1, 0, 0.5, 1)
        _SunSize ("Sun Size", Range(0.8, 1.0)) = 0.95
        _SunHeight ("Sun Height (Y Offset)", Range(-0.5, 0.5)) = 0.0
        
        [Header(Sun Stripes)]
        _StripeCount ("Stripe Count", Float) = 30.0
        _StripeSpeed ("Stripe Move Speed", Float) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

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
                float3 viewDir : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _BottomColor;
                float _StarDensity;
                float _StarThreshold;
                half4 _SunColorTop;
                half4 _SunColorBottom;
                float _SunSize;
                float _SunHeight;
                float _StripeCount;
                float _StripeSpeed;
            CBUFFER_END

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.viewDir = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.viewDir);

                float skyBlend = smoothstep(-0.2, 0.6, dir.y);
                half3 finalColor = lerp(_BottomColor.rgb, _TopColor.rgb, skyBlend);

                float starNoise = hash(dir * _StarDensity);
                float stars = step(_StarThreshold, starNoise) * smoothstep(0.1, 0.5, dir.y);
                finalColor += stars.xxx;

                float sunDist = dot(dir, normalize(float3(0, _SunHeight, 1)));
                
                if (sunDist > _SunSize)
                {
                    float sunGradient = smoothstep(_SunSize, 1.0, sunDist);
                    half3 sunColor = lerp(_SunColorBottom.rgb, _SunColorTop.rgb, dir.y * 2.0 + 0.5);

                    float stripe = sin((dir.y * _StripeCount) - (_Time.y * _StripeSpeed));
                    
                    float stripeThickness = smoothstep(-0.5, 0.5, dir.y); 
                    if (stripe > -stripeThickness)
                    {
                        finalColor = sunColor;
                    }
                }

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}