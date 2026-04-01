#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Engine.Graphics
{
    /// <summary>
    /// 动画配置加载器
    /// 负责从 JSON 文件加载 AnimationConfig 并解析动画引用
    /// </summary>
    public class AnimationConfigLoader
    {
        /// <summary>
        /// 动画引用协议前缀
        /// </summary>
        public const string AnimationProtocolPrefix = "animation://";

        /// <summary>
        /// 加载动画的回调函数
        /// 参数：动画名称或路径，返回：ModelAnimation 实例
        /// </summary>
        public Func<string, ModelAnimation> LoadAnimationCallback { get; set; }

        /// <summary>
        /// 从 JSON 文本加载配置
        /// </summary>
        /// <param name="json">JSON 文本</param>
        /// <returns>加载的 AnimationConfig 实例</returns>
        /// <exception cref="ArgumentNullException">json 为 null</exception>
        /// <exception cref="JsonException">JSON 解析失败</exception>
        public AnimationConfig LoadFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new ArgumentNullException(nameof(json));
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            AnimationConfig config = JsonSerializer.Deserialize<AnimationConfig>(json, options);

            if (config == null)
            {
                throw new JsonException("Failed to deserialize AnimationConfig: result is null");
            }

            // 验证配置
            ValidateConfig(config);

            return config;
        }

        /// <summary>
        /// 从文件路径加载配置
        /// </summary>
        /// <param name="path">JSON 配置文件路径</param>
        /// <returns>加载的 AnimationConfig 实例</returns>
        /// <exception cref="ArgumentNullException">path 为 null</exception>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        /// <exception cref="JsonException">JSON 解析失败</exception>
        public AnimationConfig LoadFromFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Animation config file not found: {path}", path);
            }

            string json = File.ReadAllText(path);
            return LoadFromJson(json);
        }

        /// <summary>
        /// 从流加载配置
        /// </summary>
        /// <param name="stream">包含 JSON 配置的流</param>
        /// <returns>加载的 AnimationConfig 实例</returns>
        /// <exception cref="ArgumentNullException">stream 为 null</exception>
        /// <exception cref="JsonException">JSON 解析失败</exception>
        public AnimationConfig LoadFromStream(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            return LoadFromJson(json);
        }

        /// <summary>
        /// 解析动画引用，获取实际的 ModelAnimation
        /// </summary>
        /// <param name="reference">动画引用配置</param>
        /// <param name="model">目标模型（用于动画查找）</param>
        /// <returns>解析后的 ModelAnimation，如果无法解析则返回 null</returns>
        public ModelAnimation ResolveAnimation(AnimationReference reference, Model model)
        {
            if (reference == null || string.IsNullOrEmpty(reference.Source))
            {
                return null;
            }

            string source = reference.Source;

            // 处理 animation:// 协议
            if (source.StartsWith(AnimationProtocolPrefix))
            {
                string animationName = source.Substring(AnimationProtocolPrefix.Length);
                return FindAnimationInModel(model, animationName);
            }

            // 使用回调加载外部动画
            if (LoadAnimationCallback != null)
            {
                return LoadAnimationCallback(source);
            }

            return null;
        }

        /// <summary>
        /// 解析所有动画引用
        /// </summary>
        /// <param name="config">动画配置</param>
        /// <param name="model">目标模型</param>
        /// <returns>解析后的动画字典（别名 -> ModelAnimation）</returns>
        public Dictionary<string, ModelAnimation> ResolveAllAnimations(AnimationConfig config, Model model)
        {
            Dictionary<string, ModelAnimation> result = new();

            if (config?.Animations == null)
            {
                return result;
            }

            foreach (var kvp in config.Animations)
            {
                string alias = kvp.Key;
                AnimationReference reference = kvp.Value;

                ModelAnimation animation = ResolveAnimation(reference, model);
                if (animation != null)
                {
                    result[alias] = animation;
                }
            }

            return result;
        }

        /// <summary>
        /// 验证配置的有效性
        /// </summary>
        /// <param name="config">要验证的配置</param>
        /// <exception cref="AnimationConfigValidationException">验证失败</exception>
        public void ValidateConfig(AnimationConfig config)
        {
            if (config == null)
            {
                throw new AnimationConfigValidationException("Config is null");
            }

            List<string> errors = new();

            // 验证模板名称
            if (string.IsNullOrEmpty(config.Template))
            {
                errors.Add("Template name is required");
            }
            else if (!AnimationTemplateRegistry.Exists(config.Template))
            {
                errors.Add($"Unknown template: {config.Template}");
            }

            // 验证动画引用
            if (config.Animations != null)
            {
                int index = 0;
                foreach (var kvp in config.Animations)
                {
                    string alias = kvp.Key;
                    AnimationReference reference = kvp.Value;

                    if (string.IsNullOrEmpty(alias))
                    {
                        errors.Add($"Animation alias at index {index} is empty");
                    }

                    if (reference != null)
                    {
                        if (string.IsNullOrEmpty(reference.Source))
                        {
                            errors.Add($"Animation '{alias}': Source is required");
                        }

                        if (reference.Speed <= 0)
                        {
                            errors.Add($"Animation '{alias}': Speed must be positive (got {reference.Speed})");
                        }

                        if (reference.InitialPhase < 0 || reference.InitialPhase > 1)
                        {
                            errors.Add($"Animation '{alias}': InitialPhase must be between 0 and 1 (got {reference.InitialPhase})");
                        }

                        if (reference.BlendDuration < 0)
                        {
                            errors.Add($"Animation '{alias}': BlendDuration cannot be negative (got {reference.BlendDuration})");
                        }
                    }

                    index++;
                }
            }

            // 验证状态规则
            if (config.StateRules != null)
            {
                int index = 0;
                foreach (var trackRules in config.StateRules)
                {
                    if (string.IsNullOrEmpty(trackRules.TrackName))
                    {
                        errors.Add($"StateRules at index {index}: TrackName is required");
                    }

                    if (trackRules.Rules != null)
                    {
                        int ruleIndex = 0;
                        foreach (var rule in trackRules.Rules)
                        {
                            if (string.IsNullOrEmpty(rule.Condition))
                            {
                                errors.Add($"StateRules '{trackRules.TrackName}' rule {ruleIndex}: Condition is required");
                            }

                            if (string.IsNullOrEmpty(rule.TargetState))
                            {
                                errors.Add($"StateRules '{trackRules.TrackName}' rule {ruleIndex}: TargetState is required");
                            }

                            ruleIndex++;
                        }
                    }

                    index++;
                }
            }

            // 验证驱动器配置
            if (config.Drivers != null)
            {
                int index = 0;
                foreach (var driver in config.Drivers)
                {
                    if (string.IsNullOrEmpty(driver.Type))
                    {
                        errors.Add($"Driver at index {index}: Type is required");
                    }

                    index++;
                }
            }

            if (errors.Count > 0)
            {
                throw new AnimationConfigValidationException(
                    $"Animation config validation failed:\n{string.Join("\n", errors)}",
                    errors);
            }
        }

        /// <summary>
        /// 在模型中查找指定名称的动画
        /// </summary>
        private ModelAnimation FindAnimationInModel(Model model, string animationName)
        {
            if (model == null || string.IsNullOrEmpty(animationName))
            {
                return null;
            }

            // 尝试精确匹配
            foreach (var anim in model.Animations)
            {
                if (anim.Name == animationName)
                {
                    return anim;
                }
            }

            // 尝试忽略大小写匹配
            foreach (var anim in model.Animations)
            {
                if (string.Equals(anim.Name, animationName, StringComparison.OrdinalIgnoreCase))
                {
                    return anim;
                }
            }

            return null;
        }

        /// <summary>
        /// 将配置转换为 AnimationController
        /// </summary>
        /// <param name="config">动画配置</param>
        /// <param name="model">目标模型</param>
        /// <returns>配置好的 AnimationController 实例</returns>
        public AnimationController CreateController(AnimationConfig config, Model model)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            // 获取模板名称
            string templateName = config.Template ?? "Simple";

            // 创建控制器
            AnimationController controller = new(model, templateName);

            // 设置初始参数
            if (config.Parameters != null)
            {
                foreach (var kvp in config.Parameters)
                {
                    SetParameterByType(controller.Parameters, kvp.Key, kvp.Value);
                }
            }

            // 配置驱动器
            if (config.Drivers != null)
            {
                foreach (var driverConfig in config.Drivers)
                {
                    ApplyDriverConfig(controller, driverConfig);
                }
            }

            // 配置状态规则
            if (config.States != null && config.States.Count > 0)
            {
                controller.SetStateConfigs(config.States);
            }

            return controller;
        }

        /// <summary>
        /// 应用驱动器配置
        /// </summary>
        private void ApplyDriverConfig(AnimationController controller, DriverConfig driverConfig)
        {
            if (driverConfig == null || string.IsNullOrEmpty(driverConfig.Type))
                return;

            // 创建驱动器实例
            IAnimationDriver driver = CreateDriver(driverConfig.Type);
            if (driver == null)
                return;

            // 应用属性
            if (driverConfig.Properties != null)
            {
                ApplyDriverProperties(driver, driverConfig.Properties);
            }

            // 绑定到对应层
            string layerName = driverConfig.Layer ?? "Base";
            controller.SetDriver(layerName, driver);
        }

        /// <summary>
        /// 创建驱动器实例
        /// 引擎层驱动器在此创建，游戏层驱动器通过反射创建
        /// </summary>
        public IAnimationDriver CreateDriver(string type)
        {
            // 引擎层驱动器
            return type switch
            {
                "LookAtDriver" => new Drivers.LookAtDriver(),
                "LookAt" => new Drivers.LookAtDriver(),  // 兼容简写
                "DeathDriver" => new Drivers.DeathDriver(),
                "Death" => new Drivers.DeathDriver(),  // 兼容简写
                "ExpressionDriver" => new Drivers.ExpressionDriver(),
                // 游戏层驱动器（通过反射创建）
                _ => CreateGameDriver(type)
            };
        }

        /// <summary>
        /// 通过反射创建游戏层驱动器
        /// </summary>
        private IAnimationDriver CreateGameDriver(string typeName)
        {
            try
            {
                // 尝试的游戏层驱动器命名空间
                string[] namespaces = new[]
                {
                    "Game.Animation.Drivers",
                    "Game"
                };

                // 尝试的类型名（原始名和加 Driver 后缀）
                string[] typeNames = new[]
                {
                    typeName,
                    typeName + "Driver"
                };

                foreach (var ns in namespaces)
                {
                    foreach (var tname in typeNames)
                    {
                        // 尝试完整类型名
                        string fullName = $"{ns}.{tname}";
                        var type = Type.GetType(fullName);
                        if (type != null)
                        {
                            return Activator.CreateInstance(type) as IAnimationDriver;
                        }

                        // 遍历所有已加载的程序集查找
                        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            type = assembly.GetType(fullName);
                            if (type != null)
                            {
                                return Activator.CreateInstance(type) as IAnimationDriver;
                            }
                        }
                    }
                }

                // 尝试直接使用传入的类型名（可能是完整类型名）
                var directType = Type.GetType(typeName);
                if (directType != null)
                {
                    return Activator.CreateInstance(directType) as IAnimationDriver;
                }

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var foundType = assembly.GetType(typeName);
                    if (foundType != null)
                    {
                        return Activator.CreateInstance(foundType) as IAnimationDriver;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 应用驱动器属性
        /// </summary>
        public void ApplyDriverProperties(IAnimationDriver driver, Dictionary<string, object> properties)
        {
            if (properties == null || driver == null)
                return;

            var driverType = driver.GetType();

            foreach (var kvp in properties)
            {
                var property = driverType.GetProperty(kvp.Key);
                if (property == null || !property.CanWrite)
                    continue;

                try
                {
                    object value = ConvertValue(kvp.Value, property.PropertyType);

                    // 处理嵌套对象
                    if (value is Dictionary<string, object> nestedDict)
                    {
                        value = CreateNestedObject(property.PropertyType, nestedDict);
                    }

                    if (value != null)
                    {
                        property.SetValue(driver, value);
                    }
                }
                catch
                {
                    // 忽略转换失败
                }
            }
        }

        /// <summary>
        /// 创建嵌套对象并设置属性
        /// </summary>
        private object CreateNestedObject(Type targetType, Dictionary<string, object> properties)
        {
            try
            {
                var obj = Activator.CreateInstance(targetType);
                if (obj == null) return null;

                foreach (var kvp in properties)
                {
                    var property = targetType.GetProperty(kvp.Key);
                    if (property == null || !property.CanWrite)
                        continue;

                    object value = ConvertValue(kvp.Value, property.PropertyType);
                    if (value is Dictionary<string, object> nestedDict)
                    {
                        value = CreateNestedObject(property.PropertyType, nestedDict);
                    }

                    if (value != null)
                    {
                        property.SetValue(obj, value);
                    }
                }

                return obj;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 转换值到目标类型
        /// </summary>
        private object ConvertValue(object value, Type targetType)
        {
            if (value == null)
                return null;

            // 处理 JsonElement（来自 System.Text.Json）
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                return ConvertJsonElement(jsonElement, targetType);
            }

            if (targetType == typeof(float))
            {
                return Convert.ToSingle(value);
            }
            if (targetType == typeof(double))
            {
                return Convert.ToDouble(value);
            }
            if (targetType == typeof(int))
            {
                return Convert.ToInt32(value);
            }
            if (targetType == typeof(bool))
            {
                return Convert.ToBoolean(value);
            }
            if (targetType == typeof(string))
            {
                return value.ToString();
            }

            return Convert.ChangeType(value, targetType);
        }

        /// <summary>
        /// 从 JsonElement 转换值
        /// </summary>
        private object ConvertJsonElement(System.Text.Json.JsonElement element, Type targetType)
        {
            // 处理嵌套对象
            if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                var dict = new Dictionary<string, object>();
                foreach (var prop in element.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value;
                }
                return dict;
            }

            // 处理数组
            if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                if (targetType == typeof(float[]))
                {
                    var arr = new float[element.GetArrayLength()];
                    int i = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        arr[i++] = item.GetSingle();
                    }
                    return arr;
                }
                // 其他数组类型可在此扩展
            }

            if (targetType == typeof(float))
            {
                return element.GetSingle();
            }
            if (targetType == typeof(double))
            {
                return element.GetDouble();
            }
            if (targetType == typeof(int))
            {
                return element.GetInt32();
            }
            if (targetType == typeof(bool))
            {
                return element.GetBoolean();
            }
            if (targetType == typeof(string))
            {
                return element.GetString();
            }

            // 默认返回字符串
            return element.ToString();
        }

        /// <summary>
        /// 根据值类型设置参数
        /// </summary>
        private void SetParameterByType(AnimationParameters parameters, string name, object value)
        {
            if (value == null) return;

            // 处理 JsonElement
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                switch (jsonElement.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.Number:
                        parameters.SetFloat(name, jsonElement.GetSingle());
                        break;
                    case System.Text.Json.JsonValueKind.True:
                    case System.Text.Json.JsonValueKind.False:
                        parameters.SetBool(name, jsonElement.GetBoolean());
                        break;
                    case System.Text.Json.JsonValueKind.String:
                        // 字符串参数暂不处理
                        break;
                }
                return;
            }

            switch (value)
            {
                case float f:
                    parameters.SetFloat(name, f);
                    break;
                case double d:
                    parameters.SetFloat(name, (float)d);
                    break;
                case int i:
                    parameters.SetFloat(name, i);
                    break;
                case bool b:
                    parameters.SetBool(name, b);
                    break;
                case Vector3 v:
                    parameters.SetVector3(name, v);
                    break;
                default:
                    // 尝试转换为 float
                    if (double.TryParse(value.ToString(), out double num))
                    {
                        parameters.SetFloat(name, (float)num);
                    }
                    break;
            }
        }

        /// <summary>
        /// 获取配置中定义的动画别名列表
        /// </summary>
        /// <param name="config">动画配置</param>
        /// <returns>动画别名列表</returns>
        public IReadOnlyList<string> GetAnimationAliases(AnimationConfig config)
        {
            List<string> aliases = new();

            if (config?.Animations != null)
            {
                foreach (var kvp in config.Animations)
                {
                    aliases.Add(kvp.Key);
                }
            }

            return aliases;
        }

        /// <summary>
        /// 获取动画引用配置
        /// </summary>
        /// <param name="config">动画配置</param>
        /// <param name="alias">动画别名</param>
        /// <returns>动画引用配置，如果不存在则返回 null</returns>
        public AnimationReference GetAnimationReference(AnimationConfig config, string alias)
        {
            if (config?.Animations == null || string.IsNullOrEmpty(alias))
            {
                return null;
            }

            return config.Animations.TryGetValue(alias, out var reference) ? reference : null;
        }
    }

    /// <summary>
    /// 动画配置验证异常
    /// </summary>
    public class AnimationConfigValidationException : Exception
    {
        /// <summary>
        /// 验证错误列表
        /// </summary>
        public IReadOnlyList<string> Errors { get; }

        public AnimationConfigValidationException(string message) : base(message)
        {
            Errors = new List<string>();
        }

        public AnimationConfigValidationException(string message, IEnumerable<string> errors) : base(message)
        {
            Errors = new List<string>(errors);
        }

        public AnimationConfigValidationException(string message, Exception innerException) : base(message, innerException)
        {
            Errors = new List<string>();
        }
    }
}
