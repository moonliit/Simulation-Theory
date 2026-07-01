// --- AUTOMATICALLY GENERATED CSG TREE CODE - DO NOT EDIT MANUALLY ---
float GetCharacterDist(float3 p, int baseIdx, float smoothness)
{
    float3 localP_0 = mul(_CharMatrices[baseIdx + 0], float4(p, 1.0)).xyz;
    float prim_0 = DistanceToBoxLocal(localP_0, _CharData[baseIdx + 0].xyz);
    float3 localP_1 = mul(_CharMatrices[baseIdx + 1], float4(p, 1.0)).xyz;
    float prim_1 = length(localP_1) - _CharData[baseIdx + 1].x;
    float step_0 = smax(prim_0, -(prim_1), smoothness);

    return step_0;
}
