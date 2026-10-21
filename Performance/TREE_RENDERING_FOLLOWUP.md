# Tree rendering follow-up — October 21, 2026

Fresh Play startup verified bakedEnabled=True and central terrain baked=True. Central dirt density is zero in the runtime clone. All 42 terrain renderers already have terrain GPU instancing enabled. Only two tiles are currently active; the second has six dirt patches. The other tiles do not have the dense dirt layer, so copying the central dirt bake onto them would be incorrect and add memory/shading cost without addressing their trees.

Headset terrain isolation (headset worn throughout): ground only 71.77 FPS; ground plus grass/details 71.65 FPS; ground plus trees 40.53 FPS; original restored 43.13 FPS. Earlier baseline in the same test was 50.60 FPS. This identifies tree rendering as the next major cost under this view. It does not distinguish vertex work, leaf overdraw, lighting or XR frame pacing by itself.

Tree material instancing A/B/A: original 59.82 FPS, instancing enabled 43.65 FPS, original restored 58.35 FPS; batches approximately 85 in all phases. Headset worn throughout. No improvement demonstrated; all original material flags restored. Head movement, shader warm-up, and runtime pacing can influence this short comparison, so the slowdown is not a universal claim about GPU instancing.

XR baseline: 72 Hz, DX11, 1824x1872 per-eye array, URP scale 1, MSAA 4. Mean frame time 26.73 ms. App GPU and compositor timing counters returned unusable readings; no GPU-duration diagnosis is claimed. Half-refresh ASW may contribute to sustained 36 FPS, but ASW state has not been verified.

No trees or particles permanently removed; no scene saved, quality reductions or research timing changes. Diagnostic tools restore temporary settings. Tree-type isolation results are saved separately in tree-types.txt.

## Tree-type isolation
Five temporary omission phases, headset worn for all measurements except 3 frames in the final restored baseline. All tree data restored. Baseline 36.67 FPS, final baseline 39.26 FPS. Without Chestnut_1 (4 instances): 57.10 FPS. Without Chestnut_7 (2): 62.72 FPS. Without Spruce_2 (13): 46.33 FPS. Without tree_1 (1): 34.94 FPS. Without Willow_7 (5): 71.62 FPS.

Willow_7 is the strongest next target in the tested view; chestnuts are also significant. Removing these assets was a diagnostic only, not an accepted optimization. Differences are not additive and can include occlusion and XR pacing effects. Next work should compare foliage shader/overdraw cost with geometry/LOD cost, preserving trees and wind; do not blindly raise LOD thresholds or disable two-sided leaves. The existing willow LOD size and threshold scale must be evaluated together before declaring its low numerical thresholds incorrect.
