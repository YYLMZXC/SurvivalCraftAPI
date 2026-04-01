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
            float speed = m_componentCreature.ComponentLocomotion.SlipSpeed
                ?? Vector3.Dot(m_componentCreature.ComponentBody.Velocity, m_componentCreature.ComponentBody.Matrix.Forward);

            // 从配置读取动画速度参数
            float feedSpeed = 2f;
            float attackSpeed = 4f;
            float attackPhaseSpeed = 2f;
            if (AnimationController != null) {
                feedSpeed = AnimationController.Parameters.GetFloat("FeedSpeed");
                attackSpeed = AnimationController.Parameters.GetFloat("AttackSpeed");
                attackPhaseSpeed = AnimationController.Parameters.GetFloat("AttackPhaseSpeed");
            }

            // 步态现在由配置中的状态规则控制
            // MovementAnimationPhase 基于速度计算
            if (MathF.Abs(speed) > 0.2f) {
                MovementAnimationPhase += speed * dt * m_walkAnimationSpeed;
                m_footstepsPhase += 1.25f * m_walkAnimationSpeed * MathF.Abs(speed) * dt;
            }
            else {
                MovementAnimationPhase = 0f;
                m_footstepsPhase = 0f;
            }

            // Bob 计算 - 使用统一的计算方式，让驱动器根据 Gait 调整
            float targetBob = 0f;
            if (MathF.Abs(speed) > 0.2f) {
                targetBob = -m_walkBobHeight * MathUtils.Sqr(MathF.Sin((float)Math.PI * 2f * MovementAnimationPhase));
            }
            float smoothFactor = MathUtils.Min(12f * m_subsystemTime.GameTimeDelta, 1f);
            Bob += smoothFactor * (targetBob - Bob);

            // 脚步声
            float num5 = MathF.Floor(m_footstepsPhase);
            if (m_footstepsPhase > num5 && footstepsPhase <= num5) {
                m_componentCreature.ComponentCreatureSounds.PlayFootstepSound(1f);
            }

            // 进食动画（使用配置速度）
            m_feedFactor = FeedOrder ? MathUtils.Min(m_feedFactor + feedSpeed * dt, 1f) : MathUtils.Max(m_feedFactor - feedSpeed * dt, 0f);

            // 攻击动画（使用配置速度）
            IsAttackHitMoment = false;
            if (AttackOrder) {
                m_buttFactor = MathUtils.Min(m_buttFactor + attackSpeed * dt, 1f);
                float buttPhase = m_buttPhase;
                m_buttPhase = MathUtils.Remainder(m_buttPhase + dt * attackPhaseSpeed, 1f);
                if (buttPhase < 0.5f && m_buttPhase >= 0.5f) {
                    IsAttackHitMoment = true;
                }
            }
            else {
                m_buttFactor = MathUtils.Max(m_buttFactor - attackSpeed * dt, 0f);
                if (m_buttPhase != 0f) {
                    if (m_buttPhase > 0.5f) {
                        m_buttPhase = MathUtils.Remainder(MathUtils.Min(m_buttPhase + dt * attackPhaseSpeed, 1f), 1f);
                    }
                    else if (m_buttPhase > 0f) {
                        m_buttPhase = MathUtils.Max(m_buttPhase - dt * attackPhaseSpeed, 0f);
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
            ctrl.Parameters.SetFloat("FeedFactor", m_feedFactor);
            ctrl.Parameters.SetFloat("Bob", Bob);

            // 顶撞/攻击动画参数
            ctrl.Parameters.SetFloat("ButtFactor", m_buttFactor);
            ctrl.Parameters.SetFloat("ButtPhase", m_buttPhase);

            // 头部追踪角度（弧度）
            var lookAngles = m_componentCreature.ComponentLocomotion.LookAngles;
            ctrl.Parameters.SetFloat("LookAngleX", lookAngles.X);
            ctrl.Parameters.SetFloat("LookAngleY", lookAngles.Y);
        }

        /// <summary>
        /// 动画由 AnimationController 和驱动器处理，不再需要硬编码
        /// </summary>
        public override void AnimateCreature() {
            // 空实现 - 动画由 AnimationController 处理
            // 如果没有配置 AnimationController，生物将不会动画
        }

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap) {
            m_walkAnimationSpeed = valuesDictionary.GetValue<float>("WalkAnimationSpeed");
            m_walkFrontLegsAngle = valuesDictionary.GetValue<float>("WalkFrontLegsAngle");
            m_walkHindLegsAngle = valuesDictionary.GetValue<float>("WalkHindLegsAngle");
            m_canterLegsAngleFactor = valuesDictionary.GetValue<float>("CanterLegsAngleFactor");
            m_walkBobHeight = valuesDictionary.GetValue<float>("WalkBobHeight");
            m_moveLegWhenFeeding = valuesDictionary.GetValue<bool>("MoveLegWhenFeeding");
            m_canCanter = valuesDictionary.GetValue<bool>("CanCanter");
            m_canTrot = valuesDictionary.GetValue<bool>("CanTrot");
            m_useCanterSound = valuesDictionary.GetValue<bool>("UseCanterSound");
            base.Load(valuesDictionary, idToEntityMap);
            m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
            m_subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true);
        }

        public override void SetModel(Model model) {
            base.SetModel(model);
            if (IsSet) return;

            // 配置驱动器参数
            if (AnimationController != null) {
                // 把 Database.xml 中的参数传递给驱动器（会覆盖动画配置文件中的设置）
                AnimationController.Parameters.SetFloat("WalkAnimationSpeed", m_walkAnimationSpeed);
                AnimationController.Parameters.SetFloat("WalkFrontLegsAngle", m_walkFrontLegsAngle);
                AnimationController.Parameters.SetFloat("WalkHindLegsAngle", m_walkHindLegsAngle);
                AnimationController.Parameters.SetFloat("CanterLegsAngleFactor", m_canterLegsAngleFactor);
                AnimationController.Parameters.SetFloat("WalkBobHeight", m_walkBobHeight);

                // 状态规则条件参数
                AnimationController.Parameters.SetBool("CanCanter", m_canCanter);
                AnimationController.Parameters.SetBool("CanTrot", m_canTrot);

                // 运行时参数初始值（会在 SyncAnimationParameters 中每帧更新）
                //AnimationController.Parameters.SetFloat("Speed", 0f);
                AnimationController.Parameters.SetFloat("SpeedAbs", 0f);
                AnimationController.Parameters.SetFloat("MovementPhase", 0f);
                AnimationController.Parameters.SetBool("IsOnGround", true);
            }
        }
    }
}
