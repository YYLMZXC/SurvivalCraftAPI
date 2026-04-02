#nullable disable

namespace Engine.Graphics
{
    /// <summary>
    /// 状态规则求值器，使用 NCalc 评估条件表达式。
    /// 支持表达式缓存和参数优化。
    /// </summary>
    public class StateRuleEvaluator
    {
        /// <summary>
        /// 编译后的表达式缓存。
        /// NCalc 内部会缓存 LogicalExpression 解析结果。
        /// </summary>
        private readonly Dictionary<string, Expression> _compiledConditions = new();

        /// <summary>
        /// 缓存每个表达式需要的参数名，避免每次求值时重新提取。
        /// </summary>
        private readonly Dictionary<string, string[]> _requiredParameters = new();

        /// <summary>
        /// 可复用的参数字典（避免每次求值分配新字典）。
        /// 注意：需要在单线程上下文中使用。
        /// </summary>
        private readonly Dictionary<string, object> _reusableParameters = new();

        /// <summary>
        /// 评估单个条件表达式。
        /// </summary>
        /// <param name="condition">条件表达式字符串</param>
        /// <param name="parameters">参数容器</param>
        /// <returns>表达式求值结果</returns>
        public bool EvaluateCondition(string condition, AnimationParameters parameters)
        {
            if (string.IsNullOrEmpty(condition))
                return false;

            try
            {
                // 获取或编译表达式
                var expression = GetOrCreateExpression(condition);

                // 绑定参数
                BindParameters(condition, expression, parameters);

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
        /// 获取或创建编译后的表达式。
        /// </summary>
        private Expression GetOrCreateExpression(string condition)
        {
            if (!_compiledConditions.TryGetValue(condition, out var expression))
            {
                expression = new Expression(condition);
                _compiledConditions[condition] = expression;

                // 提取并缓存参数名
                var paramNames = expression.GetParameterNames();
                _requiredParameters[condition] = paramNames?.ToArray() ?? Array.Empty<string>();

                // 注册自定义函数
                AnimationExpressionFunctions.RegisterFunctions(expression);
            }

            return expression;
        }

        /// <summary>
        /// 绑定参数到表达式。
        /// </summary>
        private void BindParameters(string condition, Expression expression, AnimationParameters parameters)
        {
            var requiredParams = _requiredParameters[condition];
            if (requiredParams.Length == 0)
            {
                expression.Parameters = null;
                return;
            }

            // 复用参数字典：清空后重新填充
            _reusableParameters.Clear();
            foreach (var paramName in requiredParams)
            {
                _reusableParameters[paramName] = parameters.GetValue(paramName);
            }
            expression.Parameters = _reusableParameters;
        }

        /// <summary>
        /// 清除编译缓存。
        /// </summary>
        public void ClearCache()
        {
            // 移除事件处理器以避免内存泄漏
            foreach (var kvp in _compiledConditions)
            {
                AnimationExpressionFunctions.UnregisterFunctions(kvp.Value);
            }

            _compiledConditions.Clear();
            _requiredParameters.Clear();
            _reusableParameters.Clear();
        }
    }
}
