using System;
using System.ComponentModel;

namespace OpenTap.Package
{
    // This is not actually used as a component setting. This is a hack because there currently is no other 
    // hook to know when a TestPlanRun is started, besides creating a result listener. 
    // Since this component setting is internal, it will not show up in the GUI, but it is detected by the engine
    // since the 'PluginAssembly' attribute is used in this assembly. 
    [Browsable(false)]
    internal class TestPlanRunPackageParameterMonitor : ComponentSettings, ITestPlanRunMonitor
    {
        private static TraceSource log = Log.CreateSource(nameof(TestPlanRunPackageParameterMonitor));
        public void EnterTestPlanRun(TestPlanRun plan)
        {
            try
            {
                var pathParam = plan.Parameters["TestPlanPath"];

                if (!(pathParam is string path)) return;
                if (path == "NULL" || string.IsNullOrEmpty(path)) return;

                var package = Installation.Current.FindPackageContainingFile(path);
                if (package == null) return;

                plan.Parameters["Test Plan Package"] =
                    $"{package.Name}|{package.Version}|{package.Hash ?? package.ComputeHash()}";
            }
            catch (Exception ex)
            {
                // It is crucial that this method does not prevent test plans from executing since it cannot be disabled.
                // Just log the error and return
                log.Debug(ex);
            }
        }

        public void ExitTestPlanRun(TestPlanRun plan)
        {
            
        }
    }
}
