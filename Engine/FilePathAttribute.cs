//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{
    /// <summary>
    /// Identifies a string as a file path.
    /// </summary>
    public class FilePathAttribute : Attribute
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

        /// <summary>Constructor that takes BehaviourChoice and fileExtension parameters for the FilePathAttribute.</summary>
        /// <param name="behavior"></param>
        /// <param name="fileExtension">e.g .txt, .csv, ...</param>
        public FilePathAttribute(BehaviorChoice behavior = BehaviorChoice.Open, string fileExtension = "")
        // note default value cannot be null when using VS2010. see http://stackoverflow.com/questions/15048847/attribute-argument-must-be-a-constant-expression
        {
            Behavior = behavior;
            FileExtension = fileExtension;
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
