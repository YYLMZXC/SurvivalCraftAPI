#nullable disable

namespace Engine.Media {
    /// <summary>
    /// glTF 模型加载器
    /// </summary>
    public static class GltfLoader {
        /// <summary>
        /// 外置纹理加载回调（由 Survivalcraft 项目设置）
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
            // TODO: 实现 SharpGLTF 加载
            // 目前返回空的 ModelData
            return new ModelData {
                Bones = { new ModelBoneData { Name = "Root", ParentBoneIndex = -1, Transform = Matrix.Identity } }
            };
        }

        /// <summary>
        /// 从流加载 glTF 模型
        /// </summary>
        public static ModelData Load(Stream stream, string basePath = null) {
            // TODO: 实现 SharpGLTF 加载
            return new ModelData {
                Bones = { new ModelBoneData { Name = "Root", ParentBoneIndex = -1, Transform = Matrix.Identity } }
            };
        }
    }
}
