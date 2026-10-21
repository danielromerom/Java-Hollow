using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

// Separate prefab candidates; never edits original tree meshes or the central tree_1.
public static class JavaHollowTreeCandidates
{
    const string Source="Assets/Fantasy Forest Environment Free Sample/Meshes/Prefabs/";
    const string Folder="Assets/PerformanceOptimizations/Trees";
    static readonly string[] Names={"Willow_7","Chestnut_1","Chestnut_7"};
    static IEnumerable<Terrain> Terrains => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>(true));
    static Dictionary<GameObject,GameObject> Mapping(bool reverse=false)
    {
        var map=new Dictionary<GameObject,GameObject>();
        foreach(var name in Names)
        {
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(Source+name+".prefab");
            var candidate=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+name+"_LOD1.prefab");
            if(original==null||candidate==null)throw new InvalidOperationException("Build tree candidates first.");
            map.Add(reverse?candidate:original,reverse?original:candidate);
        }
        return map;
    }
    static long TriangleCount(LOD lod) => lod.renderers.Where(r=>r!=null).Sum(r=>(long)(r.GetComponent<MeshFilter>()?.sharedMesh?.triangles.Length??0)/3);

    [MenuItem("Tools/Java Hollow/Build Surrounding Tree Candidates %&#u")]
    static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Build outside Play Mode.");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var report=new StringBuilder("Separate LOD1 candidates. tree_1 and original prefabs unchanged.\n");
        foreach(var name in Names)
        {
            string path=Folder+"/"+name+"_LOD1.prefab";
            if(File.Exists(path))throw new InvalidOperationException("Candidate already exists; preserve it before rebuilding.");
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(Source+name+".prefab");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(original);
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                instance.name=name+"_LOD1";
                var groups=instance.GetComponentsInChildren<LODGroup>(true);
                if(groups.Length!=1)throw new InvalidOperationException("Expected one LODGroup: "+name);
                var group=groups[0];var lods=group.GetLODs();
                if(lods.Length<3)throw new InvalidOperationException("Missing source LOD levels.");
                var retained=new HashSet<Renderer>(lods.Skip(1).SelectMany(l=>l.renderers));
                foreach(var renderer in lods[0].renderers)if(renderer!=null&&!retained.Contains(renderer))renderer.enabled=false;
                group.SetLODs(lods.Skip(1).ToArray());
                PrefabUtility.SaveAsPrefabAsset(instance,path);
                report.AppendLine($"{name}: closest geometry {TriangleCount(lods[0])} -> {TriangleCount(lods[1])} triangles; materials/colliders/wind retained; {lods.Length-1} LOD levels retained.");
            }
            finally {UnityEngine.Object.DestroyImmediate(instance);}
        }
        Directory.CreateDirectory("Performance");File.WriteAllText("Performance/tree-candidate-build.txt",report.ToString());
        Debug.Log("Surrounding tree candidates built, not applied.");
    }
    static bool Remap(TerrainData data,Dictionary<GameObject,GameObject> map)
    {
        var prototypes=data.treePrototypes;bool changed=false;
        foreach(var p in prototypes)if(p.prefab!=null&&map.TryGetValue(p.prefab,out var replacement)){p.prefab=replacement;changed=true;}
        if(changed)data.treePrototypes=prototypes;
        return changed;
    }
    class State { public Terrain terrain;public TerrainData original,candidate; }
    static State[] saved;
    static readonly List<double> ms=new List<double>();
    static readonly List<int> tris=new List<int>();
    static int phase,last,worn;static double start;static StringBuilder results;
    [MenuItem("Tools/Java Hollow/Compare Surrounding Tree Candidates %&#o")]
    static void Compare()
    {
        if(!EditorApplication.isPlaying||saved!=null)return;
        var map=Mapping();saved=Terrain.activeTerrains.Select(t=>new State{terrain=t,original=t.terrainData}).ToArray();
        try
        {
            foreach(var s in saved){s.candidate=UnityEngine.Object.Instantiate(s.original);Remap(s.candidate,map);}
            results=new StringBuilder($"{DateTime.Now:O}; original / surrounding LOD1 / original; 5s settling + 8s samples; tree_1 unchanged.\n");
            phase=worn=0;last=Time.frameCount;ms.Clear();tris.Clear();start=EditorApplication.timeSinceStartup;
            EditorApplication.update+=Tick;AssemblyReloadEvents.beforeAssemblyReload+=Restore;
            Debug.Log("Tree LOD comparison started; hold the same view for 40 seconds.");
        }catch{Restore();throw;}
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying){Restore();return;}
        if(last==Time.frameCount)return;last=Time.frameCount;double elapsed=EditorApplication.timeSinceStartup-start;
        if(elapsed<5)return;
        if(elapsed<13)
        {
            ms.Add(Time.unscaledDeltaTime*1000);tris.Add(UnityStats.triangles);
            if(InputDevices.GetDeviceAtXRNode(XRNode.Head).TryGetFeatureValue(CommonUsages.userPresence,out bool p)&&p)worn++;
            return;
        }
        if(ms.Count>0)results.AppendLine($"{(phase==1?"Surrounding LOD1":"Original")}: FPS={1000/ms.Average():F2}; ms={ms.Average():F2}; triangles={tris.Average():F0}; worn={worn}/{ms.Count}");
        ms.Clear();tris.Clear();worn=0;phase++;
        if(phase==3){Restore();File.WriteAllText("Performance/tree-candidate-ab.txt",results.ToString()+"Original trees restored.\n");Debug.Log("Tree LOD comparison complete; original trees restored.");return;}
        foreach(var s in saved){s.terrain.terrainData=phase==1?s.candidate:s.original;s.terrain.Flush();}
        start=EditorApplication.timeSinceStartup;
    }
    static void Restore()
    {
        EditorApplication.update-=Tick;AssemblyReloadEvents.beforeAssemblyReload-=Restore;
        if(saved!=null)foreach(var s in saved){if(s.terrain!=null)s.terrain.terrainData=s.original;if(s.candidate!=null)UnityEngine.Object.DestroyImmediate(s.candidate);}
        saved=null;
    }
    [MenuItem("Tools/Java Hollow/Apply Surrounding Tree Candidates")]
    static void Apply()=>Persist(false);
    [MenuItem("Tools/Java Hollow/Restore Original Surrounding Trees")]
    static void Revert()=>Persist(true);
    static void Persist(bool reverse)
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        var map=Mapping(reverse);var data=Terrains.Select(t=>t.terrainData).Where(d=>d!=null).ToList();
        var floor=Resources.Load<JavaHollowBakedFloor>(JavaHollowBakedFloor.ResourceName);
        if(floor!=null)foreach(var tile in floor.tiles){data.Add(tile.source);data.Add(tile.baked);}
        int changed=0;
        foreach(var d in data.Where(d=>d!=null).Distinct())
        {
            if(!Remap(d,map))continue;
            EditorUtility.SetDirty(d);AssetDatabase.SaveAssetIfDirty(d);changed++;
        }
        foreach(var t in Terrains)t.Flush();
        File.WriteAllText("Performance/tree-candidate-application.txt",$"{DateTime.Now:O}; reverted={reverse}; terrain data assets updated={changed}; includes disabled tiles and baked floor; tree counts/positions, tree_1, scene activation unchanged.\n");
        Debug.Log("Tree prototype update complete: "+changed+" terrain data assets.");
    }
}
