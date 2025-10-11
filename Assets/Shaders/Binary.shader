Shader "Custom/Binary" {
    Properties { 
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
    }

    HLSLINCLUDE
    #include "Assets/ShaderLibrary/Payload.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
    float4 _Color;
    sampler2D _MainTex;
    CBUFFER_END
    ENDHLSL

    SubShader {

        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass {
            Name "BinaryRayTracing"
            Tags { "LightMode" = "Raytracing" }

            HLSLPROGRAM
            #pragma raytracing test
            struct AttributeData {
                float2 barycentrics;
            };

            [shader("closesthit")]
            void ClosestHitMain(inout RayPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
            {
                payload.color = _Color;
            }

            ENDHLSL
        }
    }
}