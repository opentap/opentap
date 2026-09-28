//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Text;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using System.Linq;
using System.Collections.Generic;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace OpenTap.GitVersioning
{
    /// <summary>Locates and configures the host-native libgit2 used by the git version calculator.</summary>
    internal static class LibGit2NativeLibrary
    {
        private const string LibGit2Name = "libgit2-b7bad55";
        private const string DependencyDirectory = "Dependencies/LibGit2Sharp.0.27.0.0";
        private static readonly object loadLock = new object();
        private static string loadedPath;

        /// <summary>
        /// Configures LibGit2Sharp from an OpenTAP installation, an OpenTAP NuGet package, or the
        /// LibGit2Sharp.NativeBinaries layout used in project build output.
        /// </summary>
        internal static string Load(string rootDirectory)
        {
            lock (loadLock)
            {
                if (loadedPath != null)
                    return loadedPath;

                var candidates = GetCandidatePaths(rootDirectory, out var nativeFileName);
                string sourcePath = null;
                foreach (var candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        sourcePath = candidate;
                        break;
                    }
                }

                if (sourcePath == null)
                    throw new DllNotFoundException($"Could not find the native libgit2 library. Searched: {string.Join(", ", candidates)}");

                // OpenTAP runtime payloads suffix native binaries with their architecture. Copy the
                // selected binary next to the consuming assembly under the conventional name expected
                // by LibGit2Sharp. This location is covered by normal P/Invoke probing on both desktop
                // MSBuild and .NET Core MSBuild.
                var stagedPath = Path.Combine(rootDirectory, nativeFileName);
                if (!string.Equals(sourcePath, stagedPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (!File.Exists(stagedPath))
                        File.Copy(sourcePath, stagedPath);
                    sourcePath = stagedPath;
                }

                try
                {
                    LibGit2Sharp.GlobalSettings.NativeLibraryPath = rootDirectory;
                }
                catch (LibGit2SharpException ex) when (ex.Message.IndexOf("after it has been loaded", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Another LibGit2Sharp consumer initialized the process first. Its already-loaded
                    // native library is usable, and changing the search path is neither possible nor needed.
                }

                loadedPath = sourcePath;
                return loadedPath;
            }
        }

        private static List<string> GetCandidatePaths(string rootDirectory, out string nativeFileName)
        {
            var architecture = GetArchitecture();
            string packageRuntime;
            string packageFileName;
            string nativeRuntime;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                packageRuntime = nativeRuntime = "win-" + architecture;
                packageFileName = $"git2-b7bad55.dll.{architecture}";
                nativeFileName = "git2-b7bad55.dll";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                packageRuntime = "macos-" + architecture;
                packageFileName = $"{LibGit2Name}.dylib.{architecture}";
                nativeRuntime = "osx-" + architecture;
                nativeFileName = $"{LibGit2Name}.dylib";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var musl = IsMusl();
                packageRuntime = "linux-" + architecture;
                packageFileName = musl
                    ? $"{LibGit2Name}.so.musl.{architecture}"
                    : $"{LibGit2Name}.so.{architecture}";
                nativeRuntime = (musl ? "linux-musl-" : "linux-") + architecture;
                nativeFileName = $"{LibGit2Name}.so";
            }
            else
            {
                throw new PlatformNotSupportedException("Git version calculation is not supported on this operating system.");
            }

            return new List<string>
            {
                Path.Combine(rootDirectory, DependencyDirectory, packageFileName),
                Path.Combine(rootDirectory, "runtimes", packageRuntime, DependencyDirectory, packageFileName),
                Path.Combine(rootDirectory, "runtimes", nativeRuntime, "native", nativeFileName),
                Path.Combine(rootDirectory, nativeFileName)
            };
        }

        private static string GetArchitecture()
        {
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X86: return "x86";
                case Architecture.X64: return "x64";
                case Architecture.Arm: return "arm";
                case Architecture.Arm64: return "arm64";
                default: throw new PlatformNotSupportedException($"Git version calculation is not supported for {RuntimeInformation.ProcessArchitecture} processes.");
            }
        }

        private static class GlibC
        {
            [DllImport("libc")]
            internal static extern IntPtr gnu_get_libc_version();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsMusl()
        {
            try { return GlibC.gnu_get_libc_version() == IntPtr.Zero; }
            catch { return true; }
        }
    }

    internal sealed class GitVersionResult : IComparable
    {
        public readonly int Major;
        public readonly int Minor;
        public readonly int Patch;
        public readonly string PreRelease;
        public readonly string BuildMetadata;

        public GitVersionResult(int major, int minor, int patch, string preRelease, string buildMetadata)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            PreRelease = preRelease;
            BuildMetadata = buildMetadata;
        }

        public static bool TryParse(string value, out GitVersionResult version)
        {
            var match = Regex.Match(value ?? "", @"^(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?(?:-(?<pre>[a-zA-Z0-9-.]+))?(?:\+(?<meta>[a-zA-Z0-9-.]+))?$");
            if (!match.Success)
            {
                version = null;
                return false;
            }

            version = new GitVersionResult(
                int.Parse(match.Groups["major"].Value),
                int.Parse(match.Groups["minor"].Value),
                match.Groups["patch"].Success ? int.Parse(match.Groups["patch"].Value) : 0,
                match.Groups["pre"].Success ? match.Groups["pre"].Value : null,
                match.Groups["meta"].Success ? match.Groups["meta"].Value : null);
            return true;
        }

        public int CompareTo(object obj)
        {
            var other = (GitVersionResult)obj;
            var result = Major.CompareTo(other.Major);
            if (result == 0) result = Minor.CompareTo(other.Minor);
            if (result == 0) result = Patch.CompareTo(other.Patch);
            return result;
        }

        public override string ToString()
        {
            var value = $"{Major}.{Minor}.{Patch}";
            if (!string.IsNullOrWhiteSpace(PreRelease)) value += "-" + PreRelease;
            if (!string.IsNullOrWhiteSpace(BuildMetadata)) value += "+" + BuildMetadata;
            return value;
        }
    }

    /// <summary>
    /// Calculates the version number of a commit in a git repository
    /// </summary>
    internal sealed class GitVersionCalculatorCore : IDisposable
    {
        private readonly GitVersionLog log;

        internal sealed class GitVersionLog
        {
            public Action<string> Debug { get; }
            public Action<string> Warning { get; }
            public Action<string> Error { get; }

            public GitVersionLog(Action<string> debug, Action<string> warning, Action<string> error)
            {
                Debug = debug ?? (_ => { });
                Warning = warning ?? (_ => { });
                Error = error ?? (_ => { });
            }
        }
        private const string configFileName = ".gitversion";
        private readonly Lazy<LibGit2Sharp.Repository> repository;
        private readonly string RepoDir;
        private LibGit2Sharp.Repository repo => repository.Value;

        private class Config
        {
            public GitVersionResult Version { get => _version; set => _version = value; }
            private GitVersionResult _version = new GitVersionResult(0, 0, 1, null, null);

            /// <summary> version before it got parsed to a GitVersionResult. Possibly not valid.</summary>
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
                                    GitVersionResult.TryParse(val, out cfg._version);
                                    break;
                            }
                        }
                    }
                }
                return cfg;
            }
        }
        
        /// <summary>
        /// Instanciates a new <see cref="GitVersionCalculatorCore"/> to work on a specified git repository.
        /// </summary>
        /// <param name="repositoryDir">Path pointing to a directory inside the git repository to use.</param>
        /// <param name="nativeLibraryRoot">Directory containing the OpenTAP or LibGit2Sharp native assets.</param>
        /// <param name="log">Receives diagnostic messages from the calculation.</param>
        public GitVersionCalculatorCore(string repositoryDir, string nativeLibraryRoot, GitVersionLog log)
        {
            this.log = log ?? new GitVersionLog(null, null, null);
            repositoryDir = Path.GetFullPath(repositoryDir);
            RepoDir = repositoryDir;
            /* if this is a normal git repository, .git is a directory containing the local index. if this is a git worktree,
             * .git is a text file containing the path to the real index. libgit is able to handle both cases perfectly. */
            while (!Directory.Exists(Path.Combine(repositoryDir, ".git")) && !File.Exists(Path.Combine(repositoryDir, ".git")))
            {
                repositoryDir = Path.GetDirectoryName(repositoryDir);
                if (repositoryDir == null)
                    throw new ArgumentException("Directory is not a git repository.", "repositoryDir");
            }
            RepoDir = RepoDir.Substring(repositoryDir.Length);

            var repositoryRoot = repositoryDir;
            repository = new Lazy<LibGit2Sharp.Repository>(() =>
            {
                LibGit2NativeLibrary.Load(nativeLibraryRoot);
                return new LibGit2Sharp.Repository(repositoryRoot);
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private static string format(string message, object[] args) => args.Length == 0 ? message : string.Format(message, args);
        private void debug(string message, params object[] args) => log.Debug(format(message, args));
        private void warning(string message, params object[] args) => log.Warning(format(message, args));
        private void error(string message, params object[] args) => log.Error(format(message, args));

        public void Dispose()
        {
            if (repository.IsValueCreated)
                repository.Value.Dispose();
        }

        /// <summary> Keeps iterating until a valid version is read.</summary>
        GitVersionResult getLatestReadableVersion(Commit c)
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
                error("Unable to parse version specification {0}. It is not a valid semantic version.", cfg.RawVersion);
                var ver = getLatestReadableVersion(c.Parents.FirstOrDefault());
                if (ver != null)
                {
                    warning("Using previous {0} as version instead.", ver);
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
        public GitVersionResult GetVersion()
        {
            if (!repo.Commits.Any())
                return new GitVersionResult(0, 0, 0, null, null);
            return GetVersion(repo.Head.Tip);
        }

        /// <summary>
        /// Calculates the version number of a specific commit in the git repository
        /// </summary>
        public GitVersionResult GetVersion(string sha)
        {
            Commit commit = repo.Lookup<Commit>(sha);
            if (commit == null)
                throw new ArgumentException($"The commit with reference {sha} does not exist in the repository.");
            return GetVersion(commit);
        }

        /// <summary>
        /// Calculates the version number of a specific commit in the git repository
        /// </summary>
        public GitVersionResult GetVersion(Commit targetCommit)
        {
            if (repo.Lookup<Commit>(targetCommit.Sha) == null)
                throw new ArgumentException($"The commit with hash {targetCommit} does not exist the in repository.");
            if(!GetAllConfigFilesInTree(targetCommit.Tree).Any())
            {
                warning("Did not find any .gitversion file.");
            }
            Config cfg = ParseConfig(targetCommit);
            if (cfg.ConfigFilePath != configFileName)
                debug("Using configuration from {0}", cfg.ConfigFilePath);

            Branch defaultBranch = getBetaBranch(cfg);

            string branchName = guessBranchName(cfg,targetCommit,defaultBranch);

            string preRelease = "alpha";
            if (branchName == GetShortName(defaultBranch))
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
                bool isRc = preRelease.StartsWith("rc", StringComparison.OrdinalIgnoreCase);
                Commit cfgCommit = getLatestConfigVersionChange(targetCommit);
                Commit commonAncestor = findFirstCommonAncestor(defaultBranch, targetCommit);
                int commitsFromDefaultBranch = countCommitsBetween(commonAncestor, targetCommit, firstParentOnly: isRc);
                debug("Found {0} commits since branchout from beta branch in commit {1}.", commitsFromDefaultBranch, commonAncestor.Sha.Substring(0, 8));
                int commitsSinceVersionUpdate = countCommitsBetween(cfgCommit, targetCommit, firstParentOnly: isRc) + 1;
                debug("Found {0} commits since last version bump in commit {1}.", commitsSinceVersionUpdate, cfgCommit.Sha.Substring(0, 8));
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

            if (cfg.Version == null) return new GitVersionResult(0, 0, 0, preRelease, metadata);
            return new GitVersionResult(cfg.Version.Major,cfg.Version.Minor,cfg.Version.Patch,preRelease,metadata);
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
                        $"The local branch '{GetShortName(b1)}' contains commits missing from the tracked upstream branch.\n" +
                        $"This can cause unexpected mismatching version numbers. Please align '{GetShortName(b1)}' with its upstream.");
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
            debug($"Common ancestor of {b1.Tip} and {target} is not on the same branch.");
            HashSet<Commit> targetHistory = new HashSet<Commit>(repo.Commits.QueryBy(new CommitFilter() { IncludeReachableFrom = target }));
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
            return Enumerable.Count(repo.Commits.QueryBy(filter));
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
                        debug("Determined beta branch to be '{0}' by looking at the HEAD of the remote '{1}'.",
                            GetShortName(branch), remote.Name);
                        return branch;
                    }
                }
            }

            // For each regex from the config, try to find a branch that matches.
            Branch defaultBranch = cfg.BetaBranchRegexes.Select(rx => repo.Branches.FirstOrDefault(b => rx.IsMatch(GetShortName(b)))).FirstOrDefault(b => b != null);

            if (defaultBranch == null)
            {
                StringBuilder errorMessage = new StringBuilder("Unable to determine the default branch. No branch matching ");
                errorMessage.Append(String.Join(", ", cfg.BetaBranchPatterns.Take(Math.Max(0, cfg.BetaBranchPatterns.Count - 1)).Select(p => $"'{p}'")));
                if (cfg.BetaBranchPatterns.Count > 1)
                    errorMessage.Append($" or ");
                errorMessage.Append($"'{cfg.BetaBranchPatterns.Last()}' could be found. Searched {Enumerable.Count(repo.Branches)} branches.");
                error(errorMessage.ToString());
                debug("Branches:");
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
                        debug(line.ToString());
                        c = 0;
                        line.Clear();
                    }
                }
                if (line.Length > 0)
                    debug(line.ToString());
                throw new NotSupportedException(errorMessage.ToString());
            }
            debug("Determined beta branch to be '{0}' using regular expression match.", GetShortName(defaultBranch));
            return defaultBranch;
        }
        
        /// <summary>
        /// Try to find the name of the branch a commit was originally created on. Logs warnings if not sure.
        /// </summary>
        private string guessBranchName(Config cfg,Commit commit, Branch defaultBranch)
        {
            // is this the tip of the current branch
            if (repo.Head.Tip.Sha == commit.Sha && repo.Info.IsHeadDetached == false)
                return GetShortName(repo.Head);

            // is the commit directly on the default branch?
            var commitsOnDefault = repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = defaultBranch, FirstParentOnly = true });
            var shasOnDefault = new HashSet<string>(commitsOnDefault.Select(c => c.Sha));
            if (shasOnDefault.Contains(commit.Sha))
            {
                // is this also the tip of a release branch, then pick that instead
                var releaseBranch = repo.Branches.Where(r => cfg.ReleaseBranchRegex.IsMatch(GetShortName(r)) && r.Tip.Sha == commit.Sha).FirstOrDefault();
                if (releaseBranch != null)
                {
                    return GetShortName(releaseBranch);
                }
                return GetShortName(defaultBranch);
            }

            // is the commit the tip of any branches
            var tipMatches = repo.Branches.Where(r => r.Tip.Sha == commit.Sha).Where(r => !r.FriendlyName.EndsWith("HEAD"));
            if (tipMatches.Any())
            {
                if (Enumerable.Count(tipMatches) == 1)
                    return GetShortName(tipMatches.First());
                var releaseBranch = tipMatches.FirstOrDefault(b => cfg.ReleaseBranchRegex.IsMatch(b.FriendlyName));
                if (releaseBranch != null)
                    return GetShortName(releaseBranch);
                if (tipMatches.Contains(defaultBranch))
                    return GetShortName(defaultBranch);
                warning("This commit is the tip of several branches, picking one. ({0})",String.Join(", ", tipMatches.Select(b => b.FriendlyName)));
                return GetShortName(tipMatches.First());
            }

            // is the commit on the default branch indirectly through a merge commit
            foreach (Commit onDefault in commitsOnDefault)
            {
                if (Enumerable.Count(onDefault.Parents) > 1)
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
                                warning("Unable to determine old branch name. The branch has probably been deleted.");
                                return "DELETED";
                            }
                        }
                        commitOnBranch = commitOnBranch.Parents.First();
                    }
                }
            }

            // is the commit on a branch that has not yet been merged to the default branch?
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
            if (!candidates.Any())
            {
                warning("Unable to determine branch name.");
                return "ERROR";
            }
            if (candidates.Count == 1)
                return GetShortName(candidates.First());

            if(Enumerable.Count(candidates.Select(b => GetShortName(b)).Distinct()) == 1) // all candicates have the same name (e.g. on is a local branch and one is the remote of that same branch)
                return GetShortName(candidates.First());

            warning("Several possible branch names found. Picking one.");

            // pick the first candidate that is has a merge commit as the child of commit
            try
            {
                for (int b = 0; b < candidates.Count; b++)
                {
                    var commitsOnB = repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = candidates[b], ExcludeReachableFrom = commit, FirstParentOnly = true });
                    if (Enumerable.Count(commitsOnB.Last().Parents) == 2 && commitsOnB.Last().Parents.First() == commit)
                        return GetShortName(candidates[b]);
                }
            }
            catch
            {

            }

            // TODO: we need some better logic to pick the right candidate here
            //var selected = candidates.Select(b => (b, branchCountFromDefault(b, shasOnDefault))).ToList(); 
            return GetShortName(candidates.First());
        }

        private static string GetShortName(Branch branch)
        {
            if (branch.FriendlyName.StartsWith("origin/"))
                return branch.FriendlyName.Substring(7);
            return branch.FriendlyName;
        }
    }
}
