using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ErenshorGuildLife
{
    internal sealed class GuildStore
    {
        private const string Header = "ERENSHOR_GUILD_LIFE_V1";
        private readonly string _path;

        internal GuildStore(string path)
        {
            _path = path;
        }

        internal string PathOnDisk
        {
            get { return _path; }
        }

        internal GuildLifeDocument Load(out string warning)
        {
            warning = string.Empty;
            GuildLifeDocument document = new GuildLifeDocument();
            if (!File.Exists(_path)) return document;

            try
            {
                string[] lines = File.ReadAllLines(_path, Encoding.UTF8);
                if (lines.Length == 0 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
                    throw new InvalidDataException("Unknown Guild Life data format.");

                for (int i = 1; i < lines.Length; i++)
                {
                    string[] parts = lines[i].Split('|');
                    if (parts.Length < 6 || parts[0] != "E") continue;
                    long ticks;
                    if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)) continue;

                    GuildBulletinEntry value = new GuildBulletinEntry();
                    value.TimestampUtc = new DateTime(ticks, DateTimeKind.Utc);
                    value.Source = Decode(parts[2]);
                    value.Category = Decode(parts[3]);
                    value.Actor = Decode(parts[4]);
                    value.Text = Decode(parts[5]);
                    if (!string.IsNullOrWhiteSpace(value.Text)) document.Bulletin.Add(value);
                }

                while (document.Bulletin.Count > GuildLifeCore.MaxBulletinEntries)
                    document.Bulletin.RemoveAt(0);
                return document;
            }
            catch (Exception ex)
            {
                warning = ex.GetType().Name + ": " + ex.Message;
                TryBackupUnreadable();
                return new GuildLifeDocument();
            }
        }

        internal void Save(GuildLifeDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");
            string directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            string temp = _path + ".tmp";
            string backup = _path + ".bak";
            using (StreamWriter writer = new StreamWriter(temp, false, new UTF8Encoding(false)))
            {
                writer.WriteLine(Header);
                for (int i = 0; i < document.Bulletin.Count; i++)
                {
                    GuildBulletinEntry value = document.Bulletin[i];
                    writer.WriteLine(string.Join("|", new string[]
                    {
                        "E",
                        value.TimestampUtc.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture),
                        Encode(value.Source),
                        Encode(value.Category),
                        Encode(value.Actor),
                        Encode(value.Text)
                    }));
                }
            }

            if (File.Exists(_path))
            {
                try { File.Copy(_path, backup, true); } catch { }
                File.Delete(_path);
            }
            File.Move(temp, _path);
        }

        private void TryBackupUnreadable()
        {
            try
            {
                if (!File.Exists(_path)) return;
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                File.Copy(_path, _path + ".corrupt-" + stamp, true);
            }
            catch { }
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
    }

    // Guild Life moved from one global bulletin.dat to a per-character bulletin. That legacy file
    // can already hold real recorded history, so it is never deleted or truncated. Instead, exactly
    // one character - the first to load after this migration - may claim (import a copy of) it. A
    // companion claim-marker file makes the claim permanent and testable: once it exists, no later
    // character can claim the legacy data, and every character after the first-claimer starts fresh.
    internal static class LegacyBulletinClaim
    {
        internal static bool TryClaim(string legacyPath, string claimMarkerPath, string targetPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(legacyPath) || string.IsNullOrWhiteSpace(claimMarkerPath) || string.IsNullOrWhiteSpace(targetPath))
                    return false;
                if (!File.Exists(legacyPath)) return false;
                if (File.Exists(claimMarkerPath)) return false;
                if (File.Exists(targetPath)) return false; // never overwrite an existing character bulletin

                string directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                File.Copy(legacyPath, targetPath, false);

                string markerDirectory = Path.GetDirectoryName(claimMarkerPath);
                if (!string.IsNullOrWhiteSpace(markerDirectory)) Directory.CreateDirectory(markerDirectory);
                File.WriteAllText(claimMarkerPath, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                return true;
            }
            catch { return false; }
        }
    }
}
