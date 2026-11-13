using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Dependencies are job handles; names are only labels for logs.
internal sealed class BuildGraph
{
    private readonly List<BuildJob> jobs = new();
    private readonly Action<string> log;
    private bool started;

    public BuildGraph(Action<string> log) => this.log = log;

    // An actionless node completes when all its dependencies succeed.
    public BuildJob Node(string name) => Job(name, () => Task.CompletedTask);

    public BuildJob Job(string name, Func<Task> action)
    {
        if (started)
            throw new InvalidOperationException("Cannot add jobs after the graph has started.");
        ArgumentNullException.ThrowIfNull(action);
        if (string.IsNullOrWhiteSpace(name) || jobs.Any(job => job.Name == name))
            throw new ArgumentException("Job names must be non-empty and unique.", nameof(name));
        var job = new BuildJob(this, name, action);
        jobs.Add(job);
        return job;
    }

    public Task RunAsync()
    {
        if (started)
            throw new InvalidOperationException("The graph can only run once.");
        Validate();
        started = true;
        foreach (var job in jobs)
            job.Freeze();
        var tasks = new Dictionary<BuildJob, Task>();

        Task Schedule(BuildJob job)
        {
            if (tasks.TryGetValue(job, out var task))
                return task;
            var prerequisites = job.Dependencies.Select(Schedule).ToArray();
            task = Task.Run(() => ExecuteAsync(job, prerequisites));
            tasks.Add(job, task);
            return task;
        }

        return Task.WhenAll(jobs.Select(Schedule));
    }

    private async Task ExecuteAsync(BuildJob job, Task[] prerequisites)
    {
        try
        {
            await Task.WhenAll(prerequisites).ConfigureAwait(false);
        }
        catch
        {
            log($"[SKIPPED] {job.Name}: prerequisite failed or was canceled");
            throw;
        }

        log($"[START] {job.Name}");
        try
        {
            await job.Action().ConfigureAwait(false);
            log($"[DONE] {job.Name}");
        }
        catch (OperationCanceledException)
        {
            log($"[CANCELED] {job.Name}");
            throw;
        }
        catch
        {
            log($"[FAILED] {job.Name}");
            throw;
        }
    }

    public void Validate()
    {
        var missing = jobs.SelectMany(job => job.Secrets
            .Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
            .Select(name => $"  {job.Name}: {name}"))
            .ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException("Missing required environment variables:\n" + string.Join("\n", missing));
        var visiting = new HashSet<BuildJob>();
        var visited = new HashSet<BuildJob>();
        void Visit(BuildJob job)
        {
            if (visited.Contains(job))
                return;
            if (!visiting.Add(job))
                throw new InvalidOperationException($"Dependency cycle at {job.Name}.");
            foreach (var dependency in job.Dependencies)
                Visit(dependency);
            visiting.Remove(job);
            visited.Add(job);
        }
        foreach (var job in jobs)
            Visit(job);
    }
}

internal sealed class BuildJob
{
    public string Name { get; }
    internal Func<Task> Action { get; }
    private readonly List<BuildJob> dependencies = new();
    private readonly BuildGraph graph;
    internal IReadOnlyList<BuildJob> Dependencies => dependencies;
    private readonly HashSet<string> secrets = new(StringComparer.Ordinal);
    private bool frozen;
    internal IEnumerable<string> Secrets => secrets;

    public BuildJob DependsOn(params BuildJob[] jobs)
    {
        if (frozen)
            throw new InvalidOperationException("Cannot change dependencies after the graph has started.");
        ArgumentNullException.ThrowIfNull(jobs);
        if (jobs.Any(job => job == null || job.graph != graph))
            throw new ArgumentException("Dependencies must belong to this graph.", nameof(jobs));
        foreach (var job in jobs)
            if (!dependencies.Contains(job))
                dependencies.Add(job);
        return this;
    }

    public BuildJob RequiresSecrets(params string[] names)
    {
        if (frozen)
            throw new InvalidOperationException("Cannot change requirements after the graph has started.");
        if (names.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Secret names must be non-empty.", nameof(names));
        foreach (var name in names)
            secrets.Add(name);
        return this;
    }

    internal void Freeze() => frozen = true;

    internal BuildJob(BuildGraph graph, string name, Func<Task> action)
    {
        Name = name;
        Action = action;
        this.graph = graph;
    }
}
