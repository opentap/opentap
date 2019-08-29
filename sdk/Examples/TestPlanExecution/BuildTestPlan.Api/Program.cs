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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OpenTap;
using OpenTap.Plugins.BasicSteps;


// This examples shows how to build, save and execute a test plan using TAP API.
namespace OpenTap.TestPlanExecution.BuildTestplan.Api
{
    class Program
    {
        static void Main()
        {
            // If you have plugins in directories different from the location of your TAP_PATH, then add those directories here.
            // PluginManager.DirectoriesToSearch.Add(@"C:\SomeOtherDirectory");

            // Start finding plugins.
            PluginManager.SearchAsync();

            // Point to log file to be used.
            SessionLogs.Initialize("console_log.txt");

            // Create a new Test Plan.
            TestPlan myTestPlan = new TestPlan();

            // All Test Plan steps are added as child steps.
            myTestPlan.ChildTestSteps.Add(new DelayStep { DelaySecs = .1, Name = "Delay1"});

            // Sequences can be created and added to TestPlan.
            SequenceStep mySequenceStep = new SequenceStep();
            mySequenceStep.ChildTestSteps.Add(new DelayStep { DelaySecs = .2, Name = "Delay2"});

            LogStep logStep = new LogStep
            {
                Name = "SomeName",
                LogMessage = "Hello from myTestPlan at " + DateTime.Now.ToLongTimeString(),
                Severity = LogSeverity.Info
            };
            mySequenceStep.ChildTestSteps.Add(logStep); 

            // Sequences are added to the Test Plan like any other step.
            myTestPlan.ChildTestSteps.Add(mySequenceStep);

            // The Test Plan can be saved for later reuse.
            string myFilePath = Path.Combine(AssemblyDirectory, myTestPlan.Name + ".TapPlan");
            myTestPlan.Save(myFilePath);

            // Add any ResultListeners that should be used.
            // If not specified, a list of ResultListeners with a single LogResultListener will be created.
            // Alternatively, the ResultListeners could be defined in settings files.
            List<ResultListener> resultListeners = new List<ResultListener>();
            resultListeners.Add(new LogResultListener());
            //resultListeners.Add(new Keysight.OpenTap.Plugins.Csv.CsvResultListener());

            // Execute the TestPlan. This is the equivalent of the Run button in the TAP GUI.
            myTestPlan.Execute(resultListeners);

            // After the TestPlan has been run Macros, if used, can be expanded.
            SessionLogs.Rename(EngineSettings.Current.SessionLogPath.Expand(date: DateTime.Now));

            Console.WriteLine("This example builds a TestPlan programmatically.");
            Console.WriteLine("Press any key to exit.");
            Console.ReadLine();

        }

        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }
    }
}
