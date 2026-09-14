using System.Collections.Generic;
using UnityEngine;

// Daily and weekly goals, picked without a server.
//
// The day index seeds the shuffle, so the same day always produces the same
// quests across restarts and reinstalls, and nothing needs storing except each
// quest's progress row.
public static class QuestService
{
    static readonly List<QuestProgress> Empty = new List<QuestProgress>();

    public static List<QuestProgress> Daily =>
        GameState.I != null ? GameState.I.Data.dailyQuests : Empty;

    public static List<QuestProgress> Weekly =>
        GameState.I != null ? GameState.I.Data.weeklyQuests : Empty;

    public static void Init()
    {
        GameEvents.Raised -= OnGameEvent;
        GameEvents.Raised += OnGameEvent;
        EnsureCurrent();
    }

    // ---- selection -----------------------------------------------------

    public static void EnsureCurrent()
    {
        var s = GameState.I;
        if (s == null) return;
        var d = s.Data;

        int today = DailyService.Today;
        int week = DailyService.WeekIndex;
        bool newDay = d.questDayIndex != today;
        bool newWeek = d.questWeekIndex != week;

        int slots = Content.QuestsPerDay + ClubService.ExtraQuestSlots;

        if (newDay)
        {
            d.dailyQuests = Select(today, false, slots);
            d.questDayIndex = today;
        }
        else
        {
            // A club tier gained today adds its slot without disturbing
            // progress already made on the quests already on the board.
            TopUp(d.dailyQuests, today, false, slots);
        }

        if (newWeek)
        {
            d.weeklyQuests = Select(week * 7 + 1, true, 1);
            d.questWeekIndex = week;
        }

        if (newDay)
        {
            // "Open the game" is satisfied by being here. The weekly variant
            // counts distinct days, so it advances once per rollover.
            CompleteGoal(d.dailyQuests, QuestGoal.DailyLogin);
            Advance(d.weeklyQuests, QuestGoal.DailyLogin, 1);
        }

        s.Save();
    }

    static List<QuestDef> Pool(bool weekly)
    {
        var pool = new List<QuestDef>();
        var all = Content.Quests;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].weekly != weekly) continue;
            if (all[i].needsSlotGame && !Content.SlotGameAvailable) continue;
            pool.Add(all[i]);
        }
        return pool;
    }

    static List<QuestDef> SeededOrder(int seed, bool weekly)
    {
        var pool = Pool(weekly);
        var rng = new System.Random(seed);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }
        return pool;
    }

    static List<QuestProgress> Select(int seed, bool weekly, int count)
    {
        var order = SeededOrder(seed, weekly);
        var result = new List<QuestProgress>();
        int take = Mathf.Min(count, order.Count);
        for (int i = 0; i < take; i++)
            result.Add(new QuestProgress { id = order[i].id });
        return result;
    }

    static void TopUp(List<QuestProgress> list, int seed, bool weekly, int count)
    {
        if (list == null || list.Count >= count) return;
        var order = SeededOrder(seed, weekly);
        for (int i = 0; i < order.Count && list.Count < count; i++)
        {
            if (Find(list, order[i].id) != null) continue;
            list.Add(new QuestProgress { id = order[i].id });
        }
    }

    /// Exposed for the self-check: the seeded order is the one piece of
    /// quest logic that must be identical across restarts.
    public static List<string> PreviewSelection(int seed, bool weekly, int count)
    {
        var order = SeededOrder(seed, weekly);
        var ids = new List<string>();
        for (int i = 0; i < Mathf.Min(count, order.Count); i++) ids.Add(order[i].id);
        return ids;
    }

    public static int PoolSize(bool weekly) => Pool(weekly).Count;

    static QuestProgress Find(List<QuestProgress> list, string id)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i].id == id) return list[i];
        return null;
    }

    // ---- progress ------------------------------------------------------

    static void OnGameEvent(GameEvent e)
    {
        switch (e.Type)
        {
            case GameEventType.DayRollover:
                EnsureCurrent();
                break;
            case GameEventType.WheelSpun:
                Advance(QuestGoal.SpinWheel, 1);
                break;
            case GameEventType.SlotSpun:
                Advance(QuestGoal.SlotSpins, 1);
                Advance(QuestGoal.BetTotal, e.Amount);
                break;
            case GameEventType.CoinsWon:
                Advance(QuestGoal.WinCoins, e.Amount);
                break;
            case GameEventType.GemsSpent:
                Advance(QuestGoal.SpendGems, e.Amount);
                break;
            case GameEventType.LevelUp:
                Advance(QuestGoal.LevelUp, 1);
                break;
            case GameEventType.PackOpened:
                Advance(QuestGoal.OpenPacks, 1);
                break;
            case GameEventType.CardObtained:
                Advance(QuestGoal.CollectCards, 1);
                break;
            case GameEventType.SetCompleted:
                Advance(QuestGoal.CompleteSet, 1);
                break;
            case GameEventType.PuzzlePieceEarned:
                Advance(QuestGoal.EarnPuzzlePieces, 1);
                break;
            case GameEventType.MailClaimed:
                Advance(QuestGoal.ClaimMail, 1);
                break;
        }
    }

    static void Advance(QuestGoal goal, long amount)
    {
        if (amount <= 0) return;
        bool changed = Advance(Daily, goal, amount) | Advance(Weekly, goal, amount);
        if (changed && GameState.I != null) GameState.I.Save();
    }

    static bool Advance(List<QuestProgress> list, QuestGoal goal, long amount)
    {
        if (list == null) return false;
        bool changed = false;
        for (int i = 0; i < list.Count; i++)
        {
            var def = Content.Quest(list[i].id);
            if (def == null || def.goal != goal || list[i].claimed) continue;
            if (list[i].progress >= def.target) continue;
            list[i].progress = System.Math.Min(def.target, list[i].progress + amount);
            changed = true;
        }
        return changed;
    }

    static void CompleteGoal(List<QuestProgress> list, QuestGoal goal)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
        {
            var def = Content.Quest(list[i].id);
            if (def == null || def.goal != goal) continue;
            list[i].progress = def.target;
        }
    }

    // ---- claiming ------------------------------------------------------

    public static bool IsComplete(QuestProgress p)
    {
        var def = Content.Quest(p != null ? p.id : null);
        return def != null && p.progress >= def.target;
    }

    public static bool Claim(QuestProgress p)
    {
        if (p == null || p.claimed) return false;
        var def = Content.Quest(p.id);
        if (def == null || p.progress < def.target) return false;

        p.claimed = true;
        if (GameState.I != null) GameState.I.Save();
        RewardService.Grant(def.reward, RewardSource.Quest, def.title);
        GameEvents.Raise(GameEventType.QuestClaimed, 1);
        return true;
    }

    public static int ClaimableCount
    {
        get
        {
            return Count(Daily) + Count(Weekly);

            int Count(List<QuestProgress> list)
            {
                if (list == null) return 0;
                int n = 0;
                for (int i = 0; i < list.Count; i++)
                    if (!list[i].claimed && IsComplete(list[i])) n++;
                return n;
            }
        }
    }
}
