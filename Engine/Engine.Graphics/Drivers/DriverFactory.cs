#nullable disable

namespace Engine.Graphics.Drivers
{
    /// <summary>
    /// 驱动器工厂 - 创建和管理驱动器实例
    /// </summary>
    public static class DriverFactory
    {
        private static readonly Dictionary<string, Type> _driverTypes = new()
        {
            ["LookAt"] = typeof(LookAtDriver),
            ["Death"] = typeof(DeathDriver),
            ["FourLeggedWalk"] = typeof(FourLeggedWalkDriver),
            ["Expression"] = typeof(ExpressionDriver)
        };

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
        }

        /// <summary>
        /// 创建驱动器实例
        /// </summary>
        public static IAnimationDriver Create(string driverName, Dictionary<string, object> args = null)
        {
            if (!_driverTypes.TryGetValue(driverName, out var type))
            {
                throw new ArgumentException($"Unknown driver type: {driverName}");
            }

            var driver = (IAnimationDriver)Activator.CreateInstance(type);

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
            return _driverTypes.ContainsKey(driverName);
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
