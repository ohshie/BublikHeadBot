using System.Text.RegularExpressions;
using Npgsql.Replication.TestDecoding;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace BublikHeadBot;

public class BotActions
{
    private ITelegramBotClient BotClient { get; }
    private Message Message { get; }
    private DbOperations DbOperations { get; }

    private string MessagesBeforeAlarm = Environment.GetEnvironmentVariable("MessagesBeforeAlarm");
    
    public BotActions(ITelegramBotClient botClient, Message message)
    {
        Message = message;
        BotClient = botClient;
        
        DbOperations = new DbOperations();
    }
    
    public async Task NewUserRegistration()
    {
        long telegramId = Message.From.Id;
        string telegramName = Message.From.Username;

        bool success = await DbOperations.RegisterUser(telegramId, telegramName);

        if (success)
        {
            await SendBotMessage("Вітаю, ви в грі!");
        }

        await SendBotMessage("Астанавитес, ви вже зареєстровані");
    }

    public async Task MessagesAfterTaskSetCounter()
    {
        BotUser user = BublikHeadBot.BotClient.UsersList.FirstOrDefault(lou => lou.Id == Message.From!.Id)!;
        
        BublikHeadBot.BotClient.UsersList.Remove(user);
        user.MessagesCounter++;
        
        if (user.Habits.Any(h => !h.ApprovalPending))
        {
            if (user.MessagesCounter >= int.Parse(MessagesBeforeAlarm))
            {
                Habit? userHabit = user.Habits.FirstOrDefault();
                await SendBotMessage($"Друже, тобі варто попрацювати над своїм завданням {userHabit?.HabitName}");

                bool success = await DbOperations.ReserCounterInDb(user);
                if (success)
                {
                    Console.WriteLine("updated db");
                    user.MessagesCounter = 0;
                }
            }
        }
                
        BublikHeadBot.BotClient.UsersList.Add(user);
        Console.WriteLine(user.MessagesCounter);
    }

    public async Task RegisterNewHabit()
    {
        string habitName = Message.Text.Substring("/new".Length).Trim();
        
        if (string.IsNullOrWhiteSpace(habitName))
        {
            await SendBotMessage("Будь ласка, введіть правильну назву звички після введення /newhabit");
            return;
        }

        if (habitName.StartsWith("@bublikheadbot"))
        {
            habitName = habitName.Substring("@bublikheadbot".Length).Trim();
            if (string.IsNullOrEmpty(habitName))
            {
                await SendBotMessage("Я так не розумію, пиши руками /new назву завдання.\n" +
                                     "...я хотів сказати чому не державною?!");
                return;
            }
        }
        
        bool success = await DbOperations.CreateNewHabit(Message.From.Id, habitName);
        if (success)
        {
            await UpdateCurrentUserList();
            
            Message botMessage = await SendBotMessage(
                $"{Message.From.Username} ставить собі нове завдання {habitName}.\n" +
                $"Дайте відповідь + на це повідомлення, якщо схвалюєте");
            
            await DbOperations.AddApprovalMessageIdToTask(Message.From.Id, botMessage.MessageId);
            return;
        }

        await SendBotMessage($"Вибачте, зараз тільки 1 завдання одночасно");
    }

    public async Task CompleteHabit()
    {
        BotUser botUser = await DbOperations.FetchUser(Message.From.Id);
        
        if (botUser.Habits != null)
        {
            Habit? habitToMarkComplete = botUser.Habits.FirstOrDefault(h => !h.ApprovalPending);
            if (habitToMarkComplete != null)
            {
                if (habitToMarkComplete.Agreements.Count < 1)
                {
                    Message botMessage = await SendBotMessage($"{botUser.Username} хоче покінчити зі своєю звичкою, але для цього потрібно 2 дозволи.\n" +
                                                              $"Якщо ви згодні, дайте відповідь на це повідомлення +");
                    await DbOperations.MarkHabitAsPendingConfirmation(Message.From.Id, botMessage.MessageId);
                    return;
                }

                if (habitToMarkComplete.Agreements.Count == 1)
                {
                    await SendBotMessage(
                        $"{botUser.Username} хоче покінчити зі своєю звичкою, але для цього потрібно ще 1 дозволи.\n" +
                        $"Якщо ви згодні, дайте відповідь на це повідомлення +");
                }
                return;
            }
        }
        
        await SendBotMessage("Ти не маєш на це морального права.\n" +
                             "У тебе немає завдань.\n");
    }

    protected async Task UpdateCurrentUserList()
    {
        List<BotUser> updatedBotUser = await DbOperations.FetchAllUsers();
        BublikHeadBot.BotClient.UsersList = updatedBotUser;
    }
    
    public async Task DropHabit()
    {
        bool success = await DbOperations.RemoveHabit(Message.From.Id);
        if (success)
        {
            await UpdateCurrentUserList();
            await SendBotMessage("Шкода, спробуй наступного разу, дiтлах");
            return;
        }

        await SendBotMessage("Деменція? у тебе немає завдань.\n" +
                             "а міг би сьогодні нічого тупого не написати");
    }

    public async Task PrintUsersRatings()
    {
        List<BotUser> usersList = await DbOperations.FetchAllUsers();
        if (usersList.All(ul => ul.Points == 0))
        {
            await SendBotMessage("Схоже що ні в кого немає жодного закритого завдання.\n" +
                           "дітлахи..");
            return;
        }
        string messageWithRating = "";
        int position = 1;

        usersList = usersList.OrderBy(bu => bu.Points).ToList();
        
        foreach (var user in usersList)
        {
            if (user.Points > 0)
            {
                messageWithRating = $"{messageWithRating}{position++}. {user.Username} з рейтингом {user.Points}.\n";
            }
        }

        SendBotMessage($"Поточний топ:\n" +
                       $"{messageWithRating}");
    }

    
    // operations when + received from chat.
    public async Task AgreementFromGroup()
    {
        var botMessageForAgreement = Message.ReplyToMessage?.MessageId;
        Habit habit = await DbOperations.FetchMarkedHabit(botMessageForAgreement);
        
        if (habit != null)
        {
            if (Message.From?.Id == habit.UserId)
            {
                await SendBotMessage("Ти не можеш підтвердити своє ж завдання, це нахрюк");
                return;
            }
            ConfirmationProcessor confirmationProcessor = new(BotClient, Message);
            
            await confirmationProcessor.ProcessAgreementOnHabit(habit);
        }
        
        string pattern = @"(боян|баян)";

        Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);

        if (regex.IsMatch(Message.Text) && Message.ReplyToMessage.MessageId != null)
        {
            ConfirmationProcessor confirmationProcessor = new(BotClient, Message);
            await confirmationProcessor.ProcessAgreementOnBoyan();
        }
    }
    
    protected async Task<Message> SendBotMessage(string messageContent)
    {
        Message message = await BotClient.SendTextMessageAsync(chatId:Message.Chat.Id,
            text:messageContent,
            replyToMessageId: Message.MessageId);
        return message;
    }
}