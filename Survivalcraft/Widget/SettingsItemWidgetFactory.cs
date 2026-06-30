using Engine;

namespace Game {
    /// <summary>
    /// 按 descriptor.WidgetType 实例化 SettingsItemWidget。
    /// 实例化后查 Supports(Type)，不满足回退默认 Widget；默认也不满足返回 null（Screen 跳过该项 + Log）。
    /// </summary>
    public static class SettingsItemWidgetFactory {
        public static SettingsItemWidget Create(ModSettingItem descriptor, object currentValue, string nameText, string descriptionText) {
            SettingsItemWidget widget = TryCreate(descriptor.WidgetType, descriptor, currentValue);
            if (widget != null && widget.Supports(descriptor.Type)) {
                ApplyText(widget, nameText, descriptionText);
                return widget;
            }
            Type defaultType = ModSettingsParser.GetDefaultWidgetType(descriptor.Type);
            if (defaultType != null) {
                SettingsItemWidget fallback = TryCreate(defaultType, descriptor, currentValue);
                if (fallback != null && fallback.Supports(descriptor.Type)) {
                    Log.Error($"[ModSettings] 设置项 {descriptor.Id} 的 Widget 回退默认 {defaultType.Name}");
                    ApplyText(fallback, nameText, descriptionText);
                    return fallback;
                }
            }
            Log.Error($"[ModSettings] 设置项 {descriptor.Id} 无可用 Widget（类型 {descriptor.Type}），跳过");
            return null;
        }

        static SettingsItemWidget TryCreate(Type type, ModSettingItem descriptor, object currentValue) {
            if (type == null) return null;
            try { return Activator.CreateInstance(type, descriptor, currentValue) as SettingsItemWidget; }
            catch (Exception e) { Log.Error($"[ModSettings] Widget {type.Name} 实例化失败：{e.Message}"); return null; }
        }

        static void ApplyText(SettingsItemWidget widget, string name, string desc) {
            widget.NameText = name;
            widget.DescriptionText = desc;
        }
    }
}
