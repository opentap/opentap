//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary>
    /// Base class for resources. Specializations include Dut, Instrument and ResultListener.
    /// </summary>
    public abstract class Resource : ValidatingObject, IResource, INotifyActivity
    {

        /// <summary>
        /// Default log that the resource object can write to.  Typically used by instances and extensions of the Resource object.
        /// </summary>
        [XmlIgnore]
        public TraceSource Log
        {
            get;
            private set;
        }

        /// <summary>
        /// Instantiate a new instance of <see cref="Resource">Resource</see> class and creates logging source.
        /// </summary>
        public Resource()
        {
            Name = "N/A";
        }
        
        string _name = "";

        /// <summary>
        /// A short name displayed in the user interface where space is limited.
        /// </summary>
        [Display("Name", Group: "Common", Order: -3)]
        [Browsable(false)]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name != value)
                {
                    if (value == null)
                        throw new ArgumentNullException("Name");
                    _name = value;
                    if (Log != null)
                    {
                        OpenTap.Log.RemoveSource(Log);
                    }
                    Log = OpenTap.Log.CreateSource(_name, this);
                    OnPropertyChanged("Name");
                }
            }
        }

        /// <summary>
        /// Overrides ToString() to return the Name of the resource. Can be overridden by derived classes to provider a more descriptive name. Note the overrider should include the Name in the output.
        /// </summary>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// When overridden in a derived class, should contain implementation to open a connection to the resource represented by this class.
        /// Any one time initialization should be done here as well. 
        /// </summary>
        public virtual void Open()
        {
            IsConnected = true;
        }

        /// <summary>
        /// When overridden in a derived class, should contain implementation to close the connection made to the resource represented by this class.
        /// </summary>
        public virtual void Close()
        {
            IsConnected = false;
        }

        /// <summary>
        /// Invoked on activity.
        /// </summary>
        public event EventHandler<EventArgs> Activity;
        /// <summary>
        /// Triggers the ActivityStateChanged event.
        /// </summary>
        public void OnActivity()
        {
            if (Activity != null)
            {
                Activity.Invoke(this, new EventArgs());
            }
        }
        
        private bool isConnected = false;
        /// <summary>
        /// Indicates whether this resource is currently connected.
        /// This value should be set by Open() and Close().
        /// </summary>
        [XmlIgnore]
        [Browsable(false)]
        public bool IsConnected
        {
            get { return isConnected; }
            set
            {
                isConnected = value;
                OnPropertyChanged("IsConnected");
            }
        }

    }

    /// <summary>
    /// Indicates that a property contains metadata which may be saved into results.
    /// </summary>
    /// <remarks>
    /// ResultListeners can use this attribute to determine whether to save a property.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public class MetaDataAttribute : Attribute
    {
        /// <summary>
        /// Contains metadata for one property.
        /// </summary>
        public struct MetaDataParameter
        {
            /// <summary>
            /// Metadata name. Always property name.
            /// </summary>
            public readonly string Name;
            /// <summary>
            /// Metadata value. Property value.
            /// </summary>
            public readonly object Value;
            /// <summary>Constructor for MetaDataParameter.</summary>
            /// <param name="name"></param>
            /// <param name="value"></param>
            public MetaDataParameter(string name, object value)
            {
                Name = name;
                Value = value;
            }
        }

        /// <summary> Constructor for MetaDataAttribute. </summary>
        /// <param name="promptUser">The options for use with MetaData.</param>
        /// <param name="macroName">The text for use as macroname if the Macro option is selected.</param>
        public MetaDataAttribute(bool promptUser = false, string macroName = null)
        {
            this.PromptUser = promptUser;
            this.MacroName = macroName;
        }
        
        /// <summary> Which options are enabled for this attribute. </summary>
        public bool PromptUser { get; private set; }

        /// <summary> Name of the macro. </summary>
        public string MacroName { get; private set; }

        /// <summary>
        /// Gets the name and value for each metadata property in this object that is not null.
        /// </summary>
        /// <returns>Name and value for each metadata property in this
        /// object that is not null as a MetaDataParameter object.</returns>
        public static List<MetaDataParameter> GetMetaDataParameters(object obj)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");
            var metadata = ResultParameters.GetMetadataFromObject(obj);
            return metadata.Select(meta => new MetaDataParameter(meta.Name, meta.Value)).ToList();
        }
    }
}
