namespace OpenTap.Package;

/// <summary>
/// This interface is extremely specific to the image resolver. It mainly exists to facilitate unittesting.
/// </summary>
internal interface IQueryPrereleases
{
    PackageDependencyGraph QueryPrereleases(string os, CpuArchitecture deploymentInstallationArchitecture, string preRelease, string name);
}