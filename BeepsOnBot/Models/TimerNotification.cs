namespace BeepsOnBot.Models;

public class TimerNotification
{
    public long Id { get; private set; }
    
    public long ChatId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime NotifyAt { get; private set; }

    public string? Name { get; private set; }
    
    public string UserMessage { get; private set; }

    public TimerNotification(long chatId, DateTime notifyAt, string? name, string userMessage)
    {
        ChatId = chatId;
        CreatedAt = DateTime.UtcNow;
        NotifyAt = notifyAt;
        Name = name;
        UserMessage = userMessage;
    }
}