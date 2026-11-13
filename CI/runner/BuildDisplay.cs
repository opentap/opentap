using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text;

// ANSI redraws are confined to interactive terminals; redirected logs stay plain.
internal sealed class BuildDisplay : IDisposable
{
    private sealed class Row
    {
        public string State = "waiting";
        public readonly Stopwatch Time = new();
    }

    private readonly object gate = new();
    private readonly Dictionary<string, Row> rows;
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly Timer timer;
    private int drawnLines;
    private int frame;
    private bool disposed;
    public bool Interactive { get; }

    public BuildDisplay(IEnumerable<string> names, bool interactive)
    {
        rows = names.ToDictionary(name => name, _ => new Row());
        Interactive = interactive && !Console.IsOutputRedirected &&
            Environment.GetEnvironmentVariable("TERM") != "dumb";
        if (Interactive)
            Console.Out.Write("\u001b[?25l"); // Hide the cursor while animating.
        else
            Console.Out.WriteLine($"OpenTAP build   {rows.Count} jobs");
        timer = new Timer(_ => Refresh(), null, Interactive ? 0 : Timeout.Infinite, 120);
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
                    writer.WriteLine(FormatRow(name, rows[name], animated: false));
                    if (message.StartsWith("[FAILED]") || message.StartsWith("[RETRY WARNING]"))
                        writer.WriteLine("    " + message);
                }
                else
                    writer.WriteLine(message);
                return;
            }
            // Only clear when inserting permanent output, not on animation frames.
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

    private void Refresh()
    {
        lock (gate)
        {
            if (disposed || !Interactive) return;
            Draw();
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
            $"OpenTAP build   {rows.Values.Count(row => row.State == "done")}/{rows.Count} complete   {elapsed.Elapsed:mm\\:ss} elapsed"
        };
        var visible = rows.OrderBy(pair => pair.Value.State is "running" or "retrying" ? 0 : 1)
            .Take(Math.Max(1, height - 4));
        foreach (var pair in visible)
            lines.Add(FormatRow(pair.Key, pair.Value, animated: true));
        if (lines.Count - 1 < rows.Count)
            lines.Add($"  ... {rows.Count - lines.Count + 1} other jobs");
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
        return $"  {icon} {name,-24} {row.State,-9} {(row.Time.Elapsed.TotalSeconds > 0 ? row.Time.Elapsed.ToString(@"mm\:ss") : "")}";
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
                    Console.Out.WriteLine($"OpenTAP build   {rows.Values.Count(row => row.State == "done")}/{rows.Count} complete   {elapsed.Elapsed:mm\\:ss} elapsed");
            }
            finally
            {
                if (Interactive) Console.Out.Write("\u001b[?25h");
                disposed = true;
            }
        }
    }
}
