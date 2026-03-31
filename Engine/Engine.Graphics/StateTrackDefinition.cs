#nullable disable

namespace Engine.Graphics
{
    /// <summary>
    /// 状态轨道定义
    /// </summary>
    public class StateTrackDefinition
    {
        public string Name { get; set; }
        public StateTrackType Type { get; set; }

        // Enum 类型特有
        public string[] EnumValues { get; set; }

        // Float 类型特有
        public float MinValue { get; set; }
        public float MaxValue { get; set; }

        public object DefaultValue { get; set; }

        public StateTrackDefinition() { }

        public StateTrackDefinition(string name, StateTrackType type, object defaultValue = null)
        {
            Name = name;
            Type = type;
            DefaultValue = defaultValue;
        }
    }
}
