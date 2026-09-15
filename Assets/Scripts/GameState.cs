using System;
using UnityEngine;

// The only thing that mutates PlayerData. Every currency change goes through
// here so that stats, XP, the club multiplier, the save and the UI refresh all
// happen in one place instead of being re-implemented by each caller.
[DefaultExecutionOrder(-900)]
public class GameState : MonoBehaviour
{
    public static GameState I;

    public const string SaveKey = "luckypanda.save.v1";

    public PlayerData Data = new PlayerData();

    /// Raised after any change worth redrawing. LobbyUI and open panels listen.
    public event Action Changed;

    bool dirty;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    // Services subscribe to GameEvents here, before DailyService.Boot() raises
    // the first rollover. They are static rather than components because every
    // panel in the scene starts inactive, so component Awake would never run.
    void Start()
    {
        // Mail first: it is the delivery channel for club tier-ups and level
        // gifts, so it must be listening before anything can raise one.
        MailService.Init();
        ClubService.Init();
        QuestService.Init();
        PuzzleService.Init();
        CardService.Init();
        Notifications.Init();
        DailyService.Boot();
        NotifyChanged();
    }

    void OnDestroy()
    {
        if (I == this) I = null;
    }

    public int XpForNextLevel => Data.level * 1000;

    // ---- currency ------------------------------------------------------

    public void AddCoins(long amount, RewardSource src)
    {
        if (amount <= 0) return;
        if (src == RewardSource.Win)
            amount = (long)Math.Round(amount * (double)ClubService.CoinMultiplier);

        Data.coins += amount;
        AudioManager.PlayCoinGain();
        bool won = src == RewardSource.Win || src == RewardSource.Wheel;
        if (won)
        {
            Data.totalWon += amount;
            if (amount > Data.biggestWin) Data.biggestWin = amount;
        }

        Save();
        // Only actual winnings count as winnings. Raising this for quest, mail
        // and shop payouts would let a "win 50,000 coins" quest complete itself
        // out of its own reward.
        if (won) GameEvents.Raise(GameEventType.CoinsWon, amount);
        NotifyChanged();
    }

    public void AddGems(int amount, RewardSource src)
    {
        if (amount <= 0) return;
        Data.gems += amount;
        Save();
        NotifyChanged();
    }

    public bool TrySpendCoins(long amount)
    {
        if (amount <= 0) return true;
        if (Data.coins < amount) return false;
        Data.coins -= amount;
        Save();
        GameEvents.Raise(GameEventType.CoinsSpent, amount);
        NotifyChanged();
        return true;
    }

    public bool TrySpendGems(int amount)
    {
        if (amount <= 0) return true;
        if (Data.gems < amount) return false;
        Data.gems -= amount;
        Save();
        GameEvents.Raise(GameEventType.GemsSpent, amount);
        NotifyChanged();
        return true;
    }

    public void AddXp(int amount)
    {
        if (amount <= 0) return;

        Data.xp += amount;
        int gained = 0;
        while (Data.xp >= XpForNextLevel)
        {
            Data.xp -= XpForNextLevel;
            Data.level++;
            gained++;
        }

        Save();
        // Raised after the loop so a handler that grants rewards cannot
        // re-enter the level maths mid-way.
        for (int i = 0; i < gained; i++)
            GameEvents.Raise(GameEventType.LevelUp, Data.level);

        NotifyChanged();
    }

    /// Stats only — the slot game moves the coins itself via TrySpendCoins/AddCoins.
    public void RecordSpin(long bet, long won)
    {
        Data.totalSpins++;
        if (won > Data.biggestWin) Data.biggestWin = won;
        Save();
        GameEvents.Raise(GameEventType.SlotSpun, bet);
        AddXp(Math.Max(1, (int)(bet / 1000)));
    }

    public void NotifyChanged() => Changed?.Invoke();

    // ---- persistence ---------------------------------------------------

    public void Load()
    {
        var json = PlayerPrefs.GetString(SaveKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                Data = JsonUtility.FromJson<PlayerData>(json) ?? new PlayerData();
            }
            catch (Exception e)
            {
                // A corrupt save should cost the player their progress, not
                // lock them out of the game entirely.
                Debug.LogWarning("Save unreadable, starting fresh: " + e.Message);
                Data = new PlayerData();
            }
        }

        if (string.IsNullOrEmpty(Data.createdUtc))
            Data.createdUtc = DateTime.UtcNow.ToString("o");

        Sanitize();
        SaveNow();
    }

    // A hand-edited, half-written or older save must not put the game in an
    // impossible state — level 0 makes XpForNextLevel 0, which breaks levelling
    // and the profile's XP bar, and collections absent from a v1 save come back
    // null rather than empty.
    void Sanitize()
    {
        if (Data.level < 1) Data.level = 1;
        if (Data.xp < 0) Data.xp = 0;
        if (Data.coins < 0) Data.coins = 0;
        if (Data.gems < 0) Data.gems = 0;
        if (Data.bet < 1) Data.bet = 1;
        if (Data.totalSpins < 0) Data.totalSpins = 0;
        if (Data.totalWon < 0) Data.totalWon = 0;
        if (Data.biggestWin < 0) Data.biggestWin = 0;
        if (Data.clubPoints < 0) Data.clubPoints = 0;
        if (Data.clubTier < 0) Data.clubTier = 0;
        if (Data.cardPacks < 0) Data.cardPacks = 0;
        if (Data.currentPuzzle < 0) Data.currentPuzzle = 0;
        if (string.IsNullOrWhiteSpace(Data.displayName)) Data.displayName = "PLAYER";

        if (Data.dailyQuests == null) Data.dailyQuests = new System.Collections.Generic.List<QuestProgress>();
        if (Data.weeklyQuests == null) Data.weeklyQuests = new System.Collections.Generic.List<QuestProgress>();
        if (Data.mail == null) Data.mail = new System.Collections.Generic.List<MailEntry>();
        if (Data.completedPuzzles == null) Data.completedPuzzles = new System.Collections.Generic.List<string>();
        if (Data.completedSets == null) Data.completedSets = new System.Collections.Generic.List<int>();
        if (Data.cardCounts == null) Data.cardCounts = new int[0];

        Data.schemaVersion = 2;
    }

    /// Marks the save dirty; the write happens at the end of the frame so a
    /// burst of grants costs one PlayerPrefs flush rather than a dozen.
    public void Save() => dirty = true;

    public void SaveNow()
    {
        dirty = false;
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }

    void LateUpdate()
    {
        if (dirty) SaveNow();
    }

    void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
    void OnApplicationQuit() => SaveNow();
}
