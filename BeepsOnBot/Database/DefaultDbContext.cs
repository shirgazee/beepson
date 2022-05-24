using System.Text.Json;
using BeepsOnBot.Database.Infrastructure;
using BeepsOnBot.Models;
using Microsoft.EntityFrameworkCore;

namespace BeepsOnBot.Database;

public class DefaultDbContext : DbContext
{
    public DbSet<TimerNotification> TimerNotifications { get; set; } = null!;

    public DbSet<ChatPreferences> ChatPreferences { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var filename = Path.Join(Environment.GetEnvironmentVariable(Constants.DbPathEnvVariable), "beepson.sqlite");
        optionsBuilder.UseSqlite($"Filename={filename}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimerNotification>(x =>
        {
            x.HasKey(t => t.Id);
            x.Property(t => t.ChatId)
                .ValueGeneratedNever();

            x.HasIndex(t => new {t.ChatId, t.NotifyAt});
        });

        modelBuilder.Entity<ChatPreferences>(x =>
        {
            x.HasKey(c => c.ChatId);
            x.Property(c => c.ChatId)
                .ValueGeneratedNever();

            x.Property(p => p.LastMessages)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions) null!),
                    v => JsonSerializer.Deserialize<List<ChatMessage>>(v, (JsonSerializerOptions) null!)
                         ?? new List<ChatMessage>(),
                    new JsonValueComparer<List<ChatMessage>>());
        });
    }
}