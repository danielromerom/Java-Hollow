using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

// Explicit Editor-only measurements. Pipeline experiments use disposable clones.
public static class JavaHollowXRProbe
{
    static readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
    static readonly List<float> frames = new List<float>(), gpu = new List<float>(), compositor = new List<float>();
    static readonly FrameTiming[] timings = new FrameTiming[1];
    static readonly List<double> cpu = new List<double>();
    static XRDisplaySubsystem display;
    static RenderPipelineAsset originalOverride;
    static UniversalRenderPipelineAsset trial;
    static ScriptableRendererData renderer;
    static bool running, experiment;
    static int phase, lastFrame, presentSamples, presentTrue;
    static double start;
    static StringBuilder report;

    [MenuItem("Tools/Java Hollow/Record XR Baseline %&#F10")]
    public static void Baseline() => Start(false);

    [MenuItem("Tools/Java Hollow/Compare XR Forward Plus %&#F9")]
    public static void ForwardPlus() => Debug.LogWarning("Live Forward+ comparison disabled after a rendering failure. Use Record XR Baseline; do not switch the XR pipeline during Play Mode.");

    static void Start(bool compare)
    {
        if (running || !EditorApplication.isPlaying) return;
        SubsystemManager.GetSubsystems(displays);
        display = displays.FirstOrDefault(d => d.running);
        if (display == null) { Debug.LogError("XR capture needs an active headset display."); return; }
        originalOverride = QualitySettings.renderPipeline;
        report = new StringBuilder("XR Editor measurement; short sample, no standalone or experiment acceptance claim.\n");
        report.AppendLine($"Device={InputDevices.GetDeviceAtXRNode(XRNode.Head).name}; quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}; graphics={SystemInfo.graphicsDeviceName}; API={SystemInfo.graphicsDeviceType}");
        if (display.TryGetDisplayRefreshRate(out var hz)) report.AppendLine($"RefreshHz={hz}");
        var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        report.AppendLine($"URP={asset?.name}; renderScale={asset?.renderScale}; MSAA={asset?.msaaSampleCount}");
        for (int i = 0; i < display.GetRenderPassCount(); i++)
        {
            display.GetRenderPass(i, out var pass);
            report.AppendLine($"Pass {i}: {pass.renderTargetDesc.width}x{pass.renderTargetDesc.height}, layers={pass.renderTargetDesc.volumeDepth}, samples={pass.renderTargetDesc.msaaSamples}");
        }
        if (compare)
        {
            if (asset == null) throw new InvalidOperationException("URP required.");
            trial = UnityEngine.Object.Instantiate(asset);
            var serialized = new SerializedObject(trial);
            int index = serialized.FindProperty("m_DefaultRendererIndex").intValue;
            var list = serialized.FindProperty("m_RendererDataList");
            renderer = UnityEngine.Object.Instantiate((ScriptableRendererData)list.GetArrayElementAtIndex(index).objectReferenceValue);
            var rendererProperties = new SerializedObject(renderer);
            var mode = rendererProperties.FindProperty("m_RenderingMode");
            if (mode == null || mode.intValue != 0) { Cleanup(); throw new InvalidOperationException("Expected Forward renderer."); }
            mode.intValue = 2;
            rendererProperties.ApplyModifiedPropertiesWithoutUndo();
            list.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        experiment = compare; phase = 0; lastFrame = Time.frameCount;
        start = EditorApplication.timeSinceStartup; running = true;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += StopOnExit;
        AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        Debug.Log("XR capture started. Keep headset worn and look at the same area.");
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || display == null || !display.running) { Save("ABORTED: headset display stopped."); Cleanup(); return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        FrameTimingManager.CaptureFrameTimings();
        double elapsed = EditorApplication.timeSinceStartup - start;
        if (elapsed < 8) return;
        if (elapsed < 20)
        {
            frames.Add(Time.unscaledDeltaTime * 1000f);
            if (display.TryGetAppGPUTimeLastFrame(out var g)) gpu.Add(g * 1000f);
            if (display.TryGetCompositorGPUTimeLastFrame(out var c)) compositor.Add(c * 1000f);
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].cpuFrameTime > 0) cpu.Add(timings[0].cpuFrameTime);
            if (InputDevices.GetDeviceAtXRNode(XRNode.Head).TryGetFeatureValue(CommonUsages.userPresence, out var present))
            { presentSamples++; if (present) presentTrue++; }
            return;
        }
        frames.Sort();
        report.AppendLine($"{(phase == 1 ? "Forward+ trial" : "Original")}: n={frames.Count}; meanFrameMs={Mean(frames)}; p95Ms={(frames.Count == 0 ? "unavailable" : frames[(int)((frames.Count-1)*0.95)].ToString("F2"))}; appGPUms={Mean(gpu)}; compositorGPUms={Mean(compositor)}; CPUframeMs={(cpu.Count == 0 ? "unavailable" : cpu.Average().ToString("F2"))}; presence={presentTrue}/{presentSamples}; trianglesSnapshot={UnityStats.triangles}; batchesSnapshot={UnityStats.batches}");
        frames.Clear(); gpu.Clear(); compositor.Clear(); cpu.Clear(); presentSamples = presentTrue = 0;
        phase++;
        if (experiment && phase < 3)
        {
            QualitySettings.renderPipeline = phase == 1 ? trial : originalOverride;
            start = EditorApplication.timeSinceStartup;
        }
        else { Save("COMPLETE; original pipeline restored."); Cleanup(); }
    }

    static string Mean(List<float> values) => values.Count == 0 ? "unavailable" : values.Average().ToString("F2");
    static void Save(string status)
    {
        Directory.CreateDirectory("Performance");
        File.WriteAllText("Performance/xr-" + (experiment ? "forwardplus" : "baseline") + ".txt", report + "\n" + status);
        Debug.Log("XR measurement: " + status);
    }
    static void StopOnExit(PlayModeStateChange state)
    { if (state == PlayModeStateChange.ExitingPlayMode) { Save("ABORTED: Play Mode stopped."); Cleanup(); } }
    static void Cleanup()
    {
        if (running) QualitySettings.renderPipeline = originalOverride;
        running = false;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= StopOnExit;
        AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
        if (trial != null) UnityEngine.Object.DestroyImmediate(trial);
        if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
        trial = null; renderer = null;
        frames.Clear(); gpu.Clear(); compositor.Clear(); cpu.Clear(); presentSamples = presentTrue = 0;
    }
}
