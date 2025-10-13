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

            // 重心座標による補完マクロ
            #define BARY_INTERPOLATE(v0, v1, v2, bary, attr) \
                ((v0).attr * (bary).x + (v1).attr * (bary).y + (v2).attr * (bary).z)

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
            
            Vertex InterpolateVertices(Vertex v0, Vertex v1, Vertex v2, float3 bary) {
                Vertex result;
                result.position = BARY_INTERPOLATE(v0, v1, v2, bary, position);
                result.normal = normalize(BARY_INTERPOLATE(v0, v1, v2, bary, normal));
                result.tangent = BARY_INTERPOLATE(v0, v1, v2, bary, tangent);
                result.texCoord0 = BARY_INTERPOLATE(v0, v1, v2, bary, texCoord0);
                result.color = BARY_INTERPOLATE(v0, v1, v2, bary, color);
                return result;
            }

            [shader("closesthit")]
            void ClosestHitMain(inout RayPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes) {
                // ヒット位置（ワールド空間）
                float3 hitPositionWorld = WorldRayOrigin() + WorldRayDirection() * RayTCurrent();
                
                // 重心座標
                float3 bary = float3(1.0 - attribs.barycentrics.x - attribs.barycentrics.y, attribs.barycentrics.x, attribs.barycentrics.y);
                
                // 頂点を取得して補完
                uint3 indices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());
                Vertex v0 = FetchVertex(indices.x);
                Vertex v1 = FetchVertex(indices.y);
                Vertex v2 = FetchVertex(indices.z);
                Vertex interpolated = InterpolateVertices(v0, v1, v2, bary);
                
                // 法線をワールド空間に変換
                float3 normalWorld = normalize(mul((float3x3)ObjectToWorld3x4(), interpolated.normal));
                
                // 法線を色として表示（[-1,1] → [0,1]）
                payload.color = float4(normalWorld * 0.5 + 0.5, 1);
                
                // その他の表示オプション：
                // payload.color = float4(bary, 1); // 重心座標
                // payload.color = float4(abs(normalWorld), 1); // 法線の絶対値
                // payload.color = _Color * max(0, dot(normalWorld, float3(0, 1, 0))); // 簡易ライティング
            }

            ENDHLSL
        }
    }
    FallBack "Diffuse"
}