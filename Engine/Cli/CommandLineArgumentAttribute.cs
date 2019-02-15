//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap.Cli;
using System;

namespace OpenTap.Cli
{
    /// <summary>
    /// When used on a <see cref="ICliAction"/> this indicates the name and description of the action. ShortName will not be displayed in the TAP CLI help.
    /// When used on a property inside a <see cref="ICliAction"/> all the properties are valid.
    /// The <see cref="Visible"/> indicates whether the property or class will be shown in the help.
    /// </summary>
    /// <remarks>
    /// When used on properties, the property type can be: bool, string, or string[]. If it's bool the argument will not take an argument, but will instead set the property to true.
    /// If it's a string the value of the property will be set to the first occuring value set in the CLI arguments.
    /// If it's a string[] all values set in the CLI arguments will be concatenated into an array.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class CommandLineArgumentAttribute : Attribute
    {
        /// <summary>
        /// Indicates the long name of the command line argument.
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Indicates the short name of the argument. For properties this should be 1 character.
        /// </summary>
        public string ShortName { get; set; }
        /// <summary>
        /// Human readable description of the argument.
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Indicates whether this will be shown when writing CLI usage information.
        /// </summary>
        public bool Visible { get; set; }

        /// <summary>
        /// Primary constructor.
        /// </summary>
        /// <param name="name">Long name of the argument.</param>
        public CommandLineArgumentAttribute(string name)
        {
            this.Name = name;
            ShortName = null;
            Description = null;
            Visible = true;
        }
    }

    /// <summary>
    /// Used on properties of a <see cref="ICliAction"/> to define an unnamed arguments on the CLI. These can be ordered to handle cases where some values are required and others can occur multiple times.
    /// The property type will indicate how many will be consumed. The type can be either string or string[]. In case of string[] all the remaining arguments will be assigned to this property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class UnnamedCommandLineArgument : Attribute
    {
        /// <summary>
        /// Order or the arguments. The lowest value comes first.
        /// </summary>
        public int Order { get; set; }
        /// <summary>
        /// The name of the property. This will be shown in the CLI usage output.
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Indicates whether this argument is required or optional. If it's a string[] and required then this indicates that it needs at least one value.
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name"></param>
        public UnnamedCommandLineArgument(string name)
        {
            this.Name = name;
            Order = 0;
            Required = false;
        }
    }
}
