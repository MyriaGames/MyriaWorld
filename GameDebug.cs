using MyriaLib.Entities;
using MyriaLib.Entities.Characters;
using MyriaLib.Systems.Enums;

namespace MyriaWorld;

/// <summary>Debug-mode helpers — never called in release builds.</summary>
public static class GameDebug
{
#if DEBUG
    public static bool Active => true;
#else
    public static bool Active => false;
#endif

    public static Character CreateDummyCharacter()
    {
        var stats = new Stats { BaseHealth = 250, BaseMana = 80 };

        // SkillFactory.GetSkillsFor is called inside the constructor; if game
        // services aren't loaded it will return an empty list, which is fine here.
        var p = new Character("Debug", stats)
        {
            Level = 5,
            Class = CharacterClass.Fighter,
            Race  = CharacterRace.Myralu,
        };

        // Ensure HP/MP start full relative to the debug stats
        p.CurrentHealth = p.MaxHealth > 0 ? p.MaxHealth : 250;
        p.CurrentMana   = p.MaxMana   > 0 ? p.MaxMana   : 80;

        p.Money.TryAdd(10_000); // 10 silver starting gold for debug

        return p;
    }
}
