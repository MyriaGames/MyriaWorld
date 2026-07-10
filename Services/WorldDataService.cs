using System.Text.Json;
using MyriaLib.Entities.Maps;
using MyriaLib.Entities.Characters;
using MyriaLib.Entities.NPCs;
using MyriaLib.Services;
using MyriaLib.Systems.Enums;

namespace MyriaWorld.Services;

/// <summary>
/// Owns the "load all game data" step for MyriaWorld.  Call <see cref="Load"/>
/// once on a background thread (see <c>LoadingScreen</c>), then call
/// <see cref="PrepareCharacter"/> on the main thread to wire skills to the player.
/// </summary>
public static class WorldDataService
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static Dictionary<string, string> _locale = new();

    public static bool IsLoaded { get; private set; }

    // ── Initialization ────────────────────────────────────────────────────────

    /// <summary>
    /// Calls <see cref="GameService.InitializeGame()"/> (loads rooms, monsters,
    /// skills, and all other shared data) then loads the English locale file for
    /// display-name resolution.  Safe to run on a background thread.
    /// </summary>
    public static void Load()
    {
        GameService.InitializeGame();

        if (File.Exists("Data/locales/en.json"))
        {
            _locale = JsonSerializer.Deserialize<Dictionary<string, string>>(
                          File.ReadAllText("Data/locales/en.json"), _jsonOpts)
                      ?? new();
        }

        IsLoaded = true;
    }

    /// <summary>
    /// Populates the player's skill list from the loaded skill data.
    /// Must be called after <see cref="Load"/> completes and before
    /// entering <c>WorldScreen</c>.
    /// </summary>
    public static void PrepareCharacter(Character player)
        => MyriaLib.Services.Builder.SkillFactory.UpdateSkills(player);

    // ── Room queries ──────────────────────────────────────────────────────────

    public static Room? GetRoom(int roomId)
        => IsLoaded ? RoomService.GetRoomById(roomId) : null;

    public static string GetRoomName(Room room)
    {
        string raw = _locale.TryGetValue(room.Name, out var s) ? s : room.Name;
        return AsciiSafe(raw);
    }

    public static string GetItemName(MyriaLib.Entities.Items.Item item)
    {
        if (_locale.TryGetValue(item.Name, out var s) && s.Length > 0) return AsciiSafe(s);
        string raw = item.Name.StartsWith("item.") ? item.Name[5..] : item.Name;
        return string.Join(" ", raw.Split('_').Select(w => w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));
    }

    // Replace typographic characters that fall outside the SpriteFont's
    // character regions (ASCII 32-126 + Latin-1 160-255).
    public static string AsciiSafe(string s) => s
        .Replace('—', '-')   // em dash
        .Replace('–', '-')   // en dash
        .Replace('…', '.')   // horizontal ellipsis
        .Replace('‘', '\'')  // left single quote
        .Replace('’', '\'')  // right single quote
        .Replace('“', '"')   // left double quote
        .Replace('”', '"');  // right double quote

    // ── NPC name / description resolution ────────────────────────────────────

    public static string GetNpcName(Npc npc) =>
        AsciiSafe(_locale.TryGetValue(npc.NameKey, out var s) && s.Length > 0 ? s : npc.Id);

    public static string GetNpcDesc(Npc npc) =>
        AsciiSafe(_locale.TryGetValue(npc.DescriptionKey, out var s) ? s : "");

    public static bool CanEnter(Character player, Room room)
        => !IsLoaded || RoomService.CanEnterRoom(room, player);

    public static string DeniedReason(Character player, Room room)
    {
        if (room.RequirementType == RoomRequirementType.Level)
            return $"Requires Level {room.AccessLevel}";
        if (room.RequirementType == RoomRequirementType.Quest)
            return "Requires quest completion";
        return "Access denied";
    }
}
