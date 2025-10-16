Shader "Hidden/CompositeMultiply" {
    Properties {
        [MainTexture] _MainTex ("Color Texture", 2D) = "white" {}
        _ShadowTex ("Shadow Texture", 2D) = "white" {}
        _CurrentTex ("Current Frame", 2D) = "white" {}
        _PrevTex ("Previous Frame", 2D) = "white" {}
        _BlendFactor ("Blend Factor", Range(0, 1)) = 0.9
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
        
        Pass {
            Name "TemporalBlend"
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
            
            TEXTURE2D(_CurrentTex);
            SAMPLER(sampler_CurrentTex);
            TEXTURE2D(_PrevTex);
            SAMPLER(sampler_PrevTex);
            float _BlendFactor;
            
            Varyings vert(Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target {
                half4 current = SAMPLE_TEXTURE2D(_CurrentTex, sampler_CurrentTex, input.uv);
                half4 prev = SAMPLE_TEXTURE2D(_PrevTex, sampler_PrevTex, input.uv);
                
                // EMA: result = prev * blend + current * (1 - blend)
                return lerp(current, prev, _BlendFactor);
            }
            ENDHLSL
        }
    }
}

