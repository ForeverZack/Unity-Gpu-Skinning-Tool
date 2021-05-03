// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

// Unlit shader. Simplest possible textured shader.
// - no lighting
// - no lightmap support
// - no per-material color

Shader "Custom/ModifyModelMatGpuVerticesAnimation" {
Properties {
	_MainTex("MainTex", 2D) = "white" {}
	_AnimationTex("AnimationTex", 2D) = "white" {}
	_FogTex("Fog Texture", 2D) = "white" {}
	_ColorMaskTex("ColorMask Texture", 2D) = "white" {}
 
	// _MaskTex("MaskTex",2D) = "black"{} 
    _Opacity("_Opacity", Float) = 1.0

	// Light
	_AmbientColor("AmbientColor",Color) = (0.863,0.863,0.863,1)
	_LightDir("LightDir", vector) = (-400, 1000, 200, 1)
	_LightColor("LightColor", Color) = (0.64,0.64,0.64,1)
	_LightIntensity("LightIntensity",Float) = 1.2
	//_Gloss("Gloss",Float) = 8
	_SpecPower("Spec Power", Range(0.0, 40)) = 24
	_SpecStrength("Spec Strength", Range(0.0, 2)) = 0.5

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
					UNITY_FOG_COORDS(1)
					fixed3 maskCol : TEXCOORD2;
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

				sampler2D _MainTex;
				half4 _MainTex_ST;
				fixed _Opacity;

				sampler2D _ColorMaskTex;
				float4 _ColorMaskTex_TexelSize;	// x contains 1.0 / width; y contains 1.0 / height; z contains width; w contains height


				fixed4 _AmbientColor;
				float4 _LightDir;	// world-space
				half _LightIntensity;
				fixed4 _LightColor;
				//half _Gloss;
				fixed _SpecPower;
				fixed _SpecStrength;

				v2f vert(appdata_t v)
				{
					UNITY_SETUP_INSTANCE_ID(v);

					v2f o;

					float4x4 modelMat = unity_ObjectToWorld;
					float scaleY = modelMat[1][1];
					float scaleZ = length(float3(modelMat[0][2], modelMat[1][2], modelMat[2][2]));

					int frameIndex = floor(scaleY);
					int colorIndex = frac(scaleY) * 100 + 0.5;
					int blendFrameIndex = scaleZ%1000;
					float blendProgress = frac(scaleZ);
					int flag = scaleZ / 1000;
					int scaleXFlag = 1 - 2 * flag;
					//int blendFrameIndex = 0;
					//float blendProgress = 0;

					modelMat[1][1] = length(float3(modelMat[0][0], modelMat[1][0], modelMat[2][0]));	// y直接使用x方向缩放的绝对值		第1个维度表示行
					modelMat[0][2] = -modelMat[2][0] * scaleXFlag;
					modelMat[2][2] = modelMat[0][0] * scaleXFlag;

					float vertexIndex = v.vertIndex[0] + 0.5;	// 采样要做半个像素的偏移
					float4 vertexUV1 = float4((vertexIndex) / _AnimationTex_TexelSize.z, (frameIndex * 2 + 0.5) / _AnimationTex_TexelSize.w, 0, 0);
					float4 vertexUV2 = float4((vertexIndex) / _AnimationTex_TexelSize.z, (frameIndex * 2 + 1.5) / _AnimationTex_TexelSize.w, 0, 0);
					float4 pos = convertColors2Halfs(tex2Dlod(_AnimationTex, vertexUV1), tex2Dlod(_AnimationTex, vertexUV2));

					float4 blend_vertexUV1 = float4(vertexIndex / _AnimationTex_TexelSize.z, (blendFrameIndex * 2 + 0.5) / _AnimationTex_TexelSize.w, 0, 0);
					float4 blend_vertexUV2 = float4(vertexIndex / _AnimationTex_TexelSize.z, (blendFrameIndex * 2 + 1.5) / _AnimationTex_TexelSize.w, 0, 0);
					float4 blend_pos = convertColors2Halfs(tex2Dlod(_AnimationTex, blend_vertexUV1), tex2Dlod(_AnimationTex, blend_vertexUV2));

					pos = lerp(pos, blend_pos, blendProgress);

					float4 worldPos  = mul(modelMat, pos);
					o.vertex = mul(UNITY_MATRIX_VP, worldPos);
					o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

					float4 maskColorUV = float4((colorIndex+0.5)*_ColorMaskTex_TexelSize.x, 0.5, 0, 0);
					o.maskCol = tex2Dlod(_ColorMaskTex, maskColorUV);

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