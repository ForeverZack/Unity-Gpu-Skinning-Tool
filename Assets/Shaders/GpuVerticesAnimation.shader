// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/GpuVerticesAnimation" {
	Properties{
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_Color("Color", Color) = (1,1,1,1)
		_AnimationTex("Animation Texture", 2D) = "white" {}
		_AnimationTexSize("Animation Texture Size", Vector) = (0, 0, 0, 0)

		_BoneNum("Bone Num", Int) = 0
		_FrameIndex("Frame Index", Range(0.0, 196)) = 0.0
		_BlendFrameIndex("Blend Frame Index", Range(0.0, 282)) = 0.0
		_BlendProgress("Blend Progress", Range(0.0, 1.0)) = 0.0
	}

		SubShader{
			Tags { "RenderType" = "Opaque" }
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


				float convertFloat16BytesToHalf(int data1, int data2)
				{
					float result = 16 * (data1 >> 6 & 0x01) + 8 * (data1 >> 5 & 0x01) + 4 * (data1 >> 4 & 0x01) + 2 * (data1 >> 3 & 0x01) + 1 * (data1 >> 2 & 0x01)	// 整数部分
						+ 0.5*(data1 >> 1 & 0x01) + 0.25*(data1 & 0x01) + 0.125*(data2 >> 7 & 0x01) + 0.0625*(data2 >> 6 & 0x01) + 0.03125*(data2 >> 5 & 0x01)	// 小数部分
						+ 0.015625*(data2 >> 4 & 0x01) + 0.0078125*(data2 >> 3 & 0x01) + 0.00390625*(data2 >> 2 & 0x01) + 0.001953125*(data2 >> 1 & 0x01) + 0.0009765625*(data2 & 0x01);

					int flag = (data1 >> 7 & 0x01);
					result = result - 2 * (1 - flag)*result;		//0: 负  1:正

					return result;
				}

				float4 convertColors2Halfs(float4 color1, float4 color2)
				{
					return float4(convertFloat16BytesToHalf(color1.r * 255, color1.g * 255), convertFloat16BytesToHalf(color1.b * 255, color1.a * 255), convertFloat16BytesToHalf(color2.r * 255, color2.g * 255), convertFloat16BytesToHalf(color2.b * 255, color2.a * 255));
				}

				#include "UnityCG.cginc"
				#pragma multi_compile_instancing
				struct appdata
				{
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
					float2 vertIndex : TEXCOORD1;
					//float4 color : COLOR;
					half3 normal : NORMAL;
					 UNITY_VERTEX_INPUT_INSTANCE_ID
				};

				struct v2f
				{
					float4 vertex : SV_POSITION;
					float2 uv : TEXCOORD0;
					fixed3 diff : TEXCOORD2;
					fixed3 spec : TEXCOORD3;
				};

				//v2f vert(appdata v, uint vid : SV_VertexID)
				v2f vert(appdata v)
				{
					UNITY_SETUP_INSTANCE_ID(v);

					v2f o;

					//int vertexIndex = vid;
					float vertexIndex = v.vertIndex[0] + 0.5;	// 采样要做半个像素的偏移
					float4 vertexUV1 = float4((vertexIndex) / _AnimationTexSize.x, (_FrameIndex * 2 + 0.5) / _AnimationTexSize.y, 0, 0);
					float4 vertexUV2 = float4((vertexIndex) / _AnimationTexSize.x, (_FrameIndex * 2 + 1.5) / _AnimationTexSize.y, 0, 0);
					float4 pos = convertColors2Halfs(tex2Dlod(_AnimationTex, vertexUV1), tex2Dlod(_AnimationTex, vertexUV2));

					float4 blend_vertexUV1 = float4(vertexIndex / _AnimationTexSize.x, (_BlendFrameIndex * 2 + 0.5) / _AnimationTexSize.y, 0, 0);
					float4 blend_vertexUV2 = float4(vertexIndex / _AnimationTexSize.x, (_BlendFrameIndex * 2 + 1.5) / _AnimationTexSize.y, 0, 0);
					float4 blend_pos = convertColors2Halfs(tex2Dlod(_AnimationTex, blend_vertexUV1), tex2Dlod(_AnimationTex, blend_vertexUV2));

					pos = lerp(pos, blend_pos, _BlendProgress);

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
