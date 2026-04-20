using Engine;
using Engine.Graphics;
using Engine.Media;

namespace Game {
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
        /// 渲染一个 mesh
        /// </summary>
        void Render(ModelMesh mesh, ModelMaterial material,
            Matrix wvpMatrix, Matrix worldMatrix, Model model,
            float lightIntensity, Texture2D textureOverride,
            JointTexture jointTexture = null);
    }
}
