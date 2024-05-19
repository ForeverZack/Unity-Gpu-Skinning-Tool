using System.Collections;
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
            "modify model matrix (修改矩阵顶点动画 内置instance)",
            "mpb (修改mpb 内置instance)"
        };

        static string parentFolder;

        // 生成类型
        private GpuSkinningInstGenerator.GenerateType generateType = GpuSkinningInstGenerator.GenerateType.MPBVerticesAnim;
        private GpuSkinningInstGenerator.GenerateType lastGenerateType = GpuSkinningInstGenerator.GenerateType.MPBVerticesAnim;
        GameObject selectedFbx = null;
        GameObject curGameObject = null;
        // 附加动画文件
        private List<AnimationClip> extAnimationClips = new List<AnimationClip>();
        // 原始路径
        string srcPath = "";
        // 文件输出路径
        string savePath = "";
        // 数据文件名
        string saveName = "";
        // 数据法线文件名
        string saveNormalName = "";
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
        // 压缩倍数
        float compression = 1.0f;
        // 模型的动画列表
        string[] animationNames = new string[0];
        int selectedAnimation = 0;



        GpuSkinningInstGenerator generator = new GpuSkinningInstGenerator();

        [MenuItem("Window/GpuSkinningTool")]
        private static void ShowWindow()
        {
            GetWindow<GpuSkinningInstTools>().Close();
            
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
                    RefreshSkinnedMeshrenderersDict();

                    // if (!savePath.EndsWith(".FBX"))
                    // {
                    // 	Debug.LogError(savePath + "---->please choose .FBX file");
                    // 	return;
                    // }
                    GpuSkinningInstGenerator.GenerateConfig config = generator.generateConfigs[generateType];
                    saveName = selectedFbx.name + config.saveDataName;
                    saveNormalName = selectedFbx.name + config.saveNormalDataName;
                    savePrefabName = selectedFbx.name + config.savePrefabName;
                    saveMaterialName = selectedFbx.name + config.saveMaterialName;
  
                    mainTexPath = srcPath + selectedFbx.name + DEFAULT_MAIN_TEX_NAME_POSTFIX;
                    
                    extAnimationClips.Clear();
                    // animation list
                    List<string> animationNameList = new List<string>();
                    string assetPath = AssetDatabase.GetAssetPath(selectedFbx);
                    UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                    AnimationClip clip;
                    for (int i = 0; i < objs.Length; i++)
                    {
                        if (objs[i] is AnimationClip)
                        {
                            if (objs[i].hideFlags == (HideFlags.HideInHierarchy | HideFlags.NotEditable))
                                continue;

                            clip = objs[i] as AnimationClip;
                            animationNameList.Add(clip.name);
                        }
                    }
                    animationNames = animationNameList.ToArray();
                    selectedAnimation = 0;
                    for (int i=0; i<animationNames.Length; ++i)
                    {
                        if (animationNames[i].Contains("daiji01") || animationNames[i].Contains("idle"))
                        {
                            selectedAnimation = i;
                            break;
                        }
                    }

                    refreshPanel(selectedFbx);
                }
                
                EditorGUILayout.LabelField("附加动画列表:");
                EditorUtils.CreateListField(extAnimationClips, (idx, clip) =>
                {
                    var animationClip = (AnimationClip) EditorGUILayout.ObjectField(clip, typeof(AnimationClip), false);
                    extAnimationClips[idx] = animationClip;
                    if (clip != animationClip)
                    {
                        refreshPanel(selectedFbx, true);
                    }
                });
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
            if (generateType == GpuSkinningInstGenerator.GenerateType.VerticesAnim)
            {
                selectedAnimation = EditorGUILayout.Popup("请选择默认的模型姿态: ", selectedAnimation, animationNames);
            }
            compression = EditorGUILayout.FloatField("压缩率:", compression);

            EditorGUILayout.BeginVertical(EditorStyles.textField);
            EditorGUILayout.LabelField(string.Format("输出路径:{0}", savePath));
            savePath = EditorGUILayout.TextField(savePath);
            EditorGUILayout.LabelField(string.Format("输出文件:{0}", Path.Combine(savePath, saveName)));
            saveName = EditorGUILayout.TextField(saveName);
            {
                GpuSkinningInstGenerator.GenerateConfig config = generator.generateConfigs[generateType];
                if (config.animationType == GpuSkinningInstGenerator.AnimationType.Vertices)
                {
                    // 顶点动画
                    EditorGUILayout.LabelField(string.Format("输出法线文件:{0}", Path.Combine(savePath, saveNormalName)));
                    saveNormalName = EditorGUILayout.TextField(saveNormalName);
                }
            }
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
                refreshPanel(selectedFbx, true);
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
                    generator.generate_verticesAnim(parentFolder, savePath, saveName, saveMaterialName, savePrefabName, mainTexPath, generateType, animationNames[selectedAnimation]);
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

        private static void AddExtAnimClips(List<AnimationClip> extClips)
        {
            for (int i = 0; i < extClips.Count; ++i)
            {
                AnimationClip clip = extClips[i];
                if (clip != null)
                {
                    bool alreadyHas = false;
                    foreach (var tClip in m_clipList)
                    {
                        if (clip.name == tClip.name)
                        {
                            Debug.LogError($"发现重名的附加动画文件{clip.name}");
                            alreadyHas = true;
                            break;
                        }
                    }

                    if (!alreadyHas)
                    {
                        m_clipList.Add(clip);
                    }
                }
            }
        }

        void RefreshSkinnedMeshrenderersDict()
        {
            skinnedMeshRenderersDict.Clear();
            SkinnedMeshRenderer[] skinnedMeshRenderers = selectedFbx.GetComponentsInChildren<SkinnedMeshRenderer>();
            skinnedMeshRendererNames = new string[skinnedMeshRenderers.Length];
            for (int i = 0; i < skinnedMeshRenderers.Length; ++i)
            {
                skinnedMeshRendererNames[i] = skinnedMeshRenderers[i].name;
                skinnedMeshRenderersDict.Add(skinnedMeshRendererNames[i], skinnedMeshRenderers[i]);
            }
        }

        private void refreshPanel(GameObject selectedFbx, bool forceUpdate = false)
        {
            RefreshSkinnedMeshrenderersDict();
            GetAnimClips(selectedFbx);
            AddExtAnimClips(extAnimationClips);
            generator.setSelectedModel(selectedFbx, generateType, m_clipList, skinnedMeshRenderersDict[skinnedMeshRendererNames[selectedSkinnedMeshRenderer]], compression, forceUpdate);
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

            str += "\n注意: 附加动画列表中的动画必须与主模型使用相同网格和骨骼!!\n";

            return str;
        }

    }
}
