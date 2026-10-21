using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

// Explicit isolation test. No TerrainData, scene assets, particles or lights are edited.
public static class JavaHollowTerrainProbe
{
    struct State { public Terrain terrain; public bool foliage; public float density, trees; }
    static State[] saved;
    static readonly List<double> ms = new List<double>();
    static readonly List<int> triangles = new List<int>();
    static readonly List<int> batches = new List<int>();
    static readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
    static StringBuilder report;
    static int phase, lastFrame, presence;
    static double started;
    static readonly string[] names = { "Original", "Surface only", "Surface and details", "Surface and trees", "Original restored" };

    [MenuItem("Tools/Java Hollow/Isolate Terrain Cost %&#F12")]
    static void Start()
    {
        if (!EditorApplication.isPlaying || saved != null) return;
        saved = Terrain.activeTerrains.Select(t => new State { terrain = t, foliage = t.drawTreesAndFoliage, density = t.detailObjectDensity, trees = t.treeDistance }).ToArray();
        report = new StringBuilder($"{DateTime.Now:O}; active terrains={saved.Length}; quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}; API={SystemInfo.graphicsDeviceType}\nTemporary layer isolation, not a proposed visual change. Keep the viewpoint fixed.\n");
        foreach (var s in saved)
        {
            var t = s.terrain; var d = t.terrainData;
            report.AppendLine($"Terrain {t.name}: data={AssetDatabase.GetAssetPath(d)}; size={d.size}; heightResolution={d.heightmapResolution}; detailResolution={d.detailResolution}; patch={d.detailResolutionPerPatch}; trees={d.treeInstanceCount}; pixelError={t.heightmapPixelError}; foliage={s.foliage}; detailDensity={s.density}; material={AssetDatabase.GetAssetPath(t.materialTemplate)}; shader={t.materialTemplate?.shader.name}; layers={d.terrainLayers.Length}");
            for (int layer = 0; layer < d.detailPrototypes.Length; layer++)
            {
                long count = 0;
                foreach (int value in d.GetDetailLayer(0, 0, d.detailWidth, d.detailHeight, layer)) count += value;
                report.AppendLine($" Detail {layer}: storedDensitySum={count}; prefab={AssetDatabase.GetAssetPath(d.detailPrototypes[layer].prototype)}; instanced={d.detailPrototypes[layer].useInstancing}");
            }
        }
        phase = presence = 0; lastFrame = Time.frameCount; started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += Restore;
        Debug.Log("Terrain isolation started: surface/details/trees will briefly appear separately, then restore. Keep looking at the same spot for 40 seconds.");
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Save("ABORTED: Play Mode stopped"); Restore(); return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        if (EditorApplication.timeSinceStartup - started < 3) return;
        if (EditorApplication.timeSinceStartup - started < 8)
        {
            ms.Add(Time.unscaledDeltaTime * 1000.0); triangles.Add(UnityStats.triangles); batches.Add(UnityStats.batches);
            if (InputDevices.GetDeviceAtXRNode(XRNode.Head).TryGetFeatureValue(CommonUsages.userPresence, out bool worn) && worn) presence++;
            return;
        }
        SubsystemManager.GetSubsystems(displays);
        if (ms.Count > 0) report.AppendLine($"{names[phase]}: samples={ms.Count}; meanMs={ms.Average():F2}; fps={1000/ms.Average():F2}; triangles={triangles.Average():F0}; batches={batches.Average():F0}; XR={displays.Any(d => d.running)}; worn={presence}/{ms.Count}");
        ms.Clear(); triangles.Clear(); batches.Clear(); presence = 0;
        phase++;
        Apply();
        if (phase == names.Length) { Save("COMPLETE; original settings restored"); Restore(); return; }
        started = EditorApplication.timeSinceStartup;
    }

    static void Apply()
    {
        foreach (var s in saved)
        {
            if (s.terrain == null) continue;
            s.terrain.drawTreesAndFoliage = s.foliage && phase != 1;
            s.terrain.detailObjectDensity = phase == 3 ? 0 : s.density;
            s.terrain.treeDistance = phase == 2 ? 0 : s.trees;
        }
    }

    static void Save(string status)
    {
        Directory.CreateDirectory("Performance");
        File.WriteAllText("Performance/terrain-isolation.txt", report + "\n" + status);
    }

    static void Restore()
    {
        if (saved != null) { phase = 4; Apply(); }
        saved = null; ms.Clear(); triangles.Clear(); batches.Clear();
        EditorApplication.update -= Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= Restore;
    }
}
