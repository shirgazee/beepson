namespace BeepsOnBot.Models;

public class ChatPreferences
{
    public long ChatId { get; private set; }

    public string TimeZoneId { get; private set; }

    public List<ChatMessage> LastMessages { get; private set; }

    public ChatPreferences(long chatId, string timeZoneId)
    {
        ChatId = chatId;
        TimeZoneId = timeZoneId;
        LastMessages = new List<ChatMessage>();
    }

    public void UpdateTimeZone(TimeZoneInfo tz)
    {
        TimeZoneId = tz.Id;
    }

    public void UpdateLastMessages(TimerNotification lastNotification)
    {
        LastMessages.Add(new ChatMessage(lastNotification.UserMessage, lastNotification.CreatedAt));
        LastMessages = LastMessages.OrderByDescending(x => x.Sent)
            .Distinct()
            .Take(4)
            .ToList();
    }
}