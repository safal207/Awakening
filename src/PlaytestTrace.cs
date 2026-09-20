using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Probuzhdenie;

/// <summary>
/// Opt-in, local-only playtest trace.
/// Writes a bounded JSONL event stream and never transmits data.
/// No free text, player input, machine identity or world coordinates are recorded.
/// </summary>
public static class PlaytestTrace
{
    private static readonly object Gate = new();
    private static readonly Stopwatch Clock = new();
    private static string? _path;

    public static bool Enabled => !string.IsNullOrWhiteSpace(_path);
    public static string? CurrentPath => _path;

    public static bool Configure(string[] args)
    {
        if (args == null) return false;

        string? requestedPath = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "--playtest-trace", StringComparison.OrdinalIgnoreCase))
                continue;

            requestedPath = args[i + 1];
            break;
        }

        if (string.IsNullOrWhiteSpace(requestedPath))
            return false;

        try
        {
            string fullPath = Path.GetFullPath(requestedPath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            fullPath = NextAvailablePath(fullPath);

            lock (Gate)
            {
                _path = fullPath;
                File.WriteAllText(fullPath, "");
                Clock.Restart();
            }

            Record("trace_started");
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Playtest trace disabled: {e.Message}");
            lock (Gate)
            {
                _path = null;
                Clock.Reset();
            }
            return false;
        }
    }

    public static void Record(
        string eventType,
        int? day = null,
        string? actionId = null,
        string? value = null)
    {
        if (string.IsNullOrWhiteSpace(eventType)) return;

        lock (Gate)
        {
            if (string.IsNullOrWhiteSpace(_path)) return;

            var row = new
            {
                elapsed_ms = Clock.ElapsedMilliseconds,
                event_type = eventType,
                day,
                action_id = EmptyToNull(actionId),
                value = EmptyToNull(value),
            };

            try
            {
                File.AppendAllText(_path, JsonSerializer.Serialize(row) + Environment.NewLine);
            }
            catch (Exception e)
            {
                // Playtest evidence must never break the game.
                Console.WriteLine($"Playtest trace write failed: {e.Message}");
            }
        }
    }

    public static void Close()
    {
        lock (Gate)
        {
            if (string.IsNullOrWhiteSpace(_path)) return;
        }

        Record("trace_closed");

        lock (Gate)
        {
            _path = null;
            Clock.Reset();
        }
    }

    private static string NextAvailablePath(string requestedPath)
    {
        if (!File.Exists(requestedPath))
            return requestedPath;

        string? directory = Path.GetDirectoryName(requestedPath);
        string name = Path.GetFileNameWithoutExtension(requestedPath);
        string extension = Path.GetExtension(requestedPath);

        for (int suffix = 1; suffix < 1000; suffix++)
        {
            string candidate = Path.Combine(
                directory ?? ".",
                $"{name}.{suffix}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }

        throw new IOException("Could not allocate a unique local playtest trace filename.");
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
