//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap.Package
{
    /// <summary>
    /// Settings class containing plugin package manager settings
    /// </summary>
    [Display("Package Manager")]
    [Browsable(false)]
    [HelpLink("EditorHelp.chm::/Package Manager Help/Readme.html")]
    public class PackageManagerSettings : ComponentSettings<PackageManagerSettings>
    {
        /// <summary>
        /// Creates a new PackageManagerSettings. 
        /// User code should use PackageManagerSettings.Current to access the singleton instead of constructing a new object.
        /// </summary>
        public PackageManagerSettings()
        {
            Repositories = new List<RepositorySettingEntry>();
            Repositories.Add(new RepositorySettingEntry { IsEnabled = true, Url = new Uri(Path.GetFullPath(ExecutorClient.ExeDir)).AbsoluteUri });
            Repositories.Add(new RepositorySettingEntry { IsEnabled = true, Url = "https://packages.opentap.io" });
            UseLocalPackageCache = true;
        }

        /// <summary>
        /// When true a packages cached in the user-wide package cache (shared accross installations, but not accross users) is used when in addition to the repositories specified in <see cref="Repositories"/>.
        /// </summary>
        [Display("Use Local Package Cache", Group: "Package Repositories", Order: 3, Description: "Use package cache (shared across installations, but not across users) in addition to repositories specified here.")]
        public bool UseLocalPackageCache { get; set; }

        /// <summary>
        /// When true a package management UI should also list packages that are not compatible with the current installation.
        /// </summary>
        [Display("Show Incompatible Packages", Group: "General", Description: "Show all packages, including incompatible and deprecated packages.")]
        public bool ShowIncompatiblePackages { get; set; }

        /// <summary>
        /// Determines whether tap.exe will run an update check against configured repositories at startup.
        /// </summary>
        [Display("Check for Updates at Startup", Group: "General", Description: "Checks for updates against enabled package repositories. The update check sends anonymized package idenfiers to the enabled repositories.")]
        public bool CheckForUpdates { get; set; } = true;

        /// <summary>
        /// Specifies how a UI should order a list of different version of the same package name. Can be either by version or build date.
        /// </summary>
        [System.Xml.Serialization.XmlIgnore]
        [Browsable(false)]
        [Display("Sort Package Details By", Group: "General", Description: "Sorts the selected package's other versions, shown in the details window.")]
        public PackageSort Sort { get; set; }

        /// <summary>
        /// Specifies how a UI should order a list of different version of the same package name. Can be either by version or build date.
        /// </summary>
        public enum PackageSort
        {
            /// <summary> Sort packages by version number. </summary>
            Version,
            /// <summary> Sort packages by build date. </summary>
            Date
        }
        
        /// <summary>
        /// List of servers from where new plugin packages can be discovered and downloaded.
        /// </summary>
        [Display("URLs", Group: "Package Repositories", Order: 2, Description: "URLs or file-system paths from where plugin packages can be found. Example: https://packages.opentap.io")]
        [Layout(LayoutMode.FullRow)]
        public List<RepositorySettingEntry> Repositories { get; set; }

        /// <summary>
        /// Get an IPackageRepository for each of the repos defined in <see cref="Repositories"/> plus one for the cache if <see cref="UseLocalPackageCache"/> is enabled.
        /// </summary>
        /// <returns></returns>
        internal List<IPackageRepository> GetEnabledRepositories(IEnumerable<string> cliSpecifiedRepoUrls = null)
        {
            var repositories = new List<IPackageRepository>();
            if (PackageManagerSettings.Current.UseLocalPackageCache)
                repositories.Add(PackageRepositoryHelpers.DetermineRepositoryType(new Uri(PackageCacheHelper.PackageCacheDirectory).AbsoluteUri));
            if (cliSpecifiedRepoUrls == null)
                repositories.AddRange(PackageManagerSettings.Current.Repositories.Where(p => p.IsEnabled && p.Manager != null).Select(s => s.Manager).ToList());
            else
            {
                var log = Log.CreateSource("PackageAction");
                foreach (var repo in  cliSpecifiedRepoUrls)
                {
                    if (Uri.IsWellFormedUriString(repo, UriKind.Relative) && !Directory.Exists(repo))
                    {
                        log.Info($"Package directory '{repo}' not found. Trying using HTTP scheme.");
                        repositories.Add(PackageRepositoryHelpers.DetermineRepositoryType("http://" + repo));
                    }
                    else
                        repositories.Add(PackageRepositoryHelpers.DetermineRepositoryType(repo));
                }
            }
            return repositories;
        }
    }

    /// <summary>
    /// Structure used by PackageRepositories setting
    /// </summary>
    public class RepositorySettingEntry : ValidatingObject
    {
        string _Url;
        /// <summary>
        /// URL to the server
        /// </summary>
        [Display("URL", Description: "Specify URL to a local or remote repository hosting OpenTAP plugin packages.")]
        public string Url
        {
            get
            {
                return _Url;
            }
            set
            {
                if (_Url == value)
                    return;
                _Url = value;
                _Manager = null; // invalidate the cached manager, now that the URL changed
                OnPropertyChanged("Url");
                OnPropertyChanged("Error");
            }
        }

        private bool _IsEnabled;
        /// <summary>
        /// If disabled this server will not be contacted when discovering available plugins.
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                return _IsEnabled;
            }
            set
            {
                if (_IsEnabled == value)
                    return;
                _IsEnabled = value;
                _Manager = null;
                OnPropertyChanged("IsEnabled");
                OnPropertyChanged("Error");
            }
        }

        // a cached manager
        IPackageRepository _Manager = null;

        /// <summary>
        /// Get a cached instance of IPackageRepository that can query the repository that this RepositorySettingEntry represents. 
        /// </summary>
        /// <value></value>
        public IPackageRepository Manager
        {
            get
            {
                if (_Manager == null)
                {
                    if (String.IsNullOrEmpty(Url))
                        return null;
                    _Manager = PackageRepositoryHelpers.DetermineRepositoryType(Url);
                }
                return _Manager;
            }
        }

        /// <summary>
        /// Obsolete. Always false.
        /// </summary>
        [Browsable(false)]
        [XmlIgnore]
        [Obsolete]
        public bool IsBusy { get; set; }
    }
}
