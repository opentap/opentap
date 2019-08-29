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
using System.ComponentModel;
using System.Globalization;
using OpenTap;

// This example defines a "Bin" column that can be shown in the test plan editor.  
namespace OpenTap.Plugins.PluginDevelopment
{
    public enum Bin
    {
        [Display("NotSet", Description: "The bin has not been set.")] NotSet,
        [Display("Bin 1", Description: "Bin1 set.")] Bin1,
        [Display("Bin 2", Description: "Bin2 set.")] Bin2,
        [Display("Bin 3", Description: "Bin3 set.")] Bin3
    }
    
    [Display("Column Display Example",
        Groups: new[] { "Examples", "Plugin Development", "Attributes" },
        Description: "Shows how an test step setting can be added as a column to the test plan editor.")]
    public class ColumnDisplayAttributeExample : TestStep
    {
        private static readonly Random RandomSeed = new Random();

        [ColumnDisplayName("Bin")]
        [Browsable(true)]
        public Bin OutBin { get; private set; }

        public override void Run()
        {
            var v = Math.Abs(Math.Sin(RandomSeed.NextDouble()));
            Log.Info("Value={0}", v.ToString(CultureInfo.InvariantCulture));
            if (v < 0.2)
            {
                UpgradeVerdict(Verdict.Fail);
                OutBin = Bin.NotSet;
            }
            else
            {
                if (v < 0.4)
                {
                    OutBin = Bin.Bin1;
                }
                else if (v < 0.6)
                {
                    OutBin = Bin.Bin2;
                }
                else
                {
                    OutBin = Bin.Bin3;
                }
                UpgradeVerdict(Verdict.Pass);
            }
        }
    }
}
