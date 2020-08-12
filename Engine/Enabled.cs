//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections;
using System.Linq;
using System.Globalization;

namespace OpenTap
{
    /// <summary>
    /// Interface for types that can be enabled.
    /// </summary>
    public interface IEnabled
    {
        /// <summary>
        /// Gets whether a type is enabled.
        /// </summary>
        bool IsEnabled { get; }
    }

    interface IEnabledValue
    {
        bool IsEnabled { get; set; }
        object Value { get; set; }
    }
    
    /// <summary>
    /// A value that can be enabled or disabled.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Enabled<T> : IEnabled, ICloneable, IEnabledValue
    {
        /// <summary>
        /// Value of the member.
        /// </summary>
        public virtual T Value { get; set; }

        object IEnabledValue.Value
        {
            get => Value;
            set => Value = (T) value;
        }
        
        /// <summary>
        /// Gets or sets if the member is enabled. 
        /// </summary>
        public virtual bool IsEnabled { get; set; }

        /// <summary>
        /// Writes a special string if the value is not enabled. Otherwise just returns Value.ToString.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string valueString = null;
            if (Value == null)
                valueString = "NULL";
            else
                valueString = StringConvertProvider.GetString(Value);

            if (!IsEnabled)
                valueString += " (disabled)";

            return valueString;
        }

        /// <summary>
        /// Creates a clone of this object.
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            T value = Value;
            if(Value is ICloneable)
                value = (T)((ICloneable)Value).Clone();

            return new Enabled<T> { Value = value, IsEnabled = IsEnabled };
        }
    }
}
