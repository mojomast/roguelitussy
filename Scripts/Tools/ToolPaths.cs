using System;
using System.IO;
using Godot;
using Roguelike.Core;

namespace Godotussy;

internal static class ToolPaths
{
    public static string ResolveContentDirectory(string? preferredDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredDirectory) && Directory.Exists(preferredDirectory))
        {
            return Path.GetFullPath(preferredDirectory);
        }

        var godotContentPath = ProjectSettings.GlobalizePath("res://Content");
        if (!string.IsNullOrWhiteSpace(godotContentPath) && Directory.Exists(godotContentPath))
        {
            return Path.GetFullPath(godotContentPath);
        }

        var startDirectory = preferredDirectory;
        if (string.IsNullOrWhiteSpace(startDirectory))
        {
            startDirectory = Directory.GetCurrentDirectory();
        }

        return ContentLoader.FindContentDirectory(startDirectory);
    }

    public static string ResolveContentFile(string fileName, string? preferredDirectory = null)
    {
        return Path.Combine(ResolveContentDirectory(preferredDirectory), fileName);
    }
}