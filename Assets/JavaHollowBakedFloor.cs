using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Built assets, selected only for the participant scene. No per-frame work.
public sealed class JavaHollowBakedFloor : ScriptableObject
{
    [Serializable] public struct Tile
    {
        public TerrainData source;
        public TerrainData baked;
        public Material material;
    }
    public bool useBakedFloor;
    public Tile[] tiles;
    public const string ResourceName = "JavaHollowBakedFloor";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.path != "Assets/Terrain_Demo_For_Show.unity" && scene.path != "Assets/PerformanceOptimizations/StandaloneCheck.unity") return;
        var settings = Resources.Load<JavaHollowBakedFloor>(ResourceName);
        if (settings == null || !settings.useBakedFloor) return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
                foreach (var tile in settings.tiles)
                    if (terrain.terrainData == tile.source && tile.baked != null && tile.material != null)
                    {
                        terrain.terrainData = tile.baked;
                        terrain.materialTemplate = tile.material;
                        // TerrainCollider and existing audio/gameplay components remain on the original object.
                        break;
                    }
    }
}
