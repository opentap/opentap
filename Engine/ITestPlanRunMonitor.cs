//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Linq;
namespace OpenTap
{
    /// <summary>
    /// Interface for use in pre/post test plan (for example, it can be used to lock a remote setup). Only supported on <see cref="ComponentSettings"/> types.
    /// </summary>
    public interface ITestPlanRunMonitor : ITapPlugin
    {
        /// <summary>
        /// Called before Test Plan is executed.
        /// </summary>
        /// <param name="plan"></param>
        void EnterTestPlanRun(TestPlanRun plan);

        /// <summary>
        /// Called after Test Plan is executed.
        /// </summary>
        void ExitTestPlanRun(TestPlanRun plan);
    }

    static class TestPlanRunMonitors
    {
        /// <summary>
        /// Returns a list of the current access controllers. 
        /// These are ComponentSettings instances that inherits from ITestPlanAccessController.
        /// </summary>
        /// <returns></returns>
        static public ITestPlanRunMonitor[] GetCurrent()
        {
            return PluginManager.GetPlugins<ComponentSettings>()
                .Where(settings => settings.DescendsTo(typeof(ITestPlanRunMonitor)))
                .Select(ComponentSettings.GetCurrent).OfType<ITestPlanRunMonitor>()
                .ToArray();
        }
    }
}
