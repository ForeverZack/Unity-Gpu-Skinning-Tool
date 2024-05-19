using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Framework.GpuSkinning
{
    public class GPUSkinningAnimator : MonoBehaviour
    {
        public Material mat;
        public Mesh lowMesh;
        public GpuSkinningAnimData textAsset;

        [HideInInspector]
        public GpuSkinningAnimData animData ;

        private MaterialPropertyBlock m_materialPropertyBlock;
        private static int k_ShaderPropertyID_AnimatorData = -1;
        private Vector3 animatorData = Vector3.zero;
        
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private GPUSkinningAnimation m_gpuAnimation;
        private bool m_shadowVisible = true;

        private Transform m_transform;
        private GameObject m_gameObject;

        private Action m_endTrigger = null;
        private float m_startTime;
        private float m_animationLength;
        private bool isPlayTriggerAni;

        public float animatorSpeed
        {
            get { return m_gpuAnimation != null ? m_gpuAnimation.timeScale : 1.0f; }
            set { if (m_gpuAnimation != null) m_gpuAnimation.timeScale = value; }
        }

        public void Awake()
        {
            if (k_ShaderPropertyID_AnimatorData == -1)
            {
                k_ShaderPropertyID_AnimatorData = Shader.PropertyToID("_AnimatorData");
            }
            
            m_transform = transform;
            m_gameObject = gameObject;
            meshRenderer = this.gameObject.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = mat;
            meshFilter = this.gameObject.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = lowMesh;

            m_gpuAnimation = new GPUSkinningAnimation();
            if (textAsset)
            {
                animData = textAsset;
                m_gpuAnimation.Initial(animData);
            }
            
            m_materialPropertyBlock = new MaterialPropertyBlock();
        }

        public void OnDestroy()
        {
        }

        public void Update()
        {
            m_gpuAnimation.Update(Time.deltaTime);

            updateMaterialPropertyBlock();

            OnUpdate();
        }

        private void updateMaterialPropertyBlock()
        {
            m_materialPropertyBlock.Clear();
            animatorData.x = m_gpuAnimation.getFrameIndex();    // frameIndex
            animatorData.y = m_gpuAnimation.getBlendFrameIndex();    // blendFrameIndex
            animatorData.z = m_gpuAnimation.getBlendProgress();    // blendProgress
            m_materialPropertyBlock.SetVector(k_ShaderPropertyID_AnimatorData, animatorData);
            meshRenderer.SetPropertyBlock(m_materialPropertyBlock);
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