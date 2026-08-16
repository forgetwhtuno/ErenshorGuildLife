# Erenshor Guild Life 0.1.2 Release Candidate

Part of the **Forgotten Roads for Erenshor** mod collection.

A small read-only guild-presence and verified bulletin layer for Erenshor.

The goal is not to replace Erenshor's Guild Manager. The goal is to make guild membership feel like something that exists **between** raid starts, invites, and roster management.

## What 0.1.2 does

- retained-uGUI `GUILD LIFE` launcher with Suite-style drag/fallback visibility; **no global hotkey**;
- retained-uGUI roster/bulletin panel with the Suite dark/translucent/cyan frame, visible `▾`/`▸` collapse + reset/close controls, Suite-style drag, retained resize grip, and scrolling;
- read-only detection of the player's native guild roster;
- displays member names and, when the native tracking data exposes it, current zone and level;
- records verified same-guild roster joins/leaves observed during the running session;
- keeps a bounded local Guild Bulletin with duplicate suppression;
- exposes `GuildLifeApi.PostVerifiedEvent(...)` so other mods can post facts they already verified;
- binds queued external bulletin events to the active character so character switches cannot cross-contaminate local history;
- local per-character sidecar persistence under `plugins/config/ErenshorGuildLife/Characters/<character-key>/` with backup and corrupt-data recovery.

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

This version requires **native Lunaris** — BepInEx is no longer required.

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD_AND_INSTALL.ps1
```

The script locates the current Erenshor install and the Lunaris developer reference, compiles, and installs only `ErenshorGuildLife.dll` to `<Erenshor>\plugins\`. Lunaris manages enable/disable and config; local bulletin state moves to `plugins\config\ErenshorGuildLife\`. Native Erenshor guild objects are discovered through reflection at runtime, unchanged. A legacy BepInEx release remains available in this repository's Git history.

**Status:** 0.1.2 is the release-readiness source candidate. Deterministic coverage includes roster identity/diff rules, no-guild behavior, bulletin bounds/dedupe/persistence/recovery, character keys, legacy claim behavior, launcher geometry, and Suite launcher policy. A native compile and live Lunaris gameplay pass still require the current installed Erenshor/Lunaris reference DLLs.

## Testing

```powershell
powershell -ExecutionPolicy Bypass -File .\RUN_TESTS.ps1
```

Then follow `TESTING.md`.

## Development note

This project has been developed heavily with AI-assisted coding tools. The goal is to build features I wanted to use in Erenshor, with development guided through design, testing, playtesting, audits, and iteration against the game. Bug reports, code review, corrections, and contributions from experienced Erenshor modders are welcome.

This is an unofficial, community-made mod for Erenshor and is not affiliated with or endorsed by the game's developer.


## Optional Suite Hub integration

Forgotten Roads Hub is **optional**. When it is installed, this mod can expose its normal player-facing controls there through the versioned public `GuildLifeControlApi` surface. The mod remains independently usable without the Hub and does not compile against Hub types or assume Hub load order.

Guild Life remains a read-only native-guild companion with its own roster/bulletin panel. A compact standalone launcher is the safety fallback. It hides only when Suite Hub reports Ready with `uiAvailable=true`, this module bridge is registered, and the per-mod **Show launcher** setting is OFF. Missing/unavailable Hub UI forces the launcher visible for recovery; the Hub's manual interaction-validation bit is diagnostic only.

Hub can show concise guild/roster/bulletin status and open or close Guild Life. It does not invite, kick, rank, recruit, or start native guild activities.

The shared control/API and fully-in-world UI policy in this handoff are source-validated but **not yet live-tested under Lunaris hot reload**.

### Content/UI migration candidate

Guild Life uses retained Unity uGUI without changing its authority boundary: native guild state is still read-only and no invite/kick/rank/recruit/quest/raid action is exposed. Hub controls are limited to **Show Guild Life Launcher**, roster-change recording, bounded status, and Open/Close/Reset actions. Native compile and live Lunaris UI/reload verification remain part of the release checklist.
