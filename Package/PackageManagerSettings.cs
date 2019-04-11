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
        public PackageManagerSettings()
        {
            Repositories = new List<RepositorySettingEntry>();
            Repositories.Add(new RepositorySettingEntry { IsEnabled = true, Url = ExecutorClient.ExeDir });
            Repositories.Add(new RepositorySettingEntry { IsEnabled = true, Url = PackageDef.SystemWideInstallationDirectory });
            Repositories.Add(new RepositorySettingEntry { IsEnabled = false, Url = "http://packages.opentap.io" });
        }

        [Display("Show Incompatible Packages", Group: "General", Description: "Show all packages, including incompatible and deprecated packages.")]
        public bool ShowIncompatiblePackages { get; set; }

        [System.Xml.Serialization.XmlIgnore]
        [Browsable(false)]
        [Display("Sort Package Details By", Group: "General", Description: "Sorts the selected package's other versions, shown in the details window.")]
        public PackageSort Sort { get; set; }

        public enum PackageSort
        {
            Version,
            Date
        }
        
        /// <summary>
        /// List of servers from where new plugin packages can be discovered and downloaded.
        /// </summary>
        [Display("URLs", Group: "Package Repositories", Order: 2, Description: "URLs from where new plugin packages can be discovered and downloaded.")]
        public List<RepositorySettingEntry> Repositories
        {
            get;
            set;
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

        [Browsable(false)]
        [XmlIgnore]
        public bool IsBusy { get; set; }
    }
}
