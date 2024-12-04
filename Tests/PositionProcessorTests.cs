using TxOrganizer.DataSource;
using TxOrganizer.Processors;
using NUnit.Framework;
using TxOrganizer.Database;

namespace Tests
{
    [TestFixture]
    public class PositionProcessorTests
    {
        [Test]
        public async Task BuildPositions_ValidTransactions_ReturnsExpectedPositions()
        {
            // Arrange
            var dbContextFactory = new AppDbContextFactory();
            await using var dbContext = dbContextFactory.CreateDbContext(Array.Empty<string>());
            var coingeckoFetcher = new CoinGeckoPriceFetcher(dbContext);
            var settingsRepository = new SettingsRepository(dbContext);
            var processor = new PositionProcessor(coingeckoFetcher, settingsRepository);
            var repository = new FinancialDatabaseRepository(dbContext);
            var transactions = await repository.ReadAllTransactions();

            // Act
            var (positions, unmatchedSpends) = await processor.BuildPositions(transactions, null);
            var targetPosition = positions.First(x => x.Currency == "");
            var (arbPosition, deficit) = processor.ExtractArbitragePosition(targetPosition);

            // Assert
            Assert.That(positions.Count(), Is.EqualTo(2));
            Assert.That(unmatchedSpends, Is.Empty);
        }
    }
}