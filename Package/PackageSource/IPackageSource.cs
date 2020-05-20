namespace OpenTap.Package
{
    /// <summary>
    /// Interface for determining where a package definition is sourced.
    /// </summary>
    public interface IPackageDefSource
    {
    }

    /// <summary>
    /// The package definition is sourced from a .TapPackage file.
    /// </summary>
    public interface IFilePackageDefSource : IPackageDefSource
    {
        /// <summary>
        /// The file path of the .TapPackage the package definition is loaded from.
        /// </summary>
        string PackageFilePath { get; set; }
    }

    /// <summary>
    /// The package definition is sourced from a .TapPackage file.
    /// </summary>
    public class FilePackageDefSource : IFilePackageDefSource
    {
        /// <summary>
        /// The file path of the .TapPackage the package definition is loaded from.
        /// </summary>
        public string PackageFilePath { get; set; }
    }

    /// <summary>
    /// The package definition is sourced from a package repository.
    /// </summary>
    public interface IRepositoryPackageDefSource : IPackageDefSource
    {
        /// <summary>
        /// The repository the package is sourced.
        /// </summary>
        string RepositoryUrl { get; set; }
    }

    /// <summary>
    /// The package definition is sourced from a remote package repository server.
    /// </summary>
    public class HttpRepositoryPackageDefSource : IRepositoryPackageDefSource
    {
        /// <summary>
        /// A direct url for downloading the package.
        /// </summary>
        public string DirectUrl { get; set;  }
        /// <summary>
        /// The repository the package is sourced.
        /// </summary>
        public string RepositoryUrl { get; set; }
    }

    /// <summary>
    /// The package definition is sourced from a local or remote file system storage. This can be a local folder or a remove shared file system drive.
    /// </summary>
    public class FileRepositoryPackageDefSource : FilePackageDefSource, IRepositoryPackageDefSource
    {
        /// <summary>
        /// The repository the package is sourced.
        /// </summary>
        public string RepositoryUrl { get; set; }
    }

    /// <summary>
    /// The package definition is sourced from an OpenTAP installation.
    /// </summary>
    public class InstalledPackageDefSource : IPackageDefSource
    {
        /// <summary>
        /// The installation the package definition is sourced.
        /// </summary>
        public Installation Installation { get; set; }
        /// <summary>
        /// The file path to the .xml file of the package definition. 
        /// </summary>
        public string PackageDefFilePath { get; set; }
    }
}