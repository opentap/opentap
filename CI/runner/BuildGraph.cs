using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Copy this file into a build runner to reuse the graph, CLI help, and status display.
// Dependencies are job handles; unique names also identify CLI targets.
internal sealed class BuildGraph
{
    private readonly List<BuildJob> jobs = new();
    private readonly Action<string> log;
    private bool started;
    private readonly Dictionary<string, BuildArgument> arguments = new(StringComparer.Ordinal);
    public IEnumerable<BuildArgument> Arguments => arguments.Values;

    internal BuildArgument RegisterArgument<T>(string name, T defaultValue, string? description,
        Func<T, bool>? validate, string? failureMessage) where T : IParsable<T>
        => RegisterArgument(new BuildArgument<T>(name, defaultValue, description, validate, failureMessage));

    internal BuildArgument RegisterOption(string name, string? description) =>
        RegisterArgument(new BuildOption(name, description));

    private BuildArgument RegisterArgument<T>(BuildArgument<T> argument) where T : IParsable<T>
    {
        var name = argument.Name;
        if (started)
            throw new InvalidOperationException("Cannot declare arguments after the graph has started.");
        if (name is "help" or "h")
            throw new ArgumentException("Help options are built in and cannot be redeclared.");
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('-') || name.Any(char.IsWhiteSpace))
            throw new ArgumentException("Argument names must be non-empty option names without leading dashes.");
        if (arguments.TryGetValue(name, out var existing))
        {
            if (existing.GetType() != argument.GetType() || existing is not BuildArgument<T> typed ||
                !EqualityComparer<T>.Default.Equals(typed.Default, argument.Default) ||
                existing.Description != argument.Description || existing.FailureMessage != argument.FailureMessage ||
                typed.Validator != argument.Validator)
                throw new ArgumentException($"Conflicting declarations for --{name}.");
            return existing;
        }
        arguments.Add(name, argument);
        return argument;
    }

    // Return positional targets; the caller supplies its project-specific default.
    // Help returns no targets, so callers can exit before preflight or execution.
    public string[] ParseArguments(string[] args, string? defaultTarget = null, string command = "build")
    {
        if (started)
            throw new InvalidOperationException("Cannot parse arguments after the graph has started.");
        ArgumentNullException.ThrowIfNull(args);
        if (args.Any(arg => arg is "--help" or "-h"))
        {
            PrintHelp(Console.Out, defaultTarget, command);
            return [];
        }
        var targets = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (arg.StartsWith("--", StringComparison.Ordinal) && HasArgument(arg[2..]))
            {
                var name = arg[2..];
                string value;
                if (IsFlag(name))
                    value = "true";
                else
                {
                    if (++index >= args.Length)
                        throw new ArgumentException($"{arg} requires a value.");
                    value = args[index];
                }
                SetArgument(name, value);
            }
            else if (arg.StartsWith('-'))
                throw new ArgumentException($"Unknown option: {arg}. Use --help for usage.");
            else
                targets.Add(arg);
        }
        return targets.Count == 0 && defaultTarget is not null ? [defaultTarget] : targets.ToArray();
    }

    public void PrintHelp(System.IO.TextWriter writer, string? defaultTarget = null, string command = "build")
    {
        writer.WriteLine($"Usage: {command} [options] [target ...]");
        writer.WriteLine("Builds the named targets and their prerequisites, once each.");
        if (defaultTarget is not null)
            writer.WriteLine($"With no targets, builds {defaultTarget}.");
        writer.WriteLine("Missing requirements are checked before any build job starts.");
        writer.WriteLine("\nOptions:\n  -h, --help  Show this help and exit without building.");

        // Describe argument/secret usage in terms of public artifacts, including
        // requirements inherited from intermediates. Never evaluate predicates here.
        var artifacts = Artifacts.ToArray();
        var prerequisites = artifacts.ToDictionary(job => job,
            job => SelectTargets([job.Name]));
        foreach (var argument in Arguments)
        {
            var valueHint = argument.IsFlag ? "" : " VALUE";
            writer.WriteLine($"  --{argument.Name}{valueHint} ({argument.ValueType.Name}, default: {argument.DefaultText})");
            if (!string.IsNullOrWhiteSpace(argument.Description))
                writer.WriteLine($"    {argument.Description}");
            var users = artifacts.Where(artifact => prerequisites[artifact].Any(job => job.Arguments.Contains(argument)));
            writer.WriteLine($"    Used by: {string.Join(", ", users.Select(job => job.Name))}.");
        }

        writer.WriteLine("\nEnvironment (requirements may be conditional on options):");
        var secrets = jobs.SelectMany(job => job.SecretRequirements)
            .GroupBy(secret => secret.Name, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);
        foreach (var group in secrets)
        {
            var description = group.Select(secret => secret.Description)
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
            writer.WriteLine($"  {group.Key}  {description}".TrimEnd());
            var users = artifacts.Where(artifact => prerequisites[artifact]
                .Any(job => job.SecretRequirements.Any(secret => secret.Name == group.Key)));
            writer.WriteLine($"    Required by: {string.Join(", ", users.Select(job => job.Name))}.");
        }

        writer.WriteLine("\nTargets:");
        var nameWidth = artifacts.Length == 0 ? 0 : artifacts.Max(job => job.Name.Length);
        foreach (var job in artifacts)
            writer.WriteLine(string.IsNullOrWhiteSpace(job.Description)
                ? $"  {job.Name}"
                : $"  {job.Name.PadRight(nameWidth)}  {job.Description}");
    }

    public bool HasArgument(string name) => arguments.ContainsKey(name);
    public bool IsFlag(string name) => arguments[name].IsFlag;
    public void SetArgument(string name, string value)
    {
        if (started)
            throw new InvalidOperationException("Cannot change arguments after the graph has started.");
        arguments[name].Parse(value);
    }
    internal bool GetOption(string name) => ((BuildOption)arguments[name]).Value;

    internal T GetArgument<T>(string name) where T : IParsable<T> =>
        ((BuildArgument<T>)arguments[name]).Value;

    public BuildGraph(Action<string> log) => this.log = log;

    public IEnumerable<string> JobNames => jobs.Select(job => job.Name);
    public IReadOnlyList<BuildJob> Jobs => jobs.AsReadOnly();
    public IEnumerable<BuildJob> Artifacts => jobs.Where(job => job.IsArtifact);

    // Artifacts are public outputs (including aggregate and publishing targets).
    // They execute and participate in dependencies just like intermediate jobs.
    public BuildJob Artifact(string name, string? description = null) =>
        Artifact(name, () => Task.CompletedTask, description);

    public BuildJob Artifact(string name, Action action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Artifact(name, () =>
        {
            action();
            return Task.CompletedTask;
        }, description);
    }

    public BuildJob Artifact(string name, Func<Task> action, string? description = null) =>
        AddJob(name, action, description, isArtifact: true);

    // An actionless node completes when all its dependencies succeed.
    public BuildJob Node(string name, string? description = null) => Job(name, () => Task.CompletedTask, description);

    public BuildJob Job(string name, Action action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Job(name, () =>
        {
            action();
            return Task.CompletedTask;
        }, description);
    }

    public BuildJob Job(string name, Func<Task> action, string? description = null) =>
        AddJob(name, action, description, isArtifact: false);

    private BuildJob AddJob(string name, Func<Task> action, string? description, bool isArtifact)
    {
        if (started)
            throw new InvalidOperationException("Cannot add jobs after the graph has started.");
        ArgumentNullException.ThrowIfNull(action);
        if (string.IsNullOrWhiteSpace(name) || jobs.Any(job => job.Name == name))
            throw new ArgumentException("Job names must be non-empty and unique.", nameof(name));
        var job = new BuildJob(this, name, action, description, isArtifact);
        jobs.Add(job);
        return job;
    }

    // Resolve the requested targets and their transitive prerequisites in graph order.
    public IReadOnlyList<BuildJob> SelectTargets(IEnumerable<string> names)
    {
        var selected = new HashSet<BuildJob>();
        void Include(BuildJob job)
        {
            if (!selected.Add(job))
                return;
            foreach (var dependency in job.Dependencies)
                Include(dependency);
        }
        foreach (var name in names)
        {
            var job = jobs.FirstOrDefault(job => job.Name == name) ??
                throw new ArgumentException($"Unknown target: {name}. Use --help to list available targets.");
            Include(job);
        }
        return jobs.Where(selected.Contains).ToArray();
    }

    public Task RunAsync() => RunAsync(jobs);

    public Task RunAsync(IReadOnlyList<BuildJob> selectedJobs)
    {
        if (started)
            throw new InvalidOperationException("The graph can only run once.");
        Validate(selectedJobs);
        started = true;
        foreach (var job in jobs)
            job.Freeze();
        var tasks = new Dictionary<BuildJob, Task>();

        Task Schedule(BuildJob job)
        {
            if (tasks.TryGetValue(job, out var task))
                return task;
            var prerequisites = job.Dependencies.Select(Schedule).ToArray();
            task = ExecuteAsync(job, prerequisites);
            tasks.Add(job, task);
            return task;
        }

        return Task.WhenAll(selectedJobs.Select(Schedule));
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
                // Queue the action after its prerequisites complete. Queueing
                // the prerequisite wait instead lets sibling continuations run
                // synchronous action work inline, one after another.
                await Task.Run(job.Action).ConfigureAwait(false);
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

    public void Validate() => Validate(jobs);

    public void Validate(IReadOnlyList<BuildJob> selectedJobs)
    {
        var errors = new List<string>();
        var missingSecrets = new HashSet<BuildJob>();
        foreach (var job in selectedJobs)
            job.ResolveRetry();
        foreach (var job in selectedJobs)
            foreach (var secret in job.SecretRequirements)
            {
                bool required;
                try { required = secret.When?.Invoke() ?? true; }
                catch
                {
                    // Fail closed without exposing exceptions that might contain credentials.
                    errors.Add($"  {job.Name}: could not evaluate requirement for {secret.Name}.");
                    missingSecrets.Add(job);
                    continue;
                }
                if (required && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(secret.Name)))
                {
                    errors.Add($"  {job.Name}: required environment variable {secret.Name} is missing.");
                    missingSecrets.Add(job);
                }
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
        foreach (var job in selectedJobs)
            Visit(job);

        bool HasMissingSecrets(BuildJob job) => missingSecrets.Contains(job) ||
            job.Dependencies.Any(HasMissingSecrets);
        foreach (var job in selectedJobs)
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

internal abstract class BuildArgument(string name, string? description, string? failureMessage)
{
    public string Name { get; } = name;
    public string? Description { get; } = description;
    public string? FailureMessage { get; } = failureMessage;
    public virtual bool IsFlag => false;
    public abstract Type ValueType { get; }
    public abstract string DefaultText { get; }
    internal abstract void Parse(string value);
}

internal class BuildArgument<T> : BuildArgument where T : IParsable<T>
{
    public T Default { get; }
    public T Value { get; private set; }
    internal Func<T, bool>? Validator { get; }
    public override Type ValueType => typeof(T);
    public override string DefaultText => Convert.ToString(Default, System.Globalization.CultureInfo.InvariantCulture) ?? "";

    public BuildArgument(string name, T defaultValue, string? description, Func<T, bool>? validate,
        string? failureMessage) : base(name, description, failureMessage)
    {
        if (validate is not null && !validate(defaultValue))
            throw new ArgumentException($"Invalid default for --{name}.");
        Default = Value = defaultValue;
        Validator = validate;
    }

    internal override void Parse(string value)
    {
        if (!T.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ||
            (Validator is not null && !Validator(parsed)))
            throw new ArgumentException(FailureMessage ?? $"--{Name} requires a valid {ValueType.Name} value.");
        Value = parsed;
    }
}

// Options are presence-only switches, unlike value-taking arguments (including bool).
internal sealed class BuildOption(string name, string? description)
    : BuildArgument<bool>(name, false, description, null, null)
{
    public override bool IsFlag => true;
}

// Documentation is metadata only; never store environment values here.
internal sealed record BuildSecret(string Name, string? Description = null, Func<bool>? When = null);

internal sealed class BuildJob
{
    public string Name { get; }
    public string? Description { get; }
    public bool IsArtifact { get; }
    internal Func<Task> Action { get; }
    private readonly List<BuildJob> dependencies = new();
    private readonly BuildGraph graph;
    internal IReadOnlyList<BuildJob> Dependencies => dependencies;
    private readonly Dictionary<string, BuildSecret> secrets = new(StringComparer.Ordinal);
    private readonly List<(Func<bool> Predicate, string Message)> requirements = new();
    internal IEnumerable<(Func<bool> Predicate, string Message)> Requirements => requirements;
    private bool frozen;
    public IEnumerable<BuildSecret> SecretRequirements => secrets.Values;

    private Func<TimeSpan>? retryWindow;
    private readonly List<BuildArgument> arguments = new();
    public IReadOnlyList<BuildArgument> Arguments => arguments.AsReadOnly();

    public BuildJob Argument<T>(string name, T defaultValue, string? description = null,
        Func<T, bool>? validate = null, string? failureMessage = null) where T : IParsable<T>
    {
        if (frozen)
            throw new InvalidOperationException("Cannot declare arguments after the graph has started.");
        var argument = graph.RegisterArgument(name, defaultValue, description, validate, failureMessage);
        if (!arguments.Contains(argument))
            arguments.Add(argument);
        return this;
    }

    public BuildJob Option(string name, string? description = null)
    {
        if (frozen)
            throw new InvalidOperationException("Cannot declare options after the graph has started.");
        var option = graph.RegisterOption(name, description);
        if (!arguments.Contains(option))
            arguments.Add(option);
        return this;
    }

    public bool GetOption(string name) => graph.GetOption(name);

    public T GetArgument<T>(string name) where T : IParsable<T> => graph.GetArgument<T>(name);

    public BuildJob Retry(Func<TimeSpan> window, TimeSpan delay)
    {
        ArgumentNullException.ThrowIfNull(window);
        Retry(TimeSpan.Zero, delay);
        retryWindow = window;
        return this;
    }

    internal void ResolveRetry()
    {
        if (retryWindow is not null)
        {
            var window = retryWindow();
            if (window < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(window));
            RetryWindow = window;
        }
    }

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
        retryWindow = null;
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

    // Conditions are evaluated during preflight, never during graph construction or help.
    public BuildJob RequiresSecret(string name, string? description = null, Func<bool>? when = null)
    {
        if (frozen)
            throw new InvalidOperationException("Cannot change requirements after the graph has started.");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Secret names must be non-empty.", nameof(name));
        if (secrets.TryGetValue(name, out var existing))
        {
            if (string.IsNullOrWhiteSpace(description))
                description = existing.Description;
            // Repeated declarations require the secret if either condition applies.
            var previous = existing.When;
            var current = when;
            when = previous is null || current is null ? null : () => previous() || current();
        }
        secrets[name] = new BuildSecret(name, description, when);
        return this;
    }

    internal void Freeze() => frozen = true;

    internal BuildJob(BuildGraph graph, string name, Func<Task> action, string? description, bool isArtifact)
    {
        Name = name;
        Description = description;
        IsArtifact = isArtifact;
        Action = action;
        this.graph = graph;
    }
}

// ANSI redraws are confined to interactive terminals; redirected logs stay plain.
internal sealed class BuildDisplay : IDisposable
{
    private sealed class Row
    {
        public string State = "waiting";
        public string[] Dependencies = [];
        public readonly Stopwatch Time = new();
    }

    private readonly object gate = new();
    private readonly Dictionary<string, Row> rows;
    private readonly HashSet<string> intermediates;
    private readonly int nameWidth;
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly Timer timer;
    private int drawnLines;
    private int frame;
    private bool disposed;
    public bool Interactive { get; }

    public BuildDisplay(IEnumerable<BuildJob> jobs, bool interactive, IEnumerable<string>? requestedTargets = null)
    {
        var selected = jobs.ToArray();
        var requested = requestedTargets?.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
        rows = selected.ToDictionary(job => job.Name, job => new Row
        {
            Dependencies = job.Dependencies.Select(dependency => dependency.Name).ToArray()
        });
        // Explicit targets are outputs of this invocation from the user's
        // perspective. Keep their rows and count them in the summary without
        // changing graph metadata or exposing intermediate dependencies in help.
        intermediates = selected.Where(job => !job.IsArtifact && !requested.Contains(job.Name))
            .Select(job => job.Name).ToHashSet();
        nameWidth = rows.Count == 0 ? 0 : rows.Keys.Max(name => name.Length);
        Interactive = interactive && !Console.IsOutputRedirected &&
            Environment.GetEnvironmentVariable("TERM") != "dumb";
        timer = new Timer(_ => Refresh(), null, Timeout.Infinite, 120);
        if (Interactive)
        {
            // Ctrl+C terminates the process without guaranteeing using/finally
            // cleanup. Restore the terminal before allowing default termination.
            Console.CancelKeyPress += OnCancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.Out.Write("\u001b[?25l"); // Hide the cursor while animating.
            timer.Change(0, 120);
        }
        else
        {
            Console.Out.WriteLine($"Build   {ArtifactCount} artifacts");
            timer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }
    }

    public void Write(TextWriter writer, string message)
    {
        lock (gate)
        {
            bool lifecycle = Update(message);
            if (!Interactive || disposed)
            {
                if (lifecycle)
                {
                    var name = message.Substring(message.IndexOf(']') + 1).TrimStart().Split(':', 2)[0];
                    // Plain logs are chronological events, not a retained panel.
                    // Include intermediate completion so starts do not look like
                    // jobs that remain running indefinitely.
                    writer.WriteLine(FormatRow(name, rows[name], animated: false));
                    if (message.StartsWith("[FAILED]") || message.StartsWith("[RETRY WARNING]"))
                        writer.WriteLine("    " + message);
                }
                else if (!IsIntermediateRecovery(message))
                    writer.WriteLine(message);
                return;
            }
            // Only clear when inserting permanent output, not on animation frames.
            if (IsIntermediateRecovery(message))
                return;
            if (!lifecycle || message.StartsWith("[FAILED]") || message.StartsWith("[RETRY WARNING]"))
            {
                Clear();
                writer.WriteLine(message);
            }
            Draw();
        }
    }

    private bool Update(string message)
    {
        int close = message.IndexOf(']');
        if (!message.StartsWith("[") || close < 0)
            return false;
        var label = message.Substring(1, close - 1);
        var name = message.Substring(close + 1).TrimStart().Split(':', 2)[0];
        if (!rows.TryGetValue(name, out var row))
            return false;
        switch (label)
        {
            case "START": row.State = "running"; row.Time.Start(); break;
            case "DONE": row.State = "done"; row.Time.Stop(); break;
            case "FAILED": row.State = "failed"; row.Time.Stop(); break;
            case "SKIPPED": row.State = "skipped"; row.Time.Stop(); break;
            case "CANCELED": row.State = "canceled"; row.Time.Stop(); break;
            case "RETRY WARNING": row.State = "retrying"; break;
            default:
                if (label.StartsWith("ATTEMPT ") && label != "ATTEMPT FAILED")
                    row.State = "running";
                else return false;
                break;
        }
        return true;
    }

    private int ArtifactCount => rows.Count - intermediates.Count;
    private int CompletedArtifactCount => rows.Count(pair =>
        !intermediates.Contains(pair.Key) && pair.Value.State == "done");
    private static bool IsActive(Row row) => row.State is "running" or "retrying";

    private bool IsIntermediateRecovery(string message) => message.StartsWith("[RECOVERED]") &&
        intermediates.Contains(message.Substring(message.IndexOf(']') + 1).TrimStart().Split(':', 2)[0]);

    private void Refresh()
    {
        lock (gate)
        {
            if (disposed) return;
            if (Interactive)
                Draw();
            else
            {
                var active = rows.Where(pair => IsActive(pair.Value)).ToArray();
                if (active.Length == 0) return;
                Console.Out.WriteLine($"Build   {CompletedArtifactCount}/{ArtifactCount} artifacts complete   {elapsed.Elapsed:mm\\:ss} elapsed   {active.Length} active jobs");
                foreach (var pair in active)
                    Console.Out.WriteLine(FormatRow(pair.Key, pair.Value, animated: false));
                Console.Out.Flush();
            }
        }
    }

    private void Clear()
    {
        if (drawnLines == 0) return;
        Console.Out.Write($"\u001b[{drawnLines}A\r\u001b[J");
        drawnLines = 0;
    }

    private void Draw()
    {
        int width = 90, height = 24;
        try { width = Math.Max(20, Console.WindowWidth - 1); height = Math.Max(4, Console.WindowHeight); }
        catch (IOException) { }
        var lines = new List<string>
        {
            $"Build   {CompletedArtifactCount}/{ArtifactCount} artifacts complete   {elapsed.Elapsed:mm\\:ss} elapsed"
        };
        var candidates = rows.Where(pair => !intermediates.Contains(pair.Key) || IsActive(pair.Value))
            .OrderBy(pair => IsActive(pair.Value) ? 0 : pair.Value.State == "done" ? 2 : 1).ToArray();
        var visible = candidates.Take(Math.Max(1, height - 4));
        foreach (var pair in visible)
            lines.Add(FormatRow(pair.Key, pair.Value, animated: true));
        if (lines.Count - 1 < candidates.Length)
            lines.Add($"  ... {candidates.Length - lines.Count + 1} other jobs");
        // Overwrite in place, writing the new content BEFORE erasing each line's
        // tail. A single buffered write avoids exposing a blank panel on Windows.
        var output = new StringBuilder();
        if (drawnLines > 0)
            output.Append($"\u001b[{drawnLines}A");
        foreach (var line in lines)
        {
            output.Append('\r');
            output.Append(line.Length > width ? line.Substring(0, width) : line);
            output.Append("\u001b[K\r\n");
        }
        // Clear obsolete rows if a terminal resize reduced the visible panel.
        var obsoleteRows = Math.Max(0, drawnLines - lines.Count);
        for (var index = 0; index < obsoleteRows; index++)
            output.Append("\r\u001b[K\r\n");
        if (obsoleteRows > 0)
            output.Append($"\u001b[{obsoleteRows}A");
        Console.Out.Write(output.ToString());
        drawnLines = lines.Count;
        frame++;
    }

    private string FormatRow(string name, Row row, bool animated)
    {
        string icon = row.State switch
        {
            "running" => animated ? "|/-\\"[frame % 4].ToString() : ">",
            "retrying" => "~", "done" => "+", "failed" => "!",
            "canceled" => "!", "skipped" => "-", _ => "."
        };
        var detail = row.Time.Elapsed.TotalSeconds > 0 ? row.Time.Elapsed.ToString(@"mm\:ss") : "";
        if (row.State == "waiting")
        {
            var pending = row.Dependencies.Where(dependency =>
                rows.TryGetValue(dependency, out var prerequisite) && prerequisite.State != "done").ToArray();
            if (pending.Length > 0)
                detail = "for: " + string.Join(", ", pending);
        }
        return $"  {icon} {name.PadRight(nameWidth)} {row.State,-9} {detail}";
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs args) => RestoreTerminal();
    private void OnProcessExit(object? sender, EventArgs args) => RestoreTerminal();

    private void RestoreTerminal()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true; // Prevent timer callbacks from redrawing during exit.
            timer.Dispose();
            if (!Interactive) return;
            Console.CancelKeyPress -= OnCancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            try
            {
                Console.Out.Write("\u001b[0m\u001b[?25h");
                Console.Out.Flush();
            }
            catch (IOException) { } // Terminal may already be closed during exit.
            catch (ObjectDisposedException) { }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            timer.Dispose();
            try
            {
                if (Interactive)
                    Draw();
                else
                    Console.Out.WriteLine($"Build   {CompletedArtifactCount}/{ArtifactCount} artifacts complete   {elapsed.Elapsed:mm\\:ss} elapsed");
            }
            finally
            {
                RestoreTerminal();
            }
        }
    }
}
