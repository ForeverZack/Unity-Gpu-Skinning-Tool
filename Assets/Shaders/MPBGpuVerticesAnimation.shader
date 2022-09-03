Shader "Custom/MPBGpuVerticesAnimation" {
Properties {
	_MainTex("MainTex", 2D) = "white" {}
	_AnimationTex("AnimationTex", 2D) = "white" {}
}

	SubShader{
			//pass to render object
			Tags {
				"Queue" = "Geometry"
				"RenderType" = "Opaque" 
			}
			LOD 100
			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#include "UnityCG.cginc"

				struct appdata_t {
					half4 vertex : POSITION;
					half3 normal : NORMAL;
					half2 texcoord : TEXCOORD0;
					float2 vertIndex : TEXCOORD1;

					UNITY_VERTEX_INPUT_INSTANCE_ID
				};

				struct v2f {
					half4 vertex : SV_POSITION;
					float2 texcoord : TEXCOORD0;
				};

				sampler2D _AnimationTex;
				float4 _AnimationTex_TexelSize;	// x contains 1.0 / width; y contains 1.0 / height; z contains width; w contains height

				float convertFloat16BytesToHalf(int data1, int data2)
				{
					float f_data2 = data2;
					int flag = (data1 / 128);
					float result = data1 - flag * 128	// 整数部分
						+ f_data2 / 256;	// 小数部分

					result = result - 2 * flag*result;		//1: 负  0:正

					return result;
				}

				float4 convertColors2Halfs(float3 color1, float3 color2)
				{
					return float4(convertFloat16BytesToHalf(floor(color1.r * 255 + 0.5), floor(color1.g * 255 + 0.5))
								, convertFloat16BytesToHalf(floor(color1.b * 255 + 0.5), floor(color2.r * 255 + 0.5))
								, convertFloat16BytesToHalf(floor(color2.g * 255 + 0.5), floor(color2.b * 255 + 0.5))
								, 1);
				}

				#include "UnityCG.cginc"
				#pragma target 3.5
				#pragma multi_compile_instancing
				

				
                UNITY_INSTANCING_BUFFER_START(Props)
                    // put more per-instance properties here
                    // x: frameIndex y: blendFrameIndex z: blendProgress
                    UNITY_DEFINE_INSTANCED_PROP(float3, _AnimatorData)			
                UNITY_INSTANCING_BUFFER_END(Props)

				sampler2D _MainTex;
				half4 _MainTex_ST;

				v2f vert(appdata_t v)
				{
					UNITY_SETUP_INSTANCE_ID(v);

					v2f o;
					
					float3 animatorData = UNITY_ACCESS_INSTANCED_PROP(Props, _AnimatorData);
                    float frameIndex = animatorData.x;
                    float blendFrameIndex = animatorData.y;
                    float blendProgress = animatorData.z;

					float vertexIndex = v.vertIndex[0] + 0.5;	// 采样要做半个像素的偏移
					float4 vertexUV1 = float4((vertexIndex) / _AnimationTex_TexelSize.z, (frameIndex + 0.5) / _AnimationTex_TexelSize.w, 0, 0);
					float4 pos = tex2Dlod(_AnimationTex, vertexUV1);

					float4 blend_vertexUV1 = float4(vertexIndex / _AnimationTex_TexelSize.z, (blendFrameIndex + 0.5) / _AnimationTex_TexelSize.w, 0, 0);
					float4 blend_pos = tex2Dlod(_AnimationTex, blend_vertexUV1);

					pos = lerp(pos, blend_pos, blendProgress);

					o.vertex = UnityObjectToClipPos(pos);
					o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

					return o;
				}


				fixed4 frag(v2f i) : SV_Target
				{
					fixed4 col = tex2D(_MainTex, i.texcoord);
					return col;
				}
				ENDCG
			}

	}

}