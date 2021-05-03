#define SKIN_DEBUG
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Diagnostics;

namespace Framework.GpuSkinning
{
    public class GPUSkinningAnimation
    {
        public class PlayAnimationInfo
        {
            // 基本信息，需要moveData
            // 播放的动画
            public GpuSkinningAnimClip animClip = null;
            // 动画计时器
            public float timer = 0;
            // 动画是否循环
            public bool loop = false;
            // 混合时间
            public float blendDuration = 0;
            // 混合计时器
            public float blendTimer = 0;
            // 是否有数据
            public bool hasData = false;
            // 动画时长
            private float duration = 0;
            // loop动画的时长 (endFrame -> startFrame 加一帧的时长来补帧)
            private float loopDuration = 0;

            // 每帧计算，不需要moveData
            // 动画当前帧
            private int recCurFrame = 0;
            // 补帧！！！
            // 动画的下一帧 (补帧使用)
            private int recNextFrame = 0;
            // 动画到下一帧的进度 (补帧使用)
            private float recNextFrameProgress = 0;

            // 临时变量
            private float fProgress;
            private float fCurFrame;

            // 移动数据
            public void moveData(PlayAnimationInfo info)
            {
                animClip = info.animClip;
                timer = info.timer;
                loop = info.loop;
                blendDuration = info.blendDuration;
                blendTimer = info.blendTimer;
                hasData = info.hasData;
                duration = info.duration;
                loopDuration = info.loopDuration;

                info.clear();
            }
            // 设置数据
            public void setData(GpuSkinningAnimClip clip, bool l, float blendDura)
            {
                clear();

                hasData = true;
                animClip = clip;
                loop = l;
                blendDuration = blendDura;
                duration = animClip.getDuration();
                loopDuration = duration + animClip.getPerFrameDuration();
            }
            public void setData(GpuSkinningAnimClip clip, bool l, float blendDura, float timeOffset)
            {
                setData(clip, l, blendDura);

                timer = timeOffset;
                while (timer>=loopDuration && loopDuration!=0)
                {
                    timer -= loopDuration;
                }
            }
            // 清空
            public void clear()
            {
                animClip = null;
                timer = 0;
                loop = false;
                blendDuration = 0;
                blendTimer = 0;
                duration = 0;
                loopDuration = 0;
                recCurFrame = 0;
                recNextFrame = 0;
                recNextFrameProgress = 0;
                hasData = false;
            }
            // 刷新 (先调用update，在调用下面的获取方法)！！！
            public void update(float deltaTime, bool needBlend = false)
            {
                if (!hasData)
                {
                    return;
                }

                timer += deltaTime;
                if (loop && timer >= loopDuration)
                {
                    timer -= loopDuration;
                }
                if (needBlend)
                {
                    blendTimer += deltaTime;
                }
            }
            // ！！！ (先调用update，在调用下面的获取方法)
            // 获取混合进度
            public float getBlendProgress()
            {
                if (!hasData)
                {
                    return 0;
                }
                float blend_progress = blendTimer / blendDuration;
                return blend_progress > 1 ? 1 : blend_progress;
            }
            // 获取当前动画帧
            public int getAnimCurFrame(bool isBlend = false)
            {
                if (animClip == null)
                {
                    recNextFrame = 0;
                    recNextFrameProgress = 0;
                    return 0;
                }

                fProgress = timer / duration;
                fCurFrame = (animClip.startFrame + (animClip.endFrame - animClip.startFrame) * fProgress);
                recCurFrame = (int)fCurFrame;
                recCurFrame = recCurFrame > animClip.endFrame ? animClip.endFrame : recCurFrame;
                if (!isBlend)
                {
                    // 混合所用的PlayAnimationInfo无需计算
                    recNextFrame = recCurFrame + 1;
                    recNextFrame = recNextFrame > animClip.endFrame ? (loop?animClip.startFrame:animClip.endFrame) : recNextFrame;  // 注意是否loop，loop的话下一帧是startFrame，否则维持在endFrame
                    recNextFrameProgress = fCurFrame - recCurFrame;
                    recNextFrameProgress = recNextFrameProgress > 1 ? 1 : recNextFrameProgress;
                }

                return recCurFrame;
            }
            // 补帧！！！
            // 获取当前动画帧的下一帧
            public int getAnimNextFrame()
            {
                return recNextFrame;
            }
            // 获取当前动画下一帧的融合进度
            public float getAnimNextProgress()
            {
                return recNextFrameProgress;
            }
        }

        // 动画数据
        private GpuSkinningAnimData animData = null;
        private Dictionary<string, GpuSkinningAnimClip> animClipsDict = null;

        // 播放速度
        public float timeScale = 1.0f;
        // 当前动画
        PlayAnimationInfo curAnimationInfo = new PlayAnimationInfo();
        // 下一个动画
        PlayAnimationInfo nextAnimationInfo = new PlayAnimationInfo();

        // 当前动画在第几帧
        public int getFrameIndex()
        {
            return curAnimationInfo.getAnimCurFrame();
        }
        // 混合动画在第几帧
        public int getBlendFrameIndex()
        {
            if (nextAnimationInfo.hasData)
            {
                return nextAnimationInfo.getAnimCurFrame(true);
            }
            else
            {
                return curAnimationInfo.getAnimNextFrame();
            }
        }
        // 混合进度
        public float getBlendProgress()
        {
            if (nextAnimationInfo.hasData)
            {
                return nextAnimationInfo.getBlendProgress();
            }
            else
            {
                return curAnimationInfo.getAnimNextProgress();
            }
        }

        // init
        public void Initial(GpuSkinningAnimData data)
        {
            animData = data;
            animClipsDict = animData.getAnimationClipsDict();
        }

        // 播放动画
        public void playAnimation(string animName, bool loop = false, float blendDuration = 0.1f)
        {
            if (!animClipsDict.ContainsKey(animName))
            {
                return;
            }

            if (!curAnimationInfo.hasData)
            {
                // 当前没有动画正在播放
                curAnimationInfo.setData(animClipsDict[animName], loop, blendDuration);
            }
            else
            {
                if (!nextAnimationInfo.hasData)
                {
                    // 当前有动画在播放，但没有待转换的动画
                    nextAnimationInfo.setData(animClipsDict[animName], loop, blendDuration);
                }
                else
                {
                    // 当前有动画在播放，且正在转换
                    curAnimationInfo.moveData(nextAnimationInfo);
                    nextAnimationInfo.setData(animClipsDict[animName], loop, blendDuration);
                }
            }
        }
        public void playAnimation(string animName, float timeOffset, bool loop = false, float blendDuration = 0.1f)
        {
            if (!animClipsDict.ContainsKey(animName))
            {
                return;
            }

            if (!curAnimationInfo.hasData)
            {
                // 当前没有动画正在播放
                curAnimationInfo.setData(animClipsDict[animName], loop, blendDuration, timeOffset);
            }
            else
            {
                if (!nextAnimationInfo.hasData)
                {
                    // 当前有动画在播放，但没有待转换的动画
                    nextAnimationInfo.setData(animClipsDict[animName], loop, blendDuration, timeOffset);
                }
                else
                {
                    // 当前有动画在播放，且正在转换
                    curAnimationInfo.moveData(nextAnimationInfo);
                    nextAnimationInfo.setData(animClipsDict[animName], loop, blendDuration, timeOffset);
                }
            }
        }

        public void Update(float deltaTime)
        {
            deltaTime = deltaTime * timeScale;
            if (curAnimationInfo.hasData)
            {
                curAnimationInfo.update(deltaTime);
            }
            if (nextAnimationInfo.hasData)
            {
                nextAnimationInfo.update(deltaTime, true);
                if (nextAnimationInfo.getBlendProgress() >= 1)
                {
                    // 混合完成
                    curAnimationInfo.moveData(nextAnimationInfo); 
                }
            }
        }


    }
}