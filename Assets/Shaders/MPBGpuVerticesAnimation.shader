// SRP Batch: support
// GPU Instancing: support 
Shader "GPUSkin/MPBGpuVerticesAnimation" 
{
    Properties 
    {
        _BaseMap("Albedo (RGB)", 2D) = "white" {}
        _AnimationTex("AnimationTex", 2D) = "white" {}
    }

	SubShader
	{
	    HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
		    {
			    float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                float2 vertIndex : TEXCOORD1;
				//float4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
		    };
		    struct Varyings
		    {
			    float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
		    };
		    
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                // 动画纹理尺寸信息
                float4 _AnimationTex_TexelSize;
            CBUFFER_END
            
            UNITY_INSTANCING_BUFFER_START(Props)
                // put more per-instance properties here
                // x: frameIndex y: blendFrameIndex z: blendProgress
                UNITY_DEFINE_INSTANCED_PROP(float3, _AnimatorData)			
            UNITY_INSTANCING_BUFFER_END(Props)
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

			//  动画纹理
			sampler2D _AnimationTex;

			Varyings Vertex(Attributes input)
			{
				UNITY_SETUP_INSTANCE_ID(input);
				Varyings output;
				
                float3 animatorData = UNITY_ACCESS_INSTANCED_PROP(Props, _AnimatorData);
                float frameIndex = animatorData.x;
                float blendFrameIndex = animatorData.y;
                float blendProgress = animatorData.z;

                float vertexIndex = input.vertIndex[0] + 0.5;	// 采样要做半个像素的偏移
                float4 vertexUV1 = float4((vertexIndex) / _AnimationTex_TexelSize.z, (frameIndex + 0.5) / _AnimationTex_TexelSize.w, 0, 0);
                float4 pos = tex2Dlod(_AnimationTex, vertexUV1);

                float4 blend_vertexUV1 = float4(vertexIndex / _AnimationTex_TexelSize.z, (blendFrameIndex + 0.5) / _AnimationTex_TexelSize.w, 0, 0);
                float4 blend_pos = tex2Dlod(_AnimationTex, blend_vertexUV1);

                pos = lerp(pos, blend_pos, blendProgress);

                output.positionCS = TransformObjectToHClip(pos.xyz);
				output.uv = input.texcoord;

				return output;
			}

			half4 Fragment(Varyings input) : SV_Target
			{
				half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);;
				return col;
			}


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
			float3 _LightDirection;
			float3 _LightPosition;

			float4 GetShadowPositionHClip(float3 positionOS, float3 normalOS)
			{
			    float3 positionWS = TransformObjectToWorld(positionOS.xyz);
			    float3 normalWS = TransformObjectToWorldNormal(normalOS);

			    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
			        float3 lightDirectionWS = normalize(_LightPosition - positionWS);
			    #else
			        float3 lightDirectionWS = _LightDirection;
			    #endif

			    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

			    #if UNITY_REVERSED_Z
			        positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
			    #else
			        positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
			    #endif

			    return positionCS;
			}

            Varyings ShadowPassVertex(Attributes input)
            {
				UNITY_SETUP_INSTANCE_ID(input);
				Varyings output;
				
                float3 animatorData = UNITY_ACCESS_INSTANCED_PROP(Props, _AnimatorData);
                float frameIndex = animatorData.x;
                float blendFrameIndex = animatorData.y;
                float blendProgress = animatorData.z;

                float vertexIndex = input.vertIndex[0] + 0.5;	// 采样要做半个像素的偏移
                float4 vertexUV1 = float4((vertexIndex) / _AnimationTex_TexelSize.z, (frameIndex + 0.5) / _AnimationTex_TexelSize.w, 0, 0);
                float4 pos = tex2Dlod(_AnimationTex, vertexUV1);

                float4 blend_vertexUV1 = float4(vertexIndex / _AnimationTex_TexelSize.z, (blendFrameIndex + 0.5) / _AnimationTex_TexelSize.w, 0, 0);
                float4 blend_pos = tex2Dlod(_AnimationTex, blend_vertexUV1);

                pos = lerp(pos, blend_pos, blendProgress);

                output.positionCS = GetShadowPositionHClip(pos.xyz, input.normalOS);
               
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            

	    ENDHLSL
	
        //pass to render object
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque"  }
        LOD 100
        Pass
        {
            Tags { "RenderPipeline" = "UniversalPipeline" "LightMode"="UniversalForward" }
            HLSLPROGRAM
                #pragma target 3.5
                #pragma multi_compile_instancing
                
                #pragma vertex Vertex
                #pragma fragment Fragment
            ENDHLSL
        }

		// Shadow Caster
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            // -------------------------------------
            // Universal Pipeline keywords

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            ENDHLSL
        }
        
	}

}