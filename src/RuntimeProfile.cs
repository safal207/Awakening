using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Probuzhdenie;

public sealed class RuntimeProfileOptions
{
    public double DurationSeconds { get; init; } = 600;
    public string ReportPath { get; init; } = Path.Combine(Environment.CurrentDirectory, "runtime-profile.json");

    public static RuntimeProfileOptions? TryParse(string[] args)
    {
        int profileIndex = Array.IndexOf(args, "--runtime-profile");
        if (profileIndex < 0) return null;

        double durationSeconds = 600;
        string reportPath = Path.Combine(Environment.CurrentDirectory, "runtime-profile.json");

        for (int i = profileIndex + 1; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "--profile-seconds" && i + 1 < args.Length)
            {
                durationSeconds = ParsePositiveDouble(args[++i], durationSeconds);
            }
            else if (arg.StartsWith("--profile-seconds=", StringComparison.Ordinal))
            {
                durationSeconds = ParsePositiveDouble(arg["--profile-seconds=".Length..], durationSeconds);
            }
            else if (arg == "--profile-output" && i + 1 < args.Length)
            {
                reportPath = Path.GetFullPath(args[++i]);
            }
            else if (arg.StartsWith("--profile-output=", StringComparison.Ordinal))
            {
                reportPath = Path.GetFullPath(arg["--profile-output=".Length..]);
            }
            else if (double.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out double positionalDuration))
            {
                durationSeconds = Math.Max(1, positionalDuration);
            }
        }

        return new RuntimeProfileOptions
        {
            DurationSeconds = Math.Max(1, durationSeconds),
            ReportPath = reportPath,
        };
    }

    private static double ParsePositiveDouble(string value, double fallback)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? Math.Max(1, parsed)
            : fallback;
    }
}

public readonly record struct RuntimeProfileGlInfo(string Vendor, string Renderer, string Version);

public readonly record struct RuntimeProfileSnapshot(
    double ElapsedSeconds,
    long ManagedMemoryBytes,
    long ThreadAllocatedBytes,
    long EstimatedGpuBufferBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    int NpcCount,
    float TimeOfDay);

public sealed class RuntimeProfiler
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly RuntimeProfileOptions _options;
    private readonly Stopwatch _clock = new();
    private readonly List<double> _frameMilliseconds = new(4096);
    private readonly List<RuntimeProfileSnapshot> _samples = new(1024);
    private RuntimeProfileGlInfo _glInfo;
    private double _elapsedSeconds;
    private double _nextSampleAtSeconds;
    private bool _completed;

    public RuntimeProfiler(RuntimeProfileOptions options)
    {
        _options = options;
    }

    public bool IsComplete => _completed;
    public bool ShouldComplete => !_completed && _elapsedSeconds >= _options.DurationSeconds;

    public void Start(RuntimeProfileGlInfo glInfo)
    {
        _glInfo = glInfo;
        _clock.Start();
        _nextSampleAtSeconds = 0;
    }

    public void RecordFrame(double frameSeconds, RuntimeProfileSnapshot snapshot)
    {
        if (_completed) return;

        _elapsedSeconds += Math.Max(0, frameSeconds);
        _frameMilliseconds.Add(Math.Max(0, frameSeconds) * 1000.0);

        if (_elapsedSeconds >= _nextSampleAtSeconds)
        {
            _samples.Add(snapshot);
            _nextSampleAtSeconds = Math.Floor(_elapsedSeconds) + 1.0;
        }
    }

    public void Complete(RuntimeProfileSnapshot finalSnapshot, bool interrupted = false)
    {
        if (_completed) return;
        _completed = true;
        _clock.Stop();

        if (_samples.Count == 0 || _samples[^1].ElapsedSeconds < finalSnapshot.ElapsedSeconds)
            _samples.Add(finalSnapshot);

        RuntimeProfileReport report = BuildReport(finalSnapshot, interrupted);
        string? directory = Path.GetDirectoryName(_options.ReportPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(_options.ReportPath, JsonSerializer.Serialize(report, JsonOptions));
        Console.WriteLine($"Runtime profile complete. Report: {_options.ReportPath}");
        Console.WriteLine($"Duration: {report.DurationSeconds:F1}s, frames: {report.FrameCount}, avg FPS: {report.AverageFps:F1}, p95 frame: {report.P95FrameMs:F2}ms, allocated/frame: {report.ThreadAllocatedBytesPerFrame:F1} B");
    }

    private RuntimeProfileReport BuildReport(RuntimeProfileSnapshot finalSnapshot, bool interrupted)
    {
        var sortedFrames = _frameMilliseconds.OrderBy(value => value).ToArray();
        double duration = Math.Max(_elapsedSeconds, 0.0001);
        int frameCount = _frameMilliseconds.Count;
        double avgFrameMs = frameCount > 0 ? _frameMilliseconds.Average() : 0;
        long startAllocated = _samples.Count > 0 ? _samples[0].ThreadAllocatedBytes : finalSnapshot.ThreadAllocatedBytes;
        long totalAllocated = Math.Max(0, finalSnapshot.ThreadAllocatedBytes - startAllocated);

        return new RuntimeProfileReport(
            Interrupted: interrupted,
            RequestedDurationSeconds: _options.DurationSeconds,
            DurationSeconds: duration,
            WallClockSeconds: _clock.Elapsed.TotalSeconds,
            FrameCount: frameCount,
            AverageFps: frameCount / duration,
            AverageFrameMs: avgFrameMs,
            MinFrameMs: sortedFrames.Length > 0 ? sortedFrames[0] : 0,
            MaxFrameMs: sortedFrames.Length > 0 ? sortedFrames[^1] : 0,
            P50FrameMs: Percentile(sortedFrames, 0.50),
            P95FrameMs: Percentile(sortedFrames, 0.95),
            P99FrameMs: Percentile(sortedFrames, 0.99),
            ManagedMemoryStartBytes: _samples.Count > 0 ? _samples[0].ManagedMemoryBytes : finalSnapshot.ManagedMemoryBytes,
            ManagedMemoryEndBytes: finalSnapshot.ManagedMemoryBytes,
            ManagedMemoryPeakBytes: _samples.Count > 0 ? _samples.Max(sample => sample.ManagedMemoryBytes) : finalSnapshot.ManagedMemoryBytes,
            ThreadAllocatedBytesTotal: totalAllocated,
            ThreadAllocatedBytesPerFrame: frameCount > 0 ? totalAllocated / (double)frameCount : 0,
            Gen0Collections: finalSnapshot.Gen0Collections - (_samples.Count > 0 ? _samples[0].Gen0Collections : finalSnapshot.Gen0Collections),
            Gen1Collections: finalSnapshot.Gen1Collections - (_samples.Count > 0 ? _samples[0].Gen1Collections : finalSnapshot.Gen1Collections),
            Gen2Collections: finalSnapshot.Gen2Collections - (_samples.Count > 0 ? _samples[0].Gen2Collections : finalSnapshot.Gen2Collections),
            EstimatedGpuBufferBytesEnd: finalSnapshot.EstimatedGpuBufferBytes,
            EstimatedGpuBufferBytesPeak: _samples.Count > 0 ? _samples.Max(sample => sample.EstimatedGpuBufferBytes) : finalSnapshot.EstimatedGpuBufferBytes,
            NpcCount: finalSnapshot.NpcCount,
            GlVendor: _glInfo.Vendor,
            GlRenderer: _glInfo.Renderer,
            GlVersion: _glInfo.Version,
            Notes: "GPU memory is app-owned GL buffer memory estimated from BufferData capacities; exact VRAM usage is driver-specific and not exposed portably.",
            Samples: _samples);
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0) return 0;
        double index = (sortedValues.Length - 1) * percentile;
        int lower = (int)Math.Floor(index);
        int upper = (int)Math.Ceiling(index);
        if (lower == upper) return sortedValues[lower];
        double fraction = index - lower;
        return sortedValues[lower] * (1.0 - fraction) + sortedValues[upper] * fraction;
    }
}

public sealed record RuntimeProfileReport(
    bool Interrupted,
    double RequestedDurationSeconds,
    double DurationSeconds,
    double WallClockSeconds,
    int FrameCount,
    double AverageFps,
    double AverageFrameMs,
    double MinFrameMs,
    double MaxFrameMs,
    double P50FrameMs,
    double P95FrameMs,
    double P99FrameMs,
    long ManagedMemoryStartBytes,
    long ManagedMemoryEndBytes,
    long ManagedMemoryPeakBytes,
    long ThreadAllocatedBytesTotal,
    double ThreadAllocatedBytesPerFrame,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    long EstimatedGpuBufferBytesEnd,
    long EstimatedGpuBufferBytesPeak,
    int NpcCount,
    string GlVendor,
    string GlRenderer,
    string GlVersion,
    string Notes,
    IReadOnlyList<RuntimeProfileSnapshot> Samples);
