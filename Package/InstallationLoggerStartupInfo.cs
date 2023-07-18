using System.Linq;

namespace OpenTap.Package
{
    /// <summary>
    /// Log information about the current installation at startup.
    /// </summary>
    internal class InstallationLoggerStartupInfo : IStartupInfo
    {
        /// <summary>
        /// Log information about the current installation at startup.
        /// </summary>
        public void LogStartupInfo()
        {
            var log = Log.CreateSource("Installation");
            var packages = Installation.Current.GetPackages();
            var longestName = packages.Max(p => p.Name.Length);
            foreach (var pkg in packages)
            {
                var padded = pkg.Name.PadRight(longestName);
                log.Debug($"{padded} - {pkg.Version}");
            }
        }
    }
}