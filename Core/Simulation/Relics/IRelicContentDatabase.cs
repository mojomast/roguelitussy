using System.Collections.Generic;

namespace Roguelike.Core;

public interface IRelicContentDatabase
{
    IReadOnlyDictionary<string, RelicTemplate> RelicTemplates { get; }

    bool TryGetRelicTemplate(string relicId, out RelicTemplate template);
}
