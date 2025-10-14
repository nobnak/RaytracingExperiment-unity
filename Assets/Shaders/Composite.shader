Shader "Hidden/Composite" {
    Properties {
        [MainTex] _MainTex ("Main Texture", 2D) = "white" {}
        _BlendFactor ("Blend Factor", Range(0, 1)) = 0.9
    }
    
    SubShader {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        
        HLSLINCLUDE
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
        TEXTURE2D(_SecondTex);
        SAMPLER(sampler_SecondTex);
        float _BlendFactor;
        
        Varyings vert(Attributes input) {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            return output;
        }
        ENDHLSL
        
        // Pass 0: Multiply (Color * Shadow)
        Pass {
            Name "Multiply"
            ZTest Always ZWrite Off Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            half4 frag(Varyings input) : SV_Target {
                half4 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 second = SAMPLE_TEXTURE2D(_SecondTex, sampler_SecondTex, input.uv);
                return main * second;
            }
            ENDHLSL
        }
        
        // Pass 1: Temporal Blend (EMA)
        Pass {
            Name "TemporalBlend"
            ZTest Always ZWrite Off Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            half4 frag(Varyings input) : SV_Target {
                half4 current = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 prev = SAMPLE_TEXTURE2D(_SecondTex, sampler_SecondTex, input.uv);
                
                // EMA: result = prev * blend + current * (1 - blend)
                return lerp(current, prev, _BlendFactor);
            }
            ENDHLSL
        }
    }
}

