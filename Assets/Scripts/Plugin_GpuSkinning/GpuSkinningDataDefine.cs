using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// shader的uniform location (用于instance的shader)
public class GpuSkinningUniforms
{
	// 当前动画在第几帧
	public static readonly int _FrameIndex = Shader.PropertyToID("_FrameIndex");
	// 下一个动画在第几帧
	public static readonly int _BlendFrameIndex = Shader.PropertyToID("_BlendFrameIndex");
	// 下一个动画的混合程度
	public static readonly int _BlendProgress = Shader.PropertyToID("_BlendProgress");
}

// 动画clip信息
[Serializable]
public class GpuSkinningAnimClip
{
	// 名称
	public string name;
	// 起始帧
	public int startFrame;
	// 结束帧
	public int endFrame;

	public GpuSkinningAnimClip(string n, int sf, int ef)
	{
		name = n;
		startFrame = sf;
		endFrame = ef;
	}

	public int Length()
	{
		return endFrame - startFrame + 1;
	}
}

// 完整的Gpu动画数据
public class GpuSkinningAnimData : ScriptableObject
{
	// 骨骼纹理
	public int texWidth;
	public int texHeight;
	public GpuSkinningAnimClip[] clips;
	public int totalFrame;
	public int totalBoneNum;

}

