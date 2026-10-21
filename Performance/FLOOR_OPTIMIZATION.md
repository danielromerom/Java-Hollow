# Floor geometry optimization — 2026-10-07

Applied to the dirt-detail layer used by Terrain_Demo_For_Show. This is a mesh optimization, not a density reduction or a terrain-paint replacement.

## What changed

**October 7 follow-up:** `DirtDetailCaps.asset` now contains only the 20 upper-face triangles. This supersedes the 40-triangle description and measurements below, which document the first pass. All 42 stored vertices and attributes, original mesh/submesh bounds, and 20 upper triangles remain byte-identical; only 20 downward-facing triangle indices were removed. The material is opaque with back-face culling. The removed underside cannot be seen from above its plane, but views from below a detail surface can differ. Do not reuse this mesh for elevated or thick objects. Density, terrain, vegetation, colliders, lighting, particles and research behavior were not changed.

The exact prior mesh is backed up at `Performance/DirtDetailCaps-before-underside-removal.asset.txt`. To undo just this follow-up, copy those bytes back to `Assets/PerformanceOptimizations/Floor/DirtDetailCaps.asset`, preserving its existing `.meta`. `floor-top-validation.txt` records the structural validation; `optimize_floor_indices.py` is the guarded transformation. This halves this layer's submitted triangle indices, not total scene draw calls or guaranteed frame time. No HMD improvement or 60 FPS claim is established yet.

A short desktop CPU profiler capture (`xr-cpu-markers.txt`) showed approximately 23.64 ms in render-thread `GfxDeviceD3D12.WaitForGPU`, versus 0.47 ms in script Update and 0.40 ms in physics FixedUpdate. Inclusive markers overlap. The XR display was inactive in that session; it is not an HMD measurement. This supports prioritizing rendering over generic script rewrites, without proving the cause of the 11 FPS headset result.

- Added `Assets/PerformanceOptimizations/Floor/DirtDetailCaps.asset` and its prefab. The original cylinder mesh has 80 triangles / 88 vertices; the new mesh has 40 triangles / 42 vertices. Only the cylinder side walls were omitted. Both circular faces retain their original triangle positions, winding, normals, tangents and texture coordinates. Bounds, material, prefab transform and collider settings were preserved.
- Changed the cylinder detail prototype reference in `New Terrain.asset` and `TerrainData_30530da2-949b-4ba6-9e8a-63f464d6f89b.asset`, both under `Assets/Fantasy Forest Environment Free Sample/Scenes`. No detail map, terrain shape, tree, density, lighting, particle, interaction or research setting was changed. Other scenes sharing these terrain assets receive the same mesh optimization.
- Added Editor-only inspection, candidate-generation, A/B/A measurement and image-comparison tools in `JavaHollowFloorOptimization.cs`. They add no player update loop. The earlier investigation tools remain in `JavaHollowVegetationAudit.cs`.

The two terrain files retain their original byte lengths; each has only 23 changed bytes, in the prototype asset-reference GUID and GameObject file-ID regions. Original on-disk copies are in `Logs/FloorOriginals/`. The original Cylinder prefab is untouched. The open scene's pre-existing unsaved edits were not saved or discarded.

## Why this is appropriate here

The central layer generates 479,901 detail instances; an adjacent tile generates six. Their generated vertical scale is approximately 0.0000465273, giving a total cylinder thickness of about 0.093 mm. Rendering the cylinder side walls is expensive across this many instances, while their visible contribution is extremely small. Keeping both caps avoids changing the top or underside surface.

This is not mathematically pixel-identical: the tiny edge walls are gone. If the detail height is increased later, use the original cylinder or rebuild a mesh with sides. Do not reuse the cap mesh for thick or upright cylinders.

## Measured result

Same fixed simulator view, Editor Play Mode, full detail density, original → candidate → original. Each phase had a four-second settling period followed by eight seconds of sampling. These are short Editor measurements, not standalone/HMD acceptance or a measured GPU-only timing.

| Configuration | Mean frame time | Equivalent FPS | p95 frame time | Submitted triangles | Batches |
|---|---:|---:|---:|---:|---:|
| Original, first | 24.93 ms | 40.1 | 28.31 ms | 28.13 million | 2,946 |
| Optimized caps | 21.46 ms | 46.6 | 25.59 ms | 15.29 million | 2,943 |
| Original, restored | 25.95 ms | 38.5 | 29.87 ms | 28.15 million | 2,950 |

The benefit is primarily reduced GPU geometry work and vertex data. Draw calls remain essentially unchanged. This does not establish stable 60 FPS.

## Verification and visual limits

- Unity compiled the Editor tools and ran the scene and comparison successfully.
- Every generated position, rotation, scale and count matched exactly after substituting the candidate prefab, across all affected patches. Retained cap vertex attributes and original bounds were checked exactly.
- Rendered and visually inspected original/candidate pairs at standing height, 25 cm above the ground, and a 3 cm grazing view. All images are in `FloorComparison/`. The floor appears essentially unchanged in these sampled views.
- At 1280×720, mean absolute RGB differences on a 0–255 scale were 0.00317 standing, 0.01077 close, and 0.02168 grazing. The proportion of pixels with summed RGB differences over six was 0.043%, 0.230%, and 0.341%, respectively. These are local image comparisons, not a guarantee for every viewpoint or stereo rendering.
- Terrain regeneration surfaced an existing source-prototype warning about `Weeping_Willow 7` containing no valid mesh renderer. Its source prefab and tree settings were not altered by this change. The original negative-scale BoxCollider warning also remains.
- This pass did not rebuild the executable. Existing EXEs still contain the old floor; rebuild before standalone/HMD testing.

## Next checks

Open Demo for Show and run normally: inspect the dirt/grass boundary while standing, crouching and moving; check for gaps or shimmer. Build a fresh normal-quality Windows executable and test the same route and resolution. In Quest 2, check both eyes, close/grazing views, locomotion and frame-time consistency. Record application CPU/GPU frame times and headset refresh/resolution; the remaining approximately 2,940 batches and dense overlapping detail coverage still warrant profiling.

To revert only the rendering change, restore the two terrain assets from version control (after preserving any subsequent terrain edits), or reassign their affected detail prototype to the original `Textures/Cylinder.prefab`. The optimized assets and Editor tools are separable from that change.

Evidence: `floor-inspection.txt`, `floor-mesh-validation.txt`, `floor-caps-ab.txt`, `floor-image-comparison.txt`; earlier isolation result: `central-details-ab.txt`.

October 14 update: a two-triangle polygon-clipped dirt prototype now supersedes the cap prototype on the two affected terrains. See TERRAIN_OPTIMIZATION.md for current changes, headset measurements and rollback. The October 7 evidence above remains historical.
