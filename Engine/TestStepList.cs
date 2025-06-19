//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
namespace OpenTap
{

    /// <summary>
    /// Used to specify that a TestStep class allows any class of step to be added as  a child.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class AllowAnyChildAttribute : Attribute
    {

    }

    /// <summary>
    /// Identifies which <see cref="TestStep"/> types that allow this TestStep as a child.
    /// Parent type can be TestPlan.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class AllowAsChildInAttribute : Attribute
    {
        /// <summary>
        /// Type of <see cref="TestStep"/> that can be a parent for this TestStep.
        /// </summary>
        public Type ParentStepType { get; private set; }
        /// <summary>
        /// Identifies which <see cref="TestStep"/> types that allow this TestStep as a child.
        /// Parent type can be TestPlan.
        /// </summary>
        /// <param name="parentStepType">Type of <see cref="TestStep"/> that can be a parent for this TestStep.</param>
        public AllowAsChildInAttribute(Type parentStepType)
        {
            ParentStepType = parentStepType;
        }

    }

    /// <summary>
    /// Specifies that a TestStep class allows any child TestStep of a given type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class AllowChildrenOfTypeAttribute : Attribute
    {
        /// <summary>
        /// Which child type is allowed.
        /// </summary>
        public Type RequiredInterface { get; private set; }
        /// <summary>
        /// Specifies that a TestStep class allows any child TestStep of a given type.
        /// </summary>
        /// <param name="requiredInterface">Which child type is allowed.</param>
        public AllowChildrenOfTypeAttribute(Type requiredInterface)
        {
            RequiredInterface = requiredInterface;
        }
    }
    
    /// <summary>
    /// This class holds a list of TestSteps and is used for the Children property of TestStepBase. 
    /// It is responsible for making sure that all TestSteps added to the list are supported/allowed 
    /// as children of the TestStep in the TestStepList.Parent field.
    /// </summary>
    [DebuggerDisplay("TestStepList {Count}")]
    public class TestStepList : ObservableCollection<ITestStep>
    {
        /// <summary>
        /// When true, the nesting rules defined by <see cref="AllowAsChildInAttribute"/> and 
        /// <see cref="AllowAnyChildAttribute"/> are checked when trying to insert a step into 
        /// this list. If the rules are not fulfilled the TestStep is not inserted and a warning 
        /// is written to the log.
        /// </summary>
        public bool EnforceNestingRulesOnInsert = true;

        /// <summary>Determines if the TestStepList is read only.</summary>
        public bool IsReadOnly { get; set; }

        private ITestStepParent _Parent;
        /// <summary>
        /// Parent item of type <see cref="ITestStepParent"/> to which this list belongs.
        /// TestSteps in this list (including TestSteps that are added later) will have this item set as their <see cref="P:OpenTap.ITestStep.Parent"/>.
        /// </summary>
        public ITestStepParent Parent
        {
            get { return _Parent; }
            set
            {
                _Parent = value;
                foreach (ITestStep step in this)
                {
                    step.Parent = value;
                }
                onContentChanged(this, ChildStepsChangedAction.ListReplaced, null, -1);
                if (Parent is TestPlan)
                {
                    rebuildIdLookup();
                }
                else
                {
                    idLookup = null;
                }
            }
        }

        /// <summary>
        /// Removes all the items in the list.
        /// </summary>
        protected override void ClearItems()
        {
            var steps = this.ToList();
            base.ClearItems();
            foreach (var step in steps)
            {
                step.Parent = null;
                onContentChanged(this, ChildStepsChangedAction.RemovedStep, step, 0);
            }

        }

        /// <summary>Constructor for the TestStepList.</summary>
        public TestStepList()
        {
            IsReadOnly = false;
        }

        void rebuildIdLookup()
        {
            if (idLookup == null)
                idLookup = new Dictionary<Guid, ITestStep>();
            idLookup.Clear();
            foreach (var thing in RecursivelyGetAllTestSteps(TestStepSearch.All))
            {
                idLookup.Add(thing.Id, thing);
            }
            if(Parent is ITestStep step)
                idLookup.Add(step.Id, step);
        }

        Dictionary<Guid, ITestStep> idLookup = null;
        private void TestStepList_ChildStepsChanged(TestStepList senderList, ChildStepsChangedAction Action, ITestStep Object, int Index)
        {
            if (idLookup == null) rebuildIdLookup();
            if(Action == ChildStepsChangedAction.RemovedStep)
            {
                foreach(var step in Utils.FlattenHeirarchy(new[] { Object }, subStep => subStep.ChildTestSteps))
                {
                    idLookup.Remove(step.Id);
                }
            }else if (Action == ChildStepsChangedAction.AddedStep)
            {
                foreach (var step in Utils.FlattenHeirarchy(new[] { Object }, subStep => subStep.ChildTestSteps))
                {
                    // in some cases a thing can be added twice.
                    // happens for TestPlanReference. Hence we should use [] and not Add 
                    idLookup[step.Id] = step;
                }
            }
            else if (Action == ChildStepsChangedAction.SetStep)
            {
                if (idLookup.TryGetValue(Object.Id, out var previous))
                {
                    foreach(var step in Utils.FlattenHeirarchy(new []{previous}, subStep => subStep.ChildTestSteps))
                    {
                        idLookup.Remove(step.Id);
                    }
                }
                foreach (var step in Utils.FlattenHeirarchy(new[] { Object }, subStep => subStep.ChildTestSteps))
                {
                    idLookup[step.Id] = step;
                }
            }
        }

        /// <summary>
        /// Determines whether a TestStep of a specified type is allowed as child step to a parent of a specified type.
        /// </summary>
        public static bool AllowChild(Type parentType, Type childType)
        {
            if (parentType == null)
                throw new ArgumentNullException(nameof(parentType));
            if (childType == null)
                throw new ArgumentNullException(nameof(childType));
            return AllowChild(TypeData.FromType(parentType), TypeData.FromType(childType));
        }

        /// <summary>
        /// Determines whether a TestStep of a specified type is allowed as child step to a parent of a specified type.
        /// </summary>
        public static bool AllowChild(ITypeData parentType, ITypeData childType)
        {
            if (parentType == null)
                throw new ArgumentNullException(nameof(parentType));
            if (childType == null)
                throw new ArgumentNullException(nameof(childType));
            // if the parent is a TestPlan or the parent specifies "AllowAnyChild", then OK
            bool parentAllowsAnyChild = parentType.HasAttributeInherited<AllowAnyChildAttribute>();
            bool isChildIn = childType.HasAttributeInherited<AllowAsChildInAttribute>();
            if (parentAllowsAnyChild)
            {
                if (!isChildIn)
                {
                    return true;
                }
            }

            if (isChildIn)
            {
                // if the child specifies the parent type or the parent ancestor type in a "AllowAsChildIn" attribute, then OK
                var childIn = childType.GetAttributesInherited<AllowAsChildInAttribute>();
                foreach (AllowAsChildInAttribute attribute in childIn)
                {
                    if (parentType.DescendsTo(attribute.ParentStepType))
                        return true;
                }
            }

            // if the parent specifies the childType or a base type of childType in an "AllowChildrenOfType" attribute, then OK
            var child = parentType.GetAttributesInherited<AllowChildrenOfTypeAttribute>();
            foreach (AllowChildrenOfTypeAttribute attribute in child)
            {
                if (childType.DescendsTo(attribute.RequiredInterface))
                    return true;
            }

            // otherwise not OK
            return false;
        }
        
        private ITestStep findStepWithGuid(Guid guid)
        {
            TestStepList toplst;

            if (Parent != null)
            {
                ITestStepParent topitem = Parent;
                while (topitem.Parent != null) topitem = topitem.Parent;
                toplst = topitem.ChildTestSteps;
            }
            else
            {
                toplst = this;
            }
            return toplst.GetStep(guid);
        }

        
        /// <summary>
        /// Inserts an item into the collection at a specified index.
        /// </summary>
        /// <param name="index">Location in list.</param>
        /// <param name="item">To be inserted.</param>
        protected override void InsertItem(int index, ITestStep item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            
            throwIfRunning();

            var childsteps = Utils.FlattenHeirarchy([item], static step => step.ChildTestSteps);

            foreach (var step in childsteps)
            {
                var existingstep = findStepWithGuid(step.Id);
                if (existingstep != null)
                {
                    if (existingstep == step)
                        throw new InvalidOperationException("Test step already exists in the test plan");
                    step.Id = Guid.NewGuid();
                }
            }

            // Parent can be null if the list is being loaded from XML, in that case we 
            // set the parent property of the children in the setter of the Parent property
            if (Parent != null)
                item.Parent = Parent;
            
            if (EnforceNestingRulesOnInsert && Parent != null)
            {
                Type parentType = Parent.GetType();
                if (!AllowChild(parentType, item.GetType()))
                {
                    if (Parent is TestPlan)
                        throw new ArgumentException(String.Format("Cannot add test step of type {0} as a root test step (must be nested).", item.GetType().Name));
                    else
                        throw new ArgumentException(String.Format("Cannot add test step of type {0} to {1}", item.GetType().Name, Parent.GetType().Name));
                }
            }
            base.InsertItem(index, item);
            
            onContentChanged(this, ChildStepsChangedAction.AddedStep, item, index);
        }
        
        /// <summary>
        /// Invoked when an item is set. 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ArgumentException"></exception>
        protected override void SetItem(int index, ITestStep item)
        {
            throwIfRunning();

            // Parent can be null if the list is being loaded from XML, in that case we 
            // set the parent property of the children in the setter of the Parent property
            if (Parent != null)
                item.Parent = Parent;
            
            if (EnforceNestingRulesOnInsert && Parent != null)
            {
                Type parentType = Parent.GetType();
                if (!AllowChild(parentType, item.GetType()))
                {
                    if (Parent is TestPlan)
                        throw new ArgumentException(String.Format("Cannot add test step of type {0} as a root test step (must be nested).", item.GetType().Name));
                    else
                        throw new ArgumentException(String.Format("Cannot add test step of type {0} to {1}", item.GetType().Name, Parent.GetType().Name));
                }
            }
            base.SetItem(index, item);
            onContentChanged(this, ChildStepsChangedAction.SetStep, item, index);
        }

        /// <summary>
        /// Invoked when an item has been moved
        /// </summary>
        /// <param name="oldIndex"></param>
        /// <param name="newIndex"></param>
        protected override void MoveItem(int oldIndex, int newIndex)
        {
            base.MoveItem(oldIndex, newIndex);
            onContentChanged(this, ChildStepsChangedAction.MovedStep, this[newIndex], newIndex);
        }

        /// <summary>
        /// Returns true if a TestStep of type stepType can be inserted as a child step.
        /// </summary>
        /// <param name="stepType"></param>
        /// <returns></returns>
        public bool CanInsertType(Type stepType)
        {
            if (IsReadOnly) return false;
            if (Parent != null)
            {
                Type parentType = Parent.GetType();
                if (!AllowChild(parentType, stepType))
                    return false;
            }

            // If no parent, anything can be inserted.
            return true;
        }

        /// <summary>
        /// Defines the callback interface that can get invoked when a child step lists changes. 
        /// </summary>
        /// <param name="senderList"> The list that changed</param>
        /// <param name="Action">How the list changed</param>
        /// <param name="Object">Which object changed in the list (might be null if Reset)</param>
        /// <param name="Index">The index of the item changed.</param>
        public delegate void ChildStepsChangedDelegate(TestStepList senderList, ChildStepsChangedAction Action, ITestStep Object, int Index);

        /// <summary>
        /// Invoked when <see cref="TestStep.ChildTestSteps"/> changes for this TestStepList and child TestStepLists.
        /// </summary>
        public event ChildStepsChangedDelegate ChildStepsChanged;

        /// <summary>
        /// Specifies what has changed.
        /// </summary>
        public enum ChildStepsChangedAction
        {
            /// <summary>
            /// Specifies that a step has been added to a list.
            /// </summary>
            AddedStep,
            /// <summary>
            /// Specifies that a step has been removed from the list.
            /// </summary>
            RemovedStep,
            /// <summary>
            /// Specifies that the TestStepList has been replaced. The sender is in this case the new object. Object and Index will be null.
            /// </summary>
            ListReplaced,
            /// <summary>
            /// Specifies that a step has been set.
            /// </summary>
            SetStep,
            /// <summary>
            /// Specifies that a step has been moved.
            /// </summary>
            MovedStep
        }

        static readonly Random changeIdRandomState = new Random();
        // generate random IDs to try avoiding collisions with other cached states.
        // changeid is often used for caching.
        internal int ChangeId { get; set; } = changeIdRandomState.Next();
        
        void onContentChanged(TestStepList sender, ChildStepsChangedAction Action, ITestStep Object, int Index)
        {
            ChangeId += 1;
            if (Parent is TestPlan)
            {
                TestStepList_ChildStepsChanged(sender, Action, Object, Index);
            }
            else if (Parent is ITestStepParent parent && parent.Parent is ITestStepParent parent2 && parent2.ChildTestSteps != null)
                Parent.Parent.ChildTestSteps.onContentChanged(sender, Action, Object, Index);
            if (ChildStepsChanged != null)
                ChildStepsChanged(sender, Action, Object, Index);
        }
        
        TestPlan getTestPlanParent()
        {
            var parent = Parent;

            while (parent is ITestStep)
                parent = parent.Parent;

            return parent as TestPlan;
        }
        
        void throwIfRunning()
        {
            var plan = getTestPlanParent();
            if (plan != null && plan.IsRunning)
                throw new InvalidOperationException("Test plans cannot be modified while running.");
        }

        /// <summary>
        /// Removes the item at the specified index of the collection.
        /// </summary>
        protected override void RemoveItem(int index)
        {
            throwIfRunning();
            var item = this[index];
            base.RemoveItem(index);
            item.Parent = null;
            onContentChanged(this, ChildStepsChangedAction.RemovedStep, item, index);
        }

        /// <summary> Removed a number of steps from the test plan. </summary>
        /// <param name="steps">The steps to remove.</param>
        public void RemoveItems(IEnumerable<ITestStep> steps)
        {
            // find the topmost parent (usually the test plan itself).
            ITestStepParent parent = Parent;
            while (parent.Parent != null)
                parent = parent.Parent;


            HashSet<ITestStep> allRemovedSteps = Utils.FlattenHeirarchy(steps, s => s.ChildTestSteps).Distinct().ToHashSet();
            List<ITestStep> filteredsteps = steps.Where(s => allRemovedSteps.Contains(s.Parent as ITestStep) == false).ToList();
            
            if (filteredsteps.Any(step => step.Parent.ChildTestSteps.IsReadOnly))
                throw new InvalidOperationException("Cannot remove a step from a read-only list.");
            
            foreach (var step in filteredsteps)
                step.Parent.ChildTestSteps.Remove(step);

            {
                // For all remaining steps, check if they have any TestStep properties
                // If so, check that that property still exists in the plan. Otherwise set it to null.
                var AllSteps = Utils.FlattenHeirarchy(parent.ChildTestSteps, x => x.ChildTestSteps).ToHashSet();

                Dictionary<Type, System.Reflection.PropertyInfo[]> propLookup = new Dictionary<Type, System.Reflection.PropertyInfo[]>();
                foreach (var step in AllSteps)
                {
                    var t = step.GetType();
                    if (propLookup.ContainsKey(t)) continue;
                    var props = t.GetPropertiesTap().Where(p => p.PropertyType.DescendsTo(typeof(ITestStep)) && p.GetSetMethod() != null).ToArray();
                    propLookup[t] = props;
                }

                foreach (var step in AllSteps)
                {
                    var t = step.GetType();
                    if (propLookup.ContainsKey(t) == false) continue;
                    foreach (var prop in propLookup[t])
                    {
                        ITestStep value = (ITestStep)prop.GetValue(step);
                        if (AllSteps.Contains(value) == false)
                        {
                            try
                            {
                                prop.SetValue(step, null);
                            }
                            catch
                            { 
                                // catch all usercode errors here.
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Recursively iterates steps and child steps to collect all steps in the list.
        /// </summary>
        /// <param name="stepSearch">Search pattern.</param>
        /// <returns></returns>
        public IEnumerable<ITestStep> RecursivelyGetAllTestSteps(TestStepSearch stepSearch)
        {
            return Utils.FlattenHeirarchy(GetSteps(stepSearch), x => x.ChildTestSteps.GetSteps(stepSearch));
        }

        /// <summary>
        /// Gets steps based on the search pattern. Ignores child steps. Returns null if not found.  
        /// </summary>
        /// <param name="stepSearch">Search pattern.</param>
        /// <returns></returns>
        public IEnumerable<ITestStep> GetSteps(TestStepSearch stepSearch)
        {
            if (stepSearch == TestStepSearch.All) return this;
            return this.Where(step => stepSearch == TestStepSearch.EnabledOnly == step.Enabled);
        }

        /// <summary>
        /// Returns the test step that matches the <see cref="TestStep.Id"/>. Returns null if not found.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ITestStep GetStep(Guid id)
        {
            if(Parent == null && idLookup == null)
            {
                rebuildIdLookup();
            }
            if(idLookup != null)
            {
                if (idLookup.TryGetValue(id, out ITestStep result))
                    return result;
                return null;
            }
            if (this._Parent is ITestStep thisStep && thisStep.Id == id)
                return thisStep;
            foreach (ITestStep step in this)
            {
                if (step.Id == id)
                    return step;
                if (step.ChildTestSteps.Count > 0)
                {
                    ITestStep ret = step.ChildTestSteps.GetStep(id);
                    if (ret != null)
                        return ret;
                }
            }
            return null;
        }

        internal void AddRange(IEnumerable<ITestStep> steps)
        {
            foreach(var step in steps)
                Add(step);
        }
    }
}
