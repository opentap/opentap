//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace OpenTap
{
    /// <summary>
    /// Listens to events in the log and outputs them to a file. Can be configured with filters on the log verbosity level. 
    /// FilePath supports replacement of date and verdict. 
    /// </summary>
    [Display("Text Log", Group:"Text", Description: "Save the log from a test plan run as a text file.")]
    public class LogResultListener : ResultListener, IFileResultStore
    {
        /// <summary>
        /// File path of log file.
        /// </summary>
        [FilePath(FilePathAttribute.BehaviorChoice.Save, "txt")]
        [HelpLink(@"EditorHelp.chm::/Configuring TAP/Using Tags and Variables in File Names.html")]
        [Display("File Path", Description: "Output file path for the log file.")]
        public MacroString FilePath { get; set; }

        string IFileResultStore.FilePath { get { return FilePath.Expand(); } set { FilePath.Text = value; } }
        
        /// <summary>
        /// FilterOptions for filtering on log message verbosity level.
        /// </summary>
        [Flags]
        public enum FilterOptionsType
        {
            /// <summary>
            /// Debug messages.
            /// </summary>
            [Display("Debug", "Include debug level log messages.")]
            Verbose = 1,
            /// <summary>
            /// Information messages.
            /// </summary>
            [Display("Information", "Include information level log messages.")]
            Info = 2,
            /// <summary>
            /// Warning messages.
            /// </summary>
            [Display("Warning", "Include warning level log messages.")]
            Warnings = 4,
            /// <summary>
            /// Error messages.
            /// </summary>
            [Display("Error", "Include error level log messages.")]
            Errors = 8
        }

        /// <summary>
        /// to string converter LUT for filter options.
        /// </summary>
        readonly Dictionary<FilterOptionsType, string> logFilterOptionsLUT = new Dictionary<FilterOptionsType, string>()
        {
            {FilterOptionsType.Verbose, "Debug"},
            {FilterOptionsType.Info, "Information"},
            {FilterOptionsType.Warnings, "Warning"},
            {FilterOptionsType.Errors, "Error"}
        };

        /// <summary>
        /// Contains the FilterOptions flags. Any combination of the four flags is allowed.
        /// </summary>
        [Display("Filter Options", Description: "Select how to filter the log messages if needed.")]
        public FilterOptionsType FilterOptions { get; set; }

        bool useFilter()
        {
            return FilterOptions != (FilterOptionsType.Verbose |
                FilterOptionsType.Warnings |
                FilterOptionsType.Info |
                FilterOptionsType.Errors);
        }

        /// <summary>
        /// Sets default values.
        /// </summary>
        public LogResultListener()
        {
            Name = "Log";
            FilePath = new MacroString() { Text = "Results/<Date>-<Verdict>.txt" };
            FilterOptions = FilterOptionsType.Verbose |
                FilterOptionsType.Warnings |
                FilterOptionsType.Info |
                FilterOptionsType.Errors;
        }

        void filterCopyStream(Stream input, Stream outStream)
        {
            using (StreamWriter streamWriter = new StreamWriter(outStream, System.Text.Encoding.UTF8))
            {
                using (StreamReader streamReader = new StreamReader(input))
                {
                    string line;
                    Regex rx = new Regex("^(?<time>[^;]+);(?<source>[^;]+);(?<level>[^;]+)");
                    var allFilterOptions = Enum.GetValues(typeof(FilterOptionsType));
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        Match m = rx.Match(line);
                        bool skip = false;
                        if (m.Success)
                        {
                            foreach (FilterOptionsType filterOption in allFilterOptions)
                            {
                                if (!FilterOptions.HasFlag(filterOption))
                                {
                                    string searchString = logFilterOptionsLUT[filterOption];
                                    if (m.Groups["level"].Value.Trim() == searchString)
                                    {
                                        skip = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (!skip)
                        {
                            streamWriter.WriteLine(line);
                        }
                    }
                }
            }
        }

        // Multiple resources might point to the same file path.
        // Hence this lock is used to prevent the same file.
        // being created from multiple LogResultListeners.
        static object filereadlocker = new object();

        /// <summary>
        /// On test plan run completed the previously temporary file is moved to the location expanded by the macro path.
        /// </summary>
        /// <param name="planRun"></param>
        /// <param name="logStream"></param>
        public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
        {
            if (logStream == null)
                throw new ArgumentNullException("logStream");
            base.OnTestPlanRunCompleted(planRun, logStream);
            OnActivity();


            string outpath = "";
            FileStream fstr;
            lock (filereadlocker)
            {
                string realPath = FilePath.Expand(planRun);
                if (Path.GetDirectoryName(realPath) != "")
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(realPath));
                }

                int fileid = 1;
                outpath = realPath;
                while (File.Exists(outpath))
                {
                    var extension = Path.GetExtension(realPath);
                    outpath = Path.ChangeExtension(realPath, string.Format(".{0}", fileid++) + extension);
                }
                fstr = new FileStream(outpath, FileMode.Create);
            }

            if (useFilter())
            {
                filterCopyStream(logStream, fstr);
            }
            else
            {
                logStream.CopyTo(fstr);
            }
            fstr.Close();
        }

        string IFileResultStore.DefaultExtension
        {
            get { return "txt"; }
        }
    }
}
