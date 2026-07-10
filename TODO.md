# MyriaWorld — Project ToDo

Legend: ✅ Done | 🔧 In Progress | ⬜ Not Started | ⚠️ Partial

---

## Game Mechanics

| # | Item | Status | Notes |
|---|------|--------|-------|
| GM1 | XP grant on kill | ✅ Done | `OnMonsterKilled` calls `_player.GainXp(m.Data.Exp)` — hooked from both `DoAutoAttack` and `ExecuteSkill`. |
| GM2 | Level-up notification | ✅ Done | Subscribed to `_player.LeveledUp` in `LoadContent`; fading gold overlay at screen top-quarter. |
| GM3 | Loot drops on kill | ✅ Done | `OnMonsterKilled` calls `LootGenerator.GetLootFor`, adds items to inventory, shows a float text. |
| GM4 | Mana regeneration | ✅ Done | 2 % of max mana restored every 3 s out of combat in `WorldScreen.Update`. |
| GM5 | Death XP penalty | ✅ Done | `_player.ApplyDeathXpPenalty()` called in `TriggerDeath()`. |
| GM6 | Quest kill tracking | ✅ Done | `OnMonsterKilled` updates `KillProgress` and auto-completes quests. |

---

## UI / HUD

| # | Item | Status | Notes |
|---|------|--------|-------|
| UI1 | Window resize support | ✅ Done | Viewport dimensions checked at top of `Update`; `_sw`/`_sh`/`_skillBarBounds` recomputed on change. |
| UI2 | XP bar in HUD | ✅ Done | Gold XP bar drawn at y=80 below HP/MP bars; hostile-area indicator pushed to y=96. |
| UI3 | Floating combat text | ✅ Done | Single `_infoMessage` replaced with `List<FloatText>`; messages drift upward and fade, staggered to avoid overlap. |
| UI4 | Inventory screen | ✅ Done | Overlay toggled with `I`/`B` (ESC closes); shows equipment slots, item list with rarity colours, pagination (PageUp/Down). |
| UI5 | Loot popup / pickup | ✅ Done | Loot float text shown after each kill via `OnMonsterKilled`. |

---

## Screens / Flow

| # | Item | Status | Notes |
|---|------|--------|-------|
| SC1 | Character creation screen | ✅ Done | `CharacterCreationScreen` — race list (left), class grid (right), name entry + stat preview. Loads races/classes JSON directly. Creates character and passes to `LoadingScreen(player, saveOnLoad:true)`. |
| SC2 | Character selection screen | ✅ Done | `CharacterSelectScreen` — lists `Data/saves/local-*.json` via `LocalSaveService.List()` (no game data needed). Play/New/Delete with confirm dialog. Keyboard navigation. |
| SC3 | Settings screen | ✅ Done | `SettingsScreen` — fullscreen toggle + resolution picker (4 presets). Persists to `Data/settings.json`. Applied on startup via `SettingsService` + `Game1.Display`. In-world: ESC → pause menu (Resume / Save / Settings / Main Menu). F5 quick-saves anywhere. Audio section placeholder for future work. |
| SC4 | Login / Register — server wiring | ⬜ Not Started | `LoginScreen.HandleLogin` and `HandleRegister` show placeholder text. Wire to `ServerApiService` REST calls then navigate to character selection. |

---

## World & Content

| # | Item | Status | Notes |
|---|------|--------|-------|
| WC1 | Expand navmesh (world.json) | ✅ Done | 12 faces, 26 vertices. Original 8 + Echo Chamber (room 2, ore+monsters), Nuvmito Turn (room 25, monsters), Plateau South (room 15, monsters), Whispering Woods (room 21, tree gather+monsters). Portal fork at Nuvmito Trail: east→Turn, south→Woods. |
| WC2 | NPC interaction in world | ✅ Done | `WorldNpc` entities spawned from `world.json` placements per face. Type-coloured humanoid meshes, projected floating name labels, `[F] Talk` prompt when within range. Dialog panel shows name, description, service buttons (Heal is functional; others show "coming soon"). |
| WC3 | Gathering in world | ✅ Done | `WorldGatherNode` entities spawned from `world.json` `gatherNodes` array per face. Type-coloured meshes (boulder/tree/herb), projected floating labels, `[G] Gather` prompt within range. `GatherService.Gather()` called on keypress; depletion/no-tool/full-inventory results shown as float text. |

---

## World Dressing

| # | Item | Status | Notes |
|---|------|--------|-------|
| WD1 | Static world decorations | ✅ Done | `WorldDecorations.cs` pre-bakes trees, rocks, pillars, benches, well, walls, stumps per zone. Drawn each frame with `Matrix.Identity` via the shared `BasicEffect`. |

---

## NPC Service Panels

| # | Service | Status | Notes |
|---|---------|--------|-------|
| NPC1 | shop_equipment | ✅ Done | Sub-panel with item list (rarity coloured), detail pane, Buy button. |
| NPC2 | sell_items | ✅ Done | Shows player inventory with sell value; Sell 1 / Sell All buttons. |
| NPC3 | learn_job | ✅ Done | Immediate action; sets `ActiveJobId` via JobManager with cooldown check. |
| NPC4 | change_class | ✅ Done | Class list panel; shows group + level; calls ClassManager.SetClass + refreshes skills. |
| NPC5 | upgrade / craft | ⬜ Not Started | Still show "coming soon". |

---

_Last updated: 2026-06-20 (session 5)_
