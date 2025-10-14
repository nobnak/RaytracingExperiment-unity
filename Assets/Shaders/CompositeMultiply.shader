Shader "Hidden/CompositeMultiply" {
    Properties {
        [MainTexture] _MainTex ("Color Texture", 2D) = "white" {}
        _ShadowTex ("Shadow Texture", 2D) = "white" {}
    }
    
    SubShader {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        
        Pass {
            Name "CompositeMultiply"
            ZTest Always ZWrite Off Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_ShadowTex);
            SAMPLER(sampler_ShadowTex);
            
            Varyings vert(Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 shadow = SAMPLE_TEXTURE2D(_ShadowTex, sampler_ShadowTex, input.uv);
                return color * shadow;
            }
            ENDHLSL
        }
    }
}

