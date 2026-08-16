using System;
using System.Collections.Generic;

namespace ErenshorGuildLife
{
    /// <summary>
    /// Optional reflection-friendly event surface.
    /// The caller must only post facts it has actually verified.
    /// </summary>
    public static class GuildLifeApi
    {
        public const int ContractVersion = 1;
        public static bool IsAvailable { get { return ErenshorGuildLifePlugin.Instance != null; } }

        private const int MaximumPendingEvents = 256;
        private static readonly Queue<PendingGuildEvent> Pending = new Queue<PendingGuildEvent>();

        public static bool PostVerifiedEvent(string source, string category, string actor, string text)
        {
            if (!IsAvailable) return false;
            ErenshorGuildLifePlugin plugin = ErenshorGuildLifePlugin.Instance;
            string characterKey = plugin == null ? string.Empty : plugin.ControlCharacterKey;
            if (string.IsNullOrWhiteSpace(characterKey)) return false;

            string cleanText = GuildLifeCore.Clean(text, 320);
            if (cleanText.Length == 0) return false;

            PendingGuildEvent value = new PendingGuildEvent();
            value.TimestampUtc = DateTime.UtcNow;
            value.CharacterKey = characterKey;
            value.Source = GuildLifeCore.Clean(source, 64);
            value.Category = GuildLifeCore.Clean(category, 64);
            value.Actor = GuildLifeCore.Clean(actor, 96);
            value.Text = cleanText;

            lock (Pending)
            {
                if (Pending.Count >= MaximumPendingEvents) return false;
                Pending.Enqueue(value);
            }
            return true;
        }

        internal static bool TryDequeue(out PendingGuildEvent value)
        {
            lock (Pending)
            {
                if (Pending.Count == 0)
                {
                    value = null;
                    return false;
                }
                value = Pending.Dequeue();
                return true;
            }
        }

        internal static void ClearPending()
        {
            lock (Pending) Pending.Clear();
        }
    }
}
