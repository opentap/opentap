//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public class TestStepList : ObservableCollection<ITestStep> /*HierarchialList<TestStep,ITestStepParent>*/
    {
        private static readonly TraceSource log = Log.CreateSource("TestPlan");
        
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
        /// TestSteps in this list (including TestSteps that are added later) will have this item set as their <see cref="P:Keysight.Tap.ITestStep.Parent"/>.
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
                    ChildStepsChanged += TestStepList_ChildStepsChanged;
                    rebuildIdLookup();
                }
                else
                {
                    idLookup = null;
                    ChildStepsChanged -= TestStepList_ChildStepsChanged;
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
                onContentChanged(this, ChildStepsChangedAction.RemovedStep, step, 0);

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
        }

        Dictionary<Guid, ITestStep> idLookup = null;
        private void TestStepList_ChildStepsChanged(TestStepList senderList, ChildStepsChangedAction Action, ITestStep Object, int Index)
        {
            if (idLookup == null) rebuildIdLookup();
            if(Action == ChildStepsChangedAction.RemovedStep)
            {
                foreach(var thing in Utils.FlattenHeirarchy(new[] { Object }, x => x.ChildTestSteps))
                {
                    idLookup.Remove(thing.Id);
                }
            }else if (Action == ChildStepsChangedAction.AddedStep)
            {
                foreach (var thing in Utils.FlattenHeirarchy(new[] { Object }, x => x.ChildTestSteps))
                {
                    // in some cases a thing can be added twice.
                    // happens for TestPlanReference. Hence we should use [] and not Add 
                    idLookup[thing.Id] = thing;
                }
            }
        }

        /// <summary>
        /// Determines whether a TestStep of a specified type is allowed as child step to a parent of a specified type.
        /// </summary>
        public static bool AllowChild(Type parentType, Type childType)
        {
            if (parentType == null)
                throw new ArgumentNullException("parentType");
            if (childType == null)
                throw new ArgumentNullException("childType");
            // if the parent is a TestPlan or the parent specifies "AllowAnyChild", then OK
            bool parentAllowsAnyChild = parentType.HasAttribute<AllowAnyChildAttribute>();
            if (parentType == typeof(TestPlan) || parentAllowsAnyChild)
            {
                if (!childType.HasAttribute<AllowAsChildInAttribute>())
                {
                    return true;
                }
            }

            // if the child specifies the parent type or the parent ancestor type in a "AllowAsChildIn" attribute, then OK
            AllowAsChildInAttribute[] childIn = childType.GetCustomAttributes<AllowAsChildInAttribute>();
            foreach (AllowAsChildInAttribute attribute in childIn)
            {
                if (parentType.DescendsTo(attribute.ParentStepType))
                    return true;
            }

            // if the parent specifies the childType or a base type of childType in an "AllowChildrenOfType" attribute, then OK
            AllowChildrenOfTypeAttribute[] child = parentType.GetCustomAttributes<AllowChildrenOfTypeAttribute>();
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

        internal bool CheckInserts = true;

        /// <summary>
        /// Inserts an item into the collection at a specified index.
        /// </summary>
        /// <param name="index">Location in list.</param>
        /// <param name="item">To be inserted.</param>
        protected override void InsertItem(int index, ITestStep item)
        {
            if (item == null)
                throw new ArgumentNullException("item");
            throwIfRunning();

            if (CheckInserts){ // check if step GUIDs (also for child steps) already exists. If so then replace.
                var childsteps = Utils.FlattenHeirarchy(new ITestStep[] { item }, (step) => step.ChildTestSteps);
                foreach (var step in childsteps)
                {
                    var existingstep = findStepWithGuid(step.Id);
                    if (existingstep != null && existingstep != step)
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
                        throw new ArgumentException(String.Format("Cannot add Step of type {0} as a root step (must be nested).", item.GetType().Name));
                    else
                        throw new ArgumentException(String.Format("Cannot add Step of type {0} to {1}", item.GetType().Name, Parent.GetType().Name));
                }
            }
            
            base.InsertItem(index, item);
            onContentChanged(this, ChildStepsChangedAction.AddedStep, item, index);
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
            ListReplaced
        }

        void onContentChanged(TestStepList sender, ChildStepsChangedAction Action, ITestStep Object, int Index)
        {
            if (Parent != null && Parent.Parent != null && Parent.Parent.ChildTestSteps != null)
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
            onContentChanged(this, ChildStepsChangedAction.RemovedStep, item, index);
        }

        /// <summary> Removed a number of steps from the test plan. Also includes child steps of selected steps. </summary>
        /// <param name="steps">The steps to remove.</param>
        public void RemoveItems(IEnumerable<ITestStep> steps)
        {
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
            return this
                .Where(step => stepSearch == TestStepSearch.All
               || ((stepSearch == TestStepSearch.EnabledOnly) == step.Enabled));
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
    }
}
