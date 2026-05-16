namespace Engine.Graphics {
    public enum ColorFormat {
        // === 未压缩格式 ===
        Rgba8888,       // Linear RGBA8
        Rgba8888Srgb,   // sRGB + Alpha
        Rgba5551,
        Rgb565,
        R8,

        // === HDR 未压缩格式 ===
        R32f,           // Single channel float
        RG32f,          // Two channel float
        RGBA32f,        // Four channel float
        Rgba16f,        // Four channel half float (HDR 常用)

        // === ASTC 压缩格式 (仅 CompressedTexture2D) ===
        LinearLDR,      // Linear ASTC
        SrgbLDR         // sRGB ASTC
    }
}