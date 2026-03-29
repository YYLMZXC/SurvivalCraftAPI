namespace Engine.Media {
    /// <summary>
    /// 材质数据（PBR 材质属性）
    /// </summary>
    public class ModelMaterialData {
        /// <summary>
        /// 材质名称
        /// </summary>
        public string Name;

        // Core PBR factors
        public Vector4 BaseColorFactor = Vector4.One;
        public float MetallicFactor = 1f;
        public float RoughnessFactor = 1f;
        public Vector3 EmissiveFactor = Vector3.Zero;

        // Texture indices (-1 = no texture)
        public int BaseColorTextureIndex = -1;
        public int MetallicRoughnessTextureIndex = -1;
        public int NormalTextureIndex = -1;
        public int OcclusionTextureIndex = -1;
        public int EmissiveTextureIndex = -1;

        // Texture parameters
        public float NormalScale = 1f;
        public float OcclusionStrength = 1f;

        // Alpha
        public ModelAlphaMode AlphaMode = ModelAlphaMode.Opaque;
        public float AlphaCutoff = 0.5f;
        public bool DoubleSided;
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
