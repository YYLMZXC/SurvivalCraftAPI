using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Engine.Media;

namespace Game {
    /// <summary>
    /// 实例渲染数据
    /// </summary>
    public struct InstanceRenderData {
        public ModelMesh Mesh;
        public ModelMaterial Material;
        public Matrix WorldMatrix;
        public Model Model;
        public Texture2D TextureOverride;
        public float LightIntensity;
        public float CelestialBodyVisible;
    }

    /// <summary>
    /// 自定义模型渲染器接口
    /// 模组实现此接口以提供自定义渲染（如 PBR）
    /// </summary>
    public interface ICustomModelRenderer : IDisposable {
        /// <summary>
        /// 初始化渲染器
        /// </summary>
        void Initialize(SubsystemModelsRenderer subsystemModelsRenderer);

        /// <summary>
        /// 开始新帧渲染
        /// </summary>
        void BeginFrame(Camera camera);

        /// <summary>
        /// 渲染单个 mesh（蒙皮模型等无法实例化的场景）
        /// </summary>
        void Render(ModelMesh mesh, ModelMaterial material,
            Matrix wvpMatrix, Matrix worldMatrix, Model model,
            float lightIntensity, float celestialBodyVisible, Texture2D textureOverride,
            JointTexture jointTexture = null);

        /// <summary>
        /// 批量渲染实例（非蒙皮模型）
        /// 模组应按 mesh+material 分组，使用 GPU 实例化减少 draw call
        /// </summary>
        void RenderInstances(List<InstanceRenderData> instances);

        /// <summary>
        /// 当前激活的方向光方向（世界空间）
        /// 引擎用于计算太阳遮挡 raycast
        /// </summary>
        Engine.Vector3 ActiveLightDirection { get; }
    }
}
