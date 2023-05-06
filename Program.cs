using BublikHeadBot;
using Microsoft.EntityFrameworkCore;

class Program
{
    static async Task Main(string[] Args)
    {
        using BotDbContext dbContext = new BotDbContext();
        {
            await dbContext.Database.MigrateAsync();
        }
        
        BotClient botClient = new BotClient();
        await botClient.BotOperations();
        
        Console.ReadLine();
        Environment.Exit(1);
    }
}

