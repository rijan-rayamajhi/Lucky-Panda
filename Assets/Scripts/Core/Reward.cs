using System;
using System.Globalization;
using System.Text;

// Where a payout came from. Only Win is multiplied by the club coin bonus:
// the wheel applies its own separate multiplier, and quest/mail/card/puzzle
// payouts are fixed designed amounts. Multiplying a shop purchase would let
// club tier inflate money the player paid for.
public enum RewardSource { Win, Wheel, Quest, Mail, Shop, Cards, Puzzle, Club, Level, Dev, Piggy }

[Serializable]
public struct Reward
{
    public long coins;
    public int gems;
    public int xp;
    public int cardPacks;
    public int puzzlePieces;
    public int clubPoints;

    public bool IsEmpty =>
        coins == 0 && gems == 0 && xp == 0 &&
        cardPacks == 0 && puzzlePieces == 0 && clubPoints == 0;

    public static Reward Of(long coins = 0, int gems = 0, int xp = 0,
                            int cardPacks = 0, int puzzlePieces = 0, int clubPoints = 0)
    {
        return new Reward
        {
            coins = coins, gems = gems, xp = xp,
            cardPacks = cardPacks, puzzlePieces = puzzlePieces, clubPoints = clubPoints
        };
    }

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // Player-facing summary, used by the reward popup and by mail bodies.
    public string Describe()
    {
        var sb = new StringBuilder();
        if (coins > 0) Append(sb, coins.ToString("N0", Inv) + " COINS");
        if (gems > 0) Append(sb, gems.ToString(Inv) + (gems == 1 ? " GEM" : " GEMS"));
        if (cardPacks > 0) Append(sb, cardPacks.ToString(Inv) + (cardPacks == 1 ? " CARD PACK" : " CARD PACKS"));
        if (puzzlePieces > 0) Append(sb, puzzlePieces.ToString(Inv) + (puzzlePieces == 1 ? " PUZZLE PIECE" : " PUZZLE PIECES"));
        if (xp > 0) Append(sb, xp.ToString("N0", Inv) + " XP");
        if (clubPoints > 0) Append(sb, clubPoints.ToString("N0", Inv) + " CLUB PTS");
        return sb.Length == 0 ? "NOTHING" : sb.ToString();
    }

    static void Append(StringBuilder sb, string part)
    {
        if (sb.Length > 0) sb.Append("   ");
        sb.Append(part);
    }
}
