﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Framework.GpuSkinning
{
    public class GpuSkinningInstTools : EditorWindow
    {
        // 默认主纹理名称
        static readonly string DEFAULT_MAIN_TEX_NAME_POSTFIX = ".png";
        // 生成类型菜单
        static readonly string[] DEFAULT_GENERATE_TYPE_POPUP = {
            "vertices animation (顶点动画 内置instance)",
            "dynamic (骨骼动画 内置instance)",
            "instance (骨骼动画 手动Instance)",
            "noise vertices animation (噪点顶点动画 内置instance)",
            "modify model matrix (修改矩阵顶点动画 内置instance)"
        };

        static string parentFolder;

        // 生成类型
        private GpuSkinningInstGenerator.GenerateType generateType = GpuSkinningInstGenerator.GenerateType.VerticesAnim;
        private GpuSkinningInstGenerator.GenerateType lastGenerateType = GpuSkinningInstGenerator.GenerateType.VerticesAnim;
        GameObject selectedFbx = null;
        GameObject curGameObject = null;
        // 原始路径
        string srcPath = "";
        // 文件输出路径
        string savePath = "";
        // 数据文件名
        string saveName = "";
        // prefab
        string savePrefabName = "";
        // material
        string saveMaterialName = "";
        RuntimeAnimatorController controller = null;
        string mainTexPath = "";
        private static List<AnimationClip> m_clipList = new List<AnimationClip>();
        // 子模型列表
        Dictionary<string, SkinnedMeshRenderer> skinnedMeshRenderersDict = new Dictionary<string, SkinnedMeshRenderer>();
        string[] skinnedMeshRendererNames = new string[0];
        int selectedSkinnedMeshRenderer = 0;
        int lastSelectedSkinnedMeshRenderer = 0;



        GpuSkinningInstGenerator generator = new GpuSkinningInstGenerator();

        [MenuItem("Window/GpuSkinningTool")]
        private static void ShowWindow()
        {
            parentFolder = Application.dataPath.Replace("Assets", "");
            var window = GetWindow<GpuSkinningInstTools>();
            window.minSize = new Vector2(600, 800);
            window.titleContent = new GUIContent("GpuSkinningInstTools");
            window.Show();
        }

        private void OnGUI()
        {
            selectedFbx = (GameObject)EditorGUILayout.ObjectField("请选择Fbx文件:", selectedFbx, typeof(GameObject), false);
            if (selectedFbx)
            {
                //Debug.Log(selectedFbx);

                if (selectedFbx != curGameObject)
                {
                    curGameObject = selectedFbx;
                    lastSelectedSkinnedMeshRenderer = 0;
                    selectedSkinnedMeshRenderer = 0;
                    srcPath = AssetDatabase.GetAssetPath(selectedFbx);
                    srcPath = srcPath.Substring(0, srcPath.LastIndexOf("/") + 1);
                    savePath = srcPath + "Output/";
                    // mesh list
                    skinnedMeshRenderersDict.Clear();
                    SkinnedMeshRenderer[] skinnedMeshRenderers = selectedFbx.GetComponentsInChildren<SkinnedMeshRenderer>();
                    skinnedMeshRendererNames = new string[skinnedMeshRenderers.Length];
                    for (int i = 0; i < skinnedMeshRenderers.Length; ++i)
                    {
                        skinnedMeshRendererNames[i] = skinnedMeshRenderers[i].name;
                        skinnedMeshRenderersDict.Add(skinnedMeshRendererNames[i], skinnedMeshRenderers[i]);
                    }

                    // if (!savePath.EndsWith(".FBX"))
                    // {
                    // 	Debug.LogError(savePath + "---->please choose .FBX file");
                    // 	return;
                    // }
                    GpuSkinningInstGenerator.GenerateConfig config = generator.generateConfigs[generateType];
                    saveName = selectedFbx.name + config.saveDataName;
                    savePrefabName = selectedFbx.name + config.savePrefabName;
                    saveMaterialName = selectedFbx.name + config.saveMaterialName;
  
                    mainTexPath = srcPath + selectedFbx.name + DEFAULT_MAIN_TEX_NAME_POSTFIX;

                    refreshPanel(selectedFbx);
                }
            }

            generateType = (GpuSkinningInstGenerator.GenerateType)EditorGUILayout.Popup("请选择输出类型: ", (int)lastGenerateType, DEFAULT_GENERATE_TYPE_POPUP);
            if (generateType != lastGenerateType)
            {
                lastGenerateType = generateType;
                curGameObject = null;
                parentFolder = Application.dataPath.Replace("Assets", "");
                generator.reset();
            }
            selectedSkinnedMeshRenderer = EditorGUILayout.Popup("请选择模型: ", lastSelectedSkinnedMeshRenderer, skinnedMeshRendererNames);
            if (selectedSkinnedMeshRenderer != lastSelectedSkinnedMeshRenderer)
            {
                lastSelectedSkinnedMeshRenderer = selectedSkinnedMeshRenderer;
                Debug.Log(lastSelectedSkinnedMeshRenderer);
                refreshPanel(selectedFbx);
            }

            EditorGUILayout.BeginVertical(EditorStyles.textField);
            EditorGUILayout.LabelField(string.Format("输出路径:{0}", savePath));
            savePath = EditorGUILayout.TextField(savePath);
            EditorGUILayout.LabelField(string.Format("输出文件:{0}", Path.Combine(savePath, saveName)));
            saveName = EditorGUILayout.TextField(saveName);
            EditorGUILayout.LabelField(string.Format("输出材质:{0}", Path.Combine(savePath, saveMaterialName)));
            saveMaterialName = EditorGUILayout.TextField(saveMaterialName);
            EditorGUILayout.LabelField(string.Format("输出Prefab:{0}", Path.Combine(savePath, savePrefabName)));
            savePrefabName = EditorGUILayout.TextField(savePrefabName);
            EditorGUILayout.EndVertical();


            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(EditorStyles.textField);
            EditorGUILayout.LabelField("mainTex路径:");
            mainTexPath = EditorGUILayout.TextField(mainTexPath);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.TextArea(convertGpuSkinningAnimData2Str(generator.getAnimData()));

            if (GUILayout.Button("刷新"))
            {
                refreshPanel(selectedFbx);
            }
            if (GUILayout.Button("生成"))
            {
                if (!File.Exists(mainTexPath))
                {
                    Debug.LogError("main texture file not exist !!");
                }

                Directory.CreateDirectory(savePath);
                GpuSkinningInstGenerator.GenerateConfig config = generator.generateConfigs[generateType];
                if (config.animationType == GpuSkinningInstGenerator.AnimationType.Vertices)
                {
                    // 顶点动画
                    generator.generate_verticesAnim(parentFolder, savePath, saveName, saveMaterialName, savePrefabName, mainTexPath, generateType);
                }
                else
                {
                    // 骨骼动画
                    generator.generate(parentFolder, savePath, saveName, saveMaterialName, savePrefabName, mainTexPath, generateType);
                }
                EditorUtility.DisplayDialog("提示", "生成成功", "OK");

            }
        }

        private static void GetAnimClips(GameObject obj)
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);
            UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            m_clipList.Clear();
            for (int i = 0; i < objs.Length; i++)
            {
                if (objs[i] is AnimationClip)
                {
                    if (objs[i].hideFlags == (HideFlags.HideInHierarchy | HideFlags.NotEditable))
                        continue;

                    //if (!IslegalString(objs[i].name))
                    //{
                    //    string strLog = string.Format("AnimationCLip  命名错误: {0} -> {1}", assetPath, objs[i].name);
                    //    Debug.Log("命名错误", strLog);
                    //}

                    m_clipList.Add(objs[i] as AnimationClip);
                }
            }
        }

        private void refreshPanel(GameObject selectedFbx)
        {
            GetAnimClips(selectedFbx);
            generator.setSelectedModel(selectedFbx, generateType, m_clipList, skinnedMeshRenderersDict[skinnedMeshRendererNames[selectedSkinnedMeshRenderer]]);
        }

        private string convertGpuSkinningAnimData2Str(GpuSkinningAnimData data)
        {
            string str = "概要信息:\n所需数据不全,请检查\n";

            if (data != null)
            {
                string str_anims = "";
                for (int i = 0; i < data.clips.Length; ++i)
                {
                    str_anims += string.Format("    \"{0}\": {1}~{2} \n", data.clips[i].name, data.clips[i].startFrame, data.clips[i].endFrame);
                }
                str = string.Format("概要信息:\n纹理大小:{0}x{1}\n总帧数:{2}\n骨骼数量:{3}\n动画列表:\n{4}", data.texWidth, data.texHeight
                        , data.totalFrame, data.totalBoneNum, str_anims);
            }
            else
            {
                if (curGameObject == null)
                {
                    str += "提示: 未选中FBX文件!!\n";
                }
            }

            return str;
        }

    }
}
