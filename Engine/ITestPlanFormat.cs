//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
namespace OpenTap
{
    /// <summary>
    /// Custom handler for importing TestPlan data.
    /// Which implementation of ITestPlanImport is used is based on the file type (ITestPlanExport.Extension).
    /// </summary>
    /// <remarks>
    /// The developer determines the content and complexity of classes that implement this type. 
    /// It might be as simple as unzipping a TestPlan, or as complex as reading a well-defined Excel file that represents some TestPlan data.
    /// </remarks>
    [Display("Test Plan Import")]
    public interface ITestPlanImport : ITapPlugin
    {
        /// <summary>
        /// The extension of the imported file including the '.'. For example '.zip'.
        /// </summary>
        string Extension { get; }

        /// <summary>
        /// Name of the file format. Shown when the user selects the format in the UI.
        /// For example, Compressed Using Zip.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Imports a file. The contents of the file can be an entire TestPlan, or data to be inserted into a dynamically created TestPlan.  
        /// </summary>
        /// <param name="filePath">The absolute or relative path to file.</param>
        /// <returns>The test plan constructed as part of the Import. </returns>
        TestPlan ImportTestPlan(string filePath);
    }

    /// <summary>
    /// Custom handler for exporting TestPlan data.
    /// Which implementation of ITestPlanExport is used is based on the file type (ITestPlanExport.Extension).
    /// </summary>
    /// <remarks>
    /// The developer determines the content and complexity of classes that implement this type/>. 
    /// It might be as simple as unzipping a TestPlan, or as complex as reading a well-defined Excel file that represents some TestPlan data.
    /// </remarks>
    [Display("Test Plan Export")]
    public interface ITestPlanExport : ITapPlugin
    {
        /// <summary>
        /// The extension of the exported file including the '.'. For example '.zip'.
        /// </summary>
        string Extension { get; }

        /// <summary>
        /// Name of the file format. Shown when the user selects the format in the GUI.
        /// For example, Compressed Using Zip.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Exports the test plan or TestPlan data to a file. 
        /// </summary>
        /// <param name="plan"> The plan.</param>
        /// <param name="filePath">The absolute or relative path to the file. </param>
        void ExportTestPlan(TestPlan plan, string filePath);
    }

    /// <summary>
    /// Custom GUI handler for importing TestPlan data.
    /// </summary>
    /// <remarks>
    /// In contrast to <see cref="ITestPlanImport"/> implementations of this provides their own custom GUI to allows user to select the TestPlan file/data.
    /// For example, using a GUI to load the plan from a database or virtual location such as Dropbox or AWS S3.
    /// </remarks>
    [Display("Test Plan Import Custom Dialog")]
    public interface ITestPlanImportCustomDialog : ITapPlugin
    {
        /// <summary>
        /// Import TestPlan.
        /// </summary>
        /// <returns>The test plan contructed as part of the import</returns>
        TestPlan ImportTestPlan();
    }

    /// <summary>
    /// Custom GUI handler for importing TestPlan data.
    /// </summary>
    /// <remarks>
    /// In contrast to <see cref="ITestPlanExport"/> implementations of this provides their own custom GUI to allows user to select the export destination.
    /// For example, using a GUI to save the plan into a database or virtual location such as Dropbox or AWS S3.
    /// </remarks>
    [Display("Test Plan Export Custom Dialog")]
    public interface ITestPlanExportCustomDialog : ITapPlugin
    {
        /// <summary>
        /// Exports the test plan or TestPlan data. 
        /// </summary>
        /// <param name="plan">The plan</param>
        void ExportTestPlan(TestPlan plan);
    }
}
