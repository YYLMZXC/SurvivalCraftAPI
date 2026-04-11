#nullable disable

namespace Engine.Animation
{
    /// <summary>
    /// 参数容器类，用于存储动画系统需要的各种参数。
    /// 支持脏检查，避免无变化时重复评估状态规则。
    /// </summary>
    public class AnimationParameters
    {
        private readonly Dictionary<string, float> _floatParams = new();
        private readonly Dictionary<string, bool> _boolParams = new();
        private readonly Dictionary<string, Vector3> _vector3Params = new();
        private readonly Dictionary<string, Vector2> _vector2Params = new();
        private readonly Dictionary<string, string> _stringParams = new();

        // 脏标记：有参数变化时设为 true
        private bool _isDirty = true;

        /// <summary>
        /// 是否有参数变化（脏标记）
        /// </summary>
        public bool IsDirty => _isDirty;

        /// <summary>
        /// 清除脏标记（在评估完状态规则后调用）
        /// </summary>
        public void ClearDirty() => _isDirty = false;

        /// <summary>
        /// 设置脏标记（用于强制重新评估状态规则）
        /// </summary>
        public void SetDirty() => _isDirty = true;

        public void SetFloat(string name, float value)
        {
            // 检查值是否变化
            if (_floatParams.TryGetValue(name, out var existing) && existing == value)
                return; // 值未变化，不设置脏标记

            _floatParams[name] = value;
            _isDirty = true;
        }

        public void SetBool(string name, bool value)
        {
            if (_boolParams.TryGetValue(name, out var existing) && existing == value)
                return;

            _boolParams[name] = value;
            _isDirty = true;
        }

        public void SetVector3(string name, Vector3 value)
        {
            if (_vector3Params.TryGetValue(name, out var existing) && existing == value)
                return;

            _vector3Params[name] = value;
            _isDirty = true;
        }

        public void SetVector2(string name, Vector2 value)
        {
            if (_vector2Params.TryGetValue(name, out var existing) && existing == value)
                return;

            _vector2Params[name] = value;
            _isDirty = true;
        }

        public void SetString(string name, string value)
        {
            if (_stringParams.TryGetValue(name, out var existing) && existing == value)
                return;

            _stringParams[name] = value;
            _isDirty = true;
        }

        /// <summary>
        /// 设置参数值（通用方法，根据类型自动分发）
        /// </summary>
        public void SetParameter(string name, object value)
        {
            if (value is float f)
                SetFloat(name, f);
            else if (value is bool b)
                SetBool(name, b);
            else if (value is Vector3 v)
                SetVector3(name, v);
            else if (value is int i)
                SetFloat(name, i);
            else if (value is double d)
                SetFloat(name, (float)d);
            else if (value is System.Text.Json.JsonElement jsonElement)
            {
                // 处理 JsonElement 类型
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                    SetFloat(name, jsonElement.GetSingle());
                else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.True ||
                         jsonElement.ValueKind == System.Text.Json.JsonValueKind.False)
                    SetBool(name, jsonElement.GetBoolean());
                else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    SetFloat(name, float.Parse(jsonElement.GetString()));
            }
            else if (value != null)
                SetFloat(name, Convert.ToSingle(value));
        }

        public float GetFloat(string name) => _floatParams.TryGetValue(name, out var v) ? v : 0f;
        public bool GetBool(string name) => _boolParams.TryGetValue(name, out var v) ? v : false;
        public Vector3 GetVector3(string name) => _vector3Params.TryGetValue(name, out var v) ? v : Vector3.Zero;
        public Vector2 GetVector2(string name) => _vector2Params.TryGetValue(name, out var v) ? v : Vector2.Zero;
        public string GetString(string name) => _stringParams.TryGetValue(name, out var v) ? v : string.Empty;

        /// <summary>
        /// 按名称获取参数值（通用方法）
        /// </summary>
        public object GetValue(string name)
        {
            if (_floatParams.TryGetValue(name, out var f)) return f;
            if (_boolParams.TryGetValue(name, out var b)) return b;
            if (_vector3Params.TryGetValue(name, out var v)) return v;
            if (_vector2Params.TryGetValue(name, out var v2)) return v2;
            if (_stringParams.TryGetValue(name, out var s)) return s;
            return 0;
        }

        /// <summary>
        /// 尝试获取 float 参数
        /// </summary>
        public bool TryGetFloat(string name, out float value) => _floatParams.TryGetValue(name, out value);

        /// <summary>
        /// 尝试获取 bool 参数
        /// </summary>
        public bool TryGetBool(string name, out bool value) => _boolParams.TryGetValue(name, out value);

        public bool HasParameter(string name) =>
            _floatParams.ContainsKey(name) ||
            _boolParams.ContainsKey(name) ||
            _vector3Params.ContainsKey(name) ||
            _vector2Params.ContainsKey(name) ||
            _stringParams.ContainsKey(name);

        /// <summary>
        /// 获取所有参数用于表达式绑定
        /// </summary>
        public Dictionary<string, object> GetAllParameters()
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in _floatParams) result[kvp.Key] = kvp.Value;
            foreach (var kvp in _boolParams) result[kvp.Key] = kvp.Value;
            foreach (var kvp in _vector3Params) result[kvp.Key] = kvp.Value;
            foreach (var kvp in _vector2Params) result[kvp.Key] = kvp.Value;
            foreach (var kvp in _stringParams) result[kvp.Key] = kvp.Value;
            return result;
        }
    }
}
