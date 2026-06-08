# MyriaWorld — Project ToDo

Legend: ✅ Done | 🔧 In Progress | ⬜ Not Started | ⚠️ Partial

---

## Game Mechanics

| # | Item | Status | Notes |
|---|------|--------|-------|
| GM1 | XP grant on kill | ⬜ Not Started | `WorldScreen` shows `+X EXP` message but never calls `_player.GainXp()`. Hook in after monster death in both `DoAutoAttack` and `ExecuteSkill`. |
| GM2 | Level-up notification | ⬜ Not Started | Subscribe to `_player.LeveledUp` in `WorldScreen`; show a HUD overlay when it fires. |
| GM3 | Loot drops on kill | ⬜ Not Started | Call `LootGenerator.GetLootFor(monster)` on kill; add items to `_player.Inventory`. Show a brief loot notification in the HUD. |
| GM4 | Mana regeneration | ⬜ Not Started | Regen mana over time in `WorldScreen.Update` (e.g. small flat amount per second, out of combat). |
| GM5 | Death XP penalty | ⬜ Not Started | Call `_player.ApplyDeathXpPenalty()` inside `TriggerDeath()`. |
| GM6 | Quest kill tracking | ⬜ Not Started | On monster kill, iterate `_player.ActiveQuests` and update `KillProgress` for matching monster IDs. Check `QuestManager` completion logic. |

---

## UI / HUD

| # | Item | Status | Notes |
|---|------|--------|-------|
| UI1 | Window resize support | ⬜ Not Started | `_sw`, `_sh`, and `_skillBarBounds` are only set in `LoadContent`. Override `OnResize` (or check in `Update`) to recompute layout when the window size changes. |
| UI2 | XP bar in HUD | ⬜ Not Started | Show an XP progress bar (current / next level) below the HP/MP bars in the top-left panel. |
| UI3 | Floating combat text | ⬜ Not Started | Replace the single `_infoMessage` slot with a list of timed floating text entries (damage numbers, heals, XP gains) positioned in world or screen space. |
| UI4 | Inventory screen | ⬜ Not Started | Basic in-world inventory overlay (toggle with `I` or `B`) showing items in `_player.Inventory`. |
| UI5 | Loot popup / pickup | ⬜ Not Started | Show a brief loot list when items are added to inventory (e.g. `+ Iron Ore x1`). |

---

## Screens / Flow

| # | Item | Status | Notes |
|---|------|--------|-------|
| SC1 | Character creation screen | ⬜ Not Started | `MainMenuScreen.OnSinglePlayer` release path — needs a race + name entry screen before `LoadingScreen`. |
| SC2 | Character selection screen | ⬜ Not Started | List saved local characters; pick one or create new. Feeds into `LoadingScreen`. |
| SC3 | Settings screen | ⬜ Not Started | `MainMenuScreen.OnSettings` is a stub. Needs at minimum: resolution/fullscreen toggle, volume, keybinds. |
| SC4 | Login / Register — server wiring | ⬜ Not Started | `LoginScreen.HandleLogin` and `HandleRegister` show placeholder text. Wire to `ServerApiService` REST calls then navigate to character selection. |

---

## World & Content

| # | Item | Status | Notes |
|---|------|--------|-------|
| WC1 | Expand navmesh (world.json) | ⬜ Not Started | Current mesh is 5 placeholder faces. Build out a real layout matching the room graph in `rooms.json`. |
| WC2 | NPC interaction in world | ⬜ Not Started | NPCs from `room.NpcRefs` are not spawned or interactable. Add NPC world entities and a press-to-interact trigger. |
| WC3 | Gathering in world | ⬜ Not Started | `room.GatheringSpots` exist in data but are not represented in the 3D world. Add gathering node entities with `GatherService` integration. |

---

_Last updated: 2026-06-07_
