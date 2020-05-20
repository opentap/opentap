using System;

namespace OpenTap
{
    /// <summary>
    /// Test step break conditions. Can be used to define when a test step should issue a break due to it's own verdict.
    /// </summary>
    [Flags]
    internal enum BreakCondition
    {
        /// <summary> Inherit behavior from parent or engine settings. </summary>
        [Display("Inherit", "Inherit behavior from the parent step. If no parent step exist or specify a behavior, the Engine setting 'Stop Test Plan Run If' is used.")]
        Inherit = 1,
        /// <summary> If a step completes with verdict 'Error', stop execution of any subsequent steps at this level, and return control to the parent step. </summary>
        [Display("Break on Error", "If a step completes with verdict 'Error', skip execution of subsequent steps and return control to the parent step.")]
        BreakOnError = 2,
        /// <summary> If a step completes with verdict 'Fail', stop execution of any subsequent steps at this level, and return control to the parent step. </summary>
        [Display("Break on Fail", "If a step completes with verdict 'Fail', skip execution of subsequent steps and return control to the parent step.")]
        BreakOnFail = 4,
        [Display("Break on Inconclusive", "If a step completes with verdict 'inconclusive', skip execution of subsequent steps and return control to the parent step.")]
        BreakOnInconclusive = 8,
    }

    /// <summary>
    /// Break condition is an 'attached property' that can be attached to any implementor of ITestStep. This ensures that the API for ITestStep does not need to be modified to support the BreakConditions feature.
    /// </summary>
    internal static class BreakConditionProperty
    {
        /// <summary> Sets the break condition for a test step. </summary>
        /// <param name="step"> Which step to set it on.</param>
        /// <param name="condition"></param>
        public static void SetBreakCondition(ITestStep step, BreakCondition condition)
        {
            DynamicMemberTypeDataProvider.TestStepTypeData.AbortCondition.SetValue(step, condition);
        }

        /// <summary> Gets the break condition for a given test step. </summary>
        /// <param name="step"></param>
        /// <returns></returns>
        public static BreakCondition GetBreakCondition(ITestStep step)
        {
            return (BreakCondition) DynamicMemberTypeDataProvider.TestStepTypeData.AbortCondition.GetValue(step);
        }
    }

    /// <summary> Internal interface to speed up setting and getting BreakConditions on core classes like TestStep. </summary>
    internal interface IBreakConditionProvider
    {
        BreakCondition BreakCondition { get; set; }
    }

    /// <summary> Internal interface to speed up setting and getting Descriptions on core classes like TestStep. </summary>
    internal interface IDescriptionProvider
    {
        string Description { get; set; }
    }
}