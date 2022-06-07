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
    private readonly ITelegramBotClient _botClient;
    private readonly CancellationToken _ct;

    public MessagingService(ITelegramBotClient botClient, CancellationToken ct)
    {
        _botClient = botClient;
        _ct = ct;
    }

    public async Task ProcessMessage(long chatId, string message)
    {
        var chatPreferences = await GetChatPreferences(chatId);
        if (chatPreferences == null)
        {
            await SendPreferencesRequestMessage(chatId, null, null);
            return;
        }

        var tz = TimeZoneInfo.FindSystemTimeZoneById(chatPreferences.TimeZoneId);

        if (message.StartsWith('/'))
        {
            switch (message)
            {
                case "/settimezone":
                    await SendPreferencesRequestMessage(chatId, null, null);
                    return;
                case "/timers":
                    await GetTimersMessage(chatPreferences, tz);
                    return;
                case "/clear":
                    await ClearTimersMessage(chatPreferences);
                    return;
            }
        }
        else
        {
            await SetTimerMessage(chatPreferences, tz, message);
            return;
        }

        await SendUnknownMessage(chatPreferences);
    }

    public async Task ProcessCallback(long chatId, int messageId, string message)
    {
        if (int.TryParse(message, out var page))
        {
            await SendPreferencesRequestMessage(chatId, messageId, page);
            return;
        }

        var tz = TimeZoneInfo.FindSystemTimeZoneById(message);

        var chatPreferences = await SetChatPreferences(chatId, tz, null);
        await _botClient.SendTextMessageAsync(chatId,
            $"Your new timezone: {tz.DisplayName} 👍",
            replyMarkup: GetDefaultKeyboard(chatPreferences),
            cancellationToken: _ct);

        if (!chatPreferences.LastMessages.Any())
            await SendOnboardingMessage(chatPreferences);
    }

    private async Task<ChatPreferences?> GetChatPreferences(long chatId)
    {
        await using var dbContext = new DefaultDbContext();
        return await dbContext.ChatPreferences
            .FirstOrDefaultAsync(x => x.ChatId == chatId, _ct);
    }

    private async Task<ChatPreferences> SetChatPreferences(
        long chatId,
        TimeZoneInfo tz,
        TimerNotification? lastNotification)
    {
        await using var dbContext = new DefaultDbContext();
        var preferences = await dbContext.ChatPreferences
            .FirstOrDefaultAsync(x => x.ChatId == chatId, _ct);
        if (preferences == null)
        {
            preferences = new ChatPreferences(chatId, tz.Id);
            dbContext.ChatPreferences.Add(preferences);
        }
        else
        {
            preferences.UpdateTimeZone(tz);
        }

        if (lastNotification != null) preferences.UpdateLastMessages(lastNotification);

        // ReSharper disable once MethodSupportsCancellation
        await dbContext.SaveChangesAsync();
        return preferences;
    }

    private async Task SendPreferencesRequestMessage(long chatId, int? messageId, int? page)
    {
        const int utcPageNumber = 21;
        var pageValue = page ?? utcPageNumber;
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
                    : pageValue.ToString())
        });

        if (messageId != null)
        {
            await _botClient.EditMessageReplyMarkupAsync(chatId,
                messageId.Value,
                new InlineKeyboardMarkup(paginated),
                _ct);
            return;
        }

        await _botClient.SendTextMessageAsync(chatId,
            "Please choose your timezone 🗺",
            replyMarkup: new InlineKeyboardMarkup(paginated),
            cancellationToken: _ct);
    }


    private async Task GetTimersMessage(ChatPreferences chatPreferences, TimeZoneInfo tz)
    {
        List<TimerNotification> timers;
        await using var dbContext = new DefaultDbContext();
        {
            timers = await dbContext.TimerNotifications
                .Where(x => x.NotifyAt > DateTime.UtcNow && x.ChatId == chatPreferences.ChatId)
                .OrderBy(x => x.NotifyAt)
                .ToListAsync(_ct);
        }

        string message;
        if (!timers.Any())
        {
            message = "You have no timers set 🍸";
        }
        else
        {
            var sb = new StringBuilder("Incoming timers: \n");
            var now = DateTime.UtcNow;
            foreach (var timer in timers)
            {
                var notifyAt = TimeZoneInfo.ConvertTime(timer.NotifyAt, tz).ToLocalTime();
                var timerName = string.IsNullOrWhiteSpace(timer.Name) ? "" : $" #{timer.Name}";
                if (timer.NotifyAt.Date == now.Date)
                    sb.AppendLine($"- {notifyAt:t}{timerName}");
                else
                    sb.AppendLine($"- {notifyAt:f}{timerName}");
            }

            message = sb.ToString();
        }

        await _botClient.SendTextMessageAsync(chatPreferences.ChatId,
            message,
            replyMarkup: GetDefaultKeyboard(chatPreferences),
            cancellationToken: _ct);
    }

    private async Task ClearTimersMessage(ChatPreferences chatPreferences)
    {
        await using var dbContext = new DefaultDbContext();
        {
            await dbContext.Database.ExecuteSqlRawAsync("delete from TimerNotifications where ChatId = @p0",
                chatPreferences.ChatId);
        }

        await _botClient.SendTextMessageAsync(chatPreferences.ChatId,
            "All timers removed! 🗑",
            replyMarkup: GetDefaultKeyboard(chatPreferences),
            cancellationToken: _ct);
    }

    private async Task SetTimerMessage(ChatPreferences chatPreferences, TimeZoneInfo tz, string message)
    {
        var timerService = new TimerService();
        var notification = await timerService.SetTimer(chatPreferences.ChatId, tz, message);

        if (notification == null)
        {
            await SendUnknownMessage(chatPreferences);
            return;
        }

        chatPreferences = await SetChatPreferences(chatPreferences.ChatId, tz, notification);

        await _botClient.SendTextMessageAsync(chatPreferences.ChatId,
            $"Timer has been set on {TimeZoneInfo.ConvertTime(notification.NotifyAt, tz)}! ⏰ \n\n"
            + $"Your timezone is {tz.DisplayName}",
            replyMarkup: GetDefaultKeyboard(chatPreferences),
            cancellationToken: _ct);
    }

    private static IReplyMarkup GetDefaultKeyboard(ChatPreferences chatPreferences)
    {
        var buttons = chatPreferences.LastMessages
            .Where(x => !string.IsNullOrWhiteSpace(x.Message))
            .OrderByDescending(x => x.Sent)
            .Select(x => new KeyboardButton(x.Message))
            .ToList();
        return new ReplyKeyboardMarkup(new[]
        {
            buttons, new List<KeyboardButton>
            {
                "/timers",
                "/clear",
                "/settimezone"
            }
        });
    }

    private async Task SendUnknownMessage(ChatPreferences chatPreferences)
    {
        await _botClient.SendTextMessageAsync(chatPreferences.ChatId,
            "Could not parse your message 🤷‍♂️",
            replyMarkup: GetDefaultKeyboard(chatPreferences),
            cancellationToken: _ct);
    }

    private async Task SendOnboardingMessage(ChatPreferences chatPreferences)
    {
        await _botClient.SendAnimationAsync(chatPreferences.ChatId,
            new InputOnlineFile(new Uri("https://media.giphy.com/media/ENagATV1Gr9eg/giphy.gif")),
            cancellationToken: _ct);
        var rnd = new Random();
        await _botClient.SendTextMessageAsync(chatPreferences.ChatId,
            "Now you can set your timers! 🎉 \n"
            + "️Try these: \n"
            + $"- {Time12TextParser.PossibleValues[rnd.Next(Time12TextParser.PossibleValues.Length)]} \n"
            + $"- {Time24TextParser.PossibleValues[rnd.Next(Time24TextParser.PossibleValues.Length)]} \n"
            + $"- {TimeSpanTextParser.PossibleValues[rnd.Next(TimeSpanTextParser.PossibleValues.Length)]} \n",
            replyMarkup: GetDefaultKeyboard(chatPreferences),
            cancellationToken: _ct);
    }
}