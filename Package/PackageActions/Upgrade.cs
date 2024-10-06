using System.ComponentModel;
using System.Linq;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Package
{
    [Display("upgrade", Group: "package")]
    internal class Upgrade : IsolatedPackageAction
    {
        [CommandLineArgument("repository", Description = CommandLineArgumentRepositoryDescription, ShortName = "r")]
        public string[] Repository { get; set; }


        protected override int LockedExecute(CancellationToken cancellationToken)
        {
            var repositories = PackageManagerSettings.Current.GetEnabledRepositories(Repository);
            var updates = UpdateCheck.GetUpdates(cancellationToken, repositories);

            if (!updates.Any())
            {
                log.Info("Nothing to do.");
                return 0;
            }

            var installedPackages = Installation.Current.GetPackages().ToArray();
            var image = new ImageSpecifier();
            foreach (var update in updates.GroupBy(p => p.Name))
            {
                var updatedPackage = update.FindMax(p => p.Version);
                if (updatedPackage == null) continue;
                image.Packages.Add(updatedPackage.GetSpecifier());
            }
            
            image.Repositories.AddRange(repositories.Select(r => r.Url));
            
            var resolved = image.MergeAndResolve(new Installation(Target), cancellationToken);

            log.Info("The following updates will be installed:");
            foreach (var update in resolved.Packages)
            {
                var currentPackage = installedPackages.FirstOrDefault(p => p.Name == update.Name);
                if (currentPackage == null)
                {
                    log.Info($" - {update.Name}: {update.Version} (New package)");
                    continue;
                }
                if (currentPackage.Version == update.Version) continue;
                log.Info($" - {update.Name}: {currentPackage.Version} -> {update.Version}");
            }

            var question = new ContinueRequest();
            UserInput.Request(question);
            if (question.Response == ContinueResponse.Cancel)
                return 0;           
            
            resolved.Deploy(Target, cancellationToken);

            return 0;
        }
        
        enum ContinueResponse
        {
            Continue,
            Cancel,
        }

        class ContinueRequest
        {
            [Browsable(true)]
            [Layout(LayoutMode.FullRow)]
            public string Message => message;
            internal string message;
            public string Name { get; private set; } = "Continue?";

            [Submit]
            [Layout(LayoutMode.FullRow | LayoutMode.FloatBottom)]
            public ContinueResponse Response { get; set; }
        }
    }
}