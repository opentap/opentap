//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
namespace OpenTap
{
    /// <summary>
    /// Custom handler for importing external parameters from a file.
    /// </summary>
    [Display("External Parameter Import")]
    public interface IExternalTestPlanParameterImport : ITapPlugin
    {
        /// <summary>
        /// The extension of the imported file including the '.'. For example '.zip'.
        /// </summary>
        string Extension { get; }

        /// <summary>
        /// Name of the file format. Shown when the user selects the format in the GUI.
        /// For example, Compressed Using Zip.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Imports the values of a file into the external parameter values.
        /// TestPlan must already contain definitions for the external parameters found in the file.
        /// </summary>
        /// <param name="testPlan">The TestPlan to import the values into</param>
        /// <param name="parameterFilePath">Path of the file containing the values</param>
        /// <remarks>
        /// Exceptions should be thrown from here, when necessary.
        /// </remarks>
        void ImportExternalParameters(TestPlan testPlan, string parameterFilePath);
    }
}
