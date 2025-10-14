Shader "Custom/Binary" {
    Properties { 
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Ambient ("Ambient", Range(0,1)) = 0.1
    }

    HLSLINCLUDE
    #include "Assets/ShaderLibrary/Payload.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "UnityRaytracingMeshUtils.cginc"

    CBUFFER_START(UnityPerMaterial)
    float4 _Color;
    sampler2D _MainTex;
    float4 _MainTex_ST;
    float _Ambient;
    CBUFFER_END
    
    RaytracingAccelerationStructure g_Scene;
    float g_AngularDiameter;
    int g_SampleCount;
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
            
            // 疑似乱数生成関数
            float rand(float2 co) {
                return frac(sin(dot(co, float2(12.9898, 78.233))) * 43758.5453);
            }
            
            // 円盤上の一様サンプリング
            float2 randomInUnitDisk(float2 seed) {
                float r = sqrt(rand(seed));
                float theta = 2.0 * 3.14159265359 * rand(seed + float2(1.0, 2.0));
                return float2(r * cos(theta), r * sin(theta));
            }
            
            // 球内の一様サンプリング
            float3 randomInSphere(float2 seed) {
                float r = pow(rand(seed), 1.0 / 3.0);
                float theta = 2.0 * 3.14159265359 * rand(seed + float2(1.0, 2.0));
                float phi = acos(2.0 * rand(seed + float2(3.0, 4.0)) - 1.0);
                return float3(
                    r * sin(phi) * cos(theta),
                    r * sin(phi) * sin(theta),
                    r * cos(phi)
                );
            }
            
            // ライト方向を摂動
            float3 perturbLightDirection(float3 lightDir, float2 seed, float angularDiameterRad) {
                float2 disk = randomInUnitDisk(seed) * tan(angularDiameterRad * 0.5);
                
                float3 tangent = normalize(cross(lightDir, float3(0, 1, 0)));
                if (length(tangent) < 0.01) tangent = normalize(cross(lightDir, float3(1, 0, 0)));
                float3 bitangent = cross(lightDir, tangent);
                
                return normalize(lightDir + disk.x * tangent + disk.y * bitangent);
            }

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
                
                // メインライトの情報を取得
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                float3 lightColor = mainLight.color;
                
                // ランバート拡散反射
                float NdotL = max(0, dot(normalWorld, lightDir));
                float3 diffuse = lightColor * NdotL;
                
                // シャドウレイを飛ばして遮蔽テスト（ソフトシャドウ）
                float shadowFactor = 0.0;
                if (NdotL > 0) {
                    float angularDiameterRad = g_AngularDiameter * 3.14159265359 / 180.0;
                    int sampleCount = max(1, g_SampleCount);
                    
                    RayDesc shadowRay;
                    shadowRay.Origin = hitPositionWorld + normalWorld * 0.001;
                    shadowRay.TMin = 0.001;
                    shadowRay.TMax = 1000.0;
                    
                    for (int i = 0; i < sampleCount; i++) {
                        float2 seed = hitPositionWorld.xy + float2(i, i * 2);
                        shadowRay.Direction = perturbLightDirection(lightDir, seed, angularDiameterRad);
                        
                        ShadowPayload shadowPayload;
                        shadowPayload.shadowed = true;
                        
                        uint shadowMissShaderIndex = 1;
                        TraceRay(g_Scene, RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_SKIP_CLOSEST_HIT_SHADER, 
                                 0xFF, 1, 1, shadowMissShaderIndex, shadowRay, shadowPayload);
                        
                        if (!shadowPayload.shadowed) shadowFactor += 1.0;
                    }
                    
                    shadowFactor /= float(sampleCount);
                }
                
                // シェーディングと影を別々に設定
                float3 shading = _Color.rgb * (diffuse + _Ambient);
                payload.color = float4(shading, 1);
                payload.shadowFactor = shadowFactor;
                payload.hit = true;
                
                // その他の表示オプション：
                // payload.color = float4(normalWorld * 0.5 + 0.5, 1); // 法線
                // payload.color = float4(bary, 1); // 重心座標
                // payload.color = float4(lightDir * 0.5 + 0.5, 1); // ライト方向
                // payload.color = float4(lightColor, 1); // ライトカラー
            }

            ENDHLSL
        }
        
        Pass {
            Name "ShadowShader"
            Tags { "LightMode" = "Raytracing" }

            HLSLPROGRAM
            #pragma raytracing test

            [shader("anyhit")]
            void ShadowAnyHit(inout ShadowPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes) {
                // 何かにヒットしたので影になる
                payload.shadowed = true;
                AcceptHitAndEndSearch();
            }

            ENDHLSL
        }
    }
    FallBack "Diffuse"
}