namespace Roguelike.Core;

public sealed class ItemInstance
{
    public EntityId InstanceId { get; init; } = EntityId.New();

    public required string TemplateId { get; init; }

    public int CurrentCharges { get; set; }

    public int StackCount { get; set; } = 1;

    public bool IsIdentified { get; set; }
}
