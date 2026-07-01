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
                // 1. Compute global properties for safe voxel metric stepping
                float3 volumeSizes = _VolumeBoundsMax - _VolumeBoundsMin;
                float cellWidth = volumeSizes.x / max(_IndirectionRes.x, 1.0);

                // If out of bounds, return a safe conservative step size (1 cell width) to let the ray reach the volume safely
                if (any(p < _VolumeBoundsMin) || any(p > _VolumeBoundsMax))
                {
                    return cellWidth;
                }

                float3 volumeUV = (p - _VolumeBoundsMin) / (_VolumeBoundsMax - _VolumeBoundsMin);
                uint3 indirectionCoords = uint3(volumeUV * _IndirectionRes);
                indirectionCoords = clamp(indirectionCoords, uint3(0, 0, 0), uint3(_IndirectionRes) - 1);

                uint brickIndex = _SDFIndirectionGrid.Load(int4(indirectionCoords, 0));
                
                // 2. CRITICAL FIX: If it's empty space, return the cell width so the ray march loop 
                // cleanly advances to the next voxel cell instead of exploding to 10000.0!
                if (brickIndex == 0)
                {
                    return cellWidth;
                }

                uint bricksPerAxis = (uint)(_AtlasRes.x / 8.0);
                uint bricksPerSlice = bricksPerAxis * bricksPerAxis;

                float3 brickOriginInAtlas = float3(
                    (brickIndex % bricksPerAxis) * 8,
                    ((brickIndex / bricksPerAxis) % bricksPerAxis) * 8,
                    (brickIndex / bricksPerSlice) * 8
                );

                float3 cellLocalFrac = frac(volumeUV * _IndirectionRes);
                float3 voxelOffset = cellLocalFrac * 7.0; 
                float3 atlasUVW = (brickOriginInAtlas + voxelOffset) / _AtlasRes;

                float rawDistance = _SDFBrickAtlas.SampleLevel(sampler_SDFBrickAtlas, atlasUVW, 0).r;

                // Rescale the raw atlas texture distance metric back into true world space units
                return rawDistance * cellWidth;
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

            // Declare an integer toggle variable to step through compilation points:
// 1 = Volume UVs, 2 = Indirection Coordinates, 3 = Raw Brick Index, 4 = Atlas UVWs, 5 = Raw Distance
int _DebugSDFStep; 

float4 frag_debug(Varyings i) : SV_Target
{
    // 🛠️ CHOOSE YOUR DEBUG MODE HERE:
    // 1 = Stable Global Volume UVs mapped onto 3D voxel faces
    // 2 = Stable Discrete Indirection Cell Coordinates 
    // 3 = Crisp, rock-solid 3D cubes with lighting and dark borders
    // 4 = Locked, stable internal texture atlas mapping visualization
    // 5 = Volumetric distance field evaluation
    int debugMode = 6;

    // 1. Ray Initialization
    float3 rayOrigin = _WorldSpaceCameraPos;
    float3 rayDir = normalize(i.positionWS - _WorldSpaceCameraPos);
    
    bool cameraIsInside = all(_WorldSpaceCameraPos >= _VolumeBoundsMin) && all(_WorldSpaceCameraPos <= _VolumeBoundsMax);
    float3 currentPos = cameraIsInside ? rayOrigin : i.positionWS;

    float3 volumeSizes = _VolumeBoundsMax - _VolumeBoundsMin;
    float cellWidth = volumeSizes.x / max(_IndirectionRes.x, 1.0);

    // Uniform/Atlas Resolution Configs
    float3 safeAtlasRes = _AtlasRes.xyz;
    if (any(safeAtlasRes <= 0.0)) safeAtlasRes = float3(64.0, 64.0, 64.0);
    uint bricksPerAxis = (uint)(safeAtlasRes.x / 8.0);
    if (bricksPerAxis == 0) bricksPerAxis = 8;
    uint bricksPerSlice = bricksPerAxis * bricksPerAxis;

    // 2. Voxel Traversal March Loop
    [loop]
    for (int stepIndex = 0; stepIndex < 128; stepIndex++)
    {
        if (any(currentPos < _VolumeBoundsMin) || any(currentPos > _VolumeBoundsMax))
        {
            break;
        }

        float3 volumeUV = (currentPos - _VolumeBoundsMin) / volumeSizes;
        uint3 indirectionCoords = uint3(volumeUV * _IndirectionRes.xyz);
        indirectionCoords = clamp(indirectionCoords, uint3(0, 0, 0), uint3(_IndirectionRes.xyz) - 1);

        uint brickIndex = _SDFIndirectionGrid.Load(int4(indirectionCoords, 0));

        // Compute exact voxel boundaries for the current cell
        float3 voxelMin = _VolumeBoundsMin + (float3)indirectionCoords * cellWidth;
        float3 voxelMax = voxelMin + cellWidth;

        float3 invRayDir = 1.0 / (rayDir + 1e-6); 
        float3 tMin = (voxelMin - rayOrigin) * invRayDir;
        float3 tMax = (voxelMax - rayOrigin) * invRayDir;
        
        float3 t1 = min(tMin, tMax);
        float3 t2 = max(tMin, tMax);
        
        float tNear = max(max(t1.x, t1.y), t1.z);
        float tFar = min(min(t2.x, t2.y), t2.z);

        tNear = max(0.0, tNear);

        // IF EMPTY VOID CELL: Leap to the exit face precisely
        if (brickIndex == 0)
        {
            currentPos = rayOrigin + rayDir * (tFar + 0.0005);
            continue; 
        }

        // =================================================================
        // WE HIT AN ALLOCATED BRICK! 
        // =================================================================
        // Calculate the rock-solid intersection point on the face of the cube
        float3 stableHitPos = rayOrigin + rayDir * tNear;
        float3 stableVolumeUV = (stableHitPos - _VolumeBoundsMin) / volumeSizes;
        float3 stableLocalUVW = clamp(frac(stableVolumeUV * _IndirectionRes.xyz), 0.0, 1.0);

        // Calculate face normals for crisp 3D depth lighting/shading
        float3 normal = float3(0.0, 0.0, 0.0);
        if (tNear == t1.x) normal = float3(-sign(rayDir.x), 0.0, 0.0);
        else if (tNear == t1.y) normal = float3(0.0, -sign(rayDir.y), 0.0);
        else normal = float3(0.0, 0.0, -sign(rayDir.z));

        float3 lightDir = normalize(float3(0.4, 0.8, -0.4));
        float lightIntensity = max(dot(normal, lightDir), 0.0) * 0.5 + 0.5;

        // --- RESTORED DEBUG MODES ---

        if (debugMode == 1)
        {
            // Restored: Shows a rock-solid global 3D UV color gradient (RGB) mapped onto the cubes
            return float4(stableVolumeUV * lightIntensity, 1.0);
        }

        if (debugMode == 2)
        {
            // Restored: Visualizes discrete, flat-shaded indirection coordinate blocks 
            return float4(((float3)indirectionCoords / _IndirectionRes.xyz) * lightIntensity, 1.0);
        }

        if (debugMode == 3)
        {
            // Project matching 2D coordinate spaces based on hit face planes to generate margins
            float2 faceUV = float2(0.0, 0.0);
            if (tNear == t1.x) faceUV = stableLocalUVW.yz;
            else if (tNear == t1.y) faceUV = stableLocalUVW.xz;
            else faceUV = stableLocalUVW.xy;

            float minEdgeDist = min(min(faceUV.x, 1.0 - faceUV.x), min(faceUV.y, 1.0 - faceUV.y));

            float intensity = (float)brickIndex / 16.0;
            float3 baseBrickColor = float3(intensity, 1.0 - intensity, 0.0) * lightIntensity;

            if (minEdgeDist < 0.04)
            {
                return float4(baseBrickColor * 0.15, 1.0); // Clean border outlines
            }
            return float4(baseBrickColor, 1.0);
        }

        // --- PHYSICAL ATLAS COORDINATES MATH CHECK ---
        float3 brickOriginInAtlas = float3(
            (brickIndex % bricksPerAxis) * 8.0,
            ((brickIndex / bricksPerAxis) % bricksPerAxis) * 8.0,
            (brickIndex / bricksPerSlice) * 8.0
        );

        // Map smoothly from 0.0-1.0 onto the safe half-texel centers (0.5 to 7.5)
        float3 voxelOffset = 0.5 + (stableLocalUVW * 7.0);
        float3 atlasUVW = (brickOriginInAtlas + voxelOffset) / safeAtlasRes;

        if (debugMode == 4)
        {
            return float4(atlasUVW, 1.0);
        }

        if (debugMode == 5)
        {
            // 1. Calculate current local position relative to this voxel [0.0 to 1.0]
            float3 localUVW = clamp((currentPos - voxelMin) / cellWidth, 0.0, 1.0);
            float3 entireAtlasUVW = localUVW;

            // 2. Sample the raw data at the current position inside the atlas volume
            float rawDistance = _SDFBrickAtlas.SampleLevel(sampler_SDFBrickAtlas, entireAtlasUVW, 0).r;

            // 3. Isosurface check: Did we hit the 3D sphere surface?
            if (rawDistance < 0.1) 
            {
                // 4. On-the-fly Central Differences to calculate a real 3D Volume Normal
                // This stops the sphere from looking like flat 2D projected circles!
                float delta = 0.01;
                float3 n = float3(
                    _SDFBrickAtlas.SampleLevel(sampler_SDFBrickAtlas, entireAtlasUVW + float3(delta, 0, 0), 0).r -
                    _SDFBrickAtlas.SampleLevel(sampler_SDFBrickAtlas, entireAtlasUVW - float3(delta, 0, 0), 0).r,
                    _SDFBrickAtlas.SampleLevel(sampler_SDFBrickAtlas, entireAtlasUVW + float3(0, delta, 0), 0).r -
                    _SDFBrickAtlas.SampleLevel(sampler_SDFBrickAtlas, entireAtlasUVW - float3(0, delta, 0), 0).r,
                    _SDFBrickAtlas.SampleLevel(sampler_SDFBrickAtlas, entireAtlasUVW + float3(0, 0, delta), 0).r -
                    _SDFBrickAtlas.SampleLevel(sampler_SDFBrickAtlas, entireAtlasUVW - float3(0, 0, delta), 0).r
                );
                float3 volumeNormal = normalize(n + 1e-5);

                // 5. Calculate clean 3D directional diffuse lighting on the sphere's actual curves
                float3 volumeLightDir = normalize(float3(0.4, 0.8, -0.4));
                float volumeLighting = max(dot(volumeNormal, volumeLightDir), 0.0) * 0.6 + 0.4;

                // 6. Generate the smooth, unique sub-brick color gradient tint
                uint3 atlasSubBrickCoords = uint3(entireAtlasUVW * bricksPerAxis);
                atlasSubBrickCoords = clamp(atlasSubBrickCoords, uint3(0,0,0), uint3(bricksPerAxis, bricksPerAxis, bricksPerAxis) - 1);
                
                float3 subBrickColor = float3(
                    (float)atlasSubBrickCoords.x / max((float)bricksPerAxis - 1.0, 1.0),
                    (float)atlasSubBrickCoords.y / max((float)bricksPerAxis - 1.0, 1.0),
                    (float)atlasSubBrickCoords.z / max((float)bricksPerAxis - 1.0, 1.0)
                );

                // Return shaded 3D geometry!
                return float4(subBrickColor * volumeLighting, 1.0);
            }

            // 7. Step the ray forward inside the volume safely
            currentPos += rayDir * max(rawDistance * cellWidth, 0.005);
            continue;
        }

if (debugMode == 6)
        {
            float currentT = tNear;
            bool hitSurface = false;

            // Use a stable step size relative to this specific cell's width
            float fixedStepSize = cellWidth / 16.0; 

            [unroll(16)]
            for (int subStep = 0; subStep < 16; subStep++)
            {
                // Constrain the march strictly within the bounds of this cell
                if (currentT >= tFar) break;

                float3 currentPosWS = rayOrigin + rayDir * currentT;
                
                // Track local progress strictly mapped within THIS voxel's known physical boundaries [0.0 to 1.0]
                // This replaces the volatile global frac() calculation!
                float3 localVolumeUV = saturate((currentPosWS - voxelMin) / cellWidth);
                
                // Map cleanly onto the internal region layout matching your Fix 1 bake target
                float3 voxelOffset = localVolumeUV * 7.0; 
                float3 sampleAtlasUVW = (brickOriginInAtlas + voxelOffset) / safeAtlasRes;

                // Query raw distance values directly from the active atlas texture memory
                float rawDistance = _SDFBrickAtlas.SampleLevel(sampler_SDFBrickAtlas, sampleAtlasUVW, 0).r;

                if (rawDistance <= 0.015) // Matches your original SURFACE_THRESHOLD
                {
                    hitSurface = true;
                    break;
                }

                currentT += fixedStepSize;
            }

            if (hitSurface)
            {
                return float4(1.0, 1.0, 1.0, 1.0); // Pure flat white silhouette
            }
            
            // Advance past the cell boundary safely if no surface was crossed
            currentPos = rayOrigin + rayDir * (tFar + 0.0005);
            continue;
        }
    }

    return float4(0.0, 0.0, 0.0, 0.0); 
}

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / input.screenPos.w;

                float rawDepth = SampleSceneDepth(uv);
                float sceneLinearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

                float3 rayOriginWS = _WorldSpaceCameraPos;
                float3 rayDirWS = normalize(input.positionWS - rayOriginWS);

                float t = 0.0;
                const int MAX_STEPS = 80; // Slightly increased steps to handle fine voxel transitions cleanly
                const float SURFACE_THRESHOLD = 0.01;
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
                    
                    // 1. Fetch from your optimized Sparse Atlas Lookup
                    float distanceToScene = GetSceneSDF(currentPosWS);

                    // 2. Accumulate background glow tracking as before
                    accumulatedGlow += exp(-distanceToScene * glowSharpness);

                    if (distanceToScene <= SURFACE_THRESHOLD)
                    {
                        hitSurface = true;
                        hitPosWS = currentPosWS;
                        break;
                    }

                    if (t > MAX_DISTANCE) break;

                    // 3. Step clamping guard: prevent texture quantization from creating infinite tiny loops
                    t += max(distanceToScene, 0.005); 
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
                        // Instead of full neon shading, let's render a clean, unlit depth metric 
                        // to clearly visualize whether voxel seams are truly gone.
                        float depthVisualizer = saturate(1.0 - (hitEyeDepth / MAX_DISTANCE));
                        
                        // Map depth directly to a smooth gray-to-white gradient profile
                        finalColor = float3(depthVisualizer, depthVisualizer, depthVisualizer);
                    }
                }
                else
                {
                    // Keep your faint background aura active so you can track empty space evaluation
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