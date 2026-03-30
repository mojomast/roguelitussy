using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class ContentDatabase : Node
{
    public IContentDatabase? Database { get; private set; }

    public bool IsLoaded => Database is not null;

    public void SetDatabase(IContentDatabase database)
    {
        Database = database;
    }
}
