# Erenshor Guild Life 0.1.2 - release-readiness checklist

## Automated deterministic tests

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\RUN_TESTS.ps1
```

Coverage includes roster diff/identity boundaries, no-guild behavior, bulletin bounds/dedupe/persistence/recovery, character keys, one-time legacy claim, launcher geometry, Suite launcher policy, and a source-level read-only authority check.

## Build

- [ ] `RUN_TESTS.ps1` reports PASS.
- [ ] `BUILD_AND_INSTALL.ps1` compiles against the current installed Erenshor `Assembly-CSharp.dll`, Unity assemblies, and Lunaris developer reference, then installs only `ErenshorGuildLife.dll`.
- [ ] No Harmony patch or network permission is introduced.

## UI

- [ ] Expanded Guild Life header shows `▾`; collapse leaves only the ~32px draggable header with Reset and `X`, and `▸` expands back to the prior Roster/Bulletin content.
- [ ] While collapsed, no roster/bulletin body or resize grip renders or accepts input.
- [ ] Drag while collapsed near each screen edge, expand, and confirm the restored window is still clamped on-screen.
- [ ] Roster level/zone text updates retained row controls without reconstructing the roster when membership structure is unchanged.
- [ ] Repeated collapse/expand and hot reload do not create duplicate Canvas/EventSystem roots.


- [ ] `GUILD LIFE` launcher appears in ordinary gameplay according to Suite fallback policy.
- [ ] No F-key/global hotkey is registered.
- [ ] Launcher and main window drag correctly and retain position.
- [ ] Main window resizes with the lower-right grip.
- [ ] At small resolutions the panel shrinks/clamps onscreen rather than overflowing the display.
- [ ] Roster and Bulletin scroll independently where needed; no large blank regions or clipped controls appear.
- [ ] Bulletin Clear is disabled when empty and requires a second confirmation click when populated.
- [ ] Normal player UI contains no reflection diagnostics, PoC/debug labels, or fake action controls.

## Native roster / no-guild state

While in a guild:
- [ ] Roster resolves the correct guild and member names against Erenshor's Guild Manager.
- [ ] Native guild ID/name changes do not cause cross-guild roster deltas.
- [ ] Zone/level are shown only when native tracking exposes them; missing values remain unknown rather than guessed.
- [ ] Panel clearly states that guild actions remain in Erenshor's Guild Manager.

While not in a guild:
- [ ] UI clearly says `NO GUILD FOUND`.
- [ ] No other guild is selected merely because Sims belong to it.

During startup/zoning:
- [ ] Missing/null Guild Manager or Guilds collection produces the temporary unavailable state, not a false proven no-guild state and not an exception.
- [ ] If a guild object exists but its member collection cannot be read, the panel stays temporarily unavailable rather than reporting `NO GUILD FOUND`.
- [ ] Roster recovers on a later refresh when native state becomes available.
- [ ] No active character means no per-character bulletin is loaded or created.

## Roster-change bulletin

- [ ] With `RecordRosterChanges=true`, a real same-guild roster join is recorded once.
- [ ] A real same-guild roster departure is recorded once.
- [ ] Switching guild identity does not fabricate every old member as leaving and every new member as joining.
- [ ] Reloading the mod starts from the current roster baseline and does not invent changes.
- [ ] Bulletin remains bounded to 200 entries.

## External bulletin API / character isolation

- [ ] `GuildLifeApi.PostVerifiedEvent` works through optional reflection when a character context is active.
- [ ] Exact repeats inside the short duplicate window are suppressed.
- [ ] Oversized or control-character payloads are sanitized/bounded.
- [ ] Queue an event immediately before switching characters and verify it never appears in the newly active character's bulletin.
- [ ] Character A and B retain separate bulletin histories under `plugins/config/ErenshorGuildLife/Characters/<character-key>/bulletin.dat`.
- [ ] Clearing the bulletin never touches native guild state.

## Persistence / malformed state

- [ ] Save twice and verify the main bulletin remains readable and `.bak` is maintained.
- [ ] One malformed record in an otherwise valid file is skipped while readable history survives.
- [ ] A fully invalid file is preserved as `.corrupt-*` and Guild Life opens with an empty local bulletin.
- [ ] Legacy global bulletin data can be claimed once without overwriting existing per-character data or leaking to later characters.

## Suite launcher contract

- [ ] Hub absent/unusable: recovery launcher remains visible while a character is active.
- [ ] Hub healthy + Guild Life bridge registered + `Show Guild Life Launcher` OFF: standalone launcher is hidden.
- [ ] Toggling `Show Guild Life Launcher` in MODS updates immediately.
- [ ] `Open Guild Life` opens the dedicated panel and status stays concise.
- [ ] Reset panel/launcher actions remain available without adding a second settings framework.

## Read-only safety boundary

- [ ] No invite, kick, rank, recruit, create/leave, guild-quest, raid-start, summon, movement, chat, or save-file mutation is performed by Guild Life.
- [ ] Native guild state is only reflected/read.
- [ ] No networking or AI-generated guild facts are added.
- [ ] Lunaris unload/reload leaves no duplicate retained UI, stale drag state, pending bulletin event, or stale guild snapshot.
- [ ] If duplicate plugin initialization is forced in a development session, the extra instance is ignored and does not create another canvas/launcher.
