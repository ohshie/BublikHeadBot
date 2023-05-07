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
            
            case <= AmountOfAgreementsNeeded:
                
                if (await DoubleVoteProtectionFail(boyan)) return;
                
                await DbOperations.AddAgreementToBoyan(boyan, Message.From.Id);
                BotUser user = await DbOperations.FetchUser(boyan.UserId);
                
                if (user != null)
                {
                    await DbOperations.RemovePoints(user);
                    await DbOperations.RemoveBoyan(user.Id);
                    await BotClient.SendTextMessageAsync(chatId:Message.Chat.Id,text:$"" +
                        $"Вирішено, це був баян. Ну {boyan.UserName} довбоеб і отримує - рейтинг.\n"+
                        "А я все ще чекаю адекватних аргументів чому російська в Україні це добре...",
                        replyToMessageId:boyan.BoyanMessageId);
                    break;
                }
                
                await DbOperations.RemoveBoyan(boyan.UserId);
                await BotClient.SendTextMessageAsync(chatId:Message.Chat.Id,text:$"" +
                        $"Вирішено, це був баян. Але {boyan.UserName} не бере участі в грі, тож забрати рейтинг у нього не можна, пропоную його просто обісцяти.\n"+
                        "А я все ще чекаю адекватних аргументів чому російська в Україні це добре...",
                        replyToMessageId:boyan.BoyanMessageId);
                break;
        }
    }
    
    internal async Task ProcessAgreementOnHabit(Habit habit)
    {
        AgreeingUser = habit.User;
        HabitInQuestion = habit;
        
        if (habit.CompleteConfirmationPending) await ProcessConfirmations();

        if (habit.ApprovalPending) await ProcessApprovals();
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
            case <= AmountOfAgreementsNeeded:
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
            
            case <= AmountOfAgreementsNeeded:
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
    
    private async Task<bool> DoubleVoteProtectionFail(Boyan boyan)
    {
        var approval = boyan.Agreements.Any(ha => ha.AgreedByUser == AgreeingUser);

        if (approval)
        {
            await SendBotMessage("Це що за корупційна схема? Два рази підтверджувати не можна. Чекай сбу");
            return true;
        }
        
        return false;
    }
}