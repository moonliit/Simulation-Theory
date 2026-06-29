#ifndef SDF_PRIMITIVES_INCLUDED
#define SDF_PRIMITIVES_INCLUDED

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

float smax(float a, float b, float k)
{
    return -smin(-a, -b, k);
}

#endif
