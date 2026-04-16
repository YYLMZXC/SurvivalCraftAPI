using System.Numerics;

namespace Engine.Media {
    /// <summary>
    /// 材质纹理封装，包含纹理索引、UV索引和UV变换
    /// 用于 KHR_texture_transform 扩展支持
    /// </summary>
    public class ModelMaterialTexture {
        /// <summary>
        /// 纹理索引（对应 ModelData.Textures 列表）
        /// -1 表示无纹理
        /// </summary>
        public int TextureIndex { get; set; } = -1;

        /// <summary>
        /// UV 通道索引（TEXCOORD_0、TEXCOORD_1 等）
        /// </summary>
        public int UVIndex { get; set; }

        /// <summary>
        /// UV 变换矩阵 (3x2)，用于 KHR_texture_transform 扩展
        /// </summary>
        public Matrix3x2 UVTransform { get; set; } = Matrix3x2.Identity;

        /// <summary>
        /// 是否有非默认的 UV 变换（计算属性，基于 Offset/Scale/Rotation）
        /// </summary>
        public bool HasUVTransform => Offset != Vector2.Zero || Scale != Vector2.One || Rotation != 0f;

        /// <summary>
        /// UV 偏移量（用于动画）
        /// </summary>
        public Vector2 Offset { get; set; } = Vector2.Zero;

        /// <summary>
        /// UV 缩放（用于动画）
        /// </summary>
        public Vector2 Scale { get; set; } = Vector2.One;

        /// <summary>
        /// UV 旋转角度（弧度，用于动画）
        /// </summary>
        public float Rotation { get; set; }

        /// <summary>
        /// 是否有有效纹理
        /// </summary>
        public bool HasTexture => TextureIndex >= 0;

        public ModelMaterialTexture() { }

        public ModelMaterialTexture(int textureIndex, int uvIndex = 0) {
            TextureIndex = textureIndex;
            UVIndex = uvIndex;
        }

        /// <summary>
        /// 从 glTF TextureTransform 参数创建 UV 变换矩阵
        /// glTF 规范要求变换顺序：Scale -> Rotation -> Translation
        /// </summary>
        public static Matrix3x2 CreateUVTransform(Vector2 offset, Vector2 scale, float rotation) {
            // glTF UV transform 变换顺序：Scale -> Rotation -> Translation
            // 参考：https://github.com/KhronosGroup/glTF/tree/main/extensions/2.0/Khronos/KHR_texture_transform
            Matrix3x2 scaleMatrix = Matrix3x2.CreateScale(scale.X, scale.Y);
            Matrix3x2 rotationMatrix = Matrix3x2.CreateRotation(rotation);
            Matrix3x2 translationMatrix = Matrix3x2.CreateTranslation(offset.X, offset.Y);

            // 对于行向量约定，矩阵乘法顺序是：translationMatrix * rotationMatrix * scaleMatrix
            return translationMatrix * rotationMatrix * scaleMatrix;
        }

        /// <summary>
        /// 重新计算 UV 变换矩阵（动画更新后调用）
        /// </summary>
        public void RecomputeUVTransform() {
            UVTransform = CreateUVTransform(Offset, Scale, Rotation);
        }

        /// <summary>
        /// 设置 UV 变换参数并重新计算矩阵
        /// </summary>
        public void SetTransform(Vector2 offset, Vector2 scale, float rotation) {
            Offset = offset;
            Scale = scale;
            Rotation = rotation;
            RecomputeUVTransform();
        }

        /// <summary>
        /// 从纹理索引和变换参数创建
        /// </summary>
        public static ModelMaterialTexture Create(int textureIndex, int uvIndex, Vector2 offset, Vector2 scale, float rotation) {
            ModelMaterialTexture tex = new(textureIndex, uvIndex);
            tex.SetTransform(offset, scale, rotation);
            return tex;
        }
    }
}
