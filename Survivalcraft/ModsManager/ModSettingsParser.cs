using System.Text.Json;
using Engine;
using Engine.Serialization;

namespace Game {
    /// <summary>
    /// JSON → 设置描述符。容错：缺字段/解析失败降级 + Log，不禁用模组。
    /// 类型/值转换薄封装，复用 HumanReadableConverter，不重写转换逻辑。
    /// </summary>
    public static class ModSettingsParser {
        /// <summary>解析顶层 Settings 数组 → ModSettingPage 列表。失败返回空列表。</summary>
        public static List<ModSettingPage> ParseSettings(JsonElement settingsArray, string packageName) {
            List<ModSettingPage> result = new();
            if (settingsArray.ValueKind != JsonValueKind.Array) return result;
            foreach (JsonElement element in settingsArray.EnumerateArray()) {
                if (ParseElement(element, new List<string>(), packageName) is ModSettingPage page)
                    result.Add(page);
            }
            return result;
        }

        /// <summary>按值类型选默认 Widget。返回 null 表示无默认（需模组自定义）。</summary>
        public static Type GetDefaultWidgetType(Type valueType) {
            if (valueType == typeof(bool)) return typeof(BoolButtonWidget);
            if (typeof(Enum).IsAssignableFrom(valueType)) return typeof(EnumSelectionDialogWidget);
            if (IsNumeric(valueType)) return typeof(NumberSliderWidget);
            if (valueType == typeof(string)) return typeof(TextItemWidget);
            return null;
        }

        static ModSettingElement ParseElement(JsonElement obj, List<string> idChain, string packageName) {
            if (obj.ValueKind != JsonValueKind.Object) return null;
            bool hasItems = obj.TryGetProperty("Items", out JsonElement itemsEl) && itemsEl.ValueKind == JsonValueKind.Array;
            bool hasType = obj.TryGetProperty("Type", out JsonElement typeEl) && typeEl.ValueKind == JsonValueKind.String;
            bool isSeparator = obj.TryGetProperty("Separator", out JsonElement sepEl) && sepEl.ValueKind == JsonValueKind.True;
            bool hasText = obj.TryGetProperty("Text", out JsonElement textEl) && textEl.ValueKind == JsonValueKind.String;

            if (isSeparator) return new ModSettingSeparator();
            if (hasItems) return ParsePage(obj, itemsEl, packageName);
            if (hasType) return ParseItem(obj, typeEl);
            if (hasText) return new ModSettingLabel { Text = textEl.GetString() };
            return null;
        }

        static ModSettingPage ParsePage(JsonElement obj, JsonElement itemsEl, string packageName) {
            string id = GetString(obj, "Id");
            if (!IsValidId(id)) { Log.Error($"[ModSettings] 模组 {packageName} 的 Page 缺少合法 Id 或含 '/'，跳过"); return null; }
            ModSettingPage page = new() {
                Id = id,
                Name = GetString(obj, "Name"),
                Title = GetString(obj, "Title"),
                Description = GetString(obj, "Description")
            };
            foreach (JsonElement child in itemsEl.EnumerateArray()) {
                if (ParseElement(child, new List<string>(), packageName) is ModSettingElement el)
                    page.Items.Add(el);
            }
            return page;
        }

        static ModSettingItem ParseItem(JsonElement obj, JsonElement typeEl) {
            string id = GetString(obj, "Id");
            if (!IsValidId(id)) { Log.Error($"[ModSettings] 设置项缺少合法 Id 或含 '/'，跳过"); return null; }
            string typeStr = typeEl.GetString();
            Type type = ResolveType(typeStr);
            if (type == null) { Log.Error($"[ModSettings] 设置项 {id} 的 Type '{typeStr}' 无法解析，跳过"); return null; }

            ModSettingItem item = new() {
                Id = id,
                Name = GetString(obj, "Name"),
                Description = GetString(obj, "Description"),
                Type = type,
                Default = ResolveDefault(obj, type),
                WidgetType = ResolveWidget(GetString(obj, "Widget"), type)
            };
            if (obj.TryGetProperty("WidgetProperties", out JsonElement propsEl) && propsEl.ValueKind == JsonValueKind.Object)
                item.WidgetProperties = propsEl.Clone(); // Clone 脱离 JsonDocument 生命周期
            return item;
        }

        static bool IsValidId(string id) => !string.IsNullOrEmpty(id) && !id.Contains('/');

        static Type ResolveType(string typeStr) {
            if (string.IsNullOrEmpty(typeStr)) return null;
            return typeStr == "bool" ? typeof(bool) : TypeCache.FindType(typeStr, true, false);
        }

        static object ResolveDefault(JsonElement obj, Type type) {
            if (!obj.TryGetProperty("Default", out JsonElement defEl)) return GetDefaultOf(type);
            try { return ConvertValue(type, defEl); }
            catch (Exception e) { Log.Error($"[ModSettings] Default 解析失败，用 default({type.Name})：{e.Message}"); return GetDefaultOf(type); }
        }

        static object GetDefaultOf(Type type) {
            if (type == typeof(bool)) return false;
            if (type.IsValueType) return Activator.CreateInstance(type); // 数值=0，enum=首项
            return null;
        }

        /// <summary>JSON 值 → 字符串表示 → HumanReadableConverter 转值。</summary>
        internal static object ConvertValue(Type type, JsonElement valueEl) {
            string s = ValueElementToString(valueEl);
            return HumanReadableConverter.ConvertFromString(type, s);
        }

        static string ValueElementToString(JsonElement el) {
            return el.ValueKind switch {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.True => "True",
                JsonValueKind.False => "False",
                JsonValueKind.Number => el.GetRawText(),
                _ => el.GetRawText()
            };
        }

        /// <summary>内置别名/类名直接映射 typeof；模组自定义全限定名走 FindType。</summary>
        static Type ResolveWidget(string widgetStr, Type valueType) {
            if (!string.IsNullOrEmpty(widgetStr)) {
                switch (widgetStr) {
                    case "bool":
                    case "BoolButtonWidget": return typeof(BoolButtonWidget);
                    case "enum-dialog":
                    case "EnumSelectionDialogWidget": return typeof(EnumSelectionDialogWidget);
                    case "enum-slider":
                    case "EnumSliderWidget": return typeof(EnumSliderWidget);
                    case "number-slider":
                    case "NumberSliderWidget": return typeof(NumberSliderWidget);
                    case "text":
                    case "TextItemWidget": return typeof(TextItemWidget);
                }
                // 模组自定义 Widget：按全限定名查
                Type t = TypeCache.FindType(widgetStr, true, false);
                if (t != null && t.IsSubclassOf(typeof(SettingsItemWidget))) return t;
                Log.Error($"[ModSettings] Widget '{widgetStr}' 未找到或非 SettingsItemWidget 子类，回退默认");
            }
            return GetDefaultWidgetType(valueType); // 缺省/不合法 → 类型默认
        }

        static bool IsNumeric(Type t) {
            return t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
                || t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) || t == typeof(sbyte)
                || t == typeof(float) || t == typeof(double) || t == typeof(decimal);
        }

        static string GetString(JsonElement obj, string name) =>
            obj.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    }
}
