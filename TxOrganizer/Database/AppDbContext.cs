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
    public DbSet<EthHistoricalBalance> EthHistoricalBalances => Set<EthHistoricalBalance>();
    public DbSet<AddressLabel> AddressLabels => Set<AddressLabel>();
    
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
        
        modelBuilder.Entity<EthHistoricalBalance>()
            .HasKey(x => x.Id);
        modelBuilder.Entity<EthHistoricalBalance>()
            .HasIndex(x => new { x.Address, x.BlockNumber })
            .IsUnique();
        modelBuilder.Entity<EthHistoricalBalance>()
            .Property(o => o.Address)
            .IsRequired()
            .HasMaxLength(42); // Ethereum address length
        modelBuilder.Entity<EthHistoricalBalance>()
            .Property(o => o.TransactionHash)
            .HasMaxLength(66); // Transaction hash length
        modelBuilder.Entity<EthHistoricalBalance>()
            .Property(o => o.BalanceEth)
            .HasColumnType("decimal(28,18)"); // 18 decimal places for ETH
            
        modelBuilder.Entity<AddressLabel>()
            .HasKey(x => x.Address);
        modelBuilder.Entity<AddressLabel>()
            .Property(o => o.Address)
            .IsRequired()
            .HasMaxLength(42); // Ethereum address length
        modelBuilder.Entity<AddressLabel>()
            .Property(o => o.Label)
            .IsRequired()
            .HasMaxLength(200);
        modelBuilder.Entity<AddressLabel>()
            .Property(o => o.Category)
            .HasMaxLength(100);
        modelBuilder.Entity<AddressLabel>()
            .HasIndex(x => x.Category);
        modelBuilder.Entity<AddressLabel>()
            .HasIndex(x => x.Label);
    }
}