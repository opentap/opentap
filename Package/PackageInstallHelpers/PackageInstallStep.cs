using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;

namespace OpenTap.Package.PackageInstallHelpers
{
    [Browsable(false)]
    class PackageUninstallStep : TestStep
    {
        public string Target { get; set; }
        public string[] Packages { get; set; }
        public bool Force { get; set; }
        public override void Run()
        {
            var action = new PackageUninstallAction()
            {
                NonInteractive = true,
                Force = Force,
                Packages = Packages,
                Target = Target,
                AlreadyElevated = true,
            };
            try
            {
                var result = action.Execute(CancellationToken.None);
                UpgradeVerdict(result == 0 ? Verdict.Pass : Verdict.Fail);
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                Log.Debug(e);
                UpgradeVerdict(Verdict.Error);
            }
        }
    }
    
    /// <summary>
    /// Run all CustomPackageActionStep install implementations in the target installation for the given packages. 
    /// </summary>
    [Browsable(false)]
    class CustomPackageActionStep : TestStep
    {
        public string[] Packages { get; set; }
        public string Target { get; set; } 
        public bool Force { get; set; }
        public PackageActionStage ActionStage { get; set; }
        public string TemporaryDirectory { get; set; }
        public override void Run()
        {
            // We don't want to load plugins from the default installation. Preferably only the target installation
            // PluginManager.DirectoriesToSearch.Clear();
            PluginManager.DirectoriesToSearch.Add(Target);
            PluginManager.SearchAsync();

            var packages = Packages.Select(PackageDef.FromPackage).ToArray();

            try
            {
                foreach (var package in packages)
                {
                    CustomPackageActionHelper.RunCustomActions(package, ActionStage,
                        new CustomPackageActionArgs(TemporaryDirectory, Force));
                }

                UpgradeVerdict(Verdict.Pass);
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                Log.Debug(e);
                UpgradeVerdict(Verdict.Error);
            }
        }
    }

    [Browsable(false)]
    class PackageInstallStep : TestStep
    {
        public string Target { get; set; }
        public string[] Packages { get; set; }
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
                Packages = Packages,
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
                Log.Error(e.Message);
                Log.Debug(e);
                UpgradeVerdict(Verdict.Error);
            }
        }
    }
}