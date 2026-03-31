#nullable disable

namespace Engine.Graphics
{
    /// <summary>
    /// 动画层混合模式
    /// </summary>
    public enum BlendMode
    {
        /// <summary>
        /// 完全替换下层结果
        /// </summary>
        Override,

        /// <summary>
        /// 叠加到下层结果上
        /// </summary>
        Additive
    }
}
