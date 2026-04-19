using System;
using System.Collections.Generic;
using System.Numerics;
using Engine.Media;
using Silk.NET.OpenGLES;

namespace Engine.Graphics {
    /// <summary>
    /// 高级网格渲染器基类
    /// 模组开发者继承此类实现自定义渲染（PBR、卡通渲染等）
    /// </summary>
    /// <remarks>
    /// 基类管理通用 UBO：Scene、RenderState、Lights、UVTransform
    /// 子类管理材质相关 UBO（如 PBR 的 MaterialCore、MaterialExtension）
    ///
    /// 子类需要实现：
    /// - LoadShaderSources(): 加载 .vert, .frag, .glsl 文件
    /// - SetupShaderCallbacks(): 设置 Attribute/UBO 绑定回调
    /// - CreateShaderVariant(): 构建着色器变体
    /// </remarks>
    public abstract class AdvancedMeshRenderer : IDisposable {
        // 通用 UBO 实例（基类管理）
        protected UniformBuffer<SceneData> SceneUBO;
        protected UniformBuffer<LightsData> LightsUBO;
        protected UniformBuffer<RenderStateData> RenderStateUBO;
        protected UniformBuffer<UVTransformData> UVTransformUBO;

        // 渲染状态
        protected RenderStateData RenderStateData;
        protected RenderContext CurrentContext;

        // 材质缓存状态（子类可访问）
        protected ModelMaterial LastMaterial;
        protected int LastExtensionFlags;
        protected bool UvTransformDirty = true;

        // 帧级光照数据（逐模型缩放时使用）
        Vector3 _baseLightColor;
        Vector3 _viewLightDir;

        // 缓存优化
        int _cachedContextHash;
        (bool useIBL, bool useLinearOutput, ToneMapMode toneMapMode, int lightCount, DebugChannel debugChannel) _lastContextParams;

        // Uniform location 缓存
        readonly Dictionary<int, int> _jointSamplerLocationCache = [];
        protected readonly Dictionary<int, int> _glymulLocationCache = [];

        /// <summary>
        /// 当前视图投影矩阵
        /// </summary>
        public Matrix4x4 CurrentViewProjection { get; private set; }

        /// <summary>
        /// IBL 环境贴图强度（默认 1.0）
        /// </summary>
        public float EnvironmentStrength { get; set; } = 1.0f;

        /// <summary>
        /// IBL mipmap 层数
        /// </summary>
        public int MipCount { get; set; }

        /// <summary>
        /// 纹理覆盖（用于 DAE 等非 glTF 模型的 TextureOverride）
        /// 每帧渲染前设置，渲染后清除
        /// </summary>
        public Texture2D TextureOverride { get; set; }

        /// <summary>
        /// 逐模型光照强度（来自地形方块光照计算）
        /// </summary>
        public float ModelLightIntensity { get; set; } = 1f;

        protected AdvancedMeshRenderer() {
            // 创建通用 UBO
            SceneUBO = new(0);
            LightsUBO = new(2);
            RenderStateUBO = new(3);
            UVTransformUBO = new(4);

            // 初始化 ShaderCache
            ShaderCache.Initialize();

            // 调用抽象方法让子类设置
            LoadShaderSources();
            SetupShaderCallbacks();
        }

        /// <summary>
        /// 加载着色器源码（由模组实现）
        /// </summary>
        protected abstract void LoadShaderSources();

        /// <summary>
        /// 设置 Attribute/UBO 绑定回调（由模组实现）
        /// </summary>
        protected abstract void SetupShaderCallbacks();

        /// <summary>
        /// 创建着色器变体（由模组实现）
        /// </summary>
        protected abstract Shader CreateShaderVariant(ModelMesh mesh, ModelMaterial material, in RenderContext context);

        /// <summary>
        /// 开始帧渲染
        /// </summary>
        public virtual void BeginFrame(in RenderContext context) {
            CurrentContext = context;
            UpdateContextHash(context);

            // 更新视图投影矩阵
            CurrentViewProjection = context.View * context.Projection;

            // 更新 SceneData UBO
            // SC 引擎的 ModelMatrix 含 ViewMatrix（AbsoluteBoneTransformsForCamera），
            // 所以 v_Position 和法线在 view space。
            // CameraPos 设为 view space 原点 (0,0,0)，使 v = normalize(-v_Position) 正确。
            // EnvRotation = transpose(mat3(CameraView))，将 view space 向量变换回 world space 采样 IBL。
            Matrix4x4 cameraView = context.CameraView;

            SceneData sceneData = new() {
                CameraPos = new Vector4(0f, 0f, 0f, 1f),
                Exposure = 1f,
                EnvironmentStrength = EnvironmentStrength,
                MipCount = MipCount,
                EnvRotationCol0 = new Vector4(cameraView.M11, cameraView.M21, cameraView.M31, 0f),
                EnvRotationCol1 = new Vector4(cameraView.M12, cameraView.M22, cameraView.M32, 0f),
                EnvRotationCol2 = new Vector4(cameraView.M13, cameraView.M23, cameraView.M33, 0f)
            };
            SceneUBO.Update(ref sceneData);

            // 方向光：使用 CameraView 将世界空间光照方向变换到 view space
            _viewLightDir = Vector3.Normalize(Vector3.TransformNormal(context.LightDirection, cameraView));
            _baseLightColor = context.LightColor;

            UpdateLightsUBO(1f);

            // 重置材质缓存
            LastMaterial = null;
            UvTransformDirty = true;
        }

        /// <summary>
        /// 骨骼纹理纹理槽
        /// 注意：MaterialTextureSlot.MorphTargets = 30，JointTexture 使用 slot 31 避免冲突
        /// </summary>
        protected const int JointTextureSlot = 31;

        /// <summary>
        /// 渲染网格
        /// </summary>
        public virtual void Render(ModelMesh mesh, ModelMaterial material, Matrix4x4 wvpMatrix, Matrix4x4 worldMatrix, Model model, JointTexture jointTexture = null) {
            if (mesh == null) return;

            // 获取或创建着色器
            Shader shader = GetOrCreateShader(mesh, material, CurrentContext);
            if (shader == null) {
                Engine.Log.Error("AdvancedMeshRenderer.Render: shader is null, skipping draw");
                return;
            }

            shader.PrepareForDrawing();

            // 绑定着色器程序（通过 GLWrapper 封装以保持缓存同步）
            GLWrapper.UseProgram(shader.m_program);

            // 上传 u_glymul uniform（缓存 location 避免每帧查询）
            int programHandle = shader.m_program;
            if (!_glymulLocationCache.TryGetValue(programHandle, out int glymulLoc)) {
                glymulLoc = GLWrapper.GL.GetUniformLocation((uint)programHandle, "u_glymul");
                _glymulLocationCache[programHandle] = glymulLoc;
            }
            if (glymulLoc >= 0) {
                float glymul = Display.RenderTarget != null ? -1f : 1f;
                GLWrapper.GL.Uniform1(glymulLoc, glymul);
            }

            // 更新 RenderState UBO
            UpdateRenderStateUBO(wvpMatrix, worldMatrix);

            // 更新光照 UBO（逐模型强度缩放）
            UpdateLightsUBO(ModelLightIntensity);

            // 更新 UV 变换 UBO
            UpdateUVTransformUBO(material);

            // 绑定纹理
            if (model != null && material != null) {
                BindMaterialTextures(model, material, shader);
            }

            // 绑定骨骼纹理
            if (jointTexture != null) {
                BindJointTexture(jointTexture, shader);
            }

            // 设置深度状态
            SetupDepthState(material);

            // 设置剔除模式
            SetupCullMode(material);

            // 设置混合模式
            SetupBlendMode(material, CurrentContext);

            // 绘制
            DrawMesh(mesh);
        }

        /// <summary>
        /// 获取或创建着色器变体
        /// </summary>
        protected virtual Shader GetOrCreateShader(ModelMesh mesh, ModelMaterial material, in RenderContext context) {
            // 计算材质 hash（子类可重写以优化）
            int materialHash = ComputeMaterialHash(material);
            int contextHash = CachedContextHash;

            // 尝试从缓存获取
            Shader shader = ShaderCache.TryGetShaderProgram(materialHash, contextHash);
            if (shader != null) return shader;

            // 创建新的着色器变体
            return CreateShaderVariant(mesh, material, context);
        }

        /// <summary>
        /// 计算材质 hash（子类可重写以优化）
        /// </summary>
        protected virtual int ComputeMaterialHash(ModelMaterial material) {
            unchecked {
                int hash = 17;
                if (material != null) {
                    hash = hash * 31 + material.AlphaMode.GetHashCode();
                    hash = hash * 31 + material.DoubleSided.GetHashCode();
                    hash = hash * 31 + (int)MaterialUboBuilder.BuildExtensionFlags(material);
                    hash = hash * 31 + (int)MaterialUboBuilder.BuildTextureFlags(material);
                }
                if (TextureOverride != null) {
                    hash = hash * 31 + "__TEX_OVERRIDE__".GetHashCode();
                }
                return hash;
            }
        }

        /// <summary>
        /// 更新 RenderState UBO
        /// wvpMatrix: 预组合的 WVP（用于 gl_Position）
        /// worldMatrix: 世界矩阵，已含 ViewMatrix（用于 v_Position、法线变换）
        /// </summary>
        protected void UpdateRenderStateUBO(Matrix4x4 wvpMatrix, Matrix4x4 worldMatrix) {
            RenderStateData.ViewProjectionMatrix = wvpMatrix;
            RenderStateData.ModelMatrix = worldMatrix;
            RenderStateData.ViewMatrix = CurrentContext.View;
            RenderStateData.ProjectionMatrix = CurrentContext.Projection;

            // 计算法线矩阵 = transpose(inverse(worldMatrix))
            if (System.Numerics.Matrix4x4.Invert(worldMatrix, out Matrix4x4 invModel)) {
                RenderStateData.NormalMatrix = System.Numerics.Matrix4x4.Transpose(invModel);
            } else {
                RenderStateData.NormalMatrix = Matrix4x4.Identity;
            }

            RenderStateUBO.Update(ref RenderStateData);
        }

        /// <summary>
        /// 更新光照 UBO（逐模型光照强度缩放）
        /// </summary>
        protected void UpdateLightsUBO(float intensity) {
            LightsData lightsData = new() {
                LightCount = 1
            };
            lightsData.Light0 = new LightData {
                Direction = _viewLightDir,
                Color = _baseLightColor * intensity,
                Intensity = 1f,
                Type = 0
            };
            LightsUBO.Update(ref lightsData);
        }

        /// <summary>
        /// 更新 UV 变换 UBO（懒更新）
        /// </summary>
        protected void UpdateUVTransformUBO(ModelMaterial material) {
            if (!UvTransformDirty) return;

            UVTransformData uvTransformData = MaterialUboBuilder.BuildUVTransformData(material);
            UVTransformUBO.Update(ref uvTransformData);
            UvTransformDirty = false;
        }

        /// <summary>
        /// 设置深度测试（确保自定义渲染参与深度遮挡）
        /// </summary>
        protected virtual void SetupDepthState(ModelMaterial material) {
            GLWrapper.Enable(EnableCap.DepthTest);
            GLWrapper.DepthFunc(DepthFunction.Lequal);
            GLWrapper.DepthMask(true);
        }

        /// <summary>
        /// 设置剔除模式
        /// </summary>
        protected virtual void SetupCullMode(ModelMaterial material) {
            if (material?.DoubleSided == true) {
                GLWrapper.Disable(EnableCap.CullFace);
            }
            else {
                GLWrapper.Enable(EnableCap.CullFace);
                GLWrapper.CullFace(TriangleFace.Back);
                GLWrapper.FrontFace(Display.RenderTarget != null
                    ? FrontFaceDirection.Ccw
                    : FrontFaceDirection.CW);
            }
        }

        /// <summary>
        /// 设置混合模式
        /// </summary>
        protected virtual void SetupBlendMode(ModelMaterial material, in RenderContext context) {
            ModelAlphaMode alphaMode = material?.AlphaMode ?? ModelAlphaMode.Opaque;

            if (alphaMode == ModelAlphaMode.Blend) {
                GLWrapper.Enable(EnableCap.Blend);
                GLWrapper.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            }
            else {
                GLWrapper.Disable(EnableCap.Blend);
            }
        }

        /// <summary>
        /// 绘制网格
        /// </summary>
        protected virtual void DrawMesh(ModelMesh mesh) {
            if (mesh == null) return;

            foreach (ModelMeshPart part in mesh.MeshParts) {
                DrawMeshPart(part);
            }
        }

        /// <summary>
        /// 绘制网格部件
        /// </summary>
        protected virtual void DrawMeshPart(ModelMeshPart part) {
            if (part?.VertexBuffer == null || part.IndexBuffer == null) return;

            // 绑定顶点缓冲
            GLWrapper.BindBuffer(BufferTargetARB.ArrayBuffer, part.VertexBuffer.m_buffer);
            GLWrapper.BindBuffer(BufferTargetARB.ElementArrayBuffer, part.IndexBuffer.m_buffer);

            // 设置顶点属性
            SetupVertexAttributes(part.VertexBuffer.VertexDeclaration);

            // 绘制
            unsafe {
                IntPtr indexOffset = new IntPtr(part.StartIndex * part.IndexBuffer.IndexFormat.GetSize());
                GLWrapper.GL.DrawElements(
                    Silk.NET.OpenGLES.PrimitiveType.Triangles,
                    (uint)part.IndicesCount,
                    GLWrapper.TranslateIndexFormat(part.IndexBuffer.IndexFormat),
                    indexOffset.ToPointer()
                );
            }
        }

        /// <summary>
        /// 设置顶点属性
        /// 将 VertexDeclaration 中的 semantic 映射到着色器的 attribute location
        /// </summary>
        protected virtual void SetupVertexAttributes(VertexDeclaration declaration) {
            if (declaration == null) return;

            // 禁用所有 attribute（最多 8 个）
            for (int i = 0; i < 8; i++) {
                GLWrapper.VertexAttribArray(i, false);
            }

            // 遍历 vertex elements，映射到 attribute locations
            foreach (VertexElement element in declaration.VertexElements) {
                int location = SemanticToLocation(element.Semantic);
                if (location < 0) continue;

                GLWrapper.TranslateVertexElementFormat(element.Format,
                    out VertexAttribPointerType type, out bool normalize);

                int size = element.Format.GetElementsCount();
                int stride = declaration.VertexStride;

                unsafe {
                    GLWrapper.GL.VertexAttribPointer(
                        (uint)location,
                        size,
                        type,
                        normalize,
                        (uint)stride,
                        new IntPtr(element.Offset).ToPointer()
                    );
                }
                GLWrapper.VertexAttribArray(location, true);
            }
        }

        /// <summary>
        /// 将 vertex semantic 字符串映射到着色器 attribute location
        /// </summary>
        protected static int SemanticToLocation(string semantic) {
            return semantic switch {
                "POSITION" => 0,
                "NORMAL" => 1,
                "TEXCOORD" => 2,
                "TEXCOORD0" => 2,
                "TEXCOORD1" => 3,
                "TEXCOORD2" => -1,
                "COLOR" => 4,
                "TANGENT" => 5,
                "BLENDINDICES" => 6,
                "BLENDWEIGHTS" => 7,
                _ => -1
            };
        }

        void UpdateContextHash(in RenderContext context) {
            var contextParams = (context.UseIBL, context.UseLinearOutput, context.ToneMapMode, context.LightCount, context.DebugChannel);
            if (_lastContextParams == contextParams) return;

            _lastContextParams = contextParams;
            _cachedContextHash = ComputeContextHash(context);
        }

        /// <summary>
        /// 计算渲染上下文的 defines hash
        /// </summary>
        protected static int ComputeContextHash(in RenderContext context) {
            unchecked {
                int hash = 17;
                if (context.UseIBL) hash = hash * 31 + "USE_IBL 1".GetHashCode();
                if (context.LightCount > 0) hash = hash * 31 + "USE_PUNCTUAL 1".GetHashCode();
                if (context.UseLinearOutput) {
                    hash = hash * 31 + "LINEAR_OUTPUT 1".GetHashCode();
                }
                else {
                    string tonemapDefine = context.ToneMapMode switch {
                        ToneMapMode.KhrPbrNeutral => "TONEMAP_KHR_PBR_NEUTRAL 1",
                        ToneMapMode.AcesNarkowicz => "TONEMAP_ACES_NARKOWICZ 1",
                        ToneMapMode.AcesHill => "TONEMAP_ACES_HILL 1",
                        ToneMapMode.AcesHillExposureBoost => "TONEMAP_ACES_HILL_EXPOSURE_BOOST 1",
                        _ => "LINEAR_OUTPUT 1"
                    };
                    hash = hash * 31 + tonemapDefine.GetHashCode();
                }
                if (context.DebugChannel != DebugChannel.None) {
                    hash = hash * 31 + $"DEBUG {(int)context.DebugChannel}".GetHashCode();
                }
                return hash;
            }
        }

        /// <summary>
        /// 获取缓存的上下文 hash
        /// </summary>
        protected int CachedContextHash => _cachedContextHash;

        /// <summary>
        /// 绑定 Uniform Block
        /// </summary>
        protected static void BindUniformBlock(uint programHandle, string blockName, uint bindingPoint) {
            uint blockIndex = GLWrapper.GL.GetUniformBlockIndex(programHandle, blockName);
            if (blockIndex != uint.MaxValue) {
                GLWrapper.GL.UniformBlockBinding(programHandle, blockIndex, bindingPoint);
            }
        }

        /// <summary>
        /// 绑定材质纹理（从 Model 延迟加载）
        /// </summary>
        protected virtual void BindMaterialTextures(Model model, ModelMaterial material, Shader shader) {
            // TextureOverride：DAE 等非 glTF 模型使用 ComponentModel.TextureOverride
            if (TextureOverride != null) {
                MaterialTextureBinder.BindTexture2D(TextureOverride, MaterialTextureSlot.BaseColor);
                MaterialTextureBinder.SetTextureSlotUniforms(shader);
                return;
            }

            int textureCount = model.ModelData?.Textures.Count ?? 0;
            if (textureCount == 0) return;

            Texture2D[] textures = new Texture2D[textureCount];
            for (int i = 0; i < textureCount; i++) {
                textures[i] = model.GetTexture(i);
            }

            MaterialTextureBinder.BindMaterialTextures(material, textures);
            MaterialTextureBinder.SetTextureSlotUniforms(shader);
        }

        /// <summary>
        /// 绑定骨骼纹理到指定纹理槽并设置 shader uniform
        /// </summary>
        /// <param name="jointTexture">骨骼矩阵纹理</param>
        /// <param name="shader">使用该纹理的着色器</param>
        protected virtual void BindJointTexture(JointTexture jointTexture, Shader shader) {
            jointTexture.Bind(JointTextureSlot);

            int programHandle = shader.m_program;
            if (!_jointSamplerLocationCache.TryGetValue(programHandle, out int location)) {
                location = GLWrapper.GL.GetUniformLocation((uint)programHandle, "u_jointsSampler");
                _jointSamplerLocationCache[programHandle] = location;
            }

            if (location >= 0) {
                GLWrapper.GL.Uniform1(location, JointTextureSlot);
            }
        }

        public virtual void Dispose() {
            SceneUBO?.Dispose();
            LightsUBO?.Dispose();
            RenderStateUBO?.Dispose();
            UVTransformUBO?.Dispose();
            _jointSamplerLocationCache.Clear();
            _glymulLocationCache.Clear();
        }
    }
}
