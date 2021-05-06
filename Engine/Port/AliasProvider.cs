using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap
{
    public class AliasProvider : IPropertyProvider
    {
        public IMemberData GetDataForType(ITypeData type)
        {
            var portType = TypeData.FromType(typeof(Port));
            if (type.DescendsTo(portType))
                return new AliasMember(portType);

            var viaType = TypeData.FromType(typeof(ViaPoint));
            if (type.DescendsTo(viaType))
                return new AliasMember(viaType);

            return null;
        }
    }

    class AliasMember : IMemberData
    {
        public AliasMember(TypeData parentType)
        {
            DeclaringType = parentType;
        }

        public ITypeData DeclaringType { get; private set; }

        public ITypeData TypeDescriptor => TypeData.FromType(typeof(string));

        public bool Writable => true;

        public bool Readable => true;

        public IEnumerable<object> Attributes => new object[]{ new DisplayAttribute("Alias") };

        public string Name => "Alias";

        readonly ConditionalWeakTable<object, object> dict = new ConditionalWeakTable<object, object>();

        public virtual void SetValue(object owner, object value)
        {
            dict.Remove(owner);
            if (Equals(value, default(string)) == false)
                dict.Add(owner, value);
        }

        public virtual object GetValue(object owner)
        {
            // TODO: use IDynamicMembersProvider
            if (dict.TryGetValue(owner, out object value))
                return value ?? default(string);
            return default(string);
        }
    }
}
