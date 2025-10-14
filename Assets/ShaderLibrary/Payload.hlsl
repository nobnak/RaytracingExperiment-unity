#ifndef __PAYLOAD_HLSL__
#define __PAYLOAD_HLSL__

struct RayPayload {
    bool hit;
    float4 color;
    float shadowFactor;
};

struct ShadowPayload {
    bool shadowed;
};

struct AttributeData {
    float2 barycentrics;
};
#endif