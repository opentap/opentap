using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using OpenTap.Translation;

namespace OpenTap.Package
{
    internal class AutoCorrectException : Exception
    {
        public AutoCorrectException(string message) : base(message)
        {

        }
    }
    internal static class AutoCorrectPackageNames
    {
        /// <summary>
        /// Correct each name in the string array based on the available packages in the repositories.
        /// If a package name does not exist, an exception is thrown.
        /// </summary>
        /// <param name="names"></param>
        /// <param name="repositories"></param>
        /// <returns></returns>
        public static string[] Correct(string[] names, IEnumerable<IPackageRepository> repositories)
        {
            if (names == null || names.Length == 0) return names;
            // Copy the input array to use as return value
            var result = names.ToArray();

            var repos = repositories.ToArray();
            List<string> onlinePackages = null;

            var packageCache = PackageRepositoryHelpers.DetermineRepositoryType(new Uri(PackageCacheHelper.PackageCacheDirectory).AbsoluteUri);
            var knownPackages = Installation.Current.GetPackages().Select(p => p.Name)
                .Concat(packageCache.GetPackageNames()).ToHashSet();


            for (int i = 0; i < names.Length; i++)
            {
                var name = names[i];
                if (File.Exists(name)) continue;

                var notFoundMessage = $"Package '{name}' not found.";

                if (string.IsNullOrWhiteSpace(name) || knownPackages.Contains(name))
                {
                    result[i] = name;
                    continue;
                }
                // Checking the repositories is slow. Postpone it as much as possible.
                // In most cases we can resolve a correctly spelled package from local caches

                if (onlinePackages == null)
                {
                    onlinePackages = repos.SelectMany(r => r.GetPackageNames()).ToList();
                    foreach (var pkg in onlinePackages)
                    {
                        knownPackages.Add(pkg);
                    }

                    if (knownPackages.Contains(name))
                    {
                        result[i] = name;
                        continue;
                    }
                }

                var matchThreshold = 3;
                var matcher = new FuzzyMatcher(name, matchThreshold);
                var matchList = knownPackages.Select(matcher.Score).Where(m => m.Score <= matchThreshold).ToArray();

                // If any match is almost a perfect match, only consider perfect matches
                if (matchList.Any(m => m.Score == 0))
                    matchList = matchList.Where(m => m.Score == 0).ToArray();

                var scores = matchList.OrderBy(m => m.Score).Take(5).ToArray();

                if (scores.Length == 0)
                {
                    // There are no packages 
                    throw new AutoCorrectException(notFoundMessage);
                }

                var options = scores.Select(s => s.Candidate).ToList();

                var req = new AutoCorrectRequest(name, options);
                UserInput.Request(req);
                if (req.Choice == req.NegativeAnswer)
                    throw new AutoCorrectException(notFoundMessage);
                if (req.Choice == req.Yes)
                    result[i] = options[0];
                else result[i] = req.Choice;
            }

            return result;
        }
    }

    [Display("Correct package name?")]
    class AutoCorrectRequest : IStringLocalizer
    {
        public string NegativeAnswer => Options.Length == 1 ? No : Cancel;
        public string Yes => this.Translate("Yes");
        public string No => this.Translate("No");
        public string Cancel => this.Translate("Cancel");


        public string SingleOptionMessage => this.TranslateFormat("Package '{0}' not found. Did you mean '{1}'?", arguments: [Package, Options.FirstOrDefault()]);
        public string MultipleOptionsMessage => this.TranslateFormat("Package '{0}' not found. Did you mean:", arguments: [Package]);

        [Layout(LayoutMode.FullRow)]
        [Browsable(true)]
        public string Message => Options.Length == 1 ? SingleOptionMessage : MultipleOptionsMessage;

        // If there is only one option, provide a yes/no dialog
        // Otherwise, provide a ranked list
        public string[] Choices => Options.Length == 1 ? [Yes, No] : [.. Options, Cancel];
        [Submit]
        [Layout(LayoutMode.FullRow | LayoutMode.FloatBottom)]
        [AvailableValues(nameof(Choices))]
        public string Choice { get; set; }

        private readonly string Package;
        private readonly string[] Options = [];
        public AutoCorrectRequest(string package, IEnumerable<string> options)
        {
            Package = package;
            Options = [.. options];
            Choice = Choices[0];
        }

        public AutoCorrectRequest()
        {
            // default constructor needed so the type can be instantiated without parameters
        }
    }
}
