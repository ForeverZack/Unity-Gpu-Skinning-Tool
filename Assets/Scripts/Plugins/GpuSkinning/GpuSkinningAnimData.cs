using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.GpuSkinning
{
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
        // 帧率
        public float frameRate;

        public GpuSkinningAnimClip(string n, int sf, int ef, float fr=30f)
        {
            name = n;
            startFrame = sf;
            endFrame = ef;
            frameRate = fr;
        }

        // 获取帧数
        public int Length()
        {
            return endFrame - startFrame + 1;
        }
        // 获取时间
        public float getDuration()
        {
            return Length() / frameRate;
        }
        // 获取1帧的时间
        public float getPerFrameDuration()
        {
            return 1f / frameRate;
        }

    }

    // 完整的Gpu动画数据
    public class GpuSkinningAnimData : ScriptableObject
    {
        // 骨骼纹理
        public int texWidth;
        public int texHeight;
        // clips
        public GpuSkinningAnimClip[] clips;
        // 总帧数
        public int totalFrame;
        // 骨骼数量
        public int totalBoneNum;
        // 数据范围
        public float min;
        public float max;

        private Dictionary<string, GpuSkinningAnimClip> animClipsDict = null;
        public Dictionary<string, GpuSkinningAnimClip> getAnimationClipsDict()
        {
            if (animClipsDict != null)
            {
                return animClipsDict;
            }

            animClipsDict = new Dictionary<string, GpuSkinningAnimClip>();
            for (int i = 0; i < clips.Length; ++i)
            {
                animClipsDict.Add(clips[i].name, clips[i]);
            }
            return animClipsDict;
        }

    }
}
