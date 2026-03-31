#nullable disable

namespace Engine.Graphics
{
    /// <summary>
    /// 动画表达式自定义函数
    /// </summary>
    public static class AnimationExpressionFunctions
    {
        public static void RegisterFunctions(Expression expression)
        {
            expression.EvaluateFunction += (name, args) =>
            {
                switch (name.ToLowerInvariant())
                {
                    case "lerp":
                        {
                            var values = args.EvaluateParameters(default);
                            float a = Convert.ToSingle(values[0]);
                            float b = Convert.ToSingle(values[1]);
                            float t = Convert.ToSingle(values[2]);
                            args.Result = a + (b - a) * t;
                        }
                        break;
                    case "smoothstep":
                        {
                            var values = args.EvaluateParameters(default);
                            float t = Convert.ToSingle(values[0]);
                            args.Result = t * t * (3 - 2 * t);
                        }
                        break;
                    case "degtorad":
                        {
                            var values = args.EvaluateParameters(default);
                            args.Result = Convert.ToSingle(values[0]) * MathF.PI / 180f;
                        }
                        break;
                    case "radtodeg":
                        {
                            var values = args.EvaluateParameters(default);
                            args.Result = Convert.ToSingle(values[0]) * 180f / MathF.PI;
                        }
                        break;
                    case "clamp":
                        {
                            var values = args.EvaluateParameters(default);
                            args.Result = Math.Clamp(
                                Convert.ToSingle(values[0]),
                                Convert.ToSingle(values[1]),
                                Convert.ToSingle(values[2]));
                        }
                        break;
                    case "pi":
                        args.Result = MathF.PI;
                        break;
                }
            };
        }
    }
}
