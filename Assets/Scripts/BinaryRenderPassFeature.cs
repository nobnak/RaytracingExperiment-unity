using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public class RayTracingPassFeature : ScriptableRendererFeature {

    public RayTracingShader rayTracingShader;

    class RayTracingPass : ScriptableRenderPass {
        private readonly RayTracingShader rayTracingShader;
        private RayTracingAccelerationStructure rayTracingAccelerationStructure;

        public RayTracingPass(RayTracingShader rayTracingShader) {
            this.rayTracingShader = rayTracingShader;
        }

        // RenderGraph パスで必要なデータを保持するクラス
        // パス実行時にデリゲート関数へパラメータとして渡される
        private class PassData {
            public RayTracingShader rayTracingShader;
            public TextureHandle output_ColorTexture;
            public TextureHandle camera_ColorTarget;
            public RayTracingAccelerationStructure rayTracingAccelerationStructure;
            public Camera camera;
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
                ID_Output, 
                data.output_ColorTexture);

            context.cmd.DispatchRays(data.rayTracingShader, RT_RayGen, (uint)data.camera.pixelWidth, (uint)data.camera.pixelHeight, 1, data.camera);

            // 結果をカメラに書き戻す
            native_cmd.Blit(data.output_ColorTexture, data.camera_ColorTarget);
        }

        // RenderGraph ハンドルにアクセスし、グラフにレンダーパスを追加するメソッド
        // FrameData は URP リソースへのアクセスと管理を行うコンテキストコンテナ
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            const string passName = "Render Custom Pass";

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
                passData.rayTracingShader = rayTracingShader;
                passData.output_ColorTexture = resultTex;
                passData.camera_ColorTarget = colorTexture;
                passData.rayTracingAccelerationStructure = rayTracingAccelerationStructure;
                passData.camera = cameraData.camera;

                builder.UseTexture(passData.output_ColorTexture, AccessFlags.Write);
                builder.UseTexture(passData.camera_ColorTarget, AccessFlags.ReadWrite);

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
        }
    }

    RayTracingPass m_ScriptablePass;

    // レンダーパスの初期化
    public override void Create() {
        m_ScriptablePass = new RayTracingPass(rayTracingShader);

        // レンダーパスを挿入するタイミングを設定
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    // レンダラーにレンダーパスを追加
    // カメラごとにレンダラーのセットアップ時に呼び出される
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        if (renderingData.cameraData.isSceneViewCamera || renderingData.cameraData.isPreviewCamera) {
            // シーンビューカメラとプレビューカメラではパスを追加しない
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
    public const string RT_ClosestHit = "BinaryRayTracing";
    public static readonly int ID_Scene = Shader.PropertyToID("g_Scene");
    public static readonly int ID_Output = Shader.PropertyToID("g_Output");
    #endregion
}
