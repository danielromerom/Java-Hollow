using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

public static class JavaHollowTreeTypeProbe
{
    class State { public Terrain terrain; public TerrainData original, copy; public TreeInstance[] trees; }
    static State[] saved;
    static string[] types;
    static int phase,last,worn;
    static double start;
    static StringBuilder report;
    static readonly List<double> ms=new List<double>();
    static readonly List<int> triangles=new List<int>();
    [MenuItem("Tools/Java Hollow/Isolate Tree Types %&#t")]
    static void Start()
    {
        if(!EditorApplication.isPlaying || saved!=null)return;
        saved=Terrain.activeTerrains.Select(t=>new State {terrain=t,original=t.terrainData,trees=t.terrainData.treeInstances}).ToArray();
        try
        {
            types=saved.SelectMany(s=>s.trees.Select(i=>s.original.treePrototypes[i.prototypeIndex].prefab.name)).Distinct().OrderBy(n=>n).ToArray();
            report=new StringBuilder($"{DateTime.Now:O}; temporary cloned terrain data; each named phase omits ONLY that tree type; 3s settle, 6s sample.\n");
            foreach(var name in types) report.AppendLine($"{name}: count={saved.Sum(s=>s.trees.Count(i=>s.original.treePrototypes[i.prototypeIndex].prefab.name==name))}");
            foreach(var s in saved) {s.copy=UnityEngine.Object.Instantiate(s.original);s.terrain.terrainData=s.copy;}
            phase=worn=0;last=Time.frameCount;ms.Clear();triangles.Clear();start=EditorApplication.timeSinceStartup;
            EditorApplication.update+=Tick;AssemblyReloadEvents.beforeAssemblyReload+=Restore;
        }
        catch {Restore();throw;}
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying){Restore();return;}
        if(last==Time.frameCount)return;last=Time.frameCount;
        double elapsed=EditorApplication.timeSinceStartup-start;
        if(elapsed<3)return;
        if(elapsed<9)
        {
            ms.Add(Time.unscaledDeltaTime*1000);triangles.Add(UnityStats.triangles);
            if(InputDevices.GetDeviceAtXRNode(XRNode.Head).TryGetFeatureValue(CommonUsages.userPresence,out bool p)&&p)worn++;
            return;
        }
        if(ms.Count>0)report.AppendLine($"{(phase==0||phase>types.Length?"All trees":"Without "+types[phase-1])}: FPS={1000/ms.Average():F2}; ms={ms.Average():F2}; triangles={triangles.Average():F0}; worn={worn}/{ms.Count}");
        ms.Clear();triangles.Clear();worn=0;phase++;
        if(phase==types.Length+2)
        {
            Restore();File.WriteAllText("Performance/tree-types.txt",report.ToString()+"All original terrain data restored.\n");
            Debug.Log("Tree type isolation complete; all trees restored.");return;
        }
        foreach(var s in saved)
        {
            s.copy.SetTreeInstances(phase>types.Length?s.trees:s.trees.Where(i=>s.original.treePrototypes[i.prototypeIndex].prefab.name!=types[phase-1]).ToArray(),false);
            s.terrain.Flush();
        }
        start=EditorApplication.timeSinceStartup;
    }
    static void Restore()
    {
        EditorApplication.update-=Tick;AssemblyReloadEvents.beforeAssemblyReload-=Restore;
        if(saved!=null)foreach(var s in saved)
        {
            if(s.terrain!=null)s.terrain.terrainData=s.original;
            if(s.copy!=null)UnityEngine.Object.DestroyImmediate(s.copy);
        }
        saved=null;
    }
}
