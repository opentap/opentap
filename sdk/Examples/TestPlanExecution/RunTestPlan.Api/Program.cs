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
using System.IO;
using OpenTap;

namespace OpenTap.TestPlanExecution.RunTestPlan.Api
{
    class Program
    {
        // This example loads and runs a predefined test plan using the TAP API, the same interface the TAP GUI uses.
        // Use this to create custom GUIs or Operator interfaces. If no GUI is needed, the CLI may be a better alternative.
        // To run this example you must specify a path to a .TapPlan file.

        private static void Main(string[] args)
        {
            Console.WriteLine("\nThis example shows using the TAP API to control loading and running a TestPlan.");

            try
            {
                // If you have plugins in directories different from the location of your TAP_PATH, then add those directories here.
                // PluginManager.DirectoriesToSearch.Add(@"C:\SomeOtherDirectory");

                // Start finding plugins.
                PluginManager.SearchAsync();

                // Point to log file to be used.
                SessionLogs.Initialize("console_log.txt");

                // Determine path to .TapPlan file.
                string absoluteTestPlanPath;
                if (args.Length == 1  && File.Exists(args[0]))
                {
                    absoluteTestPlanPath = args[0];
                }
                else
                {
                    Console.WriteLine("Please specify an absolute path to a .TapPlan file.");
                    return;
                }
               
                // ResultsListeners are configured by settings files, using the TAP GUI.
                // If settings files do not exist, then a default set of settings files, 
                // including a default "Text Log" listener will be used.
                // Alternately, RestultListeners can be configured via TAP API. See the BuildTestPlan.Api example.
                
                // Load the Test Plan.
                TestPlan myTestPlan = TestPlan.Load(absoluteTestPlanPath);

                // Execute the Test Plan.
                TestPlanRun myTestPlanRun = myTestPlan.Execute();
                Console.WriteLine("Loaded and ran \n{0}", absoluteTestPlanPath);
                
                // Test Plan properties are accessible.
                Console.WriteLine("TestPlan verdict={0}", myTestPlanRun.Verdict);

                // After the Test Plan has been run Macros, if used, can be expanded.
                SessionLogs.Rename(EngineSettings.Current.SessionLogPath.Expand(date: DateTime.Now));

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex.Message);
            }
            finally
            {
                Console.WriteLine("Press any key to continue.");
                Console.ReadLine();
            }
        }
    }
}
