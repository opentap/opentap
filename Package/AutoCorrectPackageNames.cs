using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

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
            // We can't provide any corrections if the non-interactive user input is set
            if (NonInteractiveUserInputInterface.IsSet()) return names;
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
                
                // If there is only one option, provide a yes/no dialog
                if (scores.Length == 1)
                {
                    const string cancelChoice = "No";
                    var options = new List<string>() { "Yes", cancelChoice };
                    var req = new AutoCorrectRequest(
                        $"Package '{name}' not found. Did you mean '{scores[0].Candidate}'?", options)
                    {
                        Choice = options[0]
                    };
                    UserInput.Request(req);
                    if (req.Choice == cancelChoice)
                        throw new AutoCorrectException(notFoundMessage);
                    result[i] = scores[0].Candidate;
                }
                // If there are more options, provide a numbered list
                else if (scores.Length > 1)
                {
                    const string cancelChoice = "Cancel";
                    var options = scores.Select(s => s.Candidate).ToList();
                    options.Add(cancelChoice);

                    var req = new AutoCorrectRequest(
                        $"Package '{name}' not found. Did you mean:",
                        options)
                    {
                        Choice = options[0]
                    };
                    UserInput.Request(req);
                    if (req.Choice == cancelChoice)
                        throw new AutoCorrectException(notFoundMessage);
                    
                    result[i] = req.Choice;
                }
            }

            return result;
        }
    }
    
    // [Display("Correct the provided package name?")]
    class AutoCorrectRequest
    {
        [Layout(LayoutMode.FullRow)]
        [Browsable(true)]
        public string Message { get; }
        public List<string> Corrections { get; }
        
        [Submit]
        [Layout(LayoutMode.FullRow| LayoutMode.FloatBottom)]
        [AvailableValues(nameof(Corrections))]
        public string Choice { get; set; }

        public AutoCorrectRequest(string message, List<string> corrections)
        {
            Message = message;
            Corrections = corrections;
        }
    }
}
