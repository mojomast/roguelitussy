using System.Reflection;
using Roguelike.Tests.TestFramework;

var registry = new TestRegistry();
var filter = ParseFilter(args);

var suiteTypes = Assembly.GetExecutingAssembly()
    .GetTypes()
    .Where(type => typeof(ITestSuite).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
    .OrderBy(type => type.FullName)
    .ToArray();

foreach (var suiteType in suiteTypes)
{
    if (Activator.CreateInstance(suiteType) is ITestSuite suite)
    {
        suite.Register(registry);
    }
}

return registry.RunAll(filter);

static string? ParseFilter(string[] args)
{
    for (var index = 0; index < args.Length; index++)
    {
        var arg = args[index];
        if (string.Equals(arg, "--filter", StringComparison.OrdinalIgnoreCase))
        {
            return index + 1 < args.Length ? args[index + 1] : string.Empty;
        }

        const string FilterPrefix = "--filter=";
        if (arg.StartsWith(FilterPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return arg[FilterPrefix.Length..];
        }
    }

    return null;
}
