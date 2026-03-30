using System.Reflection;
using Roguelike.Tests.TestFramework;

var registry = new TestRegistry();

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

return registry.RunAll();
