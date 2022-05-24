using BeepsOnBot.Database;
using BeepsOnBot.Models;
using BeepsOnBot.Services.TextParser;

namespace BeepsOnBot.Services.Timer;

public class TimerService
{
    public static readonly ITextParser[] TextParsers =
        {new Time12TextParser(), new Time24TextParser(), new TimeSpanTextParser()};

    public async Task<TimerNotification?> SetTimer(long chatId, TimeZoneInfo tz, string message)
    {
        foreach (var parser in TextParsers)
        {
            var notification = parser.Parse(chatId, message, tz);
            if (notification.Parsed)
            {
                await StoreNotification(notification.Result!);
                return notification.Result;
            }
        }

        return null;
    }

    private async Task StoreNotification(TimerNotification notification)
    {
        await using var dbContext = new DefaultDbContext();
        dbContext.TimerNotifications.Add(notification);
        await dbContext.SaveChangesAsync();
    }
}