﻿
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
        [Display("Package Create Error", "Errors occured while creating package")]
        PackageCreateError = 30,
        /// <summary>
        /// Invalid data in package definition
        /// </summary>
        [Display("Invalid Package Definition", "Invalid data in package definition")]
        InvalidPackageDefinition = 31,
        /// <summary>
        /// Package name is invalid
        /// </summary>
        [Display("Invalid Package Name", "Package name is invalid")]
        InvalidPackageName = 32,
        /// <summary>
        /// Package dependency error occurred
        /// </summary>
        [Display("Package Dependency Error", "Package dependency error occurred")]
        PackageDependencyError = 33,
        /// <summary>
        /// Assembly dependencies conflict
        /// </summary>
        [Display("Assembly Dependency Error", "Conflicting assembly dependencies")]
        AssemblyDependencyError = 34,
        /// <summary>
        /// Error occurred while installing package
        /// </summary>
        [Display("Package Install Error", "Error occurred while installing package")]
        PackageInstallError = 35,
        /// <summary>
        /// Error occurred while uninstalling package
        /// </summary>
        [Display("Package Uninstall Error", "Error occurred while uninstalling package")]
        PackageUninstallError = 36,
        /// <summary>
        /// Error occurred while creating package
        /// </summary>
        [Display("Invalid Archive Error", "Error occurred while creating package")]
        InvalidPackageArchive = 37,
        /// <summary>
        /// EULA not accepted.
        /// </summary>
        [Display("EULA Not Accepted", "User did not accept the End User License Agreement")]
        EulaNotAccepted = 38,
    }
}
