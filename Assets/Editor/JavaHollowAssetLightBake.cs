using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class JavaHollowAssetLightBake
{
    const string Folder="Assets/PerformanceOptimizations/AssetLighting";
    static IEnumerable<T> All<T>() where T:Component => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true));
    static bool Excluded(Transform t)=>JavaHollowLightingAudit.PathOf(t).Contains("/CAMPFIRE")||JavaHollowLightingAudit.PathOf(t).Contains("/MARSHMELLOW STICKS");
    static bool Movable(MeshRenderer r)=>r.GetComponentInParent<Rigidbody>()!=null || r.GetComponentInParent<Animator>()!=null || r.GetComponentsInParent<MonoBehaviour>(true).Any(b=>b!=null);
    [MenuItem("Tools/Java Hollow/Prepare Static Asset Lighting %&#F6")]
    public static void Prepare()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!="Assets/Terrain_Demo_For_Show.unity")throw new InvalidOperationException("Open Demo for Show in Edit Mode.");
        if(File.Exists(Folder+"/BeforeBake.unity"))throw new InvalidOperationException("A baseline already exists. Inspect it before preparing again.");
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("Performance/AssetLighting");AssetDatabase.Refresh();
        if(!EditorSceneManager.SaveScene(scene,Folder+"/BeforeBake.unity",true))throw new InvalidOperationException("Baseline backup failed.");
        var holder=scene.GetRootGameObjects().Single(g=>g.name=="IN SCENE ASSETS");
        var mushrooms=holder.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Mushroom Holder");
        // Keep all original world transforms; separate mushrooms from excluded roasting props.
        Undo.SetTransformParent(mushrooms,holder.transform,"Separate mushroom lighting group");
        Undo.RecordObject(holder,"Enable asset holder");holder.SetActive(true);
        foreach(Transform child in holder.transform)
        {
            Undo.RecordObject(child.gameObject,"Set participant asset visibility");
            child.gameObject.SetActive(child.name!="CAMPFIRE"&&child.name!="MARSHMELLOW STICKS");
        }
        var config=Resources.Load<JavaHollowBakedFloor>(JavaHollowBakedFloor.ResourceName);
        if(config!=null)
        {
            File.Copy(AssetDatabase.GetAssetPath(config),"Performance/AssetLighting/floor-config-before.asset",true);
            // Preserve the existing runtime floor optimization while preparing lights.
        }
        Capture("before");
        var report=new StringBuilder("Baseline: "+Folder+"/BeforeBake.unity\nOriginal meshes and original lighting settings retained.\n");
        var meshCopies=new Dictionary<Mesh,Mesh>();int receiverCount=0,probeReceivers=0;
        foreach(var r in All<MeshRenderer>())
        {
            if(!r.enabled||!r.gameObject.activeInHierarchy||Excluded(r.transform))continue;
            if(Movable(r)) {probeReceivers++;continue;}
            var filter=r.GetComponent<MeshFilter>();var mesh=filter?.sharedMesh;if(mesh==null)continue;
            if(mesh.uv2.Length==0)
            {
                if(!meshCopies.TryGetValue(mesh,out var copy))
                {
                    copy=UnityEngine.Object.Instantiate(mesh);copy.name=mesh.name+"_LightmapUV";
                    if(!Unwrapping.GenerateSecondaryUVSet(copy))throw new InvalidOperationException("UV unwrap failed: "+mesh.name);
                    AssetDatabase.CreateAsset(copy,Folder+"/Mesh_"+meshCopies.Count+".asset");meshCopies.Add(mesh,copy);
                }
                Undo.RecordObject(filter,"Assign lightmap UV mesh");filter.sharedMesh=copy;
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            }
            Undo.RecordObject(r.gameObject,"Prepare GI receiver");
            GameObjectUtility.SetStaticEditorFlags(r.gameObject,GameObjectUtility.GetStaticEditorFlags(r.gameObject)|StaticEditorFlags.ContributeGI);
            Undo.RecordObject(r,"Receive baked lighting");r.receiveGI=ReceiveGI.Lightmaps;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);receiverCount++;
        }
        foreach(var t in All<Terrain>().Where(t=>t.isActiveAndEnabled))
        {
            Undo.RecordObject(t.gameObject,"Prepare terrain lightmap receiver");
            GameObjectUtility.SetStaticEditorFlags(t.gameObject,GameObjectUtility.GetStaticEditorFlags(t.gameObject)|StaticEditorFlags.ContributeGI);
        }
        var lights=holder.GetComponentsInChildren<Light>(true).Where(l=>l.enabled&&!Excluded(l.transform)&&l.gameObject.activeInHierarchy&&l.type==LightType.Point).ToArray();
        foreach(var light in lights)
        {
            Undo.RecordObject(light,"Bake static asset light");light.lightmapBakeType=LightmapBakeType.Baked;
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            report.AppendLine("BAKED "+JavaHollowLightingAudit.PathOf(light.transform));
        }
        if(lights.Length==0)throw new InvalidOperationException("No active asset lights found.");
        var settings=UnityEngine.Object.Instantiate(Lightmapping.lightingSettings);settings.name="Java Hollow Asset Lighting";
        settings.bakedGI=true;settings.realtimeGI=false;settings.lightmapMaxSize=2048;
        AssetDatabase.CreateAsset(settings,Folder+"/AssetLighting.lighting");Lightmapping.lightingSettings=settings;
        var probeObject=new GameObject("Asset Lighting Probes");Undo.RegisterCreatedObjectUndo(probeObject,"Add baked lighting probes");
        var group=probeObject.AddComponent<LightProbeGroup>();var points=new HashSet<Vector3>();
        foreach(var light in lights)
        {
            var p=light.transform.position;
            // Sparse volumetric coverage around each light, shared after quantization.
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)foreach(float y in new[]{.3f,1.2f,2.2f,3.5f})
                points.Add(new Vector3(Mathf.Round((p.x+x*2)/.5f)*.5f,y,Mathf.Round((p.z+z*2)/.5f)*.5f));
        }
        group.probePositions=points.ToArray();
        report.AppendLine($"Static receivers={receiverCount}; skipped movable/scripted renderers={probeReceivers}; UV mesh copies={meshCopies.Count}; baked point lights={lights.Length}; probes={points.Count}");
        report.AppendLine("Existing runtime floor optimization setting preserved.");
        File.WriteAllText("Performance/AssetLighting/preparation.txt",report.ToString());
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Asset lighting prepared. Generate bake next; lights are Baked but illumination is not valid until completion.");
    }
    [MenuItem("Tools/Java Hollow/Bake Static Asset Lighting %&#F7")]
    public static void Bake()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning||!File.Exists("Performance/AssetLighting/preparation.txt"))throw new InvalidOperationException("Prepare in Edit Mode first.");
        Lightmapping.bakeCompleted-=Complete;Lightmapping.bakeCompleted+=Complete;
        if(!Lightmapping.BakeAsync())throw new InvalidOperationException("Unity did not start the bake.");
    }
    static void Complete()
    {
        Lightmapping.bakeCompleted-=Complete;
        var scene=SceneManager.GetActiveScene();var maps=LightmapSettings.lightmaps;
        var report=new StringBuilder($"Bake completed: {DateTime.Now:O}; lightmaps={maps.Length}; probes={LightmapSettings.lightProbes?.count??0}\n");
        foreach(var map in maps)report.AppendLine($"Color={AssetDatabase.GetAssetPath(map.lightmapColor)}; size={map.lightmapColor?.width}x{map.lightmapColor?.height}");
        foreach(var light in All<Light>().Where(l=>l.isActiveAndEnabled))report.AppendLine($"{JavaHollowLightingAudit.PathOf(light.transform)}: mode={light.lightmapBakeType}; isBaked={light.bakingOutput.isBaked}");
        File.WriteAllText("Performance/AssetLighting/bake-result.txt",report.ToString());
        if(maps.Length==0){Debug.LogError("No lightmaps generated; do not accept this bake.");return;}
        EditorSceneManager.SaveScene(scene);Capture("after");JavaHollowLightingAudit.Audit();
        Debug.Log("Asset lighting bake completed and saved; inspect before/after images and test HMD.");
    }
    [MenuItem("Tools/Java Hollow/Fix Terrain Tree Baked Receivers %&#F8")]
    public static void FixTreeReceivers()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new InvalidOperationException("Wait for Edit Mode and bake completion.");
        var paths=All<Terrain>().Where(t=>t.terrainData!=null).SelectMany(t=>t.terrainData.treePrototypes).Where(p=>p.prefab!=null).Select(p=>AssetDatabase.GetAssetPath(p.prefab)).Distinct().ToArray();
        foreach(var path in paths)
        {
            if(!path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))continue;
            var backup="Performance/AssetLighting/OriginalTreePrefabs/"+path;
            Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!File.Exists(backup))File.Copy(path,backup);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.receiveGI=ReceiveGI.LightProbes;
                    renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();
        File.WriteAllLines("Performance/AssetLighting/tree-receiver-prefabs.txt",paths);
        Bake();
    }
    static void Capture(string name)
    {
        var main=Camera.main;if(main==null)return;
        var go=new GameObject("Temporary lighting comparison camera"){hideFlags=HideFlags.HideAndDontSave};
        var camera=go.AddComponent<Camera>();camera.CopyFrom(main);camera.enabled=false;
        camera.transform.SetPositionAndRotation(main.transform.position,main.transform.rotation);
        camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
        var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();var old=RenderTexture.active;
        try
        {
            var request=new RenderPipeline.StandardRequest{destination=rt};RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            File.WriteAllBytes("Performance/AssetLighting/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        finally{RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
    [MenuItem("Tools/Java Hollow/Compare Shared Tree Probes %&#9")]
    public static void CompareTreeProbes()
    {
        var terrains=All<Terrain>().ToArray();var flags=terrains.Select(t=>t.bakeLightProbesForTrees).ToArray();
        try{foreach(var t in terrains){t.bakeLightProbesForTrees=false;t.Flush();}Capture("tree-shared-probes");}
        finally{for(int i=0;i<terrains.Length;i++){terrains[i].bakeLightProbesForTrees=flags[i];terrains[i].Flush();}}
    }
    [MenuItem("Tools/Java Hollow/Apply Shared Tree Probes")]
    public static void UseSharedTreeProbes()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new InvalidOperationException("Wait for Edit Mode and bake completion.");
        foreach(var terrain in All<Terrain>())
        {
            Undo.RecordObject(terrain,"Use scene probes for terrain trees");
            terrain.bakeLightProbesForTrees=false;terrain.Flush();EditorUtility.SetDirty(terrain);
            PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);
        }
        var scene=SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Capture("after");JavaHollowLightingAudit.Audit();
        Debug.Log("Baked asset lighting saved with shared tree probes. Ready for visual and HMD performance review.");
    }
}
