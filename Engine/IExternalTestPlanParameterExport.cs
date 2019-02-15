//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
namespace OpenTap
{
    /// <summary>
    /// Custom handler for exporting external parameters from a TestPlan to a file.
    /// </summary>
    [Display("External Parameter Export")]
    public interface IExternalTestPlanParameterExport : ITapPlugin
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
        /// Exports currently configured external parameters and values to a file
        /// </summary>
        /// <param name="testPlan">The currently loaded TestPlan</param>
        /// <param name="parameterFilePath">Location of the file</param>
        void ExportExternalParameters(TestPlan testPlan, string parameterFilePath);
    }
}
