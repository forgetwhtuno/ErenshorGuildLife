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

        private readonly IAuraProvider<string> _describe;
        private readonly IAuraProvider<string> _settings;
        private readonly IAuraProvider<string, string, string> _setSetting;
        private readonly IAuraProvider<string, string, string> _action;

        internal bool Registered { get; private set; }

        internal GuildLifeSuiteAuraProvider(LunarisPlugin owner)
        {
            _describe = owner.IPCAuraProvider<string>(Prefix + "describe");
            _describe.RegisterFunc(Describe);
            _settings = owner.IPCAuraProvider<string>(Prefix + "settings.basic");
            _settings.RegisterFunc(Settings);
            _setSetting = owner.IPCAuraProvider<string, string, string>(Prefix + "setting.set");
            _setSetting.RegisterFunc(SetSetting);
            _action = owner.IPCAuraProvider<string, string, string>(Prefix + "action");
            _action.RegisterFunc(InvokeAction);
            Registered = true;
        }

        internal void Unregister()
        {
            Registered = false;
            try { if (_describe != null) _describe.UnregisterFunc(); } catch { }
            try { if (_settings != null) _settings.UnregisterFunc(); } catch { }
            try { if (_setSetting != null) _setSetting.UnregisterFunc(); } catch { }
            try { if (_action != null) _action.UnregisterFunc(); } catch { }
        }

        private string Describe()
        {
            const string actions = "openPanel,closePanel,resetPanel,resetLauncher";
            return "protocol=1"
                + "&module=" + GuildLifeControlApi.ModuleId
                + "&display=" + Uri.EscapeDataString("Guild Life")
                + "&version=" + Uri.EscapeDataString(ErenshorGuildLifePlugin.PluginVersion)
                + "&summary=" + Uri.EscapeDataString("Read-only verified guild roster and local bulletin view.")
                + "&status=" + Uri.EscapeDataString(SuiteUiControlPolicy.BoundStatus(GuildLifeControlApi.GetStatus()))
                + "&actions=" + actions;
        }

        private string Settings()
        {
            return "id=showLauncher&label=" + Uri.EscapeDataString("Show Guild Life launcher") + "&tier=basic&type=bool&value=" + (GuildLifeControlApi.GetShowLauncher() ? "true" : "false") + "&mutable=true"
                + "\nid=recordRosterChanges&label=" + Uri.EscapeDataString("Record verified roster changes") + "&tier=basic&type=bool&value=" + (GuildLifeControlApi.GetRecordRosterChanges() ? "true" : "false") + "&mutable=true"
                + "\nid=refreshSeconds&label=" + Uri.EscapeDataString("Roster refresh seconds") + "&tier=basic&type=number&value=" + GuildLifeControlApi.GetRefreshSeconds().ToString() + "&mutable=false";
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
