using System.Collections.Generic;
using System.ComponentModel;
using OpenTap;

namespace PluginDevelopment.Gui.OperatorPanel
{
    public class OperatorPanelSetting
    {
        /// <summary> The name of the panel. </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// the panel parameters, for example which DUT is used in this panel.
        /// </summary>
        public List<PanelParameter> Parameters { get; set; } = new List<PanelParameter>();
    }
}