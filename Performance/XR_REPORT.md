# Connected-headset diagnostic follow-up

## Standalone result, October 7, 17:24–17:25

Build completed successfully with zero errors (`standalone-build.txt`). Player.log confirmed VR quality, Direct3D11, RTX 2060 and Single Pass Instanced. Worn-headset intervals recorded 10.02, 9.88, 9.91 and 13.83 FPS with presence true for every sampled frame; other mostly-worn intervals recorded 9.56–12.13 FPS. The 200.43 FPS interval had headset presence only 125/2010 frames and is not a valid VR performance result. Selected measurements are preserved in `standalone-vr-results.txt`.

This confirms the severe slowdown persists without the Unity Editor. It does not identify GPU rendering versus compositor/Link transport delay. Do not interpret the earlier XR wait marker as proof of a runtime bug or CPU bottleneck. The next useful discriminator is whether PC Quest Link home is smooth, followed by runtime application/compositor GPU and Link encode/transport timing. Avoid another visual-quality change without that evidence.

The log also contains an OpenXR hand-provider initialization failure and repeated Meta aim-retrieval initialization errors. These warrant an interaction configuration check but are not proven responsible for approximately 100 ms frames. Hand tracking and interactions were preserved.

## DX11 result and standalone comparison preparation

The user reported approximately 11 FPS after restarting with the DX11 launcher. Editor.log confirms `-force-d3d11`, Direct3D 11.0 and the RTX 2060; Single Pass Instanced and 1824x1968 eye resolution remain active. The API switch did not establish a useful improvement. Do not attribute the issue specifically to DX12.

Added Tools > Java Hollow > Build Standalone VR Check. It saves a separate copy of the current Demo for Show scene to `Assets/PerformanceOptimizations/StandaloneCheck.unity` and builds to `Builds/VRCheck/JavaHollowVRCheck.exe`. A build-specific define selects VR quality before scene loading, matching the Editor tests rather than the project's Medium standalone default, and logs frame intervals/headset presence every ten seconds. Measurement code is excluded from ordinary builds and the Editor. No permanent quality/default/API change was made. The build was invoked and paused at Unity's existing-scene save prompt; build completion is not yet verified.

After successful compilation, `Launch-Standalone-VR-Check.cmd` starts the player in DX11. Stop Editor Play Mode first, enter Quest Link, and wear the headset for at least thirty seconds in the same view. Read `VR_CHECK` lines from the player's log, excluding startup/warmup and samples without headset presence. This is the next comparison, not a guaranteed FPS fix.

## Active headset CPU capture, October 7, 17:05

`xr-cpu-markers.txt` now contains 60 frames from an XR session that remained running, with headset presence true at start and end. Main-thread frame duration averaged 96.42 ms. Inclusive `FrameEvents.XRBeginFrame` averaged 66.71 ms, overlapping `Gfx.WaitForRenderThread` (66.68 ms); do not add these values. Script Update averaged 0.406 ms and physics FixedUpdate 0.680 ms. The capture prioritizes the graphics/XR synchronization path over gameplay-script optimization. Wait markers alone cannot distinguish GPU load, compositor pacing, driver behavior or runtime synchronization.

Prepared `Launch-JavaHollow-DX11.cmd` to launch the installed Unity 6000.2.9f1 Editor with `-force-d3d11` for this session only. Save and close the current Editor first to preserve unsaved edits; then use the launcher, enter Quest Link and compare the same view at the same headset resolution/refresh and quality. Allow shader compilation/import to settle. Verify `<DX11>` in the Editor title or Direct3D11 in a new capture. No permanent graphics API or quality setting has been changed, and no DirectX 11 improvement has been measured yet. Launching normally restores the normal API selection.

Unity documents this launch flag: https://docs.unity3d.com/6000.2/Documentation/Manual/EditorCommandLineArguments.html . This is an isolated compatibility/performance comparison, not a conclusion that DirectX 12 caused the slowdown.

## Forward+ comparison rejected

The subsequent comparison recorded only its original-renderer phase: 80.93 ms average (approximately 12.4 FPS), 96.16 ms p95, headset presence 148/148. It ended with `ABORTED: Play Mode stopped` before recording a Forward+ phase. The user reported blue assets and stopped rendering. No improvement can be inferred.

The Editor log confirms the Play Mode exit callback ran and the scene backup was reloaded. The callback restores the original quality pipeline; the test changed only disposable clones, and tracked rendering configuration assets have no changes. The exact cause of the blue rendering is not established by the log. Earlier headset-unavailable initialization errors are not proof of the cause of this trial's visual failure.

The live comparison entry point is now disabled to prevent another attempt. Instructions below to run that comparison are superseded. Future profiling should keep the original renderer fixed. No project assets or scene objects were removed, and the existing floor optimization is retained. A fresh normal Play Mode run is needed to confirm visual recovery; a standalone CPU/GPU profiler capture should precede another renderer experiment.

## Successful baseline, October 7, 2026, 16:42

The user's subsequent capture succeeded (`xr-baseline.txt`): headset presence 120/120 samples, refresh 72 Hz, RTX 2060 / Direct3D12, VR quality, URP scale 1 and 4x MSAA. The two-layer XR target is 1824x1968 per eye. Mean frame interval was 99.39 ms (approximately 10.1 FPS); p95 was 117.03 ms. The final Editor statistics snapshot reported 41,295,650 triangles and 3,535 batches; this is a rendering counter, not a count of unique scene geometry.

GPU telemetry is unusable in this capture: app GPU time is negative and the converted compositor value is suspiciously high (1045.50 ms). Do not use either to classify the bottleneck or reinterpret their units without validating the provider. CPU frame time (99.18 ms) can include waits and does not prove CPU-bound execution. Geometry and lighting remain strong candidates, not established causes of the complete slowdown.

Next controlled test: run Tools > Java Hollow > Compare XR Forward Plus in the active headset session, keeping the same view for approximately 60 seconds. Compare original/trial/original frame intervals and inspect lighting. The original pipeline is restored automatically. No permanent renderer change has been made.

The previously applied floor geometry optimization remains in place. The user reported 47–50 FPS in desktop Editor testing and 11–12 FPS with the headset. These are different workloads, not interchangeable benchmarks.

Added `Assets/Editor/JavaHollowXRProbe.cs`, an Editor-only diagnostic. It records actual XR eye targets, GPU timing when exposed by the runtime, CPU frame timing when available, frame intervals, and headset presence. Its optional Forward+ comparison uses disposable pipeline/renderer clones and restores the original pipeline on completion or Play Mode exit. It makes no persistent rendering-setting changes and is excluded from players.

The script compiled and its comparison command executed. The attempt stopped before any pipeline change because Unity reported **XR capture needs an active headset display**. No valid headset timing or Forward+ improvement was measured. Play Mode was stopped; unsaved scene edits were preserved. No new executable was built.

To continue: establish the PC Link session inside the headset before starting Play Mode, keep the headset awake/worn, then run Tools > Java Hollow > Record XR Baseline. Keep looking at the same area for 20 seconds. Results go to `Performance/xr-baseline.txt`. If the same error appears, diagnose XR session startup before comparing rendering changes.

Tools > Java Hollow > Compare XR Forward Plus performs a roughly one-minute original/trial/original comparison. Forward+ may change which lights affect objects; it is only a temporary experiment, not an approved permanent appearance change. Results go to `Performance/xr-forwardplus.txt`. Compare lighting in-headset as well as timings before adopting it.

Prior runtime evidence showed Single Pass Instanced already active and 1824×1968 recommended pixels per eye. The runtime identified a Meta Quest 3 despite the earlier Quest 2 description. Recheck these in the next active session. Geometry, stereo pixel workload, and many lights remain candidates; this failed capture does not establish which limits current headset performance.
