#nullable disable

namespace Engine.Graphics
{
    /// <summary>
    /// 内置驱动器定义
    /// </summary>
    public class BuiltInDriverDefinition
    {
        public string Type { get; set; }
        public string LayerName { get; set; }

        public BuiltInDriverDefinition() { }

        public BuiltInDriverDefinition(string type, string layerName)
        {
            Type = type;
            LayerName = layerName;
        }
    }
}
