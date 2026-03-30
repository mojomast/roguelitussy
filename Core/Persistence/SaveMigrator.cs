using System;
using System.Text.Json.Nodes;

namespace Roguelike.Core.Persistence;

public static class SaveMigrator
{
    public const int CurrentVersion = 1;

    public static JsonObject Migrate(JsonObject save, int fromVersion, int toVersion)
    {
        if (fromVersion == toVersion)
            return save;

        if (fromVersion > toVersion)
            throw new InvalidOperationException(
                $"Cannot downgrade save from version {fromVersion} to {toVersion}.");

        // Apply migrations sequentially
        for (int v = fromVersion; v < toVersion; v++)
        {
            save = v switch
            {
                // Future migrations go here:
                // 1 => MigrateV1ToV2(save),
                _ => throw new InvalidOperationException($"No migration path from version {v}."),
            };
        }

        save["version"] = toVersion;
        return save;
    }
}
