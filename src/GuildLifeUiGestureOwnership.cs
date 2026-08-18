using System;
using System.Collections.Generic;

namespace ErenshorGuildLife
{
    // Process-local, BCL-only ownership shared with the other suite UI modules. The first suite
    // gesture captures the native value; the final suite owner restores it. Guild Life can therefore
    // never clear a native or sibling owner's GameData.DraggingUIElement claim.
    internal static class GuildLifeUiGestureOwnership
    {
        private const string ProcessOwnersKey = "forgetwhtuno.erenshor.ui.drag.owners.v1";
        private const string ProcessBaselineKey = "forgetwhtuno.erenshor.ui.drag.nativeBaseline.v1";
        private const string ProcessBaselineCapturedKey = "forgetwhtuno.erenshor.ui.drag.nativeBaselineCaptured.v1";
        private const string ProcessOwner = "forgetwhtuno.erenshor.guildlife";
        private static readonly HashSet<object> LocalOwners = new HashSet<object>();

        internal static bool OwnsPointerGesture { get { return LocalOwners.Count > 0; } }

        internal static void Acquire(object owner)
        {
            if (owner == null || LocalOwners.Contains(owner)) { Reassert(); return; }
            bool first = LocalOwners.Count == 0;
            LocalOwners.Add(owner);
            if (first) AcquireProcessOwnership();
            Reassert();
        }

        internal static void Release(object owner)
        {
            if (owner == null || !LocalOwners.Remove(owner)) return;
            if (LocalOwners.Count == 0) ReleaseProcessOwnership();
            else Reassert();
        }

        internal static void Reassert()
        {
            if (LocalOwners.Count == 0) return;
            try { GameData.DraggingUIElement = true; } catch { }
        }

        internal static void ForceRelease()
        {
            bool hadOwner = LocalOwners.Count > 0;
            LocalOwners.Clear();
            if (hadOwner || ProcessContainsOwner()) ReleaseProcessOwnership();
        }

        private static void AcquireProcessOwnership()
        {
            HashSet<string> owners = GetProcessOwners(true);
            if (owners == null) return;
            lock (owners)
            {
                if (owners.Count == 0)
                {
                    bool baseline = false;
                    try { baseline = GameData.DraggingUIElement; } catch { }
                    AppDomain.CurrentDomain.SetData(ProcessBaselineKey, baseline);
                    AppDomain.CurrentDomain.SetData(ProcessBaselineCapturedKey, true);
                }
                owners.Add(ProcessOwner);
            }
            try { GameData.DraggingUIElement = true; } catch { }
        }

        private static void ReleaseProcessOwnership()
        {
            HashSet<string> owners = GetProcessOwners(false);
            if (owners == null) { RestoreBaseline(); return; }
            bool last;
            lock (owners) { owners.Remove(ProcessOwner); last = owners.Count == 0; }
            if (last) RestoreBaseline();
            else { try { GameData.DraggingUIElement = true; } catch { } }
        }

        private static bool ProcessContainsOwner()
        {
            HashSet<string> owners = GetProcessOwners(false);
            if (owners == null) return false;
            lock (owners) { return owners.Contains(ProcessOwner); }
        }

        private static HashSet<string> GetProcessOwners(bool create)
        {
            try
            {
                HashSet<string> owners = AppDomain.CurrentDomain.GetData(ProcessOwnersKey) as HashSet<string>;
                if (owners == null && create)
                {
                    owners = new HashSet<string>(StringComparer.Ordinal);
                    AppDomain.CurrentDomain.SetData(ProcessOwnersKey, owners);
                }
                return owners;
            }
            catch { return null; }
        }

        private static void RestoreBaseline()
        {
            try
            {
                object capturedValue = AppDomain.CurrentDomain.GetData(ProcessBaselineCapturedKey);
                bool captured = capturedValue is bool && (bool)capturedValue;
                object baselineValue = AppDomain.CurrentDomain.GetData(ProcessBaselineKey);
                bool baseline = baselineValue is bool && (bool)baselineValue;
                if (captured) GameData.DraggingUIElement = baseline;
                AppDomain.CurrentDomain.SetData(ProcessBaselineCapturedKey, false);
                AppDomain.CurrentDomain.SetData(ProcessBaselineKey, false);
            }
            catch { }
        }
    }
}
