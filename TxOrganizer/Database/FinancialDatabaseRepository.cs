using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using TxOrganizer.DTO;

namespace TxOrganizer.Database
{
    public class FinancialDatabaseRepository
    {
        private readonly AppDbContext _dbContext;

        public FinancialDatabaseRepository(AppDbContext dbContext)
        {
            this._dbContext = dbContext;
        }

        public void ImportTransactions(IEnumerable<Transaction> transactions)
        {
            var lastDate = ReadLastDate();

            AnsiConsole.WriteLine($"Last tx date in the database: {lastDate}");
            
            _dbContext.Transactions.RemoveRange(_dbContext.Transactions);
            _dbContext.SaveChanges();
            
            AnsiConsole.WriteLine($"Number of transactions in the DB: {_dbContext.Transactions.Count()}");
            
            _dbContext.Transactions.AddRange(transactions);
            _dbContext.SaveChanges();
            
            AnsiConsole.WriteLine("Transactions imported successfully");

        }
        
        public async Task<List<Transaction>> ReadAllTransactions()
        {

            return await _dbContext.Transactions.ToListAsync();
        }

        private DateTime? ReadLastDate()
        {
            return _dbContext.Transactions.Max(t => (DateTime?)t.Date);
        }
    }
}