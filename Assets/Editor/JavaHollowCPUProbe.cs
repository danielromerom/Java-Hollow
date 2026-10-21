using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.XR;

// Short, explicit capture with the existing renderer. No scene or quality changes.
public static class JavaHollowCPUProbe
{
    static bool active, previousEnabled, previousEditor;
    static double started;
    static int first;
    static XRDisplaySubsystem display;
    static bool presenceAtStart;

    [MenuItem("Tools/Java Hollow/Capture CPU Bottleneck")]
    static void Start()
    {
        if (active || !EditorApplication.isPlaying) return;
        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        display = displays.FirstOrDefault(d => d.running);
        if (display == null)
        {
            Debug.LogError("CPU capture needs an active Quest Link XR session. Enter Link before starting Play Mode.");
            return;
        }
        InputDevices.GetDeviceAtXRNode(XRNode.Head).TryGetFeatureValue(CommonUsages.userPresence, out presenceAtStart);
        previousEnabled = ProfilerDriver.enabled;
        previousEditor = ProfilerDriver.profileEditor;
        first = ProfilerDriver.lastFrameIndex;
        ProfilerDriver.profileEditor = false;
        ProfilerDriver.enabled = true;
        active = true;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        Debug.Log("CPU capture started; keep the headset worn for 12 seconds.");
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || display == null || !display.running)
        {
            Cleanup();
            Debug.LogWarning("CPU capture aborted because the XR session stopped; no new report written.");
            return;
        }
        if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup - started < 12) return;
        try
        {
            int last = ProfilerDriver.lastFrameIndex;
            var totals = new Dictionary<string, double>();
            int count = 0;
            for (int frame = Math.Max(Math.Max(first + 1, last - 59), ProfilerDriver.firstFrameIndex); frame <= last; frame++)
            {
                bool valid = false;
                for (int thread = 0; thread < 128; thread++)
                {
                    using (var data = ProfilerDriver.GetRawFrameDataView(frame, thread))
                    {
                        if (!data.valid) break;
                        if (data.threadName != "Main Thread" && data.threadName != "Render Thread") continue;
                        valid = true;
                        for (int sample = 0; sample < data.sampleCount; sample++)
                        {
                            string key = data.threadName + " / " + data.GetSampleName(sample);
                            totals.TryGetValue(key, out double sum);
                            totals[key] = sum + data.GetSampleTimeMs(sample);
                        }
                    }
                }
                if (valid) count++;
            }
            var report = new StringBuilder($"CPU profiler capture: {count} frames. Inclusive marker ms/frame; nested markers overlap and must not be summed. Profiling adds overhead.\n");
            InputDevices.GetDeviceAtXRNode(XRNode.Head).TryGetFeatureValue(CommonUsages.userPresence, out bool presenceAtEnd);
            report.AppendLine($"Captured={DateTime.Now:O}; XR running throughout; headset presence start={presenceAtStart}, end={presenceAtEnd}; GPU={SystemInfo.graphicsDeviceName}; API={SystemInfo.graphicsDeviceType}");
            foreach (var item in totals.OrderByDescending(p => p.Value).Take(70))
                report.AppendLine($"{item.Value / Math.Max(count, 1):F3} ms | {item.Key}");
            Directory.CreateDirectory("Performance");
            File.WriteAllText("Performance/xr-cpu-markers.txt", report.ToString());
            Debug.Log("CPU capture saved to Performance/xr-cpu-markers.txt.");
        }
        finally { Cleanup(); }
    }

    static void Cleanup()
    {
        if (!active) return;
        active = false;
        ProfilerDriver.enabled = previousEnabled;
        ProfilerDriver.profileEditor = previousEditor;
        EditorApplication.update -= Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
    }
}
