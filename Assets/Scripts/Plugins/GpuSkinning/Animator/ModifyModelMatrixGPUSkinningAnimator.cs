using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Framework.GpuSkinning
{
    public class ModifyModelMatrixGPUSkinningAnimator : MonoBehaviour
    {
        public Material mat;
        public Mesh lowMesh;
        public GpuSkinningAnimData textAsset;

        [HideInInspector]
        public GpuSkinningAnimData animData ;

        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private GPUSkinningAnimation m_gpuAnimation;
        private bool m_shadowVisible = true;

        private Transform m_transform;
        private GameObject m_gameObject;
        private Vector3 m_scale;
        private int m_scaleXFlag;   // 0.正 1.负

        private Action m_endTrigger = null;
        private float m_startTime;
        private float m_animationLength;
        private bool isPlayTriggerAni;

        int index { get; set; }
        public float animatorSpeed
        {
            get { return m_gpuAnimation != null ? m_gpuAnimation.timeScale : 1.0f; }
            set { if (m_gpuAnimation != null) m_gpuAnimation.timeScale = value; }
        }
        public Vector3 scale
        {
            set {
                m_scale.x = value.x; m_scale.z = value.z;
                m_scaleXFlag = m_scale.x > 0 ? 0 : 1000;
            }
        }

        public void Awake()
        {
            m_transform = transform;
            m_gameObject = gameObject;
            meshRenderer = this.gameObject.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = mat;
            meshFilter = this.gameObject.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = lowMesh;

            scale = m_transform.localScale;

            m_gpuAnimation = new GPUSkinningAnimation();
            if (textAsset)
            {
                animData = textAsset;
                m_gpuAnimation.Initial(animData);
            }
        }

        public void OnDestroy()
        {
        }

        public void Update()
        {
            m_gpuAnimation.Update(Time.deltaTime);

            updateAnimationInfoIntoScale();
            m_transform.localScale = m_scale;

            OnUpdate();
        }

        private void updateAnimationInfoIntoScale()
        {
            // FrameIndex（3位[0~999帧]） .(小数点后)  ColorIndex（2位）
            m_scale.y = m_gpuAnimation.getFrameIndex();
            // xFlag(1位) BlendFrameIndex（3位[0~999帧]).(小数点后) BlendProgress (n位)
            m_scale.z = m_scaleXFlag + m_gpuAnimation.getBlendFrameIndex() + m_gpuAnimation.getBlendProgress();
        }

        void OnUpdate()
        {
            if (isPlayTriggerAni && Time.time-m_startTime>=m_animationLength)
            {
                isPlayTriggerAni = false;
                if (m_endTrigger != null)
                {
                    m_endTrigger();
                    m_endTrigger = null;
                }
            }
        }

        public void SetQueue(int queue)
        {
            mat.renderQueue = queue;
        }

        public void PlayAnimation(string animName, bool loop = false, float blendDuration = 0.1f,  Action onEndHandler = null)
        {
            if (m_gpuAnimation != null)
            {
                m_gpuAnimation.playAnimation(animName, loop, blendDuration);
                if (onEndHandler != null)
                {
                    isPlayTriggerAni = true;
                    m_endTrigger = onEndHandler;

                    // 回调
                    m_startTime = Time.time;
                    m_animationLength = GetAnimationLength(animName) /m_gpuAnimation.timeScale;
                }
            }
        }

        public void PlayAnimation(string animName, float timeOffset, bool loop = false, float blendDuration = 0.1f, Action onEndHandler = null)
        {
            if (m_gpuAnimation != null)
            {
                m_gpuAnimation.playAnimation(animName, timeOffset, loop, blendDuration);
                if (onEndHandler != null)
                {
                    isPlayTriggerAni = true;
                    m_endTrigger = onEndHandler;

                    // 回调
                    m_startTime = Time.time;
                    m_animationLength = GetAnimationLength(animName) / m_gpuAnimation.timeScale;
                }
            }
        }

        public float GetAnimationLength(string animName, float delValue=0.1f)
        {
            if (animData == null)
                return delValue;

            Dictionary<string, GpuSkinningAnimClip> dict = animData.getAnimationClipsDict();
            if (dict.ContainsKey(animName))
            {
                return dict[animName].getDuration();
            }

            return delValue;
        }


    }
}