using System.Collections.Generic;
using OpenTap;

namespace PluginDevelopment.Gui.OperatorPanel
{
    [Display("Operator UI Settings")]
    public class OperatorUiSettings : ComponentSettings<OperatorUiSettings>
    {
        [Display("Operator UIs")]
        public List<OperatorPanelSetting> OperatorUis { get; set; } = new List<OperatorPanelSetting>()
        {
            new OperatorPanelSetting { Name = "Panel 1" }
        };
        [Display("Rows", Order: 1, Group:"Layout")]
        public int Rows { get; set; } = 1;
        [Display("Columns", Order: 1, Group:"Layout")]
        public int Columns { get; set; } = 1;
    }
}