using System;
using UnityEngine;

public enum NavTab { Cards, Puzzle, Quests, Wheel, Inbox, Club }

// Aggregates "is there something to collect here" for the bottom-nav badges.
// Pull rather than push: each service already knows its own claimable count, so
// nothing has to remember to notify when it changes.
public static class Notifications
{
    public static event Action Changed;

    public static void Init()
    {
        GameEvents.Raised -= OnGameEvent;
        GameEvents.Raised += OnGameEvent;
    }

    static void OnGameEvent(GameEvent e) => Bump();

    public static void Bump() => Changed?.Invoke();

    public static int CountFor(NavTab tab)
    {
        if (GameState.I == null) return 0;

        switch (tab)
        {
            case NavTab.Quests:
                return QuestService.ClaimableCount;
            case NavTab.Inbox:
                return MailService.AttentionCount;
            case NavTab.Cards:
                return CardService.AttentionCount;
            case NavTab.Puzzle:
                return PuzzleService.IsComplete ? 1 : 0;
            case NavTab.Wheel:
                return DailyService.WheelReady(out _) ? 1 : 0;
            default:
                return 0;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Changed = null;
    }
}
