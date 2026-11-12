using System;
using System.Collections.Generic;
using System.Text;

namespace ProbablyStolenModTemplate;

internal sealed class MaintenanceDiagnostics
{
    private const int Capacity = 64;
    private readonly Dictionary<(string Module, string Stage, string Code, string Error), int> _counts = new();
    private int _omitted;

    // Use fixed module, stage and code identifiers. Exception messages can contain player data.
    internal void Record(string module, string stage, string code, Exception error)
    {
        var key = (module, stage, code, error?.GetType().Name);
        if (_counts.TryGetValue(key, out int count))
            _counts[key] = Increment(count);
        else if (_counts.Count < Capacity)
            _counts.Add(key, 1);
        else
            _omitted = Increment(_omitted);
    }

    internal string Format()
    {
        var report = new StringBuilder("诊断故障");
        foreach (var entry in _counts)
        {
            var (module, stage, code, error) = entry.Key;
            report.Append('\n').Append(module).Append('/').Append(stage).Append('/').Append(code);
            if (error != null) report.Append(" (").Append(error).Append(')');
            report.Append(" x").Append(entry.Value);
        }
        if (_omitted > 0) report.Append("\n超出容量的故障次数 ").Append(_omitted);
        if (_counts.Count == 0) report.Append(" 0");
        return report.ToString();
    }

    internal void Clear()
    {
        _counts.Clear();
        _omitted = 0;
    }

    private static int Increment(int value) => value == int.MaxValue ? value : value + 1;
}
