using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenTap
{
    internal class SystemStartupInfo : IStartupInfo
    {
        public void LogStartupInfo()
        {
            var log = Log.CreateSource("SystemInfo");
            if (!String.IsNullOrEmpty(RuntimeInformation.OSDescription))
                log.Debug("{0}{1}", RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture); // This becomes something like "Microsoft Windows 10.0.14393 X64"

            if (!String.IsNullOrEmpty(RuntimeInformation.FrameworkDescription))
                log.Debug(RuntimeInformation.FrameworkDescription); // This becomes something like ".NET Framework 4.6.1586.0"
            var version = SemanticVersion.Parse(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
            log.Debug("OpenTAP Engine {0} {1}", version, RuntimeInformation.ProcessArchitecture);
        }
    }
}