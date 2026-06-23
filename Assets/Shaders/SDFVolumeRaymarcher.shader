Shader "Custom/SDFVolumeRaymarcher"
{
    Properties
    {
        _NeonColor ("Neon Color", Color) = (1, 0.5, 0, 1) 
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 2.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off 
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0; 
            };

            float4 _NeonColor;
            float _GlowIntensity;

            // --- BOX DATA BUFFER ---
            uniform float4 _CubePositions[32]; // xyz = World Pos
            uniform float4 _CubeSizes[32];     // xyz = Half-extents
            uniform int _CubeCount;

            // --- SPHERE DATA BUFFER ---
            uniform float4 _SpherePositions[32]; // xyz = World Pos
            uniform float4 _SphereRadii[32];     // x = Radius
            uniform int _SphereCount;

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS; 
                return output;
            }

            float DistanceToBox(float3 p, float3 center, float3 halfExtents)
            {
                float3 localP = p - center;
                float3 d = abs(localP) - halfExtents;
                float exteriorDistance = length(max(d, 0.0));
                float interiorDistance = min(max(d.x, max(d.y, d.z)), 0.0);
                return exteriorDistance + interiorDistance;
            }

            float DistanceToSphere(float3 p, float3 center, float radius)
            {
                return distance(p, center) - radius;
            }

            // Linear, non-branching scene evaluation step
            float GetSceneSDF(float3 p)
            {
                float finalDist = 10000.0; // Empty world base state

                // Loop 1: Evaluate all boxes safely sequentially
                [loop]
                for (int i = 0; i < _CubeCount; i++)
                {
                    float dBox = DistanceToBox(p, _CubePositions[i].xyz, _CubeSizes[i].xyz);
                    finalDist = min(finalDist, dBox);
                }

                // Loop 2: Evaluate all spheres safely sequentially (No dynamic branching penalty)
                [loop]
                for (int j = 0; j < _SphereCount; j++)
                {
                    float dSphere = DistanceToSphere(p, _SpherePositions[j].xyz, _SphereRadii[j].x);
                    finalDist = min(finalDist, dSphere);
                }

                return finalDist;
            }

            float3 CalculateCSGNormal(float3 p)
            {
                const float eps = 0.001;
                float2 e = float2(eps, 0.0);
                return normalize(float3(
                    GetSceneSDF(p + e.xyy) - GetSceneSDF(p - e.xyy),
                    GetSceneSDF(p + e.yxy) - GetSceneSDF(p - e.yxy),
                    GetSceneSDF(p + e.yyx) - GetSceneSDF(p - e.yyx)
                ));
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 rayOriginWS = _WorldSpaceCameraPos;
                float3 rayDirWS = normalize(input.positionWS - rayOriginWS);

                float t = 0.0;
                const int MAX_STEPS = 100; 
                const float SURFACE_THRESHOLD = 0.001; 
                const float MAX_DISTANCE = 40.0;   

                [loop]
                for (int i = 0; i < MAX_STEPS; i++)
                {
                    float3 currentPosWS = rayOriginWS + rayDirWS * t;
                    float distanceToScene = GetSceneSDF(currentPosWS);

                    if (distanceToScene <= SURFACE_THRESHOLD)
                    {
                        float3 normal = CalculateCSGNormal(currentPosWS);
                        float3 viewDirWS = -rayDirWS;

                        float edgeGlow = 1.0 - saturate(dot(normal, viewDirWS));
                        edgeGlow = pow(edgeGlow, 4.0); 

                        float3 finalColor = float3(0.01, 0.01, 0.02) + (_NeonColor.rgb * edgeGlow * _GlowIntensity);
                        return float4(finalColor, 1.0);
                    }

                    if (t > MAX_DISTANCE) break;
                    t += distanceToScene;
                }

                return float4(0.0, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
    }
}