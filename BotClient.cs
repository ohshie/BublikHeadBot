using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;


namespace BublikHeadBot;

public class BotClient
{
    private readonly DbOperations _dbOperations = new();
    
    private static readonly string? TelegramBotToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
    public static List<BotUser> UsersList = new();

    private TelegramBotClient _botClient = new TelegramBotClient(TelegramBotToken);

    public async Task BotOperations()
    {
        using CancellationTokenSource cts = new ();
        
        UsersList = await _dbOperations.FetchAllUsers();
        
        // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
        ReceiverOptions receiverOptions = new ()
        {
            AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );
        
        var me = await _botClient.GetMeAsync();

        Console.WriteLine(_botClient.Timeout);
        Console.WriteLine($"Start listening for @{me.Username}");
        Console.ReadLine();
        
        // Send cancellation request to stop bot
        cts.Cancel();
    }
    
async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    botClient.GetUpdatesAsync();
    // Only process Message updates: https://core.telegram.org/bots/api#message
    if (update.Message is not { } message)
        return;
    // Only process text messages
    if (message.Text is not { } messageText)
        return;

    if (message == null) return;

    var chatId = message.Chat.Id;
    var userId = message.From.Username;

    BotActions botActions = new BotActions(message: message,
                                           botClient: botClient);
    
    Console.WriteLine($"Received a '{messageText}' message in chat {chatId} from {userId}. {message.From.Id}.");
    
    if (message.Text.StartsWith("/register")) await botActions.NewUserRegistration();
    
    if (UsersList.Exists(ul => ul.Id == message.From.Id))
    {
        await botActions.MessagesAfterTaskSetCounter();
        
        if (message.Text.StartsWith("/new")) await botActions.RegisterNewHabit();
        
        if (message.Text.StartsWith("/complete")) await botActions.CompleteHabit();

        if (message.Text.StartsWith("+")) await botActions.AgreementFromGroup();

        if (message.Text.StartsWith("/drop")) await botActions.DropHabit();
        
        if (message.Text.StartsWith("/rating")) await botActions.PrintUsersRatings();

        if (message.Text.StartsWith("@bublikheadbot")) await botActions.AgreementFromGroup();
    }
}

Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}
}