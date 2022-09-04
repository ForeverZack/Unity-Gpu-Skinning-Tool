// SRP Batch: support
// GPU Instancing: support
Shader "GPUSkin/GpuSkinningAnim_Inst" 
{
	Properties 
	{
		_BaseMap ("Albedo (RGB)", 2D) = "white" {}
		_Color ("Color", Color) = (1,1,1,1)
        _AnimationTex("Animation Texture", 2D) = "white" {}

		_BoneNum("Bone Num", Int) = 0
	}

	SubShader 
	{
	    HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
		    {
			    float4 positionOS : POSITION;
                half3 normal : NORMAL;
                float2 texcoord : TEXCOORD0;
				float4 boneIndices : TEXCOORD1;
				float4 boneWeights : TEXCOORD2;
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
                // 骨骼数量
                int _BoneNum;
            CBUFFER_END
            
            UNITY_INSTANCING_BUFFER_START(Props)
				// put more per-instance properties here
				UNITY_DEFINE_INSTANCED_PROP(int, _FrameIndex) // 当前动画第几帧			
				UNITY_DEFINE_INSTANCED_PROP(int, _BlendFrameIndex) // 下一个动画在第几帧			
				UNITY_DEFINE_INSTANCED_PROP(float, _BlendProgress) // 下一个动画的融合程度				
			UNITY_INSTANCING_BUFFER_END(Props)
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

			//  动画纹理
			sampler2D _AnimationTex;
		
			float4x4 QuaternionToMatrix(float4 vec)
			{
				float4x4 ret;
				ret._11 = 2.0 * (vec.x * vec.x + vec.w * vec.w) - 1;
				ret._21 = 2.0 * (vec.x * vec.y + vec.z * vec.w);
				ret._31 = 2.0 * (vec.x * vec.z - vec.y * vec.w);
				ret._41 = 0.0;
				ret._12 = 2.0 * (vec.x * vec.y - vec.z * vec.w);
				ret._22 = 2.0 * (vec.y * vec.y + vec.w * vec.w) - 1;
				ret._32 = 2.0 * (vec.y * vec.z + vec.x * vec.w);
				ret._42 = 0.0;
				ret._13 = 2.0 * (vec.x * vec.z + vec.y * vec.w);
				ret._23 = 2.0 * (vec.y * vec.z - vec.x * vec.w);
				ret._33 = 2.0 * (vec.z * vec.z + vec.w * vec.w) - 1;
				ret._43 = 0.0;
				ret._14 = 0.0;
				ret._24 = 0.0;
				ret._34 = 0.0;
				ret._44 = 1.0;
				return ret;
			}

			float4x4 DualQuaternionToMatrix(float4 m_dual, float4 m_real)
			{
				float4x4 rotationMatrix = QuaternionToMatrix(float4(m_dual.x, m_dual.y, m_dual.z, m_dual.w));
				float4x4 translationMatrix;
				translationMatrix._11_12_13_14 = float4(1, 0, 0, 0);
				translationMatrix._21_22_23_24 = float4(0, 1, 0, 0);
				translationMatrix._31_32_33_34 = float4(0, 0, 1, 0);
				translationMatrix._41_42_43_44 = float4(0, 0, 0, 1);
				translationMatrix._14 = m_real.x;
				translationMatrix._24 = m_real.y;
				translationMatrix._34 = m_real.z;
				float4x4 scaleMatrix;
				scaleMatrix._11_12_13_14 = float4(1, 0, 0, 0);
				scaleMatrix._21_22_23_24 = float4(0, 1, 0, 0);
				scaleMatrix._31_32_33_34 = float4(0, 0, 1, 0);
				scaleMatrix._41_42_43_44 = float4(0, 0, 0, 1);
				scaleMatrix._11 = m_real.w;
				scaleMatrix._22 = m_real.w;
				scaleMatrix._33 = m_real.w;
				scaleMatrix._44 = 1;
				float4x4 M = mul(translationMatrix, mul(rotationMatrix, scaleMatrix));
				return M;
			}

			float4 indexToUV(float index)
			{
				int iIndex = trunc(index + 0.5);
				int row = (int)(iIndex * _AnimationTex_TexelSize.x);
				float col = iIndex - row*_AnimationTex_TexelSize.z;
				return float4((col+0.5)*_AnimationTex_TexelSize.x, (row+0.5) *_AnimationTex_TexelSize.y, 0, 0);
			}

			float convertFloat16BytesToHalf(int data1, int data2)
			{
				float f_data2 = data2;
				int flag = (data1/128);
				float result = data1-flag*128	// 整数部分
								+ f_data2/256;	// 小数部分
				
				result = result - 2*flag*result;		//1: 负  0:正

				return result;
			}

			float4 convertColors2Halfs(float4 color1, float4 color2)
			{
				return float4(convertFloat16BytesToHalf(floor(color1.r * 255 + 0.5), floor(color1.g * 255 + 0.5))
							, convertFloat16BytesToHalf(floor(color1.b * 255 + 0.5), floor(color1.a * 255 + 0.5))
							, convertFloat16BytesToHalf(floor(color2.r * 255 + 0.5), floor(color2.g * 255 + 0.5))
							, convertFloat16BytesToHalf(floor(color2.b * 255 + 0.5), floor(color2.a * 255 + 0.5)));
			}

			Varyings Vertex(Attributes input)
			{
				UNITY_SETUP_INSTANCE_ID(input);
				Varyings output;

				float4 boneIndices = input.boneIndices;
				float4 boneWeights = input.boneWeights;
				
				int frameIndex = UNITY_ACCESS_INSTANCED_PROP(Props, _FrameIndex);
				int blendFrameIndex = UNITY_ACCESS_INSTANCED_PROP(Props, _BlendFrameIndex);
                float blendProgress = UNITY_ACCESS_INSTANCED_PROP(Props, _BlendProgress);
                float4 boneUV1;
				float4 boneUV2;
				int frameDataPixelIndex;
				const int DEFAULT_PER_FRAME_BONE_DATASPACE = 2;

				// 正在播放的动画
				frameDataPixelIndex = _BoneNum * frameIndex * DEFAULT_PER_FRAME_BONE_DATASPACE;
				// bone0
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[0] * DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[0] * DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				float4x4 bone0_matrix = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));
				// bone1
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[1] * DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[1] * DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				float4x4 bone1_matrix = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));
				// bone2
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[2] * DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[2] * DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				float4x4 bone2_matrix = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));
				// bone3
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[3] * DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[3] * DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				float4x4 bone3_matrix = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));
				
				// 动画Blend
				frameDataPixelIndex = _BoneNum * blendFrameIndex * DEFAULT_PER_FRAME_BONE_DATASPACE;
                // bone0
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[0]*DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[0]*DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				float4x4 bone0_matrix_blend = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));
				// bone1
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[1]*DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[1]*DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				float4x4 bone1_matrix_blend = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));
				// bone2
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[2]*DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[2]*DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				float4x4 bone2_matrix_blend = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));
				// bone3
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[3]*DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[3]*DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				float4x4 bone3_matrix_blend = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));
				bone0_matrix = lerp(bone0_matrix, bone0_matrix_blend, blendProgress);
				bone1_matrix = lerp(bone1_matrix, bone1_matrix_blend, blendProgress);
				bone2_matrix = lerp(bone2_matrix, bone2_matrix_blend, blendProgress);
				bone3_matrix = lerp(bone3_matrix, bone3_matrix_blend, blendProgress);

				float4 pos =
					mul(bone0_matrix, input.positionOS) * boneWeights[0] +
					mul(bone1_matrix, input.positionOS) * boneWeights[1] +
					mul(bone2_matrix, input.positionOS) * boneWeights[2] +
					mul(bone3_matrix, input.positionOS) * boneWeights[3];
				output.positionCS = TransformObjectToHClip(pos.xyz);
				output.uv = input.texcoord;

				return output;
			}

			half4 Fragment(Varyings input) : SV_Target
			{
				half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);;
				return col;
			}
            

	    ENDHLSL
	
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
		    Tags { "RenderPipeline" = "UniversalPipeline" "LightMode"="UniversalForward" }
			HLSLPROGRAM
                #pragma target 3.0
                #pragma multi_compile_instancing
                
                #pragma vertex Vertex
                #pragma fragment Fragment
			ENDHLSL
		}

	}
}
