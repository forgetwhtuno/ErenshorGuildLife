using System;
using System.Collections.Generic;

namespace ErenshorGuildLife
{
    internal sealed class GuildMemberSnapshot
    {
        internal string Name;
        internal string Zone;
        internal int Level;
    }

    internal sealed class GuildSnapshot
    {
        internal bool RuntimeAvailable;
        internal bool InGuild;
        internal string PlayerName;
        internal string GuildName;
        internal int GuildId;
        internal string Diagnostic;
        internal readonly List<GuildMemberSnapshot> Members = new List<GuildMemberSnapshot>();
    }

    internal sealed class GuildBulletinEntry
    {
        internal DateTime TimestampUtc;
        internal string Source;
        internal string Category;
        internal string Actor;
        internal string Text;
    }

    internal sealed class GuildLifeDocument
    {
        internal readonly List<GuildBulletinEntry> Bulletin = new List<GuildBulletinEntry>();
    }

    internal sealed class PendingGuildEvent
    {
        internal DateTime TimestampUtc;
        internal string CharacterKey;
        internal string Source;
        internal string Category;
        internal string Actor;
        internal string Text;
    }

    internal sealed class GuildRosterDelta
    {
        internal readonly List<string> Joined = new List<string>();
        internal readonly List<string> Left = new List<string>();
    }
}
