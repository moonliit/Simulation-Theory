// --- AUTOMATICALLY GENERATED CSG TREE CODE - DO NOT EDIT MANUALLY ---
float GetSwordDist(float3 p, int baseIdx)
{
    float3 localP_0 = mul(_CharMatrices[baseIdx + 0], float4(p, 1.0)).xyz;
    float prim_0 = DistanceToBoxLocal(localP_0, _CharData[baseIdx + 0].xyz);
    float3 localP_1 = mul(_CharMatrices[baseIdx + 1], float4(p, 1.0)).xyz;
    float prim_1 = DistanceToBoxLocal(localP_1, _CharData[baseIdx + 1].xyz);
    float step_0 = max(prim_0, -(prim_1));
    float3 localP_2 = mul(_CharMatrices[baseIdx + 2], float4(p, 1.0)).xyz;
    float prim_2 = DistanceToBoxLocal(localP_2, _CharData[baseIdx + 2].xyz);
    float3 localP_3 = mul(_CharMatrices[baseIdx + 3], float4(p, 1.0)).xyz;
    float prim_3 = DistanceToCapsuleLocal(localP_3, _CharData[baseIdx + 3].x, _CharData[baseIdx + 3].y, (int)_CharData[baseIdx + 3].z);
    float3 localP_4 = mul(_CharMatrices[baseIdx + 4], float4(p, 1.0)).xyz;
    float prim_4 = DistanceToBoxLocal(localP_4, _CharData[baseIdx + 4].xyz);
    float3 localP_5 = mul(_CharMatrices[baseIdx + 5], float4(p, 1.0)).xyz;
    float prim_5 = DistanceToBoxLocal(localP_5, _CharData[baseIdx + 5].xyz);
    float step_1 = max(prim_3, -(prim_4));
    float step_2 = max(step_1, -(prim_5));
    float step_3 = min(step_0, prim_2);
    float step_4 = min(step_3, step_2);

    return step_4;
}
