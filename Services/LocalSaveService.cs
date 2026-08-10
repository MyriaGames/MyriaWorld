using System.Text.Json;
using MyriaLib.Entities.Characters;
using MyriaLib.Models;
using MyriaLib.Services;
using MyriaLib.Systems;

namespace Myria.Mono.Services;

/// <summary>
/// Manages local single-player character saves under Data/saves/local-{name}.json.
/// List() is safe to call any time; Load() must be called after WorldDataService.Load().
/// </summary>
public static class LocalSaveService
{
    private const string LocalUser = "local";

    private static string SaveDir    => Path.Combine("Data", "saves");
    private static string FilePath(string name) => Path.Combine(SaveDir, $"{LocalUser}-{name}.json");

    public record CharacterSummary(string Name, string Race, string Class, int Level);

    // ── Listing ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns lightweight summaries by peeking at the JSON header of each save file.
    /// Safe to call before game data is loaded.
    /// </summary>
    public static List<CharacterSummary> List()
    {
        var result = new List<CharacterSummary>();
        if (!Directory.Exists(SaveDir)) return result;

        foreach (var file in Directory.GetFiles(SaveDir, $"{LocalUser}-*.json").OrderBy(f => f))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                // Support both bare format and the { "Character": {...} } wrapper used by CharacterService
                JsonElement root = doc.RootElement.TryGetProperty("Character", out var wrapped)
                    ? wrapped : doc.RootElement;

                string name  = root.TryGetProperty("Name",  out var n) ? n.GetString() ?? "" : "";
                string race  = root.TryGetProperty("Race",  out var r) ? r.GetString() ?? "" : "";
                string cls   = root.TryGetProperty("Class", out var c) ? c.GetString() ?? "" : "";
                int    level = root.TryGetProperty("Level", out var l) ? l.GetInt32() : 1;

                if (!string.IsNullOrEmpty(name))
                    result.Add(new CharacterSummary(name, race, cls, level));
            }
            catch { /* corrupt save — skip */ }
        }
        return result;
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    public static void Save(Character character)
    {
        var opts = new JsonSerializerOptions
        {
            WriteIndented          = true,
            PropertyNameCaseInsensitive = true,
            Converters             = { new ItemConverter(), new MoneyConverter() },
        };
        Directory.CreateDirectory(SaveDir);
        File.WriteAllText(FilePath(character.Name), JsonSerializer.Serialize(character, opts));
    }

    // ── Load (call after WorldDataService.Load()) ─────────────────────────────

    public static Character? Load(string name)
    {
        var fakeUser = new UserAccount { Username = LocalUser };
        return CharacterService.LoadCharacter(name, fakeUser);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public static void Delete(string name)
    {
        string path = FilePath(name);
        if (File.Exists(path)) File.Delete(path);
    }

    public static bool NameExists(string name) => File.Exists(FilePath(name));
}
