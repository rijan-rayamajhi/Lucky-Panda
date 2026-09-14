using System;
using UnityEngine;

public class GameState : MonoBehaviour
{
    public static GameState I;

    public const string SaveKey = "luckypanda.save.v1";

    public PlayerData Data = new PlayerData();

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    public int XpForNextLevel => Data.level * 1000;

    public void AddXp(int amount)
    {
        Data.xp += amount;
        while (Data.xp >= XpForNextLevel)
        {
            Data.xp -= XpForNextLevel;
            Data.level++;
        }
        Save();
    }

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
        {
            Data.createdUtc = DateTime.UtcNow.ToString("o");
            Save();
        }

        Sanitize();
    }

    // A hand-edited or half-written save must not put the game in an impossible
    // state — level 0 makes XpForNextLevel 0, which breaks levelling and the
    // profile's XP bar.
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
        if (string.IsNullOrWhiteSpace(Data.displayName)) Data.displayName = "PLAYER";
    }

    public void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }

    void OnApplicationPause(bool paused) { if (paused) Save(); }
    void OnApplicationQuit() => Save();
}
