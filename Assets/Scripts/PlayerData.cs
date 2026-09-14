using System;
using System.Collections.Generic;

// Everything that must survive a restart lives here and nowhere else, so
// moving to a cloud backend later means swapping the storage in GameState
// rather than hunting fields across the project.
//
// JsonUtility fills absent value fields with their defaults, so a save written
// by an older build loads cleanly. Absent List<T> and array fields come back
// null rather than empty, which is what GameState.Sanitize() guards.
[Serializable]
public class PlayerData
{
    public int schemaVersion = 2;

    public string displayName = "PLAYER";
    public int level = 1;
    public int xp = 0;

    public long coins = 1_000_000;
    public int gems = 150;
    public long bet = 5_000;

    public int totalSpins;
    public long totalWon;
    public long biggestWin;

    public string createdUtc = "";
    public string lastWheelUtc = "";
    public string lastSeenUtc = "";

    public int lastDayIndex = -1;
    public int loginStreak;

    // Quests
    public List<QuestProgress> dailyQuests = new List<QuestProgress>();
    public List<QuestProgress> weeklyQuests = new List<QuestProgress>();
    public int questDayIndex = -1;
    public int questWeekIndex = -1;

    // Inbox
    public List<MailEntry> mail = new List<MailEntry>();
    public int mailSerial;
    public int lastNewsDayIndex = -1;

    // Club
    public long clubPoints;
    public int clubTier;

    // Puzzle — pieceMask is a bitfield over the current puzzle's cells.
    // 32 bits covers 3x3 and 4x4; a larger board needs a long or an array.
    public int currentPuzzle;
    public int pieceMask;
    public int bankedPieces;   // earned while the board sat full, applied on claim
    public List<string> completedPuzzles = new List<string>();

    // Cards — index-aligned to CardDef.id, so it stays a flat int array.
    public int[] cardCounts = new int[0];
    public List<int> completedSets = new List<int>();
    public int cardPacks;
}
