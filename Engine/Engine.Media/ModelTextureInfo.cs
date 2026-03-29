using SharpGLTF.Memory;

namespace Engine.Media {
    /// <summary>
    /// 纹理类型枚举
    /// </summary>
    public enum ModelTextureType {
        None,
        BaseColor,      // sRGB
        Normal,         // Linear
        MetallicRoughness, // Linear
        Occlusion,      // Linear
        Emissive        // sRGB
    }

    /// <summary>
    /// 纹理信息（支持延迟加载）
    /// </summary>
    public class ModelTextureInfo {
        /// <summary>
        /// 纹理名称
        /// </summary>
        public string Name;

        /// <summary>
        /// 纹理类型
        /// </summary>
        public ModelTextureType Type;

        /// <summary>
        /// 是否为 sRGB 颜色空间
        /// </summary>
        public bool IsSrgb;

        /// <summary>
        /// 原始图像数据（用于延迟加载）
        /// </summary>
        public MemoryImage SourceImage;

        /// <summary>
        /// 已解码的像素数据（加载后填充）
        /// </summary>
        public byte[] Pixels;

        /// <summary>
        /// 纹理宽度
        /// </summary>
        public int Width;

        /// <summary>
        /// 纹理高度
        /// </summary>
        public int Height;

        /// <summary>
        /// 是否已加载
        /// </summary>
        public bool IsLoaded => Pixels != null;

        /// <summary>
        /// 加载纹理数据（延迟调用）
        /// </summary>
        public void Load() {
            if (IsLoaded || SourceImage.IsEmpty) {
                return;
            }

            try {
                using var stream = SourceImage.Open();
                var img = Image.Load(stream);
                Width = img.Width;
                Height = img.Height;

                // 将 Color[] 转换为 byte[]
                var colors = img.Pixels;
                Pixels = new byte[colors.Length * 4];
                for (int i = 0; i < colors.Length; i++) {
                    int offset = i * 4;
                    Pixels[offset] = colors[i].R;
                    Pixels[offset + 1] = colors[i].G;
                    Pixels[offset + 2] = colors[i].B;
                    Pixels[offset + 3] = colors[i].A;
                }
            }
            catch (System.Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ModelTextureInfo] Failed to load texture: {ex.Message}");
            }
        }
    }
}
