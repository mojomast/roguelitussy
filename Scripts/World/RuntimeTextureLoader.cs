using System.Collections.Generic;
using System.IO;
using Godot;

namespace Godotussy;

internal static class RuntimeTextureLoader
{
    public static Texture2D? Load(string path, Dictionary<string, Texture2D?> cache)
    {
        if (cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var texture = IsImportedCacheMissing(path)
            ? LoadSourceTexture(path) ?? GD.Load<Texture2D>(path)
            : GD.Load<Texture2D>(path) ?? LoadSourceTexture(path);
        cache[path] = texture;
        return texture;
    }

    private static bool IsImportedCacheMissing(string path)
    {
        var importMetadataPath = ProjectSettings.GlobalizePath(path + ".import");
        if (!File.Exists(importMetadataPath))
        {
            return false;
        }

        foreach (var line in File.ReadLines(importMetadataPath))
        {
            const string prefix = "path=\"";
            if (!line.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                continue;
            }

            var importedPath = line[prefix.Length..].TrimEnd('"');
            return !File.Exists(ProjectSettings.GlobalizePath(importedPath));
        }

        return false;
    }

    private static Texture2D? LoadSourceTexture(string path)
    {
        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        if (image is null || image.IsEmpty())
        {
            return null;
        }

        var texture = ImageTexture.CreateFromImage(image);
        texture.ResourcePath = path;
        return texture;
    }
}
