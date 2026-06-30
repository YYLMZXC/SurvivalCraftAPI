namespace Game {
    /// <summary>
    /// 一个设置项 = 一行 UI（标题 + 值控件）。描述走页面共享 label。
    /// 纯代码组装，轮询 Update 检测交互。子类负责 Value↔控件互转 + 检测交互 + 触发 ValueChanged。
    /// </summary>
    public abstract class SettingsItemWidget : ContainerWidget {
        public ModSettingItem Descriptor { get; }
        public object Value { get; protected set; }
        public string DescriptionText { get; set; }
        public Action<object> ValueChanged;

        protected LabelWidget m_nameLabel;
        string m_nameText;

        protected SettingsItemWidget(ModSettingItem descriptor, object currentValue) {
            Descriptor = descriptor;
            Value = currentValue;
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

        /// <summary>子类构造时调用：建 nameLabel + 横向排布值控件，组装进垂直 StackPanel。</summary>
        protected void Assemble(Widget valueWidget) {
            m_nameLabel = new LabelWidget {
                Text = m_nameText,
                HorizontalAlignment = WidgetAlignment.Near,
                VerticalAlignment = WidgetAlignment.Center
            };
            UniformSpacingPanelWidget row = new UniformSpacingPanelWidget { Direction = LayoutDirection.Horizontal };
            row.Children.Add(m_nameLabel);
            if (valueWidget != null) row.Children.Add(valueWidget);
            StackPanelWidget column = new StackPanelWidget { Direction = LayoutDirection.Vertical };
            column.Children.Add(row);
            Children.Add(column);
        }

        /// <summary>子类检测到值变化时调用：更新 Value 并触发 ValueChanged（→ Manager.Set）。</summary>
        protected void CommitValue(object newValue) {
            Value = newValue;
            ValueChanged?.Invoke(newValue);
        }
    }
}
