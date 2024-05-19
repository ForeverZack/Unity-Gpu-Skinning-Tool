// SRP Batch: support
// GPU Instancing: support 
Shader "GPUSkin/NoiseGpuVerticesAnimation" 
{
	Properties
	{
		_BaseMap("Albedo (RGB)", 2D) = "white" {}
		_Color("Color", Color) = (1,1,1,1)
		_AnimationTex("Animation Texture", 2D) = "white" {}
		_AnimationNormalTex("Animation Normal Texture", 2D) = "white" {}

		_BoneNum("Bone Num", Int) = 0
		_FrameIndex("Frame Index", Range(0.0, 196)) = 0.0
		_BlendFrameIndex("Blend Frame Index", Range(0.0, 282)) = 0.0
		_BlendProgress("Blend Progress", Range(0.0, 1.0)) = 0.0

		_FrameIndexTex("Frame Index Texture", 2D) = "black" {}
		_PerPixelWorldSize("Per Pixel Size", Float) = 0.25
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
                // 当前动画第几帧
                int _FrameIndex;
                // 下一个动画在第几帧
                int _BlendFrameIndex;
                // 下一个动画的融合程度
                float _BlendProgress;
                // instance对应根节点的矩阵 (world -> local)
                float4x4 _WorldToAnimRootNodeMatrix;
                // 动画当前帧纹理尺寸
                float4 _FrameIndexTex_TexelSize;	// x contains 1.0 / width; y contains 1.0 / height; z contains width; w contains height
                // 动画当前纹理的单位像素对应世界上的尺寸
                float _PerPixelWorldSize;
            CBUFFER_END
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

			//  动画纹理
			sampler2D _AnimationTex;
            // 动画法线纹理
            sampler2D _AnimationNormalTex;
			
            // 动画当前帧纹理 (存储对应位置模型的动画FrameIndex)
            sampler2D _FrameIndexTex;
            
		
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

            //Varyings Vertex(Attributes input, uint vid : SV_VertexID)
            Varyings Vertex(Attributes input)
			{
				UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;

                // 计算对_Frame
                float4 animRootLocalPosition = mul(_WorldToAnimRootNodeMatrix, mul(UNITY_MATRIX_M, float4(0,0,0,1)));
                float4 frameIndexTexUV = float4(animRootLocalPosition.x / (_PerPixelWorldSize*_FrameIndexTex_TexelSize.z), animRootLocalPosition.z / (_PerPixelWorldSize*_FrameIndexTex_TexelSize.w), 0, 0);
                int frameOffset = round(tex2Dlod(_FrameIndexTex, frameIndexTexUV).r*255);

                //int vertexIndex = vid;
                float vertexIndex = input.vertIndex[0] + 0.5;	// 采样要做半个像素的偏移
                float4 vertexUV1 = float4((vertexIndex) * _AnimationTex_TexelSize.x, ((_FrameIndex+frameOffset) * 2 + 0.5) * _AnimationTex_TexelSize.y, 0, 0);
                float4 pos = tex2Dlod(_AnimationTex, vertexUV1);

                float4 blend_vertexUV1 = float4(vertexIndex * _AnimationTex_TexelSize.x, ((_BlendFrameIndex + frameOffset) * 2 + 0.5) * _AnimationTex_TexelSize.y, 0, 0);
                float4 blend_pos = tex2Dlod(_AnimationTex, blend_vertexUV1);

                pos = lerp(pos, blend_pos, _BlendProgress);

                // o.uv = TRANSFORM_TEX(v.uv, _MainTex);
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
    
        Tags { "RenderType" = "Opaque" }
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
