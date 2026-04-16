using System.Collections.Generic;
using Engine.Graphics;

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

        // Extensions - known types for convenience
        public ClearCoatExtension ClearCoat { get; set; }
        public IridescenceExtension Iridescence { get; set; }
        public TransmissionExtension Transmission { get; set; }
        public VolumeExtension Volume { get; set; }
        public SheenExtension Sheen { get; set; }
        public SpecularExtension Specular { get; set; }
        public IorExtension Ior { get; set; }
        public EmissiveStrengthExtension EmissiveStrength { get; set; }
        public DispersionExtension Dispersion { get; set; }
        public AnisotropyExtension Anisotropy { get; set; }
        public DiffuseTransmissionExtension DiffuseTransmission { get; set; }
        public VolumeScatterExtension VolumeScatter { get; set; }
        public UnlitExtension Unlit { get; set; }
        public SpecularGlossinessExtension SpecularGlossiness { get; set; }

        // Dynamic extensions storage
        readonly Dictionary<string, MaterialExtension> _extensions = new();

        // 缓存的 ShaderDefines
        ShaderDefines _cachedDefines;

        /// <summary>
        /// 生成片段着色器 defines（使用缓存避免每帧重新创建）
        /// </summary>
        public ShaderDefines GetDefines() {
            if (_cachedDefines != null) {
                return _cachedDefines;
            }

            ShaderDefines defines = ShaderDefines.CreateFragmentDefines();

            // Alpha mode
            defines.SetAlphaMode(AlphaMode);

            // Core textures + UV transforms
            if (BaseColorTexture != null) {
                defines.AddTextureMap("BASE_COLOR");
                if (BaseColorTexture.HasUVTransform) {
                    defines.AddUVTransform("BASECOLOR");
                }
            }
            if (NormalTexture != null) {
                defines.AddTextureMap("NORMAL");
                if (NormalTexture.HasUVTransform) {
                    defines.AddUVTransform("NORMAL");
                }
            }
            if (MetallicRoughnessTexture != null) {
                defines.AddTextureMap("METALLIC_ROUGHNESS");
                if (MetallicRoughnessTexture.HasUVTransform) {
                    defines.AddUVTransform("METALLICROUGHNESS");
                }
            }
            if (OcclusionTexture != null) {
                defines.AddTextureMap("OCCLUSION");
                if (OcclusionTexture.HasUVTransform) {
                    defines.AddUVTransform("OCCLUSION");
                }
            }
            if (EmissiveTexture != null) {
                defines.AddTextureMap("EMISSIVE");
                if (EmissiveTexture.HasUVTransform) {
                    defines.AddUVTransform("EMISSIVE");
                }
            }

            // Extensions
            AppendExtensionDefines(defines);

            _cachedDefines = defines;
            return defines;
        }

        /// <summary>
        /// 附加扩展 defines（统一调用各扩展的 AppendDefines 方法）
        /// </summary>
        void AppendExtensionDefines(ShaderDefines defines) {
            if (ClearCoat?.IsEnabled == true) {
                ClearCoat.AppendDefines(defines);
            }
            if (Iridescence?.IsEnabled == true) {
                Iridescence.AppendDefines(defines);
            }
            if (Transmission?.IsEnabled == true) {
                Transmission.AppendDefines(defines);
            }
            if (Volume?.IsEnabled == true) {
                Volume.AppendDefines(defines);
            }
            if (Sheen?.IsEnabled == true) {
                Sheen.AppendDefines(defines);
            }
            if (Specular?.IsEnabled == true) {
                Specular.AppendDefines(defines);
            }
            if (Ior?.IsEnabled == true) {
                Ior.AppendDefines(defines);
            }
            if (EmissiveStrength?.IsEnabled == true) {
                EmissiveStrength.AppendDefines(defines);
            }
            if (Dispersion?.IsEnabled == true) {
                Dispersion.AppendDefines(defines);
            }
            if (Anisotropy?.IsEnabled == true) {
                Anisotropy.AppendDefines(defines);
            }
            if (DiffuseTransmission?.IsEnabled == true) {
                DiffuseTransmission.AppendDefines(defines);
            }
            if (VolumeScatter?.IsEnabled == true) {
                VolumeScatter.AppendDefines(defines);
            }
            if (Unlit?.IsEnabled == true) {
                Unlit.AppendDefines(defines);
            }
            if (SpecularGlossiness?.IsEnabled == true) {
                SpecularGlossiness.AppendDefines(defines);
            }

            // 其他动态注册的扩展
            foreach (MaterialExtension ext in _extensions.Values) {
                if (ext?.IsEnabled == true) {
                    ext.AppendDefines(defines);
                }
            }
        }
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
