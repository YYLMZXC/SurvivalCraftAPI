#nullable disable

using System.Collections.Generic;

namespace Engine.Animation
{
    /// <summary>
    /// 动画模板管理器
    /// 提供模板的注册、获取和管理功能
    /// </summary>
    public static class AnimationTemplateManager
    {
        private static readonly Dictionary<string, AnimationTemplate> s_templates = new();

        /// <summary>
        /// 已注册的模板数量
        /// </summary>
        public static int RegisteredCount => s_templates.Count;

        /// <summary>
        /// 注册动画模板
        /// </summary>
        /// <param name="name">模板名称</param>
        /// <param name="template">模板实例</param>
        public static void Register(string name, AnimationTemplate template)
        {
            if (string.IsNullOrEmpty(name))
                return;

            s_templates[name] = template;
        }

        /// <summary>
        /// 获取动画模板
        /// </summary>
        /// <param name="name">模板名称</param>
        /// <returns>模板实例，如果未注册则返回 null</returns>
        public static AnimationTemplate Get(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            return s_templates.TryGetValue(name, out var template) ? template : null;
        }

        /// <summary>
        /// 检查模板是否存在
        /// </summary>
        /// <param name="name">模板名称</param>
        /// <returns>是否存在</returns>
        public static bool Exists(string name)
        {
            return !string.IsNullOrEmpty(name) && s_templates.ContainsKey(name);
        }

        /// <summary>
        /// 清除所有注册（主要用于测试）
        /// </summary>
        public static void Clear()
        {
            s_templates.Clear();
        }

        /// <summary>
        /// 获取所有已注册的模板名称
        /// </summary>
        public static IEnumerable<string> GetRegisteredNames()
        {
            return s_templates.Keys;
        }
    }
}
