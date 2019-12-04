using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MessagePack;

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
[MessagePackObject]
public class GpuSkinningAnimClip
{
	[Key(0)]
	// 名称
	public string name;
	[Key(1)]
	// 起始帧
	public int startFrame;
	[Key(2)]
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
[MessagePackObject]
public class GpuSkinningAnimData 
{
	// 骨骼纹理
	[Key(0)]
	public int texWidth;
	[Key(1)]
	public int texHeight;
	[Key(2)]
	public byte[] texBytes;

	[Key(3)]
	public GpuSkinningAnimClip[] clips;

	[Key(4)]
	public int totalFrame;
	[Key(5)]
	public int totalBoneNum;

	public GpuSkinningAnimData()
	{
	}
}

