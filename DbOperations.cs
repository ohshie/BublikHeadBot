using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bots.Types;
using Message = Telegram.Bot.Types.Message;

namespace BublikHeadBot;

public class DbOperations
{
    public async Task<bool> RegisterUser(long telegramId, string telegramName)
    {
        using (BotDbContext dbContext = new BotDbContext())
        {
            BotUser botUser = await dbContext.BotUsers.FindAsync(telegramId);

            if (botUser == null)
            {
                dbContext.BotUsers.Add(new BotUser()
                {
                    Id = telegramId,
                    Username = telegramName
                });

                await dbContext.SaveChangesAsync();
                
                BotClient.UsersList = dbContext.BotUsers.ToList();
                
                return true;
            }

            return false;
        }
    }

    public async Task<BotUser> FetchUser(long telegramId)
    {
        using (BotDbContext dbContext = new BotDbContext())
        {
            BotUser user = await dbContext.BotUsers
                .Include(bu => bu.Habits)
                .ThenInclude(h => h.Agreements)
                .FirstOrDefaultAsync(bu => bu.Id == telegramId);
            
            if (user != null)
            {
                return user;
            }
        }

        return null;
    }

    public async Task<bool> ReserCounterInDb(BotUser userInChat)
    {
        BotUser userInDb = await FetchUser(userInChat.Id);
        
        using (BotDbContext dbContext = new BotDbContext())
        {
            userInDb.MessagesCounter = 0;
            dbContext.BotUsers.Update(userInDb);
            await dbContext.SaveChangesAsync();
            return true;
        }
    }

    public async Task<bool> CreateNewHabit(long telegramId, string habitName)
    {
        BotUser userInDb = await FetchUser(telegramId);
        if (userInDb != null)
        {
            using (BotDbContext dbContext = new BotDbContext())
            {
                if (!userInDb.Habits.Any())
                {
                    userInDb.Habits.Add(new Habit()
                    {
                        HabitName = habitName,
                        User = userInDb,
                        UserId = userInDb.Id,
                        Agreements = new List<Agreement>(),
                        ApprovalPending = true,
                        CreatedAt = DateTime.Now.ToUniversalTime()
                    });

                    userInDb.MessagesCounter = 0;
                    dbContext.UpdateRange(userInDb);
                    
                    await dbContext.SaveChangesAsync();
                    return true;
                }
            }
        }
        return false;
    }

    public async Task AddApprovalMessageIdToTask(long userId, long messageId)
    {
        using (BotDbContext dbContext = new BotDbContext())
        {
            Habit newHabit = dbContext.Habits.First(h => h.UserId == userId);
            if (newHabit != null)
            {
                newHabit.MessageIdForApproval = messageId;
                dbContext.Update(newHabit);
                await dbContext.SaveChangesAsync();
            }
        }
    }

    public async Task MarkApprovalsAsDone(Habit habit)
    {
        using (BotDbContext dbContext = new BotDbContext())
        {
            habit.ApprovalPending = false;
            dbContext.RemoveRange(habit.Agreements);
            dbContext.Update(habit);
            await dbContext.SaveChangesAsync();
        }
    }
    
    public async Task<bool> RemoveHabit(long telegramId)
    {
        BotUser userInDb = await FetchUser(telegramId);
        Habit userHabit = userInDb.Habits.FirstOrDefault();

        if (userInDb != null && userHabit != null)
        {
            using (BotDbContext dbContext = new BotDbContext())
            {
                dbContext.Habits.Remove(userHabit);
                await dbContext.SaveChangesAsync();
            }

            return true;
        }

        return false;
    }

    public async Task MarkHabitAsPendingConfirmation(long telegramId, long messageId)
    {
        BotUser userInDb = await FetchUser(telegramId);
        Habit userHabit = userInDb.Habits.FirstOrDefault();

        using (BotDbContext dbContext = new BotDbContext())
        {
            userHabit.MessageIdForConfirmation = messageId;
            userHabit.CompleteConfirmationPending = true;
            dbContext.Update(userHabit);
            await dbContext.SaveChangesAsync();
        }
    }
    
    public async Task<Habit> FetchMarkedHabit(int? messageId)
    {
        using (BotDbContext dbContext = new())
        {
            Habit? markedHabit = await dbContext.Habits
                .Include(h => h.Agreements)
                .ThenInclude(ha => ha.AgreedByUser)
                .Include(h => h.User)
                .FirstOrDefaultAsync(h => h.MessageIdForApproval == messageId || h.MessageIdForConfirmation == messageId);

            if (markedHabit != null)
            {
                return markedHabit;
            }
        }
        return null;
    }
    
    public async Task<List<Habit>> FetchListOfAllHabits()
    {
        using (BotDbContext dbContext = new BotDbContext())
        {
            List<Habit> listOfHabits = dbContext.Habits.
                Include(h => h.Agreements).
                ToList();
            
            if (listOfHabits.Any())
            {
                return listOfHabits;
            }

            return null;
        }
    }
    
    public async Task<Habit> AddAgreementToHabit(Habit habitToApprove, BotUser userWhoAgreed)
    {
        using (BotDbContext dbContext = new BotDbContext())
        {
            Agreement agreement = new Agreement()
                {
                    HabitId = habitToApprove.Id,
                    AgreedByUserId = userWhoAgreed.Id,
                };

                if (habitToApprove.Agreements == null)
                {
                    habitToApprove.Agreements = new List<Agreement>();
                }
                
                dbContext.Agreements.Add(agreement);
                dbContext.Update(habitToApprove);
                await dbContext.SaveChangesAsync();
                
                dbContext.Entry(habitToApprove).Reference(hta => hta.User).Load();
                
                return habitToApprove;
        }
    }

    public async Task CreateBoyanCheck(Message boyanMarkMessage, long botMessageId)
    {
        BotUser userInDb = await FetchUser(boyanMarkMessage.ReplyToMessage.From.Id);
        if (userInDb != null)
        {
            using (BotDbContext dbContext = new())
            {
                dbContext.Boyans.Add(new Boyan
                {
                    BoyanConfirmationPending = true,
                    User = userInDb,
                    UserId = userInDb.Id,
                    MessageIdForConfirmation = botMessageId,
                    BoyanMessageId = boyanMarkMessage.ReplyToMessage.MessageId
                });

                await dbContext.SaveChangesAsync();
            }
        }
        
    }

    public async Task<List<BotUser>> FetchAllUsers()
    {
        using (BotDbContext dbContext = new BotDbContext())
        {
            List<BotUser> usersList = dbContext.BotUsers.Include(bu => bu.Habits).ToList();
            return usersList;
        }
    }

    public async Task AddPoints(BotUser user)
    {
        using (BotDbContext dbContext = new BotDbContext())
        {
            user.Points += 1;
            dbContext.Update(user);
            await dbContext.SaveChangesAsync();
        }
    }
}