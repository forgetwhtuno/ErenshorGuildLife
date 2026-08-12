using System;
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
            Console.WriteLine("PASS Erenshor Guild Life core - " + _assertions.ToString() + " assertions");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL Erenshor Guild Life core: " + ex.Message);
            return 1;
        }
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
