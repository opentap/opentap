using System.Collections.Generic;
using System.ComponentModel;
using OpenTap;

namespace PluginDevelopment.Gui.OperatorPanel
{
    public class OperatorUiSetting
    {
        public string Name { get; set; }
        public List<UiParameter> Parameters { get; set; } = new List<UiParameter>();

        [Browsable(true)]
        [Display("Grid Location", Order: 3)]
        public string Location
        {
            get
            {
                var settings = OperatorUiSettings.Current;
                var uis = OperatorUiSettings.Current.OperatorUis;
                var index = uis.IndexOf(this);
                if (index == -1) return "";
                if (index > settings.Rows * settings.Columns)
                    return "";
                return $"{index % settings.Columns + 1}, {index / settings.Rows + 1}";
            }
        }
    }
}