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
        public SubsystemModelsRenderer.ModelData ModelData;
        public Texture2D TextureOverride;
    }

    /// <summary>
    /// 自定义模型渲染器接口
    /// 模组实现此接口以提供自定义渲染（如 PBR）
    /// </summary>
    public interface ICustomModelRenderer : IDisposable {
        /// <summary>
        /// 初始化渲染器。需要在模组中自行调用
        /// </summary>
        void Initialize(SubsystemModelsRenderer subsystemModelsRenderer);

        /// <summary>
        /// 开始新帧渲染
        /// </summary>
        void BeginFrame(Camera camera);

        /// <summary>
        /// 渲染单个 mesh part（per-part 材质的蒙皮模型）
        /// </summary>
        void RenderPart(ModelMesh mesh, ModelMeshPart part, ModelMaterial material, SubsystemModelsRenderer.ModelData modelData, Texture2D textureOverride, JointTexture jointTexture = null);

        /// <summary>
        /// 批量渲染实例（非蒙皮模型）
        /// 模组应按 mesh+material 分组，使用 GPU 实例化减少 draw call
        /// </summary>
        void RenderInstances(List<InstanceRenderData> instances);

        /// <summary>
        /// 当前激活的方向光方向（世界空间）
        /// 引擎用于计算太阳遮挡 raycast
        /// </summary>
        Vector3 ActiveLightDirection { get; }
    }
}
