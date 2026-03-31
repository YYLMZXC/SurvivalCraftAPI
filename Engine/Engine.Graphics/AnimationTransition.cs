#nullable disable

namespace Engine.Graphics
{
    /// <summary>
    /// 过渡中断策略
    /// </summary>
    public enum TransitionInterruptMode
    {
        /// <summary>
        /// 可以被新过渡中断
        /// </summary>
        CanInterrupt,

        /// <summary>
        /// 不可以被新过渡中断，必须等待过渡完成
        /// </summary>
        CannotInterrupt,

        /// <summary>
        /// 只有更高优先级的过渡才能中断
        /// </summary>
        HigherPriorityOnly
    }

    /// <summary>
    /// 动画过渡，负责管理两个动画之间的平滑过渡
    /// </summary>
    public class AnimationTransition
    {
        private float _elapsedTime;
        private float _duration;
        private bool _isActive;
        private int _priority;

        // 过渡源状态
        private AnimationPlayer _sourcePlayer;
        private Matrix?[] _sourceTransforms;

        // 过渡目标状态
        private AnimationPlayer _targetPlayer;
        private Model _targetModel;
        private ModelAnimation _targetAnimation;
        private bool _targetLoop;

        /// <summary>
        /// 过渡时长（秒）
        /// </summary>
        public float Duration
        {
            get => _duration;
            set => _duration = Math.Max(0f, value);
        }

        /// <summary>
        /// 已过渡时间（秒）
        /// </summary>
        public float ElapsedTime => _elapsedTime;

        /// <summary>
        /// 过渡进度 (0-1)
        /// </summary>
        public float Progress => _duration > 0 ? Math.Min(1f, _elapsedTime / _duration) : 1f;

        /// <summary>
        /// 过渡是否正在进行
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// 过渡中断模式
        /// </summary>
        public TransitionInterruptMode InterruptMode { get; set; } = TransitionInterruptMode.CanInterrupt;

        /// <summary>
        /// 过渡优先级（数值越大优先级越高）
        /// </summary>
        public int Priority => _priority;

        /// <summary>
        /// 目标动画播放器
        /// </summary>
        public AnimationPlayer TargetPlayer => _targetPlayer;

        /// <summary>
        /// 创建动画过渡实例
        /// </summary>
        public AnimationTransition()
        {
            _duration = 0.25f; // 默认过渡时长 0.25 秒
        }

        /// <summary>
        /// 开始过渡到新动画
        /// </summary>
        /// <param name="sourcePlayer">源动画播放器（可以为 null）</param>
        /// <param name="targetModel">目标模型</param>
        /// <param name="targetAnimation">目标动画</param>
        /// <param name="loop">是否循环</param>
        /// <param name="duration">过渡时长</param>
        /// <param name="interruptMode">中断模式</param>
        /// <param name="priority">优先级</param>
        /// <returns>是否成功开始过渡</returns>
        public bool StartTransition(
            AnimationPlayer sourcePlayer,
            Model targetModel,
            ModelAnimation targetAnimation,
            bool loop,
            float duration,
            TransitionInterruptMode interruptMode = TransitionInterruptMode.CanInterrupt,
            int priority = 0)
        {
            // 检查是否可以被中断
            if (_isActive)
            {
                if (InterruptMode == TransitionInterruptMode.CannotInterrupt)
                    return false;

                if (InterruptMode == TransitionInterruptMode.HigherPriorityOnly && priority <= _priority)
                    return false;
            }

            // 保存源状态
            _sourcePlayer = sourcePlayer;
            if (_sourcePlayer != null && _sourcePlayer.IsPlaying && _sourcePlayer.Animation != null)
            {
                // 确保源变换缓冲区足够大
                int boneCount = targetModel != null ? targetModel.Bones.Count : 0;
                if (boneCount > 0)
                {
                    EnsureSourceBufferSize(boneCount);
                    _sourcePlayer.SampleBoneTransforms(_sourceTransforms);
                }
            }

            // 设置目标状态
            _targetModel = targetModel;
            _targetAnimation = targetAnimation;
            _targetLoop = loop;
            _targetPlayer = new AnimationPlayer();
            _targetPlayer.SetAnimation(targetModel, targetAnimation);
            _targetPlayer.Play(loop);

            // 设置过渡参数
            _duration = Math.Max(0f, duration);
            _elapsedTime = 0f;
            _isActive = true;
            _priority = priority;
            InterruptMode = interruptMode;

            return true;
        }

        /// <summary>
        /// 更新过渡状态
        /// </summary>
        /// <param name="deltaTime">帧时间</param>
        public void Update(float deltaTime)
        {
            if (!_isActive)
                return;

            _elapsedTime += deltaTime;

            // 更新目标动画
            _targetPlayer?.Update(deltaTime);

            // 检查过渡是否完成
            if (_elapsedTime >= _duration)
            {
                CompleteTransition();
            }
        }

        /// <summary>
        /// 采样当前过渡状态的骨骼变换
        /// </summary>
        /// <param name="boneTransforms">输出骨骼变换数组</param>
        /// <param name="model">模型对象</param>
        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            if (!_isActive || boneTransforms == null || model == null)
            {
                // 如果没有活动过渡，直接采样目标动画
                _targetPlayer?.SampleBoneTransforms(boneTransforms);
                return;
            }

            int boneCount = model.Bones.Count;

            // 如果进度为 0 或没有源变换，使用源变换
            if (Progress <= 0f || _sourceTransforms == null || !_sourcePlayer?.IsPlaying == true)
            {
                _targetPlayer?.SampleBoneTransforms(boneTransforms);
                return;
            }

            // 如果进度为 1，使用目标变换
            if (Progress >= 1f)
            {
                _targetPlayer?.SampleBoneTransforms(boneTransforms);
                return;
            }

            // 混合源和目标变换
            var targetTransforms = new Matrix?[boneCount];
            _targetPlayer?.SampleBoneTransforms(targetTransforms);

            for (int i = 0; i < boneCount; i++)
            {
                if (targetTransforms[i].HasValue)
                {
                    if (_sourceTransforms[i].HasValue)
                    {
                        // 混合变换
                        boneTransforms[i] = BlendTransforms(
                            _sourceTransforms[i].Value,
                            targetTransforms[i].Value,
                            Progress);
                    }
                    else
                    {
                        boneTransforms[i] = targetTransforms[i].Value;
                    }
                }
                else if (_sourceTransforms[i].HasValue)
                {
                    // 从源变换淡出
                    boneTransforms[i] = BlendTransforms(
                        _sourceTransforms[i].Value,
                        Matrix.Identity,
                        Progress);
                }
            }
        }

        /// <summary>
        /// 完成过渡
        /// </summary>
        public void CompleteTransition()
        {
            _isActive = false;
            _sourcePlayer = null;
            _sourceTransforms = null;
        }

        /// <summary>
        /// 取消过渡
        /// </summary>
        public void CancelTransition()
        {
            _isActive = false;
            _sourcePlayer = null;
            _sourceTransforms = null;
            _targetPlayer = null;
        }

        /// <summary>
        /// 检查是否可以开始新过渡
        /// </summary>
        /// <param name="priority">新过渡的优先级</param>
        /// <returns>是否可以开始新过渡</returns>
        public bool CanStartNewTransition(int priority = 0)
        {
            if (!_isActive)
                return true;

            if (InterruptMode == TransitionInterruptMode.CannotInterrupt)
                return false;

            if (InterruptMode == TransitionInterruptMode.HigherPriorityOnly && priority <= _priority)
                return false;

            return true;
        }

        /// <summary>
        /// 确保源变换缓冲区大小足够
        /// </summary>
        private void EnsureSourceBufferSize(int requiredSize)
        {
            if (_sourceTransforms == null || _sourceTransforms.Length < requiredSize)
            {
                _sourceTransforms = new Matrix?[Math.Max(requiredSize, 64)];
            }
            Array.Clear(_sourceTransforms, 0, _sourceTransforms.Length);
        }

        /// <summary>
        /// 混合两个变换矩阵
        /// </summary>
        private Matrix BlendTransforms(Matrix a, Matrix b, float t)
        {
            // 分解为 T、R、S 分别插值
            DecomposeMatrix(a, out var tA, out var rA, out var sA);
            DecomposeMatrix(b, out var tB, out var rB, out var sB);

            return Matrix.CreateScale(Vector3.Lerp(sA, sB, t))
                 * Matrix.CreateFromQuaternion(Quaternion.Slerp(rA, rB, t))
                 * Matrix.CreateTranslation(Vector3.Lerp(tA, tB, t));
        }

        /// <summary>
        /// 分解矩阵为平移、旋转、缩放
        /// </summary>
        private void DecomposeMatrix(Matrix m, out Vector3 translation, out Quaternion rotation, out Vector3 scale)
        {
            // 提取平移
            translation = m.Translation;

            // 提取缩放
            Vector3 right = new Vector3(m.M11, m.M12, m.M13);
            Vector3 up = new Vector3(m.M21, m.M22, m.M23);
            Vector3 forward = new Vector3(m.M31, m.M32, m.M33);

            float scaleX = right.Length();
            float scaleY = up.Length();
            float scaleZ = forward.Length();
            scale = new Vector3(scaleX, scaleY, scaleZ);

            // 提取旋转
            if (scaleX != 0) right /= scaleX;
            if (scaleY != 0) up /= scaleY;
            if (scaleZ != 0) forward /= scaleZ;

            Matrix rotationMatrix = new Matrix(
                right.X, right.Y, right.Z, 0,
                up.X, up.Y, up.Z, 0,
                forward.X, forward.Y, forward.Z, 0,
                0, 0, 0, 1);

            rotation = Quaternion.CreateFromRotationMatrix(rotationMatrix);

            // 处理负缩放
            if (scaleX * scaleY * scaleZ < 0)
            {
                scale = -scale;
            }
        }
    }
}
