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
        // Myralu race base bonuses + 8 level-ups of Myralu growth (lvl 1→9):
        //   STR: 4 (race) + 8×1 = 12   END: 6 (race) + 8×2 = 22
        var stats = new Stats
        {
            BaseHealth   = 350,
            BaseMana     = 100,
            Strength     = 12,   // race 4 + 8 levels × 1
            Dexterity    = 9,    // race 1 + 8 levels × 1
            Endurance    = 22,   // race 6 + 8 levels × 2
            Intelligence = 20,   // race 4 + 8 levels × 2
            Spirit       = 19,   // race 3 + 8 levels × 2
        };

        var p = new Character("Debug", stats)
        {
            Level = 9,
            Class = CharacterClass.Fighter,
            Race  = CharacterRace.Myralu,
        };

        // Fighter class level 4: ExtraSTR+28, ExtraDEX+8, ExtraEND+16
        // TotalXpToReach(4) = 5000 × 3 × 4 / 2 = 30000
        p.ClassXp[CharacterClass.Fighter] = 30_000L;

        p.CurrentHealth = p.MaxHealth > 0 ? p.MaxHealth : 250;
        p.CurrentMana   = p.MaxMana   > 0 ? p.MaxMana   : 80;

        p.Money.TryAdd(10_000); // 10 silver starting gold for debug

        return p;
    }
}
