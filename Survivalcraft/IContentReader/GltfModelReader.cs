using Engine.Graphics;
using Engine.Media;

namespace Game.IContentReader {
    public class GltfModelReader : IContentReader {
        public override string Type => "Engine.Graphics.Model";
        public override string[] DefaultSuffix => ["gltf", "glb"];
        public override object Get(ContentInfo[] contents) => Model.Load(GltfLoader.Load(contents[0].Duplicate()), true);
    }
}
