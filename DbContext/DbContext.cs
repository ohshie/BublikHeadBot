using Microsoft.EntityFrameworkCore;

namespace BublikHeadBot;

public class BotDbContext : DbContext
{
    public DbSet<BotUser> BotUsers { get; set; }
    public DbSet<Habit> Habits { get; set; }
    public DbSet<Agreement> Agreements { get; set; }
    public DbSet<Boyan> Boyans { get; set; }

    private string dbConnectionstring = Environment.GetEnvironmentVariable("DbConnectionString");
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql("Host=129.152.25.111;Database=bublikhead;Username=postgres;Password=CkVDuHj^C#7f");
    }
}