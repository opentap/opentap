using System;
using System.Collections.Generic;
using System.Globalization;
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

        InputOutputRelation(ITestStepParent inputObject, IMemberData inputMember, ITestStepParent outputObject,
            IMemberData outputMember)
        {
            this.outputObject = outputObject;
            this.inputObject = inputObject;
            this.outputMember = outputMember;
            this.inputMember = inputMember;
        }

        /// <summary> Returns true if member on object is assigned to an output / is an input. </summary>
        public static bool IsInput(ITestStepParent @object, IMemberData member)
            => GetOutputRelations(@object).Any(con => con.InputMember == member && con.InputObject == @object);

        /// <summary> Returns true if member on object is assigned to an input / is an output. </summary>
        public static bool IsOutput(ITestStepParent @object, IMemberData member)
            => GetInputRelations(@object).Any(con => con.OutputMember == member && con.OutputObject == @object);

        /// <summary> Create a relation between two members on two different objects. </summary>
        public static void Assign(ITestStepParent inputObject, IMemberData inputMember, ITestStepParent outputObject,
            IMemberData outputMember)
        {
            if (outputMember == null)
                throw new ArgumentNullException(nameof(outputMember));
            if (inputMember == null)
                throw new ArgumentNullException(nameof(inputMember));
            if (inputObject == null)
                throw new ArgumentNullException(nameof(inputObject));
            if (outputObject == null)
                throw new ArgumentNullException(nameof(outputObject));
            if (inputMember == outputMember && inputObject == outputObject)
                throw new ArgumentException("An input/output may not be assigned to itself.");
            if (outputMember.Readable == false)
                throw new ArgumentException(nameof(outputMember) + " is not readable!", nameof(outputMember));
            if (inputMember.Writable == false)
                throw new ArgumentException(nameof(inputMember) + " is not writeable!", nameof(inputMember));
            var connTo = getOutputRelations(inputObject).ToList();
            var connFrom = getInputRelations(outputObject).ToList();
            foreach (var connection in connTo)
                if (connection.OutputObject == outputObject && connection.OutputMember == outputMember &&
                    inputMember == connection.InputMember)
                    throw new ArgumentException("Input already assigned", nameof(inputMember));
            if (IsInput(inputObject, inputMember))
                throw new Exception("This input is already in use by another connection.");

            // these two restrictions might not be necessary, but it might be good to leave it 
            // in case we want to change the mechanism
            if (IsInput(outputObject, outputMember))
                throw new Exception("An output cannot also be an input");
            if (IsOutput(inputObject, inputMember))
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
            SetOutputRelations(relation.InputObject,
                getOutputRelations(relation.InputObject).Where(x => x != relation).ToArray());
            SetInputRelations(relation.OutputObject,
                getInputRelations(relation.OutputObject).Where(x => x != relation).ToArray());
        }

        /// <summary> Unassign an input/output relation . </summary>
        public static void Unassign(ITestStepParent target, IMemberData targetMember, ITestStepParent source,
            IMemberData sourceMember)
        {
            var from = getInputRelations(source);
            var con = from.FirstOrDefault(i =>
                i.InputObject == target && i.InputMember == targetMember && i.OutputMember == sourceMember);
            if (con != null)
                Unassign(con);
        }

        static bool IsInScope(ITestStepParent target, ITestStep otherStep)
        {
            var parent = target;
            while (parent != null)
            {
                if (parent == otherStep.Parent)
                    return true;
                        
                parent = parent.Parent;
            }

            return false;
        }
        
        static void checkRelations(ITestStepParent target)
        {
            var plan = target.GetParent<TestPlan>();
            if (plan == null) return;
            bool removeAllRelations = false;
            if (target is ITestStep step)
                removeAllRelations = plan.ChildTestSteps.GetStep(step.Id) == null;
            Action defer = () => { };

            foreach (var connection in getOutputRelations(target))
            {
                if (connection.OutputObject is ITestStep otherStep)
                {
                    // steps can only be connected to a step from the same test plan.
                    if (removeAllRelations || plan.ChildTestSteps.GetStep(otherStep.Id) == null || IsInScope(target, otherStep) == false)
                    {
                        defer = defer.Bind(Unassign, connection);
                    }
                }
            }

            foreach (var connection in getInputRelations(target))
            {
                if (connection.InputObject is ITestStep otherStep)
                {
                    // steps can only be connected to a step from the same test plan.
                    if (removeAllRelations || plan.ChildTestSteps.GetStep(otherStep.Id) == null)
                    {
                        defer = defer.Bind(Unassign, connection);
                    }
                }
            }

            defer();
        }

        static TestStepRun ResolveStepRun(ITestStep step)
        {
            TestStepRun run = step.StepRun;
            if (run != null) return run;
            if (step.Parent is ITestStep parent)
            {
                // recursively resolve the parent step's run.
                var parentRun = ResolveStepRun(parent);
                if (parentRun != null)
                {
                    // if parentRun.StepThread is the same as the current thread it does not make sense to wait.
                    return parentRun.WaitForChildStepStart(step.Id, 500, parentRun.StepThread != TapThread.Current);
                }
            }

            return null;

        }
        internal static object GetOutput(IMemberData outputMember, object outputObject)
        {
            var avail = outputMember.GetAttribute<OutputAttribute>()?.Availability ??
                        OutputAttribute.DefaultAvailability;
            if (avail != OutputAvailability.BeforeRun && outputObject is ITestStep step )
            {
                TestStepRun run = ResolveStepRun(step);  
                if (run != null)
                    run.WaitForOutput(avail, step);
            }

            return outputMember.GetValue(outputObject);
        }
    
        /// <summary> Updates the input of 'target' by reading the value of the source output.  </summary>
        public static void UpdateInputs(ITestStepParent target)
        {
            checkRelations(target);
            var outputs = getOutputRelations(target);
            if (outputs.Length == 0) return;
            
            Action defer = () => { };
            foreach (var connection in outputs)
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

                    object value = GetOutput(connection.OutputMember, src);
                    if (value is IConvertible conv &&
                        connection.InputMember.TypeDescriptor.DescendsTo(typeof(IConvertible)))
                        value = conv.ToType(connection.InputMember.TypeDescriptor.AsTypeData().Type,
                            CultureInfo.CurrentCulture);
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
            if (Outputs.GetValue(step) == null) return Array.Empty<InputOutputRelation>();
            checkRelations(step);
            return getInputRelations(step);
        }
        /// <summary> Gets a list of all the output relations from an object. </summary>
        static InputOutputRelation[] GetOutputRelations(ITestStepParent step)
        {
            if (Inputs.GetValue(step) == null) return Array.Empty<InputOutputRelation>();
            checkRelations(step);
            return getOutputRelations(step);
        }

        /// <summary> Get input/output relations to/from a test step. </summary>
        public static IEnumerable<InputOutputRelation> GetRelations(ITestStepParent step)
        {
            if (Inputs.GetValue(step) == null && Outputs.GetValue(step) == null)
                return Array.Empty<InputOutputRelation>();
            checkRelations(step);
            return getOutputRelations(step).Concat(getInputRelations(step));
        }
        
        /// <summary> Sets a list of all the input relations to an object. </summary>
        static void SetInputRelations(ITestStepParent step, InputOutputRelation[] specs) =>
            Outputs.SetValue(step, specs ?? Array.Empty<InputOutputRelation>());
    }
}