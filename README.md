# GpuSkinningInstance

## 描述 <br>
本插件基于Unity 2018.4.6f1实现。原理很简单，将骨骼动画的骨骼矩阵提取出来存入到纹理中，在播放动画时通过对该动画纹理的采样，来对模型进行蒙皮计算。
示例中含有三个场景，第一个场景示范了如何使用顶点动画纹理，对单个模型进行蒙皮操作。第二个场景示范了如何使用骨骼矩阵纹理，对单个模型进行蒙皮操作。
第三个场景示范了如何使用instance，适用于大量相同模型的动画播放。并且插件会事先计算出动画纹理的大小，并根据纹理大小一行一行的去写入骨骼矩阵。<br>


## 2019/11/27 更新
将动画纹理格式由原先的TextureFormat.RGBAHalf替换为TextureFormat.RGBA32，为了不损失动画精度，该纹理用2个RGBA32的通道(RGBA32中1个通道占8位)
去还原1个RGBAHalf的通道(RGBAHalf中1个通道16位)，这里自己做了位运算去处理Float16。所以动画纹理的大小增加了一倍，但可以在OpenGL ES2.0中使用了。
需要注意的是RGBA32的每个分量会被正交化到0~1之间，也就是颜色区间，不过这里是我们组装的浮点数(完全按照每个分量8bit来做的)，所以不会受到任何影响。<br>

## 2019/11/28 更新
骨骼id是通过uv1传入的，是float类型，在shader中直接取整会采样到错误的位置，所以加了个四舍五入的操作。另外增加了子mesh的选择。
修改了gpuskinning的shader，让它可以通过unity的instance来自动选择使用meshRenderer还是instance来渲染（勾上Enable GPU Instancing）。<br>

## 2019/12/01 更新
部分低配安卓机使用骨骼计算仍然很慢，所以添加了顶点动画的转换。顶点动画的混合原理和骨骼一样，不过动画纹理会大很多。
注意：对Point过滤方式的数据纹理，采样的时候一定要记得做0.5像素的偏移到像素中心！！！(今天因为这个原因造成了mac和pc结果不一致- -#) <br>

## 2019/12/26 更新
还原Half的函数在华为和ios上有bug，直接传入颜色值*255给int结果不正确，这里增加一步”+0.5并向下取整“。	<br>

## 2019/12/30 更新
gles3.0不支持位运算，所以把取位操作换成取余代替。<br>

## 如何导出相关纹理数据等 <br>
菜单栏"Window" -> "GpuSkinningTool" -> 选择录入资源 -> 检查生成纹理的相关信息 -> 生成	<br>

## 注意事项 <br>
1.支持同一个fbx下有多个skinnedMeshRenderer，也支持一个skinnedMeshRenderer有多个subMesh，但这些网格模型都必须使用同一张主纹理贴图
(多个也是能实现的，要自己特殊处理，看需求)	<br>
2.无法保持fbx的节点层级，只会生成一个prefab，上述的网格信息都将被存到同一个mesh中	<br>
3.Mesh的uv1和uv2分别被用来保存了骨骼id和骨骼权重用来蒙皮，注意勿重复使用 <br>
4.对于一个mesh来说boneWeight.boneIndex对应的是当前节点SkinnedMeshRenderer的bones。它们都只是整个模型文件的部分骨骼，但它们boneIndex的顺序
和bones的顺序是相同的。所以可以先遍历记录整个模型的骨骼id列表，再根据当前节点SkinnedMeshRenderer的bones来重新生成Mesh.boneWeights的骨骼id。 <br>
5.骨骼矩阵 = boneNode.localToWorldMatrix * boneBindPose(记录在Mesh中)。这里实际上是两步步骤：<br>
	1)网格模型的顶点是模型空间下的，被绑定到骨骼上(父节点发生改变)所以需要将它转到骨骼节点的坐标系。模型空间->骨骼空间	<br>
	2)bone.localToWorldMatrix是骨骼节点到模型空间的变换(在播放动画时，它记录了骨骼在模型空间下的变换).	骨骼空间->模型空间 <br>
