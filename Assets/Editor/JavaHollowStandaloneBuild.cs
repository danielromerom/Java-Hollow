using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class JavaHollowStandaloneBuild
{
    [MenuItem("Tools/Java Hollow/Build Standalone VR Check %&#F11")]
    static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Terrain_Demo_For_Show.unity") throw new InvalidOperationException("Open Demo for Show first.");
        const string snapshot = "Assets/PerformanceOptimizations/StandaloneCheck.unity";
        // Capture current user edits without saving over or closing their source scene.
        if (!EditorSceneManager.SaveScene(scene, snapshot, true)) throw new IOException("Cannot save build snapshot.");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { snapshot },
            locationPathName = "Builds/VRCheck/JavaHollowVRCheck.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
            extraScriptingDefines = new[] { "JAVA_HOLLOW_VR_CHECK" }
        });
        Directory.CreateDirectory("Performance");
        File.WriteAllText("Performance/standalone-build.txt", $"{DateTime.Now:O}: {report.summary.result}; errors={report.summary.totalErrors}; size={report.summary.totalSize}; seconds={report.summary.totalTime.TotalSeconds:F1}\nScene is a copy of the open Demo for Show scene, including unsaved edits. VR quality selected only in this diagnostic build.\n");
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Standalone VR check build failed; inspect Console.");
        Debug.Log("Standalone VR check ready: Builds/VRCheck/JavaHollowVRCheck.exe");
    }
}
