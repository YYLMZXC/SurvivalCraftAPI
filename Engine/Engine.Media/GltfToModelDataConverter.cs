#nullable disable

using Engine.Graphics;

namespace Engine.Media {
    /// <summary>
    /// glTF 到 ModelData 转换器
    /// TODO: 实现 SharpGLTF 数据转换
    /// </summary>
    public static class GltfToModelDataConverter {
        /// <summary>
        /// 将 glTF 数据转换为 ModelData
        /// </summary>
        public static ModelData Convert(
            object root,
            string basePath,
            Func<string, Stream> loadExternalTextureCallback) {

            // TODO: 实现转换逻辑
            return new ModelData {
                Bones = { new ModelBoneData { Name = "Root", ParentBoneIndex = -1, Transform = Matrix.Identity } }
            };
        }
    }
}
