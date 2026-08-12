# Erenshor Guild Life 0.1.0 Preview - acceptance checklist

## Build

- [ ] `RUN_TESTS.ps1` reports PASS.
- [ ] `BUILD_AND_INSTALL.ps1` compiles and installs.
- [ ] No Harmony or `Assembly-CSharp.dll` compile-time reference is required.

## UI

- [ ] `GUILD LIFE` launcher appears in ordinary gameplay.
- [ ] No F-key/global hotkey is registered.
- [ ] Launcher and main window drag correctly.
- [ ] Main window resizes with the lower-right `//` grip.
- [ ] Positions remain onscreen after resolution changes.

## Native roster

While in a guild:
- [ ] Roster resolves the correct guild name.
- [ ] Player is not mistaken for a different guild.
- [ ] Native member names match the Guild Manager.
- [ ] Zone is shown only where native tracking exposes it.
- [ ] Unknown zone/level remains blank/unknown rather than guessed.

While not in a guild:
- [ ] UI says no verified player guild.
- [ ] No other guild is selected just because Sims belong to it.

During startup/zoning:
- [ ] Temporary missing managers produce a fail-closed diagnostic, not an exception.
- [ ] Roster recovers on the next refresh.

## Roster change bulletin

- [ ] With `RecordRosterChanges=true`, a real same-guild roster join is recorded once.
- [ ] A real same-guild roster departure is recorded once.
- [ ] Switching guild identity does not fabricate every old member as leaving and every new member as joining.
- [ ] Reloading the mod does not invent roster changes from an empty baseline.

## External bulletin API

- [ ] `PostVerifiedEvent` works through reflection.
- [ ] Exact repeats inside 10 seconds are suppressed.
- [ ] Later genuinely separate events are retained.
- [ ] Bulletin remains bounded to 200 entries.
- [ ] Clearing the bulletin does not touch native guild state.

## Safety

- [ ] No invites/kicks/rank changes.
- [ ] No recruitment.
- [ ] No guild quest/raid control.
- [ ] No movement/combat.
- [ ] No save-file writes.
- [ ] No network.
- [ ] No AI-generated facts.
