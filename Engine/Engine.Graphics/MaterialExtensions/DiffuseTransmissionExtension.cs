using System.Collections.Generic;
using SharpGLTF.Schema2;
using System.Numerics;
using Engine.Media;
using GltfMaterial = SharpGLTF.Schema2.Material;

namespace Engine.Graphics {
    /// <summary>
    /// KHR_materials_diffuse_transmission 扩展
    /// 支持漫反射透射（如薄纸、树叶）
    /// SharpGLTF 原生支持此扩展（MaterialDiffuseTransmission），但类型是 internal
    /// </summary>
    public class DiffuseTransmissionExtension : MaterialExtension {
        public override string ExtensionName => "KHR_materials_diffuse_transmission";

        /// <summary>
        /// 漫反射透射因子 (0-1)
        /// </summary>
        public float Factor { get; set; }

        /// <summary>
        /// 漫反射透射颜色因子
        /// </summary>
        public Vector3 ColorFactor { get; set; } = Vector3.One;

        /// <summary>
        /// 漫反射透射强度纹理
        /// </summary>
        public ModelMaterialTexture Texture { get; set; }

        /// <summary>
        /// 漫反射透射颜色纹理
        /// </summary>
        public ModelMaterialTexture ColorTexture { get; set; }

        /// <summary>
        /// Diffuse Transmission 启用条件：因子大于 0
        /// </summary>
        public override bool IsEnabled => Factor > 0f;

        public override IEnumerable<MaterialTextureSlot> GetTextureSlots() {
            if (Texture != null) {
                yield return MaterialTextureSlot.DiffuseTransmission;
            }
            if (ColorTexture != null) {
                yield return MaterialTextureSlot.DiffuseTransmissionColor;
            }
        }

        public override void LoadFromGltf(GltfMaterial material, Model model) {
            MaterialChannel? channel = material.FindChannel("DiffuseTransmissionFactor");
            if (channel != null) {
                Factor = GetChannelFactor(channel, "DiffuseTransmissionFactor", 0f);
                Texture = LoadTextureFromChannel(model, channel);
            }
            channel = material.FindChannel("DiffuseTransmissionColor");
            if (channel != null) {
                var color = channel.Value.Color; // System.Numerics.Vector4
                ColorFactor = new Vector3(color.X, color.Y, color.Z);
                ColorTexture = LoadTextureFromChannel(model, channel);
            }
        }

        public override void AppendDefines(ShaderDefines defines) {
            if (IsEnabled) {
                defines.AddMaterialExtension("DIFFUSE_TRANSMISSION");
                if (Texture != null) {
                    defines.AddTextureMap("DIFFUSE_TRANSMISSION");
                    if (Texture.HasUVTransform) {
                        defines.AddUVTransform("DIFFUSETRANSMISSION");
                    }
                }
                if (ColorTexture != null) {
                    defines.AddTextureMap("DIFFUSE_TRANSMISSION_COLOR");
                    if (ColorTexture.HasUVTransform) {
                        defines.AddUVTransform("DIFFUSETRANSMISSIONCOLOR");
                    }
                }
            }
        }
    }
}
