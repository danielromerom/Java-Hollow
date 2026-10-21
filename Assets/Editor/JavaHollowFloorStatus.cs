using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class JavaHollowFloorStatus
{
    static JavaHollowFloorStatus()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Capture;
        };
    }

    [MenuItem("Tools/Java Hollow/Record Floor Status %&#s")]
    public static void Capture()
    {
        var scene = SceneManager.GetActiveScene();
        var config = Resources.Load<JavaHollowBakedFloor>(JavaHollowBakedFloor.ResourceName);
        var report = new StringBuilder($"Scene={scene.path}; playing={EditorApplication.isPlaying}; bakedEnabled={config != null && config.useBakedFloor}\n");
        foreach(var camera in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)))
            report.AppendLine($"CAMERA {camera.name}: active={camera.isActiveAndEnabled}; position={camera.transform.position}; rotation={camera.transform.eulerAngles}; mask={camera.cullingMask}; near={camera.nearClipPlane}; far={camera.farClipPlane}");
        if(config!=null)foreach(var tile in config.tiles)
            report.AppendLine($"FLOOR sourceCenterHeight={tile.source.GetInterpolatedHeight(.5f,.5f)}; bakedCenterHeight={tile.baked.GetInterpolatedHeight(.5f,.5f)}; sourceSize={tile.source.size}; bakedSize={tile.baked.size}");
        foreach (var terrain in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Terrain>(true)))
        {
            var data = terrain.terrainData;
            if (data == null) continue;
            bool baked = config != null && config.tiles.Any(t => t.baked == data && t.material == terrain.materialTemplate);
            report.AppendLine($"{terrain.name}: active={terrain.isActiveAndEnabled}; baked={baked}; data={AssetDatabase.GetAssetPath(data)}; shader={terrain.materialTemplate?.shader?.name}; trees={data.treeInstanceCount}; instanced={terrain.drawInstanced}; pixelError={terrain.heightmapPixelError}; detailDistance={terrain.detailObjectDistance}; treeDistance={terrain.treeDistance}");
            if (terrain.isActiveAndEnabled)
                for (int index=0; index<data.treeInstances.Length; index++)
                {
                    var tree=data.treeInstances[index];
                    var prefab=data.treePrototypes[tree.prototypeIndex].prefab;
                    var position=terrain.transform.TransformPoint(Vector3.Scale(tree.position,data.size));
                    report.AppendLine($"  TREE index={index}; prefab={prefab?.name}; world={position.ToString("F3")}; width={tree.widthScale}; height={tree.heightScale}");
                }
            for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
            {
                long sum = 0;
                foreach (int value in data.GetDetailLayer(0,0,data.detailWidth,data.detailHeight,layer)) sum += value;
                report.AppendLine($"  detail {layer}: densitySum={sum}; prefab={AssetDatabase.GetAssetPath(data.detailPrototypes[layer].prototype)}");
            }
        }
        Directory.CreateDirectory("Performance");
        File.WriteAllText("Performance/floor-current-status.txt",report.ToString());
        Debug.Log("Floor status captured: Performance/floor-current-status.txt");
    }
}
