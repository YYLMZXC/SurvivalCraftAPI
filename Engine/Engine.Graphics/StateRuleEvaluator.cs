#nullable disable

namespace Engine.Graphics
{
    /// <summary>
    /// 状态规则求值器，使用 NCalc 评估条件表达式
    /// </summary>
    public class StateRuleEvaluator
    {
        private readonly Dictionary<string, NCalc.Expression> _compiledConditions = new();
        // 缓存每个表达式需要的参数名
        private readonly Dictionary<string, string[]> _requiredParameters = new();
        // 可复用的参数字典（避免每次求值分配新字典）
        // 注意：NCalc.Expression.Parameters 在求值时会被读取，因此需要在单线程上下文中使用
        // 如果 StateRuleEvaluator 实例被多线程共享，则需要每次创建新字典
        private readonly Dictionary<string, object> _reusableParameters = new();

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
                    // 使用 NCalc 默认缓存（移除 NoCache 选项以提升性能）
                    expression = new Expression(condition);
                    _compiledConditions[condition] = expression;

                    // 使用 NCalc 内置方法提取参数名
                    var paramNames = expression.GetParameterNames();
                    _requiredParameters[condition] = paramNames?.ToArray() ?? Array.Empty<string>();
                }

                // 只绑定表达式需要的参数
                var requiredParams = _requiredParameters[condition];
                if (requiredParams.Length > 0)
                {
                    // 复用参数字典：清空后重新填充
                    _reusableParameters.Clear();
                    foreach (var paramName in requiredParams)
                    {
                        _reusableParameters[paramName] = parameters.GetValue(paramName);
                    }
                    expression.Parameters = _reusableParameters;
                }
                else
                {
                    expression.Parameters = null;
                }

                // 注册自定义函数
                AnimationExpressionFunctions.RegisterFunctions(expression);

                // 求值
                var result = expression.Evaluate();
                return Convert.ToBoolean(result);
            }
            catch (Exception ex)
            {
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
            _requiredParameters.Clear();
            _reusableParameters.Clear();
        }
    }
}
