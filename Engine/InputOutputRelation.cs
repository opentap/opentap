using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    /// <summary> Accelerating structure for inputs / outputs owners. Note, it is recommended to implement this explicitly. </summary>
    interface IInputOutputRelations
    {
        /// <summary> Relations to the object ('this'). </summary>
        InputOutputRelation[] Inputs { get; set; }
        /// <summary>  Relations from the object('this'); </summary>
        InputOutputRelation[] Outputs { get; set; }
    }
    
    /// <summary> Relations between two object and a pair of their members. </summary>
    public sealed class InputOutputRelation
    {
        readonly ITestStepParent outputObject;
        readonly ITestStepParent inputObject;
        readonly IMemberData outputMember;
        readonly IMemberData inputMember;
        
        /// <summary> The object that owns the output member. </summary>
        public ITestStepParent OutputObject => outputObject;
        
        /// <summary> The object that owns the input member.</summary>
        public ITestStepParent InputObject => inputObject;

        /// <summary> The member which the output value is read from. </summary>
        public IMemberData OutputMember => outputMember;

        /// <summary> The Member which the input value is read from. </summary>
        public IMemberData InputMember => inputMember;

        InputOutputRelation(ITestStepParent inputObject, IMemberData inputMember, ITestStepParent outputObject, IMemberData outputMember)
        {
            this.outputObject = outputObject;
            this.inputObject = inputObject;
            this.outputMember = outputMember;
            this.inputMember = inputMember;
        }

        /// <summary> Returns true if member on object is assigned to an output / is an input. </summary>
        public static bool IsInput(ITestStepParent @object, IMemberData member) 
            => GetOutputRelations(@object).Any(con => con.InputMember == member);
        
        /// <summary> Returns true if member on object is assigned to an input / is an output. </summary>
        public static bool IsOutput(ITestStepParent @object, IMemberData member)
            => GetInputRelations(@object).Any(con => con.OutputMember == member);

        /// <summary> Create a relation between two members on two different objects. </summary>
        public static void Assign(ITestStepParent inputObject, IMemberData inputMember, ITestStepParent outputObject, IMemberData outputMember)
        {
            if(outputMember == null)
                throw new ArgumentNullException(nameof(outputMember));
            if(inputMember == null)
                throw new ArgumentNullException(nameof(inputMember));
            if(inputObject == null)
                throw new ArgumentNullException(nameof(inputObject));
            if(outputObject == null)
                throw new ArgumentNullException(nameof(outputObject));
            if(inputMember == outputMember && inputObject == outputObject)
                throw new InvalidOperationException("An output cannot be connected to itself");
            
            if(outputMember.Readable == false)
                throw new ArgumentException(nameof(outputMember) + " is not readable!", nameof(outputMember));
            if (inputMember.Writable == false)
                throw new ArgumentException(nameof(inputMember) + " is not writeable!", nameof(inputMember));
            var connTo = getOutputRelations(inputObject).ToList();
            var connFrom = getInputRelations(outputObject).ToList();
            foreach(var connection in connTo)
                if (connection.OutputObject == outputObject && connection.OutputMember == outputMember)
                    return;
            if(IsInput(outputObject, outputMember))
                throw new Exception("An output cannot also be an input");
            if(IsInput(inputObject, inputMember))
                throw new Exception("This input is already in use by another connection.");
            if(IsOutput(inputObject, inputMember))
                throw new Exception("An input cannot also be an output");

            var newSpec = new InputOutputRelation(inputObject, inputMember, outputObject, outputMember);
            connTo.Add(newSpec);
            connFrom.Add(newSpec);
            
            SetOutputRelations(inputObject, connTo.ToArray());
            SetInputRelations(outputObject, connFrom.ToArray());
        }

        /// <summary> Unassign an input/output relation. </summary>
        /// <param name="relation"></param>
        internal static void Unassign(InputOutputRelation relation)
        {
            SetOutputRelations(relation.InputObject, getOutputRelations(relation.InputObject).Where(x => x != relation).ToArray());
            SetInputRelations(relation.OutputObject, getInputRelations(relation.OutputObject).Where(x => x != relation).ToArray());
        }
        
        /// <summary> Unassign an input/output relation . </summary>
        public static void Unassign(ITestStepParent target, IMemberData targetMember, ITestStepParent source,
            IMemberData sourceMember)
        {
            var from = getInputRelations(source).ToList();
            var con = from.FirstOrDefault(i => i.InputObject == target && i.InputMember == targetMember && i.OutputMember == sourceMember);
            if(con != null)
                Unassign(con);
        }

        static void checkRelations(ITestStepParent target)
        {
            var plan = target.GetParent<TestPlan>();
            if (plan == null) return;
            Action defer = () => { }; 
            
            foreach (var connection in getOutputRelations(target))
            {
                if (connection.InputObject is ITestStep otherStep)
                { // steps can only be connected to a step from the same test plan.
                    if (plan.ChildTestSteps.GetStep(otherStep.Id) == null)
                    {
                        defer = defer.Bind(Unassign, connection);
                    }
                }
            }
            foreach (var connection in getInputRelations(target))
            {
                if(connection.OutputObject is ITestStep otherStep)
                { // steps can only be connected to a step from the same test plan.
                    if (plan.ChildTestSteps.GetStep(otherStep.Id) == null)
                    {
                        defer = defer.Bind(Unassign, connection);
                    }
                }
            }
            
            defer();
        }
        
        /// <summary> Updates the input of 'target' by reading the value of the source output.  </summary>
        public static void UpdateInputs(ITestStepParent target)
        {
            checkRelations(target);
            Action defer = () => { }; 
            foreach (var connection in getOutputRelations(target))
            {
                if (connection.OutputObject is ITestStepParent src)
                {
                    var outputMember = connection.OutputMember;
                    var inputMember = connection.InputMember;
                    if (outputMember == null || inputMember == null)
                    {
                        defer = defer.Bind(Unassign, connection);
                        continue;
                    } 

                    object value = connection.OutputMember.GetValue(src);
                    connection.InputMember.SetValue(target, value);
                }
            }
            defer();
        }

        static readonly AcceleratedDynamicMember<IInputOutputRelations> Inputs = new AcceleratedDynamicMember<IInputOutputRelations>()
        {
            ValueSetter = (owner, value) => ((IInputOutputRelations) owner).Inputs = (InputOutputRelation[])value,
            ValueGetter = (owner) => ((IInputOutputRelations) owner).Inputs,
            Name = "ConnectedInputs",
            DefaultValue = Array.Empty<InputOutputRelation>(),
            DeclaringType = TypeData.FromType(typeof(ITestStep)),
            Writable = true,
            Readable = true,
            TypeDescriptor = TypeData.FromType(typeof(InputOutputRelation[]))
        };
        
        static readonly AcceleratedDynamicMember<IInputOutputRelations> Outputs = new AcceleratedDynamicMember<IInputOutputRelations>()
        {
            ValueSetter = (owner, value) => ((IInputOutputRelations) owner).Outputs = (InputOutputRelation[])value,
            ValueGetter = (owner) => ((IInputOutputRelations) owner).Outputs,
            Name = "ConnectedOutputs",
            DefaultValue = Array.Empty<InputOutputRelation>(),
            DeclaringType = TypeData.FromType(typeof(ITestStep)),
            Writable = true,
            Readable = true,
            TypeDescriptor = TypeData.FromType(typeof(InputOutputRelation[]))
        };

        
        static InputOutputRelation[] getOutputRelations(ITestStepParent step) =>  (InputOutputRelation[]) Inputs.GetValue(step) ?? Array.Empty<InputOutputRelation>();
        
        /// <summary> Sets all the output relations from an object. </summary>
        static void SetOutputRelations(ITestStepParent step, InputOutputRelation[] specs) =>
            Inputs.SetValue(step, specs ?? Array.Empty<InputOutputRelation>());
        
        
        static InputOutputRelation[] getInputRelations(ITestStepParent step) =>  (InputOutputRelation[]) Outputs.GetValue(step) ?? Array.Empty<InputOutputRelation>();
        
        /// <summary> Gets a list of all the input relations to an object. </summary>
        static InputOutputRelation[] GetInputRelations(ITestStepParent step)
        {
            checkRelations(step);
            return getInputRelations(step);
        }
        /// <summary> Gets a list of all the output relations from an object. </summary>
        static InputOutputRelation[] GetOutputRelations(ITestStepParent step)
        {
            checkRelations(step);
            return getOutputRelations(step);
        }

        /// <summary> Get input/output relations to/from a test step. </summary>
        public static IEnumerable<InputOutputRelation> GetRelations(ITestStepParent step)
        {
            checkRelations(step);
            return getOutputRelations(step).Concat(getInputRelations(step));
        }
        
        /// <summary> Sets a list of all the input relations to an object. </summary>
        static void SetInputRelations(ITestStepParent step, InputOutputRelation[] specs) =>
            Outputs.SetValue(step, specs ?? Array.Empty<InputOutputRelation>());
    }
}