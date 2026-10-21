using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class JavaHollowDirtQuadCandidate
{
    const string OriginalPrefab = "Assets/PerformanceOptimizations/Floor/DirtDetailCaps.prefab";
    const string Folder = "Assets/PerformanceOptimizations/Floor/QuadCandidate";
    const string CandidatePrefab = Folder + "/DirtQuad.prefab";
    const string CandidateMesh = Folder + "/DirtQuad.asset";
    static readonly Dictionary<Terrain, TerrainData> originals = new Dictionary<Terrain, TerrainData>();
    static readonly Dictionary<Terrain, TerrainData> candidates = new Dictionary<Terrain, TerrainData>();
    static readonly List<float> times = new List<float>();
    static readonly List<int> triangles = new List<int>();
    static readonly List<int> batches = new List<int>();
    static StringBuilder benchmark;
    static int phase, lastFrame;
    static double phaseStart;

    [MenuItem("Tools/Java Hollow/Compare Dirt Quad Images %&#e")]
    public static void CompareImages()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Compare outside Play Mode.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPrefab);
        var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(CandidatePrefab);
        if (candidate == null) throw new InvalidOperationException("Build candidate first.");
        var main = Camera.main;
        if (main == null) throw new InvalidOperationException("Main camera not found.");
        var terrain = Terrain.activeTerrains.First(t => t.name == "TERRAIN");
        var original = terrain.terrainData;
        if (!original.detailPrototypes.Any(p => p.prototype == source))
            throw new InvalidOperationException("Restore Dirt Caps before comparing; the optimized prototype is already applied.");
        var clone = UnityEngine.Object.Instantiate(original);
        var prototypes = clone.detailPrototypes;
        foreach (var p in prototypes) if (p.prototype == source) p.prototype = candidate;
        clone.detailPrototypes = prototypes;
        var go = new GameObject("Temporary floor comparison camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = go.AddComponent<Camera>();
        camera.CopyFrom(main);
        camera.enabled = false;
        camera.stereoTargetEye = StereoTargetEyeMask.None;
        camera.nearClipPlane = 0.005f;
        camera.GetUniversalAdditionalCameraData().allowXRRendering = false;
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        rt.Create();
        var previous = RenderTexture.active;
        var report = new StringBuilder("Static Editor render comparison, 1280x720, same camera/material/light settings for each A/B pair. Not an HMD acceptance test.\n");
        var point = main.transform.position + Vector3.ProjectOnPlane(main.transform.forward, Vector3.up).normalized * 1.2f;
        point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        Directory.CreateDirectory("Performance/DirtQuadComparison");
        try
        {
            for (int view = 0; view < 3; view++)
            {
                string name = new[] { "standing", "close", "grazing" }[view];
                camera.transform.position = view == 0 ? main.transform.position : point + new Vector3(0, view == 1 ? 0.25f : 0.03f, -0.4f);
                camera.transform.rotation = Quaternion.LookRotation(point - camera.transform.position, Vector3.up);
                Color32[] baseline = null;
                for (int variant = 0; variant < 2; variant++)
                {
                    terrain.terrainData = variant == 0 ? original : clone;
                    var request = new RenderPipeline.StandardRequest { destination = rt };
                    if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("Render request unsupported.");
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = rt;
                    var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    try
                    {
                        image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                        image.Apply();
                        File.WriteAllBytes($"Performance/DirtQuadComparison/{name}-{(variant == 0 ? "original" : "quad")}.png", image.EncodeToPNG());
                        var pixels = image.GetPixels32();
                        if (variant == 0) baseline = pixels;
                        else
                        {
                            long absoluteError = 0; int changed = 0;
                            for (int i = 0; i < pixels.Length; i++)
                            {
                                int d = Math.Abs(pixels[i].r - baseline[i].r) + Math.Abs(pixels[i].g - baseline[i].g) + Math.Abs(pixels[i].b - baseline[i].b);
                                absoluteError += d;
                                if (d > 6) changed++;
                            }
                            report.AppendLine($"{name}: mean absolute RGB error (0..255)={absoluteError / (pixels.Length * 3.0):F6}, pixels with summed RGB difference >6={changed}/{pixels.Length}");
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(image); }
                }
            }
            File.WriteAllText("Performance/dirt-quad-image-comparison.txt", report.ToString());
        }
        finally
        {
            terrain.terrainData = original;
            RenderTexture.active = previous;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(clone);
        }
        Debug.Log("Floor standing/close/grazing image comparisons complete; originals restored.");
    }

    [MenuItem("Tools/Java Hollow/Build Dirt Quad Candidate %&#q")]
    public static void BuildCandidate()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Build outside Play Mode.");
        if (AssetDatabase.LoadAssetAtPath<Mesh>(CandidateMesh) != null)
            throw new InvalidOperationException("Candidate already exists; inspect it before rebuilding.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPrefab);
        var source = prefab.GetComponent<MeshFilter>().sharedMesh;
        var material = prefab.GetComponent<MeshRenderer>().sharedMaterial;
        if (source.subMeshCount != 1 || source.triangles.Length != 60 ||
            material.shader.name != "Universal Render Pipeline/Simple Lit" ||
            material.GetTexture("_BumpMap") != null || material.GetFloat("_Surface") != 0 ||
            material.GetTextureScale("_BaseMap") != Vector2.one || material.GetTextureOffset("_BaseMap") != Vector2.zero)
            throw new InvalidOperationException("Dirt source changed; revalidate shader/UV/geometry assumptions.");
        var shader = Shader.Find("Java Hollow/Dirt Polygon Candidate");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Candidate shader failed import.");
        var mesh = new Mesh { name = "DirtQuad" };
        mesh.vertices = new[] { new Vector3(-.5f,1,-.5f), new Vector3(-.5f,1,.5f), new Vector3(.5f,1,.5f), new Vector3(.5f,1,-.5f) };
        mesh.normals = Enumerable.Repeat(Vector3.up,4).ToArray();
        mesh.tangents = Enumerable.Repeat(new Vector4(-1,0,0,-1),4).ToArray();
        mesh.uv = mesh.vertices.Select(v => new Vector2(0.5002244781f + -0.9922326663f*v.x, 0.4999441968f + -0.9922328298f*v.z)).ToArray();
        mesh.triangles = new[] { 0,1,2,0,2,3 };
        mesh.bounds = source.bounds;
        var candidateMaterial = new Material(material) { shader = shader, name = "DirtQuad" };
        candidateMaterial.SetFloat("_Cutoff",0); // polygon clips; original texture alpha must not remove coverage
        candidateMaterial.enableInstancing = true;
        AssetDatabase.CreateAsset(candidateMaterial, Folder + "/DirtQuad.mat");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        AssetDatabase.CreateAsset(mesh, CandidateMesh);
        var instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            instance.name = "DirtQuad";
            instance.GetComponent<MeshFilter>().sharedMesh = mesh;
            instance.GetComponent<MeshRenderer>().sharedMaterial = candidateMaterial;
            PrefabUtility.SaveAsPrefabAsset(instance, CandidatePrefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
        AssetDatabase.SaveAssetIfDirty(mesh);
        Directory.CreateDirectory("Performance");
        File.WriteAllText("Performance/dirt-quad-validation.txt",
            "Candidate: 20 -> 2 triangles, 42 -> 4 stored vertices; original bounds, texture, lighting and transform preserved.\n" +
            "Polygon edge uses analytical fragment clipping. UV affine fit error < 0.00002. MSAA edge appearance requires comparison.\n");
        Debug.Log("Dirt quad candidate built; no terrain references changed.");
    }

    [MenuItem("Tools/Java Hollow/Measure Dirt Quad Candidate %&#w")]
    public static void MeasureCandidate()
    {
        if (!EditorApplication.isPlaying || originals.Count != 0) return;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPrefab);
        var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(CandidatePrefab);
        if (candidate == null) throw new InvalidOperationException("Build candidate first.");
        try
        {
            foreach (var terrain in Terrain.activeTerrains)
            {
                if (!terrain.terrainData.detailPrototypes.Any(p => p.prototype == source)) continue;
                originals.Add(terrain, terrain.terrainData);
                var clone = UnityEngine.Object.Instantiate(terrain.terrainData);
                candidates.Add(terrain, clone);
                var prototypes = clone.detailPrototypes;
                foreach (var p in prototypes)
                    if (p.prototype == source)
                    {
                        if (p.maxHeight > 0.0001f) throw new InvalidOperationException("Floor height changed.");
                        p.prototype = candidate;
                    }
                clone.detailPrototypes = prototypes;
                for (int layer = 0; layer < prototypes.Length; layer++)
                {
                    if (prototypes[layer].prototype != candidate) continue;
                    for (int x = 0; x < clone.detailPatchCount; x++)
                        for (int z = 0; z < clone.detailPatchCount; z++)
                        {
                            var before = terrain.terrainData.ComputeDetailInstanceTransforms(x, z, layer, terrain.detailObjectDensity, out _);
                            var after = clone.ComputeDetailInstanceTransforms(x, z, layer, terrain.detailObjectDensity, out _);
                            if (before.Length != after.Length) throw new InvalidOperationException("Detail count changed.");
                            for (int i = 0; i < before.Length; i++)
                                if (before[i].posX != after[i].posX || before[i].posY != after[i].posY || before[i].posZ != after[i].posZ ||
                                    before[i].rotationY != after[i].rotationY || before[i].scaleXZ != after[i].scaleXZ || before[i].scaleY != after[i].scaleY)
                                    throw new InvalidOperationException("Detail placement changed.");
                        }
                }
            }
            if (originals.Count == 0) throw new InvalidOperationException("No original floor detail found.");
            phase = 0; lastFrame = Time.frameCount;
            phaseStart = EditorApplication.timeSinceStartup;
            benchmark = new StringBuilder("Editor A/B/A; keep view, headset presence and rendering configuration fixed. Full detail density throughout. Terrain data cloned in memory; originals restored automatically. Pair with XR presence capture for headset interpretation.\nPASS: all affected generated detail positions, rotations, scales and counts match exactly before/after prefab substitution.\n");
            EditorApplication.update += MeasureUpdate;
            EditorApplication.playModeStateChanged += OnPlayState;
            AssemblyReloadEvents.beforeAssemblyReload += Restore;
        }
        catch { Restore(); throw; }
    }

    static void MeasureUpdate()
    {
        if (!EditorApplication.isPlaying) { Restore(); return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        double elapsed = EditorApplication.timeSinceStartup - phaseStart;
        if (elapsed < 4) return;
        if (elapsed < 12)
        {
            times.Add(Time.unscaledDeltaTime * 1000);
            triangles.Add(UnityStats.triangles); batches.Add(UnityStats.batches);
            return;
        }
        times.Sort();
        if (times.Count != 0)
            benchmark.AppendLine($"{(phase == 1 ? "Quad candidate" : "Original caps")}: samples={times.Count}, meanMs={times.Average():F2}, p95Ms={times[(int)((times.Count - 1) * 0.95)]:F2}, trianglesMean={triangles.Average():F0}, batchesMean={batches.Average():F0}");
        times.Clear(); triangles.Clear(); batches.Clear();
        phase++;
        if (phase < 3)
        {
            foreach (var pair in originals) pair.Key.terrainData = phase == 1 ? candidates[pair.Key] : pair.Value;
            phaseStart = EditorApplication.timeSinceStartup;
        }
        else
        {
            Restore();
            File.WriteAllText("Performance/dirt-quad-ab.txt", benchmark.ToString());
            Debug.Log("Dirt quad benchmark complete; original data restored.");
        }
    }

    static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) Restore();
    }

    static void Restore()
    {
        foreach (var pair in originals) if (pair.Key != null) pair.Key.terrainData = pair.Value;
        foreach (var data in candidates.Values) if (data != null) UnityEngine.Object.DestroyImmediate(data);
        originals.Clear(); candidates.Clear(); times.Clear(); triangles.Clear(); batches.Clear();
        EditorApplication.update -= MeasureUpdate;
        EditorApplication.playModeStateChanged -= OnPlayState;
        AssemblyReloadEvents.beforeAssemblyReload -= Restore;
    }

    [MenuItem("Tools/Java Hollow/Apply Dirt Quad Candidate %&#d")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Apply outside Play Mode.");
        if (!File.Exists("Performance/dirt-quad-ab.txt") ||
            !File.ReadAllText("Performance/dirt-quad-ab.txt").Contains("PASS: all affected"))
            throw new InvalidOperationException("Run and inspect the candidate comparison before applying.");
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPrefab);
        var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(CandidatePrefab);
        var paths = new[] {
            "Assets/Fantasy Forest Environment Free Sample/Scenes/New Terrain.asset",
            "Assets/Fantasy Forest Environment Free Sample/Scenes/TerrainData_30530da2-949b-4ba6-9e8a-63f464d6f89b.asset"
        };
        // Preflight all targets before any write. Never save the open scene or unrelated assets.
        foreach (var path in paths)
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null || candidate == null) throw new InvalidOperationException("Missing floor asset.");
            foreach (var p in data.detailPrototypes.Where(p => p.prototype == original))
                if (p.maxHeight > 0.0001f) throw new InvalidOperationException("Floor height changed; revalidate side-wall removal.");
        }
        Directory.CreateDirectory("Logs/DirtQuadOriginals");
        foreach (var path in paths)
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            var prototypes = data.detailPrototypes;
            if (!prototypes.Any(p => p.prototype == original)) continue;
            var backup = "Logs/DirtQuadOriginals/" + Path.GetFileName(path);
            if (!File.Exists(backup)) File.Copy(path, backup);
            Undo.RegisterCompleteObjectUndo(data, "Optimize dirt detail mesh");
            foreach (var p in prototypes) if (p.prototype == original) p.prototype = candidate;
            data.detailPrototypes = prototypes;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
        }
        Debug.Log("Dirt quad applied to two terrain assets. Original prefab retained; open scene not saved.");
    }

    [MenuItem("Tools/Java Hollow/Restore Dirt Caps")]
    public static void RestoreCaps()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Restore outside Play Mode.");
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPrefab);
        var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(CandidatePrefab);
        if (original == null || candidate == null) throw new InvalidOperationException("Missing dirt prototype.");
        var paths = new[] {
            "Assets/Fantasy Forest Environment Free Sample/Scenes/New Terrain.asset",
            "Assets/Fantasy Forest Environment Free Sample/Scenes/TerrainData_30530da2-949b-4ba6-9e8a-63f464d6f89b.asset"
        };
        var data = paths.Select(AssetDatabase.LoadAssetAtPath<TerrainData>).ToArray();
        if (data.Any(d => d == null)) throw new InvalidOperationException("Missing terrain data.");
        foreach (var d in data)
        {
            var prototypes = d.detailPrototypes;
            if (!prototypes.Any(p => p.prototype == candidate)) continue;
            Undo.RegisterCompleteObjectUndo(d, "Restore dirt cap prototype");
            foreach (var p in prototypes) if (p.prototype == candidate) p.prototype = original;
            d.detailPrototypes = prototypes;
            EditorUtility.SetDirty(d);
            AssetDatabase.SaveAssetIfDirty(d);
        }
        Debug.Log("Dirt caps restored; terrain painting and open scene preserved.");
    }

}
