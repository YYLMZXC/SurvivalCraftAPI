#nullable disable

namespace Engine.Graphics
{
    /// <summary>
    /// 状态条件规则
    /// </summary>
    public class StateRule
    {
        /// <summary>
        /// 规则名称（用于调试）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 条件表达式（NCalc 语法）
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// 匹配时设置的状态值
        /// </summary>
        public string TargetState { get; set; }

        public StateRule() { }

        public StateRule(string condition, string targetState)
        {
            Condition = condition;
            TargetState = targetState;
        }
    }
}
