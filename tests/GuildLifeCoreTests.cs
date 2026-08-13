using System;
using System.IO;
using ErenshorGuildLife;

internal static class GuildLifeCoreTests
{
    private static int _assertions;

    public static int Main()
    {
        try
        {
            TestRosterDiff();
            TestGuildChangeDoesNotFakeDelta();
            TestBulletinDuplicateSuppression();
            TestBulletinBound();
            TestCharacterKeyWithSlot();
            TestCharacterKeyWithoutSlot();
            TestCharacterKeySanitizesUnsafeCharacters();
            TestLegacyClaimImportsOnce();
            TestLegacyClaimSkippedWhenNoLegacyFile();
            TestLegacyClaimNeverOverwritesExistingCharacterData();
            Console.WriteLine("PASS Erenshor Guild Life core - " + _assertions.ToString() + " assertions");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL Erenshor Guild Life core: " + ex.Message);
            return 1;
        }
    }

    private static void TestCharacterKeyWithSlot()
    {
        string key = GuildLifeCore.ComposeCharacterKey("Aveline", 2);
        Equal("slot2_aveline", key, "slot-qualified key");
    }

    private static void TestCharacterKeyWithoutSlot()
    {
        string key = GuildLifeCore.ComposeCharacterKey("Aveline", -1);
        Equal("aveline", key, "name-only key when slot unresolved");
    }

    private static void TestCharacterKeySanitizesUnsafeCharacters()
    {
        string key = GuildLifeCore.SafeCharacterKey("We!rd Name-42");
        True(key.IndexOf('!') < 0 && key.IndexOf(' ') < 0 && key.IndexOf('-') < 0, "unsafe characters replaced");
        Equal("we_rd_name_42", key, "sanitized key");
    }

    private static void TestLegacyClaimImportsOnce()
    {
        string root = Path.Combine(Path.GetTempPath(), "GuildLifeTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            string legacy = Path.Combine(root, "bulletin.dat");
            string marker = Path.Combine(root, "bulletin.dat.claimed");
            string targetA = Path.Combine(root, "Characters", "a", "bulletin.dat");
            string targetB = Path.Combine(root, "Characters", "b", "bulletin.dat");
            File.WriteAllText(legacy, "ERENSHOR_GUILD_LIFE_V1");

            True(LegacyBulletinClaim.TryClaim(legacy, marker, targetA), "first character claims legacy data");
            True(File.Exists(targetA), "legacy data copied to first character");
            True(File.Exists(legacy), "legacy file preserved, not deleted");
            True(File.Exists(marker), "claim marker written");

            True(!LegacyBulletinClaim.TryClaim(legacy, marker, targetB), "second character cannot claim");
            True(!File.Exists(targetB), "second character starts fresh, no import");
        }
        finally { TryDelete(root); }
    }

    private static void TestLegacyClaimSkippedWhenNoLegacyFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "GuildLifeTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            string legacy = Path.Combine(root, "bulletin.dat");
            string marker = Path.Combine(root, "bulletin.dat.claimed");
            string target = Path.Combine(root, "Characters", "a", "bulletin.dat");
            True(!LegacyBulletinClaim.TryClaim(legacy, marker, target), "no legacy file means no claim");
            True(!File.Exists(target), "nothing imported");
        }
        finally { TryDelete(root); }
    }

    private static void TestLegacyClaimNeverOverwritesExistingCharacterData()
    {
        string root = Path.Combine(Path.GetTempPath(), "GuildLifeTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            string legacy = Path.Combine(root, "bulletin.dat");
            string marker = Path.Combine(root, "bulletin.dat.claimed");
            string target = Path.Combine(root, "Characters", "a", "bulletin.dat");
            File.WriteAllText(legacy, "ERENSHOR_GUILD_LIFE_V1");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, "already has its own data");

            True(!LegacyBulletinClaim.TryClaim(legacy, marker, target), "existing character data blocks claim");
            Equal("already has its own data", File.ReadAllText(target), "existing character data left untouched");
        }
        finally { TryDelete(root); }
    }

    private static void TryDelete(string root)
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
    }

    private static void TestRosterDiff()
    {
        GuildSnapshot a = Guild("Test", "A", "B");
        GuildSnapshot b = Guild("Test", "B", "C");
        GuildRosterDelta delta = GuildLifeCore.DiffRosters(a, b);
        Equal(1, delta.Joined.Count, "one joined");
        Equal("C", delta.Joined[0], "C joined");
        Equal(1, delta.Left.Count, "one left");
        Equal("A", delta.Left[0], "A left");
    }

    private static void TestGuildChangeDoesNotFakeDelta()
    {
        GuildRosterDelta delta = GuildLifeCore.DiffRosters(Guild("One", "A"), Guild("Two", "B"));
        Equal(0, delta.Joined.Count, "guild switch no join inference");
        Equal(0, delta.Left.Count, "guild switch no leave inference");
    }

    private static void TestBulletinDuplicateSuppression()
    {
        GuildLifeDocument doc = new GuildLifeDocument();
        DateTime now = DateTime.UtcNow;
        True(GuildLifeCore.AppendBulletin(doc, now, "PvP", "Victory", "A", "Won."), "first append");
        True(!GuildLifeCore.AppendBulletin(doc, now.AddSeconds(2), "PvP", "Victory", "A", "Won."), "short duplicate suppressed");
        True(GuildLifeCore.AppendBulletin(doc, now.AddSeconds(20), "PvP", "Victory", "A", "Won."), "later event admitted");
    }

    private static void TestBulletinBound()
    {
        GuildLifeDocument doc = new GuildLifeDocument();
        DateTime now = DateTime.UtcNow;
        for (int i = 0; i < GuildLifeCore.MaxBulletinEntries + 15; i++)
            GuildLifeCore.AppendBulletin(doc, now.AddSeconds(i * 20), "T", "C", "A", "event " + i.ToString());
        Equal(GuildLifeCore.MaxBulletinEntries, doc.Bulletin.Count, "bulletin bounded");
        True(doc.Bulletin[0].Text.IndexOf("event 15", StringComparison.Ordinal) >= 0, "oldest trimmed");
    }

    private static GuildSnapshot Guild(string name, params string[] members)
    {
        GuildSnapshot value = new GuildSnapshot();
        value.RuntimeAvailable = true;
        value.InGuild = true;
        value.GuildName = name;
        for (int i = 0; i < members.Length; i++)
        {
            GuildMemberSnapshot member = new GuildMemberSnapshot();
            member.Name = members[i];
            value.Members.Add(member);
        }
        return value;
    }

    private static void True(bool value, string label)
    {
        _assertions++;
        if (!value) throw new Exception(label);
    }

    private static void Equal<T>(T expected, T actual, string label)
    {
        _assertions++;
        if (!object.Equals(expected, actual))
            throw new Exception(label + " expected=" + expected + " actual=" + actual);
    }
}
