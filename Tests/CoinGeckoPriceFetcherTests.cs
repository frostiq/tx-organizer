using Microsoft.EntityFrameworkCore;

namespace Tests;

[TestFixture]
public class CoinGeckoPriceFetcherTests
{
    private AppDbContext _dbContext;

    [SetUp]
    public void Setup()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseInMemoryDatabase("TestDb");
        _dbContext = new AppDbContext(optionsBuilder.Options);
        _dbContext.Database.EnsureCreated();
        
        // Add test data to the CoinGeckoIds DbSet
        // _dbContext.CoinGeckoIds.Add(new CoinGeckoId { Symbol = "btc", CoinId = "bitcoin" });
        // _dbContext.CoinGeckoIds.Add(new CoinGeckoId { Symbol = "eth", CoinId = "ethereum" });
        // _dbContext.SaveChanges();
    }
    
    [Test]
    public async Task GetPricesAsync_ReturnsExpectedResult()
    {
        // Arrange
        var fetcher = new CoinGeckoPriceFetcher(_dbContext);
        var tokenSymbols = new List<string> { "BTC" };

        // Act
        var result = await fetcher.GetPricesAsync(tokenSymbols);

        // Assert
        Assert.That(result.ContainsKey("BTC"), Is.True);
        Assert.That(result["BTC"], Is.GreaterThan(0));
    }
}