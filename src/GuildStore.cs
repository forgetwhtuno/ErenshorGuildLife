using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ErenshorGuildLife
{
    internal sealed class GuildStore
    {
        private const string Header = "ERENSHOR_GUILD_LIFE_V1";
        private const long MaximumFileBytes = 4L * 1024L * 1024L;
        private readonly string _path;

        internal GuildStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A Guild Life data path is required.", "path");
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
                FileInfo info = new FileInfo(_path);
                if (info.Length > MaximumFileBytes) throw new InvalidDataException("Guild Life data file is unexpectedly large.");

                string[] lines = File.ReadAllLines(_path, Encoding.UTF8);
                if (lines.Length == 0 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
                    throw new InvalidDataException("Unknown Guild Life data format.");

                int skippedRecords = 0;
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    string[] parts = lines[i].Split('|');
                    if (parts.Length < 6 || !string.Equals(parts[0], "E", StringComparison.Ordinal))
                    {
                        skippedRecords++;
                        continue;
                    }

                    long ticks;
                    string source;
                    string category;
                    string actor;
                    string text;
                    if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks) ||
                        ticks <= DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks ||
                        !TryDecode(parts[2], out source) || !TryDecode(parts[3], out category) ||
                        !TryDecode(parts[4], out actor) || !TryDecode(parts[5], out text))
                    {
                        skippedRecords++;
                        continue;
                    }

                    GuildLifeCore.AppendBulletin(document, new DateTime(ticks, DateTimeKind.Utc), source, category, actor, text);
                }

                if (skippedRecords > 0)
                    warning = "Some malformed local bulletin records were ignored; readable entries were preserved.";
                return document;
            }
            catch (Exception ex)
            {
                warning = "The local Guild Life data could not be read and was preserved as a .corrupt backup (" + ex.GetType().Name + ").";
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
                int start = Math.Max(0, document.Bulletin.Count - GuildLifeCore.MaxBulletinEntries);
                for (int i = start; i < document.Bulletin.Count; i++)
                {
                    GuildBulletinEntry value = document.Bulletin[i];
                    if (value == null || string.IsNullOrWhiteSpace(value.Text)) continue;
                    writer.WriteLine(string.Join("|", new string[]
                    {
                        "E",
                        NormalizeUtc(value.TimestampUtc).Ticks.ToString(CultureInfo.InvariantCulture),
                        Encode(GuildLifeCore.Clean(value.Source, 64)),
                        Encode(GuildLifeCore.Clean(value.Category, 64)),
                        Encode(GuildLifeCore.Clean(value.Actor, 96)),
                        Encode(GuildLifeCore.Clean(value.Text, 320))
                    }));
                }
            }

            if (!File.Exists(_path))
            {
                File.Move(temp, _path);
                return;
            }

            try
            {
                File.Replace(temp, _path, backup, true);
            }
            catch
            {
                File.Copy(_path, backup, true);
                File.Copy(temp, _path, true);
                File.Delete(temp);
            }
        }

        private void TryBackupUnreadable()
        {
            try
            {
                if (!File.Exists(_path)) return;
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string corrupt = _path + ".corrupt-" + stamp;
                int suffix = 2;
                while (File.Exists(corrupt))
                {
                    corrupt = _path + ".corrupt-" + stamp + "-" + suffix.ToString(CultureInfo.InvariantCulture);
                    suffix++;
                }
                File.Copy(_path, corrupt, true);
            }
            catch { }
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default(DateTime)) return DateTime.UtcNow;
            if (value.Kind == DateTimeKind.Utc) return value;
            try { return value.ToUniversalTime(); }
            catch { return DateTime.UtcNow; }
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static bool TryDecode(string value, out string decoded)
        {
            decoded = string.Empty;
            if (string.IsNullOrEmpty(value)) return true;
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value));
                return true;
            }
            catch
            {
                decoded = string.Empty;
                return false;
            }
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
                if (File.Exists(targetPath)) return false;

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
