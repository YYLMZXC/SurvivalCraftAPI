#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Engine.Animation;
using Engine.Graphics;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using GltfPrimitiveType = SharpGLTF.Schema2.PrimitiveType;
using PrimitiveType = Engine.Graphics.PrimitiveType;
using GltfImage = SharpGLTF.Schema2.Image;
using GltfTexture = SharpGLTF.Schema2.Texture;
using GltfMaterial = SharpGLTF.Schema2.Material;
using BYTES = System.ArraySegment<byte>;

namespace Engine.Media {
    /// <summary>
    /// glTF 模型加载器
    /// 通过回调机制加载外部资源（.bin 文件和纹理）
    /// </summary>
    public static class GltfLoader {
        /// <summary>
        /// 外置文件加载回调，一般情况下和 Storage.LoadContentStreamCallback 相同
        /// </summary>
        public static Func<string, Stream> LoadExternalStreamCallback { get; set; }

        /// <summary>
        /// 检查是否为 glTF 文件
        /// </summary>
        public static bool IsGltfFile(string filePath) {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext is ".gltf" or ".glb";
        }

        /// <summary>
        /// 从文件路径加载 glTF 模型（便利方法，仅用于测试，将持久修改 LoadExternalStreamCallback）
        /// </summary>
        /// <param name="filePath">模型文件路径（.gltf 或 .glb）</param>
        /// <returns>ModelData 实例</returns>
        public static ModelData LoadFromFile(string filePath) {
            ArgumentNullException.ThrowIfNull(filePath);

            string basePath = Path.GetDirectoryName(filePath);
            LoadExternalStreamCallback = (relativePath) => {
                string fullPath = Path.Combine(basePath, relativePath);
                return File.Exists(fullPath) ? File.OpenRead(fullPath) : null;
            };

            using var stream = File.OpenRead(filePath);
            return Load(stream, basePath);
        }

        /// <summary>
        /// 从流加载 glTF 模型
        /// </summary>
        /// <param name="stream">模型数据流（GLB 或 glTF JSON）</param>
        /// <param name="basePath">模型数据流的基路径（用于加载外部资源）</param>
        /// <returns>ModelData 实例</returns>
        public static ModelData Load(Stream stream, string basePath = null) {
            ArgumentNullException.ThrowIfNull(stream);
            BYTES FileReaderCallback(string assetName) {
                string path = basePath == null ? assetName : Storage.CombinePaths(basePath.Replace('\\', '/'), assetName);
                Stream resourceStream = LoadExternalStreamCallback(path);
                byte[] bytes = new byte[resourceStream.Length];
                int totalRead = 0;
                while (totalRead < bytes.Length) {
                    int read = resourceStream.Read(bytes, totalRead, bytes.Length - totalRead);
                    if (read == 0) {
                        throw new Exception($"Failed to read {path}");
                    }
                    totalRead += read;
                }
                return new BYTES(bytes);
            }
            var context = ReadContext.Create(FileReaderCallback);
            context.Validation = ValidationMode.Skip;
            ModelRoot modelRoot = context.ReadSchema2(stream);
            return ConvertToModelData(modelRoot);
        }

        static ModelData ConvertToModelData(ModelRoot modelRoot) {
            ModelData modelData = new();

            // 构建节点名称到索引的映射
            Dictionary<Node, int> nodeToIndex = new();
            List<Node> allNodes = new();

            // 首先收集所有节点
            foreach (Node node in modelRoot.LogicalNodes) {
                nodeToIndex[node] = allNodes.Count;
                allNodes.Add(node);
            }

            // 加载纹理和材质（在网格之前，因为需要建立索引映射）
            Dictionary<Texture, int> textureToIndex = new();
            Dictionary<Material, int> materialToIndex = new();
            ConvertTexturesAndMaterials(modelRoot, modelData, textureToIndex, materialToIndex);

            // 转换骨骼/节点数据
            ConvertBones(modelRoot, modelData, allNodes, nodeToIndex);

            // 转换网格数据
            ConvertMeshes(modelRoot, modelData, allNodes, nodeToIndex, textureToIndex, materialToIndex);

            // 转换蒙皮数据
            ConvertSkins(modelRoot, modelData, nodeToIndex);

            // 转换动画数据
            ConvertAnimations(modelRoot, modelData);

            return modelData;
        }

        static void ConvertBones(ModelRoot modelRoot, ModelData modelData, List<Node> allNodes, Dictionary<Node, int> nodeToIndex) {
            // 创建临时映射：Node -> 临时索引
            Dictionary<Node, int> nodeToTempIndex = new();
            for (int i = 0; i < allNodes.Count; i++) {
                nodeToTempIndex[allNodes[i]] = i;
            }

            // 找出所有根节点（没有视觉父节点的节点）
            List<int> rootIndices = new();
            for (int i = 0; i < allNodes.Count; i++) {
                if (allNodes[i].VisualParent == null) {
                    rootIndices.Add(i);
                }
            }

            // 检查是否需要创建虚拟根骨骼
            bool needVirtualRoot = rootIndices.Count > 1;
            int virtualRootIndex = -1;
            int totalBoneCount = allNodes.Count + (needVirtualRoot ? 1 : 0);

            // 创建临时骨骼数据数组
            ModelBoneData[] tempBones = new ModelBoneData[totalBoneCount];
            int boneOffset = needVirtualRoot ? 1 : 0;

            // 如果需要虚拟根骨骼，创建它
            if (needVirtualRoot) {
                tempBones[0] = new ModelBoneData {
                    Name = "Root",
                    Transform = Matrix.Identity,
                    ParentBoneIndex = -1
                };
                virtualRootIndex = 0;
            }

            // 创建节点对应的骨骼数据
            for (int i = 0; i < allNodes.Count; i++) {
                Node node = allNodes[i];
                int boneIndex = i + boneOffset;
                tempBones[boneIndex] = new ModelBoneData {
                    Name = node.Name ?? $"Node{node.LogicalIndex}",
                    Transform = node.LocalMatrix,
                    ParentBoneIndex = -1 // 先设为 -1，后面再更新
                };
            }

            // 设置父骨骼索引（使用临时索引）
            for (int i = 0; i < allNodes.Count; i++) {
                Node node = allNodes[i];
                int boneIndex = i + boneOffset;
                Node parent = node.VisualParent;

                if (parent != null && nodeToTempIndex.TryGetValue(parent, out int parentIndex)) {
                    tempBones[boneIndex].ParentBoneIndex = parentIndex + boneOffset;
                } else if (needVirtualRoot) {
                    // 没有父节点的节点，设置为虚拟根骨骼的子节点
                    tempBones[boneIndex].ParentBoneIndex = virtualRootIndex;
                }
            }

            // 拓扑排序：确保父骨骼在子骨骼之前
            // 计算每个骨骼的深度，按深度排序
            int[] depths = new int[totalBoneCount];
            for (int i = 0; i < totalBoneCount; i++) {
                depths[i] = CalculateDepth(i, tempBones);
            }

            // 创建排序映射：旧索引 -> 新索引
            int[] oldToNew = new int[totalBoneCount];
            List<int> sortedIndices = new();
            for (int i = 0; i < totalBoneCount; i++) {
                sortedIndices.Add(i);
            }
            sortedIndices.Sort((a, b) => depths[a].CompareTo(depths[b]));

            for (int newIndex = 0; newIndex < sortedIndices.Count; newIndex++) {
                oldToNew[sortedIndices[newIndex]] = newIndex;
            }

            // 按排序顺序添加骨骼，并更新父索引
            foreach (int oldIndex in sortedIndices) {
                ModelBoneData bone = tempBones[oldIndex];
                if (bone.ParentBoneIndex >= 0) {
                    bone.ParentBoneIndex = oldToNew[bone.ParentBoneIndex];
                }
                modelData.Bones.Add(bone);
            }

            // 更新 nodeToIndex 映射（用于后续的蒙皮和网格处理）
            nodeToIndex.Clear();
            for (int i = 0; i < allNodes.Count; i++) {
                int oldIndex = i + boneOffset;
                Node node = allNodes[i];
                nodeToIndex[node] = oldToNew[oldIndex];
            }

            // 如果没有节点，创建一个默认根节点
            if (modelData.Bones.Count == 0) {
                modelData.Bones.Add(new ModelBoneData {
                    Name = "Root",
                    ParentBoneIndex = -1,
                    Transform = Matrix.Identity
                });
            }
        }

        static int CalculateDepth(int boneIndex, ModelBoneData[] bones) {
            int depth = 0;
            int current = boneIndex;
            while (bones[current].ParentBoneIndex >= 0 && depth < bones.Length) {
                current = bones[current].ParentBoneIndex;
                depth++;
            }
            return depth;
        }

        /// <summary>
        /// 加载纹理和材质，建立索引映射
        /// </summary>
        static void ConvertTexturesAndMaterials(ModelRoot modelRoot, ModelData modelData,
            Dictionary<GltfTexture, int> textureToIndex, Dictionary<GltfMaterial, int> materialToIndex) {

            // 1. 分析纹理用途，确定 sRGB vs Linear
            Dictionary<int, bool> textureIsSrgb = new();
            foreach (GltfTexture tex in modelRoot.LogicalTextures) {
                textureIsSrgb[tex.LogicalIndex] = true; // 默认 sRGB
            }
            foreach (GltfMaterial material in modelRoot.LogicalMaterials) {
                AnalyzeTextureColorSpace(material, textureIsSrgb);
            }

            // 2. 加载所有纹理（延迟加载模式）
            foreach (GltfTexture gltfTexture in modelRoot.LogicalTextures) {
                GltfImage image = gltfTexture.PrimaryImage ?? gltfTexture.FallbackImage;
                if (image?.Content == null) {
                    continue;
                }

                int texIndex = modelData.Textures.Count;
                textureToIndex[gltfTexture] = texIndex;
                modelData.GltfTextureToModelIndex[gltfTexture.LogicalIndex] = texIndex;

                bool isSrgb = textureIsSrgb.GetValueOrDefault(gltfTexture.LogicalIndex, true);

                ModelTextureInfo texInfo = new() {
                    Name = image.Name ?? $"Texture{image.LogicalIndex}",
                    IsSrgb = isSrgb
                };

                string sourcePath = image.Content.SourcePath;
                if (!string.IsNullOrEmpty(sourcePath)) {
                    texInfo.SourceImage = image.Content;
                } else {
                    texInfo.SourceImage = image.Content;
                }

                texInfo.SetSampler(gltfTexture.Sampler);

                modelData.Textures.Add(texInfo);
            }

            // 3. 加载所有材质
            foreach (GltfMaterial gltfMaterial in modelRoot.LogicalMaterials) {
                int matIndex = modelData.Materials.Count;
                materialToIndex[gltfMaterial] = matIndex;

                ModelMaterial mat = new() {
                    Name = gltfMaterial.Name ?? $"Material{gltfMaterial.LogicalIndex}"
                };

                LoadMaterialProperties(gltfMaterial, mat, modelData);

                modelData.Materials.Add(mat);
            }
        }

        /// <summary>
        /// 分析纹理颜色空间（sRGB vs Linear）
        /// </summary>
        static void AnalyzeTextureColorSpace(GltfMaterial material, Dictionary<int, bool> textureIsSrgb) {
            // sRGB channels: BaseColor, Emissive
            // Linear channels: Normal, MetallicRoughness, Occlusion

            void MarkTexture(string channelKey, bool isSrgb) {
                MaterialChannel? channel = material.FindChannel(channelKey);
                if (channel?.Texture is { } tex) {
                    textureIsSrgb[tex.LogicalIndex] = isSrgb;
                }
            }

            MarkTexture("BaseColor", true);
            MarkTexture("Emissive", true);
            MarkTexture("Normal", false);
            MarkTexture("MetallicRoughness", false);
            MarkTexture("Occlusion", false);
        }

        /// <summary>
        /// 加载材质属性
        /// </summary>
        static void LoadMaterialProperties(GltfMaterial gltfMaterial, ModelMaterial mat, ModelData modelData) {
            // BaseColor
            MaterialChannel? channel = gltfMaterial.FindChannel("BaseColor");
            if (channel != null) {
                var color = channel.Value.Color;
                mat.BaseColorFactor = new Vector4(color.X, color.Y, color.Z, color.W);
                mat.BaseColorTexture = LoadMaterialTexture(channel, modelData);
            }

            // MetallicRoughness
            channel = gltfMaterial.FindChannel("MetallicRoughness");
            if (channel != null) {
                mat.MetallicFactor = GetFactorSafe(channel.Value, "MetallicFactor", 1f);
                mat.RoughnessFactor = GetFactorSafe(channel.Value, "RoughnessFactor", 1f);
                mat.MetallicRoughnessTexture = LoadMaterialTexture(channel, modelData);
            }

            // Normal
            channel = gltfMaterial.FindChannel("Normal");
            if (channel != null) {
                mat.NormalScale = GetFactorSafe(channel.Value, "NormalScale", 1f);
                mat.NormalTexture = LoadMaterialTexture(channel, modelData);
            }

            // Occlusion
            channel = gltfMaterial.FindChannel("Occlusion");
            if (channel != null) {
                mat.OcclusionStrength = GetFactorSafe(channel.Value, "OcclusionStrength", 1f);
                mat.OcclusionTexture = LoadMaterialTexture(channel, modelData);
            }

            // Emissive
            channel = gltfMaterial.FindChannel("Emissive");
            if (channel != null) {
                var emissive = channel.Value.Color;
                mat.EmissiveFactor = new Vector3(emissive.X, emissive.Y, emissive.Z);
                mat.EmissiveTexture = LoadMaterialTexture(channel, modelData);
            }

            // Alpha mode
            mat.AlphaMode = gltfMaterial.Alpha switch {
                AlphaMode.BLEND => ModelAlphaMode.Blend,
                AlphaMode.MASK => ModelAlphaMode.Mask,
                _ => ModelAlphaMode.Opaque
            };
            mat.AlphaCutoff = gltfMaterial.AlphaCutoff;
            mat.DoubleSided = gltfMaterial.DoubleSided;

            // 源材质索引
            mat.SourceMaterialIndex = gltfMaterial.LogicalIndex;

            // 加载材质扩展
            LoadMaterialExtensions(gltfMaterial, mat, modelData);
        }

        /// <summary>
        /// 加载材质扩展
        /// </summary>
        static void LoadMaterialExtensions(GltfMaterial gltfMaterial, ModelMaterial mat, ModelData modelData) {
            foreach (var jsonSerializable in gltfMaterial.Extensions) {
                Type type = jsonSerializable.GetType();
                string extName = null;

                // 获取扩展名称
                if (jsonSerializable is ExtraProperties) {
                    var method = type.GetMethod("GetSchemaName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    extName = (string)method?.Invoke(jsonSerializable, null);
                } else {
                    var nameProp = type.GetProperty("Name", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    extName = (string)nameProp?.GetValue(jsonSerializable);
                }

                if (extName == null || !MaterialExtensionManager.IsExtensionEnabled(extName)) {
                    continue;
                }

                // 创建扩展实例
                MaterialExtension extension = MaterialExtensionRegistry.Create(extName);
                if (extension == null) {
                    continue;
                }

                // 加载扩展数据
                extension.LoadFromGltf(gltfMaterial, modelData);
                if (!extension.IsEnabled) {
                    continue;
                }

                // 存储到材质
                switch (extension) {
                    case ClearCoatExtension cc: mat.ClearCoat = cc; break;
                    case IridescenceExtension irid: mat.Iridescence = irid; break;
                    case TransmissionExtension trans: mat.Transmission = trans; break;
                    case VolumeExtension vol: mat.Volume = vol; break;
                    case SheenExtension sheen: mat.Sheen = sheen; break;
                    case SpecularExtension spec: mat.Specular = spec; break;
                    case IorExtension ior: mat.Ior = ior; break;
                    case EmissiveStrengthExtension emissiveStr: mat.EmissiveStrength = emissiveStr; break;
                    case DispersionExtension disp: mat.Dispersion = disp; break;
                    case AnisotropyExtension aniso: mat.Anisotropy = aniso; break;
                    case DiffuseTransmissionExtension diffTrans: mat.DiffuseTransmission = diffTrans; break;
                    case VolumeScatterExtension volScatter: mat.VolumeScatter = volScatter; break;
                    case UnlitExtension unlit: mat.Unlit = unlit; break;
                    case SpecularGlossinessExtension sg: mat.SpecularGlossiness = sg; break;
                }
            }
        }

        /// <summary>
        /// 从材质通道加载 ModelMaterialTexture
        /// </summary>
        static ModelMaterialTexture LoadMaterialTexture(MaterialChannel? channel, ModelData modelData) {
            if (channel?.Texture == null) {
                return null;
            }

            int textureIndex = modelData.GetTextureIndex(channel.Value.Texture.LogicalIndex);
            if (textureIndex < 0) {
                return null;
            }

            int uvIndex = channel.Value.TextureCoordinate;
            ModelMaterialTexture matTex = new(textureIndex, uvIndex);

            // 读取 KHR_texture_transform 扩展
            TextureTransform transform = channel.Value.TextureTransform;
            if (transform != null) {
                matTex.SetTransform(
                    new Vector2(transform.Offset.X, transform.Offset.Y),
                    new Vector2(transform.Scale.X, transform.Scale.Y),
                    transform.Rotation
                );
            }

            return matTex;
        }

        static float GetFactorSafe(MaterialChannel channel, string factorName, float defaultValue) {
            try {
                return channel.GetFactor(factorName);
            } catch {
                return defaultValue;
            }
        }

        static int GetTextureIndex(GltfTexture texture, Dictionary<GltfTexture, int> textureToIndex) {
            if (texture == null || !textureToIndex.TryGetValue(texture, out int index)) {
                return -1;
            }
            return index;
        }

        static void ConvertMeshes(ModelRoot modelRoot, ModelData modelData, List<Node> allNodes,
            Dictionary<Node, int> nodeToIndex, Dictionary<GltfTexture, int> textureToIndex, Dictionary<GltfMaterial, int> materialToIndex) {
            int bufferIndex = 0;

            // 获取默认场景或使用所有根节点
            Scene scene = modelRoot.DefaultScene ?? modelRoot.LogicalScenes.FirstOrDefault();
            IEnumerable<Node> nodesToProcess;

            if (scene != null) {
                nodesToProcess = scene.VisualChildren;
            } else {
                nodesToProcess = modelRoot.LogicalNodes.Where(n => n.VisualParent == null);
            }

            foreach (Node node in nodesToProcess) {
                ProcessNodeForMesh(node, modelData, allNodes, nodeToIndex, ref bufferIndex, materialToIndex);
            }
        }

        static void ProcessNodeForMesh(Node node, ModelData modelData, List<Node> allNodes,
            Dictionary<Node, int> nodeToIndex, ref int bufferIndex, Dictionary<GltfMaterial, int> materialToIndex,
            bool parentVisible = true) {
            // 解析当前节点的 visibility 状态
            bool nodeVisible = parentVisible;
            if (node.TryGetVisibility(out bool vis)) {
                nodeVisible = vis && parentVisible;
            }

            if (node.Mesh != null) {
                int boneIndex = nodeToIndex.TryGetValue(node, out int idx) ? idx : 0;

                // 每个 primitive 创建独立 ModelMeshData，避免同一 mesh 内不同材质的 parts 被错误地一起绘制
                // EXT_mesh_gpu_instancing：读取实例变换矩阵
                MeshGpuInstancing gpuInstancing = node.GetGpuInstancing();
                System.Numerics.Matrix4x4[] instanceMatrices = null;
                int instanceCount = 0;
                if (gpuInstancing != null && gpuInstancing.Count > 0) {
                    instanceCount = gpuInstancing.Count;
                    instanceMatrices = new System.Numerics.Matrix4x4[instanceCount];
                    for (int i = 0; i < instanceCount; i++) {
                        instanceMatrices[i] = gpuInstancing.GetLocalMatrix(i);
                    }
                }

                foreach (MeshPrimitive primitive in node.Mesh.Primitives) {
                    ModelMeshPartData meshPart = ProcessPrimitive(primitive, modelData, ref bufferIndex, materialToIndex);
                    if (meshPart == null) continue;

                    // 设置实例化数据（同节点所有 primitive 共享同一数组引用，请勿修改数组内容）
                    meshPart.InstanceCount = instanceCount;
                    meshPart.InstanceMatrices = instanceMatrices;

                    ModelMeshData meshData = new() {
                        Name = node.Mesh.Name ?? $"Mesh{node.Mesh.LogicalIndex}",
                        ParentBoneIndex = boneIndex,
                        IsVisible = nodeVisible
                    };
                    meshData.MeshParts.Add(meshPart);
                    CalculateMeshBoundingBox(meshData, meshData.MeshParts);
                    modelData.Meshes.Add(meshData);
                }

                // 记录 node → mesh 索引映射（KHR_node_visibility 动画用）
                // 存储该 node 第一个 mesh 的索引，CreateNodeVisibilityTarget 通过 ParentBoneIndex 查找所有同级 mesh
                for (int mi = modelData.Meshes.Count - 1; mi >= 0; mi--) {
                    if (modelData.Meshes[mi].ParentBoneIndex == boneIndex) {
                        modelData.GltfNodeToMeshIndex[node.LogicalIndex] = mi;
                        break;
                    }
                }
            }

            // KHR_lights_punctual
            if (node.PunctualLight != null && modelData.Lights.Count < ModelLight.MaxPunctualLights) {
                var pl = node.PunctualLight;
                var wm = node.WorldMatrix;
                ModelLightData ld = new() {
                    Color = new Vector3(pl.Color.X, pl.Color.Y, pl.Color.Z),
                    Intensity = pl.Intensity,
                    Range = pl.Range,
                    Position = new Vector3(wm.M41, wm.M42, wm.M43),
                    Direction = Vector3.Normalize(new Vector3(-wm.M31, -wm.M32, -wm.M33)),
                    IsVisible = nodeVisible,
                    BoneIndex = nodeToIndex.TryGetValue(node, out int bIdx) ? bIdx : -1
                };
                switch (pl.LightType) {
                    case PunctualLightType.Directional: ld.Type = ModelLightType.Directional; break;
                    case PunctualLightType.Point: ld.Type = ModelLightType.Point; break;
                    case PunctualLightType.Spot:
                        ld.Type = ModelLightType.Spot;
                        ld.InnerConeCos = MathF.Cos(pl.InnerConeAngle);
                        ld.OuterConeCos = MathF.Cos(pl.OuterConeAngle);
                        break;
                }
                modelData.Lights.Add(ld);
                // 记录 node → light 索引映射（visibility 动画用）
                modelData.GltfNodeToLightIndex[node.LogicalIndex] = modelData.Lights.Count - 1;
            }

            foreach (Node child in node.VisualChildren) {
                ProcessNodeForMesh(child, modelData, allNodes, nodeToIndex, ref bufferIndex, materialToIndex,
                    nodeVisible);
            }
        }

        static ModelMeshPartData ProcessPrimitive(MeshPrimitive primitive, ModelData modelData, ref int bufferIndex, Dictionary<GltfMaterial, int> materialToIndex) {
            // 获取顶点数据
            var posAccessor = primitive.GetVertexAccessor("POSITION");
            if (posAccessor == null) {
                return null;
            }

            var positions = posAccessor.AsVector3Array();
            var normals = primitive.GetVertexAccessor("NORMAL")?.AsVector3Array();
            var uv0 = primitive.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
            var uv1 = primitive.GetVertexAccessor("TEXCOORD_1")?.AsVector2Array();
            var tangents = primitive.GetVertexAccessor("TANGENT")?.AsVector4Array();
            var colors = primitive.GetVertexAccessor("COLOR_0")?.AsVector4Array();
            var joints = primitive.GetVertexAccessor("JOINTS_0")?.AsVector4Array();
            var weights = primitive.GetVertexAccessor("WEIGHTS_0")?.AsVector4Array();

            // Morph Target 数据收集
            int morphTargetCount = primitive.MorphTargetsCount;
            System.Numerics.Vector3[][] morphPositions = null;
            System.Numerics.Vector3[][] morphNormals = null;
            System.Numerics.Vector4[][] morphTangentsArr = null;
            if (morphTargetCount > 0) {
                morphPositions = new System.Numerics.Vector3[morphTargetCount][];
                morphNormals = new System.Numerics.Vector3[morphTargetCount][];
                morphTangentsArr = new System.Numerics.Vector4[morphTargetCount][];
                for (int t = 0; t < morphTargetCount; t++) {
                    var ta = primitive.GetMorphTargetAccessors(t);
                    morphPositions[t] = ta.TryGetValue("POSITION", out var mp) ? mp.AsVector3Array().ToArray() : null;
                    morphNormals[t] = ta.TryGetValue("NORMAL", out var mn) ? mn.AsVector3Array().ToArray() : null;
                    morphTangentsArr[t] = ta.TryGetValue("TANGENT", out var mt) ? mt.AsVector4Array().ToArray() : null;
                }
            }

            // 获取索引数据
            uint[] indices = primitive.GetIndices()?.ToArray();
            if (indices == null || indices.Length == 0) {
                // 如果没有索引，创建顺序索引（避免 LINQ 分配）
                indices = new uint[positions.Count];
                for (int i = 0; i < positions.Count; i++) {
                    indices[i] = (uint)i;
                }
            }

            // 无预计算切线时：先 unweld 再生成切线（仅 TRIANGLES，STRIP/FAN 索引不是三元组）
            // 共享顶点在 UV 缝合线处有冲突的切线方向，unweld 后每个三角形有独立顶点
            bool isTriangle = primitive.DrawPrimitiveType is GltfPrimitiveType.TRIANGLES
                or GltfPrimitiveType.TRIANGLE_STRIP
                or GltfPrimitiveType.TRIANGLE_FAN;
            bool isTrianglesOnly = primitive.DrawPrimitiveType == GltfPrimitiveType.TRIANGLES;
            System.Numerics.Vector4[] generatedTangents = null;
            System.Numerics.Vector3[] uwPos = null, uwNrm = null;
            System.Numerics.Vector2[] uwUv0 = null, uwUv1 = null;
            System.Numerics.Vector4[] uwJoints = null, uwWeights = null, uwColors = null;

            if (isTrianglesOnly && tangents == null && normals != null && uv0 != null && indices != null) {
                int idxCount = indices.Length;
                uwPos = new System.Numerics.Vector3[idxCount];
                uwNrm = new System.Numerics.Vector3[idxCount];
                uwUv0 = new System.Numerics.Vector2[idxCount];
                if (uv1 != null) uwUv1 = new System.Numerics.Vector2[idxCount];
                if (colors != null) uwColors = new System.Numerics.Vector4[idxCount];
                if (joints != null) uwJoints = new System.Numerics.Vector4[idxCount];
                if (weights != null) uwWeights = new System.Numerics.Vector4[idxCount];

                for (int i = 0; i < idxCount; i++) {
                    int idx = (int)indices[i];
                    uwPos[i] = positions[idx];
                    uwNrm[i] = normals[idx];
                    uwUv0[i] = uv0[idx];
                    if (uwUv1 != null) uwUv1[i] = uv1[idx];
                    if (uwColors != null) uwColors[i] = colors[idx];
                    if (uwJoints != null) uwJoints[i] = joints[idx];
                    if (uwWeights != null) uwWeights[i] = weights[idx];
                }

                // Unweld Morph Targets
                if (morphTargetCount > 0) {
                    for (int t = 0; t < morphTargetCount; t++) {
                        if (morphPositions[t] != null) {
                            var uw = new System.Numerics.Vector3[idxCount];
                            for (int i = 0; i < idxCount; i++) uw[i] = morphPositions[t][(int)indices[i]];
                            morphPositions[t] = uw;
                        }
                        if (morphNormals[t] != null) {
                            var uw = new System.Numerics.Vector3[idxCount];
                            for (int i = 0; i < idxCount; i++) uw[i] = morphNormals[t][(int)indices[i]];
                            morphNormals[t] = uw;
                        }
                        if (morphTangentsArr[t] != null) {
                            var uw = new System.Numerics.Vector4[idxCount];
                            for (int i = 0; i < idxCount; i++) uw[i] = morphTangentsArr[t][(int)indices[i]];
                            morphTangentsArr[t] = uw;
                        }
                    }
                }

                indices = new uint[idxCount];
                for (int i = 0; i < idxCount; i++) indices[i] = (uint)i;

                generatedTangents = GenerateTangents(uwPos, uwNrm, uwUv0, indices);
            }

            // 构建顶点声明
            // 注意：着色器期望 Position, Normal, TexCoord 都是必需的
            List<VertexElement> elements = new();
            int offset = 0;

            // Position (Vector3)
            elements.Add(new VertexElement(offset, VertexElementFormat.Vector3, VertexElementSemantic.Position));
            offset += 12;

            // Normal (Vector3) - 始终添加，着色器期望有 NORMAL
            // 如果模型没有法线数据，使用默认值 (0, 1, 0)
            bool hasNormals = normals != null;
            elements.Add(new VertexElement(offset, VertexElementFormat.Vector3, VertexElementSemantic.Normal));
            offset += 12;

            // UV0 (Vector2) - 始终添加，着色器期望有 TEXCOORD
            // 如果模型没有 UV 数据，使用默认值 (0, 0)
            bool hasUV0 = uv0 != null;
            elements.Add(new VertexElement(offset, VertexElementFormat.Vector2, VertexElementSemantic.TextureCoordinate));
            offset += 8;

            // UV1 (Vector2) - 仅在有 TEXCOORD_1 数据时添加
            bool hasUV1 = uv1 != null;
            if (hasUV1) {
                elements.Add(new VertexElement(offset, VertexElementFormat.Vector2, VertexElementSemantic.TextureCoordinate1));
                offset += 8;
            }

            // Tangent (Vector4) - 仅在有 TANGENT 数据时添加
            bool hasTangents = tangents != null || generatedTangents != null;
            if (hasTangents) {
                elements.Add(new VertexElement(offset, VertexElementFormat.Vector4, VertexElementSemantic.Tangent));
                offset += 16;
            }

            // Color (Vector4) - 顶点颜色
            bool hasColors = colors != null;
            if (hasColors) {
                elements.Add(new VertexElement(offset, VertexElementFormat.Vector4, VertexElementSemantic.Color));
                offset += 16;
            }

            // BlendIndices (Vector4 - 作为 4 个 float 存储)
            // BlendWeights (Vector4)
            bool hasSkinning = joints != null && weights != null;
            if (hasSkinning) {
                elements.Add(new VertexElement(offset, VertexElementFormat.Vector4, VertexElementSemantic.BlendIndices));
                offset += 16;
                elements.Add(new VertexElement(offset, VertexElementFormat.Vector4, VertexElementSemantic.BlendWeights));
                offset += 16;
            }

            VertexDeclaration vertexDecl = new(elements.ToArray());

            // 构建顶点缓冲
            int vertexCount = uwPos != null ? uwPos.Length : positions.Count;
            byte[] vertexBuffer = new byte[vertexCount * offset];
            int vertexStride = offset;

            for (int i = 0; i < vertexCount; i++) {
                int baseOffset = i * vertexStride;
                int currentOffset = 0;

                // Position
                var pos = uwPos != null ? uwPos[i] : positions[i];
                WriteVector3(vertexBuffer, baseOffset + currentOffset, pos.X, pos.Y, pos.Z);
                currentOffset += 12;

                // Normal - 始终写入，没有数据时使用默认向上法线
                if (hasNormals) {
                    var normal = uwNrm != null ? uwNrm[i] : normals[i];
                    WriteVector3(vertexBuffer, baseOffset + currentOffset, normal.X, normal.Y, normal.Z);
                } else {
                    WriteVector3(vertexBuffer, baseOffset + currentOffset, 0f, 1f, 0f);
                }
                currentOffset += 12;

                // UV0 - 始终写入，没有数据时使用默认值
                if (hasUV0) {
                    var uv = uwUv0 != null ? uwUv0[i] : uv0[i];
                    WriteVector2(vertexBuffer, baseOffset + currentOffset, uv.X, uv.Y);
                } else {
                    WriteVector2(vertexBuffer, baseOffset + currentOffset, 0f, 0f);
                }
                currentOffset += 8;

                // UV1 - 仅在有 TEXCOORD_1 数据时写入
                if (hasUV1) {
                    var uv = uwUv1 != null ? uwUv1[i] : uv1[i];
                    WriteVector2(vertexBuffer, baseOffset + currentOffset, uv.X, uv.Y);
                    currentOffset += 8;
                }

                // Tangent
                if (hasTangents) {
                    if (tangents != null) {
                        var t = tangents[i];
                        WriteVector4(vertexBuffer, baseOffset + currentOffset, t.X, t.Y, t.Z, t.W);
                    } else {
                        var t = generatedTangents[i];
                        WriteVector4(vertexBuffer, baseOffset + currentOffset, t.X, t.Y, t.Z, t.W);
                    }
                    currentOffset += 16;
                }

                // Color - 顶点颜色
                if (hasColors) {
                    var c = uwColors != null ? uwColors[i] : colors[i];
                    WriteVector4(vertexBuffer, baseOffset + currentOffset, c.X, c.Y, c.Z, c.W);
                    currentOffset += 16;
                }

                // BlendIndices 和 BlendWeights
                if (hasSkinning) {
                    var joint = uwJoints != null ? uwJoints[i] : joints[i];
                    var weight = uwWeights != null ? uwWeights[i] : weights[i];

                    // BlendIndices (存储为 float)
                    WriteVector4(buffer: vertexBuffer, baseOffset + currentOffset, joint.X, joint.Y, joint.Z, joint.W);
                    currentOffset += 16;

                    // BlendWeights
                    WriteVector4(buffer: vertexBuffer, baseOffset + currentOffset, weight.X, weight.Y, weight.Z, weight.W);
                    currentOffset += 16;
                }
            }

            // 构建索引缓冲（统一使用 32 位索引，与 Collada 加载器保持一致）
            byte[] indexBuffer = new byte[indices.Length * 4];
            if (isTrianglesOnly) {
                // glTF 使用逆时针绕序 (CCW)，引擎使用 CullCounterClockwise，需要翻转绕序
                // 仅 TRIANGLES 图元的索引是三元组，STRIP/FAN 索引结构不同
                for (int triangle = 0; triangle < indices.Length / 3; triangle++) {
                    int baseIdx = triangle * 3;
                    // 翻转绕序：交换 v1 和 v2 (0,1,2 -> 0,2,1)
                    uint idx0 = indices[baseIdx];
                    uint idx1 = indices[baseIdx + 2]; // 交换
                    uint idx2 = indices[baseIdx + 1]; // 交换

                    WriteIndex32(indexBuffer, baseIdx, idx0);
                    WriteIndex32(indexBuffer, baseIdx + 1, idx1);
                    WriteIndex32(indexBuffer, baseIdx + 2, idx2);
                }
            }
            else {
                // 非三角形图元直接写入索引，不需要翻转绕序
                for (int i = 0; i < indices.Length; i++) {
                    WriteIndex32(indexBuffer, i, indices[i]);
                }
            }

            // 创建缓冲数据
            ModelBuffersData buffersData = new() {
                VertexDeclaration = vertexDecl,
                Vertices = vertexBuffer,
                Indices = indexBuffer
            };

            modelData.Buffers.Add(buffersData);

            // 计算包围盒
            BoundingBox bbox;
            if (uwPos != null) {
                var min = new Vector3(float.MaxValue);
                var max = new Vector3(float.MinValue);
                for (int i = 0; i < uwPos.Length; i++) {
                    min.X = Math.Min(min.X, uwPos[i].X);
                    min.Y = Math.Min(min.Y, uwPos[i].Y);
                    min.Z = Math.Min(min.Z, uwPos[i].Z);
                    max.X = Math.Max(max.X, uwPos[i].X);
                    max.Y = Math.Max(max.Y, uwPos[i].Y);
                    max.Z = Math.Max(max.Z, uwPos[i].Z);
                }
                bbox = new BoundingBox(min, max);
            } else {
                bbox = CalculateBoundingBoxFromPositions(positions, indices);
            }

            ModelMeshPartData meshPart = new() {
                BuffersDataIndex = bufferIndex++,
                StartIndex = 0,
                IndicesCount = indices.Length,
                BoundingBox = bbox,
                PrimitiveType = MapPrimitiveType(primitive.DrawPrimitiveType)
            };

            // 设置材质索引
            if (primitive.Material != null && materialToIndex.TryGetValue(primitive.Material, out int matIndex)) {
                meshPart.MaterialIndex = matIndex;
            }

            // Morph Target 纹理化
            if (morphTargetCount > 0) {
                HashSet<string> morphAttributes = new();
                for (int t = 0; t < morphTargetCount; t++) {
                    foreach (var attr in primitive.GetMorphTargetAccessors(t).Keys) {
                        morphAttributes.Add(attr);
                    }
                }
                int finalVertexCount = uwPos != null ? uwPos.Length : positions.Count;
                var morphTex = new MorphTargetTexture(finalVertexCount, morphTargetCount, morphAttributes);
                // 转为数组列表用于 UploadData
                var mpList = ToEngineVector3List(morphPositions);
                var mnList = ToEngineVector3List(morphNormals);
                var mtList = ToEngineVector4List(morphTangentsArr);
                morphTex.UploadData(mpList, mnList, mtList, null, null, null);
                meshPart.MorphTargetTexture = morphTex;
                meshPart.MorphTargetCount = morphTargetCount;
                var meshWeights = primitive.LogicalParent.MorphWeights;
                meshPart.MorphWeights = new float[morphTargetCount];
                if (meshWeights != null) {
                    for (int i = 0; i < Math.Min(meshWeights.Count, morphTargetCount); i++) {
                        meshPart.MorphWeights[i] = meshWeights[i];
                    }
                }
            }

            return meshPart;
        }

        static IReadOnlyList<Vector3>[] ToEngineVector3List(System.Numerics.Vector3[][] arrays) {
            if (arrays == null) return null;
            var result = new IReadOnlyList<Vector3>[arrays.Length];
            for (int t = 0; t < arrays.Length; t++) {
                if (arrays[t] == null) continue;
                var converted = new Vector3[arrays[t].Length];
                for (int i = 0; i < arrays[t].Length; i++) {
                    var v = arrays[t][i];
                    converted[i] = new Vector3(v.X, v.Y, v.Z);
                }
                result[t] = converted;
            }
            return result;
        }

        static IReadOnlyList<Vector4>[] ToEngineVector4List(System.Numerics.Vector4[][] arrays) {
            if (arrays == null) return null;
            var result = new IReadOnlyList<Vector4>[arrays.Length];
            for (int t = 0; t < arrays.Length; t++) {
                if (arrays[t] == null) continue;
                var converted = new Vector4[arrays[t].Length];
                for (int i = 0; i < arrays[t].Length; i++) {
                    var v = arrays[t][i];
                    converted[i] = new Vector4(v.X, v.Y, v.Z, v.W);
                }
                result[t] = converted;
            }
            return result;
        }

        static PrimitiveType MapPrimitiveType(GltfPrimitiveType type) => type switch {
            GltfPrimitiveType.POINTS => PrimitiveType.Points,
            GltfPrimitiveType.LINES => PrimitiveType.LineList,
            GltfPrimitiveType.LINE_LOOP => PrimitiveType.LineLoop,
            GltfPrimitiveType.LINE_STRIP => PrimitiveType.LineStrip,
            GltfPrimitiveType.TRIANGLES => PrimitiveType.TriangleList,
            GltfPrimitiveType.TRIANGLE_STRIP => PrimitiveType.TriangleStrip,
            GltfPrimitiveType.TRIANGLE_FAN => PrimitiveType.TriangleFan,
            _ => PrimitiveType.TriangleList
        };

        static void ConvertSkins(ModelRoot modelRoot, ModelData modelData, Dictionary<Node, int> nodeToIndex) {
            // 查找第一个有 Skin 的节点
            // 注意：glTF 允许一个模型有多个 Skin（用于不同的网格），但大多数模型只有一个
            // 这是一个简化处理，未来可以扩展为支持多个 Skin
            Skin firstSkin = null;
            foreach (Node node in modelRoot.LogicalNodes) {
                if (node.Skin != null) {
                    firstSkin = node.Skin;
                    break;
                }
            }

            if (firstSkin == null) {
                return; // 没有蒙皮数据
            }

            // 提取关节索引
            IReadOnlyList<Node> skinJoints = firstSkin.Joints;
            int jointCount = skinJoints.Count;

            int[] jointIndices = new int[jointCount];
            for (int i = 0; i < jointCount; i++) {
                Node joint = skinJoints[i];
                jointIndices[i] = nodeToIndex.TryGetValue(joint, out int idx) ? idx : 0;
            }

            // 提取逆绑定矩阵
            IReadOnlyList<System.Numerics.Matrix4x4> inverseBindMatrices = firstSkin.InverseBindMatrices;
            Matrix[] ibm = new Matrix[jointCount];
            for (int i = 0; i < jointCount; i++) {
                if (i < inverseBindMatrices.Count) {
                    ibm[i] = inverseBindMatrices[i];
                } else {
                    ibm[i] = Matrix.Identity;
                }
            }

            // 获取骨架根节点索引
            int skeletonRootIndex = -1;
            if (firstSkin.Skeleton != null && nodeToIndex.TryGetValue(firstSkin.Skeleton, out int rootIdx)) {
                skeletonRootIndex = rootIdx;
            }

            modelData.Skin = new ModelSkin {
                JointIndices = jointIndices,
                InverseBindMatrices = ibm,
                SkeletonRootIndex = skeletonRootIndex
            };
        }

        static void ConvertAnimations(ModelRoot modelRoot, ModelData modelData) {
            // 构建 material source index → ModelMaterial 查找表
            Dictionary<int, ModelMaterial> materialsByIndex = new();
            foreach (ModelMaterial mat in modelData.Materials) {
                if (mat.SourceMaterialIndex >= 0) {
                    materialsByIndex[mat.SourceMaterialIndex] = mat;
                }
            }

            foreach (SharpGLTF.Schema2.Animation anim in modelRoot.LogicalAnimations) {
                ModelAnimation modelAnim = new() {
                    Name = anim.Name ?? $"Animation{anim.LogicalIndex}",
                    Duration = (float)anim.Duration
                };

                foreach (AnimationChannel channel in anim.Channels) {
                    if (channel.TargetNodePath == PropertyPath.pointer) {
                        // KHR_animation_pointer 通道
                        string path = channel.TargetPointerPath;
                        if (path != null && path.StartsWith("/nodes/")) {
                            Action<float, Model> nodeTarget = CreateNodeVisibilityTarget(channel, modelData.GltfNodeToMeshIndex, modelData.GltfNodeToLightIndex);
                            if (nodeTarget != null) {
                                modelAnim.NodeVisibilityTargets.Add(nodeTarget);
                            }
                        } else {
                            Action<float> target = CreatePointerTarget(channel, materialsByIndex);
                            if (target != null) {
                                modelAnim.PointerTargets.Add(target);
                            }
                        }
                    }
                    else {
                        // 标准骨骼动画通道
                        ModelAnimation.AnimationChannel modelChannel = new() {
                            TargetBoneName = channel.TargetNode?.Name ?? $"Node{channel.TargetNode?.LogicalIndex ?? 0}",
                            Property = ConvertAnimationProperty(channel.TargetNodePath)
                        };
                        modelChannel.Sampler = ConvertSamplerByPath(channel);
                        modelAnim.Channels.Add(modelChannel);
                    }
                }

                modelData.Animations.Add(modelAnim);
            }
        }

        static ModelAnimation.AnimationProperty ConvertAnimationProperty(PropertyPath path) {
            return path switch {
                PropertyPath.translation => ModelAnimation.AnimationProperty.Translation,
                PropertyPath.rotation => ModelAnimation.AnimationProperty.Rotation,
                PropertyPath.scale => ModelAnimation.AnimationProperty.Scale,
                PropertyPath.weights => ModelAnimation.AnimationProperty.Weights,
                _ => ModelAnimation.AnimationProperty.Translation
            };
        }

        static ModelAnimation.AnimationSampler ConvertSamplerByPath(AnimationChannel channel) {
            ModelAnimation.AnimationSampler result = new();
            var path = channel.TargetNodePath;

            try {
                // 获取关键帧数量和时长
                float duration = (float)channel.LogicalParent.Duration;
                int keyCount = EstimateKeyFrameCount(channel);

                if (keyCount == 0) {
                    return result;
                }

                // 均匀采样动画数据
                List<float> times = new();
                List<Vector3> translations = new();
                List<Quaternion> rotations = new();
                List<Vector3> scales = new();

                for (int i = 0; i <= keyCount; i++) {
                    float t = (duration * i) / keyCount;
                    times.Add(t);

                    if (path == PropertyPath.translation) {
                        var sampler = channel.GetSamplerOrNull<System.Numerics.Vector3>();
                        if (sampler != null) {
                            var curveSampler = sampler.CreateCurveSampler(true);
                            var value = curveSampler.GetPoint(t);
                            translations.Add(new Vector3(value.X, value.Y, value.Z));
                        }
                    } else if (path == PropertyPath.rotation) {
                        var sampler = channel.GetSamplerOrNull<System.Numerics.Quaternion>();
                        if (sampler != null) {
                            var curveSampler = sampler.CreateCurveSampler(true);
                            var value = curveSampler.GetPoint(t);
                            rotations.Add(new Quaternion(value.X, value.Y, value.Z, value.W));
                        }
                    } else if (path == PropertyPath.scale) {
                        var sampler = channel.GetSamplerOrNull<System.Numerics.Vector3>();
                        if (sampler != null) {
                            var curveSampler = sampler.CreateCurveSampler(true);
                            var value = curveSampler.GetPoint(t);
                            scales.Add(new Vector3(value.X, value.Y, value.Z));
                        }
                    }
                }

                result.KeyTimes = times.ToArray();
                result.Translations = translations.ToArray();
                result.Rotations = rotations.ToArray();
                result.Scales = scales.ToArray();
                result.Interpolation = ModelAnimation.InterpolationType.Linear;
            } catch (Exception ex) {
                // 动画转换失败时记录错误，但继续处理其他动画
                // 注意：这里不抛出异常，允许部分动画数据加载成功
                Log.Warning($"[GltfLoader] Animation conversion warning: {ex.Message}");
            }

            return result;
        }

        #region KHR_animation_pointer

        static Action<float> CreatePointerTarget(AnimationChannel channel, Dictionary<int, ModelMaterial> materialsByIndex) {
            string path = channel.TargetPointerPath;
            if (string.IsNullOrEmpty(path)) return null;

            string[] segments = path.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3) return null;

            try {
                if (segments[0] == "materials") {
                    return CreateMaterialPointerTarget(segments, channel, materialsByIndex);
                }
            }
            catch (Exception ex) {
                Log.Warning($"[GltfLoader] Pointer target failed for {path}: {ex.Message}");
            }
            return null;
        }

        static Action<float> CreateMaterialPointerTarget(string[] segments, AnimationChannel channel, Dictionary<int, ModelMaterial> materialsByIndex) {
            if (!int.TryParse(segments[1], out int materialIndex)) return null;
            if (!materialsByIndex.TryGetValue(materialIndex, out ModelMaterial mat)) return null;

            string propertyPath = string.Join("/", segments, 2, segments.Length - 2);

            // Core PBR
            switch (propertyPath) {
                case "pbrMetallicRoughness/baseColorFactor":
                    return CreateVec4Target(channel, v => { mat.BaseColorFactor = v; mat.Version++; });
                case "pbrMetallicRoughness/metallicFactor":
                    return CreateFloatTarget(channel, v => { mat.MetallicFactor = v; mat.Version++; });
                case "pbrMetallicRoughness/roughnessFactor":
                    return CreateFloatTarget(channel, v => { mat.RoughnessFactor = v; mat.Version++; });
                case "emissiveFactor":
                    return CreateVec3Target(channel, v => { mat.EmissiveFactor = v; mat.Version++; });
                case "alphaCutoff":
                    return CreateFloatTarget(channel, v => { mat.AlphaCutoff = v; mat.Version++; });
            }

            // Texture transform
            if (propertyPath.Contains("/extensions/KHR_texture_transform/")) {
                return CreateTextureTransformTarget(propertyPath, channel, mat);
            }

            // Extensions
            if (propertyPath.StartsWith("extensions/")) {
                return CreateExtensionTarget(propertyPath.Substring(11), channel, mat);
            }
            return null;
        }

        static Action<float> CreateTextureTransformTarget(string propertyPath, AnimationChannel channel, ModelMaterial mat) {
            const string suffix = "/extensions/KHR_texture_transform/";
            int idx = propertyPath.IndexOf(suffix);
            if (idx < 0) return null;
            string texturePath = propertyPath.Substring(0, idx);
            string propName = propertyPath.Substring(idx + suffix.Length);

            ModelMaterialTexture tex = GetMaterialTexture(mat, texturePath);
            if (tex == null) return null;

            return propName switch {
                "offset" => CreateVec2Target(channel, v => { tex.Offset = v; tex.RecomputeUVTransform(); mat.Version++; }),
                "scale" => CreateVec2Target(channel, v => { tex.Scale = v; tex.RecomputeUVTransform(); mat.Version++; }),
                "rotation" => CreateFloatTarget(channel, v => { tex.Rotation = v; tex.RecomputeUVTransform(); mat.Version++; }),
                _ => null
            };
        }

        static ModelMaterialTexture GetMaterialTexture(ModelMaterial mat, string texturePath) {
            if (texturePath == "pbrMetallicRoughness/baseColorTexture") return mat.BaseColorTexture;
            if (texturePath == "pbrMetallicRoughness/metallicRoughnessTexture") return mat.MetallicRoughnessTexture;
            if (texturePath == "normalTexture") return mat.NormalTexture;
            if (texturePath == "occlusionTexture") return mat.OcclusionTexture;
            if (texturePath == "emissiveTexture") return mat.EmissiveTexture;
            if (texturePath == "extensions/KHR_materials_clearcoat/clearcoatTexture") return mat.ClearCoat?.Texture;
            if (texturePath == "extensions/KHR_materials_clearcoat/clearcoatRoughnessTexture") return mat.ClearCoat?.RoughnessTexture;
            if (texturePath == "extensions/KHR_materials_clearcoat/clearcoatNormalTexture") return mat.ClearCoat?.NormalTexture;
            if (texturePath == "extensions/KHR_materials_sheen/sheenColorTexture") return mat.Sheen?.ColorTexture;
            if (texturePath == "extensions/KHR_materials_sheen/sheenRoughnessTexture") return mat.Sheen?.RoughnessTexture;
            if (texturePath == "extensions/KHR_materials_transmission/transmissionTexture") return mat.Transmission?.Texture;
            if (texturePath == "extensions/KHR_materials_volume/thicknessTexture") return mat.Volume?.ThicknessTexture;
            if (texturePath == "extensions/KHR_materials_iridescence/iridescenceTexture") return mat.Iridescence?.Texture;
            if (texturePath == "extensions/KHR_materials_iridescence/iridescenceThicknessTexture") return mat.Iridescence?.ThicknessTexture;
            if (texturePath == "extensions/KHR_materials_specular/specularTexture") return mat.Specular?.SpecularTexture;
            if (texturePath == "extensions/KHR_materials_specular/specularColorTexture") return mat.Specular?.SpecularColorTexture;
            if (texturePath == "extensions/KHR_materials_anisotropy/anisotropyTexture") return mat.Anisotropy?.AnisotropyTexture;
            if (texturePath == "extensions/KHR_materials_diffuse_transmission/diffuseTransmissionTexture") return mat.DiffuseTransmission?.Texture;
            if (texturePath == "extensions/KHR_materials_diffuse_transmission/diffuseTransmissionColorTexture") return mat.DiffuseTransmission?.ColorTexture;
            return null;
        }

        static Action<float> CreateExtensionTarget(string extPath, AnimationChannel channel, ModelMaterial mat) {
            string[] parts = extPath.Split('/');
            if (parts.Length < 2) return null;
            string ext = parts[0], prop = parts[1];

            switch (ext) {
                case "KHR_materials_emissive_strength":
                    if (mat.EmissiveStrength != null && prop == "emissiveStrength")
                        return CreateFloatTarget(channel, v => { mat.EmissiveStrength.EmissiveStrength = v; mat.Version++; });
                    break;
                case "KHR_materials_ior":
                    if (mat.Ior != null && prop == "ior")
                        return CreateFloatTarget(channel, v => { mat.Ior.Ior = v; mat.Version++; });
                    break;
                case "KHR_materials_specular":
                    if (mat.Specular != null) {
                        if (prop == "specularFactor")
                            return CreateFloatTarget(channel, v => { mat.Specular.SpecularFactor = v; mat.Version++; });
                        if (prop == "specularColorFactor")
                            return CreateVec3Target(channel, v => { mat.Specular.SpecularColorFactor = v; mat.Version++; });
                    }
                    break;
                case "KHR_materials_sheen":
                    if (mat.Sheen != null) {
                        if (prop == "sheenColorFactor")
                            return CreateVec3Target(channel, v => { mat.Sheen.ColorFactor = v; mat.Version++; });
                        if (prop == "sheenRoughnessFactor")
                            return CreateFloatTarget(channel, v => { mat.Sheen.RoughnessFactor = v; mat.Version++; });
                    }
                    break;
                case "KHR_materials_clearcoat":
                    if (mat.ClearCoat != null) {
                        if (prop == "clearcoatFactor")
                            return CreateFloatTarget(channel, v => { mat.ClearCoat.Factor = v; mat.Version++; });
                        if (prop == "clearcoatRoughnessFactor")
                            return CreateFloatTarget(channel, v => { mat.ClearCoat.RoughnessFactor = v; mat.Version++; });
                    }
                    break;
                case "KHR_materials_transmission":
                    if (mat.Transmission != null && prop == "transmissionFactor")
                        return CreateFloatTarget(channel, v => { mat.Transmission.Factor = v; mat.Version++; });
                    break;
                case "KHR_materials_volume":
                    if (mat.Volume != null) {
                        if (prop == "thicknessFactor")
                            return CreateFloatTarget(channel, v => { mat.Volume.ThicknessFactor = v; mat.Version++; });
                        if (prop == "attenuationDistance")
                            return CreateFloatTarget(channel, v => { mat.Volume.AttenuationDistance = v; mat.Version++; });
                        if (prop == "attenuationColor")
                            return CreateVec3Target(channel, v => { mat.Volume.AttenuationColor = v; mat.Version++; });
                    }
                    break;
                case "KHR_materials_iridescence":
                    if (mat.Iridescence != null) {
                        if (prop == "iridescenceFactor")
                            return CreateFloatTarget(channel, v => { mat.Iridescence.Factor = v; mat.Version++; });
                        if (prop == "iridescenceIor")
                            return CreateFloatTarget(channel, v => { mat.Iridescence.IOR = v; mat.Version++; });
                        if (prop == "iridescenceThicknessMinimum")
                            return CreateFloatTarget(channel, v => { mat.Iridescence.ThicknessMinimum = v; mat.Version++; });
                        if (prop == "iridescenceThicknessMaximum")
                            return CreateFloatTarget(channel, v => { mat.Iridescence.ThicknessMaximum = v; mat.Version++; });
                    }
                    break;
                case "KHR_materials_anisotropy":
                    if (mat.Anisotropy != null) {
                        if (prop == "anisotropyStrength")
                            return CreateFloatTarget(channel, v => { mat.Anisotropy.AnisotropyStrength = v; mat.Version++; });
                        if (prop == "anisotropyRotation")
                            return CreateFloatTarget(channel, v => { mat.Anisotropy.AnisotropyRotation = v; mat.Version++; });
                    }
                    break;
                case "KHR_materials_dispersion":
                    if (mat.Dispersion != null && prop == "dispersion")
                        return CreateFloatTarget(channel, v => { mat.Dispersion.Dispersion = v; mat.Version++; });
                    break;
                case "KHR_materials_volume_scatter":
                    if (mat.VolumeScatter != null) {
                        if (prop == "multiscatterColor")
                            return CreateVec3Target(channel, v => { mat.VolumeScatter.MultiscatterColor = v; mat.Version++; });
                        if (prop == "scatterAnisotropy")
                            return CreateFloatTarget(channel, v => { mat.VolumeScatter.ScatterAnisotropy = v; mat.Version++; });
                    }
                    break;
                case "KHR_materials_diffuse_transmission":
                    if (mat.DiffuseTransmission != null) {
                        if (prop == "diffuseTransmissionFactor")
                            return CreateFloatTarget(channel, v => { mat.DiffuseTransmission.Factor = v; mat.Version++; });
                        if (prop == "diffuseTransmissionColorFactor")
                            return CreateVec3Target(channel, v => { mat.DiffuseTransmission.ColorFactor = v; mat.Version++; });
                    }
                    break;
            }
            return null;
        }

        // Typed closure factories — isolateMemory=true ensures independence from ModelRoot

        static Action<float> CreateFloatTarget(AnimationChannel channel, Action<float> set) {
            var sampler = channel.GetSamplerOrNull<float>();
            if (sampler == null) return null;
            var curve = sampler.CreateCurveSampler(true);
            return time => set(curve.GetPoint(time));
        }

        static Action<float> CreateVec2Target(AnimationChannel channel, Action<Vector2> set) {
            var sampler = channel.GetSamplerOrNull<System.Numerics.Vector2>();
            if (sampler == null) return null;
            var curve = sampler.CreateCurveSampler(true);
            return time => set(new Vector2(curve.GetPoint(time).X, curve.GetPoint(time).Y));
        }

        static Action<float> CreateVec3Target(AnimationChannel channel, Action<Vector3> set) {
            var sampler = channel.GetSamplerOrNull<System.Numerics.Vector3>();
            if (sampler == null) return null;
            var curve = sampler.CreateCurveSampler(true);
            return time => { var v = curve.GetPoint(time); set(new Vector3(v.X, v.Y, v.Z)); };
        }

        static Action<float> CreateVec4Target(AnimationChannel channel, Action<Vector4> set) {
            var sampler = channel.GetSamplerOrNull<System.Numerics.Vector4>();
            if (sampler == null) return null;
            var curve = sampler.CreateCurveSampler(true);
            return time => { var v = curve.GetPoint(time); set(new Vector4(v.X, v.Y, v.Z, v.W)); };
        }

        static int EstimateKeyFrameCount(AnimationChannel channel) {
            float duration = (float)channel.LogicalParent.Duration;
            int estimatedFrames = Math.Max(1, (int)(duration * 30f));
            return Math.Min(estimatedFrames, 300);
        }

        static Action<float, Model> CreateNodeVisibilityTarget(AnimationChannel channel,
            Dictionary<int, int> nodeToMeshIndex, Dictionary<int, int> nodeToLightIndex) {
            string path = channel.TargetPointerPath;
            // Expected: /nodes/{index}/extensions/KHR_node_visibility/visible
            if (!path.StartsWith("/nodes/")) return null;

            string[] segments = path.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 5 || segments[0] != "nodes" || segments[2] != "extensions")
                return null;

            if (!int.TryParse(segments[1], out int nodeIndex)) return null;

            var sampler = channel.GetSamplerOrNull<float>();
            if (sampler == null) return null;
            var curve = sampler.CreateCurveSampler(true);

            // 查找此节点对应的 mesh 和/或 light
            int meshIndex = nodeToMeshIndex.TryGetValue(nodeIndex, out int mi) ? mi : -1;
            int lightIndex = nodeToLightIndex.TryGetValue(nodeIndex, out int li) ? li : -1;

            if (meshIndex < 0 && lightIndex < 0) return null;

            return (time, model) => {
                if (model == null) return;
                float value = curve.GetPoint(time);
                bool visible = value >= 0.5f;
                if (meshIndex >= 0 && meshIndex < model.Meshes.Count) {
                    // 一个 glTF mesh 可能被拆分为多个 ModelMesh（每个 primitive 一个），
                    // 通过 ParentBone 查找所有同级 mesh
                    ModelBone bone = model.Meshes[meshIndex].ParentBone;
                    for (int i = 0; i < model.Meshes.Count; i++) {
                        if (model.Meshes[i].ParentBone == bone) {
                            model.Meshes[i].IsVisible = visible;
                        }
                    }
                }
                if (lightIndex >= 0 && lightIndex < model.Lights.Count) {
                    model.Lights[lightIndex].IsVisible = visible;
                }
            };
        }

        #endregion

        static System.Numerics.Vector4[] GenerateTangents(
            System.Numerics.Vector3[] positions,
            System.Numerics.Vector3[] normals,
            System.Numerics.Vector2[] uvs,
            uint[] indices) {
            if (positions == null || normals == null || uvs == null || indices == null) return null;
            int vertexCount = positions.Length;
            if (vertexCount == 0 || normals.Length < vertexCount || uvs.Length < vertexCount) return null;

            var tan1 = new System.Numerics.Vector3[vertexCount];
            var tan2 = new System.Numerics.Vector3[vertexCount];

            for (int i = 0; i + 2 < indices.Length; i += 3) {
                int i0 = (int)indices[i], i1 = (int)indices[i + 1], i2 = (int)indices[i + 2];
                if ((uint)i0 >= vertexCount || (uint)i1 >= vertexCount || (uint)i2 >= vertexCount) continue;

                var p0 = positions[i0]; var p1 = positions[i1]; var p2 = positions[i2];
                var uv0 = uvs[i0]; var uv1 = uvs[i1]; var uv2 = uvs[i2];

                var edge1 = p1 - p0; var edge2 = p2 - p0;
                var duv1 = uv1 - uv0; var duv2 = uv2 - uv0;

                float denom = duv1.X * duv2.Y - duv2.X * duv1.Y;
                if (MathF.Abs(denom) < 1e-8f) continue;
                float inv = 1f / denom;

                var sdir = (edge1 * duv2.Y - edge2 * duv1.Y) * inv;
                var tdir = (edge2 * duv1.X - edge1 * duv2.X) * inv;

                tan1[i0] += sdir; tan1[i1] += sdir; tan1[i2] += sdir;
                tan2[i0] += tdir; tan2[i1] += tdir; tan2[i2] += tdir;
            }

            var tangents = new System.Numerics.Vector4[vertexCount];
            for (int i = 0; i < vertexCount; i++) {
                var n = normals[i];
                if (n.LengthSquared() < float.Epsilon) {
                    tangents[i] = new System.Numerics.Vector4(1f, 0f, 0f, 1f);
                    continue;
                }
                n = System.Numerics.Vector3.Normalize(n);
                var t = tan1[i];

                if (t.LengthSquared() < 1e-12f) {
                    // 零切线退化：选一个垂直于法线的方向
                    var axis = MathF.Abs(n.Y) < 0.999f
                        ? System.Numerics.Vector3.UnitY
                        : System.Numerics.Vector3.UnitX;
                    t = System.Numerics.Vector3.Cross(axis, n);
                    if (t.LengthSquared() < 1e-12f) t = System.Numerics.Vector3.UnitX;
                    tangents[i] = new System.Numerics.Vector4(System.Numerics.Vector3.Normalize(t), 1f);
                    continue;
                }

                t = System.Numerics.Vector3.Normalize(t - n * System.Numerics.Vector3.Dot(n, t));
                var b = System.Numerics.Vector3.Cross(n, t);
                // glTF 约定：bitangent = cross(N, T) * w，与 Lengyel 标准公式方向相反
                float w = System.Numerics.Vector3.Dot(b, tan2[i]) < 0f ? 1f : -1f;
                tangents[i] = new System.Numerics.Vector4(t, w);
            }
            return tangents;
        }

        static unsafe void WriteFloat(byte[] buffer, int offset, float value) {
            // 使用指针直接写入，避免 BitConverter.GetBytes 的数组分配
            fixed (byte* ptr = &buffer[offset]) {
                *(float*)ptr = value;
            }
        }

        static void WriteVector2(byte[] buffer, int offset, float x, float y) {
            WriteFloat(buffer, offset, x);
            WriteFloat(buffer, offset + 4, y);
        }

        static void WriteVector3(byte[] buffer, int offset, float x, float y, float z) {
            WriteFloat(buffer, offset, x);
            WriteFloat(buffer, offset + 4, y);
            WriteFloat(buffer, offset + 8, z);
        }

        static void WriteVector4(byte[] buffer, int offset, float x, float y, float z, float w) {
            WriteFloat(buffer, offset, x);
            WriteFloat(buffer, offset + 4, y);
            WriteFloat(buffer, offset + 8, z);
            WriteFloat(buffer, offset + 12, w);
        }

        static void WriteIndex32(byte[] buffer, int elementIndex, uint value) {
            int offset = elementIndex * 4;
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        static BoundingBox CalculateBoundingBoxFromPositions(SharpGLTF.Memory.IAccessorArray<System.Numerics.Vector3> positions, uint[] indices) {
            if (positions == null || positions.Count == 0) {
                return new BoundingBox(Vector3.Zero, Vector3.Zero);
            }

            Vector3 min = new(float.MaxValue);
            Vector3 max = new(float.MinValue);

            if (indices != null && indices.Length > 0) {
                foreach (uint idx in indices) {
                    if (idx < positions.Count) {
                        var pos = positions[(int)idx];
                        min.X = Math.Min(min.X, pos.X);
                        min.Y = Math.Min(min.Y, pos.Y);
                        min.Z = Math.Min(min.Z, pos.Z);
                        max.X = Math.Max(max.X, pos.X);
                        max.Y = Math.Max(max.Y, pos.Y);
                        max.Z = Math.Max(max.Z, pos.Z);
                    }
                }
            } else {
                for (int i = 0; i < positions.Count; i++) {
                    var pos = positions[i];
                    min.X = Math.Min(min.X, pos.X);
                    min.Y = Math.Min(min.Y, pos.Y);
                    min.Z = Math.Min(min.Z, pos.Z);
                    max.X = Math.Max(max.X, pos.X);
                    max.Y = Math.Max(max.Y, pos.Y);
                    max.Z = Math.Max(max.Z, pos.Z);
                }
            }

            return new BoundingBox(min, max);
        }

        static void CalculateMeshBoundingBox(ModelMeshData meshData, List<ModelMeshPartData> parts) {
            if (parts.Count == 0) {
                meshData.BoundingBox = new BoundingBox(Vector3.Zero, Vector3.Zero);
                return;
            }

            Vector3 min = new(float.MaxValue);
            Vector3 max = new(float.MinValue);

            foreach (var part in parts) {
                var bbox = part.BoundingBox;
                min.X = Math.Min(min.X, bbox.Min.X);
                min.Y = Math.Min(min.Y, bbox.Min.Y);
                min.Z = Math.Min(min.Z, bbox.Min.Z);
                max.X = Math.Max(max.X, bbox.Max.X);
                max.Y = Math.Max(max.Y, bbox.Max.Y);
                max.Z = Math.Max(max.Z, bbox.Max.Z);
            }

            meshData.BoundingBox = new BoundingBox(min, max);
        }
    }
}
