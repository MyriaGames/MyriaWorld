using MyriaLib.Entities;
using MyriaLib.Entities.Players;
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

    public static Player CreateDummyPlayer()
    {
        var stats = new Stats { BaseHealth = 250, BaseMana = 80 };

        // SkillFactory.GetSkillsFor is called inside the constructor; if game
        // services aren't loaded it will return an empty list, which is fine here.
        var p = new Player("Debug", stats)
        {
            Level = 5,
            Class = PlayerClass.Fighter,
            Race  = PlayerRace.Myralu,
        };

        // Ensure HP/MP start full relative to the debug stats
        p.CurrentHealth = p.MaxHealth > 0 ? p.MaxHealth : 250;
        p.CurrentMana   = p.MaxMana   > 0 ? p.MaxMana   : 80;

        return p;
    }
}
