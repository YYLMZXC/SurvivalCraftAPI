#nullable disable

namespace Engine.Graphics
{
    /// <summary>
    /// 参数容器类，用于存储动画系统需要的各种参数。
    /// </summary>
    public class AnimationParameters
    {
        private readonly Dictionary<string, float> _floatParams = new();
        private readonly Dictionary<string, bool> _boolParams = new();
        private readonly Dictionary<string, Vector3> _vector3Params = new();

        public void SetFloat(string name, float value) => _floatParams[name] = value;
        public void SetBool(string name, bool value) => _boolParams[name] = value;
        public void SetVector3(string name, Vector3 value) => _vector3Params[name] = value;

        public float GetFloat(string name) => _floatParams.TryGetValue(name, out var v) ? v : 0f;
        public bool GetBool(string name) => _boolParams.TryGetValue(name, out var v) ? v : false;
        public Vector3 GetVector3(string name) => _vector3Params.TryGetValue(name, out var v) ? v : Vector3.Zero;

        public bool HasParameter(string name) =>
            _floatParams.ContainsKey(name) ||
            _boolParams.ContainsKey(name) ||
            _vector3Params.ContainsKey(name);

        /// <summary>
        /// 获取所有参数用于表达式绑定
        /// </summary>
        public Dictionary<string, object> GetAllParameters()
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in _floatParams) result[kvp.Key] = kvp.Value;
            foreach (var kvp in _boolParams) result[kvp.Key] = kvp.Value;
            foreach (var kvp in _vector3Params) result[kvp.Key] = kvp.Value;
            return result;
        }
    }
}
