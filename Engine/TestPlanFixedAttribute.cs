//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{
    /// <summary> Can be used to mark a property that should should not be changed during test plan execution or that should generally be excluded from automation. </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class TestPlanFixedAttribute : Attribute { }
}
