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
using System.Diagnostics;
using OpenTap;

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Log Messages Example", Groups: new[] { "Examples", "Plugin Development", "Step Execution" }, 
        Description: "Example of logging.")]
    public class LogMessages : TestStep
    {
        // Users can always use the predefined Log that is defined in the TestStep base class.
        // Optionally, they can define their own log, which is useful to show different log messages sources.
        // In the case below, MyLog will show up in the logs.
        private OpenTap.TraceSource MyLog = OpenTap.Log.CreateSource("MyLog");

        public override void Run()
        {   
            // There are four levels of log messages Info, Warning, Error, Debug.
            MyLog.Info("Info from Run");
            for (int i = 0; i < 10; i++)
            {
                MyLog.Debug("Debug {0} from Run", i); // MyLog.X works like string.Format with regards to arguments.
            }
            MyLog.Warning("Warning from Run");
            MyLog.Error("Error from Run");

            // The Log can accept a Stopwatch Object to be used for timing analysis.
            Stopwatch sw1 = Stopwatch.StartNew();
            TapThread.Sleep(100);
            MyLog.Info(sw1, "Info from Run");

            Stopwatch sw2 = Stopwatch.StartNew();
            TapThread.Sleep(200);
            MyLog.Error(sw2, "Error from step");

            // Tracebar can be used to show results in the MyLog.
            var traceBar = new TraceBar();
            traceBar.LowerLimit = -3.0;
            for (var i = -2; i < 11; i++)
            {
                traceBar.UpperLimit = i < 5 ? 3 : 15;
                // GetBar returns a string with value, low limit, a dashed line 
                // indicating magnitude, the upper limit, and (if failing), a fail indicator.
                string temp = traceBar.GetBar(i);
                MyLog.Info("MyResult: " + temp);
                TapThread.Sleep(200);
            }
            // Sample output shown below.
            //   MyResult: 2.00 - 3-------------------------|-----  3
            //   MyResult: 3.00 - 3------------------------------ | 3
            //   MyResult: 4.00 - 3------------------------------ > 3  Fail
            //   MyResult: 5.00 - 3------------ -| -----------------15
            //   MyResult: 6.00 - 3-------------- -| ---------------15

            // TraceBar remembers if any results failed, so it can be used for the verdict.
            UpgradeVerdict(traceBar.CombinedVerdict);

            // The log also supports showing stack traces. 
            // Useful for debugging.
            try
            {
                throw new Exception("My exception");
            }
            catch (Exception e)
            {
                MyLog.Error("Caught exception: '{0}'", e.Message);
                MyLog.Debug(e); // Prints the stack trace to the MyLog.
            }
        }
    }
}
