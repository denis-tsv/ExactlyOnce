using ExactlyOnce.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExactlyOnce.Db;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ProcessedMessage> ProcessedInboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("exactly_once");
        
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}