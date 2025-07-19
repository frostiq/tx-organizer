using System.Numerics;
using Microsoft.EntityFrameworkCore;
using TxOrganizer.Database;
using TxOrganizer.DataSource;
using TxOrganizer.DTO;

namespace TxOrganizer.Processors;

public class EthHistoricalBalanceProcessor
{
    private readonly AppDbContext _dbContext;
    private readonly EtherscanApiService _etherscanApi;
    private static readonly BigInteger WeiPerEth = BigInteger.Pow(10, 18);
    
    public EthHistoricalBalanceProcessor(AppDbContext dbContext, EtherscanApiService etherscanApi)
    {
        _dbContext = dbContext;
        _etherscanApi = etherscanApi;
    }
    
    /// <summary>
    /// Converts Wei (BigInteger) to ETH (decimal) without losing precision
    /// </summary>
    private static decimal WeiToEth(BigInteger wei)
    {
        var ethPart = BigInteger.DivRem(wei, WeiPerEth, out var remainder);
        return (decimal)ethPart + (decimal)remainder / (decimal)WeiPerEth;
    }
    
    /// <summary>
    /// Downloads and stores historical ETH balance changes for a given address.
    /// This method fetches all transactions affecting the address and calculates balance at each point.
    /// </summary>
    /// <param name="address">Ethereum address to track</param>
    /// <param name="startBlock">Starting block number (optional, defaults to 0)</param>
    /// <param name="endBlock">Ending block number (optional, defaults to latest)</param>
    /// <param name="batchSize">Number of balance records to save in each batch (optional, defaults to 100)</param>
    public async Task ProcessHistoricalBalancesAsync(string address, long startBlock = 0, long? endBlock = null, int batchSize = 100)
    {
        if (string.IsNullOrEmpty(address) || !IsValidEthereumAddress(address))
        {
            throw new ArgumentException("Invalid Ethereum address", nameof(address));
        }
        
        address = address.ToLowerInvariant();
        
        // Check if we already have data for this address
        var lastProcessedBlock = await _dbContext.EthHistoricalBalances
            .Where(b => b.Address == address)
            .MaxAsync(b => (long?)b.BlockNumber) ?? (startBlock - 1);
        
        if (lastProcessedBlock >= startBlock)
        {
            startBlock = lastProcessedBlock + 1;
            Console.WriteLine($"Resuming from block {startBlock} for address {address}");
        }
        
        Console.WriteLine($"Fetching transactions for address {address} from block {startBlock}...");
        
        // Get all transactions affecting this address
        var transactions = await _etherscanApi.GetTransactionsAsync(address, startBlock, endBlock ?? 999999999);
        
        if (!transactions.Any())
        {
            Console.WriteLine("No transactions found for the specified address and block range.");
            return;
        }
        
        Console.WriteLine($"Found {transactions.Count} transactions. Processing balance changes...");
        
        // Group transactions by block to calculate balance at each block
        var transactionsByBlock = transactions
            .Where(tx => !tx.IsError) // Skip failed transactions
            .GroupBy(tx => tx.BlockNumber)
            .OrderBy(g => g.Key)
            .ToList();
        
        var balanceRecords = new List<EthHistoricalBalance>();
        var currentBalance = BigInteger.Zero;
        
        // Get starting balance if we're not starting from genesis
        if (startBlock > 0)
        {
            try
            {
                var startingBalanceEth = await _etherscanApi.GetBalanceAtBlockAsync(address, startBlock - 1);
                currentBalance = (BigInteger)(startingBalanceEth * (decimal)WeiPerEth);
                Console.WriteLine($"Starting balance at block {startBlock - 1}: {startingBalanceEth} ETH");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not fetch starting balance: {ex.Message}");
            }
        }
        
        foreach (var blockGroup in transactionsByBlock)
        {
            var blockNumber = blockGroup.Key;
            var blockTransactions = blockGroup.OrderBy(tx => tx.TransactionIndex).ToList();
            
            // Calculate balance change for this block
            var balanceChange = BigInteger.Zero;
            var lastTransaction = blockTransactions.Last();
            
            foreach (var tx in blockTransactions)
            {
                if (tx.To?.ToLowerInvariant() == address)
                {
                    // Incoming transaction
                    balanceChange += tx.Value;
                }
                else if (tx.From?.ToLowerInvariant() == address)
                {
                    // Outgoing transaction
                    balanceChange -= tx.Value;
                }
            }
            
            currentBalance += balanceChange;
            
            // Create balance record
            var balanceRecord = new EthHistoricalBalance
            {
                Address = address,
                BlockNumber = blockNumber,
                BlockTimestamp = lastTransaction.Timestamp,
                TransactionHash = lastTransaction.Hash,
                BalanceEth = WeiToEth(currentBalance), // Convert Wei to ETH with full precision
                CreatedAt = DateTime.UtcNow
            };
            
            balanceRecords.Add(balanceRecord);
            
            // Save in batches to avoid memory issues
            if (balanceRecords.Count >= batchSize)
            {
                await SaveBalanceRecordsAsync(balanceRecords);
                balanceRecords.Clear();
                Console.WriteLine($"Processed up to block {blockNumber}...");
            }
        }
        
        // Save remaining records
        if (balanceRecords.Any())
        {
            await SaveBalanceRecordsAsync(balanceRecords);
        }
        
        Console.WriteLine($"Completed processing historical balances for address {address}");
        Console.WriteLine($"Final balance: {WeiToEth(currentBalance)} ETH");
    }
    
    /// <summary>
    /// Gets the ETH balance for an address at a specific block number from the database
    /// </summary>
    public async Task<decimal?> GetBalanceAtBlockAsync(string address, long blockNumber)
    {
        address = address.ToLowerInvariant();
        
        var balance = await _dbContext.EthHistoricalBalances
            .Where(b => b.Address == address && b.BlockNumber <= blockNumber)
            .OrderByDescending(b => b.BlockNumber)
            .FirstOrDefaultAsync();
        
        return balance?.BalanceEth;
    }
    
    /// <summary>
    /// Gets balance history for an address within a date range
    /// </summary>
    public async Task<List<EthHistoricalBalance>> GetBalanceHistoryAsync(string address, DateTime? startDate = null, DateTime? endDate = null)
    {
        address = address.ToLowerInvariant();
        
        var query = _dbContext.EthHistoricalBalances
            .Where(b => b.Address == address);
        
        if (startDate.HasValue)
            query = query.Where(b => b.BlockTimestamp >= startDate.Value);
        
        if (endDate.HasValue)
            query = query.Where(b => b.BlockTimestamp <= endDate.Value);
        
        return await query
            .OrderBy(b => b.BlockNumber)
            .ToListAsync();
    }
    
    private async Task SaveBalanceRecordsAsync(List<EthHistoricalBalance> records)
    {
        try
        {
            // Use bulk insert for better performance
            await _dbContext.EthHistoricalBalances.AddRangeAsync(records);
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Handle duplicate key errors gracefully
            Console.WriteLine($"Warning: Some records were skipped due to duplicates: {ex.Message}");
            
            // Try to save records one by one to identify duplicates
            foreach (var record in records)
            {
                try
                {
                    var existing = await _dbContext.EthHistoricalBalances
                        .FirstOrDefaultAsync(b => b.Address == record.Address && b.BlockNumber == record.BlockNumber);
                    
                    if (existing == null)
                    {
                        _dbContext.EthHistoricalBalances.Add(record);
                        await _dbContext.SaveChangesAsync();
                    }
                }
                catch (Exception innerEx)
                {
                    Console.WriteLine($"Failed to save record for block {record.BlockNumber}: {innerEx.Message}");
                }
            }
        }
    }
    
    private static bool IsValidEthereumAddress(string address)
    {
        return address.Length == 42 && address.StartsWith("0x") && 
               address.Substring(2).All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }
}
