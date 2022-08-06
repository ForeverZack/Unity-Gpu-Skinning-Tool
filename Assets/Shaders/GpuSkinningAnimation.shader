// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/GpuSkinningAnimation" {
	Properties {
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Color ("Color", Color) = (1,1,1,1)
        _AnimationTex("Animation Texture", 2D) = "white" {}
		_AnimationTexSize("Animation Texture Size", Vector) = (0, 0, 0, 0)

		_BoneNum("Bone Num", Int) = 0
        _FrameIndex("Frame Index", Range(0.0, 196)) = 0.0
		_BlendFrameIndex("Blend Frame Index", Range(0.0, 282)) = 0.0
		_BlendProgress("Blend Progress", Range(0.0, 1.0)) = 0.0
	}

	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _Color;

			//  动画纹理
			sampler2D _AnimationTex;
			float4 _AnimationTex_ST;
			float4 _AnimationTexSize;

			int _BoneNum;
			// 当前动画第几帧
			int _FrameIndex;
			// 下一个动画在第几帧
			int _BlendFrameIndex;
			// 下一个动画的融合程度
			float _BlendProgress;

		// #pragma target 3.0
		// #pragma multi_compile_instancing
			// UNITY_INSTANCING_BUFFER_START(Props)
			// 	// put more per-instance properties here
			// UNITY_INSTANCING_BUFFER_END(Props)

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

			float4 indexToUV(float index)
			{
				int iIndex = trunc(index+0.5);
				int row = (int)(iIndex / _AnimationTexSize.x);
				float col = iIndex - row*_AnimationTexSize.x;
				return float4((col+0.5)/_AnimationTexSize.x, (row+0.5)/_AnimationTexSize.y, 0, 0);
			}

			#include "UnityCG.cginc"
			#pragma multi_compile_instancing
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float4 boneIndices : TEXCOORD1;
				float4 boneWeights : TEXCOORD2;
				//float4 color : COLOR;
				half3 normal : NORMAL;
				 UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			v2f vert(appdata v)
			{
				UNITY_SETUP_INSTANCE_ID(v);

				v2f o;
				// UNITY_SETUP_INSTANCE_ID(v);

				float4 boneIndices = v.boneIndices;
				float4 boneWeights = v.boneWeights;
				//o.color = v.uv2;
				// half frameIndex = UNITY_ACCESS_INSTANCED_PROP(_FrameIndex_arr, _FrameIndex);
				float4 boneUV1;
				float4 boneUV2;
				float4 boneUV3;
				float4 boneUV4;
				int frameDataPixelIndex;
				static const int DEFAULT_PER_FRAME_BONE_DATASPACE = 2;

				// 正在播放的动画
				frameDataPixelIndex = (_BoneNum * _FrameIndex) * DEFAULT_PER_FRAME_BONE_DATASPACE;
				// bone0
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[0]*DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[0]*DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				float4x4 bone0_matrix = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));
				// bone1
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[1]*DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[1]*DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				float4x4 bone1_matrix = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));
				// bone2
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[2]*DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[2]*DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				float4x4 bone2_matrix = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));
				// bone3
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[3]*DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[3]*DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				float4x4 bone3_matrix = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));


				
				// float progress = UNITY_ACCESS_INSTANCED_PROP(_FrameIndex_arr, _FusionProgress);
				// float frameIndexFusion = UNITY_ACCESS_INSTANCED_PROP(_FrameIndex_arr, _FrameIndexFusion);
				// 动画Blend
				frameDataPixelIndex = _BoneNum * _BlendFrameIndex * DEFAULT_PER_FRAME_BONE_DATASPACE;

				// bone0
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[0]*DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[0]*DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				float4x4 bone0_matrix_blend = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));
				// bone1
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[1]*DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[1]*DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				boneUV3 = indexToUV(frameDataPixelIndex + boneIndices[1]*DEFAULT_PER_FRAME_BONE_DATASPACE + 2);
				boneUV4 = indexToUV(frameDataPixelIndex + boneIndices[1]*DEFAULT_PER_FRAME_BONE_DATASPACE + 3);
				float4x4 bone1_matrix_blend = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));
				// bone2
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[2]*DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[2]*DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				float4x4 bone2_matrix_blend = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));
				// bone3
				boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[3]*DEFAULT_PER_FRAME_BONE_DATASPACE);
				boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[3]*DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
				float4x4 bone3_matrix_blend = DualQuaternionToMatrix(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2));
				bone0_matrix = lerp(bone0_matrix, bone0_matrix_blend, _BlendProgress);
				bone1_matrix = lerp(bone1_matrix, bone1_matrix_blend, _BlendProgress);
				bone2_matrix = lerp(bone2_matrix, bone2_matrix_blend, _BlendProgress);
				bone3_matrix = lerp(bone3_matrix, bone3_matrix_blend, _BlendProgress);
				

				float4 pos =
					mul(bone0_matrix, v.vertex) * boneWeights[0] +
					mul(bone1_matrix, v.vertex) * boneWeights[1] +
					mul(bone2_matrix, v.vertex) * boneWeights[2] +
					mul(bone3_matrix, v.vertex) * boneWeights[3];
				// o.vertex = UnityWorldToClipPos(pos);
				// o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.vertex = UnityObjectToClipPos(pos);
				o.uv = v.uv;

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);
				return col;
			} 

			
			ENDCG
		}

	}
	FallBack "Diffuse"
}
