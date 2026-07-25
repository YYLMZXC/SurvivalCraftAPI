using Engine;
using Engine.Graphics;

namespace Game {
    public sealed class ModelWidgetRenderContext {
        public ModelWidget Widget { get; }

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
            Widget = widget;
            Models = widget.Models;
            ViewMatrix = viewMatrix;
            ProjectionMatrix = projectionMatrix;
            ModelTransform = modelTransform;
            Color = color;
        }

        public Matrix GetMeshTransform(Model model, ModelMesh mesh) {
            return Widget.GetMeshTransform(model, mesh);
        }

        public Texture2D GetTexture(Model model, ModelMeshPart meshPart) {
            return Widget.GetTexture(model, meshPart);
        }

        public int CalculateJointMatrices(Model model, Matrix[] destination) {
            return Widget.CalculateJointMatrices(model, ModelTransform, destination);
        }
    }
}
