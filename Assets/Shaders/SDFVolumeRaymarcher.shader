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

            // --- 3D TEXTURE ATLAS CACHE SLOTS ---
            Texture3D<float> _SDFIndirectionGrid;
            Texture3D<float> _SDFBrickAtlas;
            SamplerState sampler_SDFBrickAtlas; // Linear filter sampler for smooth interpolations

            // --- GLOBAL COORDINATE BOX BOUNDS ---
            uniform float3 _VolumeBoundsMin;
            uniform float3 _VolumeBoundsMax;
            uniform float3 _IndirectionRes;
            uniform float3 _AtlasRes;

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);
                return output;
            }

            // High-speed O(1) Sparse Texture Lookup
            float GetSceneSDF(float3 p)
            {
                float3 volumeSizes = _VolumeBoundsMax - _VolumeBoundsMin;
                float cellWidth = volumeSizes.x / max(_IndirectionRes.x, 1.0);

                // Out of bounds container safety check
                if (any(p < _VolumeBoundsMin) || any(p > _VolumeBoundsMax))
                {
                    return cellWidth;
                }

                // 1. Calculate precise integer coords instead of relying on volatile frac()
                float3 volumeUV = (p - _VolumeBoundsMin) / volumeSizes;
                int3 indirectionCoords = floor(volumeUV * _IndirectionRes);
                indirectionCoords = clamp(indirectionCoords, int3(0, 0, 0), int3(_IndirectionRes) - 1);

                uint brickIndex = _SDFIndirectionGrid.Load(int4(indirectionCoords, 0));
                
                // If empty space, return a fixed fractional step to advance predictably to the next cell
                if (brickIndex == 0)
                {
                    return cellWidth / 2;
                }

                // Ensure resolution counts are explicit integers to prevent float-to-int truncation shifts
                uint3 atlasResPixels = (uint3)_AtlasRes;
                uint bricksPerAxisX = atlasResPixels.x / 8;
                uint bricksPerAxisY = atlasResPixels.y / 8; // Safely handle non-uniform atlases if applicable
                uint bricksPerSliceXY = bricksPerAxisX * bricksPerAxisY;

                // Explicit integer decoding for the allocation box origins
                uint3 brickOriginPixels = uint3(
                    (brickIndex % bricksPerAxisX) * 8,
                    ((brickIndex / bricksPerAxisX) % bricksPerAxisY) * 8,
                    (brickIndex / bricksPerSliceXY) * 8
                );

                // Derive the exact world-space minimum corner of this voxel cell
                float3 voxelMin = _VolumeBoundsMin + float3(indirectionCoords) * cellWidth;

                // Local UV matching
                float3 localVolumeUV = saturate((p - voxelMin) / cellWidth);
                float3 voxelOffset = localVolumeUV * 7.0; 

                // Combine everything cleanly using pure float parameters at the final step
                float3 atlasUVW = (float3(brickOriginPixels) + voxelOffset) / _AtlasRes;

                // Query the baked distance data
                float rawDistance = _SDFBrickAtlas.Load(int4(atlasUVW, 0)).r;
                return rawDistance; // Scale back to world-space units
            }

            float3 CalculateCSGNormal(float3 p)
            {
                const float k = 0.005; // Slightly larger epsilon for filtered voxel grids
                float2 e = float2(1.0, -1.0);

                float d1 = GetSceneSDF(p + e.xyy * k);
                float d2 = GetSceneSDF(p + e.yyx * k);
                float d3 = GetSceneSDF(p + e.yxy * k);
                float d4 = GetSceneSDF(p + e.xxx * k);

                return normalize(e.xyy * d1 + e.yyx * d2 + e.yxy * d3 + e.xxx * d4);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / input.screenPos.w;

                float rawDepth = SampleSceneDepth(uv);
                float sceneLinearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

                float3 rayOriginWS = _WorldSpaceCameraPos;
                float3 rayDirWS = normalize(input.positionWS - rayOriginWS);

                // --- 1. DEFINE CELLWIDTH & VOLUME SIZES ---
                float3 volumeSizes = _VolumeBoundsMax - _VolumeBoundsMin;
                float cellWidth = volumeSizes.x / max(_IndirectionRes.x, 1.0);

                // --- 2. CALCULATE CONTAINER TFAR & TNEAR VIA RAY-BOX INTERSECTION ---
                float3 invRayDir = 1.0 / (rayDirWS + 1e-6);
                float3 tMinBox = (_VolumeBoundsMin - rayOriginWS) * invRayDir;
                float3 tMaxBox = (_VolumeBoundsMax - rayOriginWS) * invRayDir;
                float3 t1 = min(tMinBox, tMaxBox);
                float3 t2 = max(tMinBox, tMaxBox);
                
                float tNear = max(max(t1.x, t1.y), t1.z);
                float tFar = min(min(t2.x, t2.y), t2.z);

                // If the ray completely misses the volume container, or exits behind the camera
                if (tFar < 0.0 || tNear > tFar)
                {
                    discard;
                }

                // Start marching exactly where the ray enters the volume container
                float t = max(0.0, tNear); 
                
                const int MAX_STEPS = 80; 
                const float SURFACE_THRESHOLD = 0.015;
                const float MAX_DISTANCE = 60.0;

                bool hitSurface = false;
                float3 hitPosWS = float3(0.0, 0.0, 0.0);

                // --- GLOW ACCUMULATION TRACKER ---
                float accumulatedGlow = 0.0;
                float glowSharpness = 0.5;

                [loop]
                for (int i = 0; i < MAX_STEPS; i++)
                {
                    float3 currentPosWS = rayOriginWS + rayDirWS * t;
                    
                    float distanceToScene = GetSceneSDF(currentPosWS);

                    // Accumulate preview glow safely inside the volume bounds
                    if (!any(currentPosWS < _VolumeBoundsMin) && !any(currentPosWS > _VolumeBoundsMax))
                    {
                        accumulatedGlow += exp(-max(distanceToScene, 0.0) * glowSharpness);
                    }

                    // Matches the stable hit condition of debugMode 6
                    if (distanceToScene <= SURFACE_THRESHOLD) 
                    {
                        hitSurface = true;
                        hitPosWS = currentPosWS;
                        break;
                    }

                    // NOW VALID: tFar is defined as the exit face of the entire volume box!
                    if (t > tFar || t > MAX_DISTANCE) break;
                    
                    // Step forward securely by a fraction of a cell or the SDF distance
                    t += max(distanceToScene, cellWidth / 16.0);
                }

                // --- COLOR RENDERING STAGE (Depth Map Mode) ---
                float3 finalColor = float3(0.0, 0.0, 0.0);
                if (hitSurface)
                {
                    float3 cameraForward = mul((float3x3)UNITY_MATRIX_I_V, float3(0.0, 0.0, -1.0));
                    float3 vectorToHit = hitPosWS - rayOriginWS;
                    float hitEyeDepth = dot(vectorToHit, cameraForward);

                    if (hitEyeDepth < sceneLinearDepth)
                    {
                        float depthVisualizer = saturate(1.0 - (hitEyeDepth / MAX_DISTANCE));
                        finalColor = float3(depthVisualizer, depthVisualizer, depthVisualizer);
                    }
                }
                else
                {
                    float softAura = saturate(accumulatedGlow * 0.02);
                    softAura = pow(softAura, 2.0);
                    finalColor = _NeonColor.rgb * softAura * _GlowIntensity * 0.4;
                }

                if (hitSurface == false && length(finalColor) < 0.001)
                {
                    discard;
                }

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}