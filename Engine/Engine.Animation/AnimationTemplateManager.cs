#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Engine.Animation
{
    /// <summary>
    /// 动画模板管理器
    /// 提供模板的注册、获取和管理功能
    /// </summary>
    public static class AnimationTemplateManager
    {
        private static readonly Dictionary<string, AnimationTemplate> s_templates = new();

        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

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

        #region JSON 加载方法

        /// <summary>
        /// 从 JSON 字符串加载并注册动画模板
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        /// <returns>加载的模板实例，如果失败则返回 null</returns>
        public static AnimationTemplate LoadFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                var config = JsonSerializer.Deserialize<AnimationTemplateConfig>(json, s_jsonOptions);
                if (config == null || string.IsNullOrEmpty(config.Name))
                    return null;

                var template = CreateTemplateFromConfig(config);
                if (template != null)
                {
                    Register(config.Name, template);
                }
                return template;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// 从文件加载并注册动画模板
        /// </summary>
        /// <param name="path">JSON 文件路径</param>
        /// <returns>加载的模板实例，如果失败则返回 null</returns>
        public static AnimationTemplate LoadFromFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            try
            {
                var json = File.ReadAllText(path);
                return LoadFromJson(json);
            }
            catch (IOException)
            {
                return null;
            }
        }

        /// <summary>
        /// 从目录加载所有动画模板文件
        /// </summary>
        /// <param name="directory">目录路径</param>
        /// <param name="searchPattern">文件搜索模式，默认为 "*.template.json"</param>
        /// <returns>成功加载的模板数量</returns>
        public static int LoadFromDirectory(string directory, string searchPattern = "*.template.json")
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return 0;

            var count = 0;
            try
            {
                var files = Directory.GetFiles(directory, searchPattern);
                foreach (var file in files)
                {
                    if (LoadFromFile(file) != null)
                    {
                        count++;
                    }
                }
            }
            catch (IOException)
            {
                // 忽略目录访问错误
            }

            return count;
        }

        /// <summary>
        /// 从流加载并注册动画模板
        /// </summary>
        /// <param name="stream">JSON 数据流</param>
        /// <returns>加载的模板实例，如果失败则返回 null</returns>
        public static AnimationTemplate LoadFromStream(Stream stream)
        {
            if (stream == null)
                return null;

            try
            {
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                return LoadFromJson(json);
            }
            catch (IOException)
            {
                return null;
            }
        }

        #endregion

        #region 私有方法

        private static AnimationTemplate CreateTemplateFromConfig(AnimationTemplateConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.Name))
                return null;

            // 转换层定义
            var layers = new List<LayerDefinition>();
            if (config.Layers != null)
            {
                foreach (var layerConfig in config.Layers)
                {
                    var layer = CreateLayerFromConfig(layerConfig);
                    if (layer != null)
                        layers.Add(layer);
                }
            }

            // 转换状态轨道定义
            var stateTracks = new List<StateTrackDefinition>();
            if (config.StateTracks != null)
            {
                foreach (var trackConfig in config.StateTracks)
                {
                    var track = CreateStateTrackFromConfig(trackConfig);
                    if (track != null)
                        stateTracks.Add(track);
                }
            }

            // 转换必需骨骼列表
            string[] requiredBones = null;
            if (config.RequiredBones != null && config.RequiredBones.Count > 0)
            {
                requiredBones = config.RequiredBones.ToArray();
            }

            return new AnimationTemplate(
                config.Name,
                layers.ToArray(),
                stateTracks.ToArray(),
                requiredBones
            );
        }

        private static LayerDefinition CreateLayerFromConfig(TemplateLayerConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.Name))
                return null;

            var blendMode = ParseBlendMode(config.BlendMode);
            string[] boneMask = null;

            if (config.BoneMask != null && config.BoneMask.Count > 0)
            {
                boneMask = config.BoneMask.ToArray();
            }

            return new LayerDefinition(config.Name, config.Index, blendMode, boneMask);
        }

        private static StateTrackDefinition CreateStateTrackFromConfig(TemplateStateTrackConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.Name))
                return null;

            var type = ParseStateTrackType(config.Type);
            var definition = new StateTrackDefinition(config.Name, type, config.DefaultValue);

            // 设置 Enum 类型特有属性
            if (type == StateTrackType.Enum && config.EnumValues != null)
            {
                definition.EnumValues = config.EnumValues.ToArray();
            }

            // 设置 Float 类型特有属性
            if (type == StateTrackType.Float)
            {
                definition.MinValue = config.MinValue;
                definition.MaxValue = config.MaxValue;
            }

            return definition;
        }

        private static AnimationBlendMode ParseBlendMode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return AnimationBlendMode.Override;

            return value.ToLowerInvariant() switch
            {
                "override" => AnimationBlendMode.Override,
                "additive" => AnimationBlendMode.Additive,
                _ => AnimationBlendMode.Override
            };
        }

        private static StateTrackType ParseStateTrackType(string value)
        {
            if (string.IsNullOrEmpty(value))
                return StateTrackType.Float;

            return value.ToLowerInvariant() switch
            {
                "enum" => StateTrackType.Enum,
                "bool" => StateTrackType.Bool,
                "float" => StateTrackType.Float,
                _ => StateTrackType.Float
            };
        }

        #endregion
    }
}
