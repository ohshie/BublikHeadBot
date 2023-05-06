﻿using BublikHeadBot;

class Program
{
    static async Task Main(string[] Args)
    {
        using BotDbContext dbContext = new BotDbContext();
        {
            await dbContext.Database.EnsureCreatedAsync();
        }
        
        BotClient botClient = new BotClient();
        await botClient.BotOperations();
        
        Console.ReadLine();
        Environment.Exit(1);
    }
}

