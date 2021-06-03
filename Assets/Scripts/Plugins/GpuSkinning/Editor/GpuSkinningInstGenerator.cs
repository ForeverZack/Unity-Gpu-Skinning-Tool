using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;

/* 
使用限制：
	1.支持同一个fbx下有多个skinnedMeshRenderer，也支持一个skinnedMeshRenderer有多个subMesh，但这些网格模型都必须使用同一张主纹理贴图(多个也是能实现的，要自己特殊处理，看后面需求)
	2.无法保持fbx的节点层级，只会生成一个prefab，上述的网格信息都将被存到同一个mesh中

*/

namespace Framework.GpuSkinning
{
    public class GpuSkinningInstGenerator
    {
        // 动画类型
        public enum AnimationType
        {
            // 顶点动画
            Vertices = 0,
            // 骨骼动画
            Skeleton,
        };
        // 生成类型
        public enum GenerateType
        {
            // Vertics Animation -- 自动instance 顶点动画
            VerticesAnim = 0,
            // Dynamic -- 自动instance 骨骼动画
            Dynamic,
            // Instance -- gpu instance
            GpuInstance,
            // Noise Animation -- 自动instance 噪点顶点动画
            NoiseVerticesAnim,
            // Modify Molde Matrix -- 自动instance 修改model矩阵信息(通过scale传入)
            ModifyModelMatrix,
        }
        // 生成类型的配置
        public class GenerateConfig
        {
            // 动画类型
            public AnimationType animationType;
            // shader名称
            public string shaderName;
            // 存储data名称后缀
            public string saveDataName;
            // 存储prefab名称后缀
            public string savePrefabName;
            // 存储material名称后缀
            public string saveMaterialName;
            // 存储mesh名称后缀
            public string saveMeshName;

            public GenerateConfig(AnimationType at, string sn, string sdn, string spn, string smn, string smen)
            {
                animationType = at;
                shaderName = sn;
                saveDataName = sdn;
                savePrefabName = spn;
                saveMaterialName = smn;
                saveMeshName = smen;
            }
        }
        // 配置
        public Dictionary<GenerateType, GenerateConfig> generateConfigs = new Dictionary<GenerateType, GenerateConfig> {
            { GenerateType.VerticesAnim, new GenerateConfig(AnimationType.Vertices, "Custom/GpuVerticesAnimation", "_VertData.asset", "_VertPre.prefab", "_VertMat.mat", "_VertMesh.asset") },    // Vertics Animation -- 自动instance 顶点动画
            { GenerateType.Dynamic, new GenerateConfig(AnimationType.Skeleton, "Custom/GpuSkinningAnimation", "_Data.asset", "_DynPre.prefab", "_DynMat.mat", "_Mesh.asset") },    // Dynamic -- 自动instance 骨骼动画
            { GenerateType.GpuInstance, new GenerateConfig(AnimationType.Skeleton, "Custom/GpuSkinningAnim_Inst", "_Data.asset", "_InstPre.prefab", "_InstMat.mat", "_Mesh.asset") },      // Instance -- gpu instance
            { GenerateType.NoiseVerticesAnim, new GenerateConfig(AnimationType.Vertices, "Custom/NoiseGpuVerticesAnimation", "_VertData.asset", "_NoiseVertPre.prefab", "_NoiseVertMat.mat", "_VertMesh.asset") },  // Noise Animation -- 自动instance 噪点顶点动画
            { GenerateType.ModifyModelMatrix, new GenerateConfig(AnimationType.Vertices, "Custom/ModifyModelMatGpuVerticesAnimation", "_VertData.asset", "_ModifyModelMatVertPre.prefab", "_ModifyModelMatVertMat.mat", "_VertMesh.asset") },    // Modify Molde Matrix -- 自动instance 修改model矩阵信息(通过scale传入)
        };

        // 每根骨骼每帧所占像素空间(0,1:rotation, 2,3:translation)
        static readonly int DEFAULT_PER_FRAME_BONE_DATASPACE = 4;

        public GameObject curGameObject = null;
        public RuntimeAnimatorController controller = null;
        public List<AnimationClip> clipList = null;


        private GenerateType genType = (GenerateType)0;
        private GpuSkinningAnimData animData = null;
        // 生成的网格
        Mesh instMesh = null;
        List<Vector4> boneIndicesList = null;
        List<Vector4> boneWeightsList = null;
        // 生成的材质
        Material instMaterial = null;
        private AnimationClip[] animClips = null;
        private SkinnedMeshRenderer selectedSkinnedMeshRenderer = null;
        private List<GpuSkinningAnimClip> clipsData = null;
        Dictionary<Transform, int> boneIds = null;
        Dictionary<int, Matrix4x4> boneBindposes = null;
        string animTexturePath; // 动画纹理保存路径
        float compression = 1f;

        public GpuSkinningInstGenerator()
        {
            clipsData = new List<GpuSkinningAnimClip>();
            boneBindposes = new Dictionary<int, Matrix4x4>();
            boneIndicesList = new List<Vector4>();
            boneWeightsList = new List<Vector4>();
        }

        public void reset()
        {
            curGameObject = null;
            animData = null;
            clipsData.Clear();
            if (boneIds != null)
            {
                boneIds.Clear();
            }
        }

        // 设置选中的模型
        public void setSelectedModel(GameObject obj, GenerateType type, List<AnimationClip> clips, SkinnedMeshRenderer skinnedMeshRenderer, float compress)
        {
            if (obj == null)
            {
                Debug.LogError("select obj is null!!");
                animData = null;
                return;
            }

            if (curGameObject != obj || animData == null || genType != type || skinnedMeshRenderer != selectedSkinnedMeshRenderer || compress!=compression)
            {
                genType = type;
                curGameObject = obj;
                clipList = clips;
                selectedSkinnedMeshRenderer = skinnedMeshRenderer;
                compression = compress;
                refreshGeneratorInfo();
            }
        }

        public void refreshGeneratorInfo()
        {
            animData = ScriptableObject.CreateInstance<GpuSkinningAnimData>();
            int totalFrame = 0;
            int clipFrame = 0;
            clipsData.Clear();

            boneIds = resortBone(curGameObject);
            animData.totalBoneNum = boneIds.Count;

            animClips = clipList.ToArray();
            for (int i = 0; i < animClips.Length; ++i)
            {
                AnimationClip clip = animClips[i];
                clipFrame = (int)(clip.frameRate * clip.length / compression);

                GpuSkinningAnimClip clipData = new GpuSkinningAnimClip(clip.name, totalFrame, totalFrame+clipFrame-1, clip.frameRate/compression);
                clipsData.Add(clipData);

                totalFrame += clipFrame;
            }
            animData.totalFrame = totalFrame;
            animData.clips = clipsData.ToArray();

            GenerateConfig config = generateConfigs[genType];
            if (config.animationType == AnimationType.Vertices)
            {
                // 顶点动画
                Mesh mesh = selectedSkinnedMeshRenderer.sharedMesh;
                animData.texWidth = mesh.vertices.Length;
                animData.texHeight = totalFrame * 2; // vec3需要两个像素表示(rgba32->float16)
            }
            else
            {
                // 骨骼动画
                long totalPixels = boneIds.Count * DEFAULT_PER_FRAME_BONE_DATASPACE * totalFrame;
                calTextureSize(totalPixels, out animData.texWidth, out animData.texHeight);
            }
        }

        // 根据所需空间计算纹理大小
        private void calTextureSize(long totalPixels, out int width, out int height)
        {
            int step = 0;

            width = 32;
            height = 32;
            while (width * height < totalPixels)
            {
                if (step % 2 == 0)
                {
                    width *= 2;
                }
                else
                {
                    height *= 2;
                }
                ++step;
            }
        }

        // 顶点动画纹理
        public void generate_verticesAnim(string parentFolder, string savePath, string dataFileName, string matFileName, string prefabFileName, string mainTexPath, GenerateType generateType)
        {
            genType = generateType;

            // 生成纹理数据
            generateTexAndMesh_verticesAnim(parentFolder, savePath, dataFileName);

            // 生成材质
            generateMaterial(savePath, matFileName, mainTexPath);

            // 生成prefab
            generatePrefab(savePath, prefabFileName, dataFileName, parentFolder);

        }
        public void generateTexAndMesh_verticesAnim(string parentFolder, string savePath, string dataFileName)
        {
            // 重新生成mesh(这一步生成的mesh还是带骨骼indices和骨骼权重的，但顶点动画是不需要这些数据的，后面会删掉)
            rebuildAllMeshes(savePath, parentFolder);

            // 将骨骼矩阵写入纹理
            var tex2D = new Texture2D(animData.texWidth, animData.texHeight, TextureFormat.RGB24, false);
            tex2D.filterMode = FilterMode.Point;
            int clipIdx = 0;
            int pixelIdx = 0;
            int totalFrameIndex = 0;
            GpuSkinningAnimClip boneAnimation = null;
            AnimationClip clip = null;
            List<Matrix4x4> boneMatrices = null;
            Vector4 boneIndices, boneWeights;
            Matrix4x4 boneMatrix0, boneMatrix1, boneMatrix2, boneMatrix3;
            Vector3 src_vertex;
            Vector4 vertex, position;

            for (clipIdx = 0; clipIdx < animClips.Length; ++clipIdx)
            {
                boneAnimation = animData.clips[clipIdx];
                clip = animClips[clipIdx];
                for (int frameIndex = 0; frameIndex < boneAnimation.Length(); frameIndex++)
                {
                    boneMatrices = samplerAnimationClipBoneMatrices(curGameObject, clip, (float)frameIndex / (clip.frameRate/compression));
                    for (int vertexIndex = 0; vertexIndex < instMesh.vertices.Length; ++vertexIndex)
                    {
                        src_vertex = instMesh.vertices[vertexIndex];
                        vertex = new Vector4(src_vertex.x, src_vertex.y, src_vertex.z, 1);
                        boneIndices = boneIndicesList[vertexIndex];
                        boneWeights = boneWeightsList[vertexIndex];
                        boneMatrix0 = boneMatrices[Mathf.RoundToInt(boneIndices.x)];
                        boneMatrix1 = boneMatrices[Mathf.RoundToInt(boneIndices.y)];
                        boneMatrix2 = boneMatrices[Mathf.RoundToInt(boneIndices.z)];
                        boneMatrix3 = boneMatrices[Mathf.RoundToInt(boneIndices.w)];
                        matrixMulFloat(ref boneMatrix0, boneWeights.x);
                        matrixMulFloat(ref boneMatrix1, boneWeights.y);
                        matrixMulFloat(ref boneMatrix2, boneWeights.z);
                        matrixMulFloat(ref boneMatrix3, boneWeights.w);
                        position = (matrixAddMatrix(matrixAddMatrix(boneMatrix0, boneMatrix1), matrixAddMatrix(boneMatrix2, boneMatrix3))) * vertex;
                        Color[] colors = convertThreeFloat16Bytes2TwoColor(convertFloat32toFloat16Bytes(position.x), convertFloat32toFloat16Bytes(position.y), convertFloat32toFloat16Bytes(position.z));
                        tex2D.SetPixel(vertexIndex, totalFrameIndex * 2, colors[0]);
                        tex2D.SetPixel(vertexIndex, totalFrameIndex * 2 + 1, colors[1]);
                    }

                    ++totalFrameIndex;
                }
            }
            tex2D.Apply();
            // 导出动画纹理
            animTexturePath = savePath + dataFileName.Replace(".asset", "") + ".png";
            exportTexture(tex2D, animTexturePath);
            setAnimationTextureProperties(animTexturePath);

            // 存储数据
            string filePath = savePath + dataFileName;
            AssetDatabase.CreateAsset(animData, filePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 删除mesh的骨骼信息
            instMesh.uv2 = null;
            instMesh.uv3 = null;
            // 添加mesh的顶点索引 (TODO: unity shader中是否可以直接获取？)
            List<Vector4> indices = new List<Vector4>();
            for (int vertexIndex = 0; vertexIndex < instMesh.vertices.Length; ++vertexIndex)
            {
                indices.Add(new Vector4(vertexIndex, vertexIndex, vertexIndex, vertexIndex));
            }
            instMesh.SetUVs(1, indices);

            EditorUtility.SetDirty(instMesh);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh();
        }
        Matrix4x4 matrixMulFloat(ref Matrix4x4 matrix, float val)
        {
            matrix.m00 *= val;
            matrix.m01 *= val;
            matrix.m02 *= val;
            matrix.m03 *= val;
            matrix.m10 *= val;
            matrix.m11 *= val;
            matrix.m12 *= val;
            matrix.m13 *= val;
            matrix.m20 *= val;
            matrix.m21 *= val;
            matrix.m22 *= val;
            matrix.m23 *= val;
            matrix.m30 *= val;
            matrix.m31 *= val;
            matrix.m32 *= val;
            matrix.m33 *= val;
            return matrix;
        }
        Matrix4x4 matrixAddMatrix(Matrix4x4 mat1, Matrix4x4 mat2)
        {
            Matrix4x4 matrix = new Matrix4x4();
            matrix.m00 = mat1.m00 + mat2.m00;
            matrix.m01 = mat1.m01 + mat2.m01;
            matrix.m02 = mat1.m02 + mat2.m02;
            matrix.m03 = mat1.m03 + mat2.m03;
            matrix.m10 = mat1.m10 + mat2.m10;
            matrix.m11 = mat1.m11 + mat2.m11;
            matrix.m12 = mat1.m12 + mat2.m12;
            matrix.m13 = mat1.m13 + mat2.m13;
            matrix.m20 = mat1.m20 + mat2.m20;
            matrix.m21 = mat1.m21 + mat2.m21;
            matrix.m22 = mat1.m22 + mat2.m22;
            matrix.m23 = mat1.m23 + mat2.m23;
            matrix.m30 = mat1.m30 + mat2.m30;
            matrix.m31 = mat1.m31 + mat2.m31;
            matrix.m32 = mat1.m32 + mat2.m32;
            matrix.m33 = mat1.m33 + mat2.m33;
            return matrix;
        }

        void exportTexture(Texture2D texture, string path)
        {
            byte[] bytes = texture.EncodeToPNG();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            FileStream fs = new FileStream(path, FileMode.Create);
            fs.Write(bytes, 0, bytes.Length);
            fs.Close();
        }

        void setAnimationTextureProperties(string path)
        {
            AssetDatabase.Refresh();
            TextureImporter texture = AssetImporter.GetAtPath(path) as TextureImporter;
            texture.filterMode = FilterMode.Point;
            texture.mipmapEnabled = false;
            texture.npotScale = TextureImporterNPOTScale.None;
            AssetDatabase.ImportAsset(path);
        }

        // 骨骼动画纹理
        public void generate(string parentFolder, string savePath, string dataFileName, string matFileName, string prefabFileName, string mainTexPath, GenerateType generateType)
        {
            genType = generateType;

            // 生成纹理数据
            generateTexAndMesh(parentFolder, savePath, dataFileName);

            // 生成材质
            generateMaterial(savePath, matFileName, mainTexPath);

            // 生成prefab
            generatePrefab(savePath, prefabFileName, dataFileName, parentFolder);

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }

        public void generatePrefab(string savePath, string prefabFileName, string dataFileName, string parentFolder)
        {
            string prefabName = prefabFileName.Substring(0, prefabFileName.Length - ".prefab".Length);
            GameObject prefab = new GameObject(prefabFileName);

            // 组件
            MeshFilter meshFilter = prefab.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = instMesh;
            MeshRenderer renderer = prefab.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = instMaterial;
            if (this.genType == GenerateType.ModifyModelMatrix)
            {
                GPUSkinningAnimator animator = prefab.AddComponent<GPUSkinningAnimator>();
                animator.mat = instMaterial;
                animator.lowMesh = instMesh;
                animator.textAsset = AssetDatabase.LoadAssetAtPath<GpuSkinningAnimData>(Path.Combine(savePath, dataFileName));
            }
            else
            {
                GpuSkinningInstance instance = prefab.AddComponent<GpuSkinningInstance>();
                instance.textAsset = AssetDatabase.LoadAssetAtPath<GpuSkinningAnimData>(Path.Combine(savePath, dataFileName));
            }

            string prefabPath = Path.Combine(savePath, prefabFileName);
            PrefabUtility.CreatePrefab(prefabPath, prefab);

            GameObject.DestroyImmediate(prefab);
        }

        public void generateMaterial(string savePath, string matFileName, string mainTexPath)
        {
            instMaterial = null;

            GenerateConfig config = generateConfigs[genType];
            Shader shader = Shader.Find(config.shaderName);
            instMaterial = new Material(shader);
            instMaterial.enableInstancing = true;

            Texture2D saved_animTex = AssetDatabase.LoadAssetAtPath<Texture2D>(animTexturePath);
            // 材质
            if (File.Exists(mainTexPath))
            {
                Texture2D mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(mainTexPath);
                instMaterial.SetTexture("_MainTex", mainTex);
            }
            instMaterial.SetTexture("_AnimationTex", saved_animTex);

            string matPath = Path.Combine(savePath, matFileName);
            AssetDatabase.CreateAsset(instMaterial, matPath);
            EditorUtility.SetDirty(instMaterial);
        }

        public void generateTexAndMesh(string parentFolder, string savePath, string dataFileName)
        {
            int numBones = animData.totalBoneNum;

            // 重新生成mesh
            rebuildAllMeshes(savePath, parentFolder);

            // 将骨骼矩阵写入纹理
            var tex2D = new Texture2D(animData.texWidth, animData.texHeight, TextureFormat.RGBA32, false);
            tex2D.filterMode = FilterMode.Point;
            int clipIdx = 0;
            int pixelIdx = 0;
            Vector2Int pixelUv;
            GpuSkinningAnimClip boneAnimation = null;
            AnimationClip clip = null;
            List<Matrix4x4> boneMatrices = null;

            for (clipIdx = 0; clipIdx < animClips.Length; ++clipIdx)
            {
                boneAnimation = animData.clips[clipIdx];
                clip = animClips[clipIdx];
                for (int frameIndex = 0; frameIndex < boneAnimation.Length(); frameIndex++)
                {
                    boneMatrices = samplerAnimationClipBoneMatrices(curGameObject, clip, (float)frameIndex / clip.frameRate);
                    for (int boneIndex = 0; boneIndex < numBones; boneIndex++)
                    {
                        Matrix4x4 matrix = boneMatrices[boneIndex];
                        Quaternion rotation = ToQuaternion(matrix);
                        Vector3 scale = matrix.lossyScale;
                        float sx = Mathf.Floor(scale.x * 100.0f);
                        float sy = Mathf.Floor(scale.y * 100.0f);
                        float sz = Mathf.Floor(scale.z * 100.0f);
                        if ((sx - sy) > 5.0f
                            || (sx - sz) > 5.0f
                            || (sy - sz) > 5.0f)
                        {
                            Transform remapBone = null;
                            foreach (var key in boneIds.Keys)
                            {
                                if (boneIds[key] == boneIndex)
                                {
                                    remapBone = key;
                                    break;
                                }
                            }
                            string strLog = string.Format("AnimClip scale X Y Z not equal: {0} -> {1} {2}", curGameObject.name, boneAnimation.name, remapBone.transform.name);
                            Warning("AnimClip scale", strLog);
                        }

                        pixelUv = convertPixel2UV(pixelIdx++);

                        Color color1 = convertFloat16Bytes2Color(convertFloat32toFloat16Bytes(rotation.x), convertFloat32toFloat16Bytes(rotation.y));
                        tex2D.SetPixel(pixelUv.x, pixelUv.y, color1);
                        pixelUv = convertPixel2UV(pixelIdx++);
                        Color color2 = convertFloat16Bytes2Color(convertFloat32toFloat16Bytes(rotation.z), convertFloat32toFloat16Bytes(rotation.w));
                        tex2D.SetPixel(pixelUv.x, pixelUv.y, color2);
                        pixelUv = convertPixel2UV(pixelIdx++);
                        Color color3 = convertFloat16Bytes2Color(convertFloat32toFloat16Bytes(matrix.GetColumn(3).x), convertFloat32toFloat16Bytes(matrix.GetColumn(3).y));
                        tex2D.SetPixel(pixelUv.x, pixelUv.y, color3);
                        pixelUv = convertPixel2UV(pixelIdx++);
                        Color color4 = convertFloat16Bytes2Color(convertFloat32toFloat16Bytes(matrix.GetColumn(3).z), convertFloat32toFloat16Bytes(Mathf.Clamp01(matrix.lossyScale.magnitude)));
                        tex2D.SetPixel(pixelUv.x, pixelUv.y, color4);

                        //Debug.Log("==============frameIndex========start========"+ frameIndex);
                        //Debug.Log("=======rotation==========="+ rotation.x+", "+ rotation.y+", " + rotation.z +", "+rotation.w);
                        //Vector4 rrrr = convertColors2Halfs(color1, color2);
                        //Debug.Log("=======rotation===22======" + rrrr.x + ", " + rrrr.y + ", " + rrrr.z + ", " + rrrr.w);
                        //Debug.Log("=======translation===========" + matrix.GetColumn(3).x + ", " + matrix.GetColumn(3).y + ", " + matrix.GetColumn(3).z + ", " + Mathf.Clamp01(matrix.lossyScale.magnitude));
                        //Vector4 ttttt = convertColors2Halfs(color3, color4);
                        //Debug.Log("=======translation===22======" + ttttt.x + ", " + ttttt.y + ", " + ttttt.z + ", " + ttttt.w);
                        //Debug.Log("==============frameIndex========end========" + frameIndex);
                    }
                }
            }
            tex2D.Apply();
            // 导出动画纹理
            animTexturePath = savePath + dataFileName.Replace(".asset", "") + ".png";
            exportTexture(tex2D, animTexturePath);
            setAnimationTextureProperties(animTexturePath);

            // 存储
            string filePath = savePath + dataFileName;
            AssetDatabase.CreateAsset(animData, filePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        //float convertFloat16BytesToHalf(int data1, int data2)
        //{
        //    float result = 16 * (data1 >> 6 & 0x01) + 8 * (data1 >> 5 & 0x01) + 4 * (data1 >> 4 & 0x01) + 2 * (data1 >> 3 & 0x01) + 1 * (data1 >> 2 & 0x01) // 整数部分
        //        + 0.5f * (data1 >> 1 & 0x01) + 0.25f * (data1 & 0x01) + 0.125f * (data2 >> 7 & 0x01) + 0.0625f * (data2 >> 6 & 0x01) + 0.03125f * (data2 >> 5 & 0x01)    // 小数部分
        //        + 0.015625f * (data2 >> 4 & 0x01) + 0.0078125f * (data2 >> 3 & 0x01) + 0.00390625f * (data2 >> 2 & 0x01) + 0.001953125f * (data2 >> 1 & 0x01) + 0.0009765625f * (data2 & 0x01);

        //    int flag = (data1 >> 7 & 0x01);
        //    result = result - 2 * (1 - flag) * result;      //0: 负  1:正

        //    return result;
        //}

        //Vector4 convertColors2Halfs(Color color1, Color color2)
        //{
        //    return new Vector4(convertFloat16BytesToHalf((int)(color1.r * 255), (int)(color1.g * 255)), convertFloat16BytesToHalf((int)(color1.b * 255), (int)(color1.a * 255)), convertFloat16BytesToHalf((int)(color2.r * 255), (int)(color2.g * 255)), convertFloat16BytesToHalf((int)(color2.b * 255), (int)(color2.a * 255)));
        //}


        // 用一个RGB32像素，存储两个float16数据
        private Color convertFloat16Bytes2Color(byte[] data1, byte[] data2)
        {
            Color color = new Color(data1[0] / 255.0f, data1[1] / 255.0f, data2[0] / 255.0f, data2[1] / 255.0f);
            return color;
        }

        // 用两个RGB24像素，存储三个float16数据
        private Color[] convertThreeFloat16Bytes2TwoColor(byte[] data1, byte[] data2, byte[] data3)
        {
            Color[] colors = new Color[2];
            colors[0] = new Color(data1[0] / 255.0f, data1[1] / 255.0f, data2[0] / 255.0f);
            colors[1] = new Color(data2[1] / 255.0f, data3[0] / 255.0f, data3[1] / 255.0f);
            return colors;
        }

        static List<int> integer_rst = new List<int>();
        private byte[] convertFloat32toFloat16Bytes(float srcValue)
        {
            int integer = (int)srcValue;
            float floats = srcValue - integer;

            if (integer > 127)
            {
                // 超过float16的范围
                EditorUtility.DisplayDialog("警告!!", "模型数据值大于127，超过Float16的范围", "OK");
                integer = 127;
            }
            if (integer < -127)
            {
                // 超过float16的范围
                EditorUtility.DisplayDialog("警告!!", "模型数据值小于-127，超过Float16的范围", "OK");
                integer = -127;
            }

            // 1个符号位(+:1)，7个指数位，8个基数位
            int[] data = new int[16];
            int index = 0;

            // 符号 //1: 负  0:正
            if (srcValue > 0)
            {
                data[index++] = 0;
            }
            else
            {
                data[index++] = 1;
                floats = -(srcValue - integer);
                integer = -integer;
            }

            // 指数位
            integer_rst.Clear();
            while (integer > 0)
            {
                integer_rst.Add(integer % 2);
                integer /= 2;
            }
            if (integer_rst.Count < 7)
            {
                int length = 7 - integer_rst.Count;
                for (int i = 0; i < length; ++i)
                {
                    data[index++] = 0;
                }
            }
            for (int i = 0; i < integer_rst.Count; ++i)
            {
                data[index++] = integer_rst[integer_rst.Count - 1 - i];
            }


            // 小数位
            int temp;
            for (int i = 0; i < 8; ++i)
            {
                floats *= 2;
                temp = (int)floats;
                data[index++] = temp;
                floats -= temp;
            }

            byte[] result = new byte[2];
            temp = 0;
            for (int i = 0; i < 8; ++i)
            {
                temp += (int)Math.Pow(2, 7 - i) * data[i];
            }
            result[0] = (byte)temp;
            temp = 0;
            for (int i = 8; i < 16; ++i)
            {
                temp += (int)Math.Pow(2, 15 - i) * data[i];
            }
            result[1] = (byte)temp;
            return result;
        }


        private Vector2Int convertPixel2UV(int idx)
        {
            int row = (int)(idx / animData.texWidth);
            int column = idx - row * animData.texWidth;
            return new Vector2Int(column, row);
        }


        private List<Matrix4x4> samplerAnimationClipBoneMatrices(GameObject obj, AnimationClip clip, float time)
        {
            Transform root = selectedSkinnedMeshRenderer.rootBone;

            List<Matrix4x4> matrices = new List<Matrix4x4>();
            clip.SampleAnimation(obj, time);
            for (int i = 0; i < animData.totalBoneNum; ++i)
            {
                Transform bone = null;
                foreach (var key in boneIds.Keys)
                {
                    if (boneIds[key] == i)
                    {
                        bone = key;
                        break;
                    }
                }

                // 模型空间->骨骼空间->模型空间(这个世界空间不是unity真实世界的空间，它是我们新生成网格的模型空间)
                // 网格模型的顶点是模型空间下的，被绑定到骨骼上(父节点发生改变)所以需要将它转到骨骼节点的坐标系。模型空间->骨骼空间
                // bone.localToWorldMatrix是骨骼节点到模型空间的变换(在播放动画时，它记录了骨骼在模型空间下的变换).	骨骼空间->模型空间
                Matrix4x4 matrixBip001 = bone.localToWorldMatrix * boneBindposes[i];
                matrices.Add(matrixBip001);
            }


            return matrices;
        }

        private void rebuildAllMeshes(string savePath, string parentFolder)
        {
            {
                SkinnedMeshRenderer sm = selectedSkinnedMeshRenderer;

                if (!IslegalString(sm.name))
                {
                    string strLog = string.Format("Mesh  命名错误: {0} -> {1}", curGameObject.name, sm.name);
                    Warning("命名错误", strLog);
                }

                string meshName = sm.sharedMesh.name;
                instMesh = null;

                GenerateConfig config = generateConfigs[genType];
                string meshPath = Path.Combine(Path.GetDirectoryName(savePath), meshName) + config.saveMeshName;
                if (File.Exists(parentFolder + meshPath.Replace("\\", "/")))
                {
                    AssetDatabase.DeleteAsset(meshPath);
                }

                if (sm.sharedMesh.subMeshCount > 1)
                {
                    Debug.LogError("Not subMeshCount > 1:" + meshName);
                }

                instMesh = UnityEngine.Object.Instantiate<Mesh>(sm.sharedMesh);
                instMesh.uv2 = null;
                instMesh.uv3 = null;
                AssetDatabase.CreateAsset(instMesh, meshPath);

                rebulidMeshBindPose(curGameObject, sm, instMesh, boneIds);

                EditorUtility.SetDirty(instMesh);
            }

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

        }

        /// 生成骨骼id
        private Dictionary<Transform, int> resortBone(GameObject target)
        {
            if (selectedSkinnedMeshRenderer == null)
            {
                Debug.LogError("Don't has SkinnedMeshRenderer component  " + target.name);
                return null;
            }

            Transform root = selectedSkinnedMeshRenderer.rootBone;
            if (root == null)
            {
                Debug.LogError("Don't has Root: " + target.name);
                return null;
            }

            Dictionary<Transform, int> mappedIdx = new Dictionary<Transform, int>();
            {
                // if (root != sm.rootBone)
                // {
                // 	Debug.LogError(sm.name + ":Root bone error:" + sm.rootBone.name);
                // 	continue;
                // }

                Transform[] smBones = selectedSkinnedMeshRenderer.bones;
                foreach (var b in smBones)
                {
                    if (!mappedIdx.ContainsKey(b))
                    {
                        mappedIdx.Add(b, mappedIdx.Count);
                    }
                }
            }

            return mappedIdx;
        }

        // 将骨骼信息存入mesh顶点信息的uv1和uv2中 (uv1:骨骼id uv2:骨骼权重)
        private void rebulidMeshBindPose(GameObject obj, SkinnedMeshRenderer sm, Mesh targetMesh, Dictionary<Transform, int> inRemapBones)
        {
            // 初始化该节点renderer使用的骨骼列表
            /* unity中，对于一个mesh来说boneWeight.boneIndex对应的是当前节点SkinnedMeshRenderer的bones。它们都只是整个模型文件的部分骨骼，但它们boneIndex的顺序
               和bones的顺序是相同的，所以这里会用remapIdx来记录当前节点使用到的骨骼id。 */
            Transform[] aBones = sm.bones;
            int numBones = aBones.Length;

            int[] remapIdx = new int[numBones];
            for (int i = 0; i < numBones; ++i)
                remapIdx[i] = i;

            if (inRemapBones != null && inRemapBones.Count > 0)
            {
                for (int i = 0; i < numBones; ++i)
                {
                    if (!inRemapBones.ContainsKey(sm.bones[i]))
                    {
                        Debug.LogError(targetMesh.name + ":在 mappedIdx 没有找到 Transform:" + sm.bones[i].name);
                        continue;
                    }
                    remapIdx[i] = inRemapBones[sm.bones[i]];
                }
            }

            boneIndicesList.Clear();
            boneWeightsList.Clear();
            Matrix4x4[] aBindPoses = targetMesh.bindposes;
            BoneWeight[] aBoneWeights = targetMesh.boneWeights;//boneIndex对应SkinnedMeshRenderer的Bones的顺序(这里只是部分骨骼)
            for (int i = 0; i < targetMesh.vertexCount; ++i)
            {
                Vector4 boneIndex = Vector4.zero;
                Vector4 boneWeight = Vector4.zero;
                BoneWeight bw = aBoneWeights[i];

                if (Mathf.Abs(bw.weight0) > 0.00001f)
                {
                    boneIndex.x = remapIdx[bw.boneIndex0];
                    boneWeight.x = bw.weight0;
                }
                else
                {
                    Debug.LogError(targetMesh + " Idx:" + i + ": Bone 0 weight == 0.0f.");
                    boneIndex.x = 0;
                    boneWeight.x = 0.0f;
                }
                if (Mathf.Abs(bw.weight1) > 0.00001f)
                {
                    boneIndex.y = remapIdx[bw.boneIndex1];
                    boneWeight.y = bw.weight1;
                }
                else
                {
                    boneIndex.y = 0;
                    boneWeight.y = 0.0f;
                }
                if (Mathf.Abs(bw.weight2) > 0.00001f)
                {
                    boneIndex.z = remapIdx[bw.boneIndex2];
                    boneWeight.z = bw.weight2;
                }
                else
                {
                    boneIndex.z = 0;
                    boneWeight.z = 0.0f;
                }
                if (Mathf.Abs(bw.weight3) > 0.00001f)
                {
                    boneIndex.w = remapIdx[bw.boneIndex3];
                    boneWeight.w = bw.weight3;
                }
                else
                {
                    boneIndex.w = 0;
                    boneWeight.w = 0.0f;
                }
                boneIndicesList.Add(boneIndex);
                boneWeightsList.Add(boneWeight);


                float totalWeight = boneWeight.x + boneWeight.y + boneWeight.z + boneWeight.w;
                if (totalWeight - 1.0f > 0.00001f)
                {
                    Debug.LogError("BoneIndex total more than 1.0f ...vertice id =" + i);
                }
                if (totalWeight - 1.0f < -0.00001f)
                {
                    Debug.LogError("BoneIndex total less than 1.0f ...vertice id =" + i);
                }
            }
            targetMesh.SetUVs(1, boneIndicesList);
            targetMesh.SetUVs(2, boneWeightsList);

            // 记录原始网格的bindposes
            for (int bpIdx = 0; bpIdx < aBindPoses.Length; bpIdx++)
            {
                boneBindposes[remapIdx[bpIdx]] = aBindPoses[bpIdx];
            }
        }

        public GpuSkinningAnimData getAnimData()
        {
            return animData;
        }

        public static bool IslegalString(string str)
        {
            if (str.IndexOf(" ") > -1)
                return false;

            if (IsChina(str))
                return false;

            return true;
        }

        static bool IsChina(string CString)
        {
            bool BoolValue = false;
            for (int i = 0; i < CString.Length; i++)
            {
                if (Convert.ToInt32(Convert.ToChar(CString.Substring(i, 1))) > Convert.ToInt32(Convert.ToChar(128)))
                {
                    BoolValue = true;
                }

            }
            return BoolValue;
        }

        static void Warning(string tile, string strLog)
        {
            Debug.LogError(strLog);

#if DISPLAY_DIALOG
		EditorUtility.DisplayDialog(tile, strLog , "OK");
#endif
        }

        bool CompareApproximately(float f0, float f1, float epsilon = 0.000001F)
        {
            float dist = (f0 - f1);
            dist = Mathf.Abs(dist);
            return dist < epsilon;
        }

        Quaternion ToQuaternion(Matrix4x4 mat)
        {
            float det = mat.determinant;
            if (!CompareApproximately(det, 1.0F, .005f))
                return Quaternion.identity;

            Quaternion quat = Quaternion.identity;
            float tr = mat.m00 + mat.m11 + mat.m22;

            // check the diagonal
            if (tr > 0.0f)
            {
                float fRoot = Mathf.Sqrt(tr + 1.0f);  // 2w
                quat.w = 0.5f * fRoot;
                fRoot = 0.5f / fRoot;  // 1/(4w)
                quat.x = (mat[2, 1] - mat[1, 2]) * fRoot;
                quat.y = (mat[0, 2] - mat[2, 0]) * fRoot;
                quat.z = (mat[1, 0] - mat[0, 1]) * fRoot;
            }
            else
            {
                // |w| <= 1/2
                int[] s_iNext = { 1, 2, 0 };
                int i = 0;
                if (mat.m11 > mat.m00)
                    i = 1;
                if (mat.m22 > mat[i, i])
                    i = 2;
                int j = s_iNext[i];
                int k = s_iNext[j];

                float fRoot = Mathf.Sqrt(mat[i, i] - mat[j, j] - mat[k, k] + 1.0f);
                if (fRoot < float.Epsilon)
                    return Quaternion.identity;

                quat[i] = 0.5f * fRoot;
                fRoot = 0.5f / fRoot;
                quat.w = (mat[k, j] - mat[j, k]) * fRoot;
                quat[j] = (mat[j, i] + mat[i, j]) * fRoot;
                quat[k] = (mat[k, i] + mat[i, k]) * fRoot;
            }

            return QuaternionNormalize(quat);

        }

        public static Quaternion QuaternionNormalize(Quaternion quat)
        {
            float scale = new Vector4(quat.x, quat.y, quat.z, quat.w).magnitude;
            scale = 1.0f / scale;

            return new Quaternion(scale * quat.x, scale * quat.y, scale * quat.z, scale * quat.w);
        }




    }

}
