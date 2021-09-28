using System;
using System.Collections.Generic;

namespace OpenTap
{
    /// <summary>  A type data representing null values. </summary>
    sealed class NullTypeData : ITypeData
    {
        internal static readonly ITypeData Instance = new NullTypeData();
        public IEnumerable<object> Attributes => Array.Empty<object>();
        public string Name => "Null";
        public ITypeData BaseType => null;
        public IEnumerable<IMemberData> GetMembers() => Array.Empty<IMemberData>();
        public IMemberData GetMember(string name) => null;
        public object CreateInstance(object[] arguments) => null;
        public bool CanCreateInstance => true;
        NullTypeData(){}
    }
}