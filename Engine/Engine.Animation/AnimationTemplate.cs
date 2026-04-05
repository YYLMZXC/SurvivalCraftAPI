#nullable disable

namespace Engine.Animation
{
    /// <summary>
    /// 动画模板，定义生物类型的动画特性
    /// </summary>
    public class AnimationTemplate
    {
        public string Name { get; }
        public LayerDefinition[] Layers { get; }
        public StateTrackDefinition[] StateTracks { get; }
        public string[] RequiredBones { get; }

        public AnimationTemplate(
            string name,
            LayerDefinition[] layers,
            StateTrackDefinition[] stateTracks,
            string[] requiredBones = null)
        {
            Name = name;
            Layers = layers ?? new LayerDefinition[0];
            StateTracks = stateTracks ?? new StateTrackDefinition[0];
            RequiredBones = requiredBones ?? new string[0];
        }
    }
}
