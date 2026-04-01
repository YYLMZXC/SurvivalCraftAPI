#nullable disable

using NCalc;

namespace Engine.Graphics
{
    /// <summary>
    /// 状态轨道规则配置
    /// </summary>
    public class StateTrackRules
    {
        public string TrackName { get; set; }
        public List<StateRule> Rules { get; set; } = new();
        public string DefaultState { get; set; }
    }

    /// <summary>
    /// 状态规则求值器，使用 NCalc 评估条件表达式
    /// </summary>
    public class StateRuleEvaluator
    {
        private readonly Dictionary<string, Expression> _compiledConditions = new();

        /// <summary>
        /// 评估状态轨道的所有规则，返回匹配的状态
        /// </summary>
        public string Evaluate(StateTrackRules rules, AnimationParameters parameters)
        {
            if (rules?.Rules == null || rules.Rules.Count == 0)
                return rules?.DefaultState ?? string.Empty;

            foreach (var rule in rules.Rules)
            {
                if (EvaluateCondition(rule.Condition, parameters))
                {
                    return rule.TargetState;
                }
            }
            return rules.DefaultState ?? string.Empty;
        }

        /// <summary>
        /// 评估单个条件表达式
        /// </summary>
        public bool EvaluateCondition(string condition, AnimationParameters parameters)
        {
            if (string.IsNullOrEmpty(condition))
                return false;

            try
            {
                // 获取或编译表达式
                if (!_compiledConditions.TryGetValue(condition, out var expression))
                {
                    expression = new Expression(condition);
                    expression.Options = ExpressionOptions.NoCache;
                    _compiledConditions[condition] = expression;
                }

                // 绑定参数
                expression.Parameters = new Dictionary<string, object>();
                foreach (var param in parameters.GetAllParameters())
                {
                    expression.Parameters[param.Key] = param.Value;
                }

                // 注册自定义函数
                AnimationExpressionFunctions.RegisterFunctions(expression);

                // 求值
                var result = expression.Evaluate();
                return Convert.ToBoolean(result);
            }
            catch (Exception ex)
            {
                // 临时调试日志
                System.Diagnostics.Debug.WriteLine($"[NCalc Error] Condition: {condition}, Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 清除编译缓存
        /// </summary>
        public void ClearCache()
        {
            _compiledConditions.Clear();
        }
    }
}
