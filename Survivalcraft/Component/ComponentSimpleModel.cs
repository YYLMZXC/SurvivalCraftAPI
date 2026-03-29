using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game {
    public class ComponentSimpleModel : ComponentModel {
        public SubsystemGameInfo m_subsystemGameInfo;

        public ComponentSpawn m_componentSpawn;

        public override void Animate() {
            base.Animate();

            // glTF 模型（有动画或有蒙皮）需要将实体变换应用到根骨骼
            // DAE 模型通过 SetBoneTransform 处理，保持原有行为
            bool isGltfModel = Model.HasSkin || Model.HasAnimations;

            if (Animated && isGltfModel) {
                // 获取实体的位置和旋转
                Vector3 entityPosition = m_componentFrame.Position;
                Quaternion entityRotation = m_componentFrame.Rotation;
                Matrix entityTransform = Matrix.CreateFromQuaternion(entityRotation) * Matrix.CreateTranslation(entityPosition);

                // 重要：根骨骼变换（如 Z_UP 坐标转换）必须保留
                if (m_boneTransforms[Model.RootBone.Index].HasValue) {
                    // 根骨骼有动画变换，直接叠加实体变换
                    Matrix animTransform = m_boneTransforms[Model.RootBone.Index].Value;
                    m_boneTransforms[Model.RootBone.Index] = animTransform * entityTransform;
                } else {
                    // 根骨骼没有动画变换，需要保留原始变换（如 Z_UP）并叠加实体变换
                    Matrix rootBoneTransform = Model.RootBone.Transform;
                    m_boneTransforms[Model.RootBone.Index] = rootBoneTransform * entityTransform;
                }
                return;
            }

            // DAE 模型或无动画的 glTF 模型
            if (m_componentSpawn != null) {
                Opacity = m_componentSpawn.SpawnDuration > 0f
                    ? (float)MathUtils.Saturate(
                        (m_subsystemGameInfo.TotalElapsedGameTime - m_componentSpawn.SpawnTime) / m_componentSpawn.SpawnDuration
                    )
                    : 1f;
                if (m_componentSpawn.DespawnTime.HasValue) {
                    Opacity = MathUtils.Min(
                        Opacity.Value,
                        (float)MathUtils.Saturate(
                            1.0 - (m_subsystemGameInfo.TotalElapsedGameTime - m_componentSpawn.DespawnTime.Value) / m_componentSpawn.DespawnDuration
                        )
                    );
                }
            }
            SetBoneTransform(Model.RootBone.Index, m_componentFrame.Matrix);
        }

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap) {
            m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
            m_componentSpawn = Entity.FindComponent<ComponentSpawn>();
            base.Load(valuesDictionary, idToEntityMap);
        }
    }
}