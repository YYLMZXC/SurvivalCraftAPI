using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game {
    public class ComponentFourLeggedModel : ComponentCreatureModel {
        public enum Gait {
            Walk,
            Trot,
            Canter
        }

        public SubsystemAudio m_subsystemAudio;
        public SubsystemSoundMaterials m_subsystemSoundMaterials;

        public float m_walkAnimationSpeed;
        public float m_canterLegsAngleFactor;
        public float m_walkFrontLegsAngle;
        public float m_walkHindLegsAngle;
        public float m_walkBobHeight;
        public bool m_moveLegWhenFeeding;
        public bool m_canCanter;
        public bool m_canTrot;
        public bool m_useCanterSound;

        public Gait m_gait;
        public float m_feedFactor;
        public float m_buttFactor;
        public float m_buttPhase;
        public float m_footstepsPhase;

        public override float AttackPhase {
            get => m_buttPhase;
            set => m_buttPhase = value;
        }

        public override float AttackFactor {
            get => m_buttFactor;
            set => m_buttFactor = value;
        }

        public override void Update(float dt) {
            float footstepsPhase = m_footstepsPhase;
            float num = m_componentCreature.ComponentLocomotion.SlipSpeed
                ?? Vector3.Dot(m_componentCreature.ComponentBody.Velocity, m_componentCreature.ComponentBody.Matrix.Forward);
            if (m_canCanter && num > 0.7f * m_componentCreature.ComponentLocomotion.WalkSpeed) {
                m_gait = Gait.Canter;
                MovementAnimationPhase += num * dt * 0.7f * m_walkAnimationSpeed;
                m_footstepsPhase += 0.7f * m_walkAnimationSpeed * num * dt;
            }
            else if (m_canTrot && num > 0.5f * m_componentCreature.ComponentLocomotion.WalkSpeed) {
                m_gait = Gait.Trot;
                MovementAnimationPhase += num * dt * m_walkAnimationSpeed;
                m_footstepsPhase += 1.25f * m_walkAnimationSpeed * num * dt;
            }
            else if (MathF.Abs(num) > 0.2f) {
                m_gait = Gait.Walk;
                MovementAnimationPhase += num * dt * m_walkAnimationSpeed;
                m_footstepsPhase += 1.25f * m_walkAnimationSpeed * num * dt;
            }
            else {
                m_gait = Gait.Walk;
                MovementAnimationPhase = 0f;
                m_footstepsPhase = 0f;
            }
            float num2 = 0f;
            if (m_gait == Gait.Canter) {
                num2 = (0f - m_walkBobHeight) * 1.5f * MathF.Sin((float)Math.PI * 2f * MovementAnimationPhase);
            }
            else if (m_gait == Gait.Trot) {
                num2 = m_walkBobHeight * 1.5f * MathUtils.Sqr(MathF.Sin((float)Math.PI * 2f * MovementAnimationPhase));
            }
            else if (m_gait == Gait.Walk) {
                num2 = (0f - m_walkBobHeight) * MathUtils.Sqr(MathF.Sin((float)Math.PI * 2f * MovementAnimationPhase));
            }
            float num3 = MathUtils.Min(12f * m_subsystemTime.GameTimeDelta, 1f);
            Bob += num3 * (num2 - Bob);
            if (m_gait == Gait.Canter && m_useCanterSound) {
                float num4 = MathF.Floor(m_footstepsPhase);
                if (m_footstepsPhase > num4
                    && footstepsPhase <= num4) {
                    string footstepSoundMaterialName = m_subsystemSoundMaterials.GetFootstepSoundMaterialName(m_componentCreature);
                    if (!string.IsNullOrEmpty(footstepSoundMaterialName)
                        && footstepSoundMaterialName != "Water") {
                        m_subsystemAudio.PlayRandomSound(
                            "Audio/Footsteps/CanterDirt",
                            0.75f,
                            m_random.Float(-0.25f, 0f),
                            m_componentCreature.ComponentBody.Position,
                            3f,
                            true
                        );
                    }
                }
            }
            else {
                float num5 = MathF.Floor(m_footstepsPhase);
                if (m_footstepsPhase > num5
                    && footstepsPhase <= num5) {
                    m_componentCreature.ComponentCreatureSounds.PlayFootstepSound(1f);
                }
            }
            m_feedFactor = FeedOrder ? MathUtils.Min(m_feedFactor + 2f * dt, 1f) : MathUtils.Max(m_feedFactor - 2f * dt, 0f);
            IsAttackHitMoment = false;
            if (AttackOrder) {
                m_buttFactor = MathUtils.Min(m_buttFactor + 4f * dt, 1f);
                float buttPhase = m_buttPhase;
                m_buttPhase = MathUtils.Remainder(m_buttPhase + dt * 2f, 1f);
                if (buttPhase < 0.5f
                    && m_buttPhase >= 0.5f) {
                    IsAttackHitMoment = true;
                }
            }
            else {
                m_buttFactor = MathUtils.Max(m_buttFactor - 4f * dt, 0f);
                if (m_buttPhase != 0f) {
                    if (m_buttPhase > 0.5f) {
                        m_buttPhase = MathUtils.Remainder(MathUtils.Min(m_buttPhase + dt * 2f, 1f), 1f);
                    }
                    else if (m_buttPhase > 0f) {
                        m_buttPhase = MathUtils.Max(m_buttPhase - dt * 2f, 0f);
                    }
                }
            }
            FeedOrder = false;
            AttackOrder = false;
            base.Update(dt);
        }

        /// <summary>
        /// 同步动画参数到动画控制器
        /// </summary>
        protected override void SyncAnimationParameters()
        {
            base.SyncAnimationParameters();

            var ctrl = AnimationController;
            if (ctrl == null) return;

            // 四足动物特有参数
            ctrl.Parameters.SetFloat("MovementPhase", MovementAnimationPhase);
            ctrl.Parameters.SetFloat("Gait", (int)m_gait);
            ctrl.Parameters.SetFloat("FeedFactor", m_feedFactor);
            ctrl.Parameters.SetFloat("Bob", Bob);

            // 头部追踪角度（转换为度数）
            var lookAngles = m_componentCreature.ComponentLocomotion.LookAngles;
            ctrl.Parameters.SetFloat("LookAngleX", lookAngles.X * 180f / MathF.PI);
            ctrl.Parameters.SetFloat("LookAngleY", lookAngles.Y * 180f / MathF.PI);
        }

        /// <summary>
        /// 动画由 AnimationController 和驱动器处理，不再需要硬编码
        /// </summary>
        public override void AnimateCreature() {
            // 空实现 - 动画由 AnimationController 处理
            // 如果没有配置 AnimationController，生物将不会动画
        }

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap) {
            base.Load(valuesDictionary, idToEntityMap);
            m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
            m_subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true);
            m_walkAnimationSpeed = valuesDictionary.GetValue<float>("WalkAnimationSpeed");
            m_walkFrontLegsAngle = valuesDictionary.GetValue<float>("WalkFrontLegsAngle");
            m_walkHindLegsAngle = valuesDictionary.GetValue<float>("WalkHindLegsAngle");
            m_canterLegsAngleFactor = valuesDictionary.GetValue<float>("CanterLegsAngleFactor");
            m_walkBobHeight = valuesDictionary.GetValue<float>("WalkBobHeight");
            m_moveLegWhenFeeding = valuesDictionary.GetValue<bool>("MoveLegWhenFeeding");
            m_canCanter = valuesDictionary.GetValue<bool>("CanCanter");
            m_canTrot = valuesDictionary.GetValue<bool>("CanTrot");
            m_useCanterSound = valuesDictionary.GetValue<bool>("UseCanterSound");
        }

        public override void SetModel(Model model) {
            base.SetModel(model);
            if (IsSet) return;

            // 配置驱动器参数
            if (AnimationController != null) {
                // 把 Database.xml 中的参数传递给驱动器
                AnimationController.Parameters.SetFloat("WalkAnimationSpeed", m_walkAnimationSpeed);
                AnimationController.Parameters.SetFloat("WalkFrontLegsAngle", m_walkFrontLegsAngle);
                AnimationController.Parameters.SetFloat("WalkHindLegsAngle", m_walkHindLegsAngle);
                AnimationController.Parameters.SetFloat("CanterLegsAngleFactor", m_canterLegsAngleFactor);
                AnimationController.Parameters.SetFloat("WalkBobHeight", m_walkBobHeight);
            }
        }
    }
}
