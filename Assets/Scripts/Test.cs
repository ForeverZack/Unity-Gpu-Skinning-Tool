using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MessagePack;
using System.IO;


public class Test : MonoBehaviour {

	public int count = 100;
	public GameObject templateObj;

	[Range (0, 282)]
	public float frameIndex = 0.0f;
	[Range (0, 282)]
	public float blendFrameIndex = 0.0f;
	[Range (0, 1)]
	public float blendProgress = 0.0f;

	private MaterialPropertyBlock uniforms;
	private Material inst_material;
	private Mesh inst_mesh;
	private Matrix4x4[] matrices;
	private float[] frameIndices;
	private float[] blendFrameIndices;
	private float[] blendProgresses;


	private GpuSkinningAnimData anim_data;

	// Use this for initialization
	void Start () {

		inst_material = templateObj.GetComponent<MeshRenderer>().sharedMaterial;
		inst_mesh = templateObj.GetComponent<MeshFilter>().sharedMesh;

		anim_data = MessagePackSerializer.Deserialize<GpuSkinningAnimData>(templateObj.GetComponent<GpuSkinningInstance>().textAsset.bytes);

        //Texture2D anim_tex = new Texture2D(anim_data.texWidth, anim_data.texHeight, TextureFormat.RGBA32, false, true);
        //anim_tex.filterMode = FilterMode.Point;
        //anim_tex.LoadRawTextureData(anim_data.texBytes);
        //anim_tex.Apply(false, true);

        inst_material.SetInt("_BoneNum", anim_data.totalBoneNum);
		//inst_material.SetTexture("_AnimationTex", anim_tex);
		inst_material.SetVector("_AnimationTexSize", new Vector4(anim_data.texWidth, anim_data.texHeight, 1/anim_data.texWidth, 1/anim_data.texHeight));

		uniforms = new MaterialPropertyBlock();
		matrices = new Matrix4x4[count];
		frameIndices = new float[count];
		blendFrameIndices = new float[count];
		blendProgresses = new float[count];
		int index;
		for (int i=0; i<10; ++i)
		{
			for (int j=0; j<10; ++j)
			{
				index = i*10 + j;
				matrices[index] = Matrix4x4.Translate(new Vector3(i*2, 0, j*2));
				frameIndices[index] = 0;
				blendFrameIndices[index] = 0;
				blendProgresses[index] = 0;
			}
		}
	}
	
	// Update is called once per frame
	void Update () {

		for (int i=0; i<count; ++i)
		{
			frameIndices[i] = frameIndex;
			blendFrameIndices[i] = blendFrameIndex;
			blendProgresses[i] = blendProgress;
		}

		// uniforms.SetFloatArray(GpuSkinningUniforms._FrameIndex, frameIndices);
		// uniforms.SetFloatArray(GpuSkinningUniforms._BlendFrameIndex, blendFrameIndices);
		// uniforms.SetFloatArray(GpuSkinningUniforms._BlendProgress, blendProgresses);		
		uniforms.SetFloatArray("_FrameIndex", frameIndices);
		uniforms.SetFloatArray("_BlendFrameIndex", blendFrameIndices);
		uniforms.SetFloatArray("_BlendProgress", blendProgresses);

		// uniforms.SetFloat(GpuSkinningUniforms._FrameIndex, frameIndex);
		// uniforms.SetFloat(GpuSkinningUniforms._BlendFrameIndex, blendFrameIndex);
		// uniforms.SetFloat(GpuSkinningUniforms._BlendProgress, blendProgress);
		inst_material.SetFloat("_FrameIndexTest", frameIndex);

		Graphics.DrawMeshInstanced(inst_mesh, 0, inst_material, matrices, count, uniforms, UnityEngine.Rendering.ShadowCastingMode.Off, false);

	}
}
