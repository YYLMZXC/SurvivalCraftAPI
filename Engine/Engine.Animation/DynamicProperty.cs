#nullable disable

namespace Engine.Animation
{
    /// <summary>
    /// Dynamic property that supports both static values and expressions.
    /// Used for animation properties like speed, loop, blendDuration, etc.
    /// </summary>
    /// <typeparam name="T">The type of the property value</typeparam>
    public class DynamicProperty<T>
    {
        private readonly T _staticValue;
        private readonly string _expression;
        private readonly bool _isExpression;

        /// <summary>
        /// Create a dynamic property from a configuration value.
        /// </summary>
        /// <param name="value">
        /// Static value (int, float, bool, string) or expression string.
        /// Expression format: contains [Parameter] reference or starts with "expr:"
        /// </param>
        public DynamicProperty(object value)
        {
            if (value == null)
            {
                _staticValue = default;
                _isExpression = false;
                return;
            }

            if (value is string str && ExpressionEvaluator.IsExpression(str))
            {
                _expression = ExpressionEvaluator.StripPrefix(str);
                _isExpression = true;
            }
            else
            {
                _staticValue = ConvertValue(value);
                _isExpression = false;
            }
        }

        /// <summary>
        /// Private constructor for creating static/expression properties directly.
        /// </summary>
        private DynamicProperty(T staticValue, string expression, bool isExpression)
        {
            _staticValue = staticValue;
            _expression = expression;
            _isExpression = isExpression;
        }

        /// <summary>
        /// Get the current value (static or dynamically computed).
        /// </summary>
        /// <param name="parameters">Animation parameters for expression evaluation</param>
        /// <param name="evaluator">Expression evaluator instance</param>
        /// <returns>The property value</returns>
        public T GetValue(AnimationParameters parameters, ExpressionEvaluator evaluator)
        {
            if (!_isExpression)
                return _staticValue;

            if (evaluator == null)
                return _staticValue;

            return evaluator.Evaluate<T>(_expression, parameters, _staticValue);
        }

        /// <summary>
        /// Check if this property is an expression (dynamic).
        /// </summary>
        public bool IsExpression => _isExpression;

        /// <summary>
        /// Get the raw expression string (if this is an expression).
        /// </summary>
        public string Expression => _isExpression ? _expression : null;

        /// <summary>
        /// Get the static value (if this is not an expression).
        /// </summary>
        public T StaticValue => _staticValue;

        /// <summary>
        /// Create a static property.
        /// </summary>
        /// <param name="value">The static value</param>
        /// <returns>A new DynamicProperty with the static value</returns>
        public static DynamicProperty<T> Static(T value)
        {
            return new DynamicProperty<T>(value, null, false);
        }

        /// <summary>
        /// Create an expression property.
        /// </summary>
        /// <param name="expression">The expression string</param>
        /// <returns>A new DynamicProperty with the expression</returns>
        public static DynamicProperty<T> FromExpression(string expression)
        {
            return new DynamicProperty<T>(default, expression, true);
        }

        /// <summary>
        /// Implicit conversion from T to DynamicProperty&lt;T&gt;.
        /// </summary>
        public static implicit operator DynamicProperty<T>(T value)
        {
            return Static(value);
        }

        /// <summary>
        /// Convert an object value to the target type T.
        /// </summary>
        private static T ConvertValue(object value)
        {
            var targetType = typeof(T);

            if (value is T typed)
                return typed;

            // Handle numeric conversions
            if (targetType == typeof(float))
            {
                if (value is int i)
                    return (T)(object)(float)i;
                if (value is double d)
                    return (T)(object)(float)d;
                if (value is decimal dec)
                    return (T)(object)(float)dec;
            }

            if (targetType == typeof(int))
            {
                if (value is float f)
                    return (T)(object)(int)f;
                if (value is double d)
                    return (T)(object)(int)d;
            }

            if (targetType == typeof(double))
            {
                if (value is int i)
                    return (T)(object)(double)i;
                if (value is float f)
                    return (T)(object)(double)f;
            }

            // Handle bool conversion
            if (targetType == typeof(bool))
            {
                return (T)(object)Convert.ToBoolean(value);
            }

            // Handle JsonElement (from System.Text.Json)
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                return ConvertJsonElement(jsonElement, targetType);
            }

            return (T)Convert.ChangeType(value, targetType);
        }

        /// <summary>
        /// Convert a JsonElement to the target type.
        /// </summary>
        private static T ConvertJsonElement(System.Text.Json.JsonElement element, Type targetType)
        {
            if (targetType == typeof(float))
                return (T)(object)element.GetSingle();

            if (targetType == typeof(int))
                return (T)(object)element.GetInt32();

            if (targetType == typeof(double))
                return (T)(object)element.GetDouble();

            if (targetType == typeof(bool))
                return (T)(object)element.GetBoolean();

            if (targetType == typeof(string))
                return (T)(object)element.GetString();

            return default;
        }

        /// <summary>
        /// String representation for debugging.
        /// </summary>
        public override string ToString()
        {
            if (_isExpression)
                return $"expr:{_expression}";
            return _staticValue?.ToString() ?? "null";
        }
    }
}
