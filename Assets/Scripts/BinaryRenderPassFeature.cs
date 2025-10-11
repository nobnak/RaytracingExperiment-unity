using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public class BinaryRenderPassFeature : ScriptableRendererFeature {

    public RayTracingShader rayTracingShader;

    class CustomRenderPass : ScriptableRenderPass {
        private readonly RayTracingShader rayTracingShader;
        private RayTracingAccelerationStructure rayTracingAccelerationStructure;

        public CustomRenderPass(RayTracingShader rayTracingShader) {
            this.rayTracingShader = rayTracingShader;
        }

        // This class stores the data needed by the RenderGraph pass.
        // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
        private class PassData {
            public RayTracingShader rayTracingShader;
            public TextureHandle output_ColorTexture;
            public TextureHandle camera_ColorTarget;
            public RayTracingAccelerationStructure rayTracingAccelerationStructure;
            public Camera camera;
        }

        // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
        // It is used to execute draw commands.
        static void ExecutePass(PassData data, UnsafeGraphContext context) {
            var native_cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

            data.rayTracingAccelerationStructure?.Build();

            native_cmd.SetRayTracingShaderPass(data.rayTracingShader, Name_ClosestHitPass);

            context.cmd.SetRayTracingAccelerationStructure(
                data.rayTracingShader, 
                ID_AccelStruct, 
                data.rayTracingAccelerationStructure);
            context.cmd.SetRayTracingTextureParam(
                data.rayTracingShader, 
                ID_Output, 
                data.output_ColorTexture);

            context.cmd.DispatchRays(data.rayTracingShader, "MyRaygenShader", (uint)data.camera.pixelWidth, (uint)data.camera.pixelHeight, 1, data.camera);

            // 結果をカメラに書き戻す
            native_cmd.Blit(data.output_ColorTexture, data.camera_ColorTarget);
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            const string passName = "Render Custom Pass";

            // Make use of frameData to access resources and camera data through the dedicated containers.
            // Eg:
            // UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            // 現在のカメラで描画されたカラーフレームバッファを取得
            var colorTexture = resourceData.activeColorTexture;

            // レイトレ結果を描き出すバッファを作成
            RenderTextureDescriptor rtdesc = cameraData.cameraTargetDescriptor;
            rtdesc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            rtdesc.depthStencilFormat = GraphicsFormat.None;
            rtdesc.depthBufferBits = 0;
            rtdesc.enableRandomWrite = true;
            var resultTex = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, rtdesc, "_RayTracedColor", false);

            // Acceleration Structure を作成
            if (rayTracingAccelerationStructure == null) {
                var settings = new RayTracingAccelerationStructure.Settings();
                settings.rayTracingModeMask = RayTracingAccelerationStructure.RayTracingModeMask.Everything;
                settings.managementMode = RayTracingAccelerationStructure.ManagementMode.Automatic;
                settings.layerMask = 255;

                rayTracingAccelerationStructure = new RayTracingAccelerationStructure(settings);

                // AS の構築はここだけ。動的な更新には今は対応しない
                //rayTracingAccelerationStructure.Build();
            }

            // This adds a raster render pass to the graph, specifying the name and the data type that will be passed to the ExecutePass function.
            using (var builder = renderGraph.AddUnsafePass<PassData>(passName, out var passData)) {
                // Use this scope to set the required inputs and outputs of the pass and to
                // setup the passData with the required properties needed at pass execution time.

                // Setup pass inputs and outputs through the builder interface.
                // Eg:
                // builder.UseTexture(sourceTexture);
                // TextureHandle destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraData.cameraTargetDescriptor, "Destination Texture", false);
                passData.rayTracingShader = rayTracingShader;
                passData.output_ColorTexture = resultTex;
                passData.camera_ColorTarget = colorTexture;
                passData.rayTracingAccelerationStructure = rayTracingAccelerationStructure;
                passData.camera = cameraData.camera;

                builder.UseTexture(passData.output_ColorTexture, AccessFlags.Write);
                builder.UseTexture(passData.camera_ColorTarget, AccessFlags.ReadWrite);

                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => {
                    ExecutePass(data, context);
                });
            }
        }

        public void Cleanup() {
            // Cleanup the acceleration structure if it was created.
            rayTracingAccelerationStructure?.Dispose();
            rayTracingAccelerationStructure = null;
        }
    }

    CustomRenderPass m_ScriptablePass;

    /// <inheritdoc/>
    public override void Create() {
        m_ScriptablePass = new CustomRenderPass(rayTracingShader);

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        if (renderingData.cameraData.isSceneViewCamera || renderingData.cameraData.isPreviewCamera) {
            // Do not add the pass for scene view or preview cameras.
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
    public const string Name_ClosestHitPass = "BinaryRayTracing";
    public static readonly int ID_AccelStruct = Shader.PropertyToID("g_SceneAccelStruct");
    public static readonly int ID_Output = Shader.PropertyToID("g_Output");
    public static readonly int ID_Zoom = Shader.PropertyToID("g_Zoom");
    #endregion
}
