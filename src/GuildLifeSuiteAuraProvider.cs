using System;
using Lunaris;
using Lunaris.IPC;

namespace ErenshorGuildLife
{
    // Thin, optional transport adapter over the public GuildLifeControlApi. No gameplay logic
    // lives here and there is no reference to ErenshorSuiteHub.dll.
    internal sealed class GuildLifeSuiteAuraProvider
    {
        private const string Prefix = "forgetwhtuno.erenshor.suite.guildlife.v1.";

        private IAuraProvider<string> _describe;
        private IAuraProvider<string> _basicSettings;
        private IAuraProvider<string> _advancedSettings;
        private IAuraProvider<string> _uiState;
        private IAuraProvider<string, string, string> _setSetting;
        private IAuraProvider<string, string, string> _action;

        internal bool Registered { get; private set; }

        internal GuildLifeSuiteAuraProvider(LunarisPlugin owner)
        {
            _describe = owner.IPCAuraProvider<string>(Prefix + "describe");
            _describe.RegisterFunc(Describe);
            _basicSettings = owner.IPCAuraProvider<string>(Prefix + "settings.basic");
            _basicSettings.RegisterFunc(BasicSettings);
            _advancedSettings = owner.IPCAuraProvider<string>(Prefix + "settings.advanced");
            _advancedSettings.RegisterFunc(AdvancedSettings);
            _uiState = owner.IPCAuraProvider<string>(Prefix + "ui.state");
            _uiState.RegisterFunc(UiState);
            _setSetting = owner.IPCAuraProvider<string, string, string>(Prefix + "setting.set");
            _setSetting.RegisterFunc(SetSetting);
            _action = owner.IPCAuraProvider<string, string, string>(Prefix + "action");
            _action.RegisterFunc(InvokeAction);
            Registered = true;
        }

        internal void Unregister()
        {
            Registered = false;
            Safe(_describe); _describe = null;
            Safe(_basicSettings); _basicSettings = null;
            Safe(_advancedSettings); _advancedSettings = null;
            Safe(_uiState); _uiState = null;
            Safe(_setSetting); _setSetting = null;
            Safe(_action); _action = null;
        }

        private static void Safe(IAuraProvider p)
        {
            if (p == null) return;
            try { p.UnregisterFunc(); } catch { }
        }

        private string Describe()
        {
            const string actions = "openPanel,closePanel,resetPanel,resetLauncher";
            return "protocol=1"
                + "&module=" + GuildLifeControlApi.ModuleId
                + "&display=" + Uri.EscapeDataString("Guild Life")
                + "&version=" + Uri.EscapeDataString(ErenshorGuildLifePlugin.PluginVersion)
                + "&summary=" + Uri.EscapeDataString("Read-only guild roster and local bulletin.")
                + "&status=" + Uri.EscapeDataString(SuiteUiControlPolicy.BoundStatus(GuildLifeControlApi.GetStatus()))
                + "&actions=" + actions;
        }

        private string UiState()
        {
            ErenshorGuildLifePlugin p = ErenshorGuildLifePlugin.Instance;
            return SuiteUiStatePolicy.Build(GuildLifeControlApi.ModuleId,
                p != null && p.ControlPanelOpen,
                GuildWindow.CanvasSortOrder,
                p == null ? 0d : p.ControlPanelActivatedAt);
        }

        private string BasicSettings()
        {
            return "id=showLauncher&label=" + Uri.EscapeDataString("Show Guild Life Launcher")
                + "&tier=basic&type=bool&value=" + (GuildLifeControlApi.GetShowLauncher() ? "true" : "false")
                + "&mutable=true";
        }

        private string AdvancedSettings()
        {
            return "id=recordRosterChanges&label=" + Uri.EscapeDataString("Record roster changes")
                + "&tier=advanced&type=bool&value=" + (GuildLifeControlApi.GetRecordRosterChanges() ? "true" : "false")
                + "&mutable=true"
                + "\nid=refreshSeconds&label=" + Uri.EscapeDataString("Roster refresh seconds")
                + "&tier=advanced&type=number&value=" + GuildLifeControlApi.GetRefreshSeconds().ToString()
                + "&mutable=false";
        }

        private string SetSetting(string settingId, string value)
        {
            bool parsed;
            if (!SuiteUiControlPolicy.TryParseBool(value, out parsed)) return "invalid value";
            switch (settingId)
            {
                case "showLauncher": return GuildLifeControlApi.SetShowLauncher(parsed) ? "ok" : "rejected";
                case "recordRosterChanges": return GuildLifeControlApi.SetRecordRosterChanges(parsed) ? "ok" : "rejected";
                default: return "unknown setting";
            }
        }

        private string InvokeAction(string actionId, string argument)
        {
            switch (SuiteUiControlPolicy.ParsePanelAction(actionId))
            {
                case SuitePanelAction.OpenPanel: return GuildLifeControlApi.OpenPanel() ? "ok" : "rejected";
                case SuitePanelAction.ClosePanel: return GuildLifeControlApi.ClosePanel() ? "ok" : "rejected";
                case SuitePanelAction.ResetPanel: return GuildLifeControlApi.ResetPanelPosition() ? "ok" : "rejected";
                case SuitePanelAction.ResetLauncher: return GuildLifeControlApi.ResetLauncherPosition() ? "ok" : "rejected";
                default: return "unknown action";
            }
        }
    }
}
