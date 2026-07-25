using Engine;
using Engine.Graphics;

namespace Game {
    public sealed class ModelWidgetRenderContext {
        private readonly ModelWidget m_widget;

        public IReadOnlyList<Model> Models { get; }

        public Matrix ViewMatrix { get; }

        public Matrix ProjectionMatrix { get; }

        public Matrix ModelTransform { get; }

        public Color Color { get; }

        internal ModelWidgetRenderContext(
            ModelWidget widget,
            Matrix viewMatrix,
            Matrix projectionMatrix,
            Matrix modelTransform,
            Color color
        ) {
            m_widget = widget;
            Models = Array.AsReadOnly(widget.Models.ToArray());
            ViewMatrix = viewMatrix;
            ProjectionMatrix = projectionMatrix;
            ModelTransform = modelTransform;
            Color = color;
        }

        public Matrix GetMeshTransform(Model model, ModelMesh mesh) {
            return m_widget.GetMeshTransform(model, mesh);
        }

        public Texture2D GetTextureOverride(Model model) {
            return m_widget.GetTextureOverride(model);
        }

        public Texture2D GetTexture(Model model, ModelMeshPart meshPart) {
            return m_widget.GetTexture(model, meshPart);
        }

        public int CalculateJointMatrices(Model model, Matrix[] destination) {
            return m_widget.CalculateJointMatrices(model, ModelTransform, destination);
        }
    }
}
