//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using System.Linq;
using System.Collections.Generic;
using System;
using System.IO;
using System.Globalization;

namespace OpenTap.Package
{
    /// <summary>
    /// Calculates the version number of a commit in a git repository
    /// </summary>
    internal class GitVersionCalulator : IDisposable
    {
        private static readonly TraceSource log = Log.CreateSource("GitVersion");
        private const string configFileName = ".gitversion";
        private readonly Repository repo;

        private class Config
        {
            public SemanticVersion Version => _version;
            private SemanticVersion _version = new SemanticVersion(0,0,1,null,null);
            /// <summary>
            /// Regex that runs against the FriendlyName of a branch to determine if it is a beta branch 
            /// (commits from this branch will get a "beta" prerelease identifier)
            /// </summary>
            public List<Regex> BetaBranchRegexes { get; private set; } = new List<Regex>
            {
                new Regex("^integration$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex("^develop$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex("^dev$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex("^master$", RegexOptions.Compiled | RegexOptions.IgnoreCase)
            };
            public List<string> BetaBranchPatterns { get; private set; } = new List<string>
            {
                "^integration$",
                "^master$", 
                "^develop$",
                "^dev$"
            };

            /// <summary>
            /// Regex that runs against the FriendlyName of a branch to determine if it is a release branch 
            /// (commits from this branch will not get any prerelease identifier)
            /// </summary>
            public Regex ReleaseBranchRegex { get; private set; } = new Regex("^release[0-9x]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            public Regex ReleaseTagRegex { get; private set; } = new Regex(@"v\d+\.\d+\.\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            private int _maxBranchChars = 30;
            /// <summary>
            /// Cap the length of the branch name to this many chars. This can be useful e.g. if the version number is used in a file name, which could otherwise become too long.
            /// </summary>
            /// <param name="repositoryDir"></param>
            public int MaxBranchChars => _maxBranchChars;

            private Config()
            {

            }

            private static Regex configLineRegex = new Regex(@"^(?!#)(?<key>.*?)\s*=\s*(?<value>.*)", RegexOptions.Compiled);
            public static Config ParseConfig(Stream str)
            {
                Config cfg = new Config();
                if (str == null)
                    return cfg;
                bool isBetaBranchSet = false;
                using (var reader = new StreamReader(str, Encoding.UTF8))
                {
                    String line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var m = configLineRegex.Match(line);
                        if (m.Success)
                        {
                            string val = m.Groups["value"].Value;
                            switch (m.Groups["key"].Value.ToLower())
                            {
                                case "beta branch":
                                    if (!isBetaBranchSet)
                                    {
                                        cfg.BetaBranchRegexes = new List<Regex>();
                                        cfg.BetaBranchPatterns = new List<string>();
                                    }
                                    isBetaBranchSet = true;
                                    cfg.BetaBranchRegexes.Add(new Regex(val, RegexOptions.IgnoreCase));
                                    cfg.BetaBranchPatterns.Add(val);
                                    break;
                                case "release branch":
                                    cfg.ReleaseBranchRegex = new Regex(val, RegexOptions.IgnoreCase);
                                    break;
                                case "release tag":
                                    cfg.ReleaseTagRegex = new Regex(val, RegexOptions.IgnoreCase);
                                    break;
                                case "max branch chars":
                                    int.TryParse(val, out cfg._maxBranchChars);
                                    break;
                                case "version":
                                    SemanticVersion.TryParse(val, out cfg._version);
                                    break;
                            }
                        }
                    }
                }
                return cfg;
            }
        }

        /// <summary>
        /// Instanciates a new <see cref="GitVersionCalulator"/> to work on a specified git repository.
        /// </summary>
        /// <param name="repositoryDir">Path pointing to a directory inside the git repository to use.</param>
        public GitVersionCalulator(string repositoryDir)
        {
            repositoryDir = Path.GetFullPath(repositoryDir);
            while (!Directory.Exists(Path.Combine(repositoryDir, ".git")))
            {
                repositoryDir = Path.GetDirectoryName(repositoryDir);
                if (repositoryDir == null)
                    throw new ArgumentException("Directory is not a git repository.", "repositoryDir");
            }
            repo = new Repository(repositoryDir);
        }

        public void Dispose()
        {
            if (repo != null)
                repo.Dispose();
        }

        private Config ParseConfig(Commit c)
        {
            Blob configBlob = c?.Tree.FirstOrDefault(t => t.Name == configFileName)?.Target as Blob;
            return Config.ParseConfig(configBlob?.GetContentStream());
        }

        private Commit getLatestConfigVersionChange(Commit c)
        {
            // find all changes in the file
            var log = repo.Commits.QueryBy(configFileName, new CommitFilter() { IncludeReachableFrom = c, SortBy = CommitSortStrategies.Topological });

            // walk the changes to find one that changes the version number
            foreach(LogEntry entry in log)
            {
                Config cfg = ParseConfig(entry.Commit);
                Config cfgOld = ParseConfig(entry.Commit.Parents.FirstOrDefault());
                if(cfg.Version.CompareTo(cfgOld.Version) > 0)
                {
                    // the version number was bumped
                    return entry.Commit;
                }
            }
            return log.LastOrDefault()?.Commit;
        }
        
        /// <summary>
        /// Calculates the version number of the current HEAD of the git repository
        /// </summary>
        public SemanticVersion GetVersion()
        {
            if (!repo.Commits.Any())
                return new SemanticVersion(0, 0, 0, null, null);
            return GetVersion(repo.Head.Tip);
        }

        /// <summary>
        /// Calculates the version number of a specific commit in the git repository
        /// </summary>
        public SemanticVersion GetVersion(string sha)
        {
            Commit commit = repo.Commits.FirstOrDefault(c => c.Sha.StartsWith(sha));
            if (commit == null)
                throw new ArgumentException($"Commit with hash {sha} does not exist in repository.");
            return GetVersion(commit);
        }

        /// <summary>
        /// Calculates the version number of a specific commit in the git repository
        /// </summary>
        public SemanticVersion GetVersion(Commit targetCommit)
        {
            targetCommit = repo.Commits.FirstOrDefault(c => c.Sha == targetCommit.Sha);
            if (targetCommit == null)
                throw new ArgumentException("Commit does not exist in repository.");
            Config cfg = ParseConfig(targetCommit);

            Branch defaultBranch = getDefaultBranch(cfg);
            log.Debug("Determined default branch to be '{0}'", defaultBranch.GetShortName());

            string branchName = guessBranchName(cfg,targetCommit,defaultBranch);

            string preRelease = "alpha";
            if (branchName == defaultBranch.GetShortName())
                preRelease = "beta";
            if (cfg.ReleaseBranchRegex.IsMatch(branchName))
                preRelease = "rc";
            Tag releaseTag = getReleaseTag(cfg, targetCommit);
            if (releaseTag != null)
                preRelease = null;

            string metadata = targetCommit.Sha.Substring(0, 8);
            if (preRelease == "alpha")
            {
                if (branchName == "(no branch)")
                    branchName = "NONE"; // '(' and ' ' are not allowed in semver
                else
                    branchName = Regex.Replace(branchName, "[^a-zA-Z0-9-]", "-"); // replace any chars that is not valid semver with '-'
                if (branchName.Length > cfg.MaxBranchChars)
                    branchName = branchName.Remove(cfg.MaxBranchChars);
                metadata += "." + branchName;
            }
            if (!String.IsNullOrEmpty(preRelease))
            {
                Commit cfgCommit = getLatestConfigVersionChange(targetCommit) ?? targetCommit;
                Commit commonAncestor = findFirstCommonAncestor(defaultBranch, targetCommit);
                int commitsFromDefaultBranch = countCommitsBetween(commonAncestor, targetCommit, true);
                int commitsSinceVersionUpdate = countCommitsBetween(cfgCommit, targetCommit, true);
                int alphaVersion = Math.Min(commitsFromDefaultBranch, commitsSinceVersionUpdate);
                if (!preRelease.StartsWith("rc", true, CultureInfo.InvariantCulture))
                {
                    int betaVersion = countCommitsBetween(cfgCommit, commonAncestor, true) + 1;
                    if (betaVersion > 0)
                    {
                        preRelease += "." + betaVersion;
                    }
                }
                if (alphaVersion > 0)
                {
                    preRelease += "." + alphaVersion;
                }
            }
            return new SemanticVersion(cfg.Version.Major,cfg.Version.Minor,cfg.Version.Patch,preRelease,metadata);
        }
        
        private Tag getReleaseTag(Config cfg, Commit c)
        {
            foreach (Tag t in repo.Tags)
            {
                if (t.IsAnnotated &&
                    t.Target.Peel<Commit>() == c &&
                    cfg.ReleaseTagRegex.IsMatch(t.FriendlyName))
                {
                    return t;
                }
            }
            return null;
        }

        /// <summary>
        /// Find the first (youngest) commit that is reachable from two specified places
        /// Slower than <see cref="findCommonAncestor"/>
        /// </summary>
        private Commit findFirstCommonAncestor(Branch b1, Commit target)
        {
            Commit b1Commit = b1.Tip;
            while (b1Commit != null)
            {
                if (b1Commit == target)
                    return target; // target is a directly on the b1 branch
                b1Commit = b1Commit.Parents.FirstOrDefault();
            }
            HashSet<Commit> targetHistory = repo.Commits.QueryBy(new CommitFilter() { IncludeReachableFrom = target }).ToHashSet();
            Commit firstCommon = b1.Commits.FirstOrDefault(c => targetHistory.Contains(c)); // same as repo.ObjectDatabase.FindMergeBase(b1.Tip, target); but faster on average
            // if this branch is being used for several releases (merged to several times, one for each release)
            // we will need to check against older releases as well as target might already exist on the tip of 
            // this release branch (i.e. it could have been merged there "in the future").
            b1Commit = b1.Tip;
            while (firstCommon == target) // this can happen if target is later merged into b1
            {
                if (targetHistory.Contains(b1Commit))
                {
                    // We have reached past the begining of the release branch. There is no point in going further 
                    firstCommon = null;
                    break;
                }
                b1Commit = b1Commit.Parents.FirstOrDefault();
                var releaseCommits = (IQueryableCommitLog)repo.Commits.QueryBy(new CommitFilter() { IncludeReachableFrom = b1Commit });
                firstCommon = releaseCommits.FirstOrDefault(c => targetHistory.Contains(c));
            }
            return firstCommon;
        }

        private int countCommitsBetween(object tag, object now, bool firstParentOnly = false)
        {
            var filter = new CommitFilter()
            {
                SortBy = CommitSortStrategies.Reverse | CommitSortStrategies.Time,
                ExcludeReachableFrom = tag,
                IncludeReachableFrom = now,
                FirstParentOnly = firstParentOnly
            };
            return repo.Commits.QueryBy(filter).Count();
        }

        private Branch getDefaultBranch(Config cfg)
        {
            // Try to find the HEAD of the 
            foreach (var remote in repo.Network.Remotes)
            {
                string expectedDefaultRefName = $"refs/remotes/{remote.Name}/HEAD";
                var defaultRef = repo.Refs.FirstOrDefault(r => r.CanonicalName == expectedDefaultRefName) as SymbolicReference;
                if (defaultRef != null)
                {
                    var defaultName = defaultRef.TargetIdentifier.Split('/').Last();
                    var branch = repo.Branches.FirstOrDefault(b => b.GetShortName() == defaultName);
                    if (branch != null)
                        return branch;
                }
            }

            // For each regex from the config, try to find a branch that matches.
            Branch defaultBranch = cfg.BetaBranchRegexes.Select(rx => repo.Branches.FirstOrDefault(b => rx.IsMatch(b.GetShortName()))).FirstOrDefault(b => b != null);
            
            if (defaultBranch == null)
            {
                StringBuilder error = new StringBuilder("Unable to determine the default branch. No branch matching ");
                error.Append(String.Join(", ", cfg.BetaBranchPatterns.SkipLastN(1).Select(p => $"'{p}'")));
                if (cfg.BetaBranchPatterns.Count > 1)
                    error.Append($" or ");
                error.Append($"'{cfg.BetaBranchPatterns.Last()}' could be found. Searched {repo.Branches.Count()} branches.");
                log.Error(error.ToString());
                log.Debug("Branches:");
                int c = 0;
                StringBuilder line = new StringBuilder();
                foreach (Branch item in repo.Branches.OrderBy(b => b.FriendlyName))
                {
                    string bName = item.FriendlyName;
                    if (bName.Length > 25)
                        bName = bName.Substring(0, 25 - 3) + "...";
                    line.AppendFormat("{0,-26}", bName);
                    c++;
                    if (c == 4)
                    {
                        log.Debug(line.ToString());
                        c = 0;
                        line.Clear();
                    }
                }
                if (line.Length > 0)
                    log.Debug(line.ToString());
                throw new NotSupportedException(error.ToString());
            }
            return defaultBranch;
        }
        
        /// <summary>
        /// Try to find the name of the branch a commit was originally created on. Logs warnings if not sure.
        /// </summary>
        private string guessBranchName(Config cfg,Commit commit, Branch defaultBranch)
        {
            // is this the tip of the current branch
            if (repo.Head.Tip.Sha == commit.Sha && repo.Info.IsHeadDetached == false)
                return repo.Head.GetShortName();

            // is the commit directly on the default branch?
            var commitsOnDefault = repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = defaultBranch, FirstParentOnly = true });
            var shasOnDefault = commitsOnDefault.Select(c => c.Sha).ToHashSet();
            if (shasOnDefault.Contains(commit.Sha))
                return defaultBranch.GetShortName();

            // is the commit the tip of any branches
            var tipMatches = repo.Branches.Where(r => r.Tip.Sha == commit.Sha).Where(r => !r.FriendlyName.EndsWith("HEAD"));
            if (tipMatches.Any())
            {
                if (tipMatches.Count() == 1)
                    return tipMatches.First().GetShortName();
                var releaseBranch = tipMatches.FirstOrDefault(b => cfg.ReleaseBranchRegex.IsMatch(b.FriendlyName));
                if (releaseBranch != null)
                    return releaseBranch.GetShortName();
                if (tipMatches.Contains(defaultBranch))
                    return defaultBranch.GetShortName();
                log.Warning("This commit is the tip of several branches, picking one. ({0})",String.Join(", ", tipMatches.Select(b => b.FriendlyName)));
                return tipMatches.First().GetShortName();
            }

            // is the commit on the default branch indirectly through a merge commit
            foreach (Commit onDefault in commitsOnDefault)
            {
                if (onDefault.Parents.Count() > 1)
                {
                    Commit commitOnBranch = onDefault.Parents.Last();

                    while (!shasOnDefault.Contains(commitOnBranch.Sha))
                    {
                        if (commitOnBranch.Sha == commit.Sha)
                        {
                            var m = Regex.Match(onDefault.MessageShort, "Merge branch '([^']*)'");
                            if (m.Success)
                            {
                                string branchName = m.Groups[m.Groups.Count - 1].Value;
                                if (branchName.StartsWith("origin/"))
                                    branchName = branchName.Substring(7);
                                return branchName;
                            }
                            else
                            {
                                log.Warning("Unable to determine old branch name. The branch has probably been deleted.");
                                return "DELETED";
                            }
                        }
                        commitOnBranch = commitOnBranch.Parents.First();
                    }
                }
            }

            // is the commit on a branch that has not yet been merged to the default branch?
            Stopwatch timer = Stopwatch.StartNew();
            List<Branch> candidates = new List<Branch>();
            foreach (var branch in repo.Branches)
            {
                Commit commitOnBranch = branch.Tip;
                while (!shasOnDefault.Contains(commitOnBranch.Sha))
                {
                    if (commitOnBranch.Sha == commit.Sha)
                    {
                        candidates.Add(branch);
                        break;
                    }
                    commitOnBranch = commitOnBranch.Parents.First();
                }
            }
            TimeSpan dur = timer.Elapsed;
            if (!candidates.Any())
            {
                log.Warning("Unable to determine branch name.");
                return "ERROR";
            }
            if (candidates.Count == 1)
                return candidates.First().GetShortName();

            log.Warning("Several possible branch names found. Picking one.");

            // pick the first candidate that is has a merge commit as the child of commit
            try
            {
                for (int b = 0; b < candidates.Count; b++)
                {
                    var commitsOnB = repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = candidates[b], ExcludeReachableFrom = commit, FirstParentOnly = true });
                    if (commitsOnB.Last().Parents.Count() == 2 && commitsOnB.Last().Parents.First() == commit)
                        return candidates[b].GetShortName();
                }
            }
            catch
            {

            }

            // TODO: we need some better logic to pick the right candidate here
            //var selected = candidates.Select(b => (b, branchCountFromDefault(b, shasOnDefault))).ToList(); 
            return candidates.First().GetShortName();
        }      
    }

    internal static class libGit2Helpers
    {
        public static string GetShortName(this Branch b)
        {
            if (b.FriendlyName.StartsWith("origin/"))
                return b.FriendlyName.Substring(7);
            return b.FriendlyName;
        }
    }
}
