using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
namespace OpenTap
{
    class MixinMemberMenuModel : IMenuModel, IMemberMenuModel
    {
        readonly MixinMemberData member;
        public MixinMemberMenuModel(MixinMemberData member)
        {
            this.member = member;
        }

        public object[] Source { get; set; }
        
        IMemberData IMemberMenuModel.Member { get;}
        
        [Display("Modify Mixin", "Modify custom setting.", Order: 2.0, Group: "Mixins")]
        [Browsable(true)]
        [IconAnnotation(IconNames.ModifyMixin)]
        public void ModifyMixin()
        {
            var src = member.Source;
            var targetType = TypeData.GetTypeData(Source.First());
            var builders = MixinFactory.GetMixinBuilders(targetType).ToImmutableArray();
            builders = builders.Replace(builders.FirstOrDefault(x => x.GetType() == src.GetType()), src);
            src.Initialize(targetType);

            var ui = new MixinBuilderUi(builders.ToArray(), src);
            UserInput.Request(ui);

            if (ui.Submit == MixinBuilderUi.OkCancel.Cancel)
                return; // cancel

            var selectedMixin = ui.SelectedItem;
            TapSerializer serializer = null;
            
            bool first = true;
            foreach (var src2 in Source)
            {
                if (first)
                    first = false;
                else
                    selectedMixin = Utils.Clone(selectedMixin, ref serializer);
                
                var mem = selectedMixin.ToDynamicMember(targetType);
                
                var remMember = member;
                var currentValue = remMember.GetValue(src2);
                DynamicMember.RemoveDynamicMember(src2, remMember);

                DynamicMember.AddDynamicMember(src2, mem);
                

                if (currentValue != null && TypeData.GetTypeData(currentValue).DescendsTo(mem.TypeDescriptor))
                {
                    mem.SetValue(src2, currentValue);
                } else
                {
                    mem.SetValue(src2, mem.NewInstance());
                }
            }
        }

        [Display("Remove Mixin", "Remove custom setting.", Order: 2.0, Group: "Mixins")]
        [Browsable(true)]
        [IconAnnotation(IconNames.RemoveMixin)]
        public void RemoveMixin()
        {
            foreach (var src in Source)
                DynamicMember.RemoveDynamicMember(src, member);
        }
    }
}
