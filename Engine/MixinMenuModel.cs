using System.ComponentModel;
using System.Linq;
namespace OpenTap
{
    class MixinMenuModel: ITypeMenuModel
    {
        readonly ITypeData type;

        public MixinMenuModel(ITypeData type) => this.type = type;
        bool? showMixins;
        public bool ShowMixins => showMixins ??= MixinFactory.GetMixinBuilders(type).Any();
        
        [Display("Add Mixin", "Add a new mixin.", Order: 2.0)]
        [Browsable(true)]
        [IconAnnotation(IconNames.AddMixin)]
        [EnabledIf(nameof(ShowMixins), true, HideIfDisabled = true)]
        public void AddMixin()
        {
            var builders = MixinFactory.GetMixinBuilders(type);
            // send the user request
            var ui = new MixinBuilderUi(builders.ToArray());
            UserInput.Request(ui);

            if (ui.Submit == MixinBuilderUi.OkCancel.Cancel)
                return; // cancel

            var selectedMixin = ui.SelectedItem;
            
            foreach (var src in source)
                MixinFactory.LoadMixin(src, selectedMixin.Clone());
        }

        object[] source;
        object[] IMenuModel.Source
        {
            get => source;
            set => source = value;
        }
    }
}
