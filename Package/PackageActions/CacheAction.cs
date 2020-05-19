//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.ComponentModel;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Package
{
    /// <summary>
    /// Hidden CLI sub command `tap package cache` that allows clearing the package cache.
    /// </summary>
    [Display("cache", "Operations related to the package cache.", "package" )]
    [Browsable(false)]
    public class CacheAction : LockingPackageAction
    {
        /// <summary> When set, this action clears the package cache </summary>
        [CommandLineArgument("clear", Description = "Clear the package cache.")]
        public bool ClearCache { get; set; }
        /// <summary> When set, this action clears the package cache </summary>
        protected override int LockedExecute(CancellationToken cancellationToken)
        {
            if (ClearCache)
            {
                PackageCacheHelper.ClearCache();
            }
            return 0;
        }
    }
}
