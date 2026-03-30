using System;
using System.Collections.Generic;

namespace Roguelike.Tests.TestFramework;

public sealed class TestRegistry
{
    private readonly List<(string Name, Action Test)> _tests = new();

    public void Add(string name, Action test)
    {
        _tests.Add((name, test));
    }

    public int RunAll()
    {
        var failed = 0;

        foreach (var (name, test) in _tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"FAIL {name}: {ex.Message}");
            }
        }

        Console.WriteLine($"Executed {_tests.Count} tests. Failures: {failed}.");
        return failed == 0 ? 0 : 1;
    }
}
