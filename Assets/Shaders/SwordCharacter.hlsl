// --- AUTOMATICALLY GENERATED CSG TREE CODE - DO NOT EDIT MANUALLY ---
float GetSwordCharacterDist(float3 p, int baseIdx, float smoothness)
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
    float prim_8 = DistanceToBoxLocal(localP_8, _CharData[baseIdx + 8].xyz);
    float3 localP_9 = mul(_CharMatrices[baseIdx + 9], float4(p, 1.0)).xyz;
    float prim_9 = DistanceToBoxLocal(localP_9, _CharData[baseIdx + 9].xyz);
    float step_0 = smax(prim_8, -(prim_9), smoothness);
    float3 localP_10 = mul(_CharMatrices[baseIdx + 10], float4(p, 1.0)).xyz;
    float prim_10 = DistanceToBoxLocal(localP_10, _CharData[baseIdx + 10].xyz);
    float3 localP_11 = mul(_CharMatrices[baseIdx + 11], float4(p, 1.0)).xyz;
    float prim_11 = DistanceToCapsuleLocal(localP_11, _CharData[baseIdx + 11].x, _CharData[baseIdx + 11].y, (int)_CharData[baseIdx + 11].z);
    float3 localP_12 = mul(_CharMatrices[baseIdx + 12], float4(p, 1.0)).xyz;
    float prim_12 = DistanceToBoxLocal(localP_12, _CharData[baseIdx + 12].xyz);
    float3 localP_13 = mul(_CharMatrices[baseIdx + 13], float4(p, 1.0)).xyz;
    float prim_13 = DistanceToBoxLocal(localP_13, _CharData[baseIdx + 13].xyz);
    float step_1 = smax(prim_11, -(prim_12), smoothness);
    float step_2 = smax(step_1, -(prim_13), smoothness);
    float step_3 = smin(step_0, prim_10, smoothness);
    float step_4 = smin(step_3, step_2, smoothness);
    float3 localP_14 = mul(_CharMatrices[baseIdx + 14], float4(p, 1.0)).xyz;
    float prim_14 = DistanceToCapsuleLocal(localP_14, _CharData[baseIdx + 14].x, _CharData[baseIdx + 14].y, (int)_CharData[baseIdx + 14].z);
    float3 localP_15 = mul(_CharMatrices[baseIdx + 15], float4(p, 1.0)).xyz;
    float prim_15 = DistanceToCapsuleLocal(localP_15, _CharData[baseIdx + 15].x, _CharData[baseIdx + 15].y, (int)_CharData[baseIdx + 15].z);
    float3 localP_16 = mul(_CharMatrices[baseIdx + 16], float4(p, 1.0)).xyz;
    float prim_16 = DistanceToCapsuleLocal(localP_16, _CharData[baseIdx + 16].x, _CharData[baseIdx + 16].y, (int)_CharData[baseIdx + 16].z);
    float3 localP_17 = mul(_CharMatrices[baseIdx + 17], float4(p, 1.0)).xyz;
    float prim_17 = DistanceToCapsuleLocal(localP_17, _CharData[baseIdx + 17].x, _CharData[baseIdx + 17].y, (int)_CharData[baseIdx + 17].z);
    float step_5 = smin(prim_0, prim_1, smoothness);
    float step_6 = smin(step_5, prim_2, smoothness);
    float step_7 = smin(step_6, prim_3, smoothness);
    float step_8 = smin(step_7, prim_4, smoothness);
    float step_9 = smin(step_8, prim_5, smoothness);
    float step_10 = smin(step_9, prim_6, smoothness);
    float step_11 = smin(step_10, prim_7, smoothness);
    float step_12 = smin(step_11, step_4, smoothness);
    float step_13 = smin(step_12, prim_14, smoothness);
    float step_14 = smin(step_13, prim_15, smoothness);
    float step_15 = smin(step_14, prim_16, smoothness);
    float step_16 = smin(step_15, prim_17, smoothness);

    return step_16;
}
