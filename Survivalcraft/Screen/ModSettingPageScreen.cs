using System.Xml.Linq;
using Engine;

namespace Game {
    /// <summary>
    /// 通用递归设置页面。root 聚合所有模组顶层 Page；非 root 渲染指定页面 Items。
    /// 单实例复用：每次 Enter(params) 重建内容；内部页面栈导航规避单实例 SwitchScreen 同实例重入限制。
    /// </summary>
    public class ModSettingPageScreen : Screen {
        LabelWidget m_titleLabel;
        StackPanelWidget m_contentStack;
        LabelWidget m_descriptionLabel;

        string m_packageName;
        string[] m_pageIds;
        ModSettingPage m_currentPage;

        // 栈空 = root；非空 peek 为当前页
        readonly Stack<(string PackageName, string[] PageIds, string Title)> m_pageStack = new();
        readonly Dictionary<BevelledButtonWidget, (string PackageName, string[] PageIds, string Title)> m_navButtons = new();
        readonly List<IModSettingItemWidget> m_itemWidgets = new();

        public override void Enter(object[] parameters) {
            XElement node = ContentManager.Get<XElement>("Screens/ModSettingPageScreen");
            LoadContents(this, node);
            m_titleLabel = Children.Find<LabelWidget>("TopBar.Label");
            m_contentStack = Children.Find<StackPanelWidget>("ContentStack");
            m_descriptionLabel = Children.Find<LabelWidget>("Description");
            m_pageStack.Clear();
            NavigateCurrent();
        }

        void NavigateCurrent() {
            m_contentStack.Children.Clear();
            m_navButtons.Clear();
            m_itemWidgets.Clear();
            if (m_pageStack.Count == 0)
                BuildRoot();
            else {
                (string PackageName, string[] PageIds, string Title) top = m_pageStack.Peek();
                m_packageName = top.PackageName;
                m_pageIds = top.PageIds;
                m_titleLabel.Text = top.Title;
                BuildPage();
            }
        }

        void BuildRoot() {
            m_titleLabel.Text = LanguageControl.Get("ModSettings", "RootTitle");
            foreach (KeyValuePair<string, ModSettingPage> entry in ModSettingsManager.GetRootEntries()) {
                ModSettingPage page = entry.Value;
                string[] pageIds = { page.Id };
                string name = ModSettingLocalizer.ResolveText(entry.Key, pageIds, "Name", page.Name, true);
                string title = ModSettingLocalizer.ResolveText(entry.Key, pageIds, "Title", page.Title, true);
                AddNavButton(name, entry.Key, pageIds, title);
            }
            m_descriptionLabel.Text = LanguageControl.Get("ModSettings", "RootDescription");
            m_currentPage = null;
        }

        void BuildPage() {
            m_currentPage = FindPage(ModSettingsManager.GetPages(m_packageName), m_pageIds);
            if (m_currentPage == null) return;
            m_descriptionLabel.Text = ModSettingLocalizer.ResolveText(m_packageName, m_pageIds, "Description", m_currentPage.Description, false);
            foreach (ModSettingElement el in m_currentPage.Items) {
                if (CreateElementWidget(el) is Widget w)
                    m_contentStack.Children.Add(w);
            }
        }

        void AddNavButton(string text, string packageName, string[] pageIds, string title) {
            BevelledButtonWidget btn = new() {
                Style = ContentManager.Get<XElement>("Styles/ButtonStyle_310x60"),
                Text = text,
                HorizontalAlignment = WidgetAlignment.Center,
                VerticalAlignment = WidgetAlignment.Center,
                Margin = new Vector2(0f, 5f)
            };
            m_navButtons[btn] = (packageName, pageIds, title);
            m_contentStack.Children.Add(btn);
        }

        Widget CreateElementWidget(ModSettingElement el) {
            switch (el) {
                case ModSettingLabel label:
                    return new LabelWidget {
                        Text = ModSettingLocalizer.ResolveText(m_packageName, m_pageIds, "Text", label.Text, true),
                        HorizontalAlignment = WidgetAlignment.Near,
                        Color = new Color(200, 200, 200),
                        Margin = new Vector2(0f, 8f)
                    };
                case ModSettingSeparator:
                    return new RectangleWidget {
                        Size = new Vector2(300f, 2f),
                        FillColor = new Color(80, 80, 80),
                        OutlineColor = Color.Transparent,
                        HorizontalAlignment = WidgetAlignment.Center,
                        Margin = new Vector2(0f, 6f)
                    };
                case ModSettingPage subPage: {
                    string[] subIds = AppendId(m_pageIds, subPage.Id);
                    string name = ModSettingLocalizer.ResolveText(m_packageName, subIds, "Name", subPage.Name, true);
                    string title = ModSettingLocalizer.ResolveText(m_packageName, subIds, "Title", subPage.Title, true);
                    BevelledButtonWidget btn = new() {
                        Style = ContentManager.Get<XElement>("Styles/ButtonStyle_310x60"),
                        Text = name,
                        HorizontalAlignment = WidgetAlignment.Center,
                        VerticalAlignment = WidgetAlignment.Center,
                        Margin = new Vector2(0f, 5f)
                    };
                    m_navButtons[btn] = (m_packageName, subIds, title);
                    return btn;
                }
                case ModSettingItem item: {
                    string[] itemChain = AppendId(m_pageIds, item.Id);
                    string name = ModSettingLocalizer.ResolveText(m_packageName, itemChain, "Name", item.Name, true);
                    string desc = ModSettingLocalizer.ResolveText(m_packageName, itemChain, "Description", item.Description, false);
                    object current = ModSettingsManager.GetValue(BuildPath(m_packageName, itemChain));
                    IModSettingItemWidget w = SettingsItemWidgetFactory.Create(item, current, name, desc);
                    if (w == null) return null;
                    if (w is not Widget widget) {
                        if (!LanguageControl.TryGet(out string msg, nameof(ModSettingPageScreen), "WidgetNotWidget")) msg = "Setting '{0}' widget '{1}' does not derive from Widget, cannot render, skipped.";
                        Log.Error("[ModSettings] " + string.Format(msg, item.Id, w.GetType().Name));
                        return null;
                    }
                    string[] fullPath = BuildPath(m_packageName, itemChain);
                    w.ValueChanged = v => ModSettingsManager.Set(fullPath, v);
                    m_itemWidgets.Add(w);
                    return widget;
                }
            }
            return null;
        }

        static string[] AppendId(string[] ids, string id) {
            string[] result = new string[ids.Length + 1];
            ids.CopyTo(result, 0);
            result[ids.Length] = id;
            return result;
        }

        static string[] BuildPath(string packageName, string[] idChain) {
            string[] path = new string[idChain.Length + 1];
            path[0] = packageName;
            idChain.CopyTo(path, 1);
            return path;
        }

        static ModSettingPage FindPage(List<ModSettingPage> pages, string[] pageIds) {
            List<ModSettingPage> level = pages;
            ModSettingPage current = null;
            for (int i = 0; i < pageIds.Length; i++) {
                current = level?.Find(p => p.Id == pageIds[i]);
                if (current == null) return null;
                level = new List<ModSettingPage>();
                foreach (ModSettingElement el in current.Items)
                    if (el is ModSettingPage sub) level.Add(sub);
            }
            return current;
        }

        public override void Update() {
            base.Update();
            foreach (KeyValuePair<BevelledButtonWidget, (string PackageName, string[] PageIds, string Title)> nav in m_navButtons) {
                if (nav.Key.IsClicked) {
                    m_pageStack.Push(nav.Value);
                    NavigateCurrent();
                    return;
                }
            }
            // 共享 Description：激活项（如滑块滑动）显示其说明，否则页面默认
            bool anyPressed = false;
            foreach (IModSettingItemWidget w in m_itemWidgets) {
                if (w.IsOperating) {
                    m_descriptionLabel.Text = w.DescriptionText;
                    anyPressed = true;
                }
            }
            if (!anyPressed && m_currentPage != null)
                m_descriptionLabel.Text = ModSettingLocalizer.ResolveText(m_packageName, m_pageIds, "Description", m_currentPage.Description, false);

            if (Input.Back || Input.Cancel) {
                if (m_pageStack.Count > 0) {
                    m_pageStack.Pop();
                    NavigateCurrent();
                }
                else {
                    ScreensManager.GoBack();
                }
            }
        }
    }
}
