Shader "Custom/SdfVolumeRaymarcher"
{
    Properties
    {
        _NeonColor ("Neon Color", Color) = (1, 0.5, 0, 1) 
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 2.5
    }

    SubShader
    {
        // 1. Move queue into Transparent so empty areas pass through perfectly
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0; 
                float4 screenPos    : TEXCOORD1; 
            };

            float4 _NeonColor;
            float _GlowIntensity;

            // --- GLOBAL VECTOR BUFFERS ---
            uniform float4x4 _GlobalCubeMatrices[64];
            uniform float4 _GlobalCubeData[64]; // x = isSubtractive (0 or 1)
            uniform int _GlobalCubeCount;

            uniform float4 _GlobalSpherePositions[64];
            uniform float4 _GlobalSphereRadii[64];   // x = Radius, y = isSubtractive
            uniform int _GlobalSphereCount;

            uniform float4x4 _GlobalCapsuleMatrices[64];
            uniform float4 _GlobalCapsuleData[64]; // x = radius, y = height, z = direction, w = isSubtractive
            uniform int _GlobalCapsuleCount;

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS; 
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);
                return output;
            }

            float DistanceToBoxLocal(float3 localP, float3 halfExtents)
            {
                float3 d = abs(localP) - halfExtents;
                float exteriorDistance = length(max(d, 0.0));
                float interiorDistance = min(max(d.x, max(d.y, d.z)), 0.0);
                return exteriorDistance + interiorDistance;
            }

            float DistanceToSphere(float3 p, float3 center, float radius)
            {
                return distance(p, center) - radius;
            }

            float DistanceToCapsuleLocal(float3 localP, float radius, float height, int direction)
            {
                // Generate base axis vector depending on direction index (0=X, 1=Y, 2=Z)
                float3 dir = float3(0, 1, 0);
                if (direction == 0) dir = float3(1, 0, 0);
                if (direction == 2) dir = float3(0, 0, 1);

                float halfLineLen = (height * 0.5) - radius;
                halfLineLen = max(halfLineLen, 0.0);

                // Define standard segment bounds along that axis
                float3 a = dir * halfLineLen;
                float3 b = -dir * halfLineLen;

                float3 pa = localP - a;
                float3 ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h) - radius;
            }

            float smin(float a, float b, float k)
            {
                float h = max(k - abs(a - b), 0.0) / k;
                return min(a, b) - h * h * h * k * (1.0 / 6.0);
            }

            float GetSceneSdf(float3 p)
            {
                float finalDist = 10000.0; 

                // --- 1. BASE GEOMETRY PASS (Unions) ---
                for (int i = 0; i < _GlobalCubeCount; i++)
                {
                    if (_GlobalCubeData[i].x < 0.5) {
                        // Transform the evaluation ray straight into this specific cube's local space
                        float3 localP = mul(_GlobalCubeMatrices[i], float4(p, 1.0)).xyz;
                        // A default unit cube spans from -0.5 to 0.5 in local space coordinates
                        float dBox = DistanceToBoxLocal(localP, float3(0.5, 0.5, 0.5));
                        
                        // Scale correction factor to keep raymarching step steps uniform
                        float scaleCorrection = min(length(_GlobalCubeMatrices[i][0].xyz), min(length(_GlobalCubeMatrices[i][1].xyz), length(_GlobalCubeMatrices[i][2].xyz)));
                        finalDist = smin(finalDist, dBox / scaleCorrection, 1.0);
                    }
                }

                for (int j = 0; j < _GlobalSphereCount; j++)
                {
                    if (_GlobalSphereRadii[j].y < 0.5) {
                        float dSphere = DistanceToSphere(p, _GlobalSpherePositions[j].xyz, _GlobalSphereRadii[j].x);
                        finalDist = smin(finalDist, dSphere, 1.0);
                    }
                }

                for (int k = 0; k < _GlobalCapsuleCount; k++)
                {
                    if (_GlobalCapsuleData[k].w < 0.5) {
                        // localP is now in a clean, unwarped rotated space
                        float3 localP = mul(_GlobalCapsuleMatrices[k], float4(p, 1.0)).xyz;
                        
                        float dCapsule = DistanceToCapsuleLocal(
                            localP, 
                            _GlobalCapsuleData[k].x, // World Radius
                            _GlobalCapsuleData[k].y, // World Height
                            (int)_GlobalCapsuleData[k].z // Direction Axis
                        );
                        
                        // No scale correction needed anymore! Space is uniform.
                        finalDist = smin(finalDist, dCapsule, 1.0);
                    }
                }

                // --- 2. CARVING PASS (Subtractions) ---
                for (int i2 = 0; i2 < _GlobalCubeCount; i2++)
                {
                    if (_GlobalCubeData[i2].x > 0.5) {
                        float3 localP = mul(_GlobalCubeMatrices[i2], float4(p, 1.0)).xyz;
                        float dBox = DistanceToBoxLocal(localP, float3(0.5, 0.5, 0.5));
                        float scaleCorrection = min(length(_GlobalCubeMatrices[i2][0].xyz), min(length(_GlobalCubeMatrices[i2][1].xyz), length(_GlobalCubeMatrices[i2][2].xyz)));
                        finalDist = max(finalDist, -(dBox / scaleCorrection));
                    }
                }

                for (int j2 = 0; j2 < _GlobalSphereCount; j2++)
                {
                    if (_GlobalSphereRadii[j2].y > 0.5) {
                        float dSphere = DistanceToSphere(p, _GlobalSpherePositions[j2].xyz, _GlobalSphereRadii[j2].x);
                        finalDist = max(finalDist, -dSphere);
                    }
                }

                for (int k2 = 0; k2 < _GlobalCapsuleCount; k2++)
                {
                    if (_GlobalCapsuleData[k2].w > 0.5) {
                        float3 localP = mul(_GlobalCapsuleMatrices[k2], float4(p, 1.0)).xyz;
                        
                        float dCapsule = DistanceToCapsuleLocal(
                            localP, 
                            _GlobalCapsuleData[k2].x, 
                            _GlobalCapsuleData[k2].y, 
                            (int)_GlobalCapsuleData[k2].z
                        );
                        
                        finalDist = max(finalDist, -dCapsule);
                    }
                }

                return finalDist;
            }

            float3 CalculateCSGNormal(float3 p)
            {
                const float eps = 0.001;
                float2 e = float2(eps, 0.0);
                return normalize(float3(
                    GetSceneSdf(p + e.xyy) - GetSceneSdf(p - e.xyy),
                    GetSceneSdf(p + e.yxy) - GetSceneSdf(p - e.yxy),
                    GetSceneSdf(p + e.yyx) - GetSceneSdf(p - e.yyx)
                ));
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / input.screenPos.w;

                float rawDepth = SampleSceneDepth(uv);
                float sceneLinearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

                float3 rayOriginWS = _WorldSpaceCameraPos;
                float3 rayDirWS = normalize(input.positionWS - rayOriginWS);

                float t = 0.0;
                const int MAX_STEPS = 100; 
                const float SURFACE_THRESHOLD = 0.001; 
                const float MAX_DISTANCE = 60.0;   

                bool hitSurface = false;
                float3 hitPosWS = float3(0.0, 0.0, 0.0);

                [loop]
                for (int i = 0; i < MAX_STEPS; i++)
                {
                    float3 currentPosWS = rayOriginWS + rayDirWS * t;
                    float distanceToScene = GetSceneSdf(currentPosWS);

                    if (distanceToScene <= SURFACE_THRESHOLD)
                    {
                        hitSurface = true;
                        hitPosWS = currentPosWS;
                        break;
                    }

                    if (t > MAX_DISTANCE) break;
                    t += distanceToScene;
                }

                if (hitSurface)
                {
                    float3 cameraForward = mul((float3x3)UNITY_MATRIX_I_V, float3(0.0, 0.0, -1.0));
                    float3 vectorToHit = hitPosWS - rayOriginWS;
                    float hitEyeDepth = dot(vectorToHit, cameraForward);

                    // Occlude cleanly against standard opaque scene geometry like floors
                    if (hitEyeDepth < sceneLinearDepth)
                    {
                        float3 normal = CalculateCSGNormal(hitPosWS);
                        float3 viewDirWS = -rayDirWS;

                        float edgeGlow = 1.0 - saturate(dot(normal, viewDirWS));
                        edgeGlow = pow(edgeGlow, 4.0); 

                        float3 finalColor = float3(0.02, 0.02, 0.04) + (_NeonColor.rgb * edgeGlow * _GlowIntensity);
                        return float4(finalColor, 1.0);
                    }
                }

                discard; // 🌟 Completely drops execution on empty spaces to keep background pristine
                return float4(0.0, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
    }
}