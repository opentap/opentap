//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

namespace OpenTap.Plugins.BasicSteps
{
    /// <summary>
    /// Custom handler for exporting a table of values to a file.
    /// </summary>
    [Display("External Parameter Export")]
    public interface ITableExport : ITapPlugin
    {
        /// <summary>
        /// The extension of the file including the '.'. For example '.zip'.
        /// </summary>
        string Extension { get; }

        /// <summary>
        /// Name of the file format. Shown when the user selects the format in the GUI.
        /// For example, Compressed Using Zip.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Exports a table of values to a file. The table should be arranged as an array of rows.
        /// </summary>
        /// <param name="table">The values to put in the CSV file.</param>
        /// <param name="parameterFilePath">Location of the file.</param>
        void ExportTableValues(string[][] table, string parameterFilePath);
    }
}
