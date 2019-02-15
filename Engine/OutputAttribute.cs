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

    }
}
