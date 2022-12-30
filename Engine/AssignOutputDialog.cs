using System.Linq;

namespace OpenTap
{
    class AssignOutputDialog : ValidatingObject
    {
        public struct ScopeItem
        {
            public ITestStepParent Scope;
            public override string ToString()
            {
                if (Scope is TestPlan) return "Test Plan";
                if (Scope is ITestStep step) return step.GetFormattedName();
                return Scope?.ToString() ?? "";
            }

            static public ScopeItem Create(ITestStepParent item) => new ScopeItem() {Scope = item};
        }

        public string Name => "Please select an output.";
        public ScopeItem[] AvailableScopes => new[]{initScope}.Concat(initScope.GetParents()).Select(ScopeItem.Create).ToArray();

        [AvailableValues(nameof(AvailableScopes))]
        [Display(nameof(Scope), "The scope at which the output will be selected.", Order: 0)]
        public ScopeItem Scope { get => ScopeItem.Create(scope);
            set
            {
                scope = value.Scope;
                if (AvailableOutputs.Contains(Output) == false)
                {
                    Output = AvailableOutputs.FirstOrDefault() ?? Output;
                }
            } 
        }

        ITestStepParent scope;
        public class SelectedOutputItem
        {
            public readonly ITestStepParent Step;
            public readonly IMemberData Member;

            SelectedOutputItem(ITestStepParent testStepParent, IMemberData mem)
            {
                Step = testStepParent;
                Member = mem;
            }

            public static SelectedOutputItem Create(ITestStepParent testStepParent, IMemberData mem)
            {
                return new SelectedOutputItem(testStepParent, mem);
            }

            public override string ToString() => $"{Member.GetDisplayAttribute().Name} from {(Step as ITestStep).GetFormattedName() ?? "plan"}";

            public override bool Equals(object obj)
            {
                if (obj is SelectedOutputItem sel)
                    return sel.Step == Step && sel.Member == Member;
                return false;
            }

            public override int GetHashCode() => HashCode.Combine(Step,  Member, 7730122);
        }

        static bool CanAssignOutputType(ITypeData inputType, ITypeData outputType)
        {
            if (Equals(inputType, outputType))
                return true;
            var tdi = outputType.AsTypeData();
            var tdo = outputType.AsTypeData();
            if (tdi != null && tdo != null)
            {
                if (tdi.IsNumeric && tdo.IsNumeric)
                    return true;
            }
            return false;
        }
        
        public static SelectedOutputItem[] GetAvailableOutputs(ITestStepParent scope, ITestStepParent[] steps, ITypeData outputType)
        {
            var list = scope.ChildTestSteps
                .SelectMany(childStep =>
                {
                    return TypeData.GetTypeData(childStep).GetMembers()
                        .Where(y => y.HasAttribute<OutputAttribute>() && CanAssignOutputType(y.TypeDescriptor, outputType))
                        .Select(mem => SelectedOutputItem.Create(childStep, mem));
                })
                .Where(item => steps.Contains(item.Step) == false)
                .ToList();
            
            // Add the current scope
            list.AddRange(TypeData.GetTypeData(scope).GetMembers()
                .Where(y => y.HasAttribute<OutputAttribute>() && y.TypeDescriptor == outputType)
                .Select(mem => SelectedOutputItem.Create(scope, mem)));

            return list.ToArray();
        }

        public SelectedOutputItem[] GetAvailableOutputs() => GetAvailableOutputs(scope, steps, inputMember.TypeDescriptor);

        public SelectedOutputItem[] AvailableOutputs => GetAvailableOutputs(); 

        [AvailableValues(nameof(AvailableOutputs))]
        
        [Display(nameof(Output), "The output property selected." , Order: 1)]
        public SelectedOutputItem Output { get; set; }
        
        [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
        [Submit]
        public ParameterManager.OkCancel Response { get; set; }

        ITestStepParent[] steps;
        IMemberData inputMember;
        ITestStepParent initScope;
        public AssignOutputDialog(IMemberData member, ITestStepParent initScope, ITestStepParent[] steps)
        {
            this.steps = steps;
            inputMember = member;
            this.initScope = initScope;
            scope = initScope;
            
            Output = AvailableOutputs.FirstOrDefault();
        }
    }
}