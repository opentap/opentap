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

            // use DisplayAttribute if present
            var displayName = member.GetAttribute<DisplayAttribute>()?.Name;
            // Otherwise fall back to member name
            if (displayName == null)
                displayName = member.Name;

            var ui = new MixinBuilderUi(builders.ToArray(), src)
            {
                InitialMixinName = displayName
            };

            UserInput.Request(ui);

            if (ui.Submit == MixinBuilderUi.OkCancel.Cancel)
                return; // cancel

            var selectedMixinBuilder = ui.SelectedItem;
            var serializer = new TapSerializer();

            foreach (var src2 in Source)
            {
                // save the current value of the mixin so that it can be used
                // note that the type may have to change since ToDynamicMember may decide to return a new type of member.
                var currentValue = member.GetValue(src2);
                
                // make sure to unload the mixin before calling ToDynamicMember
                MixinFactory.UnloadMixin(src2, member);

                // Clone the mixin builder before applying it - Each mixin member keeps track of their own builder.
                var mem = serializer.Clone(selectedMixinBuilder).ToDynamicMember(TypeData.GetTypeData(src2));

                DynamicMember.AddDynamicMember(src2, mem);

                // if the saved value fits the member type, use it, Otherwise create a new value for it. 
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
