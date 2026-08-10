using Microsoft.Xna.Framework;
using Myria.Lib.Core.Entities.Items;

namespace Myria.Mono.World;

public sealed class WorldLootDrop
{
    public Vector3    Position { get; }
    public List<Item> Items    { get; }
    public int        Gold     { get; private set; }

    public bool IsEmpty => Items.Count == 0 && Gold == 0;

    public WorldLootDrop(Vector3 position, List<Item> items, int gold)
    {
        Position = position;
        Items    = items;
        Gold     = gold;
    }

    public void TakeGold() => Gold = 0;
}
