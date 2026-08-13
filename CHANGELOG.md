# Changelog

## Unreleased (native Lunaris migration)

- Converted the plugin host from BepInEx (`BaseUnityPlugin`/`[BepInPlugin]`/`[BepInProcess]`) to
  native Lunaris (`LunarisPlugin`/`[LunarisPlugin]`/`[LunarisPermission(FileAccess | Reflection)]`).
  No Harmony or Network permission requested — this mod patches no game methods and makes no
  network calls (no chat-command interception either, UI-button-only, no global hotkey).
- Config replaced `ConfigEntry<T>`/`Config.Bind` with native typed Lunaris config
  (`GuildLifeSettings`); all 8 existing settings (section/key/default/description) preserved
  unchanged behind a loader-neutral `GuildLifeConfigEntry<T>` shim.
- Logging replaced `BepInEx.Logging`/`ManualLogSource` with native Lunaris `Logging`.
- Local bulletin storage moved from `BepInEx/config/ErenshorGuildLife/` to
  `plugins/config/ErenshorGuildLife/` (`Paths.ConfigPath` was BepInEx-specific).
- `BUILD_AND_INSTALL.ps1`/`UNINSTALL.ps1` now target `<Erenshor>\plugins` instead of a BepInEx
  profile and no longer require `BepInEx.dll`.

## 0.1.0 - Preview foundation

- Added read-only native guild roster discovery through reflection.
- Added member zone/level display when native tracking exposes those fields.
- Added draggable no-hotkey Guild Life launcher and resizable window.
- Added bounded local Guild Bulletin.
- Added verified same-guild roster join/leave detection.
- Added reflection-friendly `GuildLifeApi.PostVerifiedEvent`.
- Added local sidecar persistence with backup/corrupt-file recovery.
- Added deterministic core tests.
- Deliberately excluded every native guild-management action.
