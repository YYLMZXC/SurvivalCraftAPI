#nullable disable

namespace Engine.Graphics
{
    /// <summary>
    /// 动画层定义
    /// </summary>
    public class LayerDefinition
    {
        public string Name { get; }
        public int Index { get; }
        public BlendMode BlendMode { get; }
        public string[] BoneMask { get; }

        public LayerDefinition(string name, int index, BlendMode blendMode, string[] boneMask = null)
        {
            Name = name;
            Index = index;
            BlendMode = blendMode;
            BoneMask = boneMask;
        }
    }
}
