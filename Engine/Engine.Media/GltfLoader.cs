#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Engine.Graphics;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using GltfPrimitiveType = SharpGLTF.Schema2.PrimitiveType;
using SharpGLTF.Memory;
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
        /// 外置纹理加载回调
        /// 参数：纹理相对路径，返回：纹理数据流
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
        /// 从流加载 glTF 模型
        /// </summary>
        /// <param name="stream">模型数据流（GLB 或 glTF JSON）</param>
        /// <param name="basePath">模型数据流的基路径（用于加载外部资源）</param>
        /// <returns>ModelData 实例</returns>
        public static ModelData Load(Stream stream, string basePath = null) {
            ArgumentNullException.ThrowIfNull(stream);
            BYTES FileReaderCallback(string assetName) {
                string path = basePath == null ? assetName : Storage.CombinePaths(basePath, assetName);
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
                    Transform = ConvertMatrix(node.LocalMatrix),
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

                bool isSrgb = textureIsSrgb.GetValueOrDefault(gltfTexture.LogicalIndex, true);

                ModelTextureInfo texInfo = new() {
                    Name = image.Name ?? $"Texture{image.LogicalIndex}",
                    IsSrgb = isSrgb
                };

                // 检查是内嵌纹理还是外置纹理
                // SharpGLTF 在加载模型时会自动加载外置资源，所以 Content 不会是空的
                // 但我们可以通过 SourcePath 来判断来源：
                // - 内嵌纹理（GLB）：SourcePath 为 null 或空
                // - 外置纹理（glTF）：SourcePath 包含文件路径
                string sourcePath = image.Content.SourcePath;
                if (!string.IsNullOrEmpty(sourcePath)) {
                    texInfo.SourceImage = image.Content;
                } else {
                    // 内嵌纹理：保存图像数据
                    texInfo.SourceImage = image.Content;
                }

                // 设置采样器状态
                texInfo.SetSampler(gltfTexture.Sampler);

                modelData.Textures.Add(texInfo);
            }

            // 3. 加载所有材质
            foreach (GltfMaterial gltfMaterial in modelRoot.LogicalMaterials) {
                int matIndex = modelData.Materials.Count;
                materialToIndex[gltfMaterial] = matIndex;

                ModelMaterialData matData = new() {
                    Name = gltfMaterial.Name ?? $"Material{gltfMaterial.LogicalIndex}"
                };

                // 加载材质属性
                LoadMaterialProperties(gltfMaterial, matData, textureToIndex);

                modelData.Materials.Add(matData);
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
        static void LoadMaterialProperties(GltfMaterial gltfMaterial, ModelMaterialData matData, Dictionary<GltfTexture, int> textureToIndex) {
            // BaseColor
            MaterialChannel? channel = gltfMaterial.FindChannel("BaseColor");
            if (channel != null) {
                var color = channel.Value.Color;
                matData.BaseColorFactor = new Vector4(color.X, color.Y, color.Z, color.W);
                matData.BaseColorTextureIndex = GetTextureIndex(channel.Value.Texture, textureToIndex);
            }

            // MetallicRoughness
            channel = gltfMaterial.FindChannel("MetallicRoughness");
            if (channel != null) {
                matData.MetallicFactor = GetFactorSafe(channel.Value, "MetallicFactor", 1f);
                matData.RoughnessFactor = GetFactorSafe(channel.Value, "RoughnessFactor", 1f);
                matData.MetallicRoughnessTextureIndex = GetTextureIndex(channel.Value.Texture, textureToIndex);
            }

            // Normal
            channel = gltfMaterial.FindChannel("Normal");
            if (channel != null) {
                matData.NormalScale = GetFactorSafe(channel.Value, "NormalScale", 1f);
                matData.NormalTextureIndex = GetTextureIndex(channel.Value.Texture, textureToIndex);
            }

            // Occlusion
            channel = gltfMaterial.FindChannel("Occlusion");
            if (channel != null) {
                matData.OcclusionStrength = GetFactorSafe(channel.Value, "OcclusionStrength", 1f);
                matData.OcclusionTextureIndex = GetTextureIndex(channel.Value.Texture, textureToIndex);
            }

            // Emissive
            channel = gltfMaterial.FindChannel("Emissive");
            if (channel != null) {
                var emissive = channel.Value.Color;
                matData.EmissiveFactor = new Vector3(emissive.X, emissive.Y, emissive.Z);
                matData.EmissiveTextureIndex = GetTextureIndex(channel.Value.Texture, textureToIndex);
            }

            // Alpha mode
            matData.AlphaMode = gltfMaterial.Alpha switch {
                AlphaMode.BLEND => ModelAlphaMode.Blend,
                AlphaMode.MASK => ModelAlphaMode.Mask,
                _ => ModelAlphaMode.Opaque
            };
            matData.AlphaCutoff = gltfMaterial.AlphaCutoff;
            matData.DoubleSided = gltfMaterial.DoubleSided;
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
            Dictionary<Node, int> nodeToIndex, ref int bufferIndex, Dictionary<GltfMaterial, int> materialToIndex) {
            if (node.Mesh != null) {
                int boneIndex = nodeToIndex.TryGetValue(node, out int idx) ? idx : 0;

                ModelMeshData meshData = new() {
                    Name = node.Mesh.Name ?? $"Mesh{node.Mesh.LogicalIndex}",
                    ParentBoneIndex = boneIndex
                };

                foreach (MeshPrimitive primitive in node.Mesh.Primitives) {
                    if (primitive.DrawPrimitiveType != GltfPrimitiveType.TRIANGLES) {
                        continue;
                    }

                    ModelMeshPartData meshPart = ProcessPrimitive(primitive, modelData, ref bufferIndex, materialToIndex);
                    if (meshPart != null) {
                        meshData.MeshParts.Add(meshPart);
                    }
                }

                if (meshData.MeshParts.Count > 0) {
                    // 计算包围盒
                    CalculateMeshBoundingBox(meshData, meshData.MeshParts);
                    modelData.Meshes.Add(meshData);
                }
            }

            foreach (Node child in node.VisualChildren) {
                ProcessNodeForMesh(child, modelData, allNodes, nodeToIndex, ref bufferIndex, materialToIndex);
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
            var joints = primitive.GetVertexAccessor("JOINTS_0")?.AsVector4Array();
            var weights = primitive.GetVertexAccessor("WEIGHTS_0")?.AsVector4Array();

            // 获取索引数据
            uint[] indices = primitive.GetIndices()?.ToArray();
            if (indices == null || indices.Length == 0) {
                // 如果没有索引，创建顺序索引（避免 LINQ 分配）
                indices = new uint[positions.Count];
                for (int i = 0; i < positions.Count; i++) {
                    indices[i] = (uint)i;
                }
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
            int vertexCount = positions.Count;
            byte[] vertexBuffer = new byte[vertexCount * offset];
            int vertexStride = offset;

            for (int i = 0; i < vertexCount; i++) {
                int baseOffset = i * vertexStride;
                int currentOffset = 0;

                // Position
                var pos = positions[i];
                WriteVector3(vertexBuffer, baseOffset + currentOffset, pos.X, pos.Y, pos.Z);
                currentOffset += 12;

                // Normal - 始终写入，没有数据时使用默认向上法线
                if (hasNormals) {
                    var normal = normals[i];
                    WriteVector3(vertexBuffer, baseOffset + currentOffset, normal.X, normal.Y, normal.Z);
                } else {
                    WriteVector3(vertexBuffer, baseOffset + currentOffset, 0f, 1f, 0f);
                }
                currentOffset += 12;

                // UV0 - 始终写入，没有数据时使用默认值
                if (hasUV0) {
                    var uv = uv0[i];
                    WriteVector2(vertexBuffer, baseOffset + currentOffset, uv.X, uv.Y);
                } else {
                    WriteVector2(vertexBuffer, baseOffset + currentOffset, 0f, 0f);
                }
                currentOffset += 8;

                // BlendIndices 和 BlendWeights
                if (hasSkinning) {
                    var joint = joints[i];
                    var weight = weights[i];

                    // BlendIndices (存储为 float)
                    WriteVector4(buffer: vertexBuffer, baseOffset + currentOffset, joint.X, joint.Y, joint.Z, joint.W);
                    currentOffset += 16;

                    // BlendWeights
                    WriteVector4(buffer: vertexBuffer, baseOffset + currentOffset, weight.X, weight.Y, weight.Z, weight.W);
                    currentOffset += 16;
                }
            }

            // 构建索引缓冲（统一使用 32 位索引，与 Collada 加载器保持一致）
            // glTF 使用逆时针绕序 (CCW)，引擎使用 CullCounterClockwise，需要翻转绕序
            System.Diagnostics.Debug.Assert(indices.Length % 3 == 0,
                $"Index count {indices.Length} is not divisible by 3 - malformed triangle data");
            byte[] indexBuffer = new byte[indices.Length * 4];
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

            // 创建缓冲数据
            ModelBuffersData buffersData = new() {
                VertexDeclaration = vertexDecl,
                Vertices = vertexBuffer,
                Indices = indexBuffer
            };

            modelData.Buffers.Add(buffersData);

            // 计算包围盒
            BoundingBox bbox = CalculateBoundingBoxFromPositions(positions, indices);

            ModelMeshPartData meshPart = new() {
                BuffersDataIndex = bufferIndex++,
                StartIndex = 0,
                IndicesCount = indices.Length,
                BoundingBox = bbox
            };

            // 设置材质索引
            if (primitive.Material != null && materialToIndex.TryGetValue(primitive.Material, out int matIndex)) {
                meshPart.MaterialIndex = matIndex;
            }

            return meshPart;
        }

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
                    ibm[i] = ConvertMatrix(inverseBindMatrices[i]);
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
            foreach (Animation anim in modelRoot.LogicalAnimations) {
                ModelAnimation modelAnim = new() {
                    Name = anim.Name ?? $"Animation{anim.LogicalIndex}",
                    Duration = (float)anim.Duration
                };

                foreach (AnimationChannel channel in anim.Channels) {
                    ModelAnimation.AnimationChannel modelChannel = new() {
                        TargetBoneName = channel.TargetNode?.Name ?? $"Node{channel.TargetNode?.LogicalIndex ?? 0}",
                        Property = ConvertAnimationProperty(channel.TargetNodePath)
                    };

                    // 根据属性类型获取采样器数据
                    modelChannel.Sampler = ConvertSamplerByPath(channel);

                    modelAnim.Channels.Add(modelChannel);
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
                System.Diagnostics.Debug.WriteLine($"[GltfLoader] Animation conversion warning: {ex.Message}");
            }

            return result;
        }

        static int EstimateKeyFrameCount(AnimationChannel channel) {
            // 尝试获取实际的关键帧数量，否则使用默认值
            // 动画采样间隔基于动画时长，每秒约 30 帧
            float duration = (float)channel.LogicalParent.Duration;
            int estimatedFrames = Math.Max(1, (int)(duration * 30f));
            return Math.Min(estimatedFrames, 300); // 限制最大帧数避免内存问题
        }

        static Matrix ConvertMatrix(System.Numerics.Matrix4x4 matrix) {
            return new Matrix(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            );
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
