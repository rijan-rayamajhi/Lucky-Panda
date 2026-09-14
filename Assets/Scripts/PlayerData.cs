using System;

// Everything that must survive a restart lives here and nowhere else, so
// moving to a cloud backend later means swapping the storage in GameState
// rather than hunting fields across the project.
[Serializable]
public class PlayerData
{
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
}
