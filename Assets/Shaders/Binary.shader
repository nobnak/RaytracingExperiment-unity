Shader "Custom/Binary" {
    Properties { 
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
    }

    HLSLINCLUDE
    #include "Assets/ShaderLibrary/Payload.hlsl"
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
                payload.color = float4(1, 0, 0, 1);
            }

            ENDHLSL
        }
        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS   : POSITION;
            };

            struct Varyings {
                float4 positionHCS  : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            sampler2D _MainTex;
            CBUFFER_END

            Varyings vert(Attributes IN) {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag() : SV_Target {
                half4 customColor = _Color;
                return customColor;
            }
            ENDHLSL
        }
    }
}