# Static asset lighting pass

Scene: Assets/Terrain_Demo_For_Show.unity. Status: baked and saved; participant visual approval and HMD FPS measurement pending. No new executable built.

## Changes and evidence
- 190 asset point lights (168 mushrooms, 22 other asset lights) now Baked. Unity reports isBaked=true for all 190. Colors, ranges, intensities, and components retained. Expected benefit: remove their real-time per-pixel GPU lighting cost in both eyes; actual FPS gain unmeasured.
- Generated three directional lightmaps (two 2048 square and one 512 square), plus 4,969 baked probes including automatically generated terrain probes. Lightmaps have texture memory/sampling cost; baking is not literally free.
- Prepared 578 static mesh receivers; skipped 38 scripted/movable receivers. Added secondary UVs to 103 mesh copies, retaining original meshes and colliders. No geometry simplification.
- Added 4,048 scene light-probe positions for movable objects and tree lighting. Terrain trees now use the scene probes rather than automatic per-tree probes, which made the central tree nearly black in comparison captures. Tree prefab MeshRenderers configured to receive probes; meshes, materials, LODs and placement preserved. Imported FBX prototypes left unchanged.
- Enabled asset groups except CAMPFIRE and MARSHMELLOW STICKS. Moved Mushroom Holder out of MARSHMELLOW STICKS, keeping world transforms, so mushrooms remain enabled independently.
- Initially disabled JavaHollowBakedFloor runtime substitution during lighting setup. After the user reported terrain-only FPS falling from 70+ to at most 30, restored its original enabled state. This is a suspected regression source, not yet confirmed by an A/B measurement. Updated the optimized terrain shader to retain sampled baked GI instead of overwriting it with ambient SH when lightmaps are enabled. Shader compilation, runtime floor/lightmap compatibility and FPS require a fresh Play Mode check. Terrain data was not edited.
- Added Editor-only audit and bake commands under Tools > Java Hollow. No runtime research scripts, audio, interactions, frame caps, particle settings, or XR settings changed.

## Visual review required
before.png and after.png use the same starting camera. Baked ground illumination is warmer/brighter and direct specular highlights differ from real-time lighting. The corrected central tree is lit again; baked lighting is not pixel-identical to the original. Review before accepting for the experiment. Moving a baked light or static receiver requires rebaking. Toggling a baked light off does not remove its already-baked illumination.

## Verification and next test
Editor scripts compiled and bake completed twice. Final bake-result.txt verifies 190 baked lights and three lightmaps. No claim of 60 FPS, executable validation, or headset visual acceptance yet.

Open Demo for Show, press Play with Quest Link active, and inspect mushrooms, central tree, canopy, table surfaces, cups/hands, fireflies and interactions. Record sustained FPS after 20 seconds facing the same view, then walk/turn through the cafe. Check CPU and GPU frame times, draw calls, triangles and GC Alloc in a development EXE on the target PC. A fresh EXE must be built to include this bake; an existing EXE is unchanged.

Furniture geometry/draw-call costs remain a separate possible bottleneck. Do not lower resolution, remove vegetation, or introduce a frame cap to hide them.

## Backups and rebaking
BeforeBake.unity in Assets/PerformanceOptimizations/AssetLighting preserves the live scene before this pass, including unsaved user terrain fixes. floor-config-before.asset and OriginalTreePrefabs in this directory preserve the prior configuration/prefab files. Keep these with the lighting changes for rollback; restore the scene and associated prefab/config changes together, not unrelated terrain edits.

For future rebakes use Tools > Java Hollow > Bake Static Asset Lighting (or Lighting > Generate Lighting). Do not rerun Prepare: it intentionally refuses to overwrite the baseline. Leave terrain Bake Light Probes For Trees disabled to retain the verified shared-probe appearance.
