//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
namespace OpenTap
{
    /// <summary>   
    /// Specifies that a <see cref="TestStep"/> property is an output parameter. This property is expected to be set by <see cref="TestStep.Run"/>  
    /// Also specifies a property that can be selected as an <see cref="Input{T}"/>  to other TestSteps. 
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class OutputAttribute : Attribute
    {
        internal const OutputAvailability DefaultAvailability = OutputAvailability.AfterDefer;
        /// <summary> Specifies the availability of the output. The default behavior is AfterDefer. </summary>
        public OutputAvailability Availability { get; } = DefaultAvailability;

        /// <summary> Creates an instance of OutputAttribute with default values. </summary>
        public OutputAttribute() { }

        /// <summary> Creates an instance of OutputAttribute with OutputAvailability specified. </summary>
        public OutputAttribute(OutputAvailability availability = DefaultAvailability) => Availability = availability;
    }

    /// <summary> Specifies that a property of a step is also a result. </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ResultAttribute : Attribute
    {
        
    }

    /// <summary> Used to specify when an output value is available.</summary>
    public enum OutputAvailability
    {
        /// <summary> The output value is available before the step has run. This can also be interpreted as always available. </summary>
        BeforeRun,
        /// <summary> After this step is completed. This may occur before or after AfterChildDefer. </summary>
        AfterRun,
        /// <summary> After defer of this step. This is the default for Outputs. This occurs after 'AfterChildDefer' and 'AfterRun'. </summary>
        AfterDefer 
    }
}
