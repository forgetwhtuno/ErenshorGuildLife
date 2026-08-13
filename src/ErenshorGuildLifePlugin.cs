using System;
using System.IO;
using Lunaris;
using Lunaris.Config;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorGuildLife
{
    [LunarisPlugin(PluginGuid, PluginVersion, "forgetwhtuno",
        "Read-only guild-presence and verified bulletin layer. Erenshor remains authoritative for guild membership/state.")]
    [LunarisPermission(LunarisPermission.FileAccess | LunarisPermission.Reflection | LunarisPermission.Harmony)]
    public sealed class ErenshorGuildLifePlugin : LunarisPlugin
    {
        internal const string PluginGuid = "forgetwhtuno.erenshor.guildlife";
        internal const string PluginName = "Erenshor Guild Life";
        internal const string PluginVersion = "0.1.1";

        internal static ErenshorGuildLifePlugin Instance;
        private Harmony _harmony;

        private GuildLifeSettings _settings;
        private GuildLifeConfigEntry<float> _launcherX;
        private GuildLifeConfigEntry<float> _launcherY;
        private GuildLifeConfigEntry<float> _windowX;
        private GuildLifeConfigEntry<float> _windowY;
        private GuildLifeConfigEntry<float> _windowWidth;
        private GuildLifeConfigEntry<float> _windowHeight;
        private GuildLifeConfigEntry<int> _refreshSeconds;
        private GuildLifeConfigEntry<bool> _recordRosterChanges;

        private GuildStore _store;
        private GuildLifeDocument _document;
        private GuildLauncher _launcher;
        private GuildWindow _window;
        private GuildSnapshot _snapshot;
        private Rect _launcherRect;
        private Rect _windowRect;
        private bool _open;

        // Open/close state changes are decided in OnGUI (where the click/close signals surface)
        // but only ever performed in Update(). Mutating _open (or calling OpenWindow/CloseWindow)
        // directly inside OnGUI can desynchronize Unity's Layout/Repaint IMGUI passes and make a
        // just-opened window immediately appear to close again - the same bug class
        // ErenshorContracts hit and fixed the same way. OnGUI only ever sets these request flags;
        // Update() consumes them once per frame.
        private bool _pendingToggle;
        private bool _pendingClose;
        private bool _dirty;
        private float _saveAfter;
        private bool _launcherDirty;
        private float _launcherSaveAfter;
        private float _nextRefresh;
        private bool _cursorVisibleBeforeOpen;
        private CursorLockMode _cursorLockBeforeOpen;
        private string _currentScene;
        private bool _loggedDrawEntry;

        // Character-scoped bulletin storage. Guild Life used to load one global bulletin.dat at
        // Awake, before any character existed. Now nothing character-specific (bulletin data, the
        // native guild snapshot) is touched until IsLocalCharacterReady() is verified true.
        private string _dataRoot;
        private string _legacyBulletinPath;
        private string _legacyClaimMarkerPath;
        private string _characterKey = "";

        private void Awake()
        {
            Instance = this;
            _settings = new GuildLifeSettings();
            Config.Register(ref _settings);
            InitializeConfigEntries();

            _dataRoot = Path.Combine(Path.Combine(AppContext.BaseDirectory, "plugins", "config"), "ErenshorGuildLife");
            _legacyBulletinPath = Path.Combine(_dataRoot, "bulletin.dat");
            _legacyClaimMarkerPath = Path.Combine(_dataRoot, "bulletin.dat.claimed");

            _launcher = new GuildLauncher();
            _window = new GuildWindow();
            _launcherRect = ResolveInitialLauncherRect();
            _windowRect = ResolveInitialWindowRect();
            _currentScene = CurrentSceneName();

            // Deliberately no bulletin load and no native guild read here: at Awake there is no
            // verified player character yet (title screen, login, character select can all reach
            // this point). Both happen only once IsLocalCharacterReady() is true, from Update().

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logging.LogInfo(
                "Erenshor Guild Life " + PluginVersion +
                " loaded. Use the draggable GUILD LIFE UI button. No global hotkey is registered. " +
                "Native guild state is read-only; this mod does not invite, kick, rank, recruit, or start guild quests/raids.");
        }

        // Verified player-ready signal (matches Erenshor-Nemesis's NemesisDirector.Ready(), already
        // live-tested there). Scene-name matching alone is not reliable: Erenshor appears to keep a
        // single persistent Unity scene across title/character-select/gameplay, so a scene-name
        // heuristic can't distinguish them. This checks the actual player object instead, and is
        // re-evaluated every frame rather than cached across scene loads.
        private static bool IsLocalCharacterReady()
        {
            try
            {
                return !GameData.InCharSelect && GameData.PlayerControl != null && GameData.PlayerControl.Myself != null &&
                    GameData.PlayerControl.Myself.MyStats != null && GameData.PlayerControl.Myself.gameObject.activeInHierarchy;
            }
            catch { return false; }
        }

        private static string PlayerName()
        {
            try
            {
                string name = GameData.PlayerControl.Myself.MyStats.MyName;
                return string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
            }
            catch { return "Player"; }
        }

        private static int ResolveSlotIndex()
        {
            try
            {
                SaveGameData active = GameData.CurrentCharacterSlot != null ? GameData.CurrentCharacterSlot : GameData.ActiveSaveSlot;
                if (active == null || active.index < 0) return -1;
                string recorded = (active.CharName ?? "").Trim();
                if (recorded.Length > 0 && !string.Equals(recorded, PlayerName(), StringComparison.OrdinalIgnoreCase)) return -1;
                return active.index;
            }
            catch { return -1; }
        }

        private static string ResolveCharacterKey()
        {
            return GuildLifeCore.ComposeCharacterKey(PlayerName(), ResolveSlotIndex());
        }

        // Runs once per frame while ready; a no-op unless the resolved character key changed.
        // Mirrors Erenshor-Nemesis's NemesisDirector.EnsureCharacter() switch sequence: save+close
        // the outgoing character, then load (or legacy-claim then load) the incoming one and refresh
        // its native guild snapshot. Character A's bulletin/snapshot must never leak into B's window.
        private void EnsureCharacter()
        {
            string key = ResolveCharacterKey();
            if (string.Equals(key, _characterKey, StringComparison.Ordinal)) return;

            if (_characterKey.Length > 0)
            {
                SaveNow();
                if (_open) CloseWindow();
            }

            _store = null;
            _document = null;
            _characterKey = key;
            LoadCharacterBulletin(key);
            RefreshGuild(true);
            if (_window != null) _window.ResetTransientState();
            Logging.LogInfo("Erenshor Guild Life character ready. key=" + key);
        }

        private void LoadCharacterBulletin(string key)
        {
            string targetPath = Path.Combine(Path.Combine(Path.Combine(_dataRoot, "Characters"), key), "bulletin.dat");

            // First character to load after the per-character migration may claim (import a copy
            // of) the pre-existing global bulletin.dat exactly once. The legacy file itself is never
            // modified or deleted; every character after the first-claimer starts fresh.
            if (LegacyBulletinClaim.TryClaim(_legacyBulletinPath, _legacyClaimMarkerPath, targetPath))
                Logging.LogInfo("Erenshor Guild Life legacy bulletin claimed by character. key=" + key);

            _store = new GuildStore(targetPath);
            string warning;
            _document = _store.Load(out warning);
            if (!string.IsNullOrEmpty(warning))
                Logging.LogWarning("Erenshor Guild Life recovered from unreadable local data for " + key + ". " + warning);
            _dirty = false;
        }

        // Per user's explicit lifecycle requirement: on character unload, save, clear the native
        // guild snapshot, close the panel, and stop refresh work until another character is ready.
        private void UnloadCharacter()
        {
            SaveNow();
            if (_open) CloseWindow();
            _snapshot = null;
            _document = null;
            _store = null;
            string previousKey = _characterKey;
            _characterKey = "";
            if (previousKey.Length > 0)
                Logging.LogInfo("Erenshor Guild Life character unloaded; native guild snapshot cleared. key=" + previousKey);
        }

        private void InitializeConfigEntries()
        {
            _launcherX = new GuildLifeConfigEntry<float>(delegate { return _settings.LauncherX; }, delegate(float v) { _settings.LauncherX = v; });
            _launcherY = new GuildLifeConfigEntry<float>(delegate { return _settings.LauncherY; }, delegate(float v) { _settings.LauncherY = v; });
            _windowX = new GuildLifeConfigEntry<float>(delegate { return _settings.WindowX; }, delegate(float v) { _settings.WindowX = v; });
            _windowY = new GuildLifeConfigEntry<float>(delegate { return _settings.WindowY; }, delegate(float v) { _settings.WindowY = v; });
            _windowWidth = new GuildLifeConfigEntry<float>(delegate { return _settings.WindowWidth; }, delegate(float v) { _settings.WindowWidth = v; });
            _windowHeight = new GuildLifeConfigEntry<float>(delegate { return _settings.WindowHeight; }, delegate(float v) { _settings.WindowHeight = v; });
            _refreshSeconds = new GuildLifeConfigEntry<int>(delegate { return _settings.RefreshSeconds; }, delegate(int v) { _settings.RefreshSeconds = v; });
            _recordRosterChanges = new GuildLifeConfigEntry<bool>(delegate { return _settings.RecordRosterChanges; }, delegate(bool v) { _settings.RecordRosterChanges = v; });
        }

        private void Update()
        {
            try
            {
                // OnGUI only ever sets request flags; every actual _open mutation happens here,
                // once per frame, before anything else runs.
                if (_pendingClose)
                {
                    _pendingClose = false;
                    bool before = _open;
                    CloseWindow();
                    Logging.LogInfo("Erenshor Guild Life toggle consumed (close). open_before=" + before + " open_after=" + _open);
                }
                if (_pendingToggle)
                {
                    _pendingToggle = false;
                    bool before = _open;
                    ToggleWindow();
                    Logging.LogInfo("Erenshor Guild Life toggle consumed (toggle). open_before=" + before + " open_after=" + _open);
                }

                // Recomputed every frame, never cached across scene loads.
                bool ready = IsLocalCharacterReady();
                if (ready) EnsureCharacter();
                else if (_characterKey.Length > 0) UnloadCharacter();

                if (ready && _document != null)
                {
                    PendingGuildEvent pending;
                    while (GuildLifeApi.TryDequeue(out pending))
                    {
                        if (GuildLifeCore.AppendBulletin(_document, pending.TimestampUtc, pending.Source, pending.Category, pending.Actor, pending.Text))
                            MarkDirty();
                    }

                    string scene = CurrentSceneName();
                    if (!string.Equals(scene, _currentScene, StringComparison.Ordinal))
                    {
                        _currentScene = scene;
                        RefreshGuild(false);
                    }

                    if (Time.unscaledTime >= _nextRefresh)
                        RefreshGuild(false);
                }

                if (_dirty && Time.unscaledTime >= _saveAfter) SaveNow();
                if (_launcherDirty && Time.unscaledTime >= _launcherSaveAfter) PersistLauncherRect();
            }
            catch (Exception ex)
            {
                Logging.LogError("Erenshor Guild Life update failed: " + ex);
            }
        }

        private void OnGUI()
        {
            try
            {
                // Player-ready is the single visibility gate: no launcher, no window, before a real
                // playable character exists. Recomputed every OnGUI call so a character unload mid-
                // session (e.g. logout to character select) closes the panel immediately.
                if (!IsLocalCharacterReady())
                {
                    if (_open) _pendingClose = true;
                    return;
                }

                if (_open && _window != null && _document != null)
                {
                    if (!_loggedDrawEntry)
                    {
                        Logging.LogInfo("Erenshor Guild Life window Draw() entry. key=" + _characterKey);
                        _loggedDrawEntry = true;
                    }
                    _windowRect = ClampWindowRect(_window.Draw(_windowRect, _snapshot, _document, ClearBulletin));
                    if (_window.RequestClose) _pendingClose = true;
                }

                if (_launcher != null)
                {
                    Rect previous = _launcherRect;
                    _launcherRect = ClampLauncherRect(_launcher.Draw(_launcherRect, _open));
                    if (!RectsNearlyEqual(previous, _launcherRect)) MarkLauncherDirty();
                    if (_launcher.RequestToggle)
                    {
                        Logging.LogInfo("Erenshor Guild Life launcher clicked. open_before=" + _open);
                        _pendingToggle = true;
                        Logging.LogInfo("Erenshor Guild Life toggle queued.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.LogError("Erenshor Guild Life UI failed: " + ex);
                if (_open) _pendingClose = true;
            }
        }

        // True while the pointer (already converted to GUI screen-space by the caller) is over
        // the guild window or its launcher button. The click-passthrough Harmony patches below
        // use this so a click on the panel cannot also drop the player's world target or spin
        // the camera.
        internal bool PointerIsOverUi(Vector2 guiPoint)
        {
            if (_open && _windowRect.Contains(guiPoint)) return true;
            if (_launcherRect.Contains(guiPoint)) return true;
            return false;
        }

        private void OnDestroy()
        {
            try { GuildLifeCameraLookPatch.Restore(); } catch { }
            try { SaveNow(); } catch { }
            try { PersistWindowRect(); } catch { }
            try { PersistLauncherRect(); } catch { }
            try { if (_window != null) _window.Dispose(); } catch { }
            try { if (_launcher != null) _launcher.Dispose(); } catch { }
            try { if (_open) RestoreCursor(); } catch { }
            try { if (_harmony != null) _harmony.UnpatchSelf(); } catch { }
            _window = null;
            _launcher = null;
            _document = null;
            _store = null;
            _snapshot = null;
            _characterKey = "";
            if (Instance == this) Instance = null;
        }

        private void RefreshGuild(bool initial)
        {
            _nextRefresh = Time.unscaledTime + Mathf.Clamp(_refreshSeconds == null ? 5 : _refreshSeconds.Value, 2, 30);
            GuildSnapshot previous = _snapshot;
            GuildSnapshot current = GuildReader.Read();
            _snapshot = current;

            if (initial || previous == null || !_recordRosterChanges.Value) return;

            GuildRosterDelta delta = GuildLifeCore.DiffRosters(previous, current);
            for (int i = 0; i < delta.Joined.Count; i++)
            {
                if (GuildLifeCore.AppendBulletin(_document, DateTime.UtcNow, "Erenshor", "Roster", delta.Joined[i],
                    delta.Joined[i] + " joined the verified guild roster."))
                    MarkDirty();
            }
            for (int i = 0; i < delta.Left.Count; i++)
            {
                if (GuildLifeCore.AppendBulletin(_document, DateTime.UtcNow, "Erenshor", "Roster", delta.Left[i],
                    delta.Left[i] + " left the verified guild roster."))
                    MarkDirty();
            }
        }

        private void ClearBulletin()
        {
            if (_document == null || _document.Bulletin.Count == 0) return;
            _document.Bulletin.Clear();
            MarkDirty();
        }

        private void ToggleWindow()
        {
            if (_open) CloseWindow();
            else OpenWindow();
        }

        private void OpenWindow()
        {
            if (_open) return;
            _open = true;
            _cursorVisibleBeforeOpen = Cursor.visible;
            _cursorLockBeforeOpen = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Logging.LogInfo("Erenshor Guild Life window opened. key=" + _characterKey);
        }

        private void CloseWindow()
        {
            if (!_open) return;
            _open = false;
            _loggedDrawEntry = false;
            SaveNow();
            PersistWindowRect();
            RestoreCursor();
            Logging.LogInfo("Erenshor Guild Life window closed. key=" + _characterKey);
        }

        private void RestoreCursor()
        {
            Cursor.visible = _cursorVisibleBeforeOpen;
            Cursor.lockState = _cursorLockBeforeOpen;
        }

        private void MarkDirty()
        {
            _dirty = true;
            _saveAfter = Time.unscaledTime + 0.8f;
        }

        private void MarkLauncherDirty()
        {
            _launcherDirty = true;
            _launcherSaveAfter = Time.unscaledTime + 0.8f;
        }

        private void SaveNow()
        {
            if (_store == null || _document == null) return;
            if (!_dirty && File.Exists(_store.PathOnDisk)) return;
            try
            {
                _store.Save(_document);
                _dirty = false;
            }
            catch (Exception ex)
            {
                _dirty = true;
                _saveAfter = Time.unscaledTime + 5f;
                Logging.LogError("Erenshor Guild Life could not save local bulletin data: " +
                                ex.GetType().Name + ": " + ex.Message);
            }
        }

        private Rect ResolveInitialWindowRect()
        {
            float width = Mathf.Clamp(_windowWidth.Value, 520f, Mathf.Max(520f, Screen.width - 20f));
            float height = Mathf.Clamp(_windowHeight.Value, 360f, Mathf.Max(360f, Screen.height - 20f));
            float x = _windowX.Value < 0f ? (Screen.width - width) * 0.5f : _windowX.Value;
            float y = _windowY.Value < 0f ? (Screen.height - height) * 0.5f : _windowY.Value;
            return ClampWindowRect(new Rect(x, y, width, height));
        }

        private Rect ResolveInitialLauncherRect()
        {
            float x = _launcherX.Value < 0f ? Mathf.Max(0f, Screen.width - GuildLauncher.Width - 18f) : _launcherX.Value;
            float y = _launcherY.Value < 0f ? Mathf.Min(Mathf.Max(8f, 208f), Mathf.Max(0f, Screen.height - GuildLauncher.Height)) : _launcherY.Value;
            return ClampLauncherRect(new Rect(x, y, GuildLauncher.Width, GuildLauncher.Height));
        }

        private static Rect ClampWindowRect(Rect rect)
        {
            float maxWidth = Mathf.Max(520f, Screen.width - 20f);
            float maxHeight = Mathf.Max(360f, Screen.height - 20f);
            rect.width = Mathf.Clamp(rect.width, 520f, maxWidth);
            rect.height = Mathf.Clamp(rect.height, 360f, maxHeight);
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            return rect;
        }

        private static Rect ClampLauncherRect(Rect rect)
        {
            rect.width = GuildLauncher.Width;
            rect.height = GuildLauncher.Height;
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            return rect;
        }

        private void PersistWindowRect()
        {
            if (_windowX == null || _windowY == null || _windowWidth == null || _windowHeight == null) return;
            Rect rect = ClampWindowRect(_windowRect);
            _windowX.Value = rect.x;
            _windowY.Value = rect.y;
            _windowWidth.Value = rect.width;
            _windowHeight.Value = rect.height;
            Config.Save();
        }

        private void PersistLauncherRect()
        {
            if (_launcherX == null || _launcherY == null) return;
            Rect rect = ClampLauncherRect(_launcherRect);
            _launcherX.Value = rect.x;
            _launcherY.Value = rect.y;
            Config.Save();
            _launcherDirty = false;
        }

        private static string CurrentSceneName()
        {
            try { return SceneManager.GetActiveScene().name ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static bool RectsNearlyEqual(Rect a, Rect b)
        {
            return Mathf.Abs(a.x - b.x) < 0.25f &&
                   Mathf.Abs(a.y - b.y) < 0.25f &&
                   Mathf.Abs(a.width - b.width) < 0.25f &&
                   Mathf.Abs(a.height - b.height) < 0.25f;
        }
    }

    // IMGUI doesn't own the raw click Erenshor reads here, so a click on the Guild Life window or
    // its launcher would otherwise also affect the world (deselect target, move camera).
    [HarmonyPatch(typeof(PlayerControl), "LeftClick")]
    internal static class GuildLifePanelLeftClickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            try
            {
                if (ErenshorGuildLifePlugin.Instance == null) return true;
                Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                return !ErenshorGuildLifePlugin.Instance.PointerIsOverUi(mouse);
            }
            catch { return true; }
        }
    }

    [HarmonyPatch(typeof(csMouseOrbit), "LateUpdate")]
    internal static class GuildLifeCameraLookPatch
    {
        private static csMouseOrbit _muted;
        private static float _mutedX;
        private static float _mutedY;

        internal static void Restore()
        {
            csMouseOrbit orbit = _muted;
            _muted = null;
            if (orbit == null) return;
            try { orbit.xSpeed = _mutedX; orbit.ySpeed = _mutedY; } catch { }
        }

        [HarmonyPrefix]
        private static void Prefix(csMouseOrbit __instance)
        {
            Restore();
            try
            {
                if (__instance == null || ErenshorGuildLifePlugin.Instance == null) return;
                Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (!ErenshorGuildLifePlugin.Instance.PointerIsOverUi(mouse)) return;
                _mutedX = __instance.xSpeed;
                _mutedY = __instance.ySpeed;
                __instance.xSpeed = 0f;
                __instance.ySpeed = 0f;
                _muted = __instance;
            }
            catch { }
        }

        [HarmonyPostfix]
        private static void Postfix() { Restore(); }
    }
}
