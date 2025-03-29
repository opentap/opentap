using System.Windows;
using Keysight.OpenTap.Wpf;
using OpenTap;

namespace PluginDevelopment.Gui.OperatorPanel
{
    /// <summary>
    /// This class is a plugin for the docking system in Test Automation, which can serve a view given the current context.
    /// </summary>
    [Display("Operator UI", Group:"Examples")]
    public class OperatorPanelProvider : ITapDockPanel
    {
        /// <summary> Creates a new operator ui main panel. </summary>
        public FrameworkElement CreateElement(ITapDockContext context) => new OperatorMainPanel(context);

        /// <summary> Default size for when opening this as a window. </summary>
        public double? DesiredWidth => 200;
        
        /// <summary> Default size for when opening this as a window. </summary>
        public double? DesiredHeight => 200;
    }
}