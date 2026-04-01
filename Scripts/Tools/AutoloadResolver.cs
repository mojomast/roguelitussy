using System;
using Godot;

namespace Godotussy;

internal static class AutoloadResolver
{
    public static T? Resolve<T>(Node context, string autoloadName) where T : class
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(autoloadName))
        {
            throw new ArgumentException("Autoload name is required.", nameof(autoloadName));
        }

        var viewportMatch = FindNamedChild<T>(context.GetViewport(), autoloadName);
        if (viewportMatch is not null)
        {
            return viewportMatch;
        }

        for (Node? current = context; current is not null; current = current.GetParent())
        {
            if (string.Equals(current.Name, autoloadName, StringComparison.Ordinal) && current is T currentMatch)
            {
                return currentMatch;
            }

            var childMatch = FindNamedChild<T>(current, autoloadName);
            if (childMatch is not null)
            {
                return childMatch;
            }
        }

        return null;
    }

    private static T? FindNamedChild<T>(Node parent, string autoloadName) where T : class
    {
        foreach (var child in parent.GetChildren())
        {
            if (string.Equals(child.Name, autoloadName, StringComparison.Ordinal) && child is T match)
            {
                return match;
            }
        }

        return null;
    }
}