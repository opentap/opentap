using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace OpenTap
{
    class MixinBuilderUiTypeData : ITypeData
    {
        private readonly ImmutableArray<IMemberData> members;

        class WrappedMemberInfo : IMemberData
        {
            public override string ToString() => Name; 
            private readonly Attribute[] attributes;
            private readonly object src;
            private readonly IMemberData member;

            public WrappedMemberInfo(object src, IMemberData member, params Attribute[] attributes)
            {
                this.attributes = attributes;
                this.src = src;
                this.member = member;
                Name = $"{src}.{member.Name}";
            }

            public IEnumerable<object> Attributes => attributes.Concat(member.Attributes);
            public string Name { get; }
        
            public ITypeData DeclaringType => member.DeclaringType;
            public ITypeData TypeDescriptor => member.TypeDescriptor;
            public bool Writable => member.Writable;
            public bool Readable => member.Readable;
            public void SetValue(object owner, object value) => member.SetValue(src, value);
            public object GetValue(object owner) => member.GetValue(src);
        }
        
        public MixinBuilderUiTypeData(MixinBuilderUi builder)
        {
            List<IMemberData> members = new List<IMemberData>();
            foreach (var item in builder.Items)
            {
                foreach (var member in TypeData.GetTypeData(item).GetMembers())
                {
                    var em = new EnabledIfAttribute(nameof(MixinBuilderUi.SelectedItem), item) {HideIfDisabled = true};
                    var newMember = new WrappedMemberInfo(item, member, em);
                    members.Add(newMember);
                }
            }

            this.members = members.ToImmutableArray();
        }

        public IEnumerable<object> Attributes { get; } = Array.Empty<object>();
        public string Name { get; } = "Mixin";
        public ITypeData BaseType { get; } = TypeData.FromType(typeof(MixinBuilderUi));
        public IEnumerable<IMemberData> GetMembers() => BaseType.GetMembers().Concat(members);

        public IMemberData GetMember(string name) => members.FirstOrDefault(x => x.Name == Name) ?? BaseType.GetMember(name);
        
        public object CreateInstance(object[] arguments)
        {
            throw new NotSupportedException();
        }

        public bool CanCreateInstance { get; } = false;
    }
}