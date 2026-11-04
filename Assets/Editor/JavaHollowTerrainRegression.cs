using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class JavaHollowTerrainRegression
{
    [MenuItem("Tools/Restore Previous Tree Placement",false,0)]
    public static void Restore()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play Mode first.");
        var rows=File.ReadAllLines("Performance/TerrainRegressionBackup/paths.txt");
        foreach(int index in new[]{1,3})
        {
            var p=rows[index].Split('|');var current=AssetDatabase.LoadAssetAtPath<TerrainData>(p[0]);var original=AssetDatabase.LoadAssetAtPath<TerrainData>(p[1]);
            Undo.RegisterCompleteObjectUndo(current,"Restore pre-edit tree placements");
            current.SetTreeInstances(original.treeInstances,false);EditorUtility.SetDirty(current);AssetDatabase.SaveAssetIfDirty(current);
        }
        foreach(var t in Terrain.activeTerrains)t.Flush();
        Audit();Debug.Log("Restored two terrain tree lists: 13 newly painted willows undone. Lighting and floor optimization retained.");
    }
    [MenuItem("Tools/Java Hollow/Audit Terrain Regression")]
    public static void Audit()
    {
        if(EditorApplication.isPlaying)return;
        var report=new StringBuilder();
        foreach(var line in File.ReadAllLines("Performance/TerrainRegressionBackup/paths.txt"))
        {
            var paths=line.Split('|');
            foreach(var path in paths)
            {
                var t=AssetDatabase.LoadAssetAtPath<TerrainData>(path);if(t==null)continue;
                report.AppendLine($"DATA {path}; trees={t.treeInstanceCount}; size={t.size}; heightRes={t.heightmapResolution}; detailRes={t.detailResolution}");
                for(int i=0;i<t.treePrototypes.Length;i++)report.AppendLine($" PROTOTYPE {i}: {AssetDatabase.GetAssetPath(t.treePrototypes[i].prefab)}; count={t.treeInstances.Count(x=>x.prototypeIndex==i)}");
                for(int i=0;i<t.treeInstances.Length;i++){var x=t.treeInstances[i];report.AppendLine($" TREE {i}: prototype={x.prototypeIndex}; pos={x.position.ToString("F5")}; scale={x.widthScale},{x.heightScale}; rotation={x.rotation}");}
            }
            var a=AssetDatabase.LoadAssetAtPath<TerrainData>(paths[0]);var b=AssetDatabase.LoadAssetAtPath<TerrainData>(paths[1]);
            if(a!=null&&b!=null){var sa=new SerializedObject(a);var sb=new SerializedObject(b);var p=sa.GetIterator();while(p.Next(true)){if(p.propertyType==SerializedPropertyType.Generic)continue;var q=sb.FindProperty(p.propertyPath);if(q!=null&&!SerializedProperty.DataEquals(p,q))report.AppendLine(" DIFF "+p.propertyPath);}}
        }
        var config=Resources.Load<JavaHollowBakedFloor>(JavaHollowBakedFloor.ResourceName);
        foreach(var tile in config.tiles){report.AppendLine("FLOOR TREE COMPARISON");foreach(var t in new[]{tile.source,tile.baked}){report.AppendLine($" {AssetDatabase.GetAssetPath(t)} count={t.treeInstanceCount}");for(int i=0;i<t.treeInstances.Length;i++){var x=t.treeInstances[i];report.AppendLine($" {i}: {AssetDatabase.GetAssetPath(t.treePrototypes[x.prototypeIndex].prefab)} pos={x.position.ToString("F5")}");}}}
        File.WriteAllText("Performance/TerrainRegressionBackup/audit.txt",report.ToString());
    }
}
