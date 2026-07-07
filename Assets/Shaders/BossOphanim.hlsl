float3 GetOphanimColor(
    float3 hitPosWS, float3 normal, float3 viewDirWS, float cutDist,
    float3 baseColor, float3 outlineColor, float3 neonColor,
    float3 eyeColor, float3 pupilColor,
    float3 corePos, float3 coreForward)
{
    // Cuerpo
    float3 gridCoord = abs(frac(hitPosWS * 1.0) - 0.5);
    float gridLine = step(0.48, max(gridCoord.x, max(gridCoord.y, gridCoord.z))); 
    
    float rim = 1.0 - saturate(dot(normal, viewDirWS));
    rim = smoothstep(0.7, 0.85, rim);
    
    float neonMask = max(rim, gridLine);
    half3 bodyFinalColor = lerp(baseColor, outlineColor * 3.0, neonMask);

    // Scars de corte
    float scarMask = 1.0 - smoothstep(0.0, 0.08, cutDist);
    bodyFinalColor = lerp(bodyFinalColor, neonColor * 5.0, scarMask);

    // Ojo
    float distToCore = distance(hitPosWS, corePos);
    float isEye = 1.0 - smoothstep(2.4, 2.6, distToCore); 
    float3 dirToSurface = normalize(hitPosWS - corePos);
    
    float pupilDot = dot(dirToSurface, coreForward);
    float isIris = step(0.88, pupilDot);

    float3 eyeRight = normalize(cross(float3(0, 1, 0), coreForward));
    float3 eyeUp = cross(coreForward, eyeRight);
    float2 eyeUV = float2(dot(dirToSurface, eyeRight), dot(dirToSurface, eyeUp));
    
    // Pupila
    float triangleDist = max(abs(eyeUV.x) * 0.866 + eyeUV.y * 0.5, -eyeUV.y);
    float isPupil = step(triangleDist, 0.12); 

    half3 eyeFinalColor = eyeColor;
    eyeFinalColor = lerp(eyeFinalColor, pupilColor * 4.0, isIris); 
    eyeFinalColor = lerp(eyeFinalColor, float3(0.0, 0.0, 0.0), isPupil); 

    return lerp(bodyFinalColor, eyeFinalColor, isEye);
}