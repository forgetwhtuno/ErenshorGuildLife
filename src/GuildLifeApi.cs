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
        private const int MaximumPendingEvents = 256;
        private static readonly Queue<PendingGuildEvent> Pending = new Queue<PendingGuildEvent>();

        public static bool PostVerifiedEvent(string source, string category, string actor, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            PendingGuildEvent value = new PendingGuildEvent();
            value.TimestampUtc = DateTime.UtcNow;
            value.Source = source == null ? string.Empty : source;
            value.Category = category == null ? string.Empty : category;
            value.Actor = actor == null ? string.Empty : actor;
            value.Text = text;

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
    }
}
