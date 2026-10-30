#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace GitVersionCalculator;

/// <summary>Read-only subset of Git's SHA-1 object database. No executable or native dependencies.</summary>
public sealed class GitRepository
{
    public record GitObject(string Type, byte[] Data);
    public record Commit(string Id, string Tree, string[] Parents, long Time, string Message);
    public record Branch(string Ref, string Id)
    {
        public string FriendlyName => Ref.StartsWith("refs/heads/") ? Ref.Substring(11) : Ref.Substring(13);
        public string ShortName => FriendlyName.StartsWith("origin/") ? FriendlyName.Substring(7) : FriendlyName;
    }
    private record PackIndex(string Pack, byte[] Bytes, int Count);
    private readonly List<string> objectDirs = new();
    private readonly List<PackIndex> packs = new();
    private readonly Dictionary<string, GitObject> objects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GitObject> packedObjects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> packData = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Commit> commits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, (string Mode, string Id)>> trees = new();
    private readonly Dictionary<string, string> config = new(StringComparer.OrdinalIgnoreCase);
    public string Root { get; }
    public string GitDir { get; }
    public string CommonDir { get; }
    public string RelativeDirectory { get; }
    public Dictionary<string, string> Refs { get; } = new(StringComparer.Ordinal);
    public List<Branch> Branches { get; }
    public string? HeadRef { get; }
    public string? HeadId => ResolveRef("HEAD");
    public IEnumerable<string> Remotes => config.Keys.Where(k => k.StartsWith("remote.", StringComparison.OrdinalIgnoreCase))
        .Select(k => k.Substring(7, k.LastIndexOf('.') - 7)).Distinct(StringComparer.Ordinal);

    public GitRepository(string directory)
    {
        var requested = Path.GetFullPath(directory);
        var root = requested;
        while (!Directory.Exists(Path.Combine(root, ".git")) && !File.Exists(Path.Combine(root, ".git")))
        {
            if (File.Exists(Path.Combine(root, "HEAD")) && Directory.Exists(Path.Combine(root, "objects"))) break;
            root = Path.GetDirectoryName(root) ?? throw new ArgumentException("Directory is not in a Git repository.");
        }
        Root = root;
        RelativeDirectory = RelativePath(root, requested).Replace('\\', '/');
        if (RelativeDirectory == ".") RelativeDirectory = "";
        var dotgit = Path.Combine(root, ".git");
        GitDir = Directory.Exists(dotgit) ? dotgit : File.Exists(dotgit)
            ? FullPath(File.ReadAllText(dotgit).Trim().Substring("gitdir:".Length).Trim(), root) : root;
        var commonFile = Path.Combine(GitDir, "commondir");
        CommonDir = File.Exists(commonFile) ? FullPath(File.ReadAllText(commonFile).Trim(), GitDir) : GitDir;
        ReadConfig(Path.Combine(CommonDir, "config"));
        if (GetConfig("extensions.objectformat") is { } format && format != "sha1")
            throw new NotSupportedException("Only SHA-1 Git repositories are supported.");
        if (GetConfig("extensions.refstorage") is { } storage && storage != "files")
            throw new NotSupportedException("Reftable reference storage is not supported.");
        if (File.Exists(Path.Combine(CommonDir, "shallow")))
            throw new NotSupportedException("Shallow history cannot produce reliable OpenTAP version counts. Use a full clone.");
        if (Directory.Exists(Path.Combine(CommonDir, "refs", "replace")) || File.Exists(Path.Combine(CommonDir, "info", "grafts")))
            throw new NotSupportedException("Replace refs and grafted history are not supported.");
        ReadRefs(CommonDir);
        if (GitDir != CommonDir) ReadRefs(GitDir);
        if (Refs.Keys.Any(k => k.StartsWith("refs/replace/"))) throw new NotSupportedException("Replace refs are not supported.");
        var head = File.ReadAllText(Path.Combine(GitDir, "HEAD")).Trim();
        Refs["HEAD"] = head;
        HeadRef = head.StartsWith("ref: ") ? head.Substring(5) : null;
        Branches = Refs.Keys.Where(k => k.StartsWith("refs/heads/") || k.StartsWith("refs/remotes/"))
            .OrderBy(value => value, StringComparer.Ordinal).Select(k => new Branch(k, ResolveRef(k)!)).Where(b => b.Id != null).ToList();
        AddObjectDirectory(Path.Combine(CommonDir, "objects"));
    }
    public string? GetConfig(string key) => config.TryGetValue(key, out var value) ? value : null;

    private static string FullPath(string path, string directory) => Path.GetFullPath(Path.Combine(directory, path));
    private static string RelativePath(string directory, string path)
    {
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), comparison)) return ".";
        // Both arguments are absolute paths. A trailing separator makes the base URI a directory.
        var root = new Uri(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
        return Uri.UnescapeDataString(root.MakeRelativeUri(new Uri(path)).ToString());
    }
    private void ReadConfig(string path)
    {
        if (!File.Exists(path)) return;
        string section = "";
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or ';') continue;
            var m = Regex.Match(line, "^\\[([^\\] \\t]+)(?:\\s+\"(.*)\")?\\]$");
            if (m.Success) { section = m.Groups[1].Value + (m.Groups[2].Success ? "." + m.Groups[2].Value : ""); continue; }
            int equal = line.IndexOf('=');
            if (equal >= 0) config[section + "." + line.Substring(0, equal).Trim()] = line.Substring(equal + 1).Trim().Trim('"');
        }
    }
    private void ReadRefs(string dir)
    {
        var packed = Path.Combine(dir, "packed-refs");
        if (File.Exists(packed)) foreach (var line in File.ReadLines(packed))
        {
            if (line.Length == 0 || line[0] is '#' or '^') continue;
            var parts = line.Split(new[] { ' ' }, 2);
            if (parts.Length == 2) Refs[parts[1]] = parts[0];
        }
        var refs = Path.Combine(dir, "refs");
        if (Directory.Exists(refs)) foreach (var file in Directory.EnumerateFiles(refs, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".lock")) continue;
            Refs[RelativePath(dir, file).Replace('\\', '/')] = File.ReadAllText(file).Trim();
        }
    }
    public string? ResolveRef(string name)
    {
        var visited = new HashSet<string>();
        while (Refs.TryGetValue(name, out var value))
        {
            if (!visited.Add(name)) throw new InvalidDataException("Cyclic symbolic reference.");
            if (!value.StartsWith("ref: ")) return value;
            name = value.Substring(5);
        }
        return null;
    }
    public string ResolveCommit(string revision)
    {
        var id = ResolveRef(revision) ?? ResolveRef("refs/heads/" + revision) ?? ResolveRef("refs/tags/" + revision)
            ?? ResolveRef("refs/remotes/" + revision);
        if (id == null && Regex.IsMatch(revision, "^[a-fA-F0-9]{4,40}$"))
        {
            var prefix = revision.ToLowerInvariant();
            if (prefix.Length == 40) id = prefix;
            else
            {
                var matches = new HashSet<string>();
                foreach (var dir in objectDirs)
                {
                    var subdir = Path.Combine(dir, prefix.Substring(0, 2));
                    if (Directory.Exists(subdir)) foreach (var file in Directory.EnumerateFiles(subdir))
                    {
                        var candidate = prefix.Substring(0, 2) + Path.GetFileName(file);
                        if (candidate.Length == 40 && candidate.StartsWith(prefix)) matches.Add(candidate);
                    }
                }
                foreach (var pack in packs) for (int i = 0; i < pack.Count; i++)
                {
                    var candidate = ToHex(pack.Bytes, 1032 + i * 20, 20);
                    if (candidate.StartsWith(prefix)) matches.Add(candidate);
                }
                if (matches.Count != 1) throw new ArgumentException("Commit prefix is missing or ambiguous.");
                id = matches.Single();
            }
        }
        if (id == null) throw new ArgumentException($"Unknown revision: {revision}. Use a branch, tag or commit hash.");
        return PeelCommit(id);
    }
    public string PeelCommit(string id)
    {
        var seen = new HashSet<string>();
        while (seen.Add(id))
        {
            var obj = ReadObject(id);
            if (obj.Type == "commit") return id;
            if (obj.Type != "tag") throw new ArgumentException("Revision does not refer to a commit.");
            id = Encoding.UTF8.GetString(obj.Data).Split('\n').First(l => l.StartsWith("object ")).Substring(7);
        }
        throw new InvalidDataException("Cyclic tag.");
    }
    public Branch? Upstream(Branch branch)
    {
        if (!branch.Ref.StartsWith("refs/heads/")) return null;
        var remote = GetConfig("branch." + branch.FriendlyName + ".remote");
        var merge = GetConfig("branch." + branch.FriendlyName + ".merge");
        if (remote == null || merge == null) return null;
        var reference = remote == "." ? merge : "refs/remotes/" + remote + "/" + merge.Substring("refs/heads/".Length);
        return Branches.FirstOrDefault(b => b.Ref == reference);
    }
    private void AddObjectDirectory(string directory)
    {
        directory = Path.GetFullPath(directory);
        if (objectDirs.Contains(directory)) return;
        objectDirs.Add(directory);
        var packDir = Path.Combine(directory, "pack");
        if (Directory.Exists(packDir)) foreach (var file in Directory.EnumerateFiles(packDir, "*.idx").OrderBy(value => value, StringComparer.Ordinal))
        {
            var bytes = File.ReadAllBytes(file);
            if (bytes.Length < 1032 || U32(bytes, 0) != 0xff744f63 || U32(bytes, 4) != 2)
                throw new NotSupportedException("Only version-2 Git pack indexes are supported.");
            int count = checked((int)U32(bytes, 1028));
            if (bytes.Length < 1032L + count * 28L + 40) throw new InvalidDataException("Truncated pack index.");
            packs.Add(new PackIndex(Path.ChangeExtension(file, ".pack"), bytes, count));
        }
        var alternates = Path.Combine(directory, "info", "alternates");
        if (File.Exists(alternates)) foreach (var line in File.ReadLines(alternates).Where(l => l.Length > 0))
        {
            if (line.StartsWith("\"", StringComparison.Ordinal)) throw new NotSupportedException("Quoted alternate object paths are not supported.");
            AddObjectDirectory(FullPath(line, directory));
        }
    }
    public GitObject ReadObject(string id)
    {
        if (!Regex.IsMatch(id, "^[0-9a-f]{40}$")) throw new InvalidDataException("Invalid SHA-1 object id.");
        if (objects.TryGetValue(id, out var cached)) return cached;
        foreach (var dir in objectDirs)
        {
            var file = Path.Combine(dir, id.Substring(0, 2), id.Substring(2));
            if (!File.Exists(file)) continue;
            using var input = File.OpenRead(file);
            var data = Inflate(input);
            int zero = Array.IndexOf(data, (byte)0);
            if (zero < 0) throw new InvalidDataException("Invalid loose object header.");
            var header = Encoding.ASCII.GetString(data, 0, zero).Split(' ');
            var body = new byte[data.Length - zero - 1];
            Buffer.BlockCopy(data, zero + 1, body, 0, body.Length);
            if (header.Length != 2 || long.Parse(header[1]) != body.Length) throw new InvalidDataException("Invalid loose object size.");
            return objects[id] = new GitObject(header[0], body);
        }
        var wanted = Enumerable.Range(0, 20).Select(i => Convert.ToByte(id.Substring(i * 2, 2), 16)).ToArray();
        foreach (var pack in packs)
        {
            int low = wanted[0] == 0 ? 0 : checked((int)U32(pack.Bytes, 8 + (wanted[0] - 1) * 4));
            int high = checked((int)U32(pack.Bytes, 8 + wanted[0] * 4)) - 1;
            while (low <= high)
            {
                int mid = low + (high - low) / 2;
                int compare = 0;
                for (int i = 0; i < wanted.Length && compare == 0; i++)
                    compare = pack.Bytes[1032 + mid * 20 + i].CompareTo(wanted[i]);
                if (compare < 0) low = mid + 1;
                else if (compare > 0) high = mid - 1;
                else
                {
                    uint offset = U32(pack.Bytes, 1032 + pack.Count * 24 + mid * 4);
                    long position = (offset & 0x80000000) == 0 ? offset : checked((long)U64(pack.Bytes, checked(1032 + pack.Count * 28 + (int)(offset & 0x7fffffff) * 8)));
                    return objects[id] = ReadPacked(pack.Pack, position, 0);
                }
            }
        }
        throw new FileNotFoundException($"Git object {id} is missing. Partial clones are not fetched automatically.");
    }
    private GitObject ReadPacked(string path, long offset, int depth)
    {
        if (depth > 256) throw new InvalidDataException("Excessive pack delta depth.");
        var key = path + ":" + offset;
        if (packedObjects.TryGetValue(key, out var cached)) return cached;
        if (!packData.TryGetValue(path, out var bytes)) packData[path] = bytes = File.ReadAllBytes(path);
        using var input = new MemoryStream(bytes, writable: false);
        input.Position = offset;
        int b = Next(input), type = (b >> 4) & 7, shift = 4;
        long size = b & 15;
        while ((b & 128) != 0)
        {
            b = Next(input);
            if (shift > 56) throw new InvalidDataException("Invalid pack object size.");
            size |= (long)(b & 127) << shift;
            shift += 7;
        }
        long baseOffset = 0;
        string? baseId = null;
        if (type == 6)
        {
            b = Next(input); long distance = b & 127;
            while ((b & 128) != 0) { b = Next(input); distance = checked(((distance + 1) << 7) + (b & 127)); }
            baseOffset = offset - distance;
            if (baseOffset < 12 || baseOffset >= offset) throw new InvalidDataException("Invalid delta base offset.");
        }
        else if (type == 7)
        {
            var hash = new byte[20];
            for (int i = 0; i < hash.Length; i++) hash[i] = (byte)Next(input);
            baseId = ToHex(hash, 0, hash.Length);
        }
        var data = Inflate(input);
        if (data.LongLength != size) throw new InvalidDataException("Invalid packed object size.");
        GitObject result;
        if (type is 6 or 7)
        {
            var basis = type == 6 ? ReadPacked(path, baseOffset, depth + 1) : ReadObject(baseId!);
            result = new GitObject(basis.Type, ApplyDelta(basis.Data, data));
        }
        else result = new GitObject(type switch { 1 => "commit", 2 => "tree", 3 => "blob", 4 => "tag", _ => throw new InvalidDataException("Invalid pack object type.") }, data);
        return packedObjects[key] = result;
    }
    private static byte[] ApplyDelta(byte[] basis, byte[] delta)
    {
        int p = 0;
        long Varint()
        {
            long n = 0; int shift = 0, b;
            do { if (p >= delta.Length || shift > 56) throw new InvalidDataException("Invalid delta integer."); b = delta[p++]; n |= (long)(b & 127) << shift; shift += 7; } while ((b & 128) != 0);
            return n;
        }
        if (Varint() != basis.LongLength) throw new InvalidDataException("Delta base size mismatch.");
        var result = new byte[checked((int)Varint())]; int output = 0;
        while (p < delta.Length)
        {
            int op = delta[p++];
            if ((op & 128) != 0)
            {
                int offset = 0, size = 0;
                for (int i = 0; i < 4; i++) if ((op & (1 << i)) != 0) offset |= delta[p++] << (8 * i);
                for (int i = 0; i < 3; i++) if ((op & (1 << (i + 4))) != 0) size |= delta[p++] << (8 * i);
                if (size == 0) size = 65536;
                if (offset < 0 || size > basis.Length - offset || size > result.Length - output) throw new InvalidDataException("Invalid delta copy.");
                Buffer.BlockCopy(basis, offset, result, output, size); output += size;
            }
            else
            {
                if (op == 0 || op > delta.Length - p || op > result.Length - output) throw new InvalidDataException("Invalid delta insert.");
                Buffer.BlockCopy(delta, p, result, output, op); p += op; output += op;
            }
        }
        if (output != result.Length) throw new InvalidDataException("Delta result size mismatch.");
        return result;
    }
    private static byte[] Inflate(Stream input)
    {
        #if NETSTANDARD2_0
        // Git uses zlib framing; .NET Framework's DeflateStream expects raw DEFLATE.
        int cmf = Next(input), flg = Next(input);
        if ((cmf & 15) != 8 || (cmf >> 4) > 7 || ((cmf << 8) + flg) % 31 != 0 || (flg & 32) != 0)
            throw new InvalidDataException("Invalid or unsupported zlib header.");
        using var z = new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true);
#else
        using var z = new ZLibStream(input, CompressionMode.Decompress, leaveOpen: true);
#endif
        using var output = new MemoryStream(); z.CopyTo(output); return output.ToArray();
    }
    private static int Next(Stream input) { int b = input.ReadByte(); return b < 0 ? throw new EndOfStreamException() : b; }
    private static uint U32(byte[] data, int offset) =>
        ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
    private static ulong U64(byte[] data, int offset) => ((ulong)U32(data, offset) << 32) | U32(data, offset + 4);
    private static string ToHex(byte[] data, int offset, int count)
    {
        const string digits = "0123456789abcdef";
        var chars = new char[count * 2];
        for (int i = 0; i < count; i++)
        {
            chars[i * 2] = digits[data[offset + i] >> 4];
            chars[i * 2 + 1] = digits[data[offset + i] & 15];
        }
        return new string(chars);
    }
    public Commit ReadCommit(string id)
    {
        if (commits.TryGetValue(id, out var cached)) return cached;
        var obj = ReadObject(id);
        if (obj.Type != "commit") throw new InvalidDataException("Expected a commit object.");
        var text = Encoding.UTF8.GetString(obj.Data); var split = text.IndexOf("\n\n", StringComparison.Ordinal);
        if (split < 0) throw new InvalidDataException("Invalid commit.");
        var lines = text.Substring(0, split).Split('\n');
        var committer = lines.First(l => l.StartsWith("committer ")).Split(' ');
        return commits[id] = new Commit(id, lines.First(l => l.StartsWith("tree ")).Substring(5),
            lines.Where(l => l.StartsWith("parent ")).Select(l => l.Substring(7)).ToArray(), long.Parse(committer[committer.Length - 2]), text.Substring(split + 2).Split('\n')[0]);
    }
    private Dictionary<string, (string Mode, string Id)> ReadTree(string id)
    {
        if (trees.TryGetValue(id, out var cached)) return cached;
        var obj = ReadObject(id); if (obj.Type != "tree") throw new InvalidDataException("Expected tree.");
        var entries = new Dictionary<string, (string, string)>(StringComparer.Ordinal); int p = 0;
        while (p < obj.Data.Length)
        {
            int space = Array.IndexOf(obj.Data, (byte)' ', p), zero = Array.IndexOf(obj.Data, (byte)0, p);
            if (space < p || zero < space || zero + 21 > obj.Data.Length) throw new InvalidDataException("Invalid tree entry.");
            var mode = Encoding.ASCII.GetString(obj.Data, p, space - p);
            var name = Encoding.UTF8.GetString(obj.Data, space + 1, zero - space - 1);
            entries[name] = (mode, ToHex(obj.Data, zero + 1, 20)); p = zero + 21;
        }
        return trees[id] = entries;
    }
    public string? ReadFile(string commit, string path)
    {
        var tree = ReadCommit(commit).Tree;
        var parts = path.Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            if (!ReadTree(tree).TryGetValue(parts[i], out var entry)) return null;
            if (i == parts.Length - 1)
            {
                var obj = ReadObject(entry.Id);
                return obj.Type == "blob" ? Encoding.UTF8.GetString(obj.Data) : null;
            }
            if (entry.Mode != "40000" && entry.Mode != "040000") return null;
            tree = entry.Id;
        }
        return null;
    }
    public HashSet<string> Reachable(string? start, bool firstParent = false)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var todo = new Stack<string>(); if (start != null) todo.Push(start);
        while (todo.Count > 0)
        {
            var id = todo.Pop();
            if (!seen.Add(id)) continue;
            var parents = ReadCommit(id).Parents;
            foreach (var parent in firstParent ? parents.Take(1) : parents) todo.Push(parent);
        }
        return seen;
    }
    public int CountBetween(string? exclude, string? include, bool firstParent = false)
    {
        var hidden = Reachable(exclude);
        return Reachable(include, firstParent).Count(id => !hidden.Contains(id));
    }
    public IEnumerable<string> Walk(string start, bool firstParent = false, bool topological = false)
    {
        if (firstParent)
        {
            string? id = start;
            while (id != null) { yield return id; id = ReadCommit(id).Parents.FirstOrDefault(); }
            yield break;
        }
        // libgit2 defaults to commit-time order. Topological walks additionally keep children before parents.
        var all = Reachable(start);
        var children = all.ToDictionary(id => id, _ => 0);
        if (topological) foreach (var id in all) foreach (var parent in ReadCommit(id).Parents) children[parent]++;
        var queue = new SortedSet<(long Time, string Id)>();
        if (topological) { foreach (var id in all.Where(id => children[id] == 0)) queue.Add((-ReadCommit(id).Time, id)); }
        else queue.Add((-ReadCommit(start).Time, start));
        var emitted = new HashSet<string>();
        while (queue.Count > 0)
        {
            var next = queue.Min;
            queue.Remove(next);
            var id = next.Id;
            if (!emitted.Add(id)) continue;
            yield return id;
            foreach (var parent in ReadCommit(id).Parents)
                if (!topological || --children[parent] == 0) queue.Add((-ReadCommit(parent).Time, parent));
        }
    }
}
