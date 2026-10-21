"""Prepare a reversible candidate against the installed URP version; no terrain edits."""
from pathlib import Path
import re, struct, math

root = Path(__file__).resolve().parent.parent
folder = root / 'Assets/PerformanceOptimizations/Floor/QuadCandidate'
folder.mkdir(exist_ok=True)
package = root / 'Library/PackageCache/com.unity.render-pipelines.universal@bc6f352be672'
mesh = (root / 'Assets/PerformanceOptimizations/Floor/DirtDetailCaps.asset').read_text()
raw = bytes.fromhex(re.search(r'_typelessdata: (\w+)', mesh).group(1))
verts = [struct.unpack_from('<14f', raw, n * 56) for n in range(42)]
left = min(verts[22:], key=lambda v:v[0]); right = max(verts[22:], key=lambda v:v[0])
back = min(verts[22:], key=lambda v:v[2]); front = max(verts[22:], key=lambda v:v[2])
sx = (right[10]-left[10])/(right[0]-left[0]); ox = left[10]-sx*left[0]
sz = (front[11]-back[11])/(front[2]-back[2]); oz = back[11]-sz*back[2]
error = max(max(abs(v[10]-(ox+sx*v[0])), abs(v[11]-(oz+sz*v[2]))) for v in verts[22:])
assert error < 0.00002, error
mask = '''// Java Hollow dirt-only candidate. Uses URP's existing lighting and texture sampling.
// Original mesh UVs have a near-affine mapping; generator validates error < 0.00002.
// Only for the supplied prefab/material with unit BaseMap tiling and no normal map.
#ifndef JAVA_HOLLOW_DIRT_MASK
#define JAVA_HOLLOW_DIRT_MASK
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
void JavaHollowClipDirt(float2 uv)
{
'''
mask += f'    float2 p = abs((uv - float2({ox:.10g}, {oz:.10g})) / float2({sx:.10g}, {sz:.10g}));\n'
mask += '    float edge = 0;\n'
for angle in [9,27,45,63,81]:
    mask += f'    edge = max(edge, dot(p, float2({math.cos(math.radians(angle)):.10g}, {math.sin(math.radians(angle)):.10g})));\n'
mask += f'    clip({0.5*math.cos(math.radians(9)):.10g} - edge);\n}}\n'
mask += '''half4 JavaHollowSampleAlbedoAlpha(float2 uv, TEXTURE2D_PARAM(map, mapSampler))
{
    JavaHollowClipDirt(uv);
    return SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(map, mapSampler));
}
#define SampleAlbedoAlpha JavaHollowSampleAlbedoAlpha
#endif
'''
(folder / 'DirtMaskInput.hlsl').write_text(mask)
shader = (package / 'Shaders/SimpleLit.shader').read_text()
shader = '// Derived from installed Unity URP SimpleLit.shader (Unity Companion License).\n// Revalidate against URP updates. All lighting/pass variants retained.\n' + shader
shader = shader.replace('Shader "Universal Render Pipeline/Simple Lit"', 'Shader "Java Hollow/Dirt Polygon Candidate"')
shader = shader.replace('#pragma shader_feature_local_fragment _ALPHATEST_ON', '// Polygon clipping is always enabled.')
shader = shader.replace('#pragma shader_feature_local _ALPHATEST_ON', '// Polygon clipping is always enabled.')
shader = shader.replace('HLSLPROGRAM', 'HLSLPROGRAM\n            #define _ALPHATEST_ON 1')
shader = shader.replace('#include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"', '#include "DirtMaskInput.hlsl"\n            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"')
# These two passes bypass SampleAlbedoAlpha, so wrap their fragment entry points.
for include, fragment in [('SimpleLitMetaPass.hlsl','UniversalFragmentMetaSimple'), ('Utils/Universal2D.hlsl','frag')]:
    line = f'#include "Packages/com.unity.render-pipelines.universal/Shaders/{include}"'
    shader = shader.replace(line, f'#define {fragment} JavaHollowOriginalFragment\n            {line}\n            #undef {fragment}\n            half4 {fragment}(Varyings input) : SV_Target\n            {{ JavaHollowClipDirt(input.uv); return JavaHollowOriginalFragment(input); }}')
# Keep our source material's keyword state; stock inspector can rewrite it on selection.
shader = shader.replace('    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.SimpleLitShader"', '')
(folder / 'DirtPolygon.shader').write_text(shader)

print(f"Prepared shader only. Maximum UV fit error {error:.9g}. No terrain asset modified.")
