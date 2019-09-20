//Copyright 2012-2019 Keysight Technologies
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.
using OpenTap;
using Keysight.OpenTap.Wpf;

// Classes that implement IMenuItem can be used to add menus to the TAP GUI.
// Only the Invoke method is required.

// When using the Display Attributes for menu choices, note the following behavior.
// The first parameter is the visible name of the sub menu. It does not need to match the class name.
// Group is the name of the top menu. An underscore in the group name indicates the shortcut key for this menu.
// Order is the left to right order of the top menus. Top menu choices are ordered low to high.
// Description is not used.
// Collapsed is not used.

// Top level custom menus are added after the last standard TAP menu.
// Sub menus are ordered alphabetically by display name.
// It is not possible to create groups under the top menus.

namespace OpenTap.Plugins.PluginDevelopment
{
    // The underscore is an keyboard short cut.
    [Display("ToolA", Group: "E_xample Menu", Order: 1, Description: "A menu choice under the ExampleMenu top level menu.")]
    public class ToolMenuA : IMenuItem
    {
        private TraceSource _log = Log.CreateSource("CustomMenus");
        public void Invoke() { _log.Info("Invocation for ToolMenuA"); }
    }

    [Display("ToolB", Group: "_Example Menu", Order: 2, Description: "A menu choice under the ExampleMenu top level menu.")]
    public class ToolMenuB : IMenuItem
    {
        private TraceSource _log = Log.CreateSource("CustomMenus");
        public void Invoke() {_log.Info("Invocation for ToolMenuB");
        }
    }

    [Display("ToolC", Groups: new[] { "_Example Menu", "SubGroupExample" }, Order: 3, Description: "A  menu choice under subgroup to ExampleMenu top level menu.")]
    public class ToolMenuC : IMenuItem
    {
        private TraceSource _log = Log.CreateSource("CustomMenus");
        public void Invoke()
        {
            _log.Info("Invocation for ToolMenuC");
        }
    }

}
