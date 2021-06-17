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

namespace OpenTap.Plugins.PluginDevelopment
{
    // An example of an instrument that uses topologies.
    // Notice that this class resource depends on a couple of other resources via the
    // 'ScpiInst' and 'IdInst' properties. This is how you define resource topologies.
    // DUTs and ResultListeners can also have topologies, but the concepts are the same.
    [Display("Topologic Instrument", Groups: new[] { "Examples", "Plugin Development", "Resource Topology" }, 
        Description: "This instrument depends on other instruments in order to work..")]
    public class InstrumentTopology : Instrument, IIdInstrument
    {
        [Display("Use SCPI Instrument", Description:"If a scpi instrument should be used or the other option.")]
        public bool UseScpiInstrument { get; set; }
        
        // When the EnabledIf attribute is used in resource topologies, only enabled resources will get connected.  
        [EnabledIf(nameof(UseScpiInstrument), true)]
        // ResourceOpenBehavior.Before: This is the default behavior. Scpi gets Opened before 'this' instrument.
        [ResourceOpen(ResourceOpenBehavior.Before)] 
        [Display("SCPI Instrument")]
        public ScpiInstrument ScpiInst { get; set; }
        
        
        [EnabledIf(nameof(UseScpiInstrument), false)]
        // ResourceOpenBehavior.InParallel: IdInst gets opened in parallel to 'this' instrument.
        // In this case this has no effect because we don't override Open/Close.
        [ResourceOpen(ResourceOpenBehavior.InParallel)]
        [Display("ID Instrument")]
        public IIdInstrument IdInst { get; set; }
        
        [ResourceOpen(ResourceOpenBehavior.Ignore)]
        //ResourceOpenBehavior.Ignore: This instrument get's ignored and will not be opened because of this reference.
        // another resource reference may cause it to be opened anyway.
        [Display("Ignored Instrument", Description:"This instrument is not opened because of this reference.")]
        public Instrument IgnoredInstrument { get; set; }
        
        public InstrumentTopology()
        {
            Name = "Topology Example";
        }

        // We don't need to override Open and Close, because our depending resources 
        // gets managed automatically.
        // public override void Open() { base.Open(); }
        // public override void Close() { base.Close(); }

        public string GetId()
        {
            if (UseScpiInstrument)
                return ScpiInst.IdnString;
            return IdInst.GetId();
        }
    }

    [Display("Topology Instrument Step", Groups: new[] { "Examples", "Plugin Development", "Instruments And Duts", "Resource Topology"})]
    public class TopologyInstrumentStep : TestStep
    {
        // ResourceOpenBehavior.Ignore can also be used in test steps when you don't want the 
        // resource manager to open a resource because of a given reference.
        //[ResourceOpen(ResourceOpenBehavior.Ignore)]
        public InstrumentTopology Instrument { get; set; }
        public override void Run()
        {
            Log.Info("Instrument ID: {0}", Instrument.GetId());
        }
    }
    
    /// <summary>  This is a simple interface that specifies an instrument that can get an ID.  </summary>
    public interface IIdInstrument : IInstrument
    {
        string GetId();
    }

    [Display("Simple ID Instrument", Groups: new[] { "Examples", "Plugin Development", "Resource Topology" })]
    public class SimpleIdInstrument : Instrument, IIdInstrument
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string GetId()
        {
            if (IsConnected == false)
                throw new Exception("This instrument has not been connected!");
            return Id;
        }

        public SimpleIdInstrument()
        {
            Name = "ID Inst";
        }
    }
}
