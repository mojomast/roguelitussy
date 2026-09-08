using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

namespace Roguelitussy.WindowsSingleFileLauncher;

internal static class Program
{
    private const string PayloadResourceName = "Roguelitussy.Payload.zip";
    private const string GameExecutableName = "Roguelitussy.exe";

    private static int Main(string[] args)
    {
        string version = SanitizeVersion(
            Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "local");
        string extractionDirectory = Path.Combine(Path.GetTempPath(), "Roguelitussy", version);

        try
        {
            TryDeleteDirectory(extractionDirectory);
            using Stream payload = OpenPayload();
            ExtractPayload(payload, extractionDirectory);

            string gamePath = Path.Combine(extractionDirectory, GameExecutableName);
            if (!File.Exists(gamePath))
            {
                Console.Error.WriteLine($"Roguelitussy launcher error: embedded payload is missing {GameExecutableName}.");
                return 1;
            }

            ProcessStartInfo startInfo = new()
            {
                FileName = gamePath,
                WorkingDirectory = extractionDirectory,
                UseShellExecute = false,
            };
            foreach (string argument in args)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process game = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The game process could not be started.");
            game.WaitForExit();
            return game.ExitCode;
        }
        catch (FileNotFoundException exception)
        {
            Console.Error.WriteLine($"Roguelitussy launcher error: {exception.Message}");
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Roguelitussy launcher error: {exception.Message}");
            return 1;
        }
        finally
        {
            TryDeleteDirectory(extractionDirectory);
        }
    }

    private static Stream OpenPayload()
    {
        Stream? payload = Assembly.GetEntryAssembly()?.GetManifestResourceStream(PayloadResourceName);
        return payload ?? throw new FileNotFoundException("The embedded game ZIP payload was not found.");
    }

    private static void ExtractPayload(Stream payload, string destination)
    {
        Directory.CreateDirectory(destination);
        string destinationRoot = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        using ZipArchive archive = new(payload, ZipArchiveMode.Read);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string relativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            string outputPath = Path.GetFullPath(Path.Combine(destination, relativePath));
            if (!outputPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Unsafe ZIP entry path: {entry.FullName}");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(outputPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using Stream input = entry.Open();
            using FileStream output = File.Create(outputPath);
            input.CopyTo(output);
        }
    }

    private static string SanitizeVersion(string version)
    {
        char[] characters = version.Select(character =>
            char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '_').ToArray();
        string safe = new string(characters).Trim('_');
        return safe.Length == 0 ? "local" : safe;
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Temporary files can remain locked by the OS after the child exits.
        }
    }
}
