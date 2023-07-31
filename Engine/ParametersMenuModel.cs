using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OpenTap
{
    class TestStepMenuModel : IMemberMenuModel, ITestStepMenuModel, IMenuModelState
    {

        public static TestStepMenuModel FromSource(IMemberData member, object source)
        {
            if (source is ITestStepParent step)
                return new TestStepMenuModel(member, new [] {step});
            if (source is IEnumerable<object> array)
                return new TestStepMenuModel(member, array.OfType<ITestStepParent>().ToArray());
            return null;
            
        } 
        
        public TestStepMenuModel(IMemberData member) => this.member = member;

        public TestStepMenuModel(IMemberData member, ITestStepParent[] source)
        {
            this.member = member;
            this.source = source;
        }
        ITestStepParent[] source;
        readonly IMemberData member;
        object[] IMenuModel.Source { get => source; set => source = value?.OfType<ITestStepParent>().ToArray() ?? Array.Empty<ITestStepParent>(); }

        /// <summary> Multiple item can be selected at once. </summary>

        ITestStepParent[] ITestStepMenuModel.Source => source;

        public IMemberData Member => member;

        public bool CanExecuteParameterize => ParameterManager.CanParameter(this) && (IsAnyOutputAssigned == false);
        
        [EnabledIf(nameof(CanExecuteParameterize), true, HideIfDisabled = true)]
        [EnabledIf(nameof(TestPlanLocked), false)]
        [Browsable(true)]
        [IconAnnotation(IconNames.Parameterize)]
        [Display("Parameterize...", "Parameterize this setting by creating, or adding to, an existing parameter.", Order: 1.0)]
        public void Parameterize() => ParameterManager.CreateParameter(this, getCommonParent(), true);

        public bool HasTestPlanParent => source.FirstOrDefault()?.GetParent<TestPlan>() != null;
        public bool CanAutoParameterize => ParameterManager.CanAutoParameterize(this.member, this.source);

        [EnabledIf(nameof(CanExecuteParameterize), true, HideIfDisabled = true)]
        [EnabledIf(nameof(HasTestPlanParent), true, HideIfDisabled = true)]
        [EnabledIf(nameof(TestPlanLocked), false)]
        [EnabledIf(nameof(CanAutoParameterize), true)]
        [Browsable(true)]
        [IconAnnotation(IconNames.ParameterizeOnTestPlan)]
        [Display("Parameterize On Test Plan", "Parameterize this setting by creating, or adding to, an existing external test plan parameter.", Order: 1.0)]
        public void ParameterizeOnTestPlan()
        {
            var plan = source.FirstOrDefault().GetParent<TestPlan>();
            if (plan == null) return;
            ParameterManager.CreateParameter(this, plan, false);
        }
        
        // note: ParameterizeOnParent only works for things that does not have the test plan as a parent.
        // for steps with the test plan as a parent, ParameterizeOnTestPlan should be used.
        public bool HasSameParents => source.Select(x => x.Parent).OfType<ITestStep>().Distinct().Take(2).Count() == 1;
        public bool CanExecutedParameterizeOnParent => CanExecuteParameterize && HasSameParents; 
        [EnabledIf(nameof(CanExecutedParameterizeOnParent), true, HideIfDisabled = true)]
        [EnabledIf(nameof(TestPlanLocked), false)]
        [EnabledIf(nameof(CanAutoParameterize), true)]
        [Browsable(true)]
        [IconAnnotation(IconNames.ParameterizeOnParent)]
        [Display("Parameterize On Parent", "Parameterize this setting by creating, or adding to, an existing parameter.", Order: 1.0)]
        public void ParameterizeOnParent()
        {
            var parents = source.Select(x => x.Parent);
            if(parents.Distinct().Count() == 1)
                ParameterManager.CreateParameter(this, parents.FirstOrDefault(), false);
        }

        bool isParameterized() => source.Any(x => DynamicMember.IsParameterized(member, x));
        public bool IsParameterized => isParameterized();
        public bool IsParameter => member is ParameterMemberData;
        public bool MultiSelect => source?.Length != 1;

        public bool TestPlanLocked
        {
            get
            {
                var plan2 = source
                    .Select(step => step is TestPlan plan ? plan : step.GetParent<TestPlan>()).FirstOrDefault();
                return plan2.IsRunning || plan2.Locked;
            }
        }

        public bool CanExecuteUnparameterize => ParameterManager.CanUnparameter(this) && (IsAnyOutputAssigned == false);
        
        [EnabledIf(nameof(CanExecuteUnparameterize), true, HideIfDisabled = true)]
        [EnabledIf(nameof(IsParameterized), true, HideIfDisabled = true)]
        [EnabledIf(nameof(TestPlanLocked), false)]
        [Display("Unparameterize", "Removes the parameterization of this setting.", Order: 1.0)]
        [IconAnnotation(IconNames.Unparameterize)]
        [Browsable(true)]
        public void Unparameterize() => ParameterManager.Unparameterize(this);

        public bool CanEditParameter => ParameterManager.CanEditParameter(this);
        
        [Display("Edit Parameter", "Edit an existing parameterization.", Order: 1.0)]
        [EnabledIf(nameof(CanEditParameter), true, HideIfDisabled = true)]
        [Browsable(true)]
        [EnabledIf(nameof(TestPlanLocked), false)]
        [IconAnnotation(IconNames.EditParameter)]
        [EnabledIf(nameof(IsParameter), true, HideIfDisabled = true)]
        [EnabledIf(nameof(MultiSelect), false, HideIfDisabled = true)]
        public void EditParameter() => ParameterManager.EditParameter(this);

        public bool CanRemoveParameter => member is IParameterMemberData && source.All(x => x is TestPlan);
        
        [Browsable(true)]
        [Display("Remove Parameter", "Remove a parameter.", Order: 1.0)]
        [IconAnnotation(IconNames.RemoveParameter)]
        [EnabledIf(nameof(CanRemoveParameter), true, HideIfDisabled = true)]
        [EnabledIf(nameof(TestPlanLocked), false)]
        public void RemoveParameter() => ParameterManager.RemoveParameter(this);

        bool CalcAnyAvailableOutputs()
        {
            var scope = getCommonParent();
            if(scope == null) return false;
            return new[] {scope}.Concat(scope.GetParents())
                .SelectMany(x => AssignOutputDialog.GetAvailableOutputs(x, source, member.TypeDescriptor))
                .Any();
        }

        bool? anyAvailableOutputs;

        public bool AnyAvailableOutputs => (anyAvailableOutputs ?? (anyAvailableOutputs = CalcAnyAvailableOutputs())) ?? false;
        
        // Input/Output
        public bool CanAssignOutput => TestPlanLocked == false && source.Length > 0 && IsReadOnly == false && member.Writable && IsSweepable && !CanUnassignOutput && !IsParameterized && !IsAnyOutputAssigned;
        [Display("Assign Output", "Control this setting using an output.", Order: 2.0)]
        [Browsable(true)]
        [IconAnnotation(IconNames.AssignOutput)]
        [EnabledIf(nameof(CanAssignOutput), true, HideIfDisabled = true)]
        [EnabledIf(nameof(AnyAvailableOutputs), true)]
        public void ControlUsingOutput()
        {
            var commonParent = getCommonParent();
            var question = new AssignOutputDialog(member, commonParent, source);
            UserInput.Request(question);
            if (question.Response == ParameterManager.OkCancel.Cancel)
                return;
            var outputObject = question.Output?.Step;
            var outputMember = question.Output?.Member;
            if (outputObject != null && outputMember != null)
            {
                foreach(var srcItem in source)
                    InputOutputRelation.Assign(srcItem, member, outputObject, outputMember);
            }
        }

        ITestStepParent getCommonParent()
        {
            // If the parents are as such:
            // A B C D [step a]
            // A B E F [step b]
            // A B C G H [step c]
            // A ist the root (Test Plan). The first common parent is then B.
            // notice the first two parents are the same for all, so the first common
            // parent is B.
            // Zip is being used to fetch only the similar ones.
            
            var parentlists = source
                .Select(x => (x?.GetParents() ?? Array.Empty<ITestStepParent>()).Reverse());
            return parentlists.Aggregate((x, y) => x.Zip(y, (x1,y1) => x1 == y1 ? x1 : null)
                    .Where(x2 => x2 != null).ToArray())
                .LastOrDefault();
        }
        public bool IsSweepable => member.HasAttribute<UnsweepableAttribute>() == false;

        public bool IsReadOnly => source.Length > 0 && source?.Any(p => p is TestStep t && t.IsReadOnly) == true;
        
        public bool IsAnyOutputAssigned => source.Any(x => InputOutputRelation.IsInput(x, member));
        
        public bool IsAnyInputAssigned => source.Any(x => InputOutputRelation.IsOutput(x, member));

        public bool IsOutput => member.HasAttribute<OutputAttribute>();
        
        public bool CanUnassignOutput => TestPlanLocked == false && source.Length > 0 && IsReadOnly == false && member.Writable && IsAnyOutputAssigned;
        [Display("Unassign Output", "Unassign the output controlling this property.", Order: 2.0)]
        [Browsable(true)]
        [IconAnnotation(IconNames.UnassignOutput)]
        [EnabledIf(nameof(CanUnassignOutput), true, HideIfDisabled = true)]
        public void UnassignOutput()
        {
            foreach (var step in source)
            {
                var con2 = InputOutputRelation.GetRelations(step)
                    .FirstOrDefault(con => con.InputMember == member && con.InputObject == step);
                if(con2 != null)
                    InputOutputRelation.Unassign(con2);
            }
        }

        bool IMenuModelState.Enabled => (source?.Length ?? 0) > 0;
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