using System.ComponentModel;
using System.Linq;
namespace OpenTap
{
    class MixinMenuModel: ITypeMenuModel
    {
        readonly ITypeData type;

        public bool TestPlanAllowsEdit => source
            .OfType<ITestStepParent>()
            .FirstNonDefault(step => step as TestPlan ?? step.GetParent<TestPlan>())
            ?.AllowEdit ?? true;
        
        public MixinMenuModel(ITypeData type) => this.type = type;
        bool? showMixins;
        public bool ShowMixins => (showMixins ??= (MixinFactory.GetMixinBuilders(type).Any())) && TestPlanAllowsEdit;
        public bool StepLocked => source.OfType<ITestStep>().Any(x => x.IsReadOnly);
        
        [Display("Add Mixin...", "Add a new mixin.", Order: 2.0, Group: "Mixins")]
        [Browsable(true)]
        [IconAnnotation(IconNames.AddMixin)]
        [EnabledIf(nameof(ShowMixins), true, HideIfDisabled = true)]
        [EnabledIf(nameof(StepLocked), false, HideIfDisabled = true)]
        
        public void AddMixin()
        {
            var builders = MixinFactory.GetMixinBuilders(type);
            
            // send the user request
            var ui = new MixinBuilderUi(builders.ToArray()) { AddMode = true };
            
            UserInput.Request(ui);

            if (ui.Submit == MixinBuilderUi.OkCancel.Cancel)
                return; // cancel

            var selectedMixin = ui.SelectedItem;
    
            var serializer = new TapSerializer();
            foreach (var src in source)
            {
                MixinFactory.LoadMixin(src, serializer.Clone(selectedMixin));
            }
        }

        object[] source;
        object[] IMenuModel.Source
        {
            get => source;
            set => source = value;
        }
    }
}
