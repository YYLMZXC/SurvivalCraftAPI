using System;
using System.Numerics;
using Engine.Media;

namespace Engine.Graphics {
    /// <summary>
    /// PBR 渲染器基类
    /// 管理 PBR 专用材质 UBO（MaterialCore、MaterialExtension）
    /// 模组开发者继承此类实现具体 PBR 渲染（如 GltfPbrRenderer）
    /// </summary>
    public abstract class PbrMeshRenderer : AdvancedMeshRenderer {
        // PBR 专用 UBO
        protected UniformBuffer<MaterialCoreData> MaterialCoreUBO;
        protected UniformBuffer<MaterialExtensionData> MaterialExtUBO;

        public PbrMeshRenderer() {
            // 创建 PBR 专用 UBO
            MaterialCoreUBO = new(1);
            MaterialExtUBO = new(6);
        }

        /// <summary>
        /// 是否已加载 IBL 环境贴图
        /// </summary>
        public virtual bool HasIBL => false;

        public override void Render(ModelMesh mesh, ModelMaterial material, Matrix4x4 wvpMatrix, Matrix4x4 worldMatrix, Model model, JointTexture jointTexture = null) {
            if (mesh == null) return;

            // 获取或创建着色器
            Shader shader = GetOrCreateShader(mesh, material, CurrentContext);
            if (shader == null) return;

            shader.PrepareForDrawing();

            // 更新 RenderState UBO
            UpdateRenderStateUBO(wvpMatrix, worldMatrix);

            // 更新材质 UBO（PBR 专用）
            UpdateMaterialUBOs(material, false);

            // 更新 UV 变换 UBO
            UpdateUVTransformUBO(material);

            // 绑定纹理
            if (model != null && material != null) {
                BindMaterialTextures(model, material, shader);
            }

            // 绑定骨骼纹理
            if (jointTexture != null) {
                BindJointTexture(jointTexture, shader);
            }

            // 设置深度状态
            SetupDepthState(material);

            // 设置剔除模式
            SetupCullMode(material);

            // 设置混合模式
            SetupBlendMode(material, CurrentContext);

            // 绘制
            DrawMesh(mesh);
        }

        /// <summary>
        /// 更新材质 UBO（带缓存优化）
        /// </summary>
        protected void UpdateMaterialUBOs(ModelMaterial material, bool useGeneratedTangents) {
            int extensionFlags = (int)MaterialUboBuilder.BuildExtensionFlags(material);

            if (LastMaterial != material) {
                MaterialCoreData coreData = MaterialUboBuilder.BuildMaterialCoreData(material, useGeneratedTangents);
                MaterialCoreUBO.Update(ref coreData);

                MaterialExtensionData extData = MaterialUboBuilder.BuildMaterialExtensionData(material);
                MaterialExtUBO.Update(ref extData);

                LastMaterial = material;
                LastExtensionFlags = extensionFlags;
                UvTransformDirty = true;
            }
            else if (LastExtensionFlags != extensionFlags) {
                MaterialExtensionData extData = MaterialUboBuilder.BuildMaterialExtensionData(material);
                MaterialExtUBO.Update(ref extData);
                LastExtensionFlags = extensionFlags;
            }
        }

        public override void Dispose() {
            MaterialCoreUBO?.Dispose();
            MaterialExtUBO?.Dispose();
            base.Dispose();
        }
    }
}
