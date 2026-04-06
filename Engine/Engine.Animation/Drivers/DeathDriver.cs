#nullable disable

using Engine.Graphics;

namespace Engine.Animation.Drivers
{
    /// <summary>
    /// 死亡动画驱动器 - 默认作用于根骨骼产生全身倒下效果
    /// </summary>
    public class DeathDriver : IAnimationDriver
    {
        public string Name => "Death";
        public AnimationBlendMode BlendMode => AnimationBlendMode.Override;

        // 目标骨骼 - 默认为空，表示作用于根骨骼
        private string[] _targetBones = Array.Empty<string>();
        public string[] TargetBones => _targetBones;

        // 可选：指定特定骨骼名称（如果为空则使用根骨骼）
        public string RootBoneName { get; set; } = null;

        public string DeathPhaseParam { get; set; } = "DeathPhase";

        // 死亡动画配置
        public float RollAngle { get; set; } = 90f;       // 侧翻角度（度）
        public float DropHeight { get; set; } = 0.3f;    // 下沉高度
        public float PitchAngle { get; set; } = 0f;      // 前后倾斜角度

        private float _deathPhase;
        private int _rootBoneIndex = -1;

        /// <summary>
        /// 获取默认根骨骼索引。优先使用 model.RootBone，否则使用第一个骨骼。
        /// </summary>
        private static int GetDefaultRootBoneIndex(Model model)
        {
            if (model.RootBone != null)
                return model.RootBone.Index;
            if (model.Bones.Count > 0)
                return model.Bones[0].Index;
            return -1;
        }

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _deathPhase = Math.Clamp(parameters.GetFloat(DeathPhaseParam), 0f, 1f);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            if (_deathPhase <= 0f) return;

            // 获取根骨骼索引
            if (_rootBoneIndex < 0)
            {
                if (!string.IsNullOrEmpty(RootBoneName))
                {
                    var bone = model.FindBone(RootBoneName);
                    _rootBoneIndex = bone?.Index ?? GetDefaultRootBoneIndex(model);
                }
                else
                {
                    _rootBoneIndex = GetDefaultRootBoneIndex(model);
                }
            }

            // 如果没有有效骨骼，直接返回
            if (_rootBoneIndex < 0) return;

            float t = _deathPhase;
            float rollRad = RollAngle * t * MathF.PI / 180f;
            float pitchRad = PitchAngle * t * MathF.PI / 180f;
            float dropY = -DropHeight * t;

            // 构建死亡变换：下沉 -> 俯仰 -> 侧翻
            Matrix deathTransform =
                Matrix.CreateTranslation(0, dropY * 0.5f, 0) *
                Matrix.CreateRotationX(pitchRad) *
                Matrix.CreateRotationZ(rollRad) *
                Matrix.CreateTranslation(0, dropY * 0.5f, 0);

            // 应用到根骨骼
            boneTransforms[_rootBoneIndex] = deathTransform;
        }
    }
}
