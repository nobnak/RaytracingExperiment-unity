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
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        [Range(0f, 90f)] public float angularDiameter = 0.5f;
        [Range(1, 64)] public int sampleCount = 16;
    }

    public Settings settings = new Settings();

    class RayTracingPass : ScriptableRenderPass {
        private readonly Settings settings;
        private RayTracingAccelerationStructure rayTracingAccelerationStructure;
        private Material compositeMaterial;

        public RayTracingPass(Settings settings) {
            this.settings = settings;
            if (settings.compositeShader != null) compositeMaterial = new Material(settings.compositeShader);
        }

        // RenderGraph パスで必要なデータを保持するクラス
        // パス実行時にデリゲート関数へパラメータとして渡される
        private class PassData {
            public RayTracingShader rayTracingShader;
            public TextureHandle color_texture;
            public TextureHandle shadow_texture;
            public TextureHandle camera_target;
            public RayTracingAccelerationStructure rayTracingAccelerationStructure;
            public Camera camera;
            public Material compositeMaterial;
            public float angularDiameter;
            public int sampleCount;
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

            // color_texture に shadow_Texture を乗算して camera_target に書き戻す
            if (data.compositeMaterial != null) {
                data.compositeMaterial.SetTexture(ID_ShadowTex, data.shadow_texture);
                native_cmd.Blit(data.color_texture, data.camera_target, data.compositeMaterial, 0);
            } else {
                native_cmd.Blit(data.color_texture, data.camera_target);
            }
        }

        // RenderGraph ハンドルにアクセスし、グラフにレンダーパスを追加するメソッド
        // FrameData は URP リソースへのアクセスと管理を行うコンテキストコンテナ
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            const string passName = "Custom Shading Pass";
            Debug.Log("RecordRenderGraph: " + passName);

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
                passData.camera_target = colorTexture;
                passData.rayTracingAccelerationStructure = rayTracingAccelerationStructure;
                passData.camera = cameraData.camera;
                passData.compositeMaterial = compositeMaterial;
                passData.angularDiameter = settings.angularDiameter;
                passData.sampleCount = settings.sampleCount;

                builder.UseTexture(passData.color_texture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.shadow_texture, AccessFlags.ReadWrite);
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
            
            if (compositeMaterial != null) {
                Object.DestroyImmediate(compositeMaterial);
                compositeMaterial = null;
            }
        }
    }

    RayTracingPass m_ScriptablePass;

    // レンダーパスの初期化
    public override void Create() {
        m_ScriptablePass = new RayTracingPass(settings);
        m_ScriptablePass.renderPassEvent = settings.renderPassEvent;
    }

    // レンダラーにレンダーパスを追加
    // カメラごとにレンダラーのセットアップ時に呼び出される
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        if (renderingData.cameraData.isPreviewCamera) {
            // プレビューカメラではパスを追加しない
            return;
        }
        renderer.EnqueuePass(m_ScriptablePass);
    }

    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        if (disposing) {
            m_ScriptablePass?.Cleanup();
            m_ScriptablePass = null;
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
    #endregion
}
