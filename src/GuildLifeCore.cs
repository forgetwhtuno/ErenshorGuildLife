using System;
using System.Collections.Generic;
using System.Linq;

namespace ErenshorGuildLife
{
    internal static class GuildLifeCore
    {
        internal const int MaxBulletinEntries = 200;

        // Two save slots can hold the same character name, so persistence keys from the verified
        // slot index when the slot's recorded name matches the live character, and from the name
        // alone otherwise. Mirrors the proven pattern from Erenshor-Nemesis's
        // NemesisDirector.ResolveCharacterKey/SafeKey. Kept Unity-free so it is directly testable.
        internal static string ComposeCharacterKey(string playerName, int slotIndex)
        {
            return slotIndex >= 0
                ? "slot" + slotIndex + "_" + SafeCharacterKey(playerName)
                : SafeCharacterKey(playerName);
        }

        internal static string SafeCharacterKey(string value)
        {
            return new string((value ?? "player").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').Take(48).ToArray());
        }

        internal static GuildRosterDelta DiffRosters(GuildSnapshot previous, GuildSnapshot current)
        {
            GuildRosterDelta delta = new GuildRosterDelta();
            if (previous == null || current == null) return delta;
            if (!previous.RuntimeAvailable || !current.RuntimeAvailable) return delta;
            if (!previous.InGuild || !current.InGuild) return delta;
            if (!string.Equals(previous.GuildName, current.GuildName, StringComparison.OrdinalIgnoreCase)) return delta;

            HashSet<string> oldNames = Names(previous.Members);
            HashSet<string> newNames = Names(current.Members);

            foreach (string name in newNames)
                if (!oldNames.Contains(name)) delta.Joined.Add(name);

            foreach (string name in oldNames)
                if (!newNames.Contains(name)) delta.Left.Add(name);

            delta.Joined.Sort(StringComparer.OrdinalIgnoreCase);
            delta.Left.Sort(StringComparer.OrdinalIgnoreCase);
            return delta;
        }

        internal static bool AppendBulletin(GuildLifeDocument document, DateTime utc, string source, string category, string actor, string text)
        {
            if (document == null || string.IsNullOrWhiteSpace(text)) return false;

            GuildBulletinEntry value = new GuildBulletinEntry();
            value.TimestampUtc = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
            value.Source = Clean(source, 64);
            value.Category = Clean(category, 64);
            value.Actor = Clean(actor, 96);
            value.Text = Clean(text, 320);
            if (value.Text.Length == 0) return false;

            // Exact short-window duplicate suppression. Companion mods should still avoid
            // emitting the same semantic event repeatedly.
            for (int i = document.Bulletin.Count - 1; i >= 0 && i >= document.Bulletin.Count - 12; i--)
            {
                GuildBulletinEntry existing = document.Bulletin[i];
                if (string.Equals(existing.Source, value.Source, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.Category, value.Category, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.Actor, value.Actor, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.Text, value.Text, StringComparison.Ordinal) &&
                    Math.Abs((existing.TimestampUtc - value.TimestampUtc).TotalSeconds) <= 10.0)
                    return false;
            }

            document.Bulletin.Add(value);
            while (document.Bulletin.Count > MaxBulletinEntries)
                document.Bulletin.RemoveAt(0);
            return true;
        }

        internal static string Clean(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string clean = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return clean.Length <= max ? clean : clean.Substring(0, max);
        }

        private static HashSet<string> Names(List<GuildMemberSnapshot> members)
        {
            HashSet<string> values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (members == null) return values;
            for (int i = 0; i < members.Count; i++)
            {
                GuildMemberSnapshot member = members[i];
                if (member == null || string.IsNullOrWhiteSpace(member.Name)) continue;
                values.Add(member.Name.Trim());
            }
            return values;
        }
    }
}
