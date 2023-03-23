using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;

namespace OpenTap.Package.PackageInstallHelpers
{
    [Browsable(false)]
    class PackageInstallStep : TestStep
    {
        public string Target { get; set; }
        public PackageDef[] Packages { get; set; }
        public bool Force { get; set; }
        public string[] Repositories { get; set; }
        
        public bool SystemWideOnly { get; set; }

        public override void Run()
        {
            var action = new PackageInstallAction()
            {
                InstallDependencies = false,
                NonInteractive = true,
                Force = Force,
                PackageReferences = Packages.Select(p => new PackageSpecifier(p.Name,
                        new VersionSpecifier(p.Version, VersionMatchBehavior.Exact), p.Architecture, p.OS))
                    .ToArray(),
                Target = Target,
                Repository = Repositories,
                SystemWideOnly = SystemWideOnly,
                AlreadyElevated = true,
            };

            try
            {
                var result = action.Execute(CancellationToken.None);
                UpgradeVerdict(result == 0 ? Verdict.Pass : Verdict.Fail);
            }
            catch(Exception e)
            {
                Log.Debug(e);
                UpgradeVerdict(Verdict.Error);
            }
        }
    }
}