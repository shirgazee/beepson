using BeepsOnBot;
using BeepsOnBot.Database;
using BeepsOnBot.Services.Messaging;
using BeepsOnBot.Services.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;


var dbPath = Environment.GetEnvironmentVariable(Constants.DbPathEnvVariable);
if (!string.IsNullOrWhiteSpace(dbPath) && !Directory.Exists(dbPath))
    Directory.CreateDirectory(dbPath);
// ReSharper disable once UseAwaitUsing
using (var dbContext = new DefaultDbContext())
{
    dbContext.Database.Migrate();
}

var tgBotClient = new TelegramBotClient(Environment.GetEnvironmentVariable(Constants.TokenEnvVariable)
                                        ?? throw new InvalidOperationException("Token not provided"));
using var cts = new CancellationTokenSource();
// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
tgBotClient.StartReceiving(
    HandleUpdateAsync,
    HandlePollingErrorAsync,
    new ReceiverOptions
    {
        AllowedUpdates = new[] {UpdateType.Message, UpdateType.CallbackQuery}
    },
    cts.Token
);

var me = await tgBotClient.GetMeAsync();
Console.WriteLine($"Start listening for @{me.Username}");

var host = new HostBuilder()
    .ConfigureHostConfiguration(configHost => { })
    .ConfigureServices((_, services) =>
    {
        services.AddHostedService(_ => new NotificationBackgroundService(tgBotClient));
    })
    .UseConsoleLifetime()
    .Build();

//run the host
host.Run();
cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    var messagingService = new MessagingService(botClient, cancellationToken);
    if (update.Type == UpdateType.Message)
    {
        if (update.Message!.Type != MessageType.Text)
            return;
        var chatId = update.Message.Chat.Id;
        var messageText = update.Message.Text;

        Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

        if (messageText != null)
            await messagingService.ProcessMessage(chatId, messageText);
    }

    else if (update.Type == UpdateType.CallbackQuery)
    {
        var query = update.CallbackQuery!;
        var chatId = query.Message!.Chat.Id;
        var messageText = query.Data;

        Console.WriteLine($"Received a callback '{messageText}' message in chat {chatId}.");

        if (messageText != null)
            try
            {
                await messagingService.ProcessCallback(chatId, query.Message!.MessageId, messageText);
            }
            catch (ApiRequestException e)
            {
                Console.WriteLine(e);
            }
    }
}

Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var errorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(errorMessage);
    return Task.CompletedTask;
}