//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace OpenTap.Cli
{
    class TestPlanRunner
    {
        private static readonly TraceSource log = Log.CreateSource("Main");
        
        public static Verdict RunPlanForDut(TestPlan Plan, List<ResultParameter> metadata, CancellationToken cancellationToken)
        {
            Plan.PrintTestPlanRunSummary = true;
            return Plan.ExecuteAsync(ResultSettings.Current, metadata, null, cancellationToken).Result.Verdict;
        }

        public static void SetSettingsDir(string dir)
        {
            ComponentSettings.PersistSettingGroups = false;
            string settingsSetDir = Path.Combine(ComponentSettings.SettingsDirectoryRoot, "Bench", dir);
            if (dir != "Default" && !Directory.Exists(settingsSetDir))
                throw new ArgumentException($"Could not find settings directory \"{settingsSetDir}\"", nameof(RunCliAction.Settings));

            ComponentSettings.SetSettingsProfile("Bench", dir);
            log.TraceInformation("Settings: " + ComponentSettings.GetSettingsDirectory("Bench"));
        }
    }
}
