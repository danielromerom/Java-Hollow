// Java Hollow dirt-only candidate. Uses URP's existing lighting and texture sampling.
// Original mesh UVs have a near-affine mapping; generator validates error < 0.00002.
// Only for the supplied prefab/material with unit BaseMap tiling and no normal map.
#ifndef JAVA_HOLLOW_DIRT_MASK
#define JAVA_HOLLOW_DIRT_MASK
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
void JavaHollowClipDirt(float2 uv)
{
    float2 p = abs((uv - float2(0.5002244781, 0.4999441968)) / float2(-0.9922326663, -0.9922328298));
    float edge = 0;
    edge = max(edge, dot(p, float2(0.9876883406, 0.156434465)));
    edge = max(edge, dot(p, float2(0.8910065242, 0.4539904997)));
    edge = max(edge, dot(p, float2(0.7071067812, 0.7071067812)));
    edge = max(edge, dot(p, float2(0.4539904997, 0.8910065242)));
    edge = max(edge, dot(p, float2(0.156434465, 0.9876883406)));
    clip(0.4938441703 - edge);
}
half4 JavaHollowSampleAlbedoAlpha(float2 uv, TEXTURE2D_PARAM(map, mapSampler))
{
    JavaHollowClipDirt(uv);
    return SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(map, mapSampler));
}
#define SampleAlbedoAlpha JavaHollowSampleAlbedoAlpha
#endif
