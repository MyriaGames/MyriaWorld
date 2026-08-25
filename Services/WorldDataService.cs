using System.Text.Json;
using Myria.Lib.Core.Entities.Maps;
using Myria.Lib.Core.Entities.Characters;
using Myria.Lib.Core.Entities.NPCs;
using Myria.Lib.Core.Services;
using Myria.Lib.Core.Services.Manager;
using Myria.Lib.Core.Systems.Enums;

namespace Myria.Mono.Services;

/// <summary>
/// Owns the "load all game data" step for Myria.Mono.  Call <see cref="Load"/>
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
        QuestManager.LoadQuests();

        // Shared Myria.Lib localization system — Npc.ToString() (NPC display names) and other
        // shared-lib code resolve their locale keys through this, independently of the
        // Mono-only _locale dictionary below (which backs WorldDataService.Localize/GetItemName).
        Myria.Lib.Core.Systems.Localization.Load(Myria.Lib.Core.Systems.Enums.GameLanguage.En);

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
        => Myria.Lib.Core.Services.Builder.SkillFactory.UpdateSkills(player);

    // ── Room queries ──────────────────────────────────────────────────────────

    public static Room? GetRoom(int roomId)
        => IsLoaded ? RoomService.GetRoomById(roomId) : null;

    public static string GetRoomName(Room room)
    {
        string raw = _locale.TryGetValue(room.Name, out var s) ? s : room.Name;
        return AsciiSafe(raw);
    }

    public static string GetItemName(Myria.Lib.Core.Entities.Items.Item item)
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

    /// <summary>Looks up a locale key and returns the translated string, or the key itself if not found.</summary>
    public static string Localize(string key)
    {
        string raw = _locale.TryGetValue(key, out var s) ? s : key;
        return AsciiSafe(raw);
    }

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
