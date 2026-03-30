using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Roguelike.Core.Simulation;

namespace Roguelike.Core.Persistence;

public static class EntitySerializer
{
    public static JsonObject Serialize(IEntity entity)
    {
        var obj = new JsonObject
        {
            ["id"] = entity.Id.Value.ToString(),
            ["name"] = entity.Name,
            ["posX"] = entity.Position.X,
            ["posY"] = entity.Position.Y,
            ["faction"] = (byte)entity.Faction,
            ["blocksMovement"] = entity.BlocksMovement,
            ["blocksSight"] = entity.BlocksSight,
            ["stats"] = SerializeStats(entity.Stats),
        };

        var inventory = entity.GetComponent<Inventory>();
        if (inventory is not null)
        {
            var items = new JsonArray();
            foreach (var item in inventory.Items)
                items.Add(SerializeItem(item));
            obj["inventory"] = items;
        }

        return obj;
    }

    public static Entity Deserialize(JsonObject obj)
    {
        var id = new EntityId(Guid.Parse(obj["id"]!.GetValue<string>()));
        var name = obj["name"]!.GetValue<string>();
        var pos = new Position(obj["posX"]!.GetValue<int>(), obj["posY"]!.GetValue<int>());
        var faction = (Faction)obj["faction"]!.GetValue<byte>();
        var stats = DeserializeStats(obj["stats"]!.AsObject());

        var entity = new Entity(id, name, pos, stats, faction)
        {
            BlocksMovement = obj["blocksMovement"]!.GetValue<bool>(),
            BlocksSight = obj["blocksSight"]!.GetValue<bool>(),
        };

        if (obj.ContainsKey("inventory"))
        {
            var inventory = new Inventory();
            foreach (var itemNode in obj["inventory"]!.AsArray())
                inventory.Add(DeserializeItem(itemNode!.AsObject()));
            entity.SetComponent(inventory);
        }

        return entity;
    }

    private static JsonObject SerializeStats(Stats s) =>
        new()
        {
            ["hp"] = s.HP,
            ["maxHp"] = s.MaxHP,
            ["attack"] = s.Attack,
            ["defense"] = s.Defense,
            ["accuracy"] = s.Accuracy,
            ["evasion"] = s.Evasion,
            ["speed"] = s.Speed,
            ["viewRadius"] = s.ViewRadius,
            ["energy"] = s.Energy,
        };

    private static Stats DeserializeStats(JsonObject obj) =>
        new()
        {
            HP = obj["hp"]!.GetValue<int>(),
            MaxHP = obj["maxHp"]!.GetValue<int>(),
            Attack = obj["attack"]!.GetValue<int>(),
            Defense = obj["defense"]!.GetValue<int>(),
            Accuracy = obj["accuracy"]!.GetValue<int>(),
            Evasion = obj["evasion"]!.GetValue<int>(),
            Speed = obj["speed"]!.GetValue<int>(),
            ViewRadius = obj["viewRadius"]!.GetValue<int>(),
            Energy = obj["energy"]!.GetValue<int>(),
        };

    private static JsonObject SerializeItem(ItemInstance item) =>
        new()
        {
            ["instanceId"] = item.InstanceId.Value.ToString(),
            ["templateId"] = item.TemplateId,
            ["currentCharges"] = item.CurrentCharges,
            ["stackCount"] = item.StackCount,
            ["isIdentified"] = item.IsIdentified,
        };

    private static ItemInstance DeserializeItem(JsonObject obj) =>
        new()
        {
            InstanceId = new EntityId(Guid.Parse(obj["instanceId"]!.GetValue<string>())),
            TemplateId = obj["templateId"]!.GetValue<string>(),
            CurrentCharges = obj["currentCharges"]!.GetValue<int>(),
            StackCount = obj["stackCount"]!.GetValue<int>(),
            IsIdentified = obj["isIdentified"]!.GetValue<bool>(),
        };
}
