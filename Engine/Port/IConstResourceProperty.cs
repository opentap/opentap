//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    /// <summary>
    /// An immutable/constant object that is a property on a Resource. When deserializing references to objects of this type, 
    /// <see cref="TapSerializer"/> makes sure that all references refer to the same unique instance on the specific instrument.
    /// </summary>
    public interface IConstResourceProperty
    {
        /// <summary>
        /// TDevice/resource to which this object belongs.  
        /// </summary>
        /// <remarks>
        /// All devices should be marked with the <see cref="System.Xml.Serialization.XmlIgnoreAttribute"/>.
        /// </remarks>
        [System.Xml.Serialization.XmlIgnore]
        IResource Device { get; }
        /// <summary>
        /// Name of this object (should be unique among objects of the same type on the same device/resource).  
        /// </summary>
        string Name { get; }
    }

    /// <summary>
    /// Helper and extension methods for IConstResourceProperty.
    /// </summary>
    public static class IConstResourcePropertyHelpers
    {
        /// <summary>
        /// Returns all IConstResourceProperty instances of a specific type defined on this resource.
        /// </summary>
        public static IEnumerable<T> GetConstProperties<T>(this IResource res) where T : IConstResourceProperty
        {
            if(res == null)
                throw new ArgumentNullException("res");
            return res.GetConstProperties().OfType<T>();
        }

        /// <summary>
        /// Returns all IConstResourceProperty instances on a resource.
        /// </summary>
        /// <param name="res"></param>
        /// <returns></returns>
        public static IEnumerable<IConstResourceProperty> GetConstProperties(this IResource res)
        {
            foreach (var prop in res.GetType().GetPropertiesTap())
            {
                if (prop.PropertyType.DescendsTo(typeof(IConstResourceProperty)))
                {
                    var value = (IConstResourceProperty)prop.GetValue(res, null);
                    if (value == null) continue;

                    yield return value;
                }
                if (prop.PropertyType.DescendsTo(typeof(IEnumerable<IConstResourceProperty>)))
                {
                    var points = (IEnumerable<IConstResourceProperty>)prop.GetValue(res, null);
                    if (points == null) continue;
                    foreach (var pt in points)
                    {
                        if (pt == null) continue;

                        yield return pt;
                    }
                }
            }
        }

    }
}
