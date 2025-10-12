Shader "Custom/Binary" {
    Properties { 
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }

    HLSLINCLUDE
    #include "Assets/ShaderLibrary/Payload.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "UnityRaytracingMeshUtils.cginc"

    CBUFFER_START(UnityPerMaterial)
    float4 _Color;
    sampler2D _MainTex;
    float4 _MainTex_ST;
    CBUFFER_END
    ENDHLSL

    SubShader {

        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass {
            Name "ClosestHitShader"
            Tags { "LightMode" = "Raytracing" }

            HLSLPROGRAM
            #pragma raytracing test

            struct Vertex {
                float3 position;
                float3 normal;
                float4 tangent;
                float2 texCoord0;
                float4 color;
            };
            Vertex FetchVertex(uint vertexIndex) {
                Vertex v;
                v.position = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributePosition);
                v.normal = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributeNormal);
                v.tangent = UnityRayTracingFetchVertexAttribute4(vertexIndex, kVertexAttributeTangent);
                v.texCoord0 = UnityRayTracingFetchVertexAttribute2(vertexIndex, kVertexAttributeTexCoord0);
                v.color = UnityRayTracingFetchVertexAttribute4(vertexIndex, kVertexAttributeColor);
                return v;
            }

            [shader("closesthit")]
            void ClosestHitMain(inout RayPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes) {
                // ヒット位置をレイから直接計算（ワールド空間）
                float3 hitPositionWorld = WorldRayOrigin() + WorldRayDirection() * RayTCurrent();
                
                // 重心座標を取得
                float3 barycentrics = float3(1.0 - attribs.barycentrics.x - attribs.barycentrics.y, attribs.barycentrics.x, attribs.barycentrics.y);
                
                // プリミティブインデックスを取得
                uint primitiveIndex = PrimitiveIndex();                
                uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(primitiveIndex);
                Vertex v0 = FetchVertex(triangleIndices.x);
                Vertex v1 = FetchVertex(triangleIndices.y);
                Vertex v2 = FetchVertex(triangleIndices.z);
                
                float3 barycentricNormal = normalize(v0.normal * barycentrics.x + v1.normal * barycentrics.y + v2.normal * barycentrics.z);
                
                // デバッグ用：PrimitiveIndexで色分け
                float3 debugColor = float3(
                    frac(primitiveIndex * 0.1),
                    frac(primitiveIndex * 0.2),
                    frac(primitiveIndex * 0.3)
                );
                
                // 重心座標で色付け（デバッグ用）
                // debugColor = barycentrics;
                
                payload.color = float4(debugColor, 1);
            }

            ENDHLSL
        }
    }
    FallBack "Diffuse"
}