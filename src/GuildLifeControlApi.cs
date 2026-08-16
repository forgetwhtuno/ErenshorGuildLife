using System;

namespace ErenshorGuildLife
{
    public sealed class GuildLifeControlState
    {
        public bool GameplayReady;
        public bool RuntimeAvailable;
        public bool InGuild;
        public string CharacterKey;
        public string GuildName;
        public int MemberCount;
        public int BulletinCount;
        public bool PanelOpen;
        public string Diagnostic;
    }

    public static class GuildLifeControlApi
    {
        public const int ApiVersion = 1;
        public const string ModuleId = "guildlife";
        public static bool HasDedicatedPanel { get { return true; } }
        public static bool IsPanelOpen { get { return ErenshorGuildLifePlugin.Instance != null && ErenshorGuildLifePlugin.Instance.ControlPanelOpen; } }

        public static GuildLifeControlState GetBasicState()
        {
            GuildLifeControlState state = new GuildLifeControlState();
            state.GameplayReady = SuiteUiPolicy.IsGameplayReady();
            ErenshorGuildLifePlugin plugin = ErenshorGuildLifePlugin.Instance;
            if (plugin == null) return state;
            state.CharacterKey = plugin.ControlCharacterKey;
            state.PanelOpen = plugin.ControlPanelOpen;
            GuildSnapshot snapshot = plugin.ControlSnapshot;
            if (snapshot != null)
            {
                state.RuntimeAvailable = snapshot.RuntimeAvailable;
                state.InGuild = snapshot.InGuild;
                state.GuildName = snapshot.GuildName ?? string.Empty;
                state.MemberCount = snapshot.Members == null ? 0 : snapshot.Members.Count;
                state.Diagnostic = snapshot.Diagnostic ?? string.Empty;
            }
            GuildLifeDocument doc = plugin.ControlDocument;
            state.BulletinCount = doc == null || doc.Bulletin == null ? 0 : doc.Bulletin.Count;
            return state;
        }

        public static string GetStatus()
        {
            GuildLifeControlState s = GetBasicState();
            if (!s.GameplayReady) return "Waiting for character.";
            if (!s.RuntimeAvailable) return "Guild information unavailable.";
            if (!s.InGuild) return "No guild found for this character.";
            string name = string.IsNullOrWhiteSpace(s.GuildName) ? "Guild roster" : s.GuildName;
            return name + ": " + s.MemberCount + " member(s), " + s.BulletinCount + " bulletin entr" + (s.BulletinCount == 1 ? "y" : "ies") + ".";
        }

        public static bool GetShowLauncher()
        {
            ErenshorGuildLifePlugin p = ErenshorGuildLifePlugin.Instance;
            return p != null && p.ControlShowStandaloneLauncher;
        }

        public static bool SetShowLauncher(bool value)
        {
            ErenshorGuildLifePlugin p = ErenshorGuildLifePlugin.Instance;
            if (p == null) return false;
            p.SetShowStandaloneLauncher(value);
            return true;
        }

        public static bool GetRecordRosterChanges()
        {
            ErenshorGuildLifePlugin p = ErenshorGuildLifePlugin.Instance;
            return p != null && p.ControlRecordRosterChanges;
        }

        public static bool SetRecordRosterChanges(bool value)
        {
            ErenshorGuildLifePlugin p = ErenshorGuildLifePlugin.Instance;
            if (p == null) return false;
            p.SetRecordRosterChanges(value);
            return true;
        }

        public static int GetRefreshSeconds()
        {
            ErenshorGuildLifePlugin p = ErenshorGuildLifePlugin.Instance;
            return p == null ? 5 : p.ControlRefreshSeconds;
        }

        public static bool OpenPanel() { ErenshorGuildLifePlugin p = ErenshorGuildLifePlugin.Instance; if (p == null || !SuiteUiPolicy.IsGameplayReady()) return false; p.RequestOpenWindow(); return true; }
        public static bool ClosePanel() { ErenshorGuildLifePlugin p = ErenshorGuildLifePlugin.Instance; if (p == null) return false; p.RequestCloseWindow(); return true; }
        public static bool ResetPanelPosition() { ErenshorGuildLifePlugin p = ErenshorGuildLifePlugin.Instance; if (p == null) return false; p.ResetWindowPosition(); return true; }
        public static bool ResetLauncherPosition() { ErenshorGuildLifePlugin p = ErenshorGuildLifePlugin.Instance; if (p == null) return false; p.ResetLauncherPosition(); return true; }
    }
}
