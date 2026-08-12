using System;
using Lunaris.Config;

namespace ErenshorGuildLife
{
    // Loader-neutral ConfigEntry-style shim. Keeping the Value surface makes the Lunaris
    // migration mechanical and lets the existing call sites keep their proven access pattern.
    internal sealed class GuildLifeConfigEntry<T>
    {
        private readonly Func<T> _get;
        private readonly Action<T> _set;

        internal GuildLifeConfigEntry(Func<T> get, Action<T> set)
        {
            _get = get;
            _set = set;
        }

        internal T Value
        {
            get { return _get(); }
            set { _set(value); }
        }
    }

    internal sealed class GuildLifeSettings
    {
        public GuildLifeSettings() { }

        [Config("LauncherX", "UI", "Saved Guild Life launcher X position.")]
        public float LauncherX = -1f;

        [Config("LauncherY", "UI", "Saved Guild Life launcher Y position.")]
        public float LauncherY = -1f;

        [Config("WindowX", "UI", "Saved Guild Life window X position.")]
        public float WindowX = -1f;

        [Config("WindowY", "UI", "Saved Guild Life window Y position.")]
        public float WindowY = -1f;

        [Config("WindowWidth", "UI", "Guild Life window width in pixels.")]
        public float WindowWidth = 680f;

        [Config("WindowHeight", "UI", "Guild Life window height in pixels.")]
        public float WindowHeight = 520f;

        [Config("RefreshSeconds", "Guild", "Read-only native guild roster refresh interval, clamped to 2-30 seconds.")]
        public int RefreshSeconds = 5;

        [Config("RecordRosterChanges", "Guild", "Record verified same-guild roster joins/leaves in the local bulletin.")]
        public bool RecordRosterChanges = true;
    }
}
