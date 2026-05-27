using Engine;
using Engine.Graphics;
using Silk.NET.OpenGLES;

namespace Game {
    public class ViewWidget : TouchInputWidget, IDragTargetWidget {
        public SubsystemDrawing m_subsystemDrawing;

        public RenderTarget2D m_scalingRenderTarget;
        public PrimitivesRenderer3D m_vrGuiPr3 = new();
        public Matrix? VrGuiQuadMatrix { get; private set; }

        public static RenderTarget2D ScreenTexture = new(Window.Size.X, Window.Size.Y, 1, ColorFormat.Rgba8888, DepthFormat.Depth24Stencil8);

        public GameWidget GameWidget { get; set; }

        public Point2? ScalingRenderTargetSize {
            get {
                if (m_scalingRenderTarget == null) {
                    return null;
                }
                return new Point2(m_scalingRenderTarget.Width, m_scalingRenderTarget.Height);
            }
        }

        public override void ChangeParent(ContainerWidget parentWidget) {
            if (parentWidget is GameWidget) {
                GameWidget = (GameWidget)parentWidget;
                m_subsystemDrawing = GameWidget.SubsystemGameWidgets.Project.FindSubsystem<SubsystemDrawing>(true);
                base.ChangeParent(parentWidget);
                return;
            }
            throw new InvalidOperationException("ViewWidget must be a child of GameWidget.");
        }

        public override void MeasureOverride(Vector2 parentAvailableSize) {
            IsDrawRequired = true;
            base.MeasureOverride(parentAvailableSize);
        }

        public override void Draw(DrawContext dc) {
            if (GameWidget.PlayerData.ComponentPlayer != null
                && GameWidget.PlayerData.IsReadyForPlaying) {
                DrawToScreen(dc);
            }
        }

        public override void Dispose() {
            base.Dispose();
            Utilities.Dispose(ref m_scalingRenderTarget);
        }

        public virtual void DragOver(Widget dragWidget, object data) { }

        public virtual void DragDrop(Widget dragWidget, object data) {
            if (data is InventoryDragData inventoryDragData
                && GameManager.Project != null) {
                SubsystemPickables subsystemPickables = GameManager.Project.FindSubsystem<SubsystemPickables>(true);
                ComponentPlayer componentPlayer = GameWidget.PlayerData.ComponentPlayer;
                int slotValue = inventoryDragData.Inventory.GetSlotValue(inventoryDragData.SlotIndex);
                int count = componentPlayer != null
                    && componentPlayer.ComponentInput.SplitSourceInventory == inventoryDragData.Inventory
                    && componentPlayer.ComponentInput.SplitSourceSlotIndex == inventoryDragData.SlotIndex ? 1 :
                    inventoryDragData.DragMode != DragMode.SingleItem ? inventoryDragData.Inventory.GetSlotCount(inventoryDragData.SlotIndex) :
                    MathUtils.Min(inventoryDragData.Inventory.GetSlotCount(inventoryDragData.SlotIndex), 1);
                int num = inventoryDragData.Inventory.RemoveSlotItems(inventoryDragData.SlotIndex, count);
                if (num > 0) {
                    Vector2 vector = dragWidget.WidgetToScreen(dragWidget.ActualSize / 2f);
                    Vector3 value = Vector3.Normalize(
                            GameWidget.ActiveCamera.ScreenToWorld(new Vector3(vector.X, vector.Y, 1f), Matrix.Identity)
                            - GameWidget.ActiveCamera.ViewPosition
                        )
                        * 12f;
                    subsystemPickables.AddPickable(slotValue, num, GameWidget.ActiveCamera.ViewPosition, value, null, componentPlayer.Entity);
                }
            }
        }

        public virtual void SetupScalingRenderTarget() {
            float num = SettingsManager.ResolutionMode == ResolutionMode.Low ? 0.5f : SettingsManager.ResolutionMode != ResolutionMode.Medium ? 1f : 0.75f;
            float num2 = GlobalTransform.Right.Length();
            float num3 = GlobalTransform.Up.Length();
            Vector2 vector = new(ActualSize.X * num2, ActualSize.Y * num3);
            Point2 point = default;
            point.X = (int)MathF.Round(vector.X * num);
            point.Y = (int)MathF.Round(vector.Y * num);
            Point2 point2 = point;
            if ((num < 1f || GlobalColorTransform != Color.White)
                && point2.X > 0
                && point2.Y > 0) {
                if (m_scalingRenderTarget == null
                    || m_scalingRenderTarget.Width != point2.X
                    || m_scalingRenderTarget.Height != point2.Y) {
                    Utilities.Dispose(ref m_scalingRenderTarget);
                    m_scalingRenderTarget = new RenderTarget2D(point2.X, point2.Y, 1, ColorFormat.Rgba8888, DepthFormat.Depth24Stencil8);
                }
                Display.RenderTarget = m_scalingRenderTarget;
                Display.Clear(Color.Black, 1f, 0);
            }
            else {
                Utilities.Dispose(ref m_scalingRenderTarget);
            }
        }

        public virtual void ApplyScalingRenderTarget(DrawContext dc) {
            if (m_scalingRenderTarget != null) {
                BlendState blendState = GlobalColorTransform.A < byte.MaxValue ? BlendState.AlphaBlend : BlendState.Opaque;
                TexturedBatch2D texturedBatch2D = dc.PrimitivesRenderer2D.TexturedBatch(
                    m_scalingRenderTarget,
                    false,
                    0,
                    DepthStencilState.None,
                    RasterizerState.CullNoneScissor,
                    blendState,
                    SamplerState.PointClamp
                );
                int count = texturedBatch2D.TriangleVertices.Count;
                texturedBatch2D.QueueQuad(Vector2.Zero, ActualSize, 0f, Vector2.Zero, Vector2.One, GlobalColorTransform);
                texturedBatch2D.TransformTriangles(GlobalTransform, count);
                dc.PrimitivesRenderer2D.Flush();
            }
        }

        public virtual void DrawToScreen(DrawContext dc) {
            if (VrManager.IsVrStarted
                && (Input.Devices & WidgetInputDevice.VrControllers) != WidgetInputDevice.None
                && VrManager.IsFrameActive
                && GameWidget.ActiveCamera is BasePerspectiveCamera camera) {
                DrawToScreenVr(camera);
                return;
            }
            GameWidget.GuiWidget.IsDrawEnabled = true;
            GameWidget.ActiveCamera.PrepareForDrawing(null);
            RenderTarget2D renderTarget = Display.RenderTarget;
            SetupScalingRenderTarget();
            try {
                m_subsystemDrawing.Draw(GameWidget.ActiveCamera);
            }
            finally {
                Display.RenderTarget = renderTarget;
            }
            ApplyScalingRenderTarget(dc);
            ModsManager.HookAction(
                "DrawToScreen",
                loader => {
                    loader.DrawToScreen(this, dc);
                    return false;
                }
            );
        }

        void DrawToScreenVr(BasePerspectiveCamera camera) {
            int vrW = VrManager.SwapchainWidth;
            int vrH = VrManager.SwapchainHeight;
            int desktopFbo = GLWrapper.m_mainFramebuffer;

            // GUI texture pre-rendered in GameWidget.ArrangeOverride.
            // GuiWidget.IsDrawEnabled is false (skipped in CollateDrawItems).

            VrManager.RenderToEyes((vrEye, eyeFrame) => {
                    camera.PrepareForDrawing(vrEye);
                    m_subsystemDrawing.Draw(camera);

                    if (GameWidget.m_vrGuiRenderTarget == null) {
                        return;
                    }

                    // Compute GUI quad from HMD (same technique as VR menu in ScreensManager)
                    Matrix hmd = VrManager.HmdMatrix;
                    Vector3 hmdFwd = hmd.Forward * new Vector3(1f, 0f, 1f);
                    if (hmdFwd.LengthSquared() < 0.001f) return;

                    float dist = 6f;
                    Vector3 center = hmd.Translation + dist * (Vector3.Normalize(hmdFwd) + new Vector3(0f, 0.1f, 0f));
                    Vector2 size = new(GameWidget.m_vrGuiRenderTarget.Width / (float)GameWidget.m_vrGuiRenderTarget.Height, 1f);
                    size /= MathUtils.Max(size.X, size.Y);
                    size *= 7.5f;
                    Vector3 faceDir = Vector3.Normalize(hmd.Translation - center);
                    Vector3 qRight = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, faceDir)) * size.X;
                    Vector3 qUp = Vector3.Normalize(Vector3.Cross(faceDir, qRight)) * size.Y;
                    Vector3 corner = center - 0.5f * qRight - 0.5f * qUp;

                    // Store quad matrix for VR cursor hit testing (used next frame)
                    VrGuiQuadMatrix = new Matrix { Translation = corner, Right = qRight, Up = qUp, Forward = faceDir };

                    // Draw GUI texture as 3D quad
                    TexturedBatch3D guiBatch = m_vrGuiPr3.TexturedBatch(
                        GameWidget.m_vrGuiRenderTarget,
                        false,
                        0,
                        DepthStencilState.None,
                        RasterizerState.CullNoneScissor,
                        BlendState.AlphaBlend,
                        SamplerState.LinearClamp
                    );
                    ScreensManager.QueueQuad(guiBatch, corner, qRight, qUp, Color.White);
                    m_vrGuiPr3.Flush(eyeFrame.ViewMatrix * eyeFrame.ProjectionMatrix);

                    // Blit to desktop before swapchain is released
                    if (vrEye == VrEye.Left) {
                        BlitVrEyeToDesktop(eyeFrame.Fbo, desktopFbo, vrW, vrH);
                    }
                }
            );

            ModsManager.HookAction(
                "DrawToScreen",
                loader => {
                    loader.DrawToScreen(this, null);
                    return false;
                }
            );
        }

        static void BlitVrEyeToDesktop(int srcFbo, int dstFbo, int vrW, int vrH) {
            if (srcFbo == 0) return;
            Point2 winSize = Display.BackbufferSize;
            float vrAspect = (float)vrW / vrH;
            float winAspect = (float)winSize.X / winSize.Y;
            int drawW, drawH, offsetX, offsetY;
            if (winAspect > vrAspect) {
                drawH = winSize.Y;
                drawW = (int)(winSize.Y * vrAspect);
                offsetX = (winSize.X - drawW) / 2;
                offsetY = 0;
            }
            else {
                drawW = winSize.X;
                drawH = (int)(winSize.X / vrAspect);
                offsetX = 0;
                offsetY = (winSize.Y - drawH) / 2;
            }
            GLWrapper.GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, (uint)dstFbo);
            GLWrapper.ClearColor(Vector4.Zero);
            GLWrapper.ApplyViewportScissor(
                new Viewport(0, 0, winSize.X, winSize.Y),
                new Rectangle(0, 0, winSize.X, winSize.Y), true);
            GLWrapper.GL.Clear(ClearBufferMask.ColorBufferBit);
            GLWrapper.GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, (uint)srcFbo);
            GLWrapper.GL.BlitFramebuffer(
                0, 0, vrW, vrH,
                offsetX, offsetY, offsetX + drawW, offsetY + drawH,
                ClearBufferMask.ColorBufferBit,
                BlitFramebufferFilter.Linear);
            GLWrapper.GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, (uint)dstFbo);
        }
    }
}
