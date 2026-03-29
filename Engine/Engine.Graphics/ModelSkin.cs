#nullable disable

namespace Engine.Graphics {
    /// <summary>
    /// 蒙皮数据，存储骨骼索引和权重
    /// </summary>
    public class ModelSkin {
        /// <summary>
        /// 关节骨骼列表（引用 Model.Bones）
        /// </summary>
        public List<ModelBone> Joints { get; set; } = [];

        /// <summary>
        /// 逆绑定矩阵，每个关节一个
        /// </summary>
        public Matrix[] InverseBindMatrices { get; set; }

        /// <summary>
        /// 根骨骼（骨骼层级根节点）
        /// </summary>
        public ModelBone SkeletonRoot { get; set; }

        /// <summary>
        /// 关节数量
        /// </summary>
        public int JointCount => Joints.Count;
    }
}
