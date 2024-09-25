using Microsoft.EntityFrameworkCore;
using TxOrganizer.DTO;

namespace TxOrganizer.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    public DbSet<CoinGeckoId> CoinGeckoIds => Set<CoinGeckoId>();
    
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Setting> Settings => Set<Setting>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CoinGeckoId>().HasData(
            new CoinGeckoId { Symbol = "BTC", CoinId = "bitcoin" }, 
            new CoinGeckoId { Symbol = "ETH", CoinId = "ethereum" });

        modelBuilder.Entity<Transaction>()
            .Property(o => o.Date)
            .HasConversion<DateTime>();

        modelBuilder.Entity<CoinGeckoId>()
            .HasKey(x => x.Symbol);
        modelBuilder.Entity<CoinGeckoId>()
            .Property(o => o.CoinId).IsRequired();
        
        modelBuilder.Entity<Setting>()
            .HasKey(x => new { x.Type, x.Key });
        modelBuilder.Entity<Setting>()
            .Property(o => o.Type)
            .HasConversion<string>(); // Store the enum as a string
    }
}