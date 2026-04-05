#nullable disable
using Engine;
using Engine.Animation;
using Engine.Graphics;

namespace Game.Animation.Drivers
{
    /// <summary>
    /// 不能飞的鸟类进食驱动器 - 处理进食时的头部动画
    /// </summary>
    public class FlightlessBirdFeedDriver : IAnimationDriver
    {
        public string Name => "FlightlessBirdFeed";
        public AnimationBlendMode BlendMode => AnimationBlendMode.Override;

        public string[] TargetBones => _targetBones;
        private string[] _targetBones = new[] { "Head", "Neck" };

        // 参数名称
        public string FeedFactorParam { get; set; } = "FeedFactor";
        public string LookAngleXParam { get; set; } = "LookAngleX";
        public string LookAngleYParam { get; set; } = "LookAngleY";
        public string GameTimeParam { get; set; } = "GameTime";

        // 可配置属性
        public float MinPeckAngle { get; set; } = 35f; // 最小啄食角度（度）
        public float MaxPeckAngle { get; set; } = 55f; // 最大啄食角度增量（度）
        public float HeadMaxAngleX { get; set; } = 90f;
        public float HeadMaxAngleY { get; set; } = 50f;
        public float HeadRatio { get; set; } = 0.6f;
        public float NeckRatio { get; set; } = 0.4f;

        private float _feedFactor;
        private float _lookAngleX;
        private float _lookAngleY;
        private float _gameTime;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _feedFactor = parameters.GetFloat(FeedFactorParam);
            _lookAngleX = parameters.GetFloat(LookAngleXParam);
            _lookAngleY = parameters.GetFloat(LookAngleYParam);
            _gameTime = parameters.GetFloat(GameTimeParam);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            if (_feedFactor <= 0f) return;

            var neckBone = model.FindBone("Neck", false);
            bool hasNeck = neckBone != null;

            var headBone = model.FindBone("Head");
            if (headBone == null) return;

            // 使用 SimplexNoise 生成自然的啄食动作
            // 原始逻辑: y = 0 - DegToRad(35 + 55 * SimplexNoise.OctavedNoise(gameTime, 3f, 2, 2f, 0.75f))
            float noise = SimplexNoise.OctavedNoise(_gameTime, 3f, 2, 2f, 0.75f);
            float peckAngle = MathUtils.DegToRad(MinPeckAngle + MaxPeckAngle * noise);

            // 向下啄食
            float headPitch = -peckAngle + _lookAngleY * (1f - _feedFactor);

            // 限制角度
            float maxAngleX = MathUtils.DegToRad(HeadMaxAngleX);
            float maxAngleY = MathUtils.DegToRad(HeadMaxAngleY);
            float lookAngleX = Math.Clamp(_lookAngleX, -maxAngleX, maxAngleX);
            float lookAngleY = Math.Clamp(headPitch, -maxAngleY, maxAngleY);

            if (hasNeck)
            {
                lookAngleX *= HeadRatio;
            }

            boneTransforms[headBone.Index] =
                Matrix.CreateRotationX(lookAngleY) *
                Matrix.CreateRotationZ(-lookAngleX);

            if (hasNeck)
            {
                float neckPitch = peckAngle * 0.3f * _feedFactor + _lookAngleY * NeckRatio;
                neckPitch = Math.Clamp(neckPitch, -maxAngleY, maxAngleY);

                boneTransforms[neckBone.Index] =
                    Matrix.CreateRotationX(neckPitch) *
                    Matrix.CreateRotationZ(-_lookAngleX * NeckRatio);
            }
        }
    }
}
