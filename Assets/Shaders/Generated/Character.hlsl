// --- AUTOMATICALLY GENERATED CSG TREE CODE - DO NOT EDIT MANUALLY ---
float GetCharacterDist(float3 p, int baseIdx, float smoothness)
{
    float3 localP_0 = mul(_CharMatrices[baseIdx + 0], float4(p, 1.0)).xyz;
    float prim_0 = DistanceToCapsuleLocal(localP_0, _CharData[baseIdx + 0].x, _CharData[baseIdx + 0].y, (int)_CharData[baseIdx + 0].z);
    float3 localP_1 = mul(_CharMatrices[baseIdx + 1], float4(p, 1.0)).xyz;
    float prim_1 = length(localP_1) - _CharData[baseIdx + 1].x;
    float3 localP_2 = mul(_CharMatrices[baseIdx + 2], float4(p, 1.0)).xyz;
    float prim_2 = DistanceToCapsuleLocal(localP_2, _CharData[baseIdx + 2].x, _CharData[baseIdx + 2].y, (int)_CharData[baseIdx + 2].z);
    float3 localP_3 = mul(_CharMatrices[baseIdx + 3], float4(p, 1.0)).xyz;
    float prim_3 = DistanceToCapsuleLocal(localP_3, _CharData[baseIdx + 3].x, _CharData[baseIdx + 3].y, (int)_CharData[baseIdx + 3].z);
    float3 localP_4 = mul(_CharMatrices[baseIdx + 4], float4(p, 1.0)).xyz;
    float prim_4 = length(localP_4) - _CharData[baseIdx + 4].x;
    float3 localP_5 = mul(_CharMatrices[baseIdx + 5], float4(p, 1.0)).xyz;
    float prim_5 = DistanceToCapsuleLocal(localP_5, _CharData[baseIdx + 5].x, _CharData[baseIdx + 5].y, (int)_CharData[baseIdx + 5].z);
    float3 localP_6 = mul(_CharMatrices[baseIdx + 6], float4(p, 1.0)).xyz;
    float prim_6 = DistanceToCapsuleLocal(localP_6, _CharData[baseIdx + 6].x, _CharData[baseIdx + 6].y, (int)_CharData[baseIdx + 6].z);
    float3 localP_7 = mul(_CharMatrices[baseIdx + 7], float4(p, 1.0)).xyz;
    float prim_7 = length(localP_7) - _CharData[baseIdx + 7].x;
    float3 localP_8 = mul(_CharMatrices[baseIdx + 8], float4(p, 1.0)).xyz;
    float prim_8 = DistanceToCapsuleLocal(localP_8, _CharData[baseIdx + 8].x, _CharData[baseIdx + 8].y, (int)_CharData[baseIdx + 8].z);
    float3 localP_9 = mul(_CharMatrices[baseIdx + 9], float4(p, 1.0)).xyz;
    float prim_9 = DistanceToCapsuleLocal(localP_9, _CharData[baseIdx + 9].x, _CharData[baseIdx + 9].y, (int)_CharData[baseIdx + 9].z);
    float3 localP_10 = mul(_CharMatrices[baseIdx + 10], float4(p, 1.0)).xyz;
    float prim_10 = DistanceToCapsuleLocal(localP_10, _CharData[baseIdx + 10].x, _CharData[baseIdx + 10].y, (int)_CharData[baseIdx + 10].z);
    float3 localP_11 = mul(_CharMatrices[baseIdx + 11], float4(p, 1.0)).xyz;
    float prim_11 = DistanceToCapsuleLocal(localP_11, _CharData[baseIdx + 11].x, _CharData[baseIdx + 11].y, (int)_CharData[baseIdx + 11].z);
    float step_0 = smin(prim_0, prim_1, smoothness);
    float step_1 = smin(step_0, prim_2, smoothness);
    float step_2 = smin(step_1, prim_3, smoothness);
    float step_3 = smin(step_2, prim_4, smoothness);
    float step_4 = smin(step_3, prim_5, smoothness);
    float step_5 = smin(step_4, prim_6, smoothness);
    float step_6 = smin(step_5, prim_7, smoothness);
    float step_7 = smin(step_6, prim_8, smoothness);
    float step_8 = smin(step_7, prim_9, smoothness);
    float step_9 = smin(step_8, prim_10, smoothness);
    float step_10 = smin(step_9, prim_11, smoothness);

    return step_10;
}
