#nullable disable

using Engine.Animation;

namespace Game.Animation
{
    /// <summary>
    /// 动画模板注册
    /// 在游戏启动时从配置文件加载所有内置模板
    /// </summary>
    public static class AnimationTemplateRegistration
    {
        private static bool s_registered = false;
        private static readonly object s_lock = new();

        /// <summary>
        /// 是否已注册
        /// </summary>
        public static bool IsRegistered => s_registered;

        /// <summary>
        /// 注册所有内置模板
        /// </summary>
        public static void Register()
        {
            lock (s_lock)
            {
                if (s_registered)
                    return;

                // 从 ContentManager 加载配置文件
                LoadTemplatesFromContent();

                // 如果没有加载到任何模板，使用硬编码的后备方案
                if (AnimationTemplateManager.RegisteredCount == 0)
                {
                    RegisterFallbackTemplates();
                }

                s_registered = true;
            }
        }

        /// <summary>
        /// 从 ContentManager 加载模板配置文件
        /// </summary>
        private static void LoadTemplatesFromContent()
        {
            // 获取 AnimationTemplates 目录下的所有文件
            var contents = ContentManager.List("AnimationTemplates");

            foreach (var contentInfo in contents)
            {
                // 只处理 .template.json 文件
                if (contentInfo.Filename == null ||
                    !contentInfo.Filename.EndsWith(".template.json"))
                {
                    continue;
                }

                try
                {
                    // 使用 ContentInfo 的流加载模板
                    using var stream = contentInfo.Duplicate();
                    AnimationTemplateManager.LoadFromStream(stream);
                }
                catch
                {
                    // 忽略单个文件加载失败
                }
            }
        }

        /// <summary>
        /// 硬编码后备方案（当配置文件不存在时使用）
        /// </summary>
        private static void RegisterFallbackTemplates()
        {
            // 保留最基础的 Simple 模板作为后备
            AnimationTemplateManager.Register("Simple", new AnimationTemplate(
                "Simple",
                new Dictionary<string, LayerDefinition>
                {
                    ["Base"] = new(0, AnimationBlendMode.Override)
                },
                new Dictionary<string, StateTrackDefinition>
                {
                    ["Gait"] = new(StateTrackType.Enum, "Idle") { EnumValues = new[] { "Idle" } }
                }
            ));
        }

        /// <summary>
        /// 重置注册状态（仅用于测试）
        /// </summary>
        public static void Reset()
        {
            lock (s_lock)
            {
                s_registered = false;
                AnimationTemplateManager.Clear();
            }
        }
    }
}
