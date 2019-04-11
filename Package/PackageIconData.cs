using System;
using System.Collections.Generic;
using System.Text;

namespace OpenTap.Package
{
    /// <summary>
    /// Used for adding icons to a TapPackage.
    /// Add this element to the package.xml inside File (<see cref="PackageFile"/>).
    /// </summary>
    [Display("PackageIcon")]
    public class PackageIconData : ICustomPackageData
    {
    }
}
