﻿//            Copyright Keysight Technologies 2012-2019
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
using Tap.Shared;

namespace OpenTap.Package
{
    /// <summary>
    /// Calculates the version number of a commit in a git repository
    /// </summary>
    internal class GitVersionCalulator : IDisposable
    {
        private static readonly TraceSource log = Log.CreateSource("GitVersion");
        private const string configFileName = ".gitversion";
        private readonly LibGit2Sharp.Repository repo;
        private readonly string RepoDir;

        private class Config
        {
            public SemanticVersion Version { get => _version; set => _version = value; }
            private SemanticVersion _version = new SemanticVersion(0, 0, 1, null, null);

            /// <summary> version before it got parsed to a SemanticVersion. Possibly not valid.</summary>
            public string RawVersion { get; set; }
            
            /// <summary>
            /// Regex that runs against the FriendlyName of a branch to determine if it is a beta branch 
            /// (commits from this branch will get a "beta" prerelease identifier)
            /// </summary>
            public List<Regex> BetaBranchRegexes { get; private set; } = new List<Regex>
            {
                new Regex("^integration$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex("^develop$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex("^dev$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex("^master$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex("^main$", RegexOptions.Compiled | RegexOptions.IgnoreCase)
            };
            public List<string> BetaBranchPatterns { get; private set; } = new List<string>
            {
                "^integration$",
                "^master$",
                "^develop$",
                "^dev$",
                "^main$"
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
            public int MaxBranchChars => _maxBranchChars;

            public string ConfigFilePath;

            private Config()
            {

            }

            private static Regex configLineRegex = new Regex(@"^(?!#)(?<key>.*?)\s*=\s*(?<value>.*)", RegexOptions.Compiled);
            public static Config ParseConfig(Stream str, string configFilePath = configFileName)
            {
                Config cfg = new Config();
                if (str == null)
                    return cfg;
                cfg.ConfigFilePath = configFilePath;
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
                                    cfg.RawVersion = val;
                                    SemanticVersion.TryParse(val, out cfg._version);
                                    break;
                            }
                        }
                    }
                }
                return cfg;
            }
        }
        
        private const string GIT_HASH = "b7bad55";
        
        void ensureLibgit2Present()
        {
            string libgit2name;

            if (OperatingSystem.Current == OperatingSystem.Windows)
                libgit2name = $"git2-{GIT_HASH}.dll";
            else if (OperatingSystem.Current == OperatingSystem.Linux)
                libgit2name = $"libgit2-{GIT_HASH}.so";
            else if (OperatingSystem.Current == OperatingSystem.MacOS)
                libgit2name = $"libgit2-{GIT_HASH}.dylib";
            else
            {
                log.Error($"Unsupported platform.");
                return;
            }
            
            var requiredFile = Path.Combine(PathUtils.OpenTapDir, libgit2name);
            if (File.Exists(requiredFile))
                return;

            string sourceFile = Path.Combine(PathUtils.OpenTapDir, "Dependencies/LibGit2Sharp.0.27.0.0/", libgit2name);
            if (OperatingSystem.Current == OperatingSystem.Windows)
                sourceFile += $".{(Environment.Is64BitProcess ? CpuArchitecture.x64 : CpuArchitecture.x86)}";
            if (OperatingSystem.Current == OperatingSystem.MacOS)
                sourceFile += $".{MacOsArchitecture.Current.Architecture}";
            if (OperatingSystem.Current == OperatingSystem.Linux)
                sourceFile += $".{LinuxArchitecture.Current.Architecture}";

            try
            {
                File.Copy(sourceFile, requiredFile, true);
            }
            catch (Exception e)
            {
                if (OperatingSystem.Current == OperatingSystem.Windows)
                {
                    var opentapArch = Installation.Current.GetOpenTapPackage()?.Architecture;
                    var processArch = Environment.Is64BitProcess ? CpuArchitecture.x64 : CpuArchitecture.x86;
                    if (opentapArch != processArch)
                        throw new PlatformNotSupportedException($"Unable to find the correct 'libgit2-{GIT_HASH}' because the process architecture '{processArch}' does not match the installed OpenTAP architecture '{opentapArch}'", e);
                }

                throw new PlatformNotSupportedException($"Unable to copy 'libgit2-{GIT_HASH}': {e.Message}.", e);
            }
        }

        /// <summary>
        /// Instanciates a new <see cref="GitVersionCalulator"/> to work on a specified git repository.
        /// </summary>
        /// <param name="repositoryDir">Path pointing to a directory inside the git repository to use.</param>
        public GitVersionCalulator(string repositoryDir)
        {
            repositoryDir = Path.GetFullPath(repositoryDir);
            RepoDir = repositoryDir;
            while (!Directory.Exists(Path.Combine(repositoryDir, ".git")))
            {
                repositoryDir = Path.GetDirectoryName(repositoryDir);
                if (repositoryDir == null)
                    throw new ArgumentException("Directory is not a git repository.", "repositoryDir");
            }
            RepoDir = RepoDir.Substring(repositoryDir.Length);

            ensureLibgit2Present();
            repo = new LibGit2Sharp.Repository(repositoryDir);
        }

        public void Dispose()
        {
            if (repo != null)
                repo.Dispose();
        }

        /// <summary> Keeps iterating until a valid version is read.</summary>
        SemanticVersion getLatestReadableVersion(Commit c)
        {
            while (c != null)
            {
                var cfg = readConfig(c);
                if (cfg.Version != null) return cfg.Version;
                c = getLatestConfigVersionChange(c.Parents.FirstOrDefault());
            }
            // no version was found.
            return null;
        }

        static IEnumerable<TreeEntry> GetAllConfigFilesInTree(Tree t)
        {
            foreach (TreeEntry te in t)
            {
                if (te.Target is Tree subtree)
                {
                    foreach (var match in GetAllConfigFilesInTree(subtree))
                        yield return match;
                }
                else if (te.Name == configFileName)
                {
                    yield return te;
                }
            }
        }

        Config readConfig(Commit c)
        {
            var cfgFiles = GetAllConfigFilesInTree(c?.Tree).ToList();

            string repositoryDir = RepoDir.TrimStart('/','\\');
            TreeEntry cfg = null;
            while (cfg == null)
            {
                var dir = Path.Combine(repositoryDir ?? "", configFileName).Replace('\\','/');
                cfg = cfgFiles.FirstOrDefault(c => c.Path == dir);
                if (String.IsNullOrEmpty(repositoryDir))
                    break;
                repositoryDir = Path.GetDirectoryName(repositoryDir);
            }
            Blob configBlob = cfg?.Target as Blob;
            return Config.ParseConfig(configBlob?.GetContentStream(), cfg?.Path);
        }

        Config ParseConfig(Commit c)
        {
            var cfg = readConfig(c);
            if (cfg.Version == null)
            {
                log.Error("Unable to parse version specification {0}. It is not a valid semantic version.", cfg.RawVersion);
                var ver = getLatestReadableVersion(c.Parents.FirstOrDefault());
                if (ver != null)
                {
                    log.Warning("Using previous {0} as version instead.", ver);
                    cfg.Version = ver;
                }
            }
             
            return cfg;
        }

        private Commit getLatestConfigVersionChange(Commit c)
        {
            if (c.Parents.Any() == false)
                return c; // 'c' is the first commit in the repo. There was never any change.
            
            // find all changes in the file (for some reason that sometimes returns an empty list)
            //var fileLog = repo.Commits.QueryBy(configFileName, new CommitFilter() { IncludeReachableFrom = c, SortBy = CommitSortStrategies.Topological, FirstParentOnly = false });
            //... go on to iterate through filelog...

            // Instead, just walk all commits comparing the version in the .gitversion file to the one in the previous commit
            Config currentCfg = readConfig(c);
            while (true)
            {
                Commit parent = c.Parents.FirstOrDefault(); // first parent only, we are only interested in when the file changes on the beta branch
                if (parent == null)
                {
                    // we got to the very first commit in this repo without seeing any changes in the gitversion
                    // this might be because there is no .gitversion file, or just because the content of the file is the same as the default values.
                    // in both cases, we should treat this commit (the initial commit) as the LatestConfigVersionChange
                    return c;
                }
                Config parentCfg = readConfig(parent);                
                if (currentCfg.Version != null && (parentCfg.Version == null || currentCfg.Version.CompareTo(parentCfg.Version) > 0))
                {
                    // the version number was bumped
                    return c;
                }
                c = parent;
                currentCfg = parentCfg;
            }
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
            Commit commit = repo.Lookup<Commit>(sha);
            if (commit == null)
                throw new ArgumentException($"The commit with reference {sha} does not exist in the repository.");
            return GetVersion(commit);
        }

        /// <summary>
        /// Calculates the version number of a specific commit in the git repository
        /// </summary>
        public SemanticVersion GetVersion(Commit targetCommit)
        {
            if (repo.Lookup<Commit>(targetCommit.Sha) == null)
                throw new ArgumentException($"The commit with hash {targetCommit} does not exist the in repository.");
            if(!GetAllConfigFilesInTree(targetCommit.Tree).Any())
            {
                log.Warning("Did not find any .gitversion file.");
            }
            Config cfg = ParseConfig(targetCommit);
            if (cfg.ConfigFilePath != configFileName)
                log.Debug("Using configuration from {0}", cfg.ConfigFilePath);

            Branch defaultBranch = getBetaBranch(cfg);

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
                // The version calculation is slightly different for RC versions
                // For an RC, we want to count merge commits as a single commit
                // For other branches, we want to count the literal number of commits
                // Historically, we have counted merge commits as single commits for all branches,
                // but this causes issues in scenarios where merge commits are fast-forwarded onto e.g. the main branch.
                // See here: https://github.com/opentap/opentap/pull/1384
                // And here: https://github.com/opentap/opentap/issues/1321#issuecomment-1895749385
                bool isRc = preRelease.StartsWith("rc", StringComparison.InvariantCultureIgnoreCase);
                Commit cfgCommit = getLatestConfigVersionChange(targetCommit);
                Commit commonAncestor = findFirstCommonAncestor(defaultBranch, targetCommit);
                int commitsFromDefaultBranch = countCommitsBetween(commonAncestor, targetCommit, firstParentOnly: isRc);
                log.Debug("Found {0} commits since branchout from beta branch in commit {1}.", commitsFromDefaultBranch, commonAncestor.Sha.Substring(0, 8));
                int commitsSinceVersionUpdate = countCommitsBetween(cfgCommit, targetCommit, firstParentOnly: isRc) + 1;
                log.Debug("Found {0} commits since last version bump in commit {1}.", commitsSinceVersionUpdate, cfgCommit.Sha.Substring(0, 8));
                int alphaVersion = Math.Min(commitsFromDefaultBranch, commitsSinceVersionUpdate);
                if (isRc == false)
                {
                    int betaVersion = countCommitsBetween(cfgCommit, commonAncestor, false) + 1;
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

            if (cfg.Version == null) return new SemanticVersion(0, 0, 0, preRelease, metadata);
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
        /// </summary>
        private Commit findFirstCommonAncestor(Branch b1, Commit target)
        {
            // This fixes gitversion calculation in scenarios where the local revision of
            // a checked out branch is behind the origin branch. If the local revision is fully merged
            // in the remote tracking branch, we base our calculation on the remote branch instead.
            // Otherwise, if the local branch contains commits that are *not* merged in the remote, we base the calculation on that.
            // This should make the gitversion calculation work as expected after `git fetch --all`.
            if (b1.TrackedBranch != null)
            {
                // Check if any local commits are unreachable from the tracking branch
                var commitsMissingFromUpstream = (IQueryableCommitLog)repo.Commits.QueryBy(new CommitFilter() { IncludeReachableFrom = b1.Tip, ExcludeReachableFrom = b1.TrackedBranch.Tip});
                if (commitsMissingFromUpstream.Any())
                {
                    throw new Exception(
                        $"The local branch '{b1.GetShortName()}' contains commits missing from the tracked upstream branch.\n" +
                        $"This can cause unexpected mismatching version numbers. Please align '{b1.GetShortName()}' with its upstream.");
                }
                b1 = b1.TrackedBranch;
            }

            Commit b1Commit = b1.Tip;
            while (b1Commit != null)
            {
                if (b1Commit.Sha == target.Sha)
                    return target; // target is a directly on the b1 branch
                b1Commit = b1Commit.Parents.FirstOrDefault();
            }
            log.Debug($"Common ancestor of {b1.Tip} and {target} is not on the same branch.");
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
                var releaseCommits = (IQueryableCommitLog)repo.Commits.QueryBy(new CommitFilter() { SortBy = CommitSortStrategies.Topological, IncludeReachableFrom = b1Commit });
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

        private Branch getBetaBranch(Config cfg)
        {
            // Try to find the HEAD of the 
            foreach (var remote in repo.Network.Remotes)
            {
                string expectedDefaultRefName = $"refs/remotes/{remote.Name}/HEAD";
                var defaultRef = repo.Refs.FirstOrDefault(r => r.CanonicalName == expectedDefaultRefName) as SymbolicReference;
                if (defaultRef != null)
                {
                    // be careful to return the remote branch instead of any local one. On build runners the local branch might be behind, as they usually just checkout a sha not the actual branch
                    var branch = repo.Branches.FirstOrDefault(b => b.CanonicalName == defaultRef.TargetIdentifier);
                    if (branch != null)
                    {
                        log.Debug("Determined beta branch to be '{0}' by looking at the HEAD of the remote '{1}'.",
                            branch.GetShortName(), remote.Name);
                        return branch;
                    }
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
            log.Debug("Determined beta branch to be '{0}' using regular expression match.", defaultBranch.GetShortName());
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
            {
                // is this also the tip of a release branch, then pick that instead
                var releaseBranch = repo.Branches.Where(r => cfg.ReleaseBranchRegex.IsMatch(r.GetShortName()) && r.Tip.Sha == commit.Sha).FirstOrDefault();
                if (releaseBranch != null)
                {
                    return releaseBranch.GetShortName();
                }
                return defaultBranch.GetShortName();
            }

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
                    if (!commitOnBranch.Parents.Any())
                        break;
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

            if(candidates.Select(b => b.GetShortName()).Distinct().Count() == 1) // all candicates have the same name (e.g. on is a local branch and one is the remote of that same branch)
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
