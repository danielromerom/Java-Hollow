using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit diagnostics only; does not modify terrain, prefabs, or scene objects.
public static class JavaHollowVegetationAudit
{
    static Terrain probeTerrain;
    static float originalDensity;
    static double phaseStart;
    static int phase, lastFrame;
    static readonly List<float> frameTimes = new List<float>();
    static readonly List<int> triangles = new List<int>();
    static readonly List<int> batches = new List<int>();
    static StringBuilder probeReport;

    [MenuItem("Tools/Java Hollow/Measure Central Terrain Details %&#F8")]
    public static void MeasureDetails()
    {
        if (!EditorApplication.isPlaying || probeTerrain != null) return;
        probeTerrain = Terrain.activeTerrains.FirstOrDefault(t => t.name == "TERRAIN");
        if (probeTerrain == null) throw new InvalidOperationException("Central terrain not found.");
        originalDensity = probeTerrain.detailObjectDensity;
        probeReport = new StringBuilder("Editor Play Mode diagnostic; fixed camera; details temporarily hidden, then restored. Not an HMD benchmark.\n");
        phase = 0;
        phaseStart = EditorApplication.timeSinceStartup;
        lastFrame = Time.frameCount;
        EditorApplication.update += ProbeUpdate;
        EditorApplication.playModeStateChanged += StopProbe;
        AssemblyReloadEvents.beforeAssemblyReload += RestoreProbe;
    }

    static void ProbeUpdate()
    {
        if (!EditorApplication.isPlaying || probeTerrain == null) { RestoreProbe(); return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        double elapsed = EditorApplication.timeSinceStartup - phaseStart;
        if (elapsed < 3) return;
        if (elapsed < 8)
        {
            frameTimes.Add(Time.unscaledDeltaTime * 1000f);
            triangles.Add(UnityStats.triangles);
            batches.Add(UnityStats.batches);
            return;
        }
        frameTimes.Sort();
        if (frameTimes.Count > 0)
            probeReport.AppendLine($"{(phase == 1 ? "Central details hidden" : "Original details")}: samples={frameTimes.Count}, meanMs={frameTimes.Average():F2}, p95Ms={frameTimes[(int)((frameTimes.Count - 1) * 0.95)]:F2}, trianglesMean={triangles.Average():F0}, batchesMean={batches.Average():F0}");
        frameTimes.Clear(); triangles.Clear(); batches.Clear();
        phase++;
        if (phase < 3)
        {
            probeTerrain.detailObjectDensity = phase == 1 ? 0 : originalDensity;
            phaseStart = EditorApplication.timeSinceStartup;
        }
        else
        {
            RestoreProbe();
            Directory.CreateDirectory("Performance");
            File.WriteAllText("Performance/central-details-ab.txt", probeReport.ToString());
            Debug.Log("Central terrain A/B/A complete; original detail density restored.");
        }
    }

    static void StopProbe(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) RestoreProbe();
    }

    static void RestoreProbe()
    {
        if (probeTerrain != null) probeTerrain.detailObjectDensity = originalDensity;
        probeTerrain = null;
        frameTimes.Clear(); triangles.Clear(); batches.Clear();
        EditorApplication.update -= ProbeUpdate;
        EditorApplication.playModeStateChanged -= StopProbe;
        AssemblyReloadEvents.beforeAssemblyReload -= RestoreProbe;
    }

    [MenuItem("Tools/Java Hollow/Audit Vegetation Geometry %&g")]
    public static void Audit()
    {
        var report = new StringBuilder();
        var counts = new Dictionary<GameObject, int>();
        var scene = SceneManager.GetActiveScene();
        report.AppendLine("Scene: " + scene.path);
        report.AppendLine("Inventory estimates, not visible draw counts or GPU timings. Detail counts are stored density-map values, not rendered instances.");
        foreach (var terrain in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)))
        {
            if (!terrain.isActiveAndEnabled || terrain.terrainData == null) continue;
            var data = terrain.terrainData;
            var prototypes = data.treePrototypes;
            foreach (var tree in data.treeInstances)
            {
                var prefab = prototypes[tree.prototypeIndex].prefab;
                if (prefab != null) counts[prefab] = counts.TryGetValue(prefab, out var n) ? n + 1 : 1;
            }
            report.AppendLine($"Terrain {terrain.name}: size={data.size}, trees={data.treeInstanceCount}");
            var details = data.detailPrototypes;
            for (int i = 0; i < details.Length; i++)
            {
                long density = 0;
                foreach (int value in data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, i)) density += value;
                var p = details[i];
                report.AppendLine($" Detail {i}: densitySum={density}, prefab={AssetDatabase.GetAssetPath(p.prototype)}, texture={AssetDatabase.GetAssetPath(p.prototypeTexture)}, instanced={p.useInstancing}, mode={p.renderMode}");
                if (p.prototype != null) Describe(p.prototype, report);
            }
        }
        foreach (var pair in counts.OrderByDescending(p => p.Value))
        {
            report.AppendLine($"TREE instances={pair.Value} prefab={AssetDatabase.GetAssetPath(pair.Key)}");
            Describe(pair.Key, report);
        }
        Directory.CreateDirectory("Performance");
        File.WriteAllText("Performance/vegetation-geometry.txt", report.ToString());
        Debug.Log("Vegetation geometry audit written to Performance/vegetation-geometry.txt");
    }

    static void Describe(GameObject prefab, StringBuilder report)
    {
        foreach (var group in prefab.GetComponentsInChildren<LODGroup>(true))
        {
            report.AppendLine($" LODGroup {group.name}: size={group.size}, enabled={group.enabled}");
            var lods = group.GetLODs();
            for (int i = 0; i < lods.Length; i++)
            {
                long triangles = 0;
                foreach (var renderer in lods[i].renderers)
                {
                    if (renderer == null) continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    var mesh = filter != null ? filter.sharedMesh : (renderer as SkinnedMeshRenderer)?.sharedMesh;
                    triangles += TriangleCount(mesh);
                }
                report.AppendLine($"  LOD{i}: threshold={lods[i].screenRelativeTransitionHeight}, triangles={triangles}, renderers={lods[i].renderers.Length}");
            }
        }
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            report.AppendLine($" Mesh {filter.name}: triangles={TriangleCount(filter.sharedMesh)}");
        foreach (var material in prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
            report.AppendLine($" Material {AssetDatabase.GetAssetPath(material)}: shader={material.shader.name}, instancing={material.enableInstancing}, queue={material.renderQueue}");
    }

    static long TriangleCount(Mesh mesh)
    {
        if (mesh == null) return 0;
        long count = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
            if (mesh.GetTopology(i) == MeshTopology.Triangles) count += (long)mesh.GetIndexCount(i) / 3;
        return count;
    }
}
