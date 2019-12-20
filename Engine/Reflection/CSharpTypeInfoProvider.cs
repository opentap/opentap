//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

namespace OpenTap
{
    /// <summary> Type data provider for .NET types. </summary>
    internal class CSharpTypeInfoProvider : ITypeDataProvider
    {
        /// <summary> The priority of this type info provider.  </summary>
        public double Priority => 0;
        /// <summary> Gets the C# type info for a string.  </summary>
        public ITypeData GetTypeData(string identifier)
        {
            
            var type = PluginManager.LocateType(identifier);
            if (type != null)
            {
                return TypeData.FromType(type);
            }
            return null;
        }

        /// <summary> Gets the C# type info for an object. </summary>
        public ITypeData GetTypeData(object obj)
        {
            var type = obj.GetType();
            return TypeData.FromType(type);
        }
    }
}
