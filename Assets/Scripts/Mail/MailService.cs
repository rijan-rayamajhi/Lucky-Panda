using System;
using System.Collections.Generic;
using UnityEngine;

// Local mail. Nothing arrives from a server: the game posts to itself when the
// player levels up, finishes a set, reaches a streak or hits a club tier, and
// authored news in Content surfaces on a schedule based on account age.
public static class MailService
{
    public const int MaxMessages = 50;
    const int ClaimedKeepDays = 7;

    static readonly List<MailEntry> Empty = new List<MailEntry>();

    public static List<MailEntry> All =>
        GameState.I != null ? GameState.I.Data.mail : Empty;

    public static void Init()
    {
        GameEvents.Raised -= OnGameEvent;
        GameEvents.Raised += OnGameEvent;
        Prune();
    }

    static void OnGameEvent(GameEvent e)
    {
        switch (e.Type)
        {
            case GameEventType.LevelUp:
                SendLevelUp((int)e.Amount);
                break;
            case GameEventType.Login:
                SendDueNews();
                SendStreak((int)e.Amount);
                break;
            case GameEventType.DayRollover:
                PayClubStipend();
                Prune();
                break;
            case GameEventType.ClubTierUp:
                SendTierUp((int)e.Amount);
                break;
        }
    }

    // ---- posting -------------------------------------------------------

    public static bool Has(string id)
    {
        var list = All;
        for (int i = 0; i < list.Count; i++)
            if (list[i].id == id) return true;
        return false;
    }

    public static MailEntry Send(string id, string sender, string title, string body,
                                 Reward reward, int expiryDays = 30)
    {
        var s = GameState.I;
        if (s == null) return null;
        if (!string.IsNullOrEmpty(id) && Has(id)) return null;

        if (string.IsNullOrEmpty(id))
            id = "m" + (++s.Data.mailSerial);

        var entry = new MailEntry
        {
            id = id,
            sender = string.IsNullOrEmpty(sender) ? "LUCKY PANDA" : sender,
            title = title,
            body = body,
            reward = reward,
            sentUtc = DateTime.UtcNow.ToString("o"),
            expiryDays = expiryDays
        };

        All.Insert(0, entry);
        Prune();
        s.Save();
        Notifications.Bump();
        return entry;
    }

    static void SendLevelUp(int level)
    {
        Send("lvl_" + level, "LUCKY PANDA", "LEVEL " + level + "!",
            "Nicely played. Rewards get bigger as you climb — keep going.",
            Reward.Of(coins: 25_000L * level, gems: 5, cardPacks: level % 5 == 0 ? 2 : 1));
    }

    static void SendStreak(int streak)
    {
        var reward = Content.StreakReward(streak);
        if (reward.IsEmpty) return;
        Send("streak_" + streak, "LUCKY PANDA", streak + " DAY STREAK!",
            "You have played " + streak + " days in a row. Here is a thank you.",
            reward);
    }

    static void SendTierUp(int tier)
    {
        if (tier <= 0 || tier >= Content.ClubTiers.Length) return;
        var def = Content.ClubTiers[tier];
        if (def.tierUpReward.IsEmpty) return;
        Send("tier_" + tier, "PANDA CLUB", def.tierName + " CLUB",
            "Welcome to " + def.tierName + ". Your wheel rewards and daily gems just went up.",
            def.tierUpReward);
    }

    static void SendDueNews()
    {
        int age = DailyService.AccountAgeDays();
        var news = Content.News;
        for (int i = 0; i < news.Length; i++)
        {
            if (news[i].afterDays > age) continue;
            Send(news[i].id, news[i].sender, news[i].title, news[i].body, news[i].reward);
        }
    }

    static void PayClubStipend()
    {
        int gems = ClubService.DailyGemStipend;
        if (gems <= 0) return;
        // Delivered as mail rather than granted silently, so a skipped day is
        // still collectable and the player can see where it came from.
        Send("stipend_" + DailyService.Today, "PANDA CLUB", "DAILY CLUB GEMS",
            ClubService.Current.tierName + " members collect gems every day.",
            Reward.Of(gems: gems), expiryDays: 3);
    }

    // ---- reading and claiming -----------------------------------------

    public static void MarkRead(MailEntry entry)
    {
        if (entry == null || entry.read) return;
        entry.read = true;
        if (GameState.I != null) GameState.I.Save();
        Notifications.Bump();
    }

    public static bool Claim(MailEntry entry)
    {
        if (entry == null || entry.claimed || entry.reward.IsEmpty) return false;
        entry.claimed = true;
        entry.read = true;
        RewardService.Grant(entry.reward, RewardSource.Mail, entry.title);
        GameEvents.Raise(GameEventType.MailClaimed, 1);
        if (GameState.I != null) GameState.I.Save();
        return true;
    }

    public static int ClaimAll()
    {
        var list = All;
        int claimed = 0;
        // Copy first: claiming grants rewards, which can post new mail.
        var pending = new List<MailEntry>();
        for (int i = 0; i < list.Count; i++)
            if (list[i].HasReward) pending.Add(list[i]);

        for (int i = 0; i < pending.Count; i++)
            if (Claim(pending[i])) claimed++;

        return claimed;
    }

    public static int UnreadCount
    {
        get
        {
            var list = All;
            int n = 0;
            for (int i = 0; i < list.Count; i++) if (!list[i].read) n++;
            return n;
        }
    }

    public static int UnclaimedCount
    {
        get
        {
            var list = All;
            int n = 0;
            for (int i = 0; i < list.Count; i++) if (list[i].HasReward) n++;
            return n;
        }
    }

    public static int AttentionCount => Mathf.Max(UnreadCount, UnclaimedCount);

    // ---- housekeeping --------------------------------------------------

    // The whole save is one PlayerPrefs string and message bodies are the only
    // unbounded field in it, so the cap is load-bearing rather than cosmetic.
    public static void Prune()
    {
        var s = GameState.I;
        if (s == null) return;
        var list = All;
        var now = DateTime.UtcNow;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            var m = list[i];
            if (!DailyService.TryParseUtc(m.sentUtc, out var sent)) continue;

            bool expired = m.expiryDays > 0 && (now - sent).TotalDays > m.expiryDays;
            bool staleClaimed = m.claimed && (now - sent).TotalDays > ClaimedKeepDays;

            // An expired message with an unclaimed reward still goes: that is
            // what the expiry is for. Anything else would make it meaningless.
            if (expired || staleClaimed) list.RemoveAt(i);
        }

        // Oldest claimed first, then oldest overall.
        while (list.Count > MaxMessages)
        {
            int victim = -1;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].claimed) { victim = i; break; }
            if (victim < 0) victim = list.Count - 1;
            list.RemoveAt(victim);
        }

        s.Save();
    }
}
