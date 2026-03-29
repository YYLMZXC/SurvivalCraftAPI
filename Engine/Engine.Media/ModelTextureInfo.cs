using SharpGLTF.Memory;

namespace Engine.Media {
    /// <summary>
    /// 纹理类型枚举
    /// </summary>
    public enum ModelTextureType {
        None,
        BaseColor,      // sRGB
        Normal,         // Linear
        MetallicRoughness, // Linear
        Occlusion,      // Linear
        Emissive        // sRGB
    }

    /// <summary>
    /// 纹理信息（支持延迟加载）
    /// </summary>
    public class ModelTextureInfo {
        /// <summary>
        /// 纹理名称
        /// </summary>
        public string Name;

        /// <summary>
        /// 纹理类型
        /// </summary>
        public ModelTextureType Type;

        /// <summary>
        /// 是否为 sRGB 颜色空间
        /// </summary>
        public bool IsSrgb;

        /// <summary>
        /// 原始图像数据（用于延迟加载）
        /// </summary>
        public MemoryImage SourceImage;
    }
}
