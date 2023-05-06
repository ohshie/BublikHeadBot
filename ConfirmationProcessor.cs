using Telegram.Bot;
using Telegram.Bot.Types;

namespace BublikHeadBot;

public class ConfirmationProcessor : BotActions
{
    private ITelegramBotClient BotClient { get; }
    private Message Message { get; }
    private DbOperations DbOperations { get; }
    private BotUser AgreeingUser { get; set; }
    private Habit HabitInQuestion { get; set; }
    
    private const int AmountOfAgreementsNeeded = 2;
    
    internal ConfirmationProcessor(ITelegramBotClient botClient, Message message) : base(botClient, message)
    {
        BotClient = botClient;
        Message = message;
        
        DbOperations = new DbOperations();
    }

    internal async Task ProcessAgreementOnHabit(Habit habit)
    {
        AgreeingUser = habit.User;
        HabitInQuestion = habit;
        
        if (habit.CompleteConfirmationPending) await ProcessConfirmations();

        if (habit.ApprovalPending) await ProcessApprovals();
    }

    internal async Task ProcessAgreementOnBoyan(Boyan boyan)
    {
        switch (boyan.Agreements.Count)
        {
            case 0:
                await DbOperations.AddAgreementToBoyan(boyan, Message.From.Id);
                await BotClient.SendTextMessageAsync(chatId:Message.Chat.Id,text:$"{Message.From.Username} підтвердив, що це боян\n" +
                                     $"Треба ще 1 і пакуємо його нахуй",
                                     replyToMessageId:boyan.BoyanMessageId);
                return;
            case 1:
                await DbOperations.AddAgreementToBoyan(boyan, Message.From.Id);
                await BotClient.SendTextMessageAsync(chatId:Message.Chat.Id,text:$"просто можна не бути малоросом" +
                    $"Треба ще 1 і пакуємо його нахуй",
                    replyToMessageId:boyan.BoyanMessageId);
                break;
        }
    }
    
    private async Task ProcessConfirmations()
    {
        switch (HabitInQuestion.Agreements.Count)
        {
            case 0:
                await DbOperations.AddAgreementToHabit(HabitInQuestion, AgreeingUser);
                await SendBotMessage(
                    $"{AgreeingUser.Username} підтвердив, що {HabitInQuestion.User.Username} закінчив своє завдання {HabitInQuestion.HabitName}.\n" +
                    $"Треба ще 1");
                return;
            case 1:
            {
                await DbOperations.AddPoints(HabitInQuestion.User); 
                await DbOperations.RemoveHabit(HabitInQuestion.User.Id);
                await SendBotMessage(
                    $"Вітаємо, {HabitInQuestion.User.Username} виконав завдання {HabitInQuestion.HabitName}");
                break;
            }
        }
    }
    
    private async Task ProcessApprovals()
    {
        switch (HabitInQuestion.Agreements.Count)
        {
            case 0:
            {
                await DbOperations.AddAgreementToHabit(HabitInQuestion, AgreeingUser);
                await SendBotMessage(
                    $"{AgreeingUser.Username} підтвердив, що {HabitInQuestion.HabitName} нормальна справа, схвалюємо.\n" +
                    "Треба ще 1");
                return;
            }
            
            case < AmountOfAgreementsNeeded:
            {
                if (await DoubleVoteProtectionFail(HabitInQuestion)) return;

                HabitInQuestion = await DbOperations.AddAgreementToHabit(HabitInQuestion, AgreeingUser);
                
                await SendBotMessage(
                    $"У {HabitInQuestion.User.Username} з'явилася нова звичка: {HabitInQuestion.HabitName}\n" +
                    $"Agreed by {PrintOutConfirmators()}");
                
                await DbOperations.MarkApprovalsAsDone(HabitInQuestion);
                break;
            }
        }
    }

    private string PrintOutConfirmators()
    {
        string confirmators = "";
        foreach (var agreement in HabitInQuestion.Agreements)
        {
            confirmators = confirmators + $"{agreement.AgreedByUser.Username}" + ", ";
        }

        return confirmators;
    }
    
    private async Task<bool> DoubleVoteProtectionFail(Habit habit)
    {
        var approval = habit.Agreements.Any(ha => ha.AgreedByUser == AgreeingUser);

        if (approval)
        {
            await SendBotMessage("Це що за корупційна схема? Два рази підтверджувати не можна. Чекай сбу");
            return true;
        }
        
        return false;
    }
}