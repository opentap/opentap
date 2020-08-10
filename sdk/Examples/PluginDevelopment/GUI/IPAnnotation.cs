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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Xml.Linq;

namespace OpenTap.Plugins.PluginDevelopment
{
    //
    // This example shows how to create a custom annotation for an IPAddress type
    // It will provide a way to convert from/to IPAddress/string, a way of showing parsing errors,
    // and a way of showing suggested values to the user.
    //
    // To create an annotation, two things are needed. The annotation itself and an IAnnotator implementation.
    // The IAnnotator is used to annotate data with the created annotations.
    //
    // Annotation types used here: 
    //             IStringValueAnnotation - Provides a way to convert from value to a string. This is used by the GUI to create a textbox.
    //             IErrorAnnotation - Provides a way to display data parsing errors to the user.
    //             ISuggestedValuesAnnotation - Provides a way to suggest values to the user. 
    // Other Annotation types available: 
    //             IAvailableValuesAnnotation, IMultiSelect  -  These are for dropdowns.
    //             IAccessAnnotation  - Read-Only and hide functionality.
    //             ICollectionAnnotation - collections of things. (advanced)
    //             IMembersAnnotation - members of objects.  (advanced)
    //             IOwnedAnnotation - annotation that has Read/Write functionality. (advanced)
    //             IMethodAnnotation - Functionality that can be invoked e.g using a button.
    //             IValueDescriptionAnnotation, IStringExampleAnnotation - Used for improved tooltips.
    //

    /// <summary> Annotate an IPAddress </summary>
    public class IPAnnotation : IStringValueAnnotation, IErrorAnnotation, ISuggestedValuesAnnotation
    {
        private AnnotationCollection annotations;

        public IPAnnotation(AnnotationCollection annotations)
        {
            this.annotations = annotations;
        }

        /// <summary> Implementing IStringValueAnnotation.Value. </summary>
        public string Value
        {
            get
            {
                // convert from the value to a string.
                var ip = (IPAddress) annotations.Get<IObjectValueAnnotation>().Value;
                if (ip == null) return "";
                return ip.ToString();
            }

            set
            {
                try
                {
                    IPAddress ip = IPAddress.Parse(value);
                    annotations.Get<IObjectValueAnnotation>().Value = ip;
                    error = null;
                }
                catch(Exception e)
                {
                    error = e.Message;
                }
            }
        }


        public string error;
        // Implementing IErrorAnnotation using this property.
        public IEnumerable<string> Errors
        {
            get { return error == null ? Array.Empty<string>() : new string[] { error }; }
        }
        
        /// <summary> Implementing ISuggestedValuesAnnotation.</summary>
        public IEnumerable SuggestedValues
        {
           get
           {
               yield return IPAddress.IPv6Loopback.ToString();
               yield return IPAddress.IPv6Any.ToString();
               yield return IPAddress.IPv6None.ToString();
               yield return IPAddress.Parse("127.0.0.1").ToString();
           }
        }
    }

    // To fully support IPaddresses, we also need to be able to serialize/deserialize them.
    // This plugin class takes care of that.
    public class IpAdressSerializer : ITapSerializerPlugin
    {
        public double Order => 5;

        public bool Deserialize(XElement node, ITypeData t, Action<object> setter)
        {
            if(t == TypeData.FromType(typeof(IPAddress)) == false) return false;
            IPAddress address;
            if (IPAddress.TryParse(node.Value, out address))
                setter(address);
            return true;
        }

        public bool Serialize(XElement node, object obj, ITypeData expectedType)
        {
            if(obj is IPAddress)
            {
                node.Value = obj.ToString();
                return true;
            }
            return false;
        }
    }

    /// <summary> For demonstration this step has a configurable IP address. </summary>
    [Display("Ping Step", "A step that pings an IP address and measures the round trip time.", Groups: new[] { "Examples", "Plugin Development", "GUI" })]
    public class PingStep : TestStep
    {
        public IPAddress IPAddress { get; set; } = IPAddress.IPv6Loopback;

        public override void Run()
        {
                var p1 = new System.Net.NetworkInformation.Ping();

                // Wait 5 s for a connection
                var reply = p1.Send(IPAddress);

                if (reply.Status == (IPStatus.Success))
                {
                    Log.Info("Address: {0}", IPAddress);
                    Log.Info("Got reply after: {0} ms", reply.RoundtripTime);

                    UpgradeVerdict(Verdict.Pass);
                }
                else
                {
                    Log.Info("No reply from " + IPAddress);
                    Log.Info("Reply Status: " + reply.Status);

                    UpgradeVerdict(Verdict.Fail);
                }
        }

        [Browsable(true)]
        public void ListAnnotators()
        {
            // it can sometimes be useful to list all plugins of a kind to see the order in which they are used.
            foreach (var controlProvider in PluginManager.GetPlugins<IAnnotator>().Select(x => (IAnnotator)Activator.CreateInstance(x)).ToArray().OrderBy(x => x.Priority))
            {
                Log.Info("{0} : {1}", controlProvider.Priority, controlProvider.ToString());
            }
        }

    }

    /// <summary> IpAnnotator is used to check when IPAnnotation is applicable and then apply it.</summary>
    public class IPAnnotator : IAnnotator
    {
        /// <summary>
        /// Priority is used to determine the order in which annotators are being used. 
        /// This can be important when layered annotaions are needed. In this case it should just have a relatively low value.
        /// </summary>
        public double Priority => 1;

        public void Annotate(AnnotationCollection annotations)
        {
            var member = annotations.Get<IMemberAnnotation>()?.Member;
            if (member == null) return;
            if (member.TypeDescriptor == TypeData.FromType(typeof(IPAddress)) == false) return;

            // now its known that it is an IPAddress
            annotations.Add(new IPAnnotation(annotations));

        }
    }
}
