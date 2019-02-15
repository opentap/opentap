//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{
    /// <summary>
    /// Interface to facilitate the dynamic generation of TestStep types when loading the TestPlan from XML. 
    /// </summary>
    public interface IDynamicStep
    {
        /// <summary>
        /// Returns the type of the class that can create the dynamic step. The Type returned should implement IDynamicStep.
        /// </summary>
        Type GetStepFactoryType();

        /// <summary> Returns itself or a new step to be exchanged with itself in the test plan. Must never return null. </summary>
        ITestStep GetStep();
    }
}
