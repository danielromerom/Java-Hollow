Shader "Hidden/Java Hollow/Dirt Coverage Bake"
{
    Properties { _BaseMap("Source",2D)="white" {} _HeightMap("Terrain heights",2D)="black" {} }
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_HeightMap); SAMPLER(sampler_HeightMap);
            float4 _TileSize; // width, height, depth, heightmap resolution
            struct A { float3 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float3 terrain:TEXCOORD1; };
            V Vert(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                V o;
                o.terrain=TransformObjectToWorld(v.vertex);
                float2 uv=o.terrain.xz/_TileSize.xz;
                o.position=float4(uv*2-1,0,1);
                #if UNITY_UV_STARTS_AT_TOP
                o.position.y=-o.position.y;
                #endif
                o.uv=v.uv;
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                // Same regular 20-sided outline as DirtDetailCaps and DirtQuad.
                float2 p=abs((i.uv-float2(.5002244781,.4999441968))/float2(-.9922326663,-.9922328298));
                float edge=max(dot(p,float2(.9876883406,.156434465)),dot(p,float2(.8910065242,.4539904997)));
                edge=max(edge,dot(p,float2(.7071067812,.7071067812)));
                edge=max(edge,dot(p,float2(.4539904997,.8910065242)));
                edge=max(edge,dot(p,float2(.156434465,.9876883406)));
                clip(.4938441703-edge);
                float2 huv=(i.terrain.xz/_TileSize.xz*(_TileSize.w-1)+.5)/_TileSize.w;
                float height=SAMPLE_TEXTURE2D_LOD(_HeightMap,sampler_HeightMap,huv,0).r;
                clip(i.terrain.y-height);
                return half4(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb,1);
            }
            ENDHLSL
        }
    }
}
