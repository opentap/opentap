//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

namespace OpenTap.Plugins.BasicSteps
{
    /// <summary>
    /// Custom handler for importing values from a file.
    /// </summary>
    [Display("Sweep Value Import")]
    public interface ITableImport : ITapPlugin
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

        /// <summary> Reads a table of values from a file. The tables are ordered an array of rows.</summary>
        /// <param name="filePath">Location of the file.</param>
        string[][] ImportTableValues(string filePath);
    }
}
