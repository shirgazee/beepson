using BeepsOnBot.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

namespace BeepsOnBot.Services.Notification;

public class NotificationBackgroundService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    public NotificationBackgroundService(ITelegramBotClient botClient)
    {
        _botClient = botClient;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            await using (var dbContext = new DefaultDbContext())
            {
                var now = DateTime.UtcNow;
                var notifications = await dbContext.TimerNotifications
                    .Where(x => x.NotifyAt <= now)
                    .ToListAsync(stoppingToken);

                foreach (var notification in notifications)
                {
                    var text = string.IsNullOrWhiteSpace(notification.Name)
                        ? "⏰ Time is up!"
                        : $"⏰  Time is up for #{notification.Name}!";
                    await _botClient.SendTextMessageAsync(notification.ChatId,
                        text,
                        cancellationToken: stoppingToken);

                    dbContext.TimerNotifications.Remove(notification);
                }

                await dbContext.SaveChangesAsync(stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}