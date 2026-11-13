using System;
using System.Diagnostics;
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

    public IEnumerable<string> JobNames => jobs.Select(job => job.Name);

    // An actionless node completes when all its dependencies succeed.
    public BuildJob Node(string name) => Job(name, () => Task.CompletedTask);

    public BuildJob Job(string name, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Job(name, () =>
        {
            action();
            return Task.CompletedTask;
        });
    }

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
        var timer = Stopwatch.StartNew();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (job.RetryWindow > TimeSpan.Zero)
                    log($"[ATTEMPT {attempt}] {job.Name}: elapsed {timer.Elapsed.TotalSeconds:F0}s, retry window {job.RetryWindow.TotalMinutes:g} min");
                await job.Action().ConfigureAwait(false);
                if (attempt > 1)
                    log($"[RECOVERED] {job.Name}: succeeded on attempt {attempt} after {timer.Elapsed.TotalSeconds:F0}s");
                log($"[DONE] {job.Name}");
                return;
            }
            catch (OperationCanceledException)
            {
                log($"[CANCELED] {job.Name}");
                throw;
            }
            catch (Exception ex)
            {
                log($"[ATTEMPT FAILED] {job.Name}: {ex.Message}");
                var remaining = job.RetryWindow - timer.Elapsed;
                if (remaining <= job.RetryDelay)
                {
                    log($"[FAILED] {job.Name}: attempt {attempt}, elapsed {timer.Elapsed.TotalSeconds:F0}s; retry window exhausted or disabled ({ex.GetType().Name})");
                    throw;
                }
                log($"[RETRY WARNING] {job.Name}: attempt {attempt} failed ({ex.GetType().Name}); waiting {job.RetryDelay.TotalSeconds:g}s, {remaining.TotalSeconds:F0}s left in retry window");
                await Task.Delay(job.RetryDelay).ConfigureAwait(false);
                if (timer.Elapsed >= job.RetryWindow)
                {
                    log($"[FAILED] {job.Name}: retry window expired while waiting");
                    throw;
                }
            }
        }
    }

    public void Validate()
    {
        var errors = new List<string>();
        var missingSecrets = new HashSet<BuildJob>();
        foreach (var job in jobs)
            foreach (var name in job.Secrets)
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
                {
                    errors.Add($"  {job.Name}: required environment variable {name} is missing.");
                    missingSecrets.Add(job);
                }

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

        bool HasMissingSecrets(BuildJob job) => missingSecrets.Contains(job) ||
            job.Dependencies.Any(HasMissingSecrets);
        foreach (var job in jobs)
        {
            // Do not evaluate predicates whose prerequisite credentials are missing.
            if (HasMissingSecrets(job))
                continue;
            foreach (var requirement in job.Requirements)
            {
                bool satisfied;
                try { satisfied = requirement.Predicate(); }
                catch { satisfied = false; } // Report the declared message, never exception secrets.
                if (!satisfied)
                    errors.Add($"  {job.Name}: {requirement.Message}");
            }
        }
        if (errors.Count > 0)
            throw new InvalidOperationException("Build preflight validation failed:\n" + string.Join("\n", errors));
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
    private readonly List<(Func<bool> Predicate, string Message)> requirements = new();
    internal IEnumerable<(Func<bool> Predicate, string Message)> Requirements => requirements;
    private bool frozen;
    internal IEnumerable<string> Secrets => secrets;

    internal TimeSpan RetryWindow { get; private set; }
    internal TimeSpan RetryDelay { get; private set; }

    // Retry only this action, not its prerequisites. The action must be safe to repeat.
    // The window includes attempt durations and waits; in-flight actions are not interrupted.
    public BuildJob Retry(TimeSpan window, TimeSpan delay)
    {
        if (frozen)
            throw new InvalidOperationException("Cannot change retry policy after the graph has started.");
        if (window < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(window));
        if (delay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay));
        RetryWindow = window;
        RetryDelay = delay;
        return this;
    }

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

    // Predicates run during preflight, before ANY job executes. They must not depend
    // on artifacts or state produced by prerequisite jobs.
    public BuildJob Requires(Func<bool> predicate, string failureMessage)
    {
        if (frozen)
            throw new InvalidOperationException("Cannot change requirements after the graph has started.");
        ArgumentNullException.ThrowIfNull(predicate);
        if (string.IsNullOrWhiteSpace(failureMessage))
            throw new ArgumentException("A failure message is required.", nameof(failureMessage));
        requirements.Add((predicate, failureMessage));
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
