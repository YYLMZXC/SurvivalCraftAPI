using Engine;
using Engine.Graphics;
using Engine.Media;

namespace Game {
    /// <summary>
    /// 自定义模型渲染器接口
    /// 模组实现此接口以提供自定义渲染（如 PBR）
    /// 每个模型按 mesh part 独立分类到不同渲染队列
    /// </summary>
    public interface ICustomModelRenderer : IDisposable {
        /// <summary>
        /// 初始化渲染器。需要在模组中自行调用
        /// </summary>
        void Initialize(SubsystemModelsRenderer subsystemModelsRenderer);

        /// <summary>
        /// 开始新帧渲染（设置光源、更新 Scene/Lights UBO 等）
        /// </summary>
        void BeginFrame(Camera camera);

        /// <summary>
        /// 准备阶段调用：扫描所有模型的 mesh part，按材质分类到不同渲染队列
        /// 在所有模型 PrepareModel 完成后调用
        /// </summary>
        void PrepareCustomQueues(Camera camera, List<SubsystemModelsRenderer.ModelData> allModels);

        /// <summary>
        /// drawOrder 1 调用：scatter pass + opaque pass + transmission FBO 捕获
        /// 自定义渲染器全权管理 GL 状态和渲染
        /// </summary>
        void RenderOpaquePass(Camera camera);

        /// <summary>
        /// drawOrder 99/201 调用：排序并渲染 transparent/transmission/scatter parts
        /// </summary>
        /// <param name="camera">当前相机</param>
        /// <param name="underwater">true = drawOrder 201（水后），false = drawOrder 99（水前）</param>
        void RenderTransparentPass(Camera camera, bool underwater);

        /// <summary>
        /// 当前激活的方向光方向（世界空间）
        /// 引擎用于计算太阳遮挡 raycast
        /// </summary>
        Vector3 ActiveLightDirection { get; }
    }
}
