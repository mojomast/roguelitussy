using System.Linq;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.RenderingTests;

public sealed class AssetConventionTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Rendering.Status icons follow the circular badge convention", StatusIconsFollowBadgeConvention);
        registry.Add("Rendering.SVG icons declare a 32x32 canvas without prologs or opaque backgrounds", SvgIconsDeclareCanvasConventions);
        registry.Add("Rendering.Every sprite asset has import metadata", EverySpriteAssetHasImportMetadata);
        registry.Add("Rendering.No import metadata is orphaned", NoImportMetadataIsOrphaned);
        registry.Add("Rendering.Import uids are unique across assets", ImportUidsAreUnique);
        registry.Add("Rendering.Import metadata points at its own source file", ImportMetadataPointsAtOwnSource);
        registry.Add("Rendering.Contracted status and item icons exist", ContractedStatusAndItemIconsExist);
    }

    private static string AssetsRoot => System.IO.Path.Combine(FindRepositoryRoot(), "Assets");

    private static string FindRepositoryRoot()
    {
        var current = System.IO.Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(current, "godotussy.csproj")))
            {
                return current;
            }

            current = System.IO.Path.GetDirectoryName(current) ?? string.Empty;
        }

        return System.IO.Directory.GetCurrentDirectory();
    }

    private static void StatusIconsFollowBadgeConvention()
    {
        var statusIcons = System.IO.Directory.GetFiles(
            System.IO.Path.Combine(AssetsRoot, "Sprites", "ui"), "status_*.svg");
        Expect.True(statusIcons.Length > 0, "There should be status icons under Assets/Sprites/ui.");

        foreach (var icon in statusIcons)
        {
            var markup = System.IO.File.ReadAllText(icon);
            Expect.True(markup.Contains("circle cx=\"16\" cy=\"16\" r=\"13\"", System.StringComparison.Ordinal),
                $"{System.IO.Path.GetFileName(icon)} should carry the shared circular badge background.");
            Expect.True(markup.Contains("#1b1d24", System.StringComparison.Ordinal),
                $"{System.IO.Path.GetFileName(icon)} should use the house outline color #1b1d24.");
        }
    }

    private static void SvgIconsDeclareCanvasConventions()
    {
        foreach (var svg in System.IO.Directory.GetFiles(AssetsRoot, "*.svg", System.IO.SearchOption.AllDirectories))
        {
            var markup = System.IO.File.ReadAllText(svg);
            var name = System.IO.Path.GetFileName(svg);
            Expect.False(markup.Contains("<?xml", System.StringComparison.Ordinal),
                $"{name} should not carry an XML prolog.");
            Expect.True(markup.Contains("width=\"32\"", System.StringComparison.Ordinal)
                && markup.Contains("height=\"32\"", System.StringComparison.Ordinal),
                $"{name} should declare an explicit 32x32 canvas.");
            Expect.False(markup.Contains("<rect width=\"32\" height=\"32\" fill=\"#", System.StringComparison.Ordinal),
                $"{name} should keep a transparent background instead of an opaque backdrop rect.");
        }
    }

    private static void EverySpriteAssetHasImportMetadata()
    {
        var assets = System.IO.Directory.GetFiles(AssetsRoot, "*.*", System.IO.SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".svg", System.StringComparison.Ordinal)
                || path.EndsWith(".png", System.StringComparison.Ordinal));

        foreach (var asset in assets)
        {
            Expect.True(System.IO.File.Exists(asset + ".import"),
                $"{System.IO.Path.GetFileName(asset)} should have matching .import metadata so Godot can load it.");
        }
    }

    private static void NoImportMetadataIsOrphaned()
    {
        foreach (var import in System.IO.Directory.GetFiles(AssetsRoot, "*.import", System.IO.SearchOption.AllDirectories))
        {
            var source = import[..^".import".Length];
            Expect.True(System.IO.File.Exists(source),
                $"{System.IO.Path.GetFileName(import)} has no source asset and should be deleted.");
        }
    }

    private static void ImportUidsAreUnique()
    {
        var uids = System.IO.Directory.GetFiles(AssetsRoot, "*.import", System.IO.SearchOption.AllDirectories)
            .Select(path => System.IO.File.ReadLines(path).FirstOrDefault(line => line.StartsWith("uid=", System.StringComparison.Ordinal)))
            .Where(line => !string.IsNullOrEmpty(line))
            .ToArray();

        Expect.True(uids.Length > 0, "Import files should declare uids.");
        Expect.Equal(uids.Length, uids.Distinct().Count(), "Every asset import uid must be unique.");
    }

    private static void ImportMetadataPointsAtOwnSource()
    {
        var root = FindRepositoryRoot();
        foreach (var import in System.IO.Directory.GetFiles(AssetsRoot, "*.import", System.IO.SearchOption.AllDirectories))
        {
            var sourceLine = System.IO.File.ReadLines(import)
                .FirstOrDefault(line => line.StartsWith("source_file=", System.StringComparison.Ordinal));
            Expect.True(sourceLine is not null, $"{System.IO.Path.GetFileName(import)} should declare a source_file.");

            var expectedResPath = "res://" + System.IO.Path.GetRelativePath(root, import[..^".import".Length]).Replace('\\', '/');
            Expect.Equal($"source_file=\"{expectedResPath}\"", sourceLine!,
                $"{System.IO.Path.GetFileName(import)} should reference its own source asset.");
        }
    }

    private static void ContractedStatusAndItemIconsExist()
    {
        var contracted = new[]
        {
            System.IO.Path.Combine("Sprites", "ui", "status_regenerating.svg"),
            System.IO.Path.Combine("Sprites", "ui", "status_flying.svg"),
            System.IO.Path.Combine("Sprites", "ui", "status_blinded.svg"),
            System.IO.Path.Combine("Sprites", "items", "arrows_bundle.svg"),
            System.IO.Path.Combine("Sprites", "items", "potion_mana.svg"),
            System.IO.Path.Combine("Sprites", "items", "scroll_frost_nova.svg"),
        };

        foreach (var relative in contracted)
        {
            var path = System.IO.Path.Combine(AssetsRoot, relative);
            Expect.True(System.IO.File.Exists(path), $"Contracted icon {relative} should exist.");
            Expect.True(System.IO.File.Exists(path + ".import"), $"Contracted icon {relative} should have import metadata.");
        }
    }
}
