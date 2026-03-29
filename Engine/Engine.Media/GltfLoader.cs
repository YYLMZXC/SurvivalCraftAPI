#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Engine.Graphics;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using GltfPrimitiveType = SharpGLTF.Schema2.PrimitiveType;

namespace Engine.Media {
    /// <summary>
    /// glTF 模型加载器
    /// </summary>
    public static class GltfLoader {
        /// <summary>
        /// 外置纹理加载回调（预留接口，未来用于加载外部纹理文件）
        /// TODO: 实现纹理加载功能
        /// </summary>
        public static Func<string, Stream> LoadExternalTextureCallback { get; set; }

        /// <summary>
        /// 检查是否为 glTF 文件
        /// </summary>
        public static bool IsGltfFile(string filePath) {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext == ".gltf" || ext == ".glb";
        }

        /// <summary>
        /// 从文件加载 glTF 模型
        /// </summary>
        public static ModelData Load(string filePath) {
            if (!File.Exists(filePath)) {
                throw new FileNotFoundException($"glTF file not found: {filePath}");
            }

            var readSettings = new ReadSettings { Validation = ValidationMode.Skip };
            ModelRoot modelRoot = ModelRoot.Load(filePath, readSettings);

            return ConvertToModelData(modelRoot, Path.GetDirectoryName(filePath));
        }

        /// <summary>
        /// 从流加载 glTF 模型
        /// </summary>
        public static ModelData Load(Stream stream, string basePath = null) {
            // SharpGLTF 需要文件路径来加载模型，使用临时文件
            string tempFile = Path.Combine(Path.GetTempPath(), $"gltf_temp_{Guid.NewGuid()}.glb");
            try {
                using (var fs = File.Create(tempFile)) {
                    stream.CopyTo(fs);
                }
                return Load(tempFile);
            } finally {
                if (File.Exists(tempFile)) {
                    File.Delete(tempFile);
                }
            }
        }

        static ModelData ConvertToModelData(ModelRoot modelRoot, string basePath) {
            ModelData modelData = new();

            // 构建节点名称到索引的映射
            Dictionary<Node, int> nodeToIndex = new();
            List<Node> allNodes = new();

            // 首先收集所有节点
            foreach (Node node in modelRoot.LogicalNodes) {
                nodeToIndex[node] = allNodes.Count;
                allNodes.Add(node);
            }

            // 转换骨骼/节点数据
            ConvertBones(modelRoot, modelData, allNodes, nodeToIndex);

            // 转换网格数据
            ConvertMeshes(modelRoot, modelData, allNodes, nodeToIndex);

            // 转换动画数据
            ConvertAnimations(modelRoot, modelData);

            return modelData;
        }

        static void ConvertBones(ModelRoot modelRoot, ModelData modelData, List<Node> allNodes, Dictionary<Node, int> nodeToIndex) {
            foreach (Node node in allNodes) {
                ModelBoneData bone = new() {
                    Name = node.Name ?? $"Node{node.LogicalIndex}",
                    Transform = ConvertMatrix(node.LocalMatrix)
                };

                // 查找父节点索引
                Node parent = node.VisualParent;
                bone.ParentBoneIndex = parent != null && nodeToIndex.TryGetValue(parent, out int parentIndex)
                    ? parentIndex
                    : -1;

                modelData.Bones.Add(bone);
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

        static void ConvertMeshes(ModelRoot modelRoot, ModelData modelData, List<Node> allNodes, Dictionary<Node, int> nodeToIndex) {
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
                ProcessNodeForMesh(node, modelData, allNodes, nodeToIndex, ref bufferIndex);
            }
        }

        static void ProcessNodeForMesh(Node node, ModelData modelData, List<Node> allNodes, Dictionary<Node, int> nodeToIndex, ref int bufferIndex) {
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

                    ModelMeshPartData meshPart = ProcessPrimitive(primitive, modelData, ref bufferIndex);
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
                ProcessNodeForMesh(child, modelData, allNodes, nodeToIndex, ref bufferIndex);
            }
        }

        static ModelMeshPartData ProcessPrimitive(MeshPrimitive primitive, ModelData modelData, ref int bufferIndex) {
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
            List<VertexElement> elements = new();
            int offset = 0;

            // Position (Vector3)
            elements.Add(new VertexElement(offset, VertexElementFormat.Vector3, VertexElementSemantic.Position));
            offset += 12;

            // Normal (Vector3)
            bool hasNormals = normals != null;
            if (hasNormals) {
                elements.Add(new VertexElement(offset, VertexElementFormat.Vector3, VertexElementSemantic.Normal));
                offset += 12;
            }

            // UV0 (Vector2)
            bool hasUV0 = uv0 != null;
            if (hasUV0) {
                elements.Add(new VertexElement(offset, VertexElementFormat.Vector2, VertexElementSemantic.TextureCoordinate));
                offset += 8;
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

                // Normal
                if (hasNormals) {
                    var normal = normals[i];
                    WriteVector3(vertexBuffer, baseOffset + currentOffset, normal.X, normal.Y, normal.Z);
                    currentOffset += 12;
                }

                // UV0
                if (hasUV0) {
                    var uv = uv0[i];
                    WriteVector2(vertexBuffer, baseOffset + currentOffset, uv.X, uv.Y);
                    currentOffset += 8;
                }

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

            return meshPart;
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
