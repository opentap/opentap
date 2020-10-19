using System.Linq;

namespace OpenTap
{
    class AssignOutputDialog : ValidatingObject
    {

        public string Name => "Please select an output.";
        public ITestStepParent[] AvailableScopes => step.GetParents().ToArray();

        [AvailableValues(nameof(AvailableScopes))]
        [Display(nameof(Scope), "The scope at which the output will be selected.")]
        public ITestStepParent Scope { get => scope;
            set
            {
                scope = value;
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

        public static SelectedOutputItem[] GetAvailableOutputs(ITestStepParent scope, ITestStepParent step, ITypeData outputType)
        {
            return scope.ChildTestSteps

                .SelectMany(childStep =>
                {
                    return TypeData.GetTypeData(childStep).GetMembers()
                        .Where(y => y.HasAttribute<OutputAttribute>() && y.TypeDescriptor == outputType)
                        .Select(mem => SelectedOutputItem.Create(childStep, mem));
                })
                .Where(item => item.Step != step)
                .ToArray();
        }

        public SelectedOutputItem[] GetAvailableOutputs() => GetAvailableOutputs(Scope, step, inputMember.TypeDescriptor);

        public SelectedOutputItem[] AvailableOutputs => GetAvailableOutputs(); 

        [AvailableValues(nameof(AvailableOutputs))]
        
        [Display(nameof(Output), "The output property selected.")]
        public SelectedOutputItem Output { get; set; }
        
        [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
        [Submit]
        public ParameterManager.OkCancel Response { get; set; }

        ITestStepParent step;
        IMemberData inputMember;
        public AssignOutputDialog(IMemberData member, ITestStepParent step)
        {
            this.step = step;
            inputMember = member;
            Scope = step.Parent ?? step;
            
            Output = AvailableOutputs.FirstOrDefault();
        }
    }
}