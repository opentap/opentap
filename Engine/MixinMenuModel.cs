using System.ComponentModel;
using System.Linq;
namespace OpenTap
{
    class MixinMenuModel: ITypeMenuModel
    {
        readonly ITypeData type;
        public object[] Source { get; set; }

        public MixinMenuModel(ITypeData type) => this.type = type;

        [Display("Add Mixin", "Add a new mixin to the item.", Order: 2.0)]
        [Browsable(true)]
        [IconAnnotation(IconNames.AddMixin)]
        public void AddMixin()
        {
            var builders = MixinFactory.GetMixinBuilders(type);
            // send the user request
            var ui = new MixinBuilderUi(builders.ToArray());
            UserInput.Request(ui);

            if (ui.Submit == MixinBuilderUi.OkCancel.Cancel)
                return; // cancel

            var selectedMixin = ui.SelectedItem;
            
            foreach (var src in Source)
            {
                var mem = selectedMixin.ToDynamicMember(type);
                
                DynamicMember.AddDynamicMember(src, mem);

                if (mem.TypeDescriptor.CanCreateInstance)
                {
                    var ins = mem.TypeDescriptor.CreateInstance();
                    mem.SetValue(src, ins);
                }
            }
        }
    }
}
