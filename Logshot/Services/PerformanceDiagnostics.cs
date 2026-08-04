using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Logshot.Services;

/// <summary>
/// Tracks Android performance metrics for baseline and optimization comparison.
/// Usage: Call Start/Stop pairs and retrieve metrics via GetMetrics().
/// </summary>
public sealed class PerformanceDiagnostics
{
    private static readonly Lazy<PerformanceDiagnostics> _instance = new(() => new PerformanceDiagnostics());
    public static PerformanceDiagnostics Instance => _instance.Value;

    private readonly Dictionary<string, Stopwatch> _activeTimers = new();
    private readonly Dictionary<string, List<long>> _measurements = new();
    private readonly Dictionary<string, int> _counters = new();
    private readonly object _lock = new();

    private PerformanceDiagnostics() { }

    /// <summary>
    /// Start timing an operation. Call Stop with the same key to record duration.
    /// </summary>
    public void Start(string operationKey)
    {
        lock (_lock)
        {
            if (!_activeTimers.ContainsKey(operationKey))
            {
                _activeTimers[operationKey] = Stopwatch.StartNew();
            }
        }
    }

    /// <summary>
    /// Stop timing and record the elapsed milliseconds.
    /// </summary>
    public void Stop(string operationKey)
    {
        lock (_lock)
        {
            if (_activeTimers.TryGetValue(operationKey, out var sw))
            {
                sw.Stop();
                if (!_measurements.ContainsKey(operationKey))
                {
                    _measurements[operationKey] = new List<long>();
                }
                _measurements[operationKey].Add(sw.ElapsedMilliseconds);
                _activeTimers.Remove(operationKey);
            }
        }
    }

    /// <summary>
    /// Increment a counter (e.g., database writes, property changes).
    /// </summary>
    public void Increment(string counterKey)
    {
        lock (_lock)
        {
            if (!_counters.ContainsKey(counterKey))
            {
                _counters[counterKey] = 0;
            }
            _counters[counterKey]++;
        }
    }

    /// <summary>
    /// Get a summary of all collected metrics.
    /// </summary>
    public string GetMetrics()
    {
        lock (_lock)
        {
            var lines = new List<string>();
            lines.Add("=== Performance Metrics ===");
            lines.Add("");

            if (_measurements.Count > 0)
            {
                lines.Add("-- Timing (ms) --");
                foreach (var kvp in _measurements.OrderBy(x => x.Key))
                {
                    var values = kvp.Value;
                    var avg = values.Count > 0 ? values.Average() : 0;
                    var min = values.Count > 0 ? values.Min() : 0;
                    var max = values.Count > 0 ? values.Max() : 0;
                    lines.Add($"{kvp.Key}: Count={values.Count}, Avg={avg:F1}ms, Min={min}ms, Max={max}ms");
                }
                lines.Add("");
            }

            if (_counters.Count > 0)
            {
                lines.Add("-- Counters --");
                foreach (var kvp in _counters.OrderBy(x => x.Key))
                {
                    lines.Add($"{kvp.Key}: {kvp.Value}");
                }
                lines.Add("");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Clear all metrics.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _activeTimers.Clear();
            _measurements.Clear();
            _counters.Clear();
        }
    }

    /// <summary>
    /// Get the count for a specific counter.
    /// </summary>
    public int GetCount(string counterKey)
    {
        lock (_lock)
        {
            return _counters.TryGetValue(counterKey, out var count) ? count : 0;
        }
    }

    /// <summary>
    /// Get average time for a specific operation.
    /// </summary>
    public double GetAverage(string operationKey)
    {
        lock (_lock)
        {
            if (_measurements.TryGetValue(operationKey, out var values) && values.Count > 0)
            {
                return values.Average();
            }
            return 0;
        }
    }
}
