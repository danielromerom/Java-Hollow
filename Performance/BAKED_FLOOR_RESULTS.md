# Baked floor candidate — October 21, 2026

Status: user accepted the comparison appearance on October 21, 2026. Baked floor is enabled in the saved configuration for the next Play session of Demo for Show and future builds. Existing executable has not been rebuilt. Fresh startup and complete-scene performance still require verification.

The central terrain contains approximately 479,901 nearly flat dirt detail instances. The candidate bakes their color and coverage into a texture and shades it directly on the existing terrain. It removes the need to submit those individual dirt meshes. Original terrain assets, collision, trees, other details, audio and research scripts remain unchanged. This is primarily a rendering/draw submission optimization.

## Measured comparison
Unity Play Mode, current terrain-focused scene state; 4 seconds settling and 8 seconds measurement per phase. Headset active and reported worn for every measured frame.

| Phase | Average FPS | Mean frame ms | p95 frame ms | Triangles | Batches |
|---|---:|---:|---:|---:|---:|
| Original | 36.04 | 27.74 | 29.74 | 2,241,646 | 1,473 |
| Baked candidate | 62.80 | 15.92 | 17.63 | 322,042 | 89 |
| Original restored | 34.64 | 28.87 | 33.27 | 2,241,611 | 1,473 |

Source: floor-bake-headset-ab.txt. Desktop comparison separately preserved in floor-bake-desktop-ab.txt.

This is a short floor comparison, not proof of sustained 60 FPS in the complete experience or on another PC. The candidate's p95 exceeds the 16.67 ms budget. Editor timing is not compositor-delivered FPS.

## Appearance and remaining verification
Fine dirt edge coverage and surface brightness differ in comparison images. Visual acceptance is required before default activation. Texture uses approximately 89 MB including mipmaps; compression has not been validated. Terrain painting changes require rebaking. Candidate shader is validated only with the current Forward renderer; deferred and distant terrain basemap appearance are not validated.

Next: accept or refine appearance, validate fresh Play startup, test full scene and interactions, build a fresh standalone executable, then measure sustained HMD movement and worst-case views. Existing executable remains unchanged. No FPS cap was added.
