# Erenshor Guild Life 0.1.0 Preview

A small read-only guild-presence and verified bulletin layer for Erenshor.

The goal is not to replace Erenshor's Guild Manager. The goal is to make guild membership feel like something that exists **between** raid starts, invites, and roster management.

## What 0.1.0 does

- draggable `GUILD LIFE` HUD button; **no global hotkey**;
- draggable/resizable Party Tools / Follow-style window;
- read-only detection of the player's native guild roster;
- displays member names and, when the native tracking data exposes it, current zone and level;
- records verified same-guild roster joins/leaves observed during the running session;
- keeps a bounded local Guild Bulletin;
- exposes `GuildLifeApi.PostVerifiedEvent(...)` so other mods can post facts they already verified;
- local sidecar persistence under `BepInEx/config/ErenshorGuildLife/`.

## What it deliberately does not do

Guild Life does **not**:

- create a guild;
- invite or kick members;
- alter guild rank;
- recruit Sims;
- start guild quests;
- start raids;
- move or summon Sims;
- send guild chat;
- invent offline activity;
- ask an LLM what happened.

All guild management remains native Erenshor behavior.

## Native data strategy

The current Deep Sims code already demonstrates a read-only guild cache based on:

- `GameData` guild-manager access;
- the native guild collection;
- guild member lists;
- Sim tracking data for guild/scene context.

Guild Life uses the same *conceptual* seam but resolves the types/members through reflection and fails closed if the current build no longer exposes the expected shape.

It does not Harmony-patch any game method and does not write any Erenshor save.

## Bulletin API

Public reflection-friendly method:

```text
GuildLifeApi.PostVerifiedEvent(
    source,
    category,
    actor,
    text
) -> bool
```

Example future uses:

- Crafting: a guildmate/player fills a verified guild provisioning order.
- PvP: a verified guild-related arranged/ambush result.
- Contracts: a verified guild contract completion.
- Deep Sims: may read bulletin facts for social expression, but should not write invented events back as facts.

The caller is responsible for provenance. Guild Life does not parse prose to decide whether an event happened.

## Why this is separate from Deep Sims

Guild Life owns deterministic guild facts/presentation.

Deep Sims can optionally **talk about** a verified bulletin event. It should not own the guild roster, invent guild history, or decide membership.

That preserves the suite rule:

> Erenshor determines what happened.  
> Deterministic mods own bounded systems.  
> Deep Sims decides whether a social reaction is appropriate.  
> Templates/LLM decide how it is expressed.

## Overlap boundary

The current public Erenshor mod scene includes older guild-nameplate customization and ErenshorQoL guild commands. Guild Life does not alter nameplates and does not add guild-management commands.

It is a read-only roster/bulletin layer.

## Build / install

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD_AND_INSTALL.ps1
```

This Preview references only BepInEx + stable Unity assemblies at compile time. Native Erenshor guild objects are discovered through reflection at runtime.

## Testing

```powershell
powershell -ExecutionPolicy Bypass -File .\RUN_TESTS.ps1
```

Then follow `TESTING.md`.

## Development note

This project has been developed heavily with AI-assisted coding tools. The goal is to build features I wanted to use in Erenshor, with development guided through design, testing, playtesting, audits, and iteration against the game. Bug reports, code review, corrections, and contributions from experienced Erenshor modders are welcome.

This is an unofficial, community-made mod for Erenshor and is not affiliated with or endorsed by the game's developer.
