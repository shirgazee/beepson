using System.Text;
using BeepsOnBot.Database;
using BeepsOnBot.Models;
using BeepsOnBot.Services.TextParser;
using BeepsOnBot.Services.Timer;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace BeepsOnBot.Services.Messaging;

public class MessagingService
{
    public async Task ProcessMessage(
        long chatId,
        string message,
        ITelegramBotClient botClient,
        CancellationToken ct)
    {
        var chatPreferences = await GetChatPreferences(chatId);
        if (chatPreferences == null)
        {
            await SendPreferencesRequestMessage(chatId, null, null, botClient, ct);
            return;
        }

        var tz = TimeZoneInfo.FindSystemTimeZoneById(chatPreferences.TimeZoneId);

        if (message.StartsWith('/'))
        {
            switch (message)
            {
                case "/settimezone":
                    await SendPreferencesRequestMessage(chatId, null, null, botClient, ct);
                    return;
                case "/timers":
                    await GetTimersMessage(chatPreferences, tz, botClient, ct);
                    return;
                case "/clear":
                    await ClearTimersMessage(chatPreferences, tz, botClient, ct);
                    return;
            }
        }
        else
        {
            await SetTimerMessage(chatPreferences, tz, message, botClient, ct);
            return;
        }

        await SendUnknownMessage(chatPreferences, botClient, ct);
    }

    public async Task ProcessCallback(
        long chatId,
        int messageId,
        string message,
        ITelegramBotClient botClient,
        CancellationToken ct)
    {
        if (int.TryParse(message, out var page))
        {
            await SendPreferencesRequestMessage(chatId,
                messageId,
                page,
                botClient,
                ct);
            return;
        }
        
        var tz = TimeZoneInfo.FindSystemTimeZoneById(message);

        var chatPreferences = await SetChatPreferences(chatId, tz, null);
        await botClient.SendTextMessageAsync(chatId,
            $"Your new timezone: {tz.DisplayName} 👍",
            replyMarkup: GetDefaultKeyboard(chatPreferences),
            cancellationToken: ct);

        if(!chatPreferences.LastMessages.Any()) 
            await SendOnboardingMessage(chatPreferences, botClient, ct);
    }

    private async Task<ChatPreferences?> GetChatPreferences(long chatId)
    {
        await using var dbContext = new DefaultDbContext();
        return await dbContext.ChatPreferences.FirstOrDefaultAsync(x => x.ChatId == chatId);
    }
    
    private async Task<ChatPreferences> SetChatPreferences(
        long chatId,
        TimeZoneInfo tz,
        TimerNotification? lastNotification)
    {
        await using var dbContext = new DefaultDbContext();
        var preferences = await dbContext.ChatPreferences.FirstOrDefaultAsync(x => x.ChatId == chatId);
        if (preferences == null)
        {
            preferences = new ChatPreferences(chatId, tz.Id);
            dbContext.ChatPreferences.Add(preferences);
        }
        else
        {
            preferences.UpdateTimeZone(tz);
        }
        
        if (lastNotification != null)
        {
            preferences.UpdateLastMessages(lastNotification);
        }
        
        await dbContext.SaveChangesAsync();
        return preferences;
    }

    private async Task SendPreferencesRequestMessage(
        long chatId,
        int? messageId,
        int? page,
        ITelegramBotClient botClient,
        CancellationToken ct)
    {
        var pageValue = page ?? 21; // page with UTC time
        const int pageCount = 8;

        var timezones = TimeZoneInfo.GetSystemTimeZones();
        var paginated = timezones
            .Select(x => new List<InlineKeyboardButton>(1)
            {
                InlineKeyboardButton.WithCallbackData(x.DisplayName, x.Id)
            })
            .Skip((pageValue - 1) * pageCount)
            .Take(pageCount)
            .ToList();
        paginated.Add(new List<InlineKeyboardButton>()
        {
            InlineKeyboardButton.WithCallbackData("⬅️",
                pageValue - 1 <= 0
                    ? pageValue.ToString()
                    : (pageValue - 1).ToString()),
            InlineKeyboardButton.WithCallbackData("➡️",
                pageValue * pageCount < timezones.Count
                    ? (pageValue + 1).ToString()
                    : pageValue.ToString()),
        });

        if (messageId != null)
        {
            await botClient.EditMessageReplyMarkupAsync(chatId, messageId.Value,
                replyMarkup: new InlineKeyboardMarkup(paginated),
                cancellationToken: ct);
            return;
        }

        await botClient.SendTextMessageAsync(chatId,
            "Please choose your timezone 🗺",
            replyMarkup: new InlineKeyboardMarkup(paginated),
            cancellationToken: ct);
    }


    private async Task GetTimersMessage(
        ChatPreferences chatPreferences,
        TimeZoneInfo tz,
        ITelegramBotClient botClient,
        CancellationToken ct)
    {
        List<TimerNotification> timers;
        await using var dbContext = new DefaultDbContext();
        {
            timers = await dbContext.TimerNotifications
                .Where(x => x.NotifyAt > DateTime.UtcNow)
                .OrderBy(x => x.NotifyAt)
                .ToListAsync(ct);
        }

        string message;
        if (!timers.Any())
            message = "You have no timers set 🍸";
        else
        {
            var sb = new StringBuilder("Incoming timers: \n");
            var now = DateTime.UtcNow;
            foreach (var timer in timers)
            {
                var notifyAt = TimeZoneInfo.ConvertTime(timer.NotifyAt, tz).ToLocalTime();
                var timerName = string.IsNullOrWhiteSpace(timer.Name) ? "" : $" #{timer.Name}";
                if (timer.NotifyAt.Date == now.Date)
                {
                    sb.AppendLine($"- {notifyAt:t}{timerName}");
                }
                else
                {
                    sb.AppendLine($"- {notifyAt:f}{timerName}");
                }
            }

            message = sb.ToString();
        }

        await botClient.SendTextMessageAsync(chatPreferences.ChatId,
            message,
            replyMarkup: GetDefaultKeyboard(chatPreferences),
            cancellationToken: ct);
    }

    private async Task ClearTimersMessage(
        ChatPreferences chatPreferences,
        TimeZoneInfo tz,
        ITelegramBotClient botClient,
        CancellationToken ct)
    {
        await using var dbContext = new DefaultDbContext();
        {
            await dbContext.Database.ExecuteSqlRawAsync("delete from TimerNotifications where ChatId = @p0",
                chatPreferences.ChatId);
        }
        
        await botClient.SendTextMessageAsync(chatPreferences.ChatId,
            "All timers removed! 🗑",
            replyMarkup: GetDefaultKeyboard(chatPreferences),
            cancellationToken: ct);
    }
    
    private async Task SetTimerMessage(
        ChatPreferences chatPreferences,
        TimeZoneInfo tz,
        string message,
        ITelegramBotClient botClient,
        CancellationToken ct)
    {
        var timerService = new TimerService();
        var notification = await timerService.SetTimer(chatPreferences.ChatId, tz, message);

        if (notification == null)
        {
            await SendUnknownMessage(chatPreferences, botClient, ct);
            return;
        }
        
        chatPreferences = await SetChatPreferences(chatPreferences.ChatId, tz, notification);

        await botClient.SendTextMessageAsync(chatPreferences.ChatId,
            $"Timer has been set on {TimeZoneInfo.ConvertTime(notification.NotifyAt, tz)}! ⏰ \n\n"
            + $"Your timezone is {tz.DisplayName}",
            replyMarkup: GetDefaultKeyboard(chatPreferences),
            cancellationToken: ct);
    }

    private static IReplyMarkup GetDefaultKeyboard(ChatPreferences chatPreferences)
    {
        var buttons = chatPreferences.LastMessages
            .Where(x => !string.IsNullOrWhiteSpace(x.Message))
            .OrderByDescending(x => x.Sent)
            .Select(x => new KeyboardButton(x.Message))
            .ToList();
        return new ReplyKeyboardMarkup(new []{buttons, new List<KeyboardButton>
        {
            "/timers",
            "/clear",
            "/settimezone"
        }});
    }

    private static async Task SendUnknownMessage(
        ChatPreferences chatPreferences,
        ITelegramBotClient botClient,
        CancellationToken ct)
    {
        await botClient.SendTextMessageAsync(chatPreferences.ChatId,
            "Could not parse your message 🤷‍♂️",
            replyMarkup: GetDefaultKeyboard(chatPreferences),
            cancellationToken: ct);
    }

    private async Task SendOnboardingMessage(
        ChatPreferences chatPreferences,
        ITelegramBotClient botClient,
        CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
        await botClient.SendAnimationAsync(chatPreferences.ChatId,
            animation: new InputOnlineFile(new Uri("https://media.giphy.com/media/ENagATV1Gr9eg/giphy.gif")),
            cancellationToken: ct);
        await Task.Delay(TimeSpan.FromSeconds(6), ct);
        var rnd = new Random();
        await botClient.SendTextMessageAsync(chatPreferences.ChatId,
            "Now you can set your timers! 🎉 \n"
            + "️Try these: \n"
            + $"- {Time12TextParser.PossibleValues[rnd.Next(Time12TextParser.PossibleValues.Length)]} \n"
            + $"- {Time24TextParser.PossibleValues[rnd.Next(Time24TextParser.PossibleValues.Length)]} \n"
            + $"- {TimeSpanTextParser.PossibleValues[rnd.Next(TimeSpanTextParser.PossibleValues.Length)]} \n",
            replyMarkup: GetDefaultKeyboard(chatPreferences),
            cancellationToken: ct);
    }
}