using System.ComponentModel;
using System.Linq;

namespace OpenTap
{
    
    class TestStepMenuModel : IMenuModel, ITestStepMenuModel
    {
        public TestStepMenuModel(IMemberData member) => this.member = member;
        ITestStepParent[] source;
        readonly IMemberData member;
        object[] IMenuModel.Source { get => source; set => source = value.OfType<ITestStepParent>().ToArray(); }

        /// <summary> Multiple item can be selected at once. </summary>

        ITestStepParent[] ITestStepMenuModel.Source => source;

        IMemberData ITestStepMenuModel.Member => member;

        public bool CanExecuteParameterize => ParameterManager.CanParameter(this);
        
        [EnabledIf(nameof(CanExecuteParameterize), true, HideIfDisabled = true)]
        [EnabledIf(nameof(TestPlanLocked), false)]
        [Browsable(true)]
        [IconAnnotation(IconNames.Parameterize)]
        [Display("Parameterize...", "Parameterize this setting by creating, or adding to, an existing parameter.", Order: 1.0)]
        public void Parameterize() => ParameterManager.CreateParameter(this, source.FirstOrDefault()?.Parent, true);

        public bool HasTestPlanParent => source.FirstOrDefault()?.GetParent<TestPlan>() != null;

        [EnabledIf(nameof(CanExecuteParameterize), true, HideIfDisabled = true)]
        [EnabledIf(nameof(HasTestPlanParent), true, HideIfDisabled = true)]
        [EnabledIf(nameof(TestPlanLocked), false)]
        [Browsable(true)]
        [IconAnnotation(IconNames.ParameterizeOnTestPlan)]
        [Display("Parameterize On Test Plan", "Parameterize this setting by creating, or adding to, an existing external test plan parameter.", Order: 1.0)]
        public void ParameterizeOnTestPlan()
        {
            var plan = source.FirstOrDefault().GetParent<TestPlan>();
            if (plan == null) return;
            ParameterManager.CreateParameter(this, plan, false);
        }
        
        public bool HasSameParents => source.Select(x => x.Parent).OfType<ITestStep>().Distinct().Take(2).Count() == 1;
        public bool CanExecutedParameterizeOnParent => CanExecuteParameterize && HasSameParents; 
        [EnabledIf(nameof(CanExecutedParameterizeOnParent), true, HideIfDisabled = true)]
        [EnabledIf(nameof(TestPlanLocked), false)]
        [Browsable(true)]
        [IconAnnotation(IconNames.ParameterizeOnParent)]
        [Display("Parameterize On Parent", "Parameterize this setting by creating, or adding to, an existing parameter.", Order: 1.0)]
        public void ParameterizeOnParent()
        {
            var parents = source.Select(x => x.Parent);
            if(parents.Distinct().Count() == 1)
                ParameterManager.CreateParameter(this, parents.FirstOrDefault(), false);
        }

        bool isParameterized(ITestStepParent item) => item.GetParents().Any(parent =>
            TypeData.GetTypeData(parent).GetMembers().OfType<ParameterMemberData>()
                .Any(x => x.ParameterizedMembers.Contains((item, Member: member))));
        
        bool isParameterized() =>  source.Any(isParameterized);
        public bool IsParameterized => isParameterized();
        public bool IsParameter => member is ParameterMemberData;

        public bool TestPlanLocked
        {
            get
            {
                var plan2 = source
                    .Select(step => step is TestPlan plan ? plan : step.GetParent<TestPlan>()).FirstOrDefault();
                return plan2.IsRunning || plan2.Locked;
            }
        }

        [EnabledIf(nameof(IsParameterized), true, HideIfDisabled = true)]
        [EnabledIf(nameof(TestPlanLocked), false)]
        [Display("Unparameterize", "Removes the parameterization of this setting.", Order: 1.0)]
        [IconAnnotation(IconNames.Unparameterize)]
        [Browsable(true)]
        public void Unparameterize() => ParameterManager.RemoveParameter(this);

        [Display("Edit Parameter", "Edit an existing parameterization.", Order: 1.0)]
        [Browsable(true)]
        [EnabledIf(nameof(TestPlanLocked), false)]
        [IconAnnotation(IconNames.EditParameter)]
        [EnabledIf(nameof(IsParameter), true, HideIfDisabled = true)]
        public void EditParameter() => ParameterManager.EditParameter(this);
    }
    
    class TestStepMenuItemsModelFactory : IMenuModelFactory
    {
        public IMenuModel CreateModel(IMemberData member)
        {
            if(member.DeclaringType.DescendsTo(typeof(ITestStepParent)))
                return new TestStepMenuModel(member);
            return null;
        }
    }
}