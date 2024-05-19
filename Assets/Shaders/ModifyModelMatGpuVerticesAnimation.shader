// SRP Batch: support
// GPU Instancing: support 
Shader "GPUSkin/ModifyModelMatGpuVerticesAnimation" 
{
    Properties 
    {
        _BaseMap("Albedo (RGB)", 2D) = "white" {}
        _AnimationTex("AnimationTex", 2D) = "white" {}
    	_AnimationNormalTex("Animation Normal Texture", 2D) = "white" {}
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
            	// 动画法线纹理尺寸信息
                float4 _AnimationNormalTex_TexelSize;
            CBUFFER_END
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

			//  动画纹理
			sampler2D _AnimationTex;
            // 动画法线纹理
            sampler2D _AnimationNormalTex;
		
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

			Varyings Vertex(Attributes input)
			{
				UNITY_SETUP_INSTANCE_ID(input);
				Varyings output;
				
				float4x4 modelMat = UNITY_MATRIX_M;
                float scaleY = modelMat[1][1];
                float scaleZ = length(float3(modelMat[0][2], modelMat[1][2], modelMat[2][2]));

                int frameIndex = floor(scaleY);
                int colorIndex = frac(scaleY) * 100 + 0.5;
                int blendFrameIndex = scaleZ%1000;
                float blendProgress = frac(scaleZ);
                int flag = scaleZ / 1000;
                int scaleXFlag = 1 - 2 * flag;

                modelMat[1][1] = length(float3(modelMat[0][0], modelMat[1][0], modelMat[2][0]));	// y直接使用x方向缩放的绝对值		第1个维度表示行
                modelMat[0][2] = -modelMat[2][0] * scaleXFlag;
                modelMat[2][2] = modelMat[0][0] * scaleXFlag;

                float vertexIndex = input.vertIndex[0] + 0.5;	// 采样要做半个像素的偏移
                float4 vertexUV1 = float4((vertexIndex) * _AnimationTex_TexelSize.x, (frameIndex + 0.5) * _AnimationTex_TexelSize.y, 0, 0);
                float4 pos = tex2Dlod(_AnimationTex, vertexUV1);

                float4 blend_vertexUV1 = float4(vertexIndex * _AnimationTex_TexelSize.x, (blendFrameIndex + 0.5) * _AnimationTex_TexelSize.y, 0, 0);
                float4 blend_pos = tex2Dlod(_AnimationTex, blend_vertexUV1);

                pos = lerp(pos, blend_pos, blendProgress);

                float4 worldPos  = mul(modelMat, pos);

                output.positionCS = mul(UNITY_MATRIX_VP, float4(worldPos.xyz, 1));
				output.uv = input.texcoord;

				return output;
			}

			half4 Fragment(Varyings input) : SV_Target
			{
				half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);;
				return col;
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

	}

}