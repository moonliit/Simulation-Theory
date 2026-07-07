Shader "Custom/SdfVolumeRaymarcher"
{
    Properties
    {
        _NeonColor ("Neon Color", Color) = (1, 0.5, 0, 1) 
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 2.5

        [Header(SDF Aesthetics)]
        _SDFBaseColor ("Dark Body Color", Color) = (0.05, 0.02, 0.05, 1)
        [HDR] _NeonOutlineColor ("Neon Grid/Edge Color", Color) = (1, 0.1, 0.2, 1)
        
        [Header(Ophanim Eye)]
        _EyeColor ("Eye Sclera Color", Color) = (0.9, 0.9, 0.9, 1)
        [HDR] _PupilColor ("Glowing Pupil Color", Color) = (0, 1, 1, 1)
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

            float4 _BossCorePos;
            float4 _BossCoreForward;
            
            half4 _SDFBaseColor;
            half4 _NeonOutlineColor;
            half4 _EyeColor;
            half4 _PupilColor;

            #define MAX_STANDALONE_PRIMITIVES 64
            #define MAX_CSG_INSTANCES 32
            #define MAX_TOTAL_CSG_NODES 128

            // --- GLOBAL VECTOR BUFFERS ---
            uniform float4x4 _GlobalCubeMatrices[MAX_STANDALONE_PRIMITIVES];
            uniform float4 _GlobalCubeData[MAX_STANDALONE_PRIMITIVES]; // x = isSubtractive (0 or 1)
            uniform int _GlobalCubeCount;

            uniform float4 _GlobalSpherePositions[MAX_STANDALONE_PRIMITIVES];
            uniform float4 _GlobalSphereRadii[MAX_STANDALONE_PRIMITIVES];   // x = Radius, y = isSubtractive
            uniform int _GlobalSphereCount;

            uniform float4x4 _GlobalCapsuleMatrices[MAX_STANDALONE_PRIMITIVES];
            uniform float4 _GlobalCapsuleData[MAX_STANDALONE_PRIMITIVES]; // x = radius, y = height, z = direction, w = isSubtractive
            uniform int _GlobalCapsuleCount;

            uniform float4x4 _CharMatrices[MAX_TOTAL_CSG_NODES];
            uniform float4 _CharData[MAX_TOTAL_CSG_NODES];
            uniform int _ActiveCsgInstanceCount;
            uniform float _CsgInstanceOffsets[MAX_CSG_INSTANCES];
            uniform float _CsgAssetTypes[MAX_CSG_INSTANCES];

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
                float3 dir = float3(0, 1, 0);
                if (direction == 0) dir = float3(1, 0, 0);
                if (direction == 2) dir = float3(0, 0, 1);

                float halfLineLen = (height * 0.5) - radius;
                halfLineLen = max(halfLineLen, 0.0);

                float3 a = dir * halfLineLen;
                float3 b = -dir * halfLineLen;

                float3 pa = localP - a;
                float3 ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h) - radius;
            }

            float DistanceToTorusLocal(float3 p, float tubeThickness, float ringRadius)
            {
                float2 q = float2(length(p.xz) - ringRadius, p.y);
                return length(q) - tubeThickness;
            }

            float smin(float a, float b, float k)
            {
                float h = max(k - abs(a - b), 0.0) / k;
                return min(a, b) - h * h * h * k * (1.0 / 6.0);
            }

            float smax(float a, float b, float k)
            {
                return -smin(-a, -b, k);
            }

            // Pull in the static topology structure generated by your baker script
            #include "Sword.hlsl"
            #include "Character.hlsl"
            #include "SwordCharacter.hlsl"
            #include "BossOphanim.hlsl"

            float GetSceneSdf(float3 p)
            {
                float finalDist = 10000.0;

                for (int instanceIdx = 0; instanceIdx < _ActiveCsgInstanceCount; instanceIdx++)
                {
                    int baseBufferIndex = (int)_CsgInstanceOffsets[instanceIdx];
                    int assetType = (int)_CsgAssetTypes[instanceIdx];

                    float dInstance = 10000.0;
                    float smoothness = 0.25;

                    if (assetType == 0)
                    {
                        dInstance = GetSwordDist(p, baseBufferIndex, smoothness);
                    }
                    else if (assetType == 1)
                    {
                        dInstance = GetCharacterDist(p, baseBufferIndex, smoothness);
                    }
                    else if (assetType == 2)
                    {
                        dInstance = GetSwordCharacterDist(p, baseBufferIndex, smoothness);
                    }

                    finalDist = smin(finalDist, dInstance, 1.0);
                }

                // --- 1. BASE GEOMETRY PASS (Unions) ---
                for (int i = 0; i < _GlobalCubeCount; i++)
                {
                    if (_GlobalCubeData[i].w < 0.5) {
                        float3 localP = mul(_GlobalCubeMatrices[i], float4(p, 1.0)).xyz;
                        float dBox = DistanceToBoxLocal(localP, _GlobalCubeData[i].xyz);
                        finalDist = smin(finalDist, dBox, 1.0);
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
                        float3 localP = mul(_GlobalCapsuleMatrices[k], float4(p, 1.0)).xyz;
                        float dShape = 10000.0;

                        if ((int)_GlobalCapsuleData[k].z == 3) {
                            dShape = DistanceToTorusLocal(localP, _GlobalCapsuleData[k].x, _GlobalCapsuleData[k].y);
                        } else {
                            dShape = DistanceToCapsuleLocal(localP, _GlobalCapsuleData[k].x, _GlobalCapsuleData[k].y, (int)_GlobalCapsuleData[k].z);
                        }

                        finalDist = smin(finalDist, dShape, 1.0);
                    }
                }

                // --- 2. CARVING PASS (Subtractions) ---
                for (int i2 = 0; i2 < _GlobalCubeCount; i2++)
                {
                    if (_GlobalCubeData[i2].w > 0.5) {
                        float3 localP = mul(_GlobalCubeMatrices[i2], float4(p, 1.0)).xyz;
                        float dBox = DistanceToBoxLocal(localP, _GlobalCubeData[i2].xyz);
                        finalDist = max(finalDist, -dBox);
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
                        float dShape = 10000.0;

                        if ((int)_GlobalCapsuleData[k2].z == 3) {
                            dShape = DistanceToTorusLocal(localP, _GlobalCapsuleData[k2].x, _GlobalCapsuleData[k2].y);
                        } else {
                            dShape = DistanceToCapsuleLocal(localP, _GlobalCapsuleData[k2].x, _GlobalCapsuleData[k2].y, (int)_GlobalCapsuleData[k2].z);
                        }

                        finalDist = max(finalDist, -dShape);
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

            float DistanceToSubtractions(float3 p)
            {
                float minDist = 10000.0;

                for (int i2 = 0; i2 < _GlobalCubeCount; i2++) {
                    if (_GlobalCubeData[i2].w > 0.5) {
                        float3 localP = mul(_GlobalCubeMatrices[i2], float4(p, 1.0)).xyz;
                        float dBox = DistanceToBoxLocal(localP, _GlobalCubeData[i2].xyz);
                        minDist = min(minDist, dBox);
                    }
                }

                for (int j2 = 0; j2 < _GlobalSphereCount; j2++) {
                    if (_GlobalSphereRadii[j2].y > 0.5) {
                        float dSphere = DistanceToSphere(p, _GlobalSpherePositions[j2].xyz, _GlobalSphereRadii[j2].x);
                        minDist = min(minDist, dSphere);
                    }
                }

                for (int k2 = 0; k2 < _GlobalCapsuleCount; k2++) {
                    if (_GlobalCapsuleData[k2].w > 0.5) {
                        float3 localP = mul(_GlobalCapsuleMatrices[k2], float4(p, 1.0)).xyz;
                        float dShape = 10000.0;
                        if ((int)_GlobalCapsuleData[k2].z == 3) {
                            dShape = DistanceToTorusLocal(localP, _GlobalCapsuleData[k2].x, _GlobalCapsuleData[k2].y);
                        } else {
                            dShape = DistanceToCapsuleLocal(localP, _GlobalCapsuleData[k2].x, _GlobalCapsuleData[k2].y, (int)_GlobalCapsuleData[k2].z);
                        }
                        minDist = min(minDist, dShape);
                    }
                }
                return minDist;
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
                        
                        float cutDist = abs(DistanceToSubtractions(hitPosWS));

                        float3 finalRGB = GetOphanimColor(
                            hitPosWS, normal, viewDirWS, cutDist,
                            _SDFBaseColor.rgb, _NeonOutlineColor.rgb, _NeonColor.rgb,
                            _EyeColor.rgb, _PupilColor.rgb,
                            _BossCorePos.xyz, _BossCoreForward.xyz
                        );
                        
                        return float4(finalRGB, 1.0);
                    }
                }

                discard; // Completely drops execution on empty spaces to keep background pristine
                return float4(0.0, 0.0, 0.0, 0.0);
            }

            ENDHLSL
        }
    }
}