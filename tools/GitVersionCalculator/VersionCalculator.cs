using System.Text.RegularExpressions;

namespace GitVersionCalculator;

public sealed record CalculatedVersion(int Major, int Minor, int Patch, string? PreRelease, string? BuildMetadata)
{
    public override string ToString() => $"{Major}.{Minor}.{Patch}" +
        (string.IsNullOrEmpty(PreRelease) ? "" : "-" + PreRelease) +
        (string.IsNullOrEmpty(BuildMetadata) ? "" : "+" + BuildMetadata);
}

/// <summary>Standalone implementation of OpenTAP's Git version policy.</summary>
public sealed class VersionCalculator
{
    private readonly GitRepository repo;
    private readonly Dictionary<string, Config> configs = new();
    public Action<string>? Warning { get; set; }
    public VersionCalculator(string directory) => repo = new GitRepository(directory);

    private sealed record BaseVersion(int Major, int Minor, int Patch, string? PreRelease)
    {
        public static BaseVersion? Parse(string text)
        {
            var m = Regex.Match(text, @"^(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?(?:-(?<pre>[a-zA-Z0-9-.]+))?(?:\+[a-zA-Z0-9-.]+)?$");
            if (!m.Success || !int.TryParse(m.Groups["major"].Value, out var major) || !int.TryParse(m.Groups["minor"].Value, out var minor)) return null;
            int patch = 0;
            if (m.Groups["patch"].Success && !int.TryParse(m.Groups["patch"].Value, out patch)) return null;
            return new(major, minor, patch, m.Groups["pre"].Success ? m.Groups["pre"].Value : null);
        }
        public int CompareTo(BaseVersion other)
        {
            int c = Major.CompareTo(other.Major); if (c != 0) return c;
            c = Minor.CompareTo(other.Minor); if (c != 0) return c;
            c = Patch.CompareTo(other.Patch); if (c != 0) return c;
            if (PreRelease == other.PreRelease) return 0;
            if (string.IsNullOrEmpty(PreRelease)) return 1;
            if (string.IsNullOrEmpty(other.PreRelease)) return -1;
            var a = PreRelease.Split('.'); var b = other.PreRelease.Split('.');
            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                c = int.TryParse(a[i], out var ai) && int.TryParse(b[i], out var bi) ? ai.CompareTo(bi) : string.Compare(a[i], b[i]);
                if (c != 0) return c;
            }
            return a.Length.CompareTo(b.Length);
        }
    }
    private sealed class Config
    {
        public BaseVersion? Version = new(0, 0, 1, null);
        public string? RawVersion;
        public List<string> BetaPatterns = new() { "^integration$", "^develop$", "^dev$", "^master$", "^main$" };
        public string ReleaseBranch = "^release[0-9x]*$";
        public string ReleaseTag = @"v\d+\.\d+\.\d+";
        public int MaxBranchChars = 30;
        public string? Path;
    }
    private static bool Match(string pattern, string text) => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
    private Config ReadConfig(string commit)
    {
        if (configs.TryGetValue(commit, out var cached)) return cached;
        var cfg = new Config(); string? content;
        string dir = repo.RelativeDirectory;
        while (true)
        {
            var path = dir.Length == 0 ? ".gitversion" : dir + "/.gitversion";
            content = repo.ReadFile(commit, path);
            if (content != null) { cfg.Path = path; break; }
            if (dir.Length == 0) break;
            int slash = dir.LastIndexOf('/'); dir = slash < 0 ? "" : dir[..slash];
        }
        bool betaSet = false;
        if (content != null) foreach (var raw in content.TrimStart('\uFEFF').Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var m = Regex.Match(line, @"^(?!#)(?<key>.*?)\s*=\s*(?<value>.*)");
            if (!m.Success) continue;
            var value = m.Groups["value"].Value;
            switch (m.Groups["key"].Value.ToLowerInvariant())
            {
                case "version": cfg.RawVersion = value; cfg.Version = BaseVersion.Parse(value); break;
                case "beta branch": if (!betaSet) { cfg.BetaPatterns.Clear(); betaSet = true; } cfg.BetaPatterns.Add(value); break;
                case "release branch": cfg.ReleaseBranch = value; break;
                case "release tag": cfg.ReleaseTag = value; break;
                case "max branch chars": int.TryParse(value, out cfg.MaxBranchChars); break;
            }
        }
        return configs[commit] = cfg;
    }
    private string LatestVersionBump(string commit)
    {
        var cfg = ReadConfig(commit);
        while (true)
        {
            var parent = repo.ReadCommit(commit).Parents.FirstOrDefault();
            if (parent == null) return commit;
            var prev = ReadConfig(parent);
            if (cfg.Version != null && (prev.Version == null || cfg.Version.CompareTo(prev.Version) > 0)) return commit;
            commit = parent; cfg = prev;
        }
    }
    private BaseVersion? UsableVersion(string commit)
    {
        var cfg = ReadConfig(commit);
        if (cfg.Version != null) return cfg.Version;
        Warning?.Invoke($"Invalid version '{cfg.RawVersion}'; looking for an earlier readable version.");
        var parent = repo.ReadCommit(commit).Parents.FirstOrDefault();
        while (parent != null)
        {
            var earlier = ReadConfig(parent).Version;
            if (earlier != null) return earlier;
            var previous = repo.ReadCommit(parent).Parents.FirstOrDefault();
            parent = previous == null ? null : LatestVersionBump(previous);
        }
        return null;
    }
    private GitRepository.Branch BetaBranch(Config cfg)
    {
        foreach (var remote in repo.Remotes)
        {
            var name = $"refs/remotes/{remote}/HEAD";
            if (repo.Refs.TryGetValue(name, out var symbolic) && symbolic.StartsWith("ref: "))
            {
                var branch = repo.Branches.FirstOrDefault(b => b.Ref == symbolic[5..]);
                if (branch != null) return branch;
            }
        }
        foreach (var pattern in cfg.BetaPatterns)
        {
            var branch = repo.Branches.FirstOrDefault(b => Match(pattern, b.ShortName));
            if (branch != null) return branch;
        }
        throw new NotSupportedException("No default branch matches: " + string.Join(", ", cfg.BetaPatterns));
    }
    private string? FirstCommonAncestor(GitRepository.Branch branch, string target)
    {
        if (repo.Upstream(branch) is { } upstream)
        {
            if (repo.CountBetween(upstream.Id, branch.Id) > 0)
                throw new InvalidOperationException($"The local branch '{branch.ShortName}' contains commits missing from the tracked upstream branch. Align it with its upstream.");
            branch = upstream;
        }
        if (repo.Reachable(branch.Id, true).Contains(target)) return target;
        var history = repo.Reachable(target);
        var first = repo.Walk(branch.Id).FirstOrDefault(history.Contains);
        string? tip = branch.Id;
        while (first == target && tip != null)
        {
            if (history.Contains(tip)) return null;
            tip = repo.ReadCommit(tip).Parents.FirstOrDefault();
            first = tip == null ? null : repo.Walk(tip, topological: true).FirstOrDefault(history.Contains);
        }
        return first;
    }
    private string GuessBranch(Config cfg, string target, GitRepository.Branch beta)
    {
        if (repo.HeadId == target && repo.HeadRef != null)
        {
            var head = repo.Branches.FirstOrDefault(b => b.Ref == repo.HeadRef);
            if (head != null) return head.ShortName;
        }
        var onDefault = repo.Walk(beta.Id, firstParent: true).ToList();
        var defaultIds = onDefault.ToHashSet();
        if (defaultIds.Contains(target))
            return repo.Branches.FirstOrDefault(b => Match(cfg.ReleaseBranch, b.ShortName) && b.Id == target)?.ShortName ?? beta.ShortName;
        var tips = repo.Branches.Where(b => b.Id == target && !b.FriendlyName.EndsWith("HEAD")).ToList();
        if (tips.Count > 0)
        {
            if (tips.Count == 1) return tips[0].ShortName;
            var release = tips.FirstOrDefault(b => Match(cfg.ReleaseBranch, b.FriendlyName));
            if (release != null) return release.ShortName;
            if (tips.Contains(beta)) return beta.ShortName;
            Warning?.Invoke("Commit is the tip of several branches; selecting " + tips[0].FriendlyName);
            return tips[0].ShortName;
        }
        foreach (var id in onDefault)
        {
            var merge = repo.ReadCommit(id);
            if (merge.Parents.Length < 2) continue;
            string? walk = merge.Parents[^1];
            while (walk != null && !defaultIds.Contains(walk))
            {
                if (walk == target)
                {
                    var match = Regex.Match(merge.Message, "Merge branch '([^']*)'");
                    if (!match.Success) { Warning?.Invoke("Old branch name cannot be determined."); return "DELETED"; }
                    var name = match.Groups[1].Value; return name.StartsWith("origin/") ? name[7..] : name;
                }
                walk = repo.ReadCommit(walk).Parents.FirstOrDefault();
            }
        }
        var candidates = repo.Branches.Where(b => repo.Walk(b.Id, firstParent: true).TakeWhile(id => !defaultIds.Contains(id)).Contains(target)).ToList();
        if (candidates.Count == 0) { Warning?.Invoke("Unable to determine branch name."); return "ERROR"; }
        if (candidates.Select(b => b.ShortName).Distinct().Count() == 1) return candidates[0].ShortName;
        Warning?.Invoke("Several possible branch names; choosing a candidate.");
        var hidden = repo.Reachable(target);
        foreach (var candidate in candidates)
        {
            var last = repo.Walk(candidate.Id, firstParent: true).Where(id => !hidden.Contains(id)).LastOrDefault();
            if (last == null) continue;
            var parents = repo.ReadCommit(last).Parents;
            if (parents.Length == 2 && parents[0] == target) return candidate.ShortName;
        }
        return candidates[0].ShortName;
    }
    public CalculatedVersion GetVersion(string revision = "HEAD")
    {
        if (revision == "HEAD" && repo.HeadId == null) return new(0, 0, 0, null, null);
        string target = repo.ResolveCommit(revision);
        var cfg = ReadConfig(target);
        if (cfg.Path == null) Warning?.Invoke("No applicable committed .gitversion file; using the default base version 0.0.1.");
        var version = UsableVersion(target);
        var beta = BetaBranch(cfg);
        string branch = GuessBranch(cfg, target, beta);
        string? pre = branch == beta.ShortName ? "beta" : "alpha";
        if (Match(cfg.ReleaseBranch, branch)) pre = "rc";
        foreach (var tag in repo.Refs.Keys.Where(k => k.StartsWith("refs/tags/")))
        {
            var id = repo.ResolveRef(tag)!;
            if (Match(cfg.ReleaseTag, tag[10..]) && repo.ReadObject(id).Type == "tag" && repo.PeelCommit(id) == target) { pre = null; break; }
        }
        string metadata = target[..8];
        if (pre == "alpha")
        {
            branch = branch == "(no branch)" ? "NONE" : Regex.Replace(branch, "[^a-zA-Z0-9-]", "-");
            if (cfg.MaxBranchChars < 0) throw new InvalidDataException("max branch chars must not be negative.");
            if (branch.Length > cfg.MaxBranchChars) branch = branch[..cfg.MaxBranchChars];
            metadata += "." + branch;
        }
        if (pre != null)
        {
            bool rc = pre == "rc";
            var bump = LatestVersionBump(target);
            var common = FirstCommonAncestor(beta, target);
            var fromBranch = repo.CountBetween(common, target, rc);
            var sinceBump = repo.CountBetween(bump, target, rc) + 1;
            var alpha = Math.Min(fromBranch, sinceBump);
            if (!rc)
            {
                var betaNumber = repo.CountBetween(bump, common) + 1;
                if (betaNumber > 0) pre += "." + betaNumber;
            }
            if (alpha > 0) pre += "." + alpha;
        }
        return new(version?.Major ?? 0, version?.Minor ?? 0, version?.Patch ?? 0, pre, metadata);
    }
}
