# Terrain detail optimization — October 14, 2026

Applied: the two terrain assets now use `Floor/QuadCandidate/DirtQuad.prefab` for the flat dirt detail layer. This is a measured improvement, not a complete solution to the VR frame-rate target. The open scene was not saved; the user's isolated/disabled objects remain as they were.

## Diagnosis

The first hierarchy object, `TERRAIN`, is both a parent and a Terrain renderer. Turning off its children leaves its own detail layer rendering. It has 479,901 stored dirt detail instances on a 10×10 m terrain; the neighboring tile has six more. GPU instancing was already enabled.

In the initial desktop isolation test, bare terrain submitted approximately 5,253 triangles / 8 batches. Surface plus details submitted 6.59 million triangles / 1,013 batches. Surface plus trees submitted about 259,000 triangles / 85 batches. This identifies the dense ground details as the main geometric cost in this view, rather than the terrain heightmap alone. These figures do not establish that every remaining frame-time cost comes from terrain.

## Change and rationale

- Each flat dirt patch now uses a four-vertex, two-triangle quad instead of a 20-triangle cap with 42 stored vertices. An analytic shader mask reproduces the original 20-sided outline. No instance density, position, scale, rotation, terrain height, grass or tree placement was reduced.
- The mesh preserves the original bounds and prefab transform. Generated detail transforms and counts were compared across all affected patches and matched exactly.
- The material preserves the original dirt texture, color and URP Simple Lit lighting. The shader derives from the installed URP SimpleLit shader and retains its stereo, instancing, lighting and render-pass variants. Depth, shadow, normal and motion-vector passes also clip to the polygon. Texture alpha is not used to remove additional coverage.
- The source material uses unit texture tiling, zero offset and no normal map. The candidate is specific to those assumptions. Revalidate before changing its material or upgrading URP; it is not a general replacement for Simple Lit.
- Two TerrainData prototype references were updated and saved individually. Original assets were backed up under `Logs/DirtQuadOriginals/`. Asset byte lengths remain unchanged, with 24 changed bytes per terrain asset. Original cap/cylinder assets remain available.
- Added Editor-only terrain isolation and candidate comparison tools. No new player Update loop, frame cap, research timing, interaction, particle, lighting, audio or quality-setting change was introduced.

Benefit: primarily less GPU geometry/vertex processing, including stereo rendering. Draw-call count is essentially unchanged. Shader clipping adds fragment work, so performance depends on the view and GPU; it is not automatically faster in all desktop situations.

## Measurements

The first A/B/A run was invalidated by the headset becoming active partway through; retained as `dirt-quad-ab-transition-invalid.txt`. Do not use it as evidence of an FPS gain.

The repeated headset run used the same isolated scene and approximately the same view. Small head motion remained. Each phase settled for four seconds and sampled for eight seconds:

| Phase | Mean frame time | Equivalent FPS | p95 | Submitted triangles | Batches |
|---|---:|---:|---:|---:|---:|
| Original caps | 55.32 ms | 18.08 | 60.13 ms | 19.36 million | 1,452 |
| Quad candidate | 42.04 ms | 23.79 | 55.85 ms | 2.23 million | 1,447 |
| Original restored | 55.70 ms | 17.95 | 59.44 ms | 19.50 million | 1,464 |

Relative to the two baseline means: about 24% lower average frame time / 32% higher FPS. This is a short Editor headset test with most environment objects disabled, not full-scene or standalone acceptance. It does not demonstrate 40 or 60 FPS.

An overlapping XR capture confirmed headset presence in all 256 sampled frames: RTX 2060, DX11, VR quality, 72 Hz, renderScale 1, MSAA 4, stereo target 1824×1872 per layer. Its timing spans different candidate phases and must not be treated as a separate baseline. XR-provided GPU metrics were invalid/unusable; no GPU-only time claim is made. See `dirt-quad-xr-context.txt` and `dirt-quad-ab.txt`.

## Appearance and verification

Unity compiled the C# and shader, rendered the comparisons, and ran the candidate in the active headset without a shader error found in the checked log. Every generated dirt instance transform and count matched. See `dirt-quad-validation.txt` and `dirt-quad-asset-validation.txt`.

Original/candidate images were inspected at standing, close and grazing angles in `DirtQuadComparison/`. Mean absolute RGB differences on a 0–255 scale were 0.073, 0.143 and 0.084. They look very similar in these samples, but are not pixel-identical: polygon clipping and MSAA rasterized edges can differ; tree wind also changes between frames. Moving/stereo edge shimmer still needs participant-height inspection. The original heightmap, coverage pattern and texture were preserved.

The standalone EXE was not rebuilt in this pass. Existing EXEs contain the previous implementation. Remaining known source-tree/collider warnings were not addressed by this scoped change.

## Next tests and remaining work

1. Editor: restore the normal set of objects you disabled, run Demo for Show, and inspect dirt/grass boundaries, close views, locomotion, interactions, particles, lighting and audio. The optimization already applies to the terrain assets; do not save the isolated scene as the normal build accidentally.
2. Standalone: build the normal/full-quality scene again and repeat the same route. Compare the same resolution and quality settings. Existing test EXEs are stale.
3. HMD: inspect both eyes while standing, crouching and turning; watch for shimmering/missing dirt patches. Measure application CPU/GPU frame times with headset presence confirmed, not just the Editor Stats FPS. Keep refresh rate, render resolution and view consistent.
4. Remaining likely costs include around 1,450 batches in this isolated stereo view and overlapping dirt coverage. Use a GPU capture/Frame Debugger to separate detail shading/overdraw, submission, shadows and the restored full environment. The runtime XR timing API here did not provide usable GPU timing.
5. The larger structural option is baking the flat dirt coverage into a terrain/overlay texture, eliminating hundreds of thousands of individual patches. That could substantially change edge detail, filtering and lighting, so it was deliberately not implemented without a visual comparison and user approval. Reducing density, removing vegetation/particles, lowering resolution or changing research timing was also not used.

## Rollback

To revert only this pass, use the added `Tools > Java Hollow > Restore Dirt Caps` command outside Play Mode. It changes only matching quad prototype references back to `DirtDetailCaps.prefab`, preserving subsequent terrain painting. Alternatively, manually reassign that prototype on both affected terrain assets. The prior files are backed up in `Logs/DirtQuadOriginals/`; copying whole backups is appropriate only if no later terrain edits need preservation. Do not restore the whole scene or unrelated settings.

Tools: `Isolate Terrain Cost` (Play Mode, automatic restore); `Measure Dirt Quad Candidate` (Play Mode, requires original caps references, automatic restore); `Compare Dirt Quad Images` (Edit Mode, requires original caps references); `Apply Dirt Quad Candidate` (Edit Mode, after comparison). Once applied, restore caps before rerunning the old-to-new comparison, then reapply afterward.
