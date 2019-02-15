//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    /// <summary>
    /// Indicates a well defined action to be performed on a package.
    /// A number of common actions are <see cref="PackageInstallAction"/>, <see cref="PackageUninstallAction"/>, and <see cref="PackageRunCommandAction"/>.
    /// Deriving from this, and annotating the class and any public properties with <see cref="CommandLineArgumentAttribute"/> and <see cref="UnnamedCommandLineArgument"/> attributes
    /// will allow it to be called from the OpenTAP.Package CLI.
    /// </summary>
    public abstract class PackageAction : ICliAction
    {
        protected static TraceSource log =  OpenTap.Log.CreateSource("PackageAction");

        /// <summary>
        /// A delegate used by <see cref="ProgressUpdate"/>
        /// </summary>
        /// <param name="progressPercent">Indicates progress from 0 to 100.</param>
        /// <param name="message"></param>
        public delegate void ProgressUpdateDelegate(int progressPercent, string message);
        /// <summary>
        /// Called by the action to indicate how far it has gotten. Will usually be called with a progressPercent of 100 to indicate that it is done.
        /// </summary>
        public event ProgressUpdateDelegate ProgressUpdate;

        /// <summary>
        /// A delegate type used by the <see cref="Error"/> event.
        /// </summary>
        /// <param name="ex"></param>
        public delegate void ErrorDelegate(Exception ex);
        /// <summary>
        /// Called when a critical error happens.
        /// </summary>
        public event ErrorDelegate Error;

        /// <summary>
        /// Call this to raise the <see cref="Error"/> event.
        /// </summary>
        /// <param name="ex"></param>
        protected void RaiseError(Exception ex)
        {
            if (Error != null)
                Error(ex);
        }

        /// <summary>
        /// Call this to raise the <see cref="ProgressUpdate"/> event.
        /// </summary>
        /// <param name="progressPercent"></param>
        /// <param name="message"></param>
        protected void RaiseProgressUpdate(int progressPercent, string message)
        {
            if (ProgressUpdate != null)
                ProgressUpdate(progressPercent, message);
        }

        /// <summary>
        /// The code to be executed by the action.
        /// </summary>
        /// <returns>Return 0 to indicate success. Otherwise return a custom errorcode that will be set as the exitcode from the CLI.</returns>
        public abstract int Execute(CancellationToken cancellationToken);
    }

    internal static class PackageActionHelper
    {
        private readonly static TraceSource log =  OpenTap.Log.CreateSource("PackageAction");

        /// <summary>
        /// Logs the assembly name and version then executes the action with the given parameters.
        /// </summary>
        /// <param name="action">The oackage action to be executed.</param>
        /// <param name="parameters">The parameters for the action.</param>
        /// <returns>Return 0 to indicate success. Otherwise return a custom errorcode that will be set as the exitcode from the CLI.</returns>
        public static int Execute(this PackageAction action, string[] parameters)
        {
            action.LogAssemblyNameAndVersion();
            ICliAction cliAction = action;
            return cliAction.PerformExecute(parameters);
        }

        public static List<PackageDef> FilterPreRelease(this List<PackageDef> packages, string PreRelease)
        {
            if (PreRelease != null)
                packages = packages.Where(p => (p.Version.PreRelease ?? "").ToLower() == (PreRelease ?? "").ToLower()).ToList();
            else
            {
                var filteredPackages = new List<PackageDef>();
                var packageGroups = packages.GroupBy(p => p.Name);
                foreach (var item in packageGroups)
                {
                    if (item.Any(p => string.IsNullOrEmpty(p.Version.PreRelease)))
                        filteredPackages.AddRange(item.Where(p => string.IsNullOrEmpty(p.Version.PreRelease)));
                    else
                        filteredPackages.AddRange(item);
                }

                packages = filteredPackages;
            }

            return packages;
        }
        
        private static void LogAssemblyNameAndVersion(this PackageAction action)
        {
            log.Debug("{0} version {1}", typeof(Installer).Assembly.GetName().Name, typeof(Installer).Assembly.GetName().Version.ToString(3));
        }
    }
}
