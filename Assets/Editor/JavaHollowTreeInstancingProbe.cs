using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

// Temporary A/B/A test. All material flags are restored, including on Play exit/reload.
public static class JavaHollowTreeInstancingProbe
{
    static readonly Dictionary<Material,bool> saved = new Dictionary<Material,bool>();
    static readonly List<double> times = new List<double>();
    static readonly List<int> batches = new List<int>();
    static double start;
    static int phase, lastFrame, worn;
    static StringBuilder report;

    [MenuItem("Tools/Java Hollow/Compare Tree Instancing %&#i")]
    static void Start()
    {
        if (!EditorApplication.isPlaying || saved.Count != 0) return;
        report = new StringBuilder($"{DateTime.Now:O}; Tree material instancing A/B/A; 4s settling + 8s measurement.\n");
        foreach (var t in Terrain.activeTerrains)
            foreach (var p in t.terrainData.treePrototypes)
                if (p.prefab != null)
                    foreach (var r in p.prefab.GetComponentsInChildren<Renderer>(true))
                        foreach (var m in r.sharedMaterials)
                            if (m != null && !saved.ContainsKey(m))
                            {
                                saved.Add(m,m.enableInstancing);
                                report.AppendLine($"{AssetDatabase.GetAssetPath(m)}; shader={m.shader.name}; originalInstancing={m.enableInstancing}");
                            }
        phase = worn = 0; lastFrame = Time.frameCount; times.Clear(); batches.Clear();
        start = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += Restore;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Restore(); return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        double elapsed = EditorApplication.timeSinceStartup-start;
        if (elapsed < 4) return;
        if (elapsed < 12)
        {
            times.Add(Time.unscaledDeltaTime*1000); batches.Add(UnityStats.batches);
            if (InputDevices.GetDeviceAtXRNode(XRNode.Head).TryGetFeatureValue(CommonUsages.userPresence,out bool p) && p) worn++;
            return;
        }
        if (times.Count>0) report.AppendLine($"{(phase==1?"Instancing enabled":"Original")}: FPS={1000/times.Average():F2}; ms={times.Average():F2}; batches={batches.Average():F1}; worn={worn}/{times.Count}");
        times.Clear(); batches.Clear(); worn=0; phase++;
        if (phase==3)
        {
            Restore(); File.WriteAllText("Performance/tree-instancing-ab.txt",report.ToString()+"Restored all material flags.\n");
            Debug.Log("Tree instancing comparison complete; original flags restored."); return;
        }
        foreach (var pair in saved) pair.Key.enableInstancing = phase==1 || pair.Value;
        foreach (var t in Terrain.activeTerrains) t.Flush();
        start=EditorApplication.timeSinceStartup;
    }

    static void Restore()
    {
        foreach (var pair in saved) if (pair.Key != null) pair.Key.enableInstancing=pair.Value;
        saved.Clear();
        EditorApplication.update-=Tick;
        AssemblyReloadEvents.beforeAssemblyReload-=Restore;
    }
}
