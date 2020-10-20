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

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Resource Open Before Example",
        Groups: new[] { "Examples", "Plugin Development", "Attributes" },
        Description: "TestStep that uses ResourceOpen attribute to show opening sequence for dependent instruments of different resource open modes.")]
    public class OpenPriorResource : TestStep
    {
        public OpenPriorResource() { }
        public OpenPriorInstrument OpenPriorInstr { get; set; }

        public override void Run() { }
    }

    [Display("Resource Open Parallel Example",
        Groups: new[] { "Examples", "Plugin Development", "Attributes" },
        Description: "TestStep that uses ResourceOpen attribute to show opening sequence for dependent instruments of different resource open modes.")]
    public class OpenParallelResource : TestStep
    {
        public OpenParallelResource() { }
        public OpenParallelInstrument OpenParallelInstr { get; set; }

        public override void Run() { }
    }

    // Note that ResourceOpenBefore is the default behavior for all resources. Instrument, a derived type of Resource is used in this example. Of course, other resource types derived from Resource class can also be used eg. DUT, Listeners etc.

    // PriorSubInstr is open before its parent instr is open, a sleep delay is used to show this sequence.
    // PriorSubInstr will close 2 sec after base instr is closed.
    [Display("Open Prior Instrument", Groups: new[] { "Examples", "Plugin Development" }, Description: "An instrument containing dependent instruments of resource open before & ignore behaviour type.")]
    public class OpenPriorInstrument : Instrument
    {
        [ResourceOpen(ResourceOpenBehavior.Before)] // This is the default behavior
        public Instrument PriorSubInstr { get; set; }

        [ResourceOpen(ResourceOpenBehavior.Ignore)]
        public Instrument IgnoreSubInstr { get; set; }

        public override void Open()
        {
            TapThread.Sleep(2000);
            Log.Info("Opening Prior Instrument");
            base.Open();
            PrintSubInstrStatus();
        }

        public override void Close()
        {
            TapThread.Sleep(1000);  // Can be some operations that take eg. 1 sec just to show it is connected

            Log.Info("Closing Prior Instrument");
            base.Close();
            TapThread.Sleep(2000);
            PrintSubInstrStatus();
        }

        public void PrintSubInstrStatus()
        {
            Log.Info("PriorSubInstr connected: {0}", PriorSubInstr.IsConnected);
            Log.Info("IgnoreSubInstr connected: {0}", IgnoreSubInstr.IsConnected);
        }
    }

    // ParallelSubinstr will open in parallel with its parent instr. To show the difference in opening time, parent instr is delayed by 2 secs.
    // ParallelSubinstr will close as soon as connection is no longer needed. To show the sequence, parent instr is delayed to close by 2 secs.
    [Display("Open Parallel Instrument", Groups: new[] { "Examples", "Plugin Development" }, Description: "An instrument containing dependent instruments of resource open parallel & ignore behaviour type.")]
    public class OpenParallelInstrument : Instrument
    {
        [ResourceOpen(ResourceOpenBehavior.InParallel)]
        public Instrument ParallelSubInstr { get; set; }

        [ResourceOpen(ResourceOpenBehavior.Ignore)]
        public Instrument IgnoreSubInstr { get; set; }

        public override void Open()
        {
            TapThread.Sleep(2000);
            Log.Info("Opening Parallel Instrument");
            base.Open();
            PrintSubInstrStatus();
        }

        public override void Close()
        {
            Log.Info("Closing Parallel Instrument");
            TapThread.Sleep(2000);
            base.Close();
            PrintSubInstrStatus();
        }

        public void PrintSubInstrStatus()
        {
            Log.Info("ParallelSubInstr connected: {0}", ParallelSubInstr.IsConnected);
            Log.Info("IgnoreSubInstr connected: {0}", IgnoreSubInstr.IsConnected);
        }
    }
}
