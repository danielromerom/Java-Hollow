using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public static class JavaHollowLightingAudit
{
    public static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    [MenuItem("Tools/Java Hollow/Audit Asset Lighting %&#l")]
    public static void Audit()
    {
        var scene=SceneManager.GetActiveScene();
        var roots=scene.GetRootGameObjects();
        var report=new StringBuilder($"Scene={scene.path}; playing={EditorApplication.isPlaying}; pipeline={GraphicsSettings.currentRenderPipeline?.name}; quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}\n");
        report.AppendLine($"LightingSettings={AssetDatabase.GetAssetPath(Lightmapping.lightingSettings)}; lightingData={AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset)}; lightmaps={LightmapSettings.lightmaps.Length}; probeGroups={roots.SelectMany(r=>r.GetComponentsInChildren<LightProbeGroup>(true)).Count()}\n");
        foreach(var root in roots)
        {
            report.AppendLine($"ROOT {root.name}: active={root.activeSelf}");
            if(root.name=="IN SCENE ASSETS")foreach(Transform child in root.transform)report.AppendLine($" ASSET {child.name}: activeSelf={child.gameObject.activeSelf}; lights={child.GetComponentsInChildren<Light>(true).Length}; renderers={child.GetComponentsInChildren<Renderer>(true).Length}");
        }
        foreach(var light in roots.SelectMany(r=>r.GetComponentsInChildren<Light>(true)))
        {
            report.AppendLine($"LIGHT {PathOf(light.transform)}; active={light.isActiveAndEnabled}; self={light.enabled}; type={light.type}; bake={light.lightmapBakeType}; intensity={light.intensity}; range={light.range}; shadows={light.shadows}; color={light.color}; pos={light.transform.position}");
            foreach(var behaviour in light.GetComponentsInParent<MonoBehaviour>(true))report.AppendLine($" SCRIPT {behaviour?.GetType().FullName}; enabled={behaviour?.enabled}");
        }
        foreach(var terrain in roots.SelectMany(r=>r.GetComponentsInChildren<Terrain>(true)))
            report.AppendLine($"TERRAIN {PathOf(terrain.transform)}; active={terrain.isActiveAndEnabled}; data={AssetDatabase.GetAssetPath(terrain.terrainData)}; GI={GameObjectUtility.GetStaticEditorFlags(terrain.gameObject)}; shader={terrain.materialTemplate?.shader?.name}; lightmap={terrain.lightmapIndex}");
        foreach(var t in roots.SelectMany(r=>r.GetComponentsInChildren<Terrain>(true)).Where(t=>t.terrainData!=null))
        {
            var data=t.terrainData;
            for(int i=0;i<data.treePrototypes.Length;i++)
            {
                var p=data.treePrototypes[i].prefab;
                report.AppendLine($"TREE {t.name} prototype={i}; prefab={AssetDatabase.GetAssetPath(p)}; count={data.treeInstances.Count(x=>x.prototypeIndex==i)}");
                if(p!=null)foreach(var r in p.GetComponentsInChildren<Renderer>(true))report.AppendLine($" TREE RENDERER {r.name}; probes={r.lightProbeUsage}; shaders={string.Join("|",r.sharedMaterials.Select(m=>m==null?"null":m.shader.name))}");
            }
        }
        foreach(var renderer in roots.SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true)))
        {
            report.AppendLine($"RECEIVER {PathOf(renderer.transform)}; lightmap={renderer.lightmapIndex}; probes={renderer.lightProbeUsage}; mesh={AssetDatabase.GetAssetPath(renderer.GetComponent<MeshFilter>()?.sharedMesh)}");
            var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;
            report.AppendLine($"MESH {PathOf(renderer.transform)}; active={(renderer.enabled && renderer.gameObject.activeInHierarchy)}; GI={GameObjectUtility.GetStaticEditorFlags(renderer.gameObject)}; receive={renderer.receiveGI}; tris={(mesh==null?0:mesh.triangles.Length/3)}; uv2={(mesh==null?0:mesh.uv2.Length)}; bounds={renderer.bounds}; materials={string.Join("|",renderer.sharedMaterials.Select(m=>m==null?"null":m.name+":"+m.shader.name))}; scripts={string.Join("|",renderer.GetComponentsInParent<MonoBehaviour>(true).Where(m=>m!=null).Select(m=>m.GetType().Name))}");
        }
        Directory.CreateDirectory("Performance");File.WriteAllText("Performance/lighting-current-audit.txt",report.ToString());
        Debug.Log("Asset lighting audit saved.");
    }
}
