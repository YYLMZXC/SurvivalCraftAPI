#nullable disable
using Engine;
using Engine.Graphics;
using Engine.Graphics.Drivers;

namespace Game.Animation.Drivers
{
    /// <summary>
    /// 鸟类啄食驱动器 - 处理啄食时的头部动画
    /// </summary>
    public class BirdPeckDriver : IAnimationDriver
    {
        public string Name => "BirdPeck";
        public BlendMode BlendMode => BlendMode.Override;

        public string[] TargetBones => _targetBones;
        private string[] _targetBones = new[] { "Head", "Neck" };

        // 参数名称
        public string PeckPhaseParam { get; set; } = "PeckPhase";
        public string LookAngleXParam { get; set; } = "LookAngleX";
        public string LookAngleYParam { get; set; } = "LookAngleY";

        // 可配置属性
        public float PeckAngle { get; set; } = 1.25f; // 啄食时的头部下摆角度（弧度）

        private float _peckPhase;
        private float _lookAngleX;
        private float _lookAngleY;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _peckPhase = parameters.GetFloat(PeckPhaseParam);
            _lookAngleX = parameters.GetFloat(LookAngleXParam);
            _lookAngleY = parameters.GetFloat(LookAngleYParam);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            var neckBone = model.FindBone("Neck", false);
            bool hasNeck = neckBone != null;

            var headBone = model.FindBone("Head");
            if (headBone == null) return;

            // 计算啄食动画
            // 原始逻辑: num4 = 0.5f * Sin(MovementPhase / 2)
            //          num4 -= 1.25f * (1 - (cos >= 0 ? cos : -0.5 * cos))
            float cosPhase = MathF.Cos(MathF.PI * 2f * _peckPhase);
            float peckAngle = PeckAngle * (1f - (cosPhase >= 0f ? cosPhase : -0.5f * cosPhase));

            // 站立时的头部摆动
            float headPitch = -peckAngle + _lookAngleY;

            boneTransforms[headBone.Index] =
                Matrix.CreateRotationX(headPitch) *
                Matrix.CreateRotationZ(-_lookAngleX / 2f);

            if (hasNeck)
            {
                float neckPitch = peckAngle * 0.5f + _lookAngleY;
                boneTransforms[neckBone.Index] =
                    Matrix.CreateRotationX(neckPitch) *
                    Matrix.CreateRotationZ(-_lookAngleX / 2f);
            }
        }
    }
}
