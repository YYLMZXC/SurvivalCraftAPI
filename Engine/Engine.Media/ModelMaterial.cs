namespace Engine.Media {
    /// <summary>
    /// 材质数据（PBR 材质属性）
    /// </summary>
    public class ModelMaterial {
        /// <summary>
        /// 材质名称
        /// </summary>
        public string Name;

        // Core PBR factors
        public Vector4 BaseColorFactor = Vector4.One;
        public float MetallicFactor = 1f;
        public float RoughnessFactor = 1f;
        public Vector3 EmissiveFactor = Vector3.Zero;

        // Core PBR textures (with UV transform support)
        public ModelMaterialTexture BaseColorTexture;
        public ModelMaterialTexture MetallicRoughnessTexture;
        public ModelMaterialTexture NormalTexture;
        public ModelMaterialTexture OcclusionTexture;
        public ModelMaterialTexture EmissiveTexture;

        // Texture parameters
        public float NormalScale = 1f;
        public float OcclusionStrength = 1f;

        // Alpha
        public ModelAlphaMode AlphaMode = ModelAlphaMode.Opaque;
        public float AlphaCutoff = 0.5f;
        public bool DoubleSided;

        /// <summary>
        /// 源材质在 glTF 中的逻辑索引（用于 KHR_animation_pointer）
        /// </summary>
        public int SourceMaterialIndex { get; set; } = -1;
    }

    /// <summary>
    /// Alpha 模式
    /// </summary>
    public enum ModelAlphaMode {
        Opaque,
        Mask,
        Blend
    }
}
