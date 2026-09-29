using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Explicit, read-only audit. Never runs in a player or automatically on import.
public static class JavaHollowPerformanceAudit
{
    const string ScenePath = "Assets/Terrain_Demo_For_Show.unity";

    [MenuItem("Tools/Java Hollow/Audit Open Scene Performance")]
    public static void AuditOpenScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var objects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
        var components = objects.SelectMany(g => g.GetComponents<Component>()).Where(c => c != null).ToArray();
        var report = new StringBuilder();
        Action<string> line = s => report.AppendLine(s);
        line("Scene: " + scene.path);
        line("Unity: " + Application.unityVersion + "; quality: " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
        line("Editor inventory; NOT runtime visibility, draw calls, GPU time, or FPS. Includes inactive objects unless labelled active.");
        line("Objects: " + objects.Length + "; active: " + objects.Count(g => g.activeInHierarchy));
        line("Missing scripts: " + objects.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount));
        foreach (var group in components.GroupBy(c => c.GetType().Name).OrderByDescending(g => g.Count()))
            line("Component " + group.Key + ": " + group.Count());
        foreach (var c in components.OfType<Camera>())
        {
            var data = c.GetComponent<UniversalAdditionalCameraData>();
            line($"Camera {PathOf(c)} active={c.isActiveAndEnabled} target={c.targetTexture} stereo={c.stereoTargetEye} near={c.nearClipPlane} far={c.farClipPlane} occlusion={c.useOcclusionCulling} post={data?.renderPostProcessing} stack={data?.renderType}");
        }
        foreach (var l in components.OfType<Light>())
            line($"Light {PathOf(l)} active={l.isActiveAndEnabled} type={l.type} mode={l.lightmapBakeType} shadows={l.shadows} range={l.range} intensity={l.intensity}");
        foreach (var p in components.OfType<ReflectionProbe>())
            line($"Reflection {PathOf(p)} active={p.isActiveAndEnabled} mode={p.mode} refresh={p.refreshMode} slicing={p.timeSlicingMode} resolution={p.resolution}");
        var renderers = components.OfType<Renderer>().ToArray();
        line("Active enabled renderers: " + renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy));
        line("Batching-static renderers: " + renderers.Count(r => GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.BatchingStatic)));
        line("Shadow-casting renderers: " + renderers.Count(r => r.shadowCastingMode != ShadowCastingMode.Off));
        var meshes = components.OfType<MeshFilter>().Where(f => f.sharedMesh != null).ToArray();
        foreach (var g in meshes.GroupBy(f => f.sharedMesh).OrderByDescending(g => (long)g.Key.vertexCount * g.Count()).Take(30))
            line($"Mesh {AssetDatabase.GetAssetPath(g.Key)} / {g.Key.name}: instances={g.Count()} verticesEach={g.Key.vertexCount} indicesEach={Enumerable.Range(0, g.Key.subMeshCount).Sum(i => (long)g.Key.GetIndexCount(i))} readable={g.Key.isReadable}");
        foreach (var g in renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).GroupBy(m => m).OrderByDescending(g => g.Count()))
            line($"Material {AssetDatabase.GetAssetPath(g.Key)} refs={g.Count()} shader={g.Key.shader.name} queue={g.Key.renderQueue} instancing={g.Key.enableInstancing}");
        foreach (var p in components.OfType<ParticleSystem>())
        {
            var r = p.GetComponent<ParticleSystemRenderer>();
            line($"Particle {PathOf(p)} active={p.gameObject.activeInHierarchy} max={p.main.maxParticles} loop={p.main.loop} space={p.main.simulationSpace} culling={p.main.cullingMode} emission={p.emission.rateOverTime.constantMax} lifetime={p.main.startLifetime.constantMax} collision={p.collision.enabled} trigger={p.trigger.enabled} trails={p.trails.enabled} noise={p.noise.enabled} render={r?.renderMode} instancing={r?.enableGPUInstancing} bounds={r?.localBounds}");
        }
        foreach (var t in components.OfType<Terrain>())
        {
            var d = t.terrainData;
            line($"Terrain {PathOf(t)} active={t.isActiveAndEnabled} instanced={t.drawInstanced} pixelError={t.heightmapPixelError} detailDistance={t.detailObjectDistance} density={t.detailObjectDensity} treeDistance={t.treeDistance} billboard={t.treeBillboardDistance} maxFullLOD={t.treeMaximumFullLODCount} trees={d.treeInstanceCount} detailResolution={d.detailResolution} heights={d.heightmapResolution}");
            foreach (var p in d.detailPrototypes) line($" Detail mesh={p.prototype} texture={p.prototypeTexture} instancing={p.useInstancing} mode={p.renderMode}");
            foreach (var p in d.treePrototypes) line($" Tree prefab={AssetDatabase.GetAssetPath(p.prefab)} LODGroups={p.prefab.GetComponentsInChildren<LODGroup>(true).Length}");
        }
        foreach (var v in components.OfType<Volume>())
            line($"Volume {PathOf(v)} active={v.isActiveAndEnabled} global={v.isGlobal} weight={v.weight} profile={AssetDatabase.GetAssetPath(v.sharedProfile)} effects={string.Join(",", v.sharedProfile == null ? Array.Empty<string>() : v.sharedProfile.components.Where(c => c.active).Select(c => c.name).ToArray())}");
        foreach (var a in components.OfType<AudioSource>()) line($"Audio {PathOf(a)} active={a.isActiveAndEnabled} clip={AssetDatabase.GetAssetPath(a.clip)} loop={a.loop} spatial={a.spatialBlend}");
        foreach (var b in components.OfType<MonoBehaviour>().Where(b => b.GetType().Assembly.GetName().Name == "Assembly-CSharp"))
            line($"Script {PathOf(b)} type={b.GetType().Name} active={b.isActiveAndEnabled}");
        var dependencies = AssetDatabase.GetDependencies(scene.path, true);
        foreach (var path in dependencies)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            var tex = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (tex != null && (tex.width >= 2048 || tex.height >= 2048 || importer.isReadable))
                line($"Texture {path} size={tex.width}x{tex.height} bytesEditor={Profiler.GetRuntimeMemorySizeLong(tex)} readable={importer.isReadable} mipmaps={importer.mipmapEnabled} streaming={importer.streamingMipmaps} compression={importer.textureCompression}");
        }
        Directory.CreateDirectory("Performance");
        File.WriteAllText("Performance/scene-inventory.txt", report.ToString());
        Debug.Log("Java Hollow audit written to Performance/scene-inventory.txt");
    }

    public static void AuditBuildScene()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        AuditOpenScene();
    }

    public static void BuildWindows()
    {
        ValidateLiquidTransfers();
        ValidateCandleParticleBudget();
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = "Builds/PerformanceValidation/JavaHollow.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        Debug.Log($"Java Hollow build: {result.summary.result}, errors={result.summary.totalErrors}, bytes={result.summary.totalSize}");
        if (result.summary.result != BuildResult.Succeeded) throw new Exception("Windows validation build failed");
    }

    [MenuItem("Tools/Java Hollow/Validate Liquid Transfers")]
    public static void ValidateLiquidTransfers()
    {
        var sourceObject = new GameObject("Liquid regression source");
        var targetObject = new GameObject("Liquid regression target");
        var infiniteObject = new GameObject("Liquid regression infinite");
        try
        {
            var source = sourceObject.AddComponent<LiquidContainer>();
            var target = targetObject.AddComponent<LiquidContainer>();
            source.AddLiquid(0.6f, LiquidType.Milk);
            source.AddLiquid(0.4f, LiquidType.Espresso);
            var originalColor = source.GetBlendedColor();
            source.PourTo(target, 0.25f);
            Near(source.GetTotalVolume(), 0.75f, "source conservation");
            Near(target.GetTotalVolume(), 0.25f, "target conservation");
            Near(target.liquidVolumes[LiquidType.Milk], 0.15f, "milk ratio");
            Near(target.liquidVolumes[LiquidType.Espresso], 0.1f, "espresso ratio");
            Near(target.GetBlendedColor().r, originalColor.r, "blend");
            target.RemoveProportionalLiquid(0.1f);
            Near(target.GetTotalVolume(), 0.15f, "sip");
            target.RemoveProportionalLiquid(1f);
            Near(target.GetTotalVolume(), 0f, "empty and key removal");
            source.PourTo(target, source.GetTotalVolume());
            Near(source.GetTotalVolume(), 0f, "full transfer");
            target.AddLiquid(2f, LiquidType.Water);
            Near(target.GetTotalVolume(), 1f, "capacity clamp");
            var infinite = infiniteObject.AddComponent<InfiniteLiquidContainer>();
            infinite.liquidVolumes.Clear();
            infinite.liquidVolumes[LiquidType.Water] = infinite.maxCapacity;
            source.liquidVolumes.Clear();
            infinite.PourTo(source, 0.2f);
            Near(infinite.GetTotalVolume(), 1f, "infinite source");
            Near(source.GetTotalVolume(), 0.2f, "infinite transfer");
            source.RemoveProportionalLiquid(1f);
            // Warm the buffers and dictionary collections, then exercise steady-state transfers.
            for (int i = 0; i < 10; i++) { infinite.PourTo(source, 0.01f); source.RemoveProportionalLiquid(1f); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { infinite.PourTo(source, 0.01f); source.RemoveProportionalLiquid(1f); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (allocated != 0) throw new Exception("Steady-state liquid math allocated " + allocated + " bytes");
            Directory.CreateDirectory("Performance");
            File.WriteAllText("Performance/liquid-validation.txt", "PASS: finite mixture, conservation, blend, sip, empty/key removal, capacity clamp, infinite transfer.\nPASS: 1000 warmed transfer/removal cycles allocated 0 managed bytes on the calling thread (liquidVisual=null; rendering not measured).\n");
            Debug.Log("Java Hollow liquid regression checks passed");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceObject);
            UnityEngine.Object.DestroyImmediate(targetObject);
            UnityEngine.Object.DestroyImmediate(infiniteObject);
        }
    }

    public static void ValidateCandleParticleBudget()
    {
        const string prefabPath = "Assets/Piloto Studio/Campfire And Torches Pack/Prefabs/SM_props_candle_01_AmbienceFX.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var original = prefab.GetComponentsInChildren<ParticleSystem>(true).Single(p => p.name == "SmokyRibbon_BiggerBackdrop (1)");
        var main = original.main;
        var emission = original.emission;
        // Validate the authored upper bound before testing a smaller capacity.
        if (main.startLifetime.mode != ParticleSystemCurveMode.Constant || main.startLifetime.constant > 2f ||
            emission.rateOverTime.mode != ParticleSystemCurveMode.Constant || emission.rateOverTime.constant > 30f ||
            emission.rateOverDistance.mode != ParticleSystemCurveMode.Constant || emission.rateOverDistance.constant != 0f || emission.burstCount != 0 ||
            original.subEmitters.enabled || main.ringBufferMode != ParticleSystemRingBufferMode.Disabled)
            throw new Exception("Candle authoring changed; reassess the particle budget before applying it.");
        var report = new StringBuilder();
        foreach (uint seed in new uint[] { 1, 12345, 987654 })
        {
            var a = UnityEngine.Object.Instantiate(original);
            var b = UnityEngine.Object.Instantiate(original);
            try
            {
                a.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                b.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                a.useAutoRandomSeed = b.useAutoRandomSeed = false;
                a.randomSeed = b.randomSeed = seed;
                var am = a.main; am.maxParticles = 100000;
                var bm = b.main; bm.maxParticles = 128;
                var ap = new ParticleSystem.Particle[128];
                var bp = new ParticleSystem.Particle[128];
                int peak = 0;
                for (int frame = 0; frame < 1800; frame++)
                {
                    a.Simulate(1f / 60f, false, frame == 0, false);
                    b.Simulate(1f / 60f, false, frame == 0, false);
                    int count = a.GetParticles(ap);
                    peak = Math.Max(peak, a.particleCount);
                    if (a.particleCount >= 128 || a.particleCount != b.particleCount || count != b.GetParticles(bp))
                        throw new Exception("Particle count changed under reduced capacity");
                    for (int i = 0; i < count; i++)
                        if (ap[i].randomSeed != bp[i].randomSeed || ap[i].position != bp[i].position ||
                            ap[i].remainingLifetime != bp[i].remainingLifetime || !ap[i].startColor.Equals(bp[i].startColor))
                            throw new Exception("Particle state changed under reduced capacity");
                }
                report.AppendLine($"PASS: seed={seed}, 1800 steps at 1/60s, capacity 100000 vs 128, peak={peak}; counts, seeds, positions, lifetimes and start colors match.");
            }
            finally { UnityEngine.Object.DestroyImmediate(a.gameObject); UnityEngine.Object.DestroyImmediate(b.gameObject); }
        }
        Directory.CreateDirectory("Performance");
        File.WriteAllText("Performance/particle-validation.txt", report.ToString());
        Debug.Log("Java Hollow candle particle budget checks passed");
    }

    static void Near(float actual, float expected, string label)
    {
        if (Mathf.Abs(actual - expected) > 0.00001f) throw new Exception(label + ": " + actual + " != " + expected);
    }

    static string PathOf(Component c) => c.transform.parent == null ? c.name : PathOf(c.transform.parent) + "/" + c.name;
}
