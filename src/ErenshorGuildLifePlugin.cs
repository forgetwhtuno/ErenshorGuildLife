using System;
using System.IO;
using Lunaris;
using Lunaris.Config;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorGuildLife
{
    [LunarisPlugin(PluginGuid, PluginVersion, "forgetwhtuno",
        "Read-only guild-presence and verified bulletin layer. Erenshor remains authoritative for guild membership/state.")]
    [LunarisPermission(LunarisPermission.FileAccess | LunarisPermission.Reflection)]
    public sealed class ErenshorGuildLifePlugin : LunarisPlugin
    {
        internal const string PluginGuid = "forgetwhtuno.erenshor.guildlife";
        internal const string PluginName = "Erenshor Guild Life";
        internal const string PluginVersion = "0.1.2";

        internal static ErenshorGuildLifePlugin Instance;
        private bool _initialized;
        private GuildLifeSuiteAuraProvider _auraProvider;

        private GuildLifeSettings _settings;
        private GuildLifeConfigEntry<float> _launcherX;
        private GuildLifeConfigEntry<float> _launcherY;
        private GuildLifeConfigEntry<bool> _showStandaloneLauncherWithHub;
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
        private bool _open;
        private double _panelActivatedAt;
        private bool _pendingToggle;
        private bool _pendingClose;
        private bool _pendingOpen;
        private bool _dirty;
        private float _saveAfter;
        private float _nextRefresh;
        private bool _cursorVisibleBeforeOpen;
        private CursorLockMode _cursorLockBeforeOpen;
        private string _currentScene;

        // Character-scoped bulletin storage. Nothing character-specific (bulletin data or the
        // native guild snapshot) is touched until IsLocalCharacterReady() is verified true.
        private string _dataRoot;
        private string _legacyBulletinPath;
        private string _legacyClaimMarkerPath;
        private string _characterKey = "";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                try { Logging.LogWarning("Erenshor Guild Life duplicate plugin instance ignored."); } catch { }
                enabled = false;
                return;
            }
            Instance = this;
            _initialized = true;
            _settings = new GuildLifeSettings();
            Config.Register(ref _settings);
            InitializeConfigEntries();
            SuiteUiPolicy.InitializeHubPresence(this);

            _dataRoot = Path.Combine(Path.Combine(AppContext.BaseDirectory, "plugins", "config"), "ErenshorGuildLife");
            _legacyBulletinPath = Path.Combine(_dataRoot, "bulletin.dat");
            _legacyClaimMarkerPath = Path.Combine(_dataRoot, "bulletin.dat.claimed");

            _launcher = new GuildLauncher();
            _window = new GuildWindow();
            InitializeRetainedUi();
            _currentScene = CurrentSceneName();

            // Deliberately no bulletin load and no native guild read here: at Awake there is no
            // verified player character yet. Both happen once IsLocalCharacterReady() is true.
            try { _auraProvider = new GuildLifeSuiteAuraProvider(this); }
            catch (Exception ex) { try { Logging.LogInfo("Guild Life Aura provider init failed (" + ex.GetType().Name + ")."); } catch { } }

            Logging.LogInfo(
                "Erenshor Guild Life " + PluginVersion +
                " loaded. The retained GUILD LIFE launcher appears according to Suite fallback policy. " +
                "No global hotkey is registered. Native guild state is read-only; this mod does not invite, kick, rank, recruit, or start guild quests/raids.");
        }

        private static bool IsLocalCharacterReady()
        {
            return SuiteUiPolicy.IsGameplayReady();
        }

        private static string PlayerName()
        {
            try
            {
                string name = GameData.PlayerControl.Myself.MyStats.MyName;
                return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
            }
            catch { return string.Empty; }
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
            string name = PlayerName();
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            return GuildLifeCore.ComposeCharacterKey(name, ResolveSlotIndex());
        }

        private void EnsureCharacter()
        {
            string key = ResolveCharacterKey();
            if (string.IsNullOrWhiteSpace(key))
            {
                if (_characterKey.Length > 0) UnloadCharacter();
                return;
            }
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
            _currentScene = CurrentSceneName();
            RefreshGuild(true);
            if (_window != null) _window.ResetTransientState();
            Logging.LogInfo("Erenshor Guild Life character context is ready.");
        }

        private void LoadCharacterBulletin(string key)
        {
            string targetPath = Path.Combine(Path.Combine(Path.Combine(_dataRoot, "Characters"), key), "bulletin.dat");

            if (LegacyBulletinClaim.TryClaim(_legacyBulletinPath, _legacyClaimMarkerPath, targetPath))
                Logging.LogInfo("Erenshor Guild Life legacy bulletin was imported for the active character.");

            _store = new GuildStore(targetPath);
            string warning;
            _document = _store.Load(out warning);
            if (!string.IsNullOrEmpty(warning))
                Logging.LogWarning("Erenshor Guild Life recovered readable local bulletin data. " + warning);
            _dirty = false;
        }

        private void UnloadCharacter()
        {
            SaveNow();
            if (_open) CloseWindow();
            _snapshot = null;
            _document = null;
            _store = null;
            bool hadCharacter = _characterKey.Length > 0;
            _characterKey = "";
            if (_window != null) _window.ResetTransientState();
            if (hadCharacter)
                Logging.LogInfo("Erenshor Guild Life character context was cleared.");
        }

        private void InitializeConfigEntries()
        {
            _launcherX = new GuildLifeConfigEntry<float>(delegate { return _settings.LauncherX; }, delegate(float v) { _settings.LauncherX = v; });
            _launcherY = new GuildLifeConfigEntry<float>(delegate { return _settings.LauncherY; }, delegate(float v) { _settings.LauncherY = v; });
            _showStandaloneLauncherWithHub = new GuildLifeConfigEntry<bool>(delegate { return _settings.ShowStandaloneLauncherWithHub; }, delegate(bool v) { _settings.ShowStandaloneLauncherWithHub = v; });
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
                bool ready = SuiteUiPolicy.IsGameplayReady();

                if (_pendingOpen)
                {
                    _pendingOpen = false;
                    if (ready) { if (_open) MarkPanelActivated(); else OpenWindow(); }
                }
                if (_pendingClose)
                {
                    _pendingClose = false;
                    CloseWindow();
                }
                if (_pendingToggle)
                {
                    _pendingToggle = false;
                    if (ready) ToggleWindow();
                }

                if (ready) EnsureCharacter();
                else
                {
                    if (_characterKey.Length > 0) UnloadCharacter();
                    SuiteDragHandler.ForceReleaseIfOwned();
                }

                if (ready && _document != null)
                {
                    PendingGuildEvent pending;
                    while (GuildLifeApi.TryDequeue(out pending))
                    {
                        if (pending == null || !string.Equals(pending.CharacterKey, _characterKey, StringComparison.Ordinal)) continue;
                        if (GuildLifeCore.AppendBulletin(_document, pending.TimestampUtc, pending.Source, pending.Category, pending.Actor, pending.Text))
                            MarkDirty();
                    }

                    string scene = CurrentSceneName();
                    if (!string.Equals(scene, _currentScene, StringComparison.Ordinal))
                    {
                        SuiteDragHandler.ForceReleaseIfOwned();
                        _currentScene = scene;
                        RefreshGuild(false);
                    }

                    if (Time.unscaledTime >= _nextRefresh)
                        RefreshGuild(false);
                }

                bool bridgeRegistered = _auraProvider != null && _auraProvider.Registered;
                bool showLauncher = SuiteUiPolicy.ShouldShowStandaloneLauncher(
                    bridgeRegistered,
                    _showStandaloneLauncherWithHub != null && _showStandaloneLauncherWithHub.Value);
                if (_launcher != null) _launcher.Tick(showLauncher, _open);
                if (_window != null) _window.Tick(ready && _open && _document != null, _snapshot, _document, ClearBulletin);

                if (_dirty && Time.unscaledTime >= _saveAfter) SaveNow();
            }
            catch (Exception ex)
            {
                SuiteDragHandler.ForceReleaseIfOwned();
                Logging.LogError("Erenshor Guild Life update failed (" + ex.GetType().Name + ").");
            }
        }

        private void OnDestroy()
        {
            if (!_initialized) return;
            _initialized = false;
            try { if (_auraProvider != null) _auraProvider.Unregister(); } catch { }
            _auraProvider = null;
            try { SaveNow(); } catch { }
            try { SuiteDragHandler.ForceReleaseIfOwned(); } catch { }
            try { GuildLifeApi.ClearPending(); } catch { }
            try { if (_window != null) _window.Dispose(); } catch { }
            try { if (_launcher != null) _launcher.Dispose(); } catch { }
            try { if (_open) RestoreCursor(); } catch { }
            _window = null;
            _launcher = null;
            _document = null;
            _store = null;
            _snapshot = null;
            _characterKey = "";
            SuiteUiPolicy.Reset();
            if (Instance == this) Instance = null;
        }

        private void RefreshGuild(bool initial)
        {
            _nextRefresh = Time.unscaledTime + Mathf.Clamp(_refreshSeconds == null ? 5 : _refreshSeconds.Value, 2, 30);
            GuildSnapshot previous = _snapshot;
            GuildSnapshot current = GuildReader.Read(PlayerName());
            _snapshot = current;

            if (initial || previous == null || _recordRosterChanges == null || !_recordRosterChanges.Value || _document == null) return;

            GuildRosterDelta delta = GuildLifeCore.DiffRosters(previous, current);
            for (int i = 0; i < delta.Joined.Count; i++)
            {
                if (GuildLifeCore.AppendBulletin(_document, DateTime.UtcNow, "Erenshor", "Roster", delta.Joined[i],
                    delta.Joined[i] + " joined the guild roster."))
                    MarkDirty();
            }
            for (int i = 0; i < delta.Left.Count; i++)
            {
                if (GuildLifeCore.AppendBulletin(_document, DateTime.UtcNow, "Erenshor", "Roster", delta.Left[i],
                    delta.Left[i] + " left the guild roster."))
                    MarkDirty();
            }
        }

        private void ClearBulletin()
        {
            if (_document == null || _document.Bulletin.Count == 0) return;
            _document.Bulletin.Clear();
            MarkDirty();
        }

        internal bool ControlPanelOpen { get { return _open; } }
        internal double ControlPanelActivatedAt { get { return _panelActivatedAt; } }
        internal string ControlCharacterKey { get { return _characterKey ?? string.Empty; } }
        internal GuildSnapshot ControlSnapshot { get { return _snapshot; } }
        internal GuildLifeDocument ControlDocument { get { return _document; } }
        internal bool ControlShowStandaloneLauncher { get { return _showStandaloneLauncherWithHub != null && _showStandaloneLauncherWithHub.Value; } }
        internal bool ControlRecordRosterChanges { get { return _recordRosterChanges != null && _recordRosterChanges.Value; } }
        internal int ControlRefreshSeconds { get { return _refreshSeconds == null ? 5 : Mathf.Clamp(_refreshSeconds.Value, 2, 30); } }
        internal void RequestOpenWindow() { _pendingOpen = true; }
        internal void RequestCloseWindow() { _pendingClose = true; }

        internal void SetShowStandaloneLauncher(bool value)
        {
            if (_showStandaloneLauncherWithHub != null) _showStandaloneLauncherWithHub.Value = value;
            try { Config.Save(); } catch { }
        }

        internal void SetRecordRosterChanges(bool value)
        {
            if (_recordRosterChanges != null) _recordRosterChanges.Value = value;
            try { Config.Save(); } catch { }
        }

        internal void ResetLauncherPosition()
        {
            if (_launcher != null) _launcher.ResetPosition();
        }

        internal void ResetWindowPosition()
        {
            if (_window != null) _window.ResetPosition();
        }

        private void InitializeRetainedUi()
        {
            _window.Initialize(_windowX.Value, _windowY.Value, _windowWidth.Value, _windowHeight.Value,
                PersistWindowPosition, PersistWindowSize, RequestCloseWindow, ResetWindowPosition);
            _launcher.Initialize(_launcherX.Value, _launcherY.Value, PersistLauncherPosition,
                delegate { _pendingToggle = true; });
        }

        private void PersistWindowPosition(float x, float y)
        {
            if (_windowX == null || _windowY == null) return;
            _windowX.Value = x;
            _windowY.Value = y;
            try { Config.Save(); } catch { }
        }

        private void PersistWindowSize(float width, float height)
        {
            if (_windowWidth == null || _windowHeight == null) return;
            if (float.IsNaN(width) || float.IsInfinity(width) || float.IsNaN(height) || float.IsInfinity(height)) return;
            _windowWidth.Value = Mathf.Max(GuildWindow.MinimumWidth, width);
            _windowHeight.Value = Mathf.Max(GuildWindow.MinimumHeight, height);
            try { Config.Save(); } catch { }
        }

        private void PersistLauncherPosition(float x, float y)
        {
            if (_launcherX == null || _launcherY == null) return;
            _launcherX.Value = x;
            _launcherY.Value = y;
            try { Config.Save(); } catch { }
        }

        private void ToggleWindow()
        {
            if (_open) CloseWindow();
            else OpenWindow();
        }

        private void OpenWindow()
        {
            if (_open) { MarkPanelActivated(); return; }
            _open = true;
            MarkPanelActivated();
            _cursorVisibleBeforeOpen = Cursor.visible;
            _cursorLockBeforeOpen = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void CloseWindow()
        {
            if (!_open) return;
            SuiteDragHandler.ForceReleaseIfOwned();
            _open = false;
            SaveNow();
            RestoreCursor();
        }

        private void MarkPanelActivated()
        {
            _panelActivatedAt = Time.realtimeSinceStartup;
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
                Logging.LogError("Erenshor Guild Life could not save local bulletin data (" +
                                ex.GetType().Name + ").");
            }
        }

        private static string CurrentSceneName()
        {
            try { return SceneManager.GetActiveScene().name ?? string.Empty; }
            catch { return string.Empty; }
        }
    }
}
