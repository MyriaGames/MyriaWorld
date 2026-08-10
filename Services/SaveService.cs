using System.Text.Json;
using Microsoft.Xna.Framework;
using MyriaLib.Entities.Characters;
using MyriaLib.Entities.Items;
using MyriaLib.Services.Builder;
using MyriaLib.Services.Manager;

namespace Myria.Mono.Services;

public static class SaveService
{
    private static readonly string SavePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "Myria.Mono", "save.json");

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    // ── DTOs ─────────────────────────────────────────────────────────────────

    private sealed record ItemEntry(string Id, int Stack);

    private sealed record SaveData(
        float PosX, float PosY, float PosZ,
        int   CurrentHp, int MaxHp,
        int   CurrentMp, int MaxMp,
        long  Experience,
        int   Level,
        long  GoldBronze,
        List<ItemEntry>  Inventory,
        List<string>     ActiveQuestIds,
        List<string>     CompletedQuestIds,
        List<int>        DiscoveredWaypointRoomIds);

    // ── Save ─────────────────────────────────────────────────────────────────

    public static void Save(Character player, Vector3 position,
                            IEnumerable<int> discoveredWaypointRoomIds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);

        var data = new SaveData(
            PosX: position.X, PosY: position.Y, PosZ: position.Z,
            CurrentHp: player.CurrentHealth, MaxHp: player.MaxHealth,
            CurrentMp: player.CurrentMana,   MaxMp: player.MaxMana,
            Experience: (long)player.Experience,
            Level:      player.Level,
            GoldBronze: player.Money.Balance.BronzeTotal,
            Inventory:  player.Inventory.Items
                             .Select(i => new ItemEntry(i.Id, i.StackSize))
                             .ToList(),
            ActiveQuestIds:             player.ActiveQuests.Select(q => q.Id).ToList(),
            CompletedQuestIds:          player.CompletedQuests.Select(q => q.Id).ToList(),
            DiscoveredWaypointRoomIds:  discoveredWaypointRoomIds.ToList());

        File.WriteAllText(SavePath, JsonSerializer.Serialize(data, Opts));
    }

    // ── Load ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads save data into <paramref name="player"/> and returns
    /// the saved world position and the list of discovered waypoint roomIds.
    /// </summary>
    public static (Vector3 pos, List<int> waypointRoomIds) Load(Character player)
    {
        if (!File.Exists(SavePath)) return (Vector3.Zero, []);

        SaveData? data;
        try { data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(SavePath)); }
        catch { return (Vector3.Zero, []); }
        if (data == null) return (Vector3.Zero, []);

        // Stats
        player.CurrentHealth = Math.Clamp(data.CurrentHp, 0, data.MaxHp);
        player.CurrentMana   = Math.Clamp(data.CurrentMp, 0, data.MaxMp);
        player.Experience    = data.Experience;

        // Level: apply level-ups to reach saved level
        while (player.Level < data.Level)
            player.LevelUp();

        // Gold
        long current = player.Money.Balance.BronzeTotal;
        if (data.GoldBronze > current) player.Money.TryAdd(data.GoldBronze - current);

        // Inventory
        player.Inventory.Items.Clear();
        foreach (var entry in data.Inventory)
        {
            if (ItemFactory.TryCreateItem(entry.Id, out var item))
            {
                item.StackSize = entry.Stack;
                player.Inventory.AddItem(item, player);
            }
        }

        // Quests
        player.ActiveQuests.Clear();
        player.CompletedQuests.Clear();
        foreach (var id in data.ActiveQuestIds)
        {
            var q = QuestManager.GetQuestById(id)?.Clone();
            if (q != null) { q.Status = MyriaLib.Systems.Enums.QuestStatus.InProgress; player.ActiveQuests.Add(q); }
        }
        foreach (var id in data.CompletedQuestIds)
        {
            var q = QuestManager.GetQuestById(id)?.Clone();
            if (q != null) { q.Status = MyriaLib.Systems.Enums.QuestStatus.Returned; player.CompletedQuests.Add(q); }
        }

        return (new Vector3(data.PosX, data.PosY, data.PosZ),
                data.DiscoveredWaypointRoomIds ?? []);
    }

    public static bool HasSave() => File.Exists(SavePath);
}
