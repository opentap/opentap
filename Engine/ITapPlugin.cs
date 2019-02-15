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
    /// Base type for all TAP plugins.
    /// </summary>
    public interface ITapPlugin { }

    /// <summary>
    /// Extension class for Type. 
    /// </summary>
    public static class TapPluginExtensions
    {

        static void _getPluginType(HashSet<Type> foundPluginTypes, Type typeToCheck)
        {
            if (foundPluginTypes.Contains(typeToCheck))
                return; // Type already checked.
            IEnumerable<Type> other = typeToCheck.GetInterfaces();
            if (other.Contains(typeof(ITapPlugin)) == false)
                return;
            if (typeToCheck.BaseType != null)
                other = other.Append(typeToCheck.BaseType);

            var otherType = other.Where(x => x != typeof(ITapPlugin) && x.HasInterface<ITapPlugin>());

            if (otherType.Any() == false) {
                foundPluginTypes.Add(typeToCheck);
                return;
            }

            foreach(var t in otherType)
            {   // Recurse to check sub types.
                _getPluginType(foundPluginTypes, t);
            }
            
        }

        /// <summary>
        /// Gets the plugin types that a type implements.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Type[] GetPluginType(this Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");
            var outTypes = new HashSet<Type>();
            _getPluginType(outTypes, type);
            return outTypes.ToArray();
        }
    }
}
