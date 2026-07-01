using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Engine;

namespace Game {
    /// <summary>
    /// 一个设置项 = 一行 UI（标题 + 值控件）。描述走页面共享 label。
    /// 纯代码组装，轮询 Update 检测交互。子类负责 Value↔控件互转 + 检测交互 + 触发 ValueChanged。
    /// </summary>
    // 继承 StackPanelWidget 而非 ContainerWidget：ContainerWidget.MeasureOverride 只 measure 子不汇总尺寸，
    // 自身 DesiredSize 保持 (Inf,Inf)，父容器按零尺寸排列导致整行不可见。StackPanel 会汇总子尺寸设 DesiredSize。
    public abstract class SettingsItemWidget : StackPanelWidget {
        public ModSettingItem Descriptor { get; }
        public object Value { get; protected set; }
        public string DescriptionText { get; set; }
        public Action<object> ValueChanged;

        protected LabelWidget m_nameLabel;
        string m_nameText;

        protected SettingsItemWidget(ModSettingItem descriptor, object currentValue) {
            Descriptor = descriptor;
            Value = currentValue;
            Direction = LayoutDirection.Horizontal;
        }

        /// <summary>Factory 实例化后设值，触发 nameLabel 更新。</summary>
        public string NameText {
            get => m_nameText;
            set {
                m_nameText = value;
                if (m_nameLabel != null) m_nameLabel.Text = value;
            }
        }

        /// <summary>子类判定是否支持某值类型（enum Widget 用 typeof(Enum).IsAssignableFrom）。</summary>
        public abstract bool Supports(Type type);

        /// <summary>页面轮询识别激活项（如滑块滑动中），用于更新共享 Description。默认 false。</summary>
        public virtual bool IsPressed => false;

        /// <summary>子类构造时调用：建 nameLabel + 值控件，水平排进自身（已是 Horizontal StackPanel）。</summary>
        protected void Assemble(Widget valueWidget) {
            m_nameLabel = new LabelWidget {
                Text = m_nameText,
                VerticalAlignment = WidgetAlignment.Center
            };
            Children.Add(m_nameLabel);
            if (valueWidget != null) Children.Add(valueWidget);
        }

        /// <summary>子类检测到值变化时调用：更新 Value 并触发 ValueChanged（→ Manager.Set）。</summary>
        protected void CommitValue(object newValue) {
            Value = newValue;
            ValueChanged?.Invoke(newValue);
        }

        /// <summary>从描述符 CachedPath 取 packageName（path 首段），用于 enum 成员本地化。</summary>
        protected static string ExtractPackageName(ModSettingItem d) =>
            d.CachedPath != null && d.CachedPath.Contains('/') ? d.CachedPath.Substring(0, d.CachedPath.IndexOf('/')) : null;
    }

    // ===== 内置子类 =====

    /// <summary>bool 开关按钮。Text = Value ? TextTrue : TextFalse（默认 Yes/No，可配 Enable/Disable）。</summary>
    public class BoolButtonWidget : SettingsItemWidget {
        BevelledButtonWidget m_button;
        string m_textTrue, m_textFalse;

        public BoolButtonWidget(ModSettingItem descriptor, object currentValue) : base(descriptor, currentValue) {
            m_textTrue = LanguageControl.Yes;
            m_textFalse = LanguageControl.No;
            if (descriptor.WidgetProperties is JsonElement props) {
                if (props.TryGetProperty("TextTrue", out JsonElement tt) && tt.ValueKind == JsonValueKind.String) m_textTrue = tt.GetString();
                if (props.TryGetProperty("TextFalse", out JsonElement tf) && tf.ValueKind == JsonValueKind.String) m_textFalse = tf.GetString();
            }
            m_button = new BevelledButtonWidget {
                Style = ContentManager.Get<XElement>("Styles/ButtonStyle_310x60"),
                Text = (bool)Value ? m_textTrue : m_textFalse,
                HorizontalAlignment = WidgetAlignment.Far,
                VerticalAlignment = WidgetAlignment.Center
            };
            Assemble(m_button);
        }

        public override bool Supports(Type type) => type == typeof(bool);

        public override void Update() {
            base.Update();
            if (m_button.IsClicked) {
                CommitValue(!(bool)Value);
                m_button.Text = (bool)Value ? m_textTrue : m_textFalse;
            }
        }
    }

    /// <summary>enum 多项选择对话框（项数多时默认）。点按钮弹 ListSelectionDialog。</summary>
    public class EnumSelectionDialogWidget : SettingsItemWidget {
        BevelledButtonWidget m_button;
        readonly Array m_members;
        readonly string m_packageName;

        public EnumSelectionDialogWidget(ModSettingItem descriptor, object currentValue) : base(descriptor, currentValue) {
            m_packageName = ExtractPackageName(descriptor);
            m_members = Enum.GetValues(descriptor.Type);
            m_button = new BevelledButtonWidget {
                Style = ContentManager.Get<XElement>("Styles/ButtonStyle_310x60"),
                Text = MemberText(Value),
                HorizontalAlignment = WidgetAlignment.Far,
                VerticalAlignment = WidgetAlignment.Center
            };
            Assemble(m_button);
        }

        public override bool Supports(Type type) => typeof(Enum).IsAssignableFrom(type);

        string MemberText(object value) => ModSettingLocalizer.GetEnumMemberText(m_packageName, Descriptor.Type, value);

        public override void Update() {
            base.Update();
            if (m_button.IsClicked) {
                DialogsManager.ShowDialog(null, new ListSelectionDialog(
                    NameText, m_members, 60f,
                    item => new LabelWidget { Text = MemberText(item), HorizontalAlignment = WidgetAlignment.Center },
                    item => { CommitValue(item); m_button.Text = MemberText(item); }
                ));
            }
        }
    }

    /// <summary>enum 少项滑块（SliderWidget + 整数下标）。</summary>
    public class EnumSliderWidget : SettingsItemWidget {
        SliderWidget m_slider;
        readonly Array m_members;
        readonly string m_packageName;

        public EnumSliderWidget(ModSettingItem descriptor, object currentValue) : base(descriptor, currentValue) {
            m_packageName = ExtractPackageName(descriptor);
            m_members = Enum.GetValues(descriptor.Type);
            m_slider = new SliderWidget {
                MinValue = 0,
                MaxValue = Math.Max(1, m_members.Length - 1),
                Granularity = 1,
                Value = Array.IndexOf(m_members, Value)
            };
            if (descriptor.WidgetProperties is JsonElement props) {
                if (props.TryGetProperty("Granularity", out JsonElement g) && g.ValueKind == JsonValueKind.Number) m_slider.Granularity = g.GetSingle();
            }
            m_slider.Text = MemberText(Value);
            Assemble(m_slider);
        }

        public override bool Supports(Type type) => typeof(Enum).IsAssignableFrom(type);
        public override bool IsPressed => m_slider.IsSliding;

        string MemberText(object value) => ModSettingLocalizer.GetEnumMemberText(m_packageName, Descriptor.Type, value);

        public override void Update() {
            base.Update();
            if (m_slider.IsSliding || m_slider.SlidingCompleted) {
                int idx = Math.Clamp((int)Math.Round(m_slider.Value), 0, m_members.Length - 1);
                object current = m_members.GetValue(idx);
                m_slider.Text = MemberText(current);
                if (m_slider.SlidingCompleted) CommitValue(current);
            }
        }
    }

    /// <summary>数值滑块（int/float 等）。需 WidgetProperties.MinValue/MaxValue，缺则降级 0~1。</summary>
    public class NumberSliderWidget : SettingsItemWidget {
        SliderWidget m_slider;

        public NumberSliderWidget(ModSettingItem descriptor, object currentValue) : base(descriptor, currentValue) {
            m_slider = new SliderWidget {
                MinValue = 0,
                MaxValue = 1,
                Granularity = 0.1f,
                Value = ToFloat(Value)
            };
            if (descriptor.WidgetProperties is JsonElement props) {
                if (props.TryGetProperty("MinValue", out JsonElement min) && min.ValueKind == JsonValueKind.Number) m_slider.MinValue = min.GetSingle();
                if (props.TryGetProperty("MaxValue", out JsonElement max) && max.ValueKind == JsonValueKind.Number) m_slider.MaxValue = max.GetSingle();
                if (props.TryGetProperty("Granularity", out JsonElement g) && g.ValueKind == JsonValueKind.Number) m_slider.Granularity = g.GetSingle();
            }
            UpdateText();
            Assemble(m_slider);
        }

        public override bool Supports(Type type) => IsNumeric(type);
        public override bool IsPressed => m_slider.IsSliding;

        static bool IsNumeric(Type t) =>
            t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
            || t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) || t == typeof(sbyte)
            || t == typeof(float) || t == typeof(double) || t == typeof(decimal);

        static float ToFloat(object v) => Convert.ToSingle(v, CultureInfo.InvariantCulture);
        void UpdateText() => m_slider.Text = Value?.ToString() ?? "";

        public override void Update() {
            base.Update();
            if (m_slider.IsSliding) m_slider.Text = m_slider.Value.ToString("0.###", CultureInfo.InvariantCulture);
            if (m_slider.SlidingCompleted) {
                object newVal = Convert.ChangeType(m_slider.Value, Descriptor.Type, CultureInfo.InvariantCulture);
                CommitValue(newVal);
                UpdateText();
            }
        }
    }

    /// <summary>文本输入（TextBoxWidget）。</summary>
    public class TextItemWidget : SettingsItemWidget {
        TextBoxWidget m_textBox;

        public TextItemWidget(ModSettingItem descriptor, object currentValue) : base(descriptor, currentValue) {
            m_textBox = new TextBoxWidget { Text = Value as string ?? "" };
            Assemble(m_textBox);
        }

        public override bool Supports(Type type) => type == typeof(string);

        public override void Update() {
            base.Update();
            string current = Value as string;
            if (m_textBox.Text != current) CommitValue(m_textBox.Text);
        }
    }
}
