# Changelog

## Unreleased - bounded Suite UI polish

- Aligned Guild Life and its launcher with the canonical dark/translucent/cyan Sim Actions palette and added a thin cyan frame.
- Added a consistent `▾` / `▸` header collapse control. Collapsed Guild Life keeps only the draggable 32px header plus Reset/Close; roster/bulletin content and resize grip are hidden.
- Collapse/expand preserves the header's screen position and clamps both states without changing the read-only guild authority boundary.
- Retained roster level/zone text continues to update existing TMP controls in place; collapse adds no ordinary dynamic-text rebuild.
- Extended Unity-free Suite UI policy tests for compact geometry, collapse/expand heights, top-edge preservation, containment clamp, launcher fallback, and structural-vs-dynamic rebuild behavior.

## 0.1.2 - playable-state / release-readiness

- Kept native guild authority strictly read-only and removed player-facing reflection/diagnostic terminology from the retained roster panel.
- Native guild data is considered available only after the actual Guilds collection resolves; missing/null guild managers now fail closed instead of looking like a proven no-guild state.
- If any native guild object exposes no readable member collection, the snapshot now stays unavailable instead of incorrectly concluding that the active character has no guild.
- Reject duplicate plugin initialization so an abnormal double-start cannot create duplicate retained UI or lifecycle ownership.
- Guild membership matching now uses the plugin's verified active-character name rather than a scene GameObject-name fallback, and no synthetic guild name is invented.
- Roster-change detection prefers native guild IDs when available, preventing same-name/different-guild snapshots from fabricating join/leave history.
- Bound queued external bulletin events to the active character and bounded/sanitized payloads before enqueue.
- Reworked bulletin persistence to replace the live file safely with `.bak` recovery instead of deleting it before moving the temp file; malformed individual records no longer discard readable history.
- Added confirmation/disabled state for Bulletin Clear and screen fitting for small resolutions; normal minimum panel size is now 440x320.
- Removed character keys, local paths, and exception-message detail from normal runtime logs.
- Added deterministic tests for no-guild/different-guild boundaries, persistence/backup, malformed data, payload bounds, and empty character-key fallback.

## Unreleased - Suite panel consistency

- Kept the retained roster/bulletin panel and read-only native guild boundaries unchanged.
- Hub Basic settings now contain only **Show Guild Life launcher**; roster recording/refresh information is under Advanced.
- Added the shared `ui.state` + existing `closePanel` contract for centralized quick-close selection without adding an Escape handler.
- Standalone launcher suppression consumes the Hub presence endpoint and fails safe to visible unless the Hub is Ready, reports `uiAvailable=true`, and this module bridge is registered; the manual interaction-validation bit is diagnostic only.

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


## Unreleased - Suite UI/API coherence handoff

- Added optional, versioned `GuildLifeControlApi` discovery/control surface for Suite Hub without a hard Hub dependency.
- Kept standalone commands and core gameplay authority intact.
- Documented the retained panel/launcher policy and Lunaris live-test requirement.
- Strengthened fully-in-world gating, made the launcher Hub-aware fallback-only, and added panel Reset Position / whole-drag input capture. Native guild authority remains read-only.
## 0.1.3 - Forgotten Roads launcher chrome

- Standardized the standalone retained-uGUI launcher at 154x32 with programmatic grip marks and collection hover/pressed colors.
- Standardized compact header naming while preserving drag, collapse, reset, close, position, and fallback behavior.
