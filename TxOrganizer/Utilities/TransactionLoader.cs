using Spectre.Console;
using TxOrganizer.Database;
using TxOrganizer.DTO;

namespace TxOrganizer.Utilities;

public static class TransactionLoader
{
    /// <summary>
    /// Loads all transactions from the database with a status indicator
    /// </summary>
    /// <param name="repository">The financial database repository</param>
    /// <returns>List of all transactions</returns>
    public static async Task<List<Transaction>> ReadAllTransactionsAsync(FinancialDatabaseRepository repository)
    {
        return await AnsiConsole.Status()
            .StartAsync("Loading transactions from database...", _ => repository.ReadAllTransactions());
    }
}
