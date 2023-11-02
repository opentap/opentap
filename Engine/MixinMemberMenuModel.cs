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

        IMemberData IMemberMenuModel.Member => member;
        
        public bool TestPlanLocked
        {
            get
            {
                var plan2 = Source.OfType<ITestStepParent>()
                    .Select(step => step is TestPlan plan ? plan : step.GetParent<TestPlan>()).FirstOrDefault();
                return (plan2?.IsRunning ?? false) || (plan2?.Locked ?? false);
            }
        }

        public bool StepLocked => Source.OfType<ITestStep>().Any(x => x.ChildTestSteps.IsReadOnly);
        
        [Display("Modify Mixin...", "Modify custom setting.", Order: 2.0, Group: "Mixins")]
        [Browsable(true)]
        [IconAnnotation(IconNames.ModifyMixin)]
        [EnabledIf(nameof(TestPlanLocked), false)]
        [EnabledIf(nameof(StepLocked), false, HideIfDisabled = true)]
        public void ModifyMixin()
        {
            var src = member.Source;
            var targetType = TypeData.GetTypeData(Source.First());
            var builders = MixinFactory.GetMixinBuilders(targetType).ToImmutableArray();
            builders = builders.Replace(builders.FirstOrDefault(x => x.GetType() == src.GetType()), src);
            src.Initialize(targetType);

            var ui = new MixinBuilderUi(builders.ToArray(), src)
            {
                InitialMixinName = member.Name
            };
            
            UserInput.Request(ui);

            if (ui.Submit == MixinBuilderUi.OkCancel.Cancel)
                return; // cancel

            var selectedMixin = ui.SelectedItem;
            var serializer = new TapSerializer();

            foreach (var src2 in Source)
            {
                var mem = serializer.Clone(selectedMixin).ToDynamicMember(targetType);

                var remMember = member;
                var currentValue = remMember.GetValue(src2);
                MixinFactory.UnloadMixin(src2, remMember);

                DynamicMember.AddDynamicMember(src2, mem);


                if (currentValue != null && TypeData.GetTypeData(currentValue).DescendsTo(mem.TypeDescriptor))
                {
                    mem.SetValue(src2, currentValue);
                }
                else
                {
                    mem.SetValue(src2, mem.NewInstance());
                }
            }
        }

        [Display("Remove Mixin", "Remove custom setting.", Order: 2.0, Group: "Mixins")]
        [Browsable(true)]
        [IconAnnotation(IconNames.RemoveMixin)]
        [EnabledIf(nameof(TestPlanLocked), false)]
        [EnabledIf(nameof(StepLocked), false, HideIfDisabled = true)]
        public void RemoveMixin()
        {
            foreach (var src in Source)
                MixinFactory.UnloadMixin(src, member);
        }
    }
}
