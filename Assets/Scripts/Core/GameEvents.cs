using System;
using UnityEngine;

public enum GameEventType
{
    Login,
    DayRollover,
    WheelSpun,
    SlotSpun,
    CoinsWon,
    CoinsSpent,
    GemsSpent,
    LevelUp,
    PackOpened,
    CardObtained,
    SetCompleted,
    PuzzlePieceEarned,
    PuzzleCompleted,
    MailClaimed,
    QuestClaimed,
    ShopPurchase,
    ClubTierUp
}

public readonly struct GameEvent
{
    public readonly GameEventType Type;
    public readonly long Amount;
    public readonly int Id;

    public GameEvent(GameEventType type, long amount, int id)
    {
        Type = type;
        Amount = amount;
        Id = id;
    }
}

// Every feature that tracks player activity listens here instead of reaching
// into the systems that produce it.
public static class GameEvents
{
    public static event Action<GameEvent> Raised;

    public static void Raise(GameEventType type, long amount = 0, int id = 0)
    {
        Raised?.Invoke(new GameEvent(type, amount, id));
    }

    // With Enter Play Mode Options set to skip domain reload, statics survive
    // between play sessions and stale subscribers fire into destroyed objects.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Raised = null;
    }
}
