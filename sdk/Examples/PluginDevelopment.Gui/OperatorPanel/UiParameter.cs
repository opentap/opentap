using OpenTap;

namespace PluginDevelopment.Gui.OperatorPanel
{
    [Display("UI Parameter")]
    public class UiParameter
    {
        [Display("External Name")]
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
    }
}