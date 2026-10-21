using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public static class JavaHollowFloorBake
{
    const string Folder = "Assets/PerformanceOptimizations/Floor/Baked";
    const string ConfigPath = "Assets/Resources/JavaHollowBakedFloor.asset";
    static JavaHollowBakedFloor Config => AssetDatabase.LoadAssetAtPath<JavaHollowBakedFloor>(ConfigPath);
    static IEnumerable<Terrain> Terrains => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Terrain>(true));
    static bool IsDirt(DetailPrototype p)
    {
        string path = AssetDatabase.GetAssetPath(p.prototype);
        return path == "Assets/PerformanceOptimizations/Floor/QuadCandidate/DirtQuad.prefab" ||
               path == "Assets/PerformanceOptimizations/Floor/DirtDetailCaps.prefab";
    }

    [MenuItem("Tools/Java Hollow/Bake Ground Surface %&#b")]
    public static void Build()
    {
        if (EditorApplication.isPlaying || Config != null) throw new InvalidOperationException("Build once outside Play Mode; preserve the existing candidate before rebuilding.");
        Directory.CreateDirectory(Folder); Directory.CreateDirectory("Assets/Resources");
        AssetDatabase.Refresh();
        var report = new StringBuilder("Ground bake: albedo/coverage only; terrain collider, trees, other details, audio and scene unchanged.\n");
        var tiles = new List<JavaHollowBakedFloor.Tile>();
        foreach (var terrain in Terrains.GroupBy(t => t.terrainData).Select(g => g.First()))
        {
            var source = terrain.terrainData;
            if (source == null || !source.detailPrototypes.Any(IsDirt)) continue;
            if (terrain.detailObjectDensity != 1) throw new InvalidOperationException("Bake requires full detail density.");
            string prefix = Folder + "/Tile" + tiles.Count;
            var baked = UnityEngine.Object.Instantiate(source);
            var pixels = new RenderTexture(4096,4096,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            pixels.Create();
            var old = RenderTexture.active;
            Texture2D heights = null;
            Material bake = null;
            GameObject surface = null;
            try
            {
                int hr=source.heightmapResolution;
                heights=new Texture2D(hr,hr,TextureFormat.RFloat,false,true) { filterMode=FilterMode.Bilinear, wrapMode=TextureWrapMode.Clamp };
                var h=source.GetHeights(0,0,hr,hr); var hf=new float[hr*hr];
                for(int z=0;z<hr;z++) for(int x=0;x<hr;x++) hf[z*hr+x]=h[z,x]*source.size.y;
                heights.SetPixelData(hf,0); heights.Apply();
                bake=new Material(Shader.Find("Hidden/Java Hollow/Dirt Coverage Bake")) { enableInstancing=true };
                bake.SetTexture("_HeightMap",heights); bake.SetVector("_TileSize",new Vector4(source.size.x,source.size.y,source.size.z,hr));
                var cb=new CommandBuffer { name="Bake static dirt color and coverage" };
                cb.SetRenderTarget(pixels); cb.ClearRenderTarget(false,true,Color.clear);
                long count=0;
                var matrices=new List<Matrix4x4>(1023);
                Material sourceMaterial=null;
                for(int layer=0;layer<source.detailPrototypes.Length;layer++)
                {
                    var p=source.detailPrototypes[layer]; if(!IsDirt(p)) continue;
                    sourceMaterial=p.prototype.GetComponent<MeshRenderer>().sharedMaterial;
                    if(p.maxHeight>.0001f || sourceMaterial.GetTexture("_BumpMap")!=null)
                        throw new InvalidOperationException("Floor no longer flat/unmapped; revalidate bake.");
                    bake.SetTexture("_BaseMap",sourceMaterial.GetTexture("_BaseMap"));
                    var mesh=p.prototype.GetComponent<MeshFilter>().sharedMesh;
                    for(int px=0;px<source.detailPatchCount;px++) for(int pz=0;pz<source.detailPatchCount;pz++)
                    {
                        foreach(var t in source.ComputeDetailInstanceTransforms(px,pz,layer,1,out _))
                        {
                            matrices.Add(Matrix4x4.TRS(new Vector3(t.posX,t.posY,t.posZ),Quaternion.Euler(0,t.rotationY*Mathf.Rad2Deg,0),new Vector3(t.scaleXZ,t.scaleY,t.scaleXZ)));
                            count++;
                            if(matrices.Count==1023) { cb.DrawMeshInstanced(mesh,0,bake,0,matrices.ToArray()); matrices.Clear(); }
                        }
                    }
                    if(matrices.Count>0) { cb.DrawMeshInstanced(mesh,0,bake,0,matrices.ToArray()); matrices.Clear(); }
                    baked.SetDetailLayer(0,0,layer,new int[source.detailHeight,source.detailWidth]);
                }
                Graphics.ExecuteCommandBuffer(cb); cb.Release();
                RenderTexture.active=pixels;
                var image=new Texture2D(pixels.width,pixels.height,TextureFormat.RGBA32,false,false);
                image.ReadPixels(new Rect(0,0,pixels.width,pixels.height),0,0); image.Apply();
                File.WriteAllBytes(prefix+".png",image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
                AssetDatabase.ImportAsset(prefix+".png",ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(prefix+".png");
                importer.sRGBTexture=true; importer.alphaSource=TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency=true; importer.mipmapEnabled=true; importer.mipMapsPreserveCoverage=true;
                importer.alphaTestReferenceValue=.5f; importer.wrapMode=TextureWrapMode.Clamp;
                importer.filterMode=FilterMode.Trilinear; importer.anisoLevel=8; importer.maxTextureSize=4096;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                var material=new Material(sourceMaterial) { name="Baked dirt", shader=Shader.Find("Universal Render Pipeline/Simple Lit") };
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+".png"));
                material.SetFloat("_AlphaClip",1); material.SetFloat("_Cutoff",.5f);
                material.EnableKeyword("_ALPHATEST_ON"); material.SetOverrideTag("RenderType","TransparentCutout"); material.renderQueue=2450;
                AssetDatabase.CreateAsset(material,prefix+".mat");
                int n=128; float error=HeightError(source,n);
                while(error>.0005f && n<hr-1) { n*=2; error=HeightError(source,n); }
                var overlay=SurfaceMesh(source,n);
                AssetDatabase.CreateAsset(overlay,prefix+".mesh.asset");
                surface=new GameObject("Baked dirt surface");
                surface.AddComponent<MeshFilter>().sharedMesh=overlay;
                var renderer=surface.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=true;
                var prefab=PrefabUtility.SaveAsPrefabAsset(surface,prefix+".prefab");
                AssetDatabase.CreateAsset(baked,prefix+".terrain.asset");
                tiles.Add(new JavaHollowBakedFloor.Tile {source=source,baked=baked});
                report.AppendLine($"{terrain.name}: {count} dirt instances baked; overlay {overlay.triangles.Length/3} triangles; grid {n}; sampled height error {error:R}m; texture 4096x4096; other detail maps/trees unchanged.");
            }
            finally
            {
                RenderTexture.active=old; pixels.Release(); UnityEngine.Object.DestroyImmediate(pixels);
                if(heights!=null) UnityEngine.Object.DestroyImmediate(heights);
                if(bake!=null) UnityEngine.Object.DestroyImmediate(bake);
                if(surface!=null) UnityEngine.Object.DestroyImmediate(surface);
            }
        }
        if(tiles.Count==0) throw new InvalidOperationException("No matching floor details found.");
        var config=ScriptableObject.CreateInstance<JavaHollowBakedFloor>(); config.tiles=tiles.ToArray(); config.useBakedFloor=false;
        AssetDatabase.CreateAsset(config,ConfigPath);
        Directory.CreateDirectory("Performance"); File.WriteAllText("Performance/floor-bake-build.txt",report.ToString());
        Debug.Log("Floor bake built, not enabled. Compare appearance and performance first.");
    }

    static float HeightError(TerrainData d,int n)
    {
        float max=0; int r=d.heightmapResolution-1;
        for(int z=0;z<=r;z++) for(int x=0;x<=r;x++)
        {
            float u=(float)x/r,v=(float)z/r,gx=u*n,gz=v*n;
            int ix=Mathf.Min((int)gx,n-1),iz=Mathf.Min((int)gz,n-1);
            float estimate=Mathf.Lerp(Mathf.Lerp(d.GetInterpolatedHeight((float)ix/n,(float)iz/n),d.GetInterpolatedHeight((float)(ix+1)/n,(float)iz/n),gx-ix),Mathf.Lerp(d.GetInterpolatedHeight((float)ix/n,(float)(iz+1)/n),d.GetInterpolatedHeight((float)(ix+1)/n,(float)(iz+1)/n),gx-ix),gz-iz);
            max=Mathf.Max(max,Mathf.Abs(estimate-d.GetInterpolatedHeight(u,v)));
        }
        return max;
    }

    static Mesh SurfaceMesh(TerrainData d,int n)
    {
        var vertices=new Vector3[(n+1)*(n+1)]; var uv=new Vector2[vertices.Length]; var normals=new Vector3[vertices.Length]; var tangents=new Vector4[vertices.Length];
        for(int z=0;z<=n;z++) for(int x=0;x<=n;x++)
        { int i=z*(n+1)+x; float u=(float)x/n,v=(float)z/n; vertices[i]=new Vector3(u*d.size.x,d.GetInterpolatedHeight(u,v)+.0002f,v*d.size.z); uv[i]=new Vector2(u,v); normals[i]=Vector3.up; tangents[i]=new Vector4(-1,0,0,-1); }
        var indices=new int[n*n*6]; int k=0;
        for(int z=0;z<n;z++) for(int x=0;x<n;x++) {int a=z*(n+1)+x; indices[k++]=a;indices[k++]=a+n+1;indices[k++]=a+1;indices[k++]=a+1;indices[k++]=a+n+1;indices[k++]=a+n+2;}
        var m=new Mesh {name="Terrain-shaped dirt surface",indexFormat=IndexFormat.UInt32}; m.vertices=vertices;m.uv=uv;m.normals=normals;m.tangents=tangents;m.triangles=indices;m.RecalculateBounds();return m;
    }

    static readonly Dictionary<Terrain,TerrainData> saved=new Dictionary<Terrain,TerrainData>();
    static readonly Dictionary<Terrain,Material> savedMaterials=new Dictionary<Terrain,Material>();
    static readonly List<GameObject> surfaces=new List<GameObject>();
    static void Preview(bool baked)
    {
        foreach(var surface in surfaces) if(surface!=null) UnityEngine.Object.DestroyImmediate(surface);
        surfaces.Clear(); foreach(var pair in saved) if(pair.Key!=null) pair.Key.terrainData=pair.Value;
        foreach(var pair in savedMaterials) if(pair.Key!=null) pair.Key.materialTemplate=pair.Value;
        if(!baked) return;
        foreach(var terrain in Terrains)
            foreach(var tile in Config.tiles)
                if(terrain.terrainData==tile.source)
                {
                    if(!saved.ContainsKey(terrain)) saved.Add(terrain,terrain.terrainData);
                    if(!savedMaterials.ContainsKey(terrain)) savedMaterials.Add(terrain,terrain.materialTemplate);
                    terrain.terrainData=tile.baked;
                    terrain.materialTemplate=tile.material; break;
                }
    }
    static void Restore()
    {
        Preview(false); saved.Clear(); savedMaterials.Clear(); EditorApplication.update-=Tick; AssemblyReloadEvents.beforeAssemblyReload-=Restore;
    }

    [MenuItem("Tools/Java Hollow/Compare Baked Ground Images %&#n")]
    public static void CompareImages()
    {
        if(EditorApplication.isPlaying || Config==null) throw new InvalidOperationException("Compare in Edit Mode after building.");
        var main=Camera.main; if(main==null) throw new InvalidOperationException("Main camera missing.");
        var terrain=Terrains.First(t=>t.terrainData==Config.tiles[0].source);
        var cameraObject=new GameObject("Temporary comparison camera") {hideFlags=HideFlags.HideAndDontSave};
        var camera=cameraObject.AddComponent<Camera>(); camera.CopyFrom(main);camera.enabled=false;camera.nearClipPlane=.005f;
        camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
        var rt=new RenderTexture(1280,720,24) {antiAliasing=4};rt.Create();var previous=RenderTexture.active;
        var point=main.transform.position+Vector3.ProjectOnPlane(main.transform.forward,Vector3.up).normalized*1.2f;
        point.y=terrain.SampleHeight(point)+terrain.transform.position.y;
        Directory.CreateDirectory("Performance/BakedFloorComparison");var report=new StringBuilder();
        try
        {
            for(int view=0;view<3;view++)
            {
                string name=new[]{"standing","close","grazing"}[view];
                camera.transform.position=view==0?main.transform.position:point+new Vector3(0,view==1?.25f:.03f,-.4f);
                camera.transform.rotation=Quaternion.LookRotation(point-camera.transform.position);
                Color32[] baseline=null;
                for(int variant=0;variant<2;variant++)
                {
                    Preview(variant==1);
                    var request=new RenderPipeline.StandardRequest {destination=rt};
                    RenderPipeline.SubmitRenderRequest(camera,request);RenderPipeline.SubmitRenderRequest(camera,request);
                    RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
                    image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                    File.WriteAllBytes($"Performance/BakedFloorComparison/{name}-{(variant==0?"original":"baked")}.png",image.EncodeToPNG());
                    var colors=image.GetPixels32();UnityEngine.Object.DestroyImmediate(image);
                    if(variant==0) baseline=colors; else {long delta=0;for(int i=0;i<colors.Length;i++) delta+=Math.Abs(colors[i].r-baseline[i].r)+Math.Abs(colors[i].g-baseline[i].g)+Math.Abs(colors[i].b-baseline[i].b);report.AppendLine($"{name}: mean RGB absolute difference={delta/(colors.Length*3.0):F5}/255");}
                }
            }
            File.WriteAllText("Performance/floor-bake-images.txt",report.ToString());
        }
        finally {Restore();RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(cameraObject);}
        Debug.Log("Baked floor image comparison finished; scene restored.");
    }

    static readonly List<double> ms=new List<double>();static readonly List<int> tris=new List<int>(),batches=new List<int>();
    static double start;static int phase,lastFrame,worn;static StringBuilder results;
    [MenuItem("Tools/Java Hollow/Measure Baked Ground %&#m")]
    public static void Measure()
    {
        if(!EditorApplication.isPlaying || Config==null || Config.useBakedFloor) throw new InvalidOperationException("Measure in Play Mode with baked startup disabled.");
        Restore();phase=0;lastFrame=Time.frameCount;start=EditorApplication.timeSinceStartup;ms.Clear();tris.Clear();batches.Clear();worn=0;
        results=new StringBuilder("Baked ground A/B/A, 4s settling + 8s measured per phase; fixed view required.\n");
        EditorApplication.update+=Tick;AssemblyReloadEvents.beforeAssemblyReload+=Restore;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying) {Restore();return;} if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        double elapsed=EditorApplication.timeSinceStartup-start;if(elapsed<4)return;
        if(elapsed<12) {ms.Add(Time.unscaledDeltaTime*1000);tris.Add(UnityStats.triangles);batches.Add(UnityStats.batches);if(InputDevices.GetDeviceAtXRNode(XRNode.Head).TryGetFeatureValue(CommonUsages.userPresence,out bool present)&&present)worn++;return;}
        if(ms.Count>0) {var displays=new List<XRDisplaySubsystem>();SubsystemManager.GetSubsystems(displays);ms.Sort();results.AppendLine($"{(phase==1?"Baked":"Original")}: meanMs={ms.Average():F2}; FPS={1000/ms.Average():F2}; p95={ms[(int)((ms.Count-1)*.95)]:F2}; triangles={tris.Average():F0}; batches={batches.Average():F0}; XR={displays.Any(d=>d.running)}; worn={worn}/{ms.Count}");}
        ms.Clear();tris.Clear();batches.Clear();worn=0;phase++;
        if(phase==3) {Restore();File.WriteAllText("Performance/floor-bake-ab.txt",results.ToString());Debug.Log("Floor bake A/B/A complete; original restored.");return;}
        Preview(phase==1);start=EditorApplication.timeSinceStartup;
    }

    [MenuItem("Tools/Java Hollow/Enable Baked Ground %&#j")]
    public static void Enable()
    {
        if(EditorApplication.isPlaying || Config==null || !File.Exists("Performance/floor-bake-ab.txt")) throw new InvalidOperationException("Validate candidate first, then enable outside Play Mode.");
        Config.useBakedFloor=true;EditorUtility.SetDirty(Config);AssetDatabase.SaveAssetIfDirty(Config);Debug.Log("Baked ground enabled for Demo for Show on next Play/build. Open scene not saved.");
    }
    [MenuItem("Tools/Java Hollow/Restore Original Ground")]
    public static void Disable()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if(Config!=null) {Config.useBakedFloor=false;EditorUtility.SetDirty(Config);AssetDatabase.SaveAssetIfDirty(Config);}
    }

    [MenuItem("Tools/Java Hollow/Finalize Baked Terrain Material %&#k")]
    public static void FinalizeTerrain()
    {
        if(EditorApplication.isPlaying || Config==null) throw new InvalidOperationException("Finalize in Edit Mode.");
        var tile=Config.tiles[0];
        var terrain=Terrains.First(t=>t.terrainData==tile.source);
        if(tile.source.terrainLayers.Length>4) throw new InvalidOperationException("Multipass terrain requires a separate validation.");
        var shader=Shader.Find("Java Hollow/Baked Dirt Terrain");
        if(shader==null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Terrain shader import failed.");
        string path=Folder+"/TerrainMaterial.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) {material=new Material(terrain.materialTemplate) {shader=shader,name="Baked dirt terrain"};AssetDatabase.CreateAsset(material,path);}
        material.SetTexture("_GroundBake",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Tile0.png"));
        var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+"/Tile0.png");
        importer.mipMapsPreserveCoverage=false; importer.SaveAndReimport();
        tile.material=material;Config.tiles=new[]{tile}; // Six patches on the adjacent tile do not justify baking.
        EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);EditorUtility.SetDirty(Config);AssetDatabase.SaveAssetIfDirty(Config);
        Debug.Log("Baked coverage now renders directly on the central terrain; adjacent sparse details preserved.");
    }
}
