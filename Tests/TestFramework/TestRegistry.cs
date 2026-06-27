using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace Roguelike.Tests.TestFramework;

public sealed class TestRegistry
{
    private readonly List<(string Name, Action Test)> _tests = new();

    public void Add(string name, Action test)
    {
        _tests.Add((name, test));
    }

    public int RunAll(string? filter = null, TextWriter? output = null)
    {
        output ??= Console.Out;
        var failed = 0;
        var executed = 0;
        var total = _tests.Count;
        var activeFilter = string.IsNullOrWhiteSpace(filter) ? null : filter;

        foreach (var (name, test) in _tests)
        {
            if (activeFilter is not null && !name.Contains(activeFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            executed++;
            GodotStubTestState.ResetTestState();

            try
            {
                test();
                output.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                failed++;
                output.WriteLine($"--- FAIL {name} ---");
                output.WriteLine(ex.ToString());
                output.WriteLine("--- END FAIL ---");
            }
        }

        output.WriteLine($"Executed {executed} of {total} registered tests. Skipped {total - executed}. Failures: {failed}.");
        return failed == 0 ? 0 : 1;
    }
}
