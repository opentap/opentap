//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenTap.Cli
{
    /// <summary>
    /// Parser for command line arguments. Supports --,-,/ based argument options 
    /// as well as unnamed options mixed with named ones.
    /// </summary>
    internal class ArgumentsParser
    {
        public ArgumentCollection AllOptions = new ArgumentCollection();

        struct optionFindResult
        {
            public Argument FoundOption;
            public bool IsUnknown;
            public string InlineArg;
        }

        optionFindResult findOption(string st, ArgumentCollection options)
        {
            optionFindResult opt = new optionFindResult { IsUnknown = false };
            int len = 0;
            if (st.StartsWith("--"))
            {
                len = 2;
            }
            else if (st.StartsWith("-")) // Removed check for starting with '\'. It breaks installing a pacakge from network drive because it starts with \\.
            {
                len = 1;
            }
            if (len == 0)
            {
                return opt;
            }

            st = st.Substring(len);
            var idx = st.IndexOf('=');
            if (idx > 0)
            {
                opt.InlineArg = st.Substring(idx + 1);
                st = st.Substring(0, idx);
            }
            opt.FoundOption = options
                .Where(o => o.Value.CompareTo(st))
                .FirstOrDefault().Value;

            if (opt.FoundOption == null)
            {
                opt.FoundOption = AllOptions
                        .Where(o => o.Value.CompareTo(st))
                        .FirstOrDefault().Value;
            }

            if (opt.FoundOption == null)
            {
                opt.IsUnknown = true;
            }
            else
            {
                opt.FoundOption = opt.FoundOption.Clone();
            }
            return opt;
        }

        public ArgumentCollection Parse(string[] rawArgs)
        {
            ArgumentCollection options = AllOptions.CreateDefault();
            List<string> restList = options.UnnamedArguments.ToList();
            for (int i = 0; i < rawArgs.Length; i++)
            {
                string arg = rawArgs[i];
                optionFindResult optResult = findOption(arg, options);
                Argument opt = optResult.FoundOption;
                if (opt == null)
                {
                    if (optResult.IsUnknown == false)
                    {
                        restList.Add(arg);
                    }
                    else
                    {
                        options.UnknownsOptions.Add(arg);
                    }
                    continue;
                }

                if (opt.NeedsArgument)
                {
                    if (optResult.InlineArg != null)
                    {
                        opt.Values.Add(optResult.InlineArg);
                    }
                    else if (i + 1 < rawArgs.Length)
                    {
                        opt.Values.Add(rawArgs[++i]);
                    }
                    else
                    {
                        options.MissingArguments.Add(opt);
                        continue;
                    }
                }
                else
                {
                    if (optResult.InlineArg != null)
                    {
                        // Add the value of the inline arg, even if NeedsArgument was not specified
                        opt.Values.Add(optResult.InlineArg);
                    }
                }
                options.Add(opt);
            }
            options.UnnamedArguments = restList.ToArray();
            return options;
        }
    }

    internal class Argument
    {
        /// <summary>
        /// Optional. Used with one '-' or a '/'.
        /// </summary>
        public char ShortName { get; private set; }
        /// <summary>
        /// Non optional. used with '--' or '/'. Also used for argument lookup.
        /// </summary>
        public string LongName { get; private set; }
        /// <summary>
        /// If an argument is required for this option.
        /// </summary>
        public bool NeedsArgument { get; private set; }
        /// <summary>
        /// Argument given to this option.
        /// </summary>
        public string Value { get { return Values.FirstOrDefault(); } }
        /// <summary>
        /// Argument given to this option. Also used as a default.
        /// </summary>
        public List<string> Values { get; private set; }
        /// <summary>
        /// Short description for this option.
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Initializes a new instance of the Option class.
        /// </summary>
        public Argument(string longName, char shortName = default(char), bool needsArgument = true, string description = "", string defaultArg = null)
        {
            ShortName = shortName;
            LongName = longName;
            NeedsArgument = needsArgument;
            Description = description;
            Values = new List<string>();
            if (String.IsNullOrWhiteSpace(defaultArg) == false)
            {
                Values.Add(defaultArg);
            }
        }

        /// <summary>
        /// Clones the option with a new argument
        /// </summary>
        /// <param name="argument"></param>
        /// <returns></returns>
        public Argument Clone(string argument)
        {
            var opt = Clone();
            opt.Values = new List<string> { argument };
            return opt;
        }

        public Argument Clone()
        {
            return new Argument(LongName, ShortName, NeedsArgument)
            {
                Values = new List<string>(Values),
                Description = Description
            };
        }

        public bool CompareTo(string arg)
        {
            if (arg.Length == 1 && ShortName != default(char))
            {
                return arg[0] == ShortName;
            }
            return arg == LongName;
        }
    }

    /// <summary>
    /// A collection of options optionally with arguments.
    /// Also includes Unnamed arguments and in case of errors unknown options and missing arguments
    /// This class is used both as and input and output to option parsing.
    /// </summary>
    internal class ArgumentCollection : Dictionary<string, Argument>
    {
        public string[] UnnamedArguments { get; set; }

        public List<string> UnknownsOptions { get; set; }
        public List<Argument> MissingArguments { get; set; }

        public ArgumentCollection()
        {
            UnnamedArguments = new string[0];
            UnknownsOptions = new List<string>();
            MissingArguments = new List<Argument>();
        }

        public Argument Add(Argument option)
        {
            this[option.LongName] = option;
            return option;
        }

        public Argument Add(string longName, char shortName = default(char),
            bool needsArgument = true, string description = "", string defaultArgument = null)
        {
            var option = new Argument(longName, shortName, needsArgument, description, defaultArgument);
            return Add(option);
        }

        public bool Contains(string optionName)
        {
            return ContainsKey(optionName);
        }

        public Argument GetOrDefault(string optionLongName)
        {
            if (Contains(optionLongName))
            {
                return this[optionLongName];
            }
            return null;
        }

        public string Argument(string optionLongName)
        {
            return this[optionLongName].Value;
        }

        public string GetArgumentOrDefault(string optionLongName, string def = default(string))
        {
            var opt = GetOrDefault(optionLongName);
            if (opt != null)
            {
                return opt.Value;
            }
            return def;
        }
        /// <summary>
        /// Transfers an option from one
        /// </summary>
        /// <param name="optionName"></param>
        /// <param name="from"></param>
        /// <returns></returns>
        public Argument TakeOption(string optionName, ArgumentCollection from)
        {
            if (from.Contains(optionName))
            {
                this[optionName] = from[optionName].Clone();
                return this[optionName];
            }
            return null;
        }

        public ArgumentCollection CreateDefault()
        {
            ArgumentCollection opt = new ArgumentCollection { UnnamedArguments = UnnamedArguments };
            foreach (var key in Keys)
            {
                if (this[key].Value != null)
                {
                    opt[key] = this[key];
                }
            }
            return opt;
        }

        /// <summary>
        /// Converts the ArgumentCollection to a help-string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder output = new StringBuilder();
            foreach (var option in this)
            {
                var opt = option.Value;
                var arg = "--" + opt.LongName;
                if (opt.ShortName != default(char))
                {
                    arg = String.Format("-{0}, {1}", opt.ShortName, arg);
                }
                arg = "  " + arg;
                foreach (var descSplit in opt.Description.Split('\n'))
                {
                    arg = arg + new String(' ', 22 - arg.Length) + descSplit;
                    output.AppendLine(arg);
                    arg = "";
                }
            }
            return output.ToString();
        }
    }
}
