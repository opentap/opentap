//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Linq;
using System.ComponentModel;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary>
    /// Settings class containing user-configurable platform options.
    /// </summary>
    [Display("Engine", "Engine Settings")]
    [HelpLink(@"EditorHelp.chm::/Configurations/Engine Configuration.html")]
    public class EngineSettings : ComponentSettings<EngineSettings>
    {
        /// <summary>
        /// Enum to represent choices for <see cref="AbortTestPlan"/> setting.
        /// </summary>
        [Flags]
        public enum AbortTestPlanType
        {
            /// <summary> If a step completes with verdict 'Fail', the test plan execution should be aborted.</summary>
            [Display("Step Fail", "If a step completes with verdict 'Fail', the test plan execution should be aborted.")]
            Step_Fail = 1,
            /// <summary> If a step completes with verdict 'Error', the test plan execution should be aborted.</summary>
            [Display("Step Error", "If a step completes with verdict 'Error', the test plan execution should be aborted.")]
            Step_Error = 2
        }


        /// <summary>
        /// Where the session logs are saved. Must be a valid path.
        /// </summary>
        [Display("Log Path", Group: "General", Order:1, Description: "Where to save the session log file. This setting only takes effect after restart.")]
        [FilePath(FilePathAttribute.BehaviorChoice.Save)]
        [HelpLink(@"EditorHelp.chm::/Configurations/Using Tags and Variables in File Names.html")]
        public MacroString SessionLogPath { get; set; }
        
        /// <summary>
        /// Controls whether the engine should propagate a request for metadata.
        /// </summary>
        [Display("Allow Metadata Prompt", Group: "General", Order: 2, Description: "When the test plan starts, prompt the user for data specified by the plugin to associate with the results.")]
        public bool PromptForMetaData { get; set; }

        /// <summary>
        /// Configures the engine to stop the test plan run if a step fails or causes an error.  
        /// </summary>
        [Display("Abort Run If", Group: "General", Order: 1, Description: "Allows aborting the test plan run if a step fails or causes an error.")]
        public AbortTestPlanType AbortTestPlan { get; set; }

        /// <summary>
        /// Name of the operator. This name will be saved along with the results.
        /// </summary>
        [Display("Name", Group: "Operator", Order: 20, Description: "Name of the operator. This name will be saved along with the results.")]
        [MetaData]
        public string OperatorName { get; set; }

        /// <summary>
        /// Name of the test station. This name will be saved along with the results.
        /// </summary>
        [Display("Station", Group: "Operator", Order: 21, Description: "Name of the test station. This name will be saved along with the results.")]
        [MetaData]
        public string StationName { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowed latency for result propagation. When the limit is reached, the test plan run pauses while the results are propagated to the Result Listeners. 
        /// Result processing time is an estimated value based on previous processing delays.
        /// </summary>
        [Display("Result Latency Limit", Group:"Advanced", Collapsed:true, Order:100.1, Description: "The maximum allowed latency for result propagation.  Reaching this limit will result in a temporary pause in the test plan while the results are being propagated to the ResultListeners. Result processing time is an estimated value based on previous processing delays.")]
        [Unit("s")]
        [Browsable(false)]
        public double ResultLatencyLimit { get; set; }

        /// <summary>
        /// True, if there is more than one log timestamping type.
        /// </summary>
        [XmlIgnore]
        [Browsable(false)]
        public bool HasMultipleTimestampers
        {
            get
            {
                return PluginManager.GetPlugins<Diagnostic.ILogTimestampProvider>().Count > 1;
            }
        }

        /// <summary>
        /// Sets the log timestamp mechanism.
        /// </summary>
        [Display("Log Timestamping", Group: "General", Order: 10, Description: "Which timestamping mechanism to use in the log.")]
        [PluginTypeSelector]
        [EnabledIf("HasMultipleTimestampers", true, HideIfDisabled = true)]
        public Diagnostic.ILogTimestampProvider LogTimestamper
        {
            get { return Log.Timestamper; }
            set { Log.Timestamper = value; }
        }
        

        /// <summary>
        /// Sets up some default values for the various settings.
        /// </summary>
        public EngineSettings()
        {
            SessionLogPath = new MacroString { Text = "SessionLogs/SessionLog <Date>.txt" };
            ResultLatencyLimit = 3.0;
            OperatorName = Environment.ExpandEnvironmentVariables("%USERNAME%");
            StationName = System.Environment.MachineName;

            // Set OpenTAP to abort on step error by default.
            AbortTestPlan = AbortTestPlanType.Step_Error;

            PromptForMetaData = false;

            ResourceManagerType = new ResourceTaskManager();
        }

        /// <summary> Where the test executive was started from. </summary>
        public static string StartupDir
        {
            get { return Environment.GetEnvironmentVariable("STARTUP_DIR"); }
            set { Environment.SetEnvironmentVariable("STARTUP_DIR", value); }
        }

        /// <summary>
        /// 
        /// </summary>
        [PluginTypeSelector]
        [Display("Resource Strategy", Group: "General", Order: 12, Description: "Selects which strategy to use for opening resources while the test plan is executing.")]
        public IResourceManager ResourceManagerType { get; set; }

        /// <summary> Loads a new working directory and sets up environment variables important to OpenTAP.</summary>
        /// <param name="newWorkingDirectory"></param>
        public static void LoadWorkingDirectory(string newWorkingDirectory)
        {
            EngineSettings.StartupDir = System.IO.Directory.GetCurrentDirectory();
            System.IO.Directory.SetCurrentDirectory(newWorkingDirectory);
        }

        static EngineSettings()
        {
            StartupDir = System.IO.Directory.GetCurrentDirectory();
            Environment.SetEnvironmentVariable("ENGINE_DIR", System.IO.Path.GetDirectoryName(typeof(TestPlan).Assembly.Location));
        }
    }
}
