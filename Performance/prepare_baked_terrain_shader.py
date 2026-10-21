from pathlib import Path
root=Path(__file__).resolve().parent.parent
package=root/'Library/PackageCache/com.unity.render-pipelines.universal@bc6f352be672/Shaders/Terrain'
out=root/'Assets/PerformanceOptimizations/Floor/Baked'
shader=(package/'TerrainLit.shader').read_text()
shader=shader.replace('Shader "Universal Render Pipeline/Terrain/Lit"','Shader "Java Hollow/Baked Dirt Terrain"')
shader=shader.replace('    Properties\n    {','    Properties\n    {\n        _GroundBake("Baked dirt color and coverage", 2D) = "black" {}')
shader=shader.replace('#include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitPasses.hlsl"','#include "BakedTerrainPasses.hlsl"')
passes=(package/'TerrainLitPasses.hlsl').read_text()
passes=passes.replace('struct Attributes','TEXTURE2D(_GroundBake); SAMPLER(sampler_GroundBake);\n\nstruct Attributes',1)
needle='    outColor = half4(color.rgb, 1.0h);'
replacement='''    // Bake stores unlit color and filtered subpixel coverage; dynamic lights remain dynamic.
    half4 ground = SAMPLE_TEXTURE2D(_GroundBake, sampler_GroundBake, IN.uvMainAndLM.xy);
    InputData dirtInput = inputData;
    dirtInput.normalWS = half3(0,1,0); // The original dirt cap normals point up.
    dirtInput.bakedGI = SampleSH(half3(0,1,0));
    SurfaceData dirtSurface = (SurfaceData)0;
    dirtSurface.albedo = ground.rgb;
    dirtSurface.alpha = 1;
    dirtSurface.occlusion = 1;
    dirtSurface.smoothness = 1;
    half4 dirtColor = UniversalFragmentBlinnPhong(dirtInput, dirtSurface);
    dirtColor.rgb = MixFog(dirtColor.rgb, inputData.fogCoord);
    outColor = half4(lerp(color.rgb, dirtColor.rgb, ground.a), 1.0h);'''
assert passes.count(needle)==1
passes=passes.replace(needle,replacement)
header='// Derived from installed URP terrain shaders (Unity Companion License). Revalidate on URP upgrades.\n'
(out/'BakedTerrain.shader').write_text(header+shader)
(out/'BakedTerrainPasses.hlsl').write_text(header+passes)
print('Prepared terrain shader; uses existing terrain geometry, not an overlay mesh.')
