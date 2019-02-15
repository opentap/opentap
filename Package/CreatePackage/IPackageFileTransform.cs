//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    /// <summary>
    /// Generic plugin type that can transform files in a TAP package. Do not implement this directly, instead implement 
    /// <see cref="IPackageObfuscator"/>, <see cref="IPackageSigner"/> or <see cref="IPackageLicenseInjector"/>
    /// </summary>
    public interface IPackageFileTransform : ITapPlugin
    {
        bool CanTransform(PackageDef package);
        bool Transform(string tempDir, PackageDef package);
        double GetOrder(PackageDef package);
    }
    
    public interface IPackageLicenseInjector : IPackageFileTransform
    {

    }
    
    public interface IPackageSigner : IPackageFileTransform
    {
    }
    
    public interface IPackageObfuscator : IPackageFileTransform
    {
    }

}
