using UnityEngine;
using UnityEditor;

public class ApplyTerrainSettingsAll : MonoBehaviour
{
    [MenuItem("Tools/Apply Terrain Settings to All")]
    static void ApplySettings()
    {
        foreach (Terrain t in FindObjectsOfType<Terrain>())
        {
            var d = t.detailObjectDistance = 60f;
            t.treeDistance = 150f;
            t.basemapDistance = 100f;
            t.heightmapPixelError = 50f;
            t.treeBillboardDistance = 100f;
            t.treeMaximumFullLODCount = 2000;
        }
        Debug.Log("✅ Applied optimized terrain settings to all terrains.");
    }
}
