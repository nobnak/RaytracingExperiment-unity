using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public class RayTracingPassFeature : ScriptableRendererFeature {

    [System.Serializable]
    public class Settings {
        public RayTracingShader rayTracingShader;
        public Shader compositeShader;
        public Shader temporalShader;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        [Range(0f, 90f)] public float angularDiameter = 0.5f;
        [Range(1, 64)] public int sampleCount = 16;
        [Range(0f, 1f)] public float temporalBlend = 0.9f;
    }

    public Settings settings = new Settings();

    class RayTracingPass : ScriptableRenderPass {
        private readonly Settings settings;
        private RayTracingAccelerationStructure rayTracingAccelerationStructure;
        private Material compositeMaterial;
        private Material temporalBlendMaterial;
        private RTHandle prevShadowTexture;
        private int prevWidth;
        private int prevHeight;

        public RayTracingPass(Settings settings) {
            this.settings = settings;
            if (settings.compositeShader != null) compositeMaterial = new Material(settings.compositeShader);
            if (settings.temporalShader != null) temporalBlendMaterial = new Material(settings.temporalShader);
        }

        // RenderGraph パスで必要なデータを保持するクラス
        // パス実行時にデリゲート関数へパラメータとして渡される
        private class PassData {
            public RayTracingShader rayTracingShader;
            public TextureHandle color_texture;
            public TextureHandle shadow_texture;
            public TextureHandle blended_shadow_texture;
            public TextureHandle camera_target;
            public RayTracingAccelerationStructure rayTracingAccelerationStructure;
            public Camera camera;
            public Material compositeMaterial;
            public Material temporalBlendMaterial;
            public RTHandle prevShadowTexture;
            public float angularDiameter;
            public int sampleCount;
            public float temporalBlend;
        }

        // RenderGraph の RenderFunc デリゲートとして渡される静的メソッド
        // 描画コマンドの実行に使用される
        static void ExecutePass(PassData data, UnsafeGraphContext context) {
            var native_cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

            data.rayTracingAccelerationStructure?.Build();

            native_cmd.SetRayTracingShaderPass(data.rayTracingShader, RT_ClosestHit);

            context.cmd.SetRayTracingAccelerationStructure(
                data.rayTracingShader, 
                ID_Scene, 
                data.rayTracingAccelerationStructure);
            context.cmd.SetRayTracingTextureParam(
                data.rayTracingShader, 
                ID_ColorOutput, 
                data.color_texture);
            context.cmd.SetRayTracingTextureParam(
                data.rayTracingShader, 
                ID_ShadowOutput, 
                data.shadow_texture);
            
            // HitShader のグローバル変数を設定
            context.cmd.SetGlobalFloat(ID_AngularDiameter, data.angularDiameter);
            context.cmd.SetGlobalInt(ID_SampleCount, data.sampleCount);

            context.cmd.DispatchRays(data.rayTracingShader, RT_RayGen, (uint)data.camera.pixelWidth, (uint)data.camera.pixelHeight, 1, data.camera);

            // 時間平均処理（EMA）
            if (data.temporalBlendMaterial != null && data.prevShadowTexture != null) {
                data.temporalBlendMaterial.SetTexture(ID_CurrentTex, data.shadow_texture);
                data.temporalBlendMaterial.SetTexture(ID_PrevTex, data.prevShadowTexture);
                data.temporalBlendMaterial.SetFloat(ID_BlendFactor, data.temporalBlend);
                native_cmd.Blit(data.shadow_texture, data.blended_shadow_texture, data.temporalBlendMaterial, 0);
                
                // 次フレーム用に保存
                native_cmd.Blit(data.blended_shadow_texture, data.prevShadowTexture);
            } else {
                // 初回フレームまたはマテリアルがない場合
                native_cmd.Blit(data.shadow_texture, data.blended_shadow_texture);
                if (data.prevShadowTexture != null)
                    native_cmd.Blit(data.shadow_texture, data.prevShadowTexture);
            }

            // color_texture に blended_shadow_texture を乗算して camera_target に書き戻す
            if (data.compositeMaterial != null) {
                data.compositeMaterial.SetTexture(ID_ShadowTex, data.blended_shadow_texture);
                native_cmd.Blit(data.color_texture, data.camera_target, data.compositeMaterial, 0);
            } else {
                native_cmd.Blit(data.color_texture, data.camera_target);
            }
        }

        // RenderGraph ハンドルにアクセスし、グラフにレンダーパスを追加するメソッド
        // FrameData は URP リソースへのアクセスと管理を行うコンテキストコンテナ
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            const string passName = "Custom Shading Pass";

            // frameData から必要なリソースとカメラデータを取得
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            // 現在のカメラで描画されたカラーフレームバッファを取得
            var colorTexture = resourceData.activeColorTexture;

            // レイトレーシング結果を描き出すバッファを作成
            RenderTextureDescriptor rtdesc = cameraData.cameraTargetDescriptor;
            rtdesc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            rtdesc.depthStencilFormat = GraphicsFormat.None;
            rtdesc.depthBufferBits = 0;
            rtdesc.enableRandomWrite = true;
            var resultTex = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, rtdesc, "_RayTracedColor", false);
            var shadowTex = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, rtdesc, "_RayTracedShadow", false);
            var blendedShadowTex = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, rtdesc, "_BlendedShadow", false);
            
            // 前フレームの影テクスチャを初期化（解像度変更時も再作成）
            int currentWidth = cameraData.cameraTargetDescriptor.width;
            int currentHeight = cameraData.cameraTargetDescriptor.height;
            bool resolutionChanged = currentWidth != prevWidth || currentHeight != prevHeight;
            
            if (prevShadowTexture == null || resolutionChanged) {
                prevShadowTexture?.Release();
                prevShadowTexture = RTHandles.Alloc(
                    currentWidth,
                    currentHeight,
                    1,
                    DepthBits.None,
                    GraphicsFormat.R16G16B16A16_SFloat,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    TextureDimension.Tex2D,
                    enableRandomWrite: true,
                    useMipMap: false,
                    name: "_PrevShadowTexture");
                
                // 白（影なし）で初期化
                RenderTexture.active = prevShadowTexture;
                GL.Clear(false, true, Color.white);
                RenderTexture.active = null;
                
                prevWidth = currentWidth;
                prevHeight = currentHeight;
            }

            // アクセラレーション構造を作成
            if (rayTracingAccelerationStructure == null) {
                var settings = new RayTracingAccelerationStructure.Settings();
                settings.rayTracingModeMask = RayTracingAccelerationStructure.RayTracingModeMask.Everything;
                settings.managementMode = RayTracingAccelerationStructure.ManagementMode.Automatic;
                settings.layerMask = 255;

                rayTracingAccelerationStructure = new RayTracingAccelerationStructure(settings);

                // アクセラレーション構造の構築は ExecutePass で実行される
                // 動的な更新には今のところ対応しない
            }

            // レンダーパスをグラフに追加し、ExecutePass 関数に渡すデータ型を指定
            using (var builder = renderGraph.AddUnsafePass<PassData>(passName, out var passData)) {
                // パスの入出力を設定し、実行時に必要なプロパティを passData にセットアップ
                passData.rayTracingShader = settings.rayTracingShader;
                passData.color_texture = resultTex;
                passData.shadow_texture = shadowTex;
                passData.blended_shadow_texture = blendedShadowTex;
                passData.camera_target = colorTexture;
                passData.rayTracingAccelerationStructure = rayTracingAccelerationStructure;
                passData.camera = cameraData.camera;
                passData.compositeMaterial = compositeMaterial;
                passData.temporalBlendMaterial = temporalBlendMaterial;
                passData.prevShadowTexture = prevShadowTexture;
                passData.angularDiameter = settings.angularDiameter;
                passData.sampleCount = settings.sampleCount;
                passData.temporalBlend = settings.temporalBlend;

                builder.UseTexture(passData.color_texture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.shadow_texture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.blended_shadow_texture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.camera_target, AccessFlags.ReadWrite);

                // レンダーパスのデリゲートに ExecutePass 関数を割り当て
                // RenderGraph によるパス実行時に呼び出される
                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => {
                    ExecutePass(data, context);
                });
            }
        }

        public void Cleanup() {
            // アクセラレーション構造が作成されていれば解放
            rayTracingAccelerationStructure?.Dispose();
            rayTracingAccelerationStructure = null;
            
            prevShadowTexture?.Release();
            prevShadowTexture = null;
            
            if (compositeMaterial != null) {
                Object.DestroyImmediate(compositeMaterial);
                compositeMaterial = null;
            }
            
            if (temporalBlendMaterial != null) {
                Object.DestroyImmediate(temporalBlendMaterial);
                temporalBlendMaterial = null;
            }
        }
    }

    Dictionary<int, RayTracingPass> m_PassPerCamera = new Dictionary<int, RayTracingPass>();

    // レンダーパスの初期化
    public override void Create() {
        // カメラごとにパスを作成するため、ここでは初期化のみ
    }

    // レンダラーにレンダーパスを追加
    // カメラごとにレンダラーのセットアップ時に呼び出される
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        if (renderingData.cameraData.isPreviewCamera) {
            // プレビューカメラではパスを追加しない
            return;
        }

        Camera camera = renderingData.cameraData.camera;
        if (camera == null) return;
        
        int cameraID = camera.GetInstanceID();
        
        // カメラごとにパスを取得または作成
        if (!m_PassPerCamera.TryGetValue(cameraID, out var pass)) {
            pass = new RayTracingPass(settings);
            pass.renderPassEvent = settings.renderPassEvent;
            m_PassPerCamera[cameraID] = pass;
        }
        
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        if (disposing) {
            foreach (var pass in m_PassPerCamera.Values)
                pass?.Cleanup();
            m_PassPerCamera.Clear();
        }
    }

    #region declarations
    public const string RT_RayGen = "RaygenShader";
    public const string RT_ClosestHit = "ClosestHitShader";
    public static readonly int ID_Scene = Shader.PropertyToID("g_Scene");
    public static readonly int ID_ColorOutput = Shader.PropertyToID("g_ColorOutput");
    public static readonly int ID_ShadowOutput = Shader.PropertyToID("g_ShadowOutput");
    public static readonly int ID_ShadowTex = Shader.PropertyToID("_ShadowTex");
    public static readonly int ID_AngularDiameter = Shader.PropertyToID("g_AngularDiameter");
    public static readonly int ID_SampleCount = Shader.PropertyToID("g_SampleCount");
    public static readonly int ID_CurrentTex = Shader.PropertyToID("_CurrentTex");
    public static readonly int ID_PrevTex = Shader.PropertyToID("_PrevTex");
    public static readonly int ID_BlendFactor = Shader.PropertyToID("_BlendFactor");
    #endregion
}
