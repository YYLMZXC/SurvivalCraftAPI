#nullable disable

namespace Engine.Graphics.Drivers
{
    /// <summary>
    /// 死亡动画驱动器
    /// </summary>
    public class DeathDriver : IAnimationDriver
    {
        public string Name => "Death";
        public BlendMode BlendMode => BlendMode.Override;

        // 可配置的目标骨骼名称
        public string BodyBoneName { get; set; } = "Body";
        public string HeadBoneName { get; set; } = "Head";

        // IAnimationDriver 接口实现
        public string[] TargetBones => _cachedTargetBones ??= new[] { BodyBoneName, HeadBoneName };
        private string[] _cachedTargetBones;

        public string DeathPhaseParam { get; set; } = "DeathPhase";

        // 死亡动画配置
        public float BodyRotationMax { get; set; } = 90f;  // 身体最大旋转角度
        public float HeadRotationMax { get; set; } = 50f;  // 头部最大旋转角度
        public float BodyDropHeight { get; set; } = 0.5f;  // 身体下落高度

        private float _deathPhase;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _deathPhase = Math.Clamp(parameters.GetFloat(DeathPhaseParam), 0f, 1f);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            float t = _deathPhase;

            // 身体：旋转 + 下落
            var bodyBone = model.FindBone(BodyBoneName);
            if (bodyBone != null)
            {
                float rotation = BodyRotationMax * t * MathF.PI / 180f;
                float dropY = -BodyDropHeight * t;

                boneTransforms[bodyBone.Index] =
                    Matrix.CreateTranslation(0, dropY * 0.5f, 0) *
                    Matrix.CreateRotationZ(rotation) *
                    Matrix.CreateTranslation(0, dropY * 0.5f, 0);
            }

            // 头部：俯仰
            var headBone = model.FindBone(HeadBoneName);
            if (headBone != null)
            {
                float rotation = HeadRotationMax * t * MathF.PI / 180f;
                boneTransforms[headBone.Index] = Matrix.CreateRotationX(rotation);
            }
        }
    }
}
