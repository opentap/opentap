//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary>
    /// <see cref="TestPlan"/> or <see cref="TestStep"/>. Specifies that a class can be inserted into the test plan hierarchy.
    /// </summary>
    public interface ITestStepParent //: IHierarchialListItem<TestStep, ITestStepParent>
    {
        /// <summary>
        /// Parent TestStep for this TestStep. Null if this TestStep is not a child of any other TestSteps. 
        /// Only guaranteed to be set during <see cref="TestPlan.Execute()"/>. 
        /// </summary>
        [XmlIgnore]
        ITestStepParent Parent { get; set; }
        
        /// <summary>
        /// Gets or sets a list of child TestSteps. (Inherited from <see cref="ITestStepParent"/>)
        /// </summary>
        [Browsable(false)]
        TestStepList ChildTestSteps { get; }
    }

    /// <summary>
    /// An interface for a <see cref="TestStep"/>. All TestSteps are instances of the ITestStep interface.
    /// 
    /// <para>---------------------------------------------------------------------------------------------------</para>
    /// 
    /// <para>The following attributes are mandatory</para>
    /// <para><seealso cref="ITestStepParent.Parent"/> [XmlIgnore]</para>
    /// 
    /// <para>---------------------------------------------------------------------------------------------------</para>
    /// 
    /// <para>The following attributes are recommended:</para>
    /// <para><seealso cref="ITestStepParent.ChildTestSteps"/> [XmlIgnore]</para>
    /// <para><seealso cref="Enabled"/> [Browsable(false)] [ColumnDisplayName("", Order = -101)]</para>
    /// <para><seealso cref="Id"/> [XmlAttribute("Id")] [Browsable(false)]</para>
    /// <para><seealso cref="PlanRun"/> [Browsable(false)]</para>
    /// <para><seealso cref="StepRun"/> [Browsable(false)]</para>
    /// <para><seealso cref="IsReadOnly"/> [XmlIgnore] attribute.</para>
    /// <para><seealso cref="TypeName"/> [ColumnDisplayName("Step Type", Order = 1)] [Browsable(false)]</para>
    /// <para><seealso cref="Verdict"/> [Browsable(false)] [ColumnDisplayName(Order = -99)] [XmlIgnore] [Output]</para>
    /// </summary>
    [Display("Test Step")]
    public interface ITestStep : ITestStepParent, IValidatingObject, ITapPlugin
    {
        // TODO (breaking): Split ITestStep so the base implementation does not have PrePlanRun and PostPlanRun.
        //                  That way we can quickly see if someone implements those methods without having to do it with reflection.

        /// <summary>
        /// Gets or sets the verdict. Only available during <see cref="TestStep"/> run. 
        /// This property value is propagated to the <see cref="TestStepRun"/> when the step run completes.  
        /// </summary>
        [Browsable(false)]
        [ColumnDisplayName(Order : -99, IsReadOnly: true)]
        [XmlIgnore]
        [Output]
        Verdict Verdict { get; set; }

        /// <summary>
        /// Name of the step. Should be set by the user if using multiple instances of the same type.
        /// </summary>
        [ColumnDisplayName(nameof(Name), Order: -100)]
        [Browsable(false)]
        string Name { get; set; }

        /// <summary>
        /// Gets or sets boolean value that indicates whether this step is enabled in the <see cref="TestPlan"/>.  
        /// </summary>
        [Browsable(false)]
        [ColumnDisplayName("", Order : -101)]
        bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the current <see cref="TestPlanRun"/>.  
        /// </summary>
        [Browsable(false)]
        TestPlanRun PlanRun { get; set; }

        /// <summary>
        /// Gets or sets the currently running and most recently started <see cref="TestStepRun"/>.
        /// </summary>
        [Browsable(false)]
        TestStepRun StepRun { get; set; }

        /// <summary>
        /// Gets or sets boolean value that indicates whether this step is read only in the <see cref="TestPlan"/>.  
        /// </summary>
        [Browsable(false)]
        [XmlIgnore]
        bool IsReadOnly { get; set; }

        /// <summary>
        /// Name of this <see cref="TestStep"/> type.  
        /// </summary>
        [ColumnDisplayName("Type", Order : 1)]
        [Browsable(false)]
        string TypeName { get; }

        /// <summary>
        /// Called by TestPlan.Run() for each step in the test plan prior to calling the TestStepBase.Run() methods of each step.
        /// </summary>
        void PrePlanRun();

        /// <summary>
        /// Called by TestPlan.Run() to run each TestStep. 
        /// If this step has children (ChildTestSteps.Count > 0), then these are executed instead.
        /// </summary>
        void Run();

        /// <summary>
        /// Called by TestPlan.Run() after completing all TestStepBase.Run() methods in the TestPlan
        /// <remarks>Note that TestStep.PostPlan run is run in reverse order
        ///          For example, if you had three Tests (T1, T2, and T3), and T2 was disabled, then
        ///          PrePlanRun would run for T1 and T3 (in that order)
        ///          PostPlanRun would run for T3 and T1 (in that order)</remarks>
        /// </summary>
        void PostPlanRun();

        /// <summary>
        /// Unique ID used for storing references to test steps.  
        /// </summary>
        [XmlAttribute("Id")]
        [Browsable(false)]
        Guid Id { get; set; }
    }

    /// <summary>
    /// Search pattern to use while getting child steps.
    /// </summary>
    public enum TestStepSearch
    {
        /// <summary>
        /// All steps are wanted.
        /// </summary>
        All,
        /// <summary>
        /// Only enabled steps are wanted.
        /// </summary>
        EnabledOnly,
        /// <summary>
        /// Only disabled steps are wanted.
        /// </summary>
        NotEnabledOnly
    }

    internal static class ITestStepParentExtensions
    {
        public static IEnumerable<ITestStepParent> GetParents(this ITestStepParent step)
        {
            for (ITestStepParent it = step.Parent; it != null; it = it.Parent)
                yield return it;
        }
    }
}
