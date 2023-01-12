using System.Collections.Generic;

namespace PluginDevelopment.Gui.OperatorPanel
{
    public class OperatorMainPanelViewModel
    {
        public int Rows => OperatorUiSettings.Current.Rows;
        public int Columns => OperatorUiSettings.Current.Columns;
        public IEnumerable<OperatorUiSetting> Items => OperatorUiSettings.Current.OperatorUis;
    }
}