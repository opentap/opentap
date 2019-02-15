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

namespace OpenTap.Package
{
    /// <summary>
    /// Calculates the version number of a commit in a git repository
    /// </summary>
    internal class GitVersionCalulator : IDisposable
    {
        private readonly Repository repo;

        private Branch defaultBranch;
        public string DefaultBranchName
        {
            get { return defaultBranch.FriendlyName; }
            set { defaultBranch = repo.Branches.FirstOrDefault(b => b.FriendlyName == value); }
        }

        /// <summary>
        /// Regex that runs against the FriendlyName of a branch to determine if it is a beta branch 
        /// (commits from this branch will get a "beta" prerelease identifier)
        /// </summary>
        public Regex BetaBranchRegex { get; set; }

        /// <summary>
        /// Regex that runs against the FriendlyName of a branch to determine if it is a release candidate branch 
        /// (commits from this branch will get a "rc" prerelease identifier)
        /// </summary>
        public Regex ReleaseCandidateBranchRegex { get; set; } = new Regex("^rc.*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        /// <summary>
        /// Regex that runs against the FriendlyName of a branch to determine if it is a release branch 
        /// (commits from this branch will not get any prerelease identifier)
        /// </summary>
        public Regex ReleaseBranchRegex { get; set; } = new Regex("^release.*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Cap the length of the branch name to this many chars. This can be useful e.g. if the version number is used in a file name, which could otherwise become too long.
        /// </summary>
        /// <param name="repositoryDir"></param>
        public int MaxBranchChars { get; set; } = 30;

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
            defaultBranch = guessDefaultBranch();
            BetaBranchRegex = new Regex(defaultBranch.FriendlyName, RegexOptions.Compiled);
        }

        /// <summary>
        /// Calculates the version number of the current HEAD of the git repository
        /// </summary>
        public SemanticVersion GetVersion()
        {
            return GetVersion(repo.Head.Tip);
        }

        /// <summary>
        /// Calculates the version number of a specific commit in the git repository
        /// </summary>
        public SemanticVersion GetVersion(string sha)
        {
            return GetVersion(repo.Commits.FirstOrDefault(c => c.Sha.StartsWith(sha)));
        }

        /// <summary>
        /// Calculates the version number of a specific commit in the git repository
        /// </summary>
        public SemanticVersion GetVersion(Commit c)
        {
            Commit commonAncestor = repo.ObjectDatabase.FindMergeBase(defaultBranch.Tip, c);
            TagVersion tagVer = getLatestTagVersion(c);

            string preRelease = getPrerelease(guessBranchName(c));

            string metadata = c.Sha.Substring(0, 8);
            if (preRelease == "alpha")
            {
                string branchName = guessBranchName(c);
                if (branchName == "(no branch)")
                    branchName = "NA"; // '(' and ' ' are not allowed in semver
                else
                    branchName = Regex.Replace(branchName, "[^a-zA-Z0-9-]", "-"); // replace any chars that is not valid semver with '-'
                if (branchName.Length > MaxBranchChars)
                    branchName = branchName.Remove(MaxBranchChars);
                metadata += "." + branchName;
            }
#if MORE_SEMVER
            tagVer.Patch += countCommitsBetween(tagVer.Tag, commonAncestor);
            int branchCommitCount = countCommitsBetween(commonAncestor, c, true);
            if (branchCommitCount > 1)
            {
                preRelease += "." + branchCommitCount;
            }
#else
            tagVer.Patch += countCommitsBetween(tagVer.Tag, c);

#endif
            return new SemanticVersion(tagVer.Major,tagVer.Minor,tagVer.Patch,preRelease,metadata);
        }

        private class TagVersion : IComparable<TagVersion>
        {
            public int Major { get; private set; }
            public int Minor { get; private set; }
            public int Patch { get; set; }

            public Tag Tag { get; private set; }

            private TagVersion()
            {

            }

            public static TagVersion FromTag(Tag tag)
            {
                var tv = new TagVersion();
                tv.Tag = tag;
                string name = tag.FriendlyName;
                var match = Regex.Match(name, @"^(v|V)(?<major>\d{1,8})\.(?<minor>\d{1,8})(\.(?<patch>\d{1,8}))?$", RegexOptions.Compiled);
                if (!match.Success)
                {
                    return null;
                }
                tv.Major = int.Parse(match.Groups["major"].Value);
                tv.Minor = int.Parse(match.Groups["minor"].Value);

                if (match.Groups["patch"].Success)
                    tv.Patch = int.Parse(match.Groups["patch"].Value);
                else
                    tv.Patch = 0;
                return tv;
            }

            public int CompareTo(TagVersion other)
            {
                if (this.Major == other.Major)
                {
                    if (this.Minor == other.Minor)
                    {
                        return this.Patch.CompareTo(other.Patch);
                    }
                    return this.Minor.CompareTo(other.Minor);
                }
                return this.Major.CompareTo(other.Major);
            }

            public override string ToString()
            {
                return $"{Major}.{Minor}.{Patch}";
            }
        }

        private TagVersion getLatestTagVersion(Commit c)
        {
            Regex tagPattern = new Regex(@"v|V\d+\.\d+", RegexOptions.Compiled);
            IEnumerable<TagVersion> versionTags = repo.Tags.Where(t => t.IsAnnotated).Select(t => TagVersion.FromTag(t)).Where(tv => tv != null);

            return versionTags.OrderByDescending(t => t).FirstOrDefault();
            
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

        private Branch guessDefaultBranch()
        {
            var defaultRef = repo.Refs.FirstOrDefault(r => r.CanonicalName == "refs/remotes/origin/HEAD") as SymbolicReference;
            if (defaultRef != null)
            {
                var defaultName = defaultRef.TargetIdentifier.Split('/').Last();
                var branch = repo.Branches.FirstOrDefault(b => b.FriendlyName == defaultName);
                if (branch != null)
                    return branch;
            }
            Branch defaultBranch;
            defaultBranch = repo.Branches.FirstOrDefault(b => b.FriendlyName == "origin/integration");
            if (defaultBranch != null) return defaultBranch;
            defaultBranch = repo.Branches.FirstOrDefault(b => b.FriendlyName == "integration");
            if (defaultBranch != null) return defaultBranch;
            defaultBranch = repo.Branches.FirstOrDefault(b => b.FriendlyName == "origin/develop");
            if (defaultBranch != null) return defaultBranch;
            defaultBranch = repo.Branches.FirstOrDefault(b => b.FriendlyName == "develop");
            if (defaultBranch != null) return defaultBranch;
            defaultBranch = repo.Branches.FirstOrDefault(b => b.FriendlyName == "origin/dev");
            if (defaultBranch != null) return defaultBranch;
            defaultBranch = repo.Branches.FirstOrDefault(b => b.FriendlyName == "dev");
            if (defaultBranch != null) return defaultBranch;
            defaultBranch = repo.Branches.FirstOrDefault(b => b.FriendlyName == "origin/master");
            if (defaultBranch != null) return defaultBranch;
            defaultBranch = repo.Branches.FirstOrDefault(b => b.FriendlyName == "master");
            if (defaultBranch == null)
            {
                var log = Log.CreateSource("GitVersion");
                string error = $"Unalbe to find a default branch. No branch named 'integration', 'develop', 'dev' or 'master' could be found. Searched {repo.Branches.Count()} branches.";
                log.Error(error);
                log.Debug("Branches:");
                int c = 0;
                StringBuilder line = new StringBuilder();
                foreach (Branch item in repo.Branches.OrderBy(b => b.FriendlyName))
                {
                    string bName = item.FriendlyName;
                    if (bName.Length > 25)
                        bName = bName.Substring(0, 25 - 3) + "...";
                    line.AppendFormat("{0,-26}",bName);
                    c++;
                    if (c == 4)
                    {
                        log.Debug(line.ToString());
                        c = 0;
                        line.Clear();
                    }
                }
                if(line.Length > 0)
                    log.Debug(line.ToString());
                throw new NotSupportedException(error);
            }
            return defaultBranch;
        }

        private string guessBranchName(Commit commit)
        {
            var tipMatches = repo.Branches.Where(r => r.Tip.Sha == commit.Sha);
            if (tipMatches.Any())
            {
                if (repo.Head.Tip.Sha == commit.Sha && repo.Info.IsHeadDetached == false)
                    return repo.Head.GetShortName();
                return pickBestBranch(tipMatches).GetShortName();
            }

            // is the commit directly on the default branch?
            var commitsOnDefault = repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = defaultBranch, FirstParentOnly = true });
            var shasOnDefault = commitsOnDefault.Select(c => c.Sha).ToHashSet();
            if (shasOnDefault.Contains(commit.Sha))
                return defaultBranch.GetShortName();

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
                                return m.Groups[m.Groups.Count-1].Value;
                            else
                                return "(deleted)";
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
                return "error";

            // TODO: we need some better logic to pick the right candidate here
            //var selected = candidates.Select(b => (b, branchCountFromDefault(b, shasOnDefault))).ToList(); 
            return candidates.First().GetShortName();

        }

        /// <summary>
        /// Finds the most stable (release > rc > beta > alpha) branch in a list.
        /// </summary>
        Branch pickBestBranch(IEnumerable<Branch> branches)
        {
            if (!branches.Any())
                return null;
            if (branches.Count() == 1)
                return branches.First();
            var releaseBranch = branches.FirstOrDefault(b => ReleaseBranchRegex.IsMatch(b.FriendlyName));
            if (releaseBranch != null)
                return releaseBranch;
            var rcBranch = branches.FirstOrDefault(b => ReleaseCandidateBranchRegex.IsMatch(b.FriendlyName));
            if (rcBranch != null)
                return rcBranch;
            var betaBranch = branches.FirstOrDefault(b => BetaBranchRegex.IsMatch(b.FriendlyName));
            if (betaBranch != null)
                return betaBranch;
            // TODO: warn here
            return branches.First();
        }

        private int branchCountFromDefault(Branch b, HashSet<string> shasOnDefault)
        {
            if (b.FriendlyName == defaultBranch.FriendlyName)
                return 0;
            int count = 0;
            Commit commitOnBranch = b.Tip;
            int branchesContainingCurrentCommit = 1;
            while (!shasOnDefault.Contains(commitOnBranch.Sha))
            {
                int branchesContainingCommit = countBranchesContainingCommit(commitOnBranch, shasOnDefault);
                if (branchesContainingCommit > branchesContainingCurrentCommit)
                {
                    branchesContainingCurrentCommit = branchesContainingCommit;
                    count++;
                }

                commitOnBranch = commitOnBranch.Parents.First();
            }
            return count;
        }

        private int countBranchesContainingCommit(Commit commit, HashSet<string> shasOnDefault)
        {
            int count = 0;
            foreach (var branch in repo.Branches)
            {
                Commit commitOnBranch = branch.Tip;
                while (!shasOnDefault.Contains(commitOnBranch.Sha))
                {
                    if (commitOnBranch.Sha == commit.Sha)
                    {
                        count++;
                        break;
                    }
                    commitOnBranch = commitOnBranch.Parents.First();
                }
            }
            return count;
        }

        private string getPrerelease(string branchName)
        {
            if (branchName.StartsWith("origin/"))
                branchName = branchName.Substring(7);
            if (BetaBranchRegex.IsMatch(branchName))
                return "beta";
            if (ReleaseCandidateBranchRegex.IsMatch(branchName))
                return "rc";
            if (ReleaseBranchRegex.IsMatch(branchName))
                return null;
            return "alpha";
        }

        public void Dispose()
        {
            if(repo != null)
                repo.Dispose();
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
