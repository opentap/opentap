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
using OpenTap.Authentication;

namespace OpenTap.Package
{
    /// <summary>
    /// Indicates a well defined action to be performed on a package.
    /// A number of common actions are <see cref="PackageInstallAction"/>, <see cref="PackageUninstallAction"/>, and <see cref="PackageDownloadAction"/>.
    /// Deriving from this, and annotating the class and any public properties with <see cref="CommandLineArgumentAttribute"/> and <see cref="UnnamedCommandLineArgument"/> attributes
    /// will allow it to be called from the OpenTAP.Package CLI.
    /// </summary>
    public abstract class PackageAction : ICliAction
    {
        /// <summary> Log source for PackageAction plugins. </summary>
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

        internal string[] ExtractRepositoryTokens(string[] repositories, bool saveSettings)
        {
            if (repositories == null) return null;
            // Repositories can have additional arguments appended as key-value-pairs. 
            // Currently, the only supported key is 'token=<repo token>'
            // The goal here is the following:
            // 1. Extract the tokens
            // 2. Add them to AuthenticationSettings
            // 3. Save the updated settings if needed and requested
            // 4. Return the list of repositories without the kvps
            var result = new string[repositories.Length];
            bool tokenAdded = false;

            for (int i = 0; i < repositories.Length; i++)
            {
                var argument = repositories[i];

                var parts = argument.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                var repo = parts[0];
                string token = null;

                for (int j = 1; j < parts.Length; j++)
                {
                    var part = parts[j];
                    // A user token could contain an equal sign. It should be fine if we just use the first equal
                    // sign as a pivot. This is also the way environment variables are handled on Unix systems.
                    var pivot = part.IndexOf('=');
                    if (pivot == -1)
                    {
                        log.Warning($"Missing '=' sign in key-value-pair '{part}'. This value will be ignored.");
                        continue;
                    }

                    var key = part.Substring(0, pivot);
                    var value = part.Substring(pivot + 1);
                    switch (key)
                    {
                        case "token":
                            token = value;
                            break;
                        default:
                            log.Warning(
                                $"Unrecognized key '{key}' specified in repository argument '{argument}'. This value will be ignored.");
                            break;
                    }
                }

                // Only accepts tokens for http repositories
                var repoType = PackageRepositoryHelpers.DetermineRepositoryType(repo);
                if (repoType is HttpPackageRepository && Uri.TryCreate(repoType.Url, UriKind.Absolute, out var uri))
                {
                    repo = repoType.Url;
                    // Add the specified token to the current authentication settings
                    if (token != null)
                    {
                        tokenAdded = true;
                        // If a token is already configured for this repo, update it
                        if (AuthenticationSettings.Current.Tokens.FirstOrDefault(t => t.Domain.Equals(uri.Authority)) is
                            TokenInfo t)
                        {
                            t.AccessToken = token;
                        }
                        // Otherwise, add it
                        else
                        {
                            AuthenticationSettings.Current.Tokens.Add(new TokenInfo(token, null, uri.Authority));
                        }
                    }
                }

                result[i] = repo;
            }

            if (saveSettings && tokenAdded)
            {
                try
                {
                    AuthenticationSettings.Current.Save();
                }
                catch (Exception e)
                {
                    log.Warning($"Error saving credentials: '{e.Message}");
                    log.Debug(e);
                }
            }

            return result;
        }
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
