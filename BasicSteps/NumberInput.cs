//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap;
using System;
using System.Globalization;

namespace OpenTap.Plugins.BasicSteps
{
    /// <summary> Inputs that can be any numbers. </summary>
    public class NumberInput : Input<IConvertible>, IInputTypeRestriction
    {
        /// <summary> Implementation of input type restrictions. This returns true if its a number type. </summary>
        /// <param name="concreteType"></param>
        /// <returns></returns>
        public override bool SupportsType(ITypeData concreteType)
        {
            if (concreteType is TypeData cst)
            {
                if (!base.SupportsType(concreteType)) return false;
                return cst.Type.IsNumeric();
            }
            return false;
        }
    }
}
