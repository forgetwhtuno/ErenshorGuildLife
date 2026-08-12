# AGENTS.md — Erenshor Guild Life

Instructions for AI/coding agents working in this repository. Read this before making changes.

## What this mod is

A read-only guild-presence and verified bulletin layer for Erenshor (BepInEx 5 plugin, .NET Framework 4.8, C# 5 effective language level via `csc`). It displays the player's native guild roster (and zone/level where the native data exposes it) and keeps a bounded local Guild Bulletin of verified events other mods report.

## Core design boundary

- Guild Life is **read-only**. It does not create a guild, invite/kick members, alter rank, recruit Sims, start guild quests or raids, move/summon Sims, or send guild chat.
- It does not Harmony-patch any game method and does not write any Erenshor save.
- Native guild objects are resolved through **reflection**, and the code must fail closed (not throw, not fabricate data) if a game update changes the expected shape. See `src/GuildReader.cs`.
- The Bulletin only records events a caller already verified via `GuildLifeApi.PostVerifiedEvent(...)`. Guild Life does not infer that an event happened and does not parse prose to decide provenance — the caller owns that.

## What Erenshor remains authoritative for

Guild membership, rank, invites, recruitment, and all guild-management actions. This mod only reads and displays.

## Forbidden

- Do not add any guild-management write path (invite, kick, rank change, recruit, raid start) — that is the mod's entire reason for existing as "read-only."
- Do not add a Harmony patch or any code that writes to an Erenshor save.
- Do not invent the shape of native guild types; if reflection can't find the expected member, degrade gracefully and report a diagnostic string instead of guessing.
- Do not commit `bin/`, `obj/`, `refs/`, compiled DLLs, game assemblies, or anything under a live BepInEx/Erenshor install path. `.gitignore` already covers the standard cases.
- No secrets, personal file paths, tokens, or real names in source, docs, or commit messages.
- Do not commit or push changes unrelated to the task at hand.

## Important source files

- `src/GuildReader.cs` — reflection-based native guild roster access; the fail-closed boundary lives here.
- `src/GuildLifeCore.cs` — join/leave detection, bulletin bounding logic.
- `src/GuildStore.cs` — local sidecar persistence (`BepInEx/config/ErenshorGuildLife/`).
- `src/GuildLifeApi.cs` — the public `PostVerifiedEvent(...)` surface for other mods.
- `src/GuildWindow.cs`, `src/GuildLauncher.cs` — UI.
- `src/GuildModels.cs` — data shapes.
- `src/ErenshorGuildLifePlugin.cs` — BepInEx plugin entry point.

## Build / test procedure

- Deterministic core tests: `powershell -ExecutionPolicy Bypass -File .\RUN_TESTS.ps1` (standalone `csc` compile + run, no game/BepInEx dependency).
- Full plugin build: `powershell -ExecutionPolicy Bypass -File .\BUILD_AND_INSTALL.ps1` — locates the current Erenshor/BepInEx install and **installs over the live plugin folder**. Don't use it as a plain compile check.
- The shipped build compiles with the legacy .NET Framework `csc.exe` (effectively C# 5) despite the `.csproj` claiming `LangVersion 7.3`. Avoid string interpolation, `nameof`, null-conditional operators, expression-bodied members, and inline `out` variables.
- Compile and run the deterministic tests before claiming a change works.

## Compatibility boundaries

- Does not alter guild nameplates and does not add guild-management commands (that overlaps existing public mods — see `MOD_OVERLAP_NOTES.md`).
- Deep Sims may optionally react to a verified bulletin event but must not become an authority on guild history through this mod.
- Other deterministic mods (Crafting, PvP) may post verified guild-related facts via the API; Guild Life never originates those facts itself.
