using System;

[Serializable]
public class MailEntry
{
    public string id;
    public string title;
    public string body;
    public string sender;
    public string sentUtc;
    public int expiryDays;   // 0 = never expires
    public Reward reward;
    public bool read;
    public bool claimed;

    public bool HasReward => !reward.IsEmpty && !claimed;
}
