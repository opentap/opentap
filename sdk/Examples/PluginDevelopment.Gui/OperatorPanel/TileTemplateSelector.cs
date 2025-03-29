using System.Windows;
using System.Windows.Controls;

namespace PluginDevelopment.Gui.OperatorPanel
{
    public class TileTemplateSelector : DataTemplateSelector 
    { 
        public override DataTemplate SelectTemplate(object item, DependencyObject container) 
        { 
            if (item is OperatorPanelSetting)
                return PanelTemplate;
            return NoPanelTemplate; 
        } 
        public DataTemplate PanelTemplate { get; set; }
        public DataTemplate NoPanelTemplate { get; set; }
    }
}