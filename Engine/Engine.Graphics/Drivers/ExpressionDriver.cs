#nullable disable

using NCalc;

namespace Engine.Graphics.Drivers
{
    /// <summary>
    /// 单个骨骼的表达式配置
    /// </summary>
    public class BoneExpressionConfig
    {
        /// <summary>
        /// 目标骨骼名称
        /// </summary>
        public string BoneName { get; set; }

        /// <summary>
        /// 位置 X 表达式（默认 "0"）
        /// </summary>
        public string PositionX { get; set; } = "0";

        /// <summary>
        /// 位置 Y 表达式（默认 "0"）
        /// </summary>
        public string PositionY { get; set; } = "0";

        /// <summary>
        /// 位置 Z 表达式（默认 "0"）
        /// </summary>
        public string PositionZ { get; set; } = "0";

        /// <summary>
        /// 旋转 X 表达式（度数，默认 "0"）
        /// </summary>
        public string RotationX { get; set; } = "0";

        /// <summary>
        /// 旋转 Y 表达式（度数，默认 "0"）
        /// </summary>
        public string RotationY { get; set; } = "0";

        /// <summary>
        /// 旋转 Z 表达式（度数，默认 "0"）
        /// </summary>
        public string RotationZ { get; set; } = "0";

        /// <summary>
        /// 缩放 X 表达式（默认 "1"）
        /// </summary>
        public string ScaleX { get; set; } = "1";

        /// <summary>
        /// 缩放 Y 表达式（默认 "1"）
        /// </summary>
        public string ScaleY { get; set; } = "1";

        /// <summary>
        /// 缩放 Z 表达式（默认 "1"）
        /// </summary>
        public string ScaleZ { get; set; } = "1";
    }

    /// <summary>
    /// 表达式驱动器
    /// 使用 NCalc 表达式计算骨骼变换
    /// </summary>
    public class ExpressionDriver : IAnimationDriver
    {
        public string Name => "Expression";
        public BlendMode BlendMode { get; set; } = BlendMode.Override;

        // 骨骼配置列表
        private readonly List<BoneExpressionConfig> _boneConfigs = new();

        // 编译后的表达式缓存
        private readonly Dictionary<string, Expression> _expressionCache = new();

        // 目标骨骼列表缓存
        private string[] _cachedTargetBones;

        /// <summary>
        /// 添加骨骼表达式配置
        /// </summary>
        public void AddBoneConfig(BoneExpressionConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.BoneName))
                return;

            _boneConfigs.Add(config);
            _cachedTargetBones = null;

            // 预编译表达式
            PrecompileExpression(config.PositionX);
            PrecompileExpression(config.PositionY);
            PrecompileExpression(config.PositionZ);
            PrecompileExpression(config.RotationX);
            PrecompileExpression(config.RotationY);
            PrecompileExpression(config.RotationZ);
            PrecompileExpression(config.ScaleX);
            PrecompileExpression(config.ScaleY);
            PrecompileExpression(config.ScaleZ);
        }

        /// <summary>
        /// 清除所有骨骼配置
        /// </summary>
        public void ClearBoneConfigs()
        {
            _boneConfigs.Clear();
            _cachedTargetBones = null;
        }

        /// <summary>
        /// 获取骨骼配置列表（只读）
        /// </summary>
        public IReadOnlyList<BoneExpressionConfig> BoneConfigs => _boneConfigs;

        // IAnimationDriver 接口实现
        public string[] TargetBones
        {
            get
            {
                if (_cachedTargetBones == null)
                {
                    _cachedTargetBones = _boneConfigs
                        .Select(c => c.BoneName)
                        .ToArray();
                }
                return _cachedTargetBones;
            }
        }

        // 当前参数（用于表达式求值）
        private AnimationParameters _currentParameters;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _currentParameters = parameters;
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            if (_currentParameters == null)
                return;

            foreach (var config in _boneConfigs)
            {
                var bone = model.FindBone(config.BoneName, throwIfNotFound: false);
                if (bone == null)
                    continue;

                try
                {
                    // 计算变换分量
                    float posX = EvaluateFloat(config.PositionX);
                    float posY = EvaluateFloat(config.PositionY);
                    float posZ = EvaluateFloat(config.PositionZ);

                    float rotX = EvaluateFloat(config.RotationX) * MathF.PI / 180f;
                    float rotY = EvaluateFloat(config.RotationY) * MathF.PI / 180f;
                    float rotZ = EvaluateFloat(config.RotationZ) * MathF.PI / 180f;

                    float scaleX = EvaluateFloat(config.ScaleX);
                    float scaleY = EvaluateFloat(config.ScaleY);
                    float scaleZ = EvaluateFloat(config.ScaleZ);

                    // 构建变换矩阵（缩放 -> 旋转 -> 平移）
                    var transform =
                        Matrix.CreateScale(scaleX, scaleY, scaleZ) *
                        Matrix.CreateRotationX(rotX) *
                        Matrix.CreateRotationY(rotY) *
                        Matrix.CreateRotationZ(rotZ) *
                        Matrix.CreateTranslation(posX, posY, posZ);

                    boneTransforms[bone.Index] = transform;
                }
                catch
                {
                    // 表达式求值失败时跳过该骨骼
                }
            }
        }

        /// <summary>
        /// 预编译表达式
        /// </summary>
        private void PrecompileExpression(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return;

            if (!_expressionCache.ContainsKey(expression))
            {
                try
                {
                    var expr = new Expression(expression);
                    expr.Options = ExpressionOptions.NoCache;
                    _expressionCache[expression] = expr;
                }
                catch
                {
                    // 表达式语法错误，忽略
                }
            }
        }

        /// <summary>
        /// 计算浮点表达式
        /// </summary>
        private float EvaluateFloat(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return 0f;

            // 检查是否为常量
            if (float.TryParse(expression, out float constant))
                return constant;

            if (!_expressionCache.TryGetValue(expression, out var expr))
                return 0f;

            try
            {
                // 绑定参数
                expr.Parameters = new Dictionary<string, object>();
                foreach (var param in _currentParameters.GetAllParameters())
                {
                    expr.Parameters[param.Key] = param.Value;
                }

                // 注册自定义函数
                AnimationExpressionFunctions.RegisterFunctions(expr);

                // 求值
                var result = expr.Evaluate();
                return Convert.ToSingle(result);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// 清除表达式缓存
        /// </summary>
        public void ClearCache()
        {
            _expressionCache.Clear();
        }
    }
}
