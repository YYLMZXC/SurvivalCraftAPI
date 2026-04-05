#nullable disable

namespace Engine.Animation
{
    /// <summary>
    /// 动画层定义
    /// </summary>
    public class LayerDefinition
    {
        public string Name { get; }
        public int Index { get; }
        public AnimationBlendMode BlendMode { get; }
        public string[] BoneMask { get; }

        public LayerDefinition(string name, int index, AnimationBlendMode blendMode, string[] boneMask = null)
        {
            Name = name;
            Index = index;
            BlendMode = blendMode;
            BoneMask = boneMask;
        }
    }
}
