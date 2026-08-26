# Myria — MonoGame Client

Myria is an RPG built around character races and classes, jobs and skill
progression, monsters and quests, gathering and crafting, equipment and
loot, and social systems like guilds, parties, friends, and trading — the
full data model lives in the shared [`Myria.Lib`](https://github.com/MyriaGames/MyriaLib)
library and is consumed identically by all of Myria's clients. This
repository, **Myria.Mono**, is the MonoGame-based desktop client: a 3D,
navmesh-driven take on the game, currently in active early development.

## Current Status

This client renders and plays a full local, single-player slice of Myria,
but it is **not yet connected to the multiplayer backend**. The main menu's
Login and Register screens exist and are fully laid out, but their action
handlers (`LoginScreen.HandleLogin` / `HandleRegister` in
[`Screens/LoginScreen.cs`](Screens/LoginScreen.cs)) are still TODO stubs —
they just display "Login not yet implemented." / "Registration not yet
implemented." and do not call the auth server. `Myria.Mono/TODO.md` tracks
this explicitly as item `SC4` ("Login / Register — server wiring — Not
Started").

Everything you can do today happens through **local character saves**
(`Data/saves/local-*.json`, managed by `LocalSaveService`) rather than a
server account — there is no live multiplayer session yet.

## Features

What's already implemented and playable:

- **3D world rendering** on a custom navigation mesh (`World/NavMesh.cs`,
  `NavMeshRenderer.cs`) loaded from `Content/world.json`, with terrain
  heights, procedural textures, static world decorations, a day/night
  cycle, and a weather system.
- **Character creation & local saves** — race/class picker with stat
  preview (`Screens/CharacterCreationScreen.cs`), a character-select screen
  backed by local JSON saves (`Screens/CharacterSelectScreen.cs`,
  `Services/LocalSaveService.cs`, `Services/SaveService.cs`).
- **Combat, XP and leveling** — auto-attacks and skills against
  `WorldMonster` entities, XP/loot on kill, level-up notifications, death
  XP penalties, mana regeneration.
- **NPCs and quests** — `WorldNpc` entities with dialog panels and
  service sub-panels (shop, sell, learn job, change class, craft/upgrade),
  quest kill-tracking and auto-completion.
- **Gathering and crafting** — `WorldGatherNode` entities tied into
  `Myria.Lib`'s gather/craft services.
- **UI overlays** — inventory, quest log, shop, dialogue, world map,
  fast travel, pause menu, zone transitions (see `UI/`).
- **Settings** — fullscreen/resolution presets persisted to
  `Data/settings.json` via `Services/SettingsService.cs`.
- **Procedural audio** — sound effects generated at runtime, no external
  audio assets required (`Services/AudioService.cs`).

Not yet implemented: any connection to the live auth/realm servers (login,
registration, multiplayer sessions, guilds/parties/trading against real
other players).

## Architecture

Myria is split across several repositories under the
[MyriaGames](https://github.com/MyriaGames) organization:

- **[MyriaLib](https://github.com/MyriaGames/MyriaLib)** — shared,
  client-agnostic game logic: entities (characters, monsters, items,
  skills, quests, jobs...), services, and systems. Referenced directly as
  a project reference from this repo (`Myria.Lib.Core.csproj`).
- **[MyriaAuthServer](https://github.com/MyriaGames/MyriaAuthServer)** and
  **[MyriaServer](https://github.com/MyriaGames/MyriaServer)** — the
  backend auth and realm servers that multiplayer clients talk to.
- **[MyriaRPG](https://github.com/MyriaGames/MyriaRPG)** (WPF) and
  **[ConsoleWorldRPG](https://github.com/MyriaGames/ConsoleWorldRPG)**
  (console) — the two existing clients that are already fully wired up to
  the auth/realm servers and support live multiplayer today.
- **MyriaWorld** (this repository, `Myria.Mono`) — the MonoGame client,
  intended to become a third fully-fledged client alongside the WPF and
  console clients once its server wiring lands.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0` target
  framework, per `Myria.Mono.csproj`)
- MonoGame via NuGet — `MonoGame.Framework.DesktopGL` and
  `MonoGame.Content.Builder.Task`, both pinned to `3.8.*`
- A checked-out copy of [`Myria.Lib`](https://github.com/MyriaGames/MyriaLib)
  as a sibling directory (`../Myria.Lib/Myria.Lib.Core/Myria.Lib.Core.csproj`
  is referenced directly from `Myria.Mono.csproj`)

## Getting Started

```bash
# from the Myria.Mono directory
dotnet build
dotnet run
```

The project restores the MonoGame Content Builder automatically via the
`MonoGame.Content.Builder.Task` package reference, so a plain `dotnet
build`/`dotnet run` is sufficient — no separate MGCB install step is
required.

## Legal / Privacy

Because this client is planned to eventually connect to the same account
system as the WPF and console clients, its `Legal/` folder already
contains the same privacy and terms documents used across the project —
[`Legal/Impressum.md`](Legal/Impressum.md),
[`Legal/Datenschutzerklaerung.md`](Legal/Datenschutzerklaerung.md), and
[`Legal/Nutzungsbedingungen.md`](Legal/Nutzungsbedingungen.md) — and they
already explicitly account for the Mono client even though it isn't
connected to multiplayer yet.

## License

MIT — see [`LICENSE`](LICENSE).

## Contributing

Myria is an active, early-development hobby project. If you'd like to
help, wiring up `LoginScreen.HandleLogin` / `HandleRegister` in
[`Screens/LoginScreen.cs`](Screens/LoginScreen.cs) to the auth server
(mirroring how the WPF and console clients already do it) is the single
biggest missing piece and a good place to start — see `TODO.md` (item
`SC4`) for the current state.
