#nullable disable

namespace Engine.Graphics.Drivers
{
    /// <summary>
    /// 驱动器工厂 - 创建和管理驱动器实例
    /// 统一使用 AnimationDriverManager 进行驱动器注册
    /// </summary>
    public static class DriverFactory
    {
        private static readonly Dictionary<string, Type> _driverTypes = new()
        {
            ["LookAt"] = typeof(LookAtDriver),
            ["Death"] = typeof(DeathDriver),
            ["Expression"] = typeof(ExpressionDriver)
        };

        // 静态构造函数：将引擎层驱动器注册到 AnimationDriverManager
        static DriverFactory()
        {
            foreach (var kvp in _driverTypes)
            {
                AnimationDriverManager.Register(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// 注册自定义驱动器类型
        /// </summary>
        public static void RegisterDriver(string name, Type driverType)
        {
            if (!typeof(IAnimationDriver).IsAssignableFrom(driverType))
            {
                throw new ArgumentException($"Type {driverType} does not implement IAnimationDriver");
            }
            _driverTypes[name] = driverType;
            // 同时注册到 AnimationDriverManager
            AnimationDriverManager.Register(name, driverType);
        }

        /// <summary>
        /// 创建驱动器实例
        /// </summary>
        public static IAnimationDriver Create(string driverName, Dictionary<string, object> args = null)
        {
            // 优先使用 AnimationDriverManager
            var driver = AnimationDriverManager.Create(driverName);
            if (driver == null && !_driverTypes.TryGetValue(driverName, out var type))
            {
                throw new ArgumentException($"Unknown driver type: {driverName}");
            }

            // 如果 AnimationDriverManager 没找到，回退到本地创建
            if (driver == null)
            {
                driver = (IAnimationDriver)Activator.CreateInstance(_driverTypes[driverName]);
            }

            // 如果驱动器支持配置，应用参数
            if (args != null && driver is IConfigurableDriver configurable)
            {
                configurable.Configure(args);
            }

            return driver;
        }

        /// <summary>
        /// 检查驱动器类型是否存在
        /// </summary>
        public static bool HasDriver(string driverName)
        {
            return AnimationDriverManager.IsRegistered(driverName) || _driverTypes.ContainsKey(driverName);
        }

        /// <summary>
        /// 获取所有已注册的驱动器类型名称
        /// </summary>
        public static IEnumerable<string> GetRegisteredDrivers()
        {
            return _driverTypes.Keys;
        }
    }

    /// <summary>
    /// 可配置驱动器接口
    /// </summary>
    public interface IConfigurableDriver
    {
        void Configure(Dictionary<string, object> args);
    }
}
