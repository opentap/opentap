//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{
    /// <summary> Marks a property on a test step that should be fixed during test plan execution. This means it should be excluded from e.g. sweep loops.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class UnsweepableAttribute : Attribute { }
}
