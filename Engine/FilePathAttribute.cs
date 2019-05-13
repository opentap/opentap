//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;

namespace OpenTap
{
    /// <summary>
    /// Identifies a string as a file path.
    /// </summary>
    public class FilePathAttribute : Attribute, IAnnotation
    {
        ///<summary>
        /// Default file extension for this file path.
        /// </summary>
        public string FileExtension { get; private set; }

        /// <summary>
        /// Boolean setting. When true, raises a prompt for permission to overwrite the file if it already exists. 
        /// If false, no prompt is raised and the file is overwritten.
        /// </summary>
        public bool OverwritePrompt { get; private set; }

        /// <summary>
        /// The behavior of the file path dialog associated with this file path.
        /// </summary>
        public BehaviorChoice Behavior { get; private set; }

        /// <summary>Constructor for the FilePathAttribute.</summary>
        /// <remarks>Provided for backward compatibility.</remarks>
        public FilePathAttribute()
        {
            Behavior = BehaviorChoice.Open;
            FileExtension = "";
        }

        class InvalidFilterSpecification : FormatException
        {
            string filter;
            public InvalidFilterSpecification(string filter)
            {
                this.filter = filter;
            }

            public override string Message => $"Invalid file filter specification: '{filter}'";

        }

        static readonly HashSet<char> invalidchars = System.IO.Path.GetInvalidFileNameChars().ToHashSet();
        void validateFileFilter(string str)
        {
            if (string.IsNullOrEmpty(str)) return;
            if (str.Contains("*.") == false) return;
            var s = str.Split('|');
            if (s.Length % 2 == 1) throw new InvalidFilterSpecification(str);
            for(int i = 0; i < s.Length; i += 2)
            {
                var name = s[i];
                var value = s[i + 1];
                if (value.Contains("*.") == false || value.Contains(","))
                    throw new InvalidFilterSpecification(str);
                var filters = value.Split(';');
                foreach(var filter in filters)
                {
                    var filter2 = filter.Trim();
                    
                    if(filter2.StartsWith("*.") == false)
                        throw new InvalidFilterSpecification(str);
                    filter2 = filter2.Substring(2);
                    if (filter2 == "*")
                        continue;
                    foreach(var chr in filter2)
                    {
                        if(invalidchars.Contains(chr))
                            throw new InvalidFilterSpecification(str);
                    }
                    
                }


            }
            
        }

        /// <summary>Constructor that takes BehaviourChoice and fileExtension parameters for the FilePathAttribute.</summary>
        /// <param name="behavior"></param>
        /// <param name="fileExtension">File extension or filter. Simple file extensions can be used like this: txt, csv, .... Or a filter extression can be used, for example "Text Document (*.txt) | *.txt. See examples for more info."  </param>
        public FilePathAttribute(BehaviorChoice behavior = BehaviorChoice.Open, string fileExtension = "")
        // note default value cannot be null when using VS2010. see http://stackoverflow.com/questions/15048847/attribute-argument-must-be-a-constant-expression
        {
            Behavior = behavior;
            FileExtension = fileExtension;
            validateFileFilter(FileExtension);
        }

        /// <summary>
        /// Describes how the file should be used on access.
        /// </summary>
        public enum BehaviorChoice
        {
            /// <summary>
            /// Opens the file.
            /// </summary>
            Open,
            /// <summary>
            /// Saves the file. Provides a warning if file already exists.
            /// </summary>
            Save
        };
    }

}
