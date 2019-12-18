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
using System;
using System.Windows;
using System.IO;
using Keysight.OpenTap.Wpf;
using System.Windows.Threading;
using System.Windows.Forms;
using System.Windows.Controls;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;

// This file shows how to implement a custom dockable panel. The panel can be enabled/disabled under 
// the View menu choice in the TAP GUI. The panel can be configured to be either floating or docked.

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Dockable Panel Example")]
    // A custom dockable panel has to implement ITapDockPanel. 
    public class DockablePanel : ITapDockPanel
    {
        // Default panel dimensions
        public double? DesiredWidth { get { return 200; } }

        public double? DesiredHeight { get { return 200; } }

        dockResultListener listener;

        static TraceSource Log = OpenTap.Log.CreateSource("DockExample");

        // In this method the layout of the dockable panel is defined/setup. 
        // The ITapDockContext enables you to set the TestPlan, attach ResultListeners, 
        // configure Settings and start execution of a TestPlan. 
        public FrameworkElement CreateElement(ITapDockContext context)
        {
            var loadPlanBtn = new Button() { Content = "Load Plan" };
            var runPlanBtn = new Button() { Content = "Run Plan" };
            var stopPlanBtn = new Button() { Content = "Stop Plan" };
            var statusTxt = new TextBlock
            {
                FontSize = 40,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };

            // Setup UI panel and add elements
            var panel = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Vertical };

            panel.Children.Add(loadPlanBtn);
            panel.Children.Add(runPlanBtn);
            panel.Children.Add(stopPlanBtn);
            panel.Children.Add(statusTxt);

            TapThread planThread = null;
            // Register event-handling methods for each of the buttons
            runPlanBtn.Click += (s, e) => planThread = context.Run();
            stopPlanBtn.Click += (s, e) => planThread?.Abort();
            loadPlanBtn.Click += (s, e) =>
            {
                var fd = new OpenFileDialog();
                fd.CheckFileExists = true;
                var r = fd.ShowDialog();
                try
                {
                    if (r == DialogResult.OK)
                        context.Plan = TestPlan.Load(fd.FileName);
                }
                catch (InvalidOperationException ex)
                {
                    Log.Warning("{0}", ex.Message);
                }
            };
            // Attach Result listener. runPlanBtn and statusTxt is updated according to status
            context.ResultListeners.Add(listener = new dockResultListener(runPlanBtn, statusTxt));

            return panel;
        }

        // Result listener used for dockable panel. Result listeners can be used in 
        // a custom dockable panel. 
        [System.ComponentModel.Browsable(false)]
        class dockResultListener : ResultListener
        {
            Button btn;
            TextBlock txt;

            public dockResultListener(Button b, TextBlock txt)
            {
                btn = b;
                this.txt = txt;
                OpenTap.Log.RemoveSource(this.Log);
            }
            public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
            {
                GuiHelper.GuiInvoke(() =>
                {
                    btn.IsEnabled = true;
                    txt.Text = planRun.Verdict.ToString();
                    txt.Foreground = Brushes.Gray;
                    if (planRun.Verdict == Verdict.Pass)
                        txt.Foreground = Brushes.Green;
                    if (planRun.Verdict == Verdict.Fail)
                        txt.Foreground = Brushes.Red;
                });
            }

            public override void OnTestPlanRunStart(TestPlanRun planRun)
            {
                GuiHelper.GuiInvoke(() =>
                {
                    btn.IsEnabled = false;
                    txt.Text = "";
                });
            }
        }
    }

    // GuiHelper class for updating GUIs. It can be reused for custom UI components.
    class GuiHelper
    {
        static Dispatcher getGuiDispatcher()
        {
            if (System.Windows.Application.Current != null) return System.Windows.Application.Current.Dispatcher;
            return null;

        }

        /// <summary>
        /// Invoke action in GUI thread. Optionally blocking.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="NonBlocking">use Invoke or BeginInvoke</param>
        public static void GuiInvoke(Action action, Dispatcher dispatch = null, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            try
            {
                dispatch = dispatch ?? getGuiDispatcher();
                if (dispatch == null)
                {
                    try
                    {
                        action();
                    }
                    catch (InvalidOperationException)
                    {
                        // There is a chance that this might throw an InvalidOperationException ("The calling thread cannot access this object because a different thread owns it.") 
                        // because we are not on the correct thread.
                        // This can happen when the app is closing.
                    }
                }
                else if (dispatch.CheckAccess())
                {
                    action();
                }
                else
                {
                    dispatch.Invoke(action, priority);
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                // If the dispatcher is stopped, this can happen.
                // Should only happen upon exiting, so we need to check that it is because the dispatcher is shutting down.
                if (dispatch != null && (dispatch.HasShutdownStarted || dispatch.HasShutdownFinished))
                {
                    // Do nothing. This is OK.
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
