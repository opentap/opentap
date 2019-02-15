//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenTap
{
    
    /// <summary>
    /// Converts a macro to its expanded string format. 
    /// </summary>
    class MacroExpansion
    {
        /// <summary>
        /// Name of macro.
        /// </summary>
        public string MacroName { get; set; }
        /// <summary>
        /// Description read from attribute.
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// String to identify where the macro comes from. 
        /// </summary>
        public string Origin { get; set; }
    }
    
    /// <summary> a string that can be expanded with macros.</summary>
    public class MacroString
    {
        /// <summary> Optional context for macro strings that refers to a test step. This is used to find additional macro definitions such as TestPlanDir.</summary>
        public ITestStepParent Context { get; set; }

        string text = "";

        /// <summary> The text that can be expanded. </summary>
        public string Text
        {
            get { return text; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                if (text == value) return;
                text = value;
            }
        }
        
        /// <summary> Creates a new instance of MacroString with a context. </summary>
        /// <param name="context"></param>
        public MacroString(ITestStepParent context)
        {
            Context = context;
        }

        /// <summary> Creates a new MacroString without a context. If a TestStep is used use that as context to get access to TestPlan related macros.</summary>
        public MacroString()
        {

        }

        /// <summary> Expands the text. Macros are harvested from the optional TestPlanRun or the test step.</summary>
        /// <param name="run">A place to find additional metadata for macro expansion.</param>
        /// <param name="date">If no date was found in the metadata, this date will be used. If date is not supplied, DateTime.Now will be used.</param>
        /// <param name="testPlanDir">If no TestPlanDir was found in the metata, this TestPlanDir will be used.</param>
        /// <returns>The expanded string.</returns>
        public string Expand(TestPlanRun run = null, DateTime? date = null, string testPlanDir = null)
        {
            return Expand(run, date, testPlanDir, null);
        }
        
        /// <summary> Expands the text. Macros are harvested from the optional TestPlanRun or the test step.</summary>
        /// <param name="run">A place to find additional metadata for macro expansion.</param>
        /// <param name="date">If no date was found in the metadata, this date will be used. If date is not supplied, DateTime.Now will be used.</param>
        /// <param name="testPlanDir">If no TestPlanDir was found in the metata, this TestPlanDir will be used.</param>
        /// <param name="replacements">Overrides other macro parameters.</param>
        /// <returns>The expanded string.</returns>
        public string Expand(TestPlanRun run, DateTime? date, string testPlanDir, Dictionary<string, object> replacements)
        {
            
            ITestStepParent context = Context;
            var replacers = new Dictionary<string, object>();
            if (testPlanDir != null)
                replacers["TestPlanDir"] = testPlanDir;
            if (date != null)
                replacers["Date"] = date;
            
            var met = ResultParameters.GetComponentSettingsMetadata();
            foreach (var v in met)
            {
                if(v.IsMetaData)
                    replacers[v.MacroName ?? v.Name] = v.Value;
            }

            if (run != null)
            {
                foreach (var v in run.Parameters.Concat(ResultParameters.GetMetadataFromObject(run)))
                {
                    if (v.IsMetaData == false)
                        continue;
                    if (replacers.ContainsKey(v.Name) == false)
                    {
                        var path = v.Value;
                        if (path is string && System.IO.File.Exists((string)path))
                        {
                            path = System.IO.Path.GetDirectoryName((string)path);
                        }
                        replacers[v.MacroName ?? v.Name] = path;
                    }
                }
            }
            ITestStepParent ctx = context;
            while (ctx != null)
            {
                var p = ResultParameters.GetMetadataFromObject(ctx);
                foreach (var v in p)
                {
                    if (v.IsMetaData == false)
                        continue;
                    if (replacers.ContainsKey(v.MacroName) == false)
                    {
                        var path = v.Value;
                        if (path is string && System.IO.File.Exists((string)path))
                        {
                            path = System.IO.Path.GetDirectoryName((string)path);
                        }
                        replacers[v.MacroName ?? v.Name] = path;
                    }
                }
                ctx = ctx.Parent;
            }
            if (!replacers.ContainsKey("Date"))
                replacers["Date"] = DateTime.Now;
            if (!replacers.ContainsKey("Verdict"))
                replacers["Verdict"] = Verdict.NotSet;
            if(replacements != null)
                foreach (var rep in replacements)
                    replacers[rep.Key] = rep.Value;
            var text = Text;

            var replacers2 = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var kv in replacers)
            {
                var value = StringConvertProvider.GetString(kv.Value);
                replacers2.Add(kv.Key, value);
            }

            text = ReplaceMacros(text, replacers2);
            return Environment.ExpandEnvironmentVariables(text);
        }

        /// <summary> Expands the text.</summary>
        public override string ToString()
        {
            return Expand(date: DateTime.Now);
        }
        
        /// <summary> Implicit to string conversion that expands the text of the macroString. This makes it possible to seamlessly switch between string and MacroString in implementation.</summary>
        public static implicit operator string(MacroString macroString)
        {
            return macroString.Expand();
        }
        /// <summary>
        /// Replaces macro strings with the strings in the macroDef dictionary.
        /// If the macro name does not exist in the expanders dictionary.
        /// </summary>
        /// <param name="userString">The string to replace macros in.</param>
        /// <param name="macroDef">The macro definitions.</param>
        /// <param name="macroDefault">Default value if MacroName is not in macroDef.</param>
        /// <returns>A string with macros expanded.</returns>
        internal static string ReplaceMacros(string userString, Dictionary<string, string> macroDef, string macroDefault = "TBD")
        {
            List<macroLocation> macroLocations = macroLocation.GetMacroLocations(userString);
            int removed = 0;
            foreach (var mloc in macroLocations)
            {

                int taglen = mloc.MacroTagLength;
                string inserted;
                if (macroDef.ContainsKey(mloc.MacroName))
                {
                    inserted = macroDef[mloc.MacroName];
                }
            
                else
                {
                    inserted = macroDefault;
                }

                userString = userString.Remove(mloc.MacroTagBegin - removed, mloc.MacroTagLength);
                userString = userString.Insert(mloc.MacroTagBegin - removed, inserted);
                removed += taglen - inserted.Length;
            }
            userString = Environment.ExpandEnvironmentVariables(userString);

            return userString;
        }

        /// <summary>
        /// To keep track of a macro in the user string.
        /// </summary>
        class macroLocation
        {
            /// <summary>
            /// Name of the macro.
            /// </summary>
            public readonly string MacroName;

            /// <summary>
            /// Index of the first character of the macro.
            /// </summary>
            public readonly int MacroBegin;

            /// <summary>
            /// The location of the first macro delimiter.
            /// </summary>
            public int MacroTagBegin
            {
                get
                {
                    return MacroBegin - 1;
                }
            }

            /// <summary>
            /// Location of the last macro delimiter.
            /// </summary>
            public readonly int MacroEnd;

            public int MacroTagEnd
            {
                get { return MacroEnd + 1; }
            }

            /// <summary>
            /// Length from delimiter to delimiter.
            /// </summary>
            public int MacroTagLength
            {
                get { return MacroTagEnd - MacroTagBegin + 1; }
            }


            public macroLocation(string macroName, int macroStart, int macroStop)
            {
                MacroName = macroName;
                MacroBegin = macroStart;
                MacroEnd = macroStop;
            }
            
            static readonly Regex locateMacroRegex = new Regex(@"\<(.*?)\>");

            /// <summary>
            /// Extract macro information from a string.
            /// </summary>
            /// <param name="suppliedString"></param>
            /// <returns></returns>
            public static List<macroLocation> GetMacroLocations(string suppliedString)
            {
                List<macroLocation> locations = new List<macroLocation>();

                // search for things delimited by '<' and '>'.
                for(int i = 0; i < suppliedString.Length; i++)
                {
                    if(suppliedString[i] == '<')
                    {
                        i += 1; // skip to first char.
                        int start = i;
                        for (; i < suppliedString.Length; i++)
                        {
                            if(suppliedString[i] == '>')
                            {
                                int stop = i - 1; // last char thats not a '>'.
                                locations.Add(new macroLocation(suppliedString.Substring(start, stop - start + 1).Trim(), start, stop));
                                break;
                            }
                        }
                    }
                }
                return locations;
            }
        }
    }
}
