using System.Xml.Linq;
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;

public class GameWidget : CanvasWidget {
    public List<Camera> m_cameras = new();
    public Dictionary<Camera, Func<GameWidget, bool>> m_isCameraEnable = new();

    public Camera m_activeCamera;
    public RenderTarget2D m_vrGuiRenderTarget;
    public PrimitivesRenderer2D m_vrCursorRenderer;
    public PrimitivesRenderer3D m_vrGuiPr3 = new();
    public Matrix? VrGuiQuadMatrix { get; set; }

    public CanvasWidget m_vrGuiRoot;
    public bool m_vrGuiActive;
    public WidgetInput m_vrWidgetInput;

    public ViewWidget ViewWidget { get; set; }

    public ContainerWidget GuiWidget { get; set; }

    public int GameWidgetIndex { get; set; }

    public SubsystemGameWidgets SubsystemGameWidgets { get; set; }

    public PlayerData PlayerData { get; set; }

    public ReadOnlyList<Camera> Cameras => new(m_cameras);

    public Camera ActiveCamera {
        get => m_activeCamera;
        set {
            if (value == null
                || value.GameWidget != this) {
                throw new InvalidOperationException("Invalid camera.");
            }
            if (!IsCameraAllowed(value)) {
                value = FindCamera<FppCamera>();
            }
            if (value != m_activeCamera) {
                Camera activeCamera = m_activeCamera;
                m_activeCamera = value;
                m_activeCamera.Activate(activeCamera);
            }
        }
    }

    public ComponentCreature Target { get; set; }

    public GameWidget(PlayerData playerData, int gameViewIndex) {
        PlayerData = playerData;
        GameWidgetIndex = gameViewIndex;
        SubsystemGameWidgets = playerData.SubsystemGameWidgets;
        LoadContents(this, ContentManager.Get<XElement>("Widgets/GameWidget"));
        ViewWidget = Children.Find<ViewWidget>("View");
        GuiWidget = Children.Find<ContainerWidget>("Gui");
        AddCamera(new FppCamera(this));
        AddCamera(new DeathCamera(this));
        AddCamera(new IntroCamera(this));
        AddCamera(new TppCamera(this));
        AddCamera(new OrbitCamera(this));
        AddCamera(new FixedCamera(this));
        AddCamera(new LoadingCamera(this));
        ModsManager.HookAction(
            "ManageCameras",
            modLoader => {
                modLoader.ManageCameras(this);
                return false;
            }
        );
        List<KeyValuePair<string, int>> list = ModSettingsManager.CombinedCameraManageSettings.OrderBy(x => x.Value).ToList();
        int num = 0;
        foreach (KeyValuePair<string, int> item in list) {
            string name = item.Key;
            int value = item.Value;
            if (value >= 0) { //刷新列表时重新按顺序分配值，避免出现空缺
                SettingsManager.SetCameraManageSetting(name, num);
                num++;
            }
        }
        m_activeCamera = FindCamera<LoadingCamera>();
    }

    public T FindCamera<T>(bool throwOnError = true) where T : Camera {
        T val = (T)m_cameras.FirstOrDefault(c => c is T);
        if (val != null
            || !throwOnError) {
            return val;
        }
        throw new InvalidOperationException($"Camera with type \"{typeof(T).Name}\" not found.");
    }

    public Camera FindCamera(Type type, bool throwOnError = true) {
        Camera val = m_cameras.FirstOrDefault(c => c.GetType() == type);
        if (val != null
            || !throwOnError) {
            return val;
        }
        throw new InvalidOperationException($"Camera with type \"{type.Name}\" not found.");
    }

    /// <summary>
    /// </summary>
    /// <param name="type"></param>
    /// <param name="isEnable">用于判定当前摄像机是否可用，比如在非创造模式中调试视角不可用</param>
    /// <param name="throwOnError"></param>
    /// <returns></returns>
    public Camera FindCamera(Type type, out bool isEnable, bool throwOnError = true) {
        isEnable = true;
        Camera result = FindCamera(type, throwOnError);
        if (m_isCameraEnable.TryGetValue(result, out Func<GameWidget, bool> func)) {
            isEnable = func?.Invoke(this) ?? true;
        }
        return result;
    }

    /// <summary>
    ///     此方法建议在ModLoader.ManageCameras接口中使用，避免重复添加。若无需结合条件判断摄像机是否可用(比如调试视角仅创造模式可用)，则isEnable可传null
    /// </summary>
    /// <param name="camera"></param>
    /// <param name="isEnable">在Func中进行判断，若输出false，则表示该摄像机目前不可用</param>
    public void AddCamera(Camera camera, Func<GameWidget, bool> isEnable = null) {
        if (camera == null) {
            return;
        }
        m_cameras.Add(camera);
        if (isEnable != null) {
            m_isCameraEnable.Add(camera, isEnable);
        }
    }

    public bool IsEntityTarget(Entity entity) {
        if (Target != null) {
            return Target.Entity == entity;
        }
        return false;
    }

    public bool IsEntityFirstPersonTarget(Entity entity) {
        if (IsEntityTarget(entity)) {
            return ActiveCamera is FppCamera;
        }
        return false;
    }

    public void AttachGuiToVrRoot() {
        if (m_vrGuiActive) return;
        m_vrGuiRoot ??= new CanvasWidget();
        Children.Remove(GuiWidget);
        m_vrGuiRoot.Children.Add(GuiWidget);
        m_vrGuiActive = true;
    }

    public void DetachGuiFromVrRoot() {
        if (!m_vrGuiActive) return;
        m_vrGuiRoot.Children.Remove(GuiWidget);
        Children.InsertAfter(ViewWidget, GuiWidget);
        GuiWidget.WidgetsHierarchyInput = null;
        m_vrGuiActive = false;
        m_vrWidgetInput = null;
    }

    public override void Update() {
        WidgetInputDevice widgetInputDevice = DetermineInputDevices();
        bool isVrPlayer = VrManager.IsFrameActive
            && (widgetInputDevice & WidgetInputDevice.VrControllers) != WidgetInputDevice.None;

        if (isVrPlayer && !m_vrGuiActive) {
            AttachGuiToVrRoot();
        }
        else if (!isVrPlayer && m_vrGuiActive) {
            DetachGuiFromVrRoot();
        }

        if (m_vrGuiActive) {
            WidgetInputDevice vrDevices = widgetInputDevice & ~(WidgetInputDevice.Mouse | WidgetInputDevice.MultiMice | WidgetInputDevice.Touch);
            if (m_vrWidgetInput == null || m_vrWidgetInput.Devices != vrDevices) {
                m_vrWidgetInput = new WidgetInput(vrDevices);
                GuiWidget.WidgetsHierarchyInput = m_vrWidgetInput;
            }
            if (VrGuiQuadMatrix.HasValue) {
                m_vrWidgetInput.VrQuadMatrix = VrGuiQuadMatrix;
            }
            else {
                m_vrWidgetInput.VrQuadMatrix = null;
            }
            UpdateWidgetsHierarchy(GuiWidget);
            // Dialog hide may trigger DetachGuiFromVrRoot which clears m_vrWidgetInput
            if (m_vrWidgetInput == null) return;
            // Sync Back to GameWidget's input
            // ComponentGui reads GameWidget.WidgetsHierarchyInput
            if (WidgetsHierarchyInput == null
                || WidgetsHierarchyInput.Devices != vrDevices) {
                WidgetsHierarchyInput = new WidgetInput(vrDevices);
            }
            WidgetsHierarchyInput.Back = m_vrWidgetInput.Back;
        }
        else {
            if (WidgetsHierarchyInput == null
                || WidgetsHierarchyInput.Devices != widgetInputDevice) {
                WidgetsHierarchyInput = new WidgetInput(widgetInputDevice);
            }
            if ((widgetInputDevice & WidgetInputDevice.VrControllers) != WidgetInputDevice.None
                && VrManager.IsFrameActive && VrGuiQuadMatrix.HasValue) {
                WidgetsHierarchyInput.VrQuadMatrix = VrGuiQuadMatrix;
            }
            else {
                WidgetsHierarchyInput.VrQuadMatrix = null;
            }
            WidgetsHierarchyInput.UseSoftMouseCursor = (widgetInputDevice & WidgetInputDevice.MultiMice) != WidgetInputDevice.None
                && (widgetInputDevice & WidgetInputDevice.Mouse) == WidgetInputDevice.None;
            if (GuiWidget.ParentWidget == null) {
                UpdateWidgetsHierarchy(GuiWidget);
            }
        }
    }

    public override void ArrangeOverride() {
        base.ArrangeOverride();
        if (m_vrGuiActive) {
            GuiWidget.MarginLeft = 0f;
            GuiWidget.MarginTop = 0f;
            GuiWidget.MarginRight = 0f;
            GuiWidget.MarginBottom = 0f;
            float num = 850f / Math.Clamp(SettingsManager.UIScale, 0.5f, 1.2f) * ScreensManager.DebugUiScale;
            Vector2 availableSize = new(num, num * 9f / 16f);
            float vrScale = 1280f / num;
            m_vrGuiRoot.LayoutTransform = Matrix.CreateScale(vrScale, vrScale, 1);
            m_vrGuiRoot.Measure(availableSize);
            m_vrGuiRoot.Arrange(Vector2.Zero, availableSize);
            RenderGuiToTexture();
        }
        else {
            GuiWidget.IsDrawEnabled = true;
        }
    }

    public WidgetInputDevice DetermineInputDevices() {
        bool flag = false;
        foreach (PlayerData playersDatum in PlayerData.SubsystemPlayers.PlayersData) {
            if ((playersDatum.InputDevice & WidgetInputDevice.MultiMice) != 0) {
                flag = true;
            }
        }
        WidgetInputDevice widgetInputDevice = WidgetInputDevice.None;
        foreach (WidgetInputDevice allInputDevice in PlayerScreen.AllInputDevices) {
            if (!flag
                || allInputDevice != (WidgetInputDevice.Keyboard | WidgetInputDevice.Mouse)) {
                widgetInputDevice |= allInputDevice;
            }
        }
        if (PlayerData.SubsystemPlayers.PlayersData.Count > 0
            && PlayerData == PlayerData.SubsystemPlayers.PlayersData[0]) {
            WidgetInputDevice widgetInputDevice2 = WidgetInputDevice.None;
            foreach (PlayerData playersDatum2 in PlayerData.SubsystemPlayers.PlayersData) {
                if (playersDatum2 != PlayerData) {
                    widgetInputDevice2 |= playersDatum2.InputDevice;
                }
            }
            return (widgetInputDevice & ~widgetInputDevice2) | WidgetInputDevice.Touch | PlayerData.InputDevice;
        }
        WidgetInputDevice widgetInputDevice3 = WidgetInputDevice.None;
        foreach (PlayerData playersDatum3 in PlayerData.SubsystemPlayers.PlayersData) {
            if (playersDatum3 == PlayerData) {
                break;
            }
            widgetInputDevice3 |= playersDatum3.InputDevice;
        }
        return (PlayerData.InputDevice & ~widgetInputDevice3) | WidgetInputDevice.Touch;
    }

    public bool IsCameraAllowed(Camera camera) {
        if (camera is LoadingCamera) {
            return false;
        }
        return true;
    }

    public void RenderGuiToTexture() {
        if (m_vrWidgetInput != null && WidgetsHierarchyInput != null) {
            m_vrWidgetInput.IsVrCursorVisible = WidgetsHierarchyInput.IsVrCursorVisible;
        }
        if (m_vrGuiRenderTarget == null) {
            m_vrGuiRenderTarget = new RenderTarget2D(1280, 720, 1, ColorFormat.Rgba8888, DepthFormat.Depth24Stencil8);
        }
        m_vrCursorRenderer ??= new PrimitivesRenderer2D();
        RenderTarget2D prevRT = Display.RenderTarget;
        Display.RenderTarget = m_vrGuiRenderTarget;
        Display.Clear(Color.Transparent, 1f, 0);
        DrawWidgetsHierarchy(m_vrGuiRoot);

        if (m_vrWidgetInput != null
            && m_vrWidgetInput.IsVrCursorVisible
            && m_vrWidgetInput.VrCursorLocalPosition.HasValue) {
            Vector2 screenPos = Vector2.Transform(
                m_vrWidgetInput.VrCursorLocalPosition.Value,
                GuiWidget.GlobalTransform
            );
            m_vrCursorRenderer.FlatBatch(0, null, null, null).QueueDisc(
                screenPos, new Vector2(10f, 10f), 0f, Color.White);
            m_vrCursorRenderer.Flush();
        }

        Display.RenderTarget = prevRT;
    }

    public void DrawVrGui(VrEye vrEye, EyeFrame eyeFrame) {
        if (m_vrGuiRenderTarget == null) return;

        Matrix hmd = VrManager.HmdMatrix;
        Vector3 hmdFwd = hmd.Forward * new Vector3(1f, 0f, 1f);
        if (hmdFwd.LengthSquared() < 0.001f) return;

        float dist = 1.5f;
        Vector3 center = hmd.Translation + dist * Vector3.Normalize(hmdFwd) + new Vector3(0f, 0.025f, 0f);
        float width = 1.24f;
        float aspect = m_vrGuiRenderTarget.Width / (float)m_vrGuiRenderTarget.Height;
        Vector2 size = new(width, width / aspect);
        Vector3 faceDir = Vector3.Normalize(hmd.Translation - center);
        Vector3 qRight = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, faceDir)) * size.X;
        Vector3 qUp = Vector3.Normalize(Vector3.Cross(faceDir, qRight)) * size.Y;
        Vector3 corner = center - 0.5f * qRight - 0.5f * qUp;

        VrGuiQuadMatrix = new Matrix { Translation = corner, Right = qRight, Up = qUp, Forward = faceDir };

        TexturedBatch3D guiBatch = m_vrGuiPr3.TexturedBatch(
            m_vrGuiRenderTarget,
            false,
            0,
            DepthStencilState.None,
            RasterizerState.CullNoneScissor,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp
        );
        ScreensManager.QueueQuad(guiBatch, corner, qRight, qUp, Color.White);
        m_vrGuiPr3.Flush(eyeFrame.ViewMatrix * eyeFrame.ProjectionMatrix);
    }

    public override void Dispose() {
        if (m_vrGuiActive) {
            DetachGuiFromVrRoot();
        }
        base.Dispose();
        Utilities.Dispose(ref m_vrGuiRenderTarget);
        m_vrGuiRoot?.Dispose();
    }
}