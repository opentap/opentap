using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    /// <summary>
    /// ExitCodes relevant to Package CLI actions. Uses integer range 30 to 59
    /// </summary>
    public enum PackageExitCodes
    {
        /// <summary>
        /// Errors occured while creating package
        /// </summary>
        [Display("PackageCreateError", "Errors occured while creating package")]
        PackageCreateError = 30,
        /// <summary>
        /// Invalid data in package definition
        /// </summary>
        [Display("InvalidPackageDefinition", "Invalid data in package definition")]
        InvalidPackageDefinition = 31,
        /// <summary>
        /// Package name is invalid
        /// </summary>
        [Display("InvalidPackageName", "Package name is invalid")]
        InvalidPackageName = 32,
        /// <summary>
        /// Package dependency error occurred
        /// </summary>
        [Display("PackageDependencyError", "Package dependency error occurred")]
        PackageDependencyError = 33,
        /// <summary>
        /// Assembly dependencies conflict
        /// </summary>
        [Display("AssemblyDependencyError", "Assembly dependencies conflict")]
        AssemblyDependencyError = 34,
        /// <summary>
        /// Error occurred while installing package
        /// </summary>
        [Display("PackageInstallError", "Error occurred while installing package")]
        PackageInstallError = 35,
    }
}
