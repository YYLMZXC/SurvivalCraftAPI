using System.Xml.Linq;
using Engine;

namespace Game {
    public class VrControllerMappingScreen : Screen {
        public const string fName = nameof(VrControllerMappingScreen);
        public const string keysSection = "VrControllerMappingScreenKeys";
        public const string actionsSection = "VrControllerMappingScreenActions";

        public ListPanelWidget m_bindingsList;
        public BevelledButtonWidget m_gameHelpButton;

        public VrControllerMappingScreen() {
            XElement node = ContentManager.Get<XElement>("Screens/KeyboardMappingScreen");
            LoadContents(this, node);
            m_bindingsList = Children.Find<ListPanelWidget>("KeysList");
            m_bindingsList.ItemWidgetFactory = (Func<object, Widget>)Delegate.Combine(m_bindingsList.ItemWidgetFactory, BindingInfoWidget);
            m_bindingsList.ScrollPosition = 0f;
            m_bindingsList.ScrollSpeed = 0f;
            m_bindingsList.ItemClicked += item => { };
            m_gameHelpButton = Children.Find<BevelledButtonWidget>("GameHelp");
            Children.Find<BevelledButtonWidget>("SetKey").IsVisible = false;
            Children.Find<BevelledButtonWidget>("DisableKey").IsVisible = false;
            Children.Find<BevelledButtonWidget>("Reset").IsVisible = false;
        }

        public Widget BindingInfoWidget(object item) {
            XElement node = ContentManager.Get<XElement>("Widgets/KeyboardMappingItem");
            node.SetAttributeValue("Name", $"VrBinding_{item}");
            ContainerWidget containerWidget = (ContainerWidget)LoadWidget(this, node, null);
            LabelWidget nameLabel = containerWidget.Children.Find<LabelWidget>("Name");
            LabelWidget actionLabel = containerWidget.Children.Find<LabelWidget>("BoundKey");
            if (item is VrBindingEntry entry) {
                nameLabel.Text = entry.Button;
                actionLabel.Text = entry.Action;
            }
            return containerWidget;
        }

        public override void Enter(object[] parameters) {
            m_gameHelpButton.IsVisible = ScreensManager.PreviousScreen is GameScreen;
            m_bindingsList.ClearItems();
            PopulateBindings();
        }

        public override void Update() {
            if (m_gameHelpButton.IsClicked) {
                ScreensManager.SwitchScreen("Help");
            }
            if (Children.Find<ButtonWidget>("TopBar.Back").IsClicked || Input.Back || Input.Cancel) {
                ScreensManager.GoBack();
            }
        }

        static string Tk(string key) => LanguageControl.Get(keysSection, key);
        static string Ta(string key) {
            string s = LanguageControl.Get(out bool r, "KeyboardMappingScreen", key);
            if (r) return s;
            return LanguageControl.Get(actionsSection, key);
        }
        static string Compose(params string[] keys) => string.Join(" / ", keys.Select(Ta));

        void PopulateBindings() {
            AddHeader(Tk("LeftController"));
            AddBinding(Tk("ThumbstickTrackpad"), Ta("HorizontalMove"));
            AddBinding(Tk("TriggerClick"), Ta("Interact"));
            AddBinding(Tk("TriggerHold"), Compose("Aim", "SpecialClick"));
            AddBinding(Tk("Grip"), Ta("EditItem"));
            AddBinding(Tk("YTrackpadUp"), Ta("ToggleMount"));
            AddBinding(Tk("XTrackpadDown"), Ta("ToggleCrouch"));
            AddBinding(Tk("ThumbrestTrackpadLeft"), Ta("ToggleInventory"));
            AddBinding(Tk("TrackpadRight"), Ta("ToggleClothing"));
            AddBinding(Tk("Menu"), Compose("GameMenu", "UIBack"));

            AddHeader(Tk("RightController"));
            AddBinding(Tk("ThumbstickTrackpadHorizontal"), Ta("Look"));
            AddBinding(
                Tk("ThumbstickTrackpadVertical"),
                $"{LanguageControl.Get("KeyboardMappingScreen", "MoveUp")}, {LanguageControl.Get("KeyboardMappingScreen", "MoveDown")} / {LanguageControl.Get(actionsSection, "UIScroll")}"
            );
            AddBinding(Tk("TriggerClick"), Compose("Hit", "UIClick"));
            AddBinding(Tk("TriggerHold"), Compose("Dig", "UIPress"));
            AddBinding(Tk("Grip"), Compose("Drop", "UIBack"));
            AddBinding(Tk("BTrackpadLeft"), Ta("ScrollInventoryLeft"));
            AddBinding(Tk("ATrackpadRight"), Ta("ScrollInventoryRight"));
            AddBinding(Tk("ThumbrestTrackpadUp"), Ta("ToggleFly"));
            AddBinding(Tk("TrackpadDown"), Ta("SwitchCameraMode"));
            AddBinding(Tk("Menu"), Compose("GameMenu", "UIBack"));
        }

        void AddHeader(string text) {
            m_bindingsList.AddItem(new VrBindingEntry(text, ""));
        }

        void AddBinding(string button, string action) {
            m_bindingsList.AddItem(new VrBindingEntry(button, action));
        }

        public class VrBindingEntry {
            public string Button;
            public string Action;
            public VrBindingEntry(string button, string action) {
                Button = button;
                Action = action;
            }
        }
    }
}
