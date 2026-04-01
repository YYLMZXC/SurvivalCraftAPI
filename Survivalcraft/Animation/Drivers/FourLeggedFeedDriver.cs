#nullable disable
using Engine;
using Engine.Graphics;
using Engine.Graphics.Drivers;

namespace Game.Animation.Drivers
{
    /// <summary>
    /// 四足进食驱动器 - 处理进食时的头部动画
    /// </summary>
    public class FourLeggedFeedDriver : IAnimationDriver
    {
        public string Name => "FourLeggedFeed";
        public BlendMode BlendMode => BlendMode.Override;

        public string[] TargetBones => _targetBones;
        private string[] _targetBones = new[] { "Head", "Neck" };

        // 参数名称
        public string FeedFactorParam { get; set; } = "FeedFactor";
        public string GameTimeParam { get; set; } = "GameTime";
        public string LookAngleXParam { get; set; } = "LookAngleX";
        public string LookAngleYParam { get; set; } = "LookAngleY";

        // 可配置属性
        public float HeadMaxAngleX { get; set; } = 65f;
        public float HeadMaxAngleY { get; set; } = 55f;
        public float HeadRatio { get; set; } = 0.4f;
        public float NeckRatio { get; set; } = 0.6f;

        // 进食动画参数
        public float FeedBaseAngle { get; set; } = 25f;
        public float FeedNoiseRange { get; set; } = 45f;
        public float FeedNoiseFrequency { get; set; } = 3f;
        public int FeedNoiseOctaves { get; set; } = 2;
        public float FeedNoiseFreqStep { get; set; } = 2f;
        public float FeedNoiseAmpStep { get; set; } = 0.75f;

        private float _feedFactor;
        private float _gameTime;
        private float _lookAngleX;
        private float _lookAngleY;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _feedFactor = parameters.GetFloat(FeedFactorParam);
            _gameTime = parameters.GetFloat(GameTimeParam);
            _lookAngleX = parameters.GetFloat(LookAngleXParam);
            _lookAngleY = parameters.GetFloat(LookAngleYParam);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            var neckBone = model.FindBone("Neck", false);
            bool hasNeck = neckBone != null;

            var headBone = model.FindBone("Head");
            if (headBone == null) return;

            // 基础角度
            float maxAngleX = MathUtils.DegToRad(HeadMaxAngleX);
            float maxAngleY = MathUtils.DegToRad(HeadMaxAngleY);
            float lookAngleX = Math.Clamp(_lookAngleX, -maxAngleX, maxAngleX);
            float lookAngleY = Math.Clamp(_lookAngleY, -maxAngleY, maxAngleY);

            if (hasNeck)
            {
                lookAngleX *= HeadRatio;
                lookAngleY *= HeadRatio;
            }

            // 进食动画覆盖
            float noise = OctavedNoise1D(_gameTime, FeedNoiseFrequency, FeedNoiseOctaves, FeedNoiseFreqStep, FeedNoiseAmpStep);
            float feedY = -MathUtils.DegToRad(FeedBaseAngle + FeedNoiseRange * noise);
            lookAngleX = MathUtils.Lerp(lookAngleX, 0f, _feedFactor);
            lookAngleY = MathUtils.Lerp(lookAngleY, feedY, _feedFactor);

            boneTransforms[headBone.Index] =
                Matrix.CreateRotationX(lookAngleY) *
                Matrix.CreateRotationZ(-lookAngleX);

            // 颈部
            if (hasNeck)
            {
                float neckAngleX = Math.Clamp(_lookAngleX * NeckRatio, -maxAngleX, maxAngleX);
                float neckAngleY = Math.Clamp(_lookAngleY * NeckRatio, -maxAngleY, maxAngleY);
                neckAngleY = MathUtils.Lerp(neckAngleY, feedY * NeckRatio / HeadRatio, _feedFactor);

                boneTransforms[neckBone.Index] =
                    Matrix.CreateRotationX(neckAngleY) *
                    Matrix.CreateRotationZ(-neckAngleX);
            }
        }

        private static float OctavedNoise1D(float x, float frequency, int octaves, float frequencyStep, float amplitudeStep)
        {
            float total = 0f;
            float amplitude = 1f;
            float maxAmplitude = 0f;

            for (int i = 0; i < octaves; i++)
            {
                total += amplitude * Noise1D(x * frequency);
                maxAmplitude += amplitude;
                frequency *= frequencyStep;
                amplitude *= amplitudeStep;
            }

            return total / maxAmplitude;
        }

        private static float Noise1D(float x)
        {
            int i = (int)MathF.Floor(x);
            int j = (int)MathF.Ceiling(x);
            float t = x - i;
            float n0 = Hash(i);
            float n1 = Hash(j);
            float smooth = t * t * (3f - 2f * t);
            return n0 + smooth * (n1 - n0);
        }

        private static float Hash(int x)
        {
            x = (x << 13) ^ x;
            return ((x * (x * x * 15731 + 789221) + 1376312589) & 0x7FFFFFFF) / 2147483648f;
        }
    }
}
