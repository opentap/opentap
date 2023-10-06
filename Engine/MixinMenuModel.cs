using System.ComponentModel;
using System.Linq;
namespace OpenTap
{
    class MixinMenuModel: ITypeMenuModel
    {
        readonly ITypeData type;

        public bool TestPlanLocked
        {
            get
            {
                var plan2 = source.OfType<ITestStep>()
                    .Select(step => step is TestPlan plan ? plan : step.GetParent<TestPlan>()).FirstOrDefault();
                return (plan2?.IsRunning ?? false) || (plan2?.Locked ?? false);
            }
        }
        
        public MixinMenuModel(ITypeData type) => this.type = type;
        bool? showMixins;
        public bool ShowMixins => (showMixins ??= (MixinFactory.GetMixinBuilders(type).Any()))&& !TestPlanLocked;
        
        [Display("Add Mixin...", "Add a new mixin.", Order: 2.0, Group: "Mixins")]
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
    
            bool first = true;
            TapSerializer serializer = null;
            foreach (var src in source)
            {
                if (first)
                    first = false;
                else
                    selectedMixin = Utils.Clone(selectedMixin, ref serializer);
                
                MixinFactory.LoadMixin(src, selectedMixin);
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
