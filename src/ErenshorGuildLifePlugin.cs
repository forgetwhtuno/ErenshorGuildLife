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
        internal const string PluginVersion = "0.1.0";

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
        private bool _dirty;
        private float _saveAfter;
        private bool _launcherDirty;
        private float _launcherSaveAfter;
        private float _nextRefresh;
        private bool _cursorVisibleBeforeOpen;
        private CursorLockMode _cursorLockBeforeOpen;
        private string _currentScene;

        private void Awake()
        {
            _settings = new GuildLifeSettings();
            Config.Register(ref _settings);
            InitializeConfigEntries();

            string dataDirectory = Path.Combine(Path.Combine(AppContext.BaseDirectory, "plugins", "config"), "ErenshorGuildLife");
            _store = new GuildStore(Path.Combine(dataDirectory, "bulletin.dat"));
            string warning;
            _document = _store.Load(out warning);
            if (!string.IsNullOrEmpty(warning))
                Logging.LogWarning("Erenshor Guild Life recovered from unreadable local data. " + warning);

            _launcher = new GuildLauncher();
            _window = new GuildWindow();
            _launcherRect = ResolveInitialLauncherRect();
            _windowRect = ResolveInitialWindowRect();
            _currentScene = CurrentSceneName();
            RefreshGuild(true);

            Logging.LogInfo(
                "Erenshor Guild Life " + PluginVersion +
                " loaded. Use the draggable GUILD LIFE UI button. No global hotkey is registered. " +
                "Native guild state is read-only; this mod does not invite, kick, rank, recruit, or start guild quests/raids.");
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
                if (!IsUsableScene(_currentScene))
                {
                    if (_open) CloseWindow();
                    return;
                }

                if (_open && _window != null)
                {
                    _windowRect = ClampWindowRect(_window.Draw(_windowRect, _snapshot, _document, ClearBulletin));
                    if (_window.RequestClose) CloseWindow();
                }

                if (_launcher != null)
                {
                    Rect previous = _launcherRect;
                    _launcherRect = ClampLauncherRect(_launcher.Draw(_launcherRect, _open));
                    if (!RectsNearlyEqual(previous, _launcherRect)) MarkLauncherDirty();
                    if (_launcher.RequestToggle) ToggleWindow();
                }
            }
            catch (Exception ex)
            {
                Logging.LogError("Erenshor Guild Life UI failed: " + ex);
                if (_open) CloseWindow();
            }
        }

        private void OnDestroy()
        {
            try { SaveNow(); } catch { }
            try { PersistWindowRect(); } catch { }
            try { PersistLauncherRect(); } catch { }
            try { if (_window != null) _window.Dispose(); } catch { }
            try { if (_launcher != null) _launcher.Dispose(); } catch { }
            try { if (_open) RestoreCursor(); } catch { }
            _window = null;
            _launcher = null;
            _document = null;
            _store = null;
            _snapshot = null;
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
        }

        private void CloseWindow()
        {
            if (!_open) return;
            _open = false;
            SaveNow();
            PersistWindowRect();
            RestoreCursor();
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

        private static bool IsUsableScene(string scene)
        {
            if (string.IsNullOrWhiteSpace(scene)) return false;
            string lower = scene.ToLowerInvariant();
            if (lower.IndexOf("title", StringComparison.Ordinal) >= 0 ||
                lower.IndexOf("login", StringComparison.Ordinal) >= 0 ||
                lower.IndexOf("characterselect", StringComparison.Ordinal) >= 0 ||
                lower.IndexOf("mainmenu", StringComparison.Ordinal) >= 0)
                return false;
            return true;
        }

        private static bool RectsNearlyEqual(Rect a, Rect b)
        {
            return Mathf.Abs(a.x - b.x) < 0.25f &&
                   Mathf.Abs(a.y - b.y) < 0.25f &&
                   Mathf.Abs(a.width - b.width) < 0.25f &&
                   Mathf.Abs(a.height - b.height) < 0.25f;
        }
    }
}
